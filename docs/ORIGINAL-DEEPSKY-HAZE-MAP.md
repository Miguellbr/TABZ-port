# Original TABZ DeepSky Haze — pipeline map

Source inspected: original TABZ Modded v5.1 build, `GAME_Data/resources.assets`, plus its original `Assembly-CSharp.dll`. The shader assets are also present in the v4 build; the inspected asset files were previously confirmed identical between the two builds.

This is the reverse-engineering reference for the mobile implementation. The current implementation status and its explicit limitations are documented in [`MobilePort/MOBILE-HAZE-IMPLEMENTATION.md`](../MobilePort/MOBILE-HAZE-IMPLEMENTATION.md).

## 1. Shader assets

| Shader | Role |
|---|---|
| `Hidden/DS_Haze` | Main camera haze/fog pipeline: depth-aware radiance, blur, temporal reconstruction and final scene composite |
| `Hidden/DS_HazeLightVolume` | Local volumetric lights; separate ray-marched light-volume pass |
| `Hidden/Post FX/Fog` | Separate, simpler depth-based fog effect; not the main DeepSky Haze pipeline |

The serialized v5.1 assets contain all three shaders. The pass mapping below is for `Hidden/DS_Haze`, asset path ID 155.

## 2. Main shader pass map

The pass names come from the ShaderLab export. The vertex/pixel program indices were cross-checked against Unity's serialized pass references and its DX11 program table. For this build, the relevant DX11 program types are GPU program types 15 (vertex SM4.0) and 17 (pixel SM4.0); types 10 and 12 are the DX9 SM3.0 variants and are not the Android target.

| Pass index | Original pass name | DX11 vertex program | DX11 pixel program | Purpose |
|---:|---|---:|---:|---|
| 0 | `DS_RENDERLIGHTSHAFTS` | 0 | 1 | Builds directional-light radiance/light shafts. Called from the directional-light command buffer when a direct light is assigned. |
| 1 | `DS_BILATERALBLUR_X` | 2 | 3 | Horizontal bilateral blur of the radiance target, using half-resolution depth. |
| 2 | `DS_BILATERALBLUR_Y` | 2 | 4 | Vertical bilateral blur of the radiance target. |
| 3 | `DS_TEMPORALUPSCALE` | 32 keyword variants (table indices 0, 5–35) | 32 keyword variants (36–67) | Main full-resolution composite/upscale. Applies the atmospheric contribution to the scene and optionally performs temporal reprojection. |
| 4 | `DS_DEPTHGRAB` | 2 | 68 | Captures depth for temporal history/rejection. |
| 5 | `DS_DEPTHDOWNSAMPLE` | 69 | 70 | Produces the half-resolution linear depth texture consumed by the bilateral blur. |

Pass 3 has 32 DX11 keyword combinations per stage. The keywords are combinations of:

- `DS_HAZE_TEMPORAL`
- `DS_HAZE_APPLY_FOG_RADIANCE`
- `DS_HAZE_APPLY_FOG_EXTINCTION`
- `DS_HAZE_APPLY_MIE`
- `DS_HAZE_APPLY_RAYLEIGH`

The serialized metadata contains both DX9 and DX11 variants, which is why each stage lists 64 serialized variants even though the DX11 table has 32 variants for that stage.

## 3. Actual camera render sequence

Recovered from `DS_HazeView.OnRenderImage`:

1. Allocate a color temporary and an `RFloat` depth temporary.
2. Run pass 5 to produce half-resolution depth; bind it as `_HalfResDepth`.
3. Run pass 1 then pass 2 on the per-frame radiance target.
4. If temporal reprojection is enabled, bind `_PrevAccumBuffer` and `_PrevDepthBuffer`.
5. Bind the filtered radiance texture as `_RadianceBuffer`.
6. Run pass 3 to composite/upscale the source scene into the destination. With temporal reprojection enabled, it writes both the scene result and the current radiance history.
7. Run pass 4 to update the previous-depth target.
8. Publish the selected radiance target as `_DS_RadianceBuffer` and release temporary targets.

Pass 0 is not the final fog composite. It is scheduled separately through a directional-light command buffer. Local `DS_HazeLightVolume` objects also render into the per-frame radiance target through their own command buffers.

## 4. DS_HazeView → DS_HazeContext parameter contract

The original C# source shows the exact named Unity globals populated from each blended `DS_HazeContextItem`.

| Unity shader global | Value sent by the original controller |
|---|---|
| `_DS_RBetaS` | `m_AirScatteringScale * (0.00116, 0.0027, 0.00662)` |
| `_DS_BetaParams` | `(m_HazeScatteringScale * 0.0021, m_HazeSecondaryScatteringRatio, m_FogScatteringScale, m_FogExtinctionScale * 0.01)` |
| `_DS_AirHazeParams` | `(m_AirDensityHeightFalloff, m_HazeDensityHeightFalloff, 0, m_HazeScatteringDirection)` |
| `_DS_FogParams` | `(m_FogStartDistance, m_FogDensityHeightFalloff, m_FogOpacity, m_FogScatteringDirection)` |
| `_DS_InitialDensityParams` | `(airHeightFactor, hazeHeightFactor, fogHeightFactor, 0)` |
| `_DS_FogAmbientLight` | Linear ambient fog colour; multiplied by direct-light intensity when a direct light is assigned |
| `_DS_FogDirectLight` | Linear direct fog-light colour; multiplied by direct-light intensity when a direct light is assigned |
| `_DS_LightDirection` | Negative direct-light forward vector; otherwise world up |
| `_DS_LightColour` | Direct-light linear colour × intensity; otherwise white |

For exponential height falloff, the three initial-density factors are calculated at the camera's world height:

`exp(-airFalloff * abs(cameraY))`, `exp(-hazeFalloff * abs(cameraY))`, and `exp(-fogFalloff * abs(cameraY))`.

When height falloff is disabled, all three factors are 1.

### Fog toggle behavior

The controller first sets `_DS_FogParams.x` (start distance) to 5, retaining the other three fog parameters. It replaces that vector with the context's actual fog parameters only when `TABZChat.fogToggle` is true. Therefore the original `fogoff` path does not remove the whole atmosphere; it moves the fog start distance to 5.

### Camera/material parameters

- `_SamplingParams = (m_GaussianDepthFalloff, m_UpsampleDepthThreshold * 0.01, m_TemporalRejectionScale, m_TemporalBlendFactor)`
- Original defaults: Gaussian depth falloff 500, upsample depth threshold 0.06 (sent as 0.0006), temporal rejection scale 0.1, temporal blend factor 0.25.
- `_InterleavedOffset` comes from a 64-entry offset sequence.
- `_PreviousViewProjMatrix` and `_PreviousInvViewProjMatrix` provide temporal reprojection.
- `_ViewportCorner`, `_ViewportRight`, and `_ViewportUp` are derived from camera transform, far clip, field of view and aspect ratio for the directional-light ray pass.

## 5. Context defaults and selection

The decompiled `DS_HazeContextItem` constructor declares these defaults:

| Field | Default |
|---|---:|
| Air scattering scale | 1 |
| Air density height falloff | 0.001 |
| Haze scattering scale | 1 |
| Haze density height falloff | 0.003 |
| Haze scattering direction | 0.8 |
| Haze secondary scattering ratio | 0.8 |
| Fog opacity | 1 |
| Fog scattering scale | 1 |
| Fog extinction scale | 1 |
| Fog density height falloff | 0.01 |
| Fog start distance | 0 |
| Fog scattering direction | 0.7 |
| Fog ambient/direct colours | White |

These are class defaults, not a claim that every TABZ zone uses them. At render time, `DS_HazeView` either uses its explicitly overridden context asset or asks `DS_HazeCore.GetRenderContextAtPosition(cameraPosition)` for the context at the camera's position. `DS_HazeCore` selects/blends the applicable zone context using its current time. `DayNightCycle` writes its current `time` into `DS_HazeCore.Time`.

## 6. Original defaults versus mobile-port choices

The original `DS_HazeView` defaults include:

- temporal reprojection enabled;
- local volumetrics enabled;
- haze, fog extinction and fog lighting applied to the skybox;
- air/Rayleigh skybox application disabled.

Those are the original PC effect defaults, not recommended Android defaults. For the low-end Android baseline, temporal history, directional shafts and local volumetric lights are candidates to omit or make optional. The first mobile pass now uses the recovered context data and a depth-aware geometry-extinction approximation. It does not yet reproduce skybox integration, the full scattering/radiance path, or temporal reconstruction; see the mobile implementation status document.

## 7. Original platform requirements\n\nThe original `DS_HazeView.CheckHasSystemSupport` disables the effect when image effects or render textures are unavailable, when shader model 3.0 is unavailable, or when `RFloat` render textures are unsupported. HDR cameras additionally require `ARGBHalf`. Forward rendering with MSAA is rejected because the effect uses floating-point render targets. This is another reason not to assume the original PC component can simply be enabled unchanged on every Android device.\n\n## 8. Named constant-buffer and texture mapping

The Unity serialized shader metadata stores the per-subprogram constant-buffer layouts. In that metadata, `m_Index` is the member's byte offset in its constant buffer. Dividing by 16 gives the corresponding HLSL `float4` register; scalar offsets within a register identify the component. The binding table identifies the constant-buffer slot (`b0`, `b1`, etc.).

### Main composite — `DS_TEMPORALUPSCALE` pixel shader

This is the important mapping for the actual atmosphere-to-scene composite.

| Constant buffer | Slot | Member | Byte offset | HLSL register |
|---|---:|---|---:|---|
| `$Globals` | b0 | `_DS_RBetaS` | 176 | `cb0[11]` |
| `$Globals` | b0 | `_DS_BetaParams` | 192 | `cb0[12]` |
| `$Globals` | b0 | `_DS_AirHazeParams` | 208 | `cb0[13]` |
| `$Globals` | b0 | `_DS_FogParams` | 224 | `cb0[14]` |
| `$Globals` | b0 | `_DS_InitialDensityParams` | 240 | `cb0[15]` |
| `$Globals` | b0 | `_DS_LightDirection` | 256 | `cb0[16]` |
| `$Globals` | b0 | `_DS_LightColour` | 272 | `cb0[17]` |
| `$Globals` | b0 | `_DS_FogAmbientLight` | 288 | `cb0[18]` |
| `$Globals` | b0 | `_DS_FogDirectLight` | 304 | `cb0[19]` |
| `$Globals` | b0 | `_HalfResDepth_TexelSize` | 352 | `cb0[22]` |
| `$Globals` | b0 | `_SamplingParams` | 368 | `cb0[23]` |
| `$Globals` | b0 | `_PreviousViewProjMatrix` | 448 | `cb0[28..31]` |
| `$Globals` | b0 | `_PreviousInvViewProjMatrix` | 512 | `cb0[32..35]` |
| `UnityPerCamera` | b1 | `_WorldSpaceCameraPos` | 64 | `cb1[4]` |
| `UnityPerCamera` | b1 | `_ProjectionParams` | 80 | `cb1[5]` |
| `UnityPerCamera` | b1 | `_ZBufferParams` | 112 | `cb1[7]` |

The temporal keyword variant adds `_SamplingParams` and the two previous-frame matrices. The non-temporal variant retains the atmospheric globals but does not need the history matrices.

Pixel-shader texture bindings for the composite:

| Texture slot | Texture |
|---:|---|
| t0 | `_CameraDepthTexture` |
| t1 | `_MainTex` (camera scene) |
| t2 | `_HalfResDepth` |
| t3 | `_RadianceBuffer` |
| t4 | `_PrevAccumBuffer` (temporal variant) |
| t5 | `_PrevDepthBuffer` (temporal variant) |

### Supporting passes

- **Bilateral blur X/Y pixel shaders:** `$Globals` b0 has `_MainTex_TexelSize` at byte offset 320 (`cb0[20]`) and `_SamplingParams` at 368 (`cb0[23]`). `UnityPerCamera` b1 has `_ZBufferParams` at 112 (`cb1[7]`). Texture slots are t0 `_MainTex` and t1 `_HalfResDepth`.
- **Directional light-shaft pixel shader:** `$Globals` b0 uses `_DS_BetaParams` at 192 (`cb0[12]`), `_DS_AirHazeParams` at 208 (`cb0[13]`), `_DS_LightDirection` at 256 (`cb0[16]`), and `_DS_LightColour` at 272 (`cb0[17]`). `UnityPerCamera` b1 contains camera position at 64 and Z-buffer parameters at 112. `UnityShadows` b2 carries cascade split spheres/radii and world-to-shadow matrices. Textures: t0 `_SamplingOffsets`, t1 `_CameraDepthTexture`, t2 `_ShadowCascades`.
- **Depth downsample pixel shader:** `$Globals` b0 has `_CameraDepthTexture_TexelSize` at byte offset 448 (`cb0[28]`); t0 is `_CameraDepthTexture`.
- **Directional-light-shaft vertex shader:** `$Globals` b0 has `_MainTex_TexelSize` at 320, `_SamplingOffsets_TexelSize` at 336, `_ViewportCorner` at 384, `_ViewportRight` at 400, `_ViewportUp` at 416, and `_InterleavedOffset` at 432. The vertex stage also binds `UnityPerCamera` b1, `UnityPerDraw` b2 and `UnityPerFrame` b3.

### Local volumetric-light shader

`Hidden/DS_HazeLightVolume` has a separate set of parameters. Its `$Globals` b0 includes:

| Member | Byte offset | HLSL register |
|---|---:|---|
| `_DS_HazeLightVolumeColour` | 176 | `cb0[11]` |
| `_DS_HazeLightVolumeScattering` | 192 | `cb0[12]` |
| `_DS_HazeLightVolumeParams0` | 208 | `cb0[13]` |
| `_DS_HazeCameraDirection` | 224 | `cb0[14]` |
| `_SamplingOffsets_TexelSize` | 240 | `cb0[15]` |
| `_DS_HazeSamplingParams` | 256 | `cb0[16]` |
| `_DS_HazeLightVolumeParams1` | 400 | `cb0[25]` |
| `_DS_HazeLightVolumeParams2` | 416 | `cb0[26]` |
| `_DS_HazeDensityParams` | 432 | `cb0[27]` |

Cookie/shadow variants use a matrix at byte offset 432 (`cb0[27..30]`) named `_DS_Haze_WorldToCookie` or `_DS_Haze_WorldToShadow`. The shader also binds `UnityPerCamera`, `UnityPerCameraRare`, and, for shadow variants, `UnityShadows`. Its texture inputs vary by keyword variant and include `_CameraDepthTexture`, `_SamplingOffsets`, `_DensityTexture`, `_LightTexture0`, `_LightTextureB0`, and `_ShadowMapTexture`.

### Separate legacy fog shader

For comparison, `Hidden/Post FX/Fog` uses `$Globals` b0 with `_FogColor` at byte offset 64 (`cb0[4]`), `_Density` at offset 80 (`cb0[5].x`), and `_Start` at offset 84 (`cb0[5].y`). It samples `_MainTex` at t0 and `_CameraDepthTexture` at t1. This confirms that it is a distinct, much simpler effect—not the main DeepSky haze composite.

## 9. What is and is not mapped

**Mapped:** original C# context fields to named Unity shader globals; DX11 vertex/pixel pairing; all six main shader pass responsibilities; the core composite's named constant-buffer slots and byte offsets; its temporal/non-temporal texture inputs; and the key supporting-pass and local-volumetric bindings.

**Completed in section 10:** serialized values for the main-menu context and all five gameplay haze zones, including time curves, priorities, blend ranges and fog colours. The mobile implementation has not yet been changed.

No values in the existing experimental `MobileDistanceFog` implementation were adjusted as part of this mapping.

## 10. Exact serialized values from the original v5.1 scene

These values were read directly from the AssetRipper-exported `TABZ_World.unity` YAML, not estimated from screenshots. The scene contains five `DS_HazeZone` components. Their GameObject names, priorities and blend ranges are:

| Zone GameObject | Priority | Blend range | Context items |
|---|---:|---:|---|
| `DS_HazeZone` (general map zone) | 0 | 0.1 | MyDay, MyDay_Copy, MyDay_Copy_Copy, Night |
| `Snow2` | 100 | 0.531 | MyDay, MyDay_Copy, MyDay_Copy_Copy, Night |
| `LightHouse` | 250 | 0.1 | MyDay, MyDay_Copy, MyDay_Copy_Copy, Night |
| `Snow` | 250 | 0.531 | MyDay, MyDay_Copy, MyDay_Copy_Copy, Night |
| `darknes` | 250 | 0.05 | MyDay only |

### Shared values in all gameplay zones

The following values are identical in every serialized gameplay context item:

| Field | Serialized value | Multiplier enum | Effective multiplier |
|---|---:|---:|---:|
| Air scattering scale | 1 | 3 = One | 1× |
| Air density height falloff | 0.001 | — | 0.001 |
| Haze scattering scale | 1 | 3 = One | 1× |
| Haze density height falloff | 0.003 | — | 0.003 |
| Haze scattering direction | 0.4 | — | 0.4 |
| Haze secondary scattering ratio | 0.8 | — | 0.8 |
| Fog opacity | 1 | — | 1 |
| Fog scattering scale | 0 | — | 0 |
| Fog start distance | 0 | — | 0 |
| Fog scattering direction | 0.7 | — | 0.7 |
| Fog light colour | `(1, 0.6653145, 0.44852942, 1)` | — | unchanged |

The multiplier enum is defined in `DS_HazeContextItem.cs`: `0=OneTenth`, `1=OneFifth`, `2=OneHalf`, `3=One`, `4=Two`, `5=Five`, `6=Ten`. Therefore the serialized multiplier value `3` is exactly 1×, not 3×.

### Zone-specific fog values

All values below are raw serialized values. The extinction value is still converted by the original controller using the shader's `* 0.01` scale. Thus 3 becomes 0.03, 5 becomes 0.05, 4.75 becomes 0.0475, and 8 becomes 0.08 in `_DS_BetaParams.w`.

| Zone | Context item | Fog extinction scale | Shader extinction component | Fog density height falloff | Fog ambient colour (RGBA) |
|---|---|---:|---:|---:|---|
| General `DS_HazeZone` | MyDay | 5 | 0.05 | 0.015 | (0.6911765, 0.93610543, 1, 1) |
| General `DS_HazeZone` | MyDay_Copy | 5 | 0.05 | 0.015 | (1, 0.7380324, 0.53676474, 1) |
| General `DS_HazeZone` | MyDay_Copy_Copy | 5 | 0.05 | 0.015 | (1, 0.42647058, 0.50953335, 1) |
| General `DS_HazeZone` | Night | 4.75 | 0.0475 | 0.0001 | (0.25, 0.25, 0.25, 1) |
| `Snow2` | MyDay | 3 | 0.03 | 0.0001 | (0.875, 0.9741379, 1, 1) |
| `Snow2` | MyDay_Copy | 3 | 0.03 | 0.0001 | (1, 0.92099386, 0.8602941, 1) |
| `Snow2` | MyDay_Copy_Copy | 3 | 0.03 | 0.0001 | (1, 0.78676474, 0.81764704, 1) |
| `Snow2` | Night | 4.75 | 0.0475 | 0.0001 | (0.4705882, 0.4705882, 0.4705882, 1) |
| `LightHouse` | MyDay | 5 | 0.05 | 0.0001 | (0.6911765, 0.93610543, 1, 1) |
| `LightHouse` | MyDay_Copy | 5 | 0.05 | 0.0001 | (1, 0.7380324, 0.53676474, 1) |
| `LightHouse` | MyDay_Copy_Copy | 5 | 0.05 | 0.0001 | (1, 0.42647058, 0.50953335, 1) |
| `LightHouse` | Night | 4.75 | 0.0475 | 0.0001 | (0.25, 0.25, 0.25, 1) |
| `Snow` | MyDay | 5 | 0.05 | 0.0001 | (0.875, 0.9741379, 1, 1) |
| `Snow` | MyDay_Copy | 5 | 0.05 | 0.0001 | (1, 0.92099386, 0.8602941, 1) |
| `Snow` | MyDay_Copy_Copy | 5 | 0.05 | 0.0001 | (1, 0.78676474, 0.81764704, 1) |
| `Snow` | Night | 4.75 | 0.0475 | 0.0001 | (0.4705882, 0.4705882, 0.4705882, 1) |
| `darknes` | MyDay | 8 | 0.08 | 0.0001 | (1, 0.11764705, 0.11764705, 1) |

Every gameplay context item has `m_FogOpacity=1`, `m_FogScatteringScale=0`, `m_FogStartDistance=0`, and `m_FogScatteringDirection=0.7`. The fog-light colour is the shared warm value shown above. The general map zone's daytime height falloff (`0.015`) is much higher than the special zones' (`0.0001`); this is an original scene distinction and must not be flattened to one guessed value.

### Time curves and exact keyframes

All four-context gameplay zones serialize the same curves. `DS_HazeContext.GetContextItemBlended` copies the first item as the baseline and then lerps each later item using that item's `AnimationCurve.Evaluate(time)`. Consequently, `MyDay` is the base item; its own curve is serialized but is not evaluated by that blending loop.

| Context item | Keyframe time | Weight | In/out tangent |
|---|---:|---:|---:|
| MyDay (base) | 0.15 | 0 | 0 |
| MyDay (base) | 0.5 | 1 | 0 |
| MyDay (base) | 0.85 | 0 | 0 |
| MyDay_Copy | 0 | 0 | 14.749998 |
| MyDay_Copy | 0.15 | 1 | 0 |
| MyDay_Copy | 0.35 | 0 | -0.024193546 |
| MyDay_Copy | 1 | 0 | 0.016614795 |
| MyDay_Copy_Copy | 0 | 0.012499999 | 0 |
| MyDay_Copy_Copy | 0.65000004 | 0 | -0.02970297 |
| MyDay_Copy_Copy | 0.8666687 | 0.97500074 | 0 |
| MyDay_Copy_Copy | 1 | 0 | -8.399995 |
| Night | 0 | 1 | 0.02848094 |
| Night | 0.1 | 0 | 0 |
| Night | 0.90000004 | 0.0000011436641 | -0.0000035189667 |
| Night | 1 | 1 | 0 |

The `darknes` zone contains only `MyDay`, so it has no serialized Night or transition context item of its own.

### Main-menu context (separate from gameplay)

The main menu has its own `DS_HazeZone` with priority 0 and blend range 0.1. Its only item is named `Morning` (the original YAML stores this name as UTF-8 hex `4d6f726e696e67`). Its values are:

- Air scattering scale 1, multiplier One; air height falloff 0.001.
- Haze scattering scale 1, multiplier One; haze height falloff 0.003; haze direction 0.4; secondary ratio 0.8.
- Fog opacity 1; fog scattering scale 2; fog extinction scale 8, multiplier One; fog height falloff 0.0001; start distance 0; scattering direction 0.7.
- Ambient colour `(0.36764705, 0.36764705, 0.36764705, 1)`.
- Fog-light colour `(1, 0.6653145, 0.44852942, 1)`.
- Its weight curve keyframes are (0.25, 0), (0.5, 1), (0.75, 0).

### Source and interpretation notes

- The values above are from the serialized `DS_HazeZone` MonoBehaviours in `TABZ_World.unity`, not class constructor defaults.
- The zone priority and blend range are serialized on the zone component. The actual blend weight also depends on the camera's position inside that zone; priority and spatial blending are handled by `DS_HazeCore`/`DS_HazeZone`.
- The time-curve weights are not labels for fixed hours. They are evaluated against the normalized day-cycle time supplied by the game's day/night system.
- The recovered values are now wired into the first mobile context-data implementation. The original multi-pass PC shader still requires a separate mobile-compatible implementation; the current low-cost geometry pass is explicitly an approximation.

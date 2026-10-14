# DeepSky Haze — DXBC/HLSL reconstruction notes

This document records findings from the decompiled DX11 programs extracted from the original TABZ v5 build. The HLSL files are decompiler output from compiled DXBC, not the original authored ShaderLab source. Register names such as `cb0[13]` are therefore preserved as generated; their semantic mapping is taken from the serialized Unity shader metadata documented in [ORIGINAL-DEEPSKY-HAZE-MAP.md](ORIGINAL-DEEPSKY-HAZE-MAP.md).

Extraction source: workflow run [38301772658](https://github.com/Miguellbr/TABZ-port/actions/runs/38301772658), artifact `tabz-decompiled-haze-hlsl`. The report records 452 extracted DXBC programs and zero extraction errors.

## 1. Main composite programs

The original `Hidden/DS_Haze` DX11 table maps pass 3 (`DS_TEMPORALUPSCALE`) pixel programs to indices 36–67.

Two representative programs were inspected:

- **Program 36** is the non-temporal form: one color output (`SV_Target0`), four texture inputs for camera depth, camera color, half-resolution depth and radiance.
- **Program 37** is the temporal form: it adds previous accumulated radiance and previous depth inputs, and writes a second output (`SV_Target1`) for the next history buffer.

This is direct evidence that temporal reconstruction is an optional variant of the same atmospheric composite, rather than a separate fog effect.

### Texture slots

| Slot | Non-temporal | Temporal |
|---|---|---|
| t0 | `_CameraDepthTexture` | `_CameraDepthTexture` |
| t1 | `_MainTex` (camera color) | `_MainTex` |
| t2 | `_HalfResDepth` | `_HalfResDepth` |
| t3 | `_RadianceBuffer` | `_RadianceBuffer` |
| t4 | — | `_PrevAccumBuffer` |
| t5 | — | `_PrevDepthBuffer` |

### Composite sequence observed in program 36

1. Read full-resolution camera depth and convert it to linear eye depth using Unity's `_ZBufferParams`.
2. Read four neighboring half-resolution depth samples and convert each to linear depth.
3. Choose the radiance sample corresponding to the closest depth among those four candidates. This is a depth-aware upsample decision, preventing radiance from leaking freely across foreground/background boundaries.
4. Read the camera scene color.
5. Reconstruct the view ray from the interpolated camera direction and linear depth.
6. Evaluate atmospheric scattering and extinction using the original parameter vectors.
7. Blend the atmospheric result with the selected radiance contribution and camera color.

Program 37 follows the same path, but first reprojects the current world position into the previous frame. It checks whether the previous-frame coordinate is inside the valid viewport, samples previous depth, estimates the distance between current and reprojected positions, and attenuates history using the temporal rejection scale. The previous accumulated radiance is then blended using the temporal blend factor. It writes the updated history separately from the final scene output.

## 2. Semantic constant-buffer mapping

For the main composite, the metadata mapping is:

| Register | Semantic parameter |
|---|---|
| `cb0[11]` | `_DS_RBetaS` |
| `cb0[12]` | `_DS_BetaParams` |
| `cb0[13]` | `_DS_AirHazeParams` |
| `cb0[14]` | `_DS_FogParams` |
| `cb0[15]` | `_DS_InitialDensityParams` |
| `cb0[16]` | `_DS_LightDirection` |
| `cb0[17]` | `_DS_LightColour` |
| `cb0[18]` | `_DS_FogAmbientLight` |
| `cb0[19]` | `_DS_FogDirectLight` |
| `cb0[22]` | `_HalfResDepth_TexelSize` |
| `cb0[23]` | `_SamplingParams` |
| `cb1[4]` | `_WorldSpaceCameraPos` |
| `cb1[5]` | `_ProjectionParams` |
| `cb1[7]` | `_ZBufferParams` |

The temporal variant additionally uses `cb0[28..35]` for the previous view-projection and inverse view-projection matrices.

## 3. Atmospheric math visible in the decompiled composite

The generated register code is verbose, but several operations are unambiguous:

- The Rayleigh phase term has the form `(1 - g²) / (4π(1 - g·μ)²)`, where the direction cosine is formed from the normalized view ray and `_DS_LightDirection`. The shader uses the constant approximately `1/(4π) = 0.07957747`.
- The Mie-like phase term uses the same normalized light/view direction relationship with the anisotropy parameter from the haze/fog context. The squared denominator is visible in the DXBC-derived expression.
- Height-dependent density is integrated along the ray. For nonzero falloff, the shader evaluates an exponential integral of the form `(1 - exp(-falloff * distance)) / falloff`; near-zero falloff takes the linear-distance branch to avoid division instability.
- Fog extinction is evaluated with base-2 exponentials. The constant `1.44269502` is `log2(e)`, so `exp2(-x * log2(e))` is the compiled form of `exp(-x)`.
- The resulting atmospheric in-scattering is combined with ambient/direct fog lighting and the radiance buffer; extinction controls how much of the scene contribution is attenuated.

These are observations from the compiled program and register mapping. They should not be treated as a fully symbolized, source-level reconstruction yet; in particular, the original shader's intermediate variable names and high-level function boundaries are not recoverable from DXBC alone.

## 4. Bilateral blur confirmation

The horizontal and vertical blur programs are distinct compiled kernels. Both sample the radiance texture and half-resolution depth around the current pixel. Their weights combine fixed spatial kernel coefficients with an exponential depth-difference term:

`weight = spatialWeight * exp(-samplingFalloff * abs(sampleDepth - centerDepth)^2)`

The horizontal pass offsets along the X axis; the vertical pass offsets along Y. The output is normalized by the sum of weights. This confirms that the original blur is depth-aware, not a conventional Gaussian blur.

## 5. Depth passes

- The depth-grab pixel program copies the sampled camera depth into the red channel.
- The depth-downsample pixel program's effective output is the maximum of two diagonal half-texel-offset raw-depth samples. The decompiler shows two additional fetches whose values do not feed the output. This is the compiled output behavior; do not replace it with an assumed average.

## 6. What this means for the Android port

The current `MobileDistanceFog.shader` is still only a one-pass geometry-extinction approximation. It does not implement the original composite, radiance generation, bilateral blur, or temporal path.

The next implementation should be staged:

1. Build a non-temporal atmospheric composite first, retaining the original parameter meanings and depth-aware upsample behavior.
2. Port the radiance-generation path and its inputs; without a valid radiance buffer, the composite alone cannot reproduce the original lighting.
3. Add the two depth-aware blur passes.
4. Compare against the original scene using the same context values and camera position.
5. Only then consider temporal history and local volumetric lights.

Do not enable a partial multi-pass effect by default until every required render target is initialized and populated. A missing radiance or depth target can produce a full-screen tint or other misleading output.

## 7. Platform caveat

The recovered programs are DX11 shader-model 4.0 variants. They expose the algorithm and parameter contract, but they are not directly usable as Android GLES/Vulkan shader binaries. The Android implementation must be re-authored for the Unity mobile graphics backends and validated on the target device. The decompiled HLSL is a reference for behavior, not a drop-in mobile shader.


## 8. Directional radiance generation — pass 0 pixel program

The pass-0 pairing in the serialized shader map is DX11 vertex program 0 plus pixel program 1. I reopened the extracted program-1-kShaderGpuProgramDX11PixelSM40.hlsl from the workflow artifact and inspected its texture/register usage. This fills in the most important missing stage before the main composite.

### Inputs and output

- t0: _SamplingOffsets, sampled using the interpolated screen coordinate. This supplies the per-pixel interleaved sampling offset.
- t1: _CameraDepthTexture, sampled at the screen UV to reconstruct the camera-ray endpoint in world space.
- t2: _ShadowCascades, accessed with a comparison sampler (SampleCmpLevelZero).
- b2: Unity shadow cascade data: four cascade sphere centers (cb2[0..3]), their squared radii (cb2[4]), and the world-to-shadow transforms (cb2[8..23]).
- b1: camera position and Z-buffer parameters used to reconstruct the view-space distance and world-space ray.
- b0: haze scattering parameters, light direction and light colour. The shader uses _DS_BetaParams.x, _DS_BetaParams.y, _DS_AirHazeParams.y/.w, _DS_LightDirection and _DS_LightColour.
- Output: SV_Target0 is RGB directional radiance; alpha is explicitly zero. This pass contributes radiance, it does not composite the final camera image.

### Work performed per pixel

1. Read the scene depth and reconstruct the world-space ray segment from the camera to the visible surface.
2. Use the camera's position and the world-space sample position to determine which of the four shadow cascades contains each sample. The shader tests squared distance against the four cascade radii and chooses the matching world-to-shadow transform.
3. March along the ray in regular 1/16 segment increments. The DXBC-derived HLSL contains 15 SampleCmpLevelZero calls against the cascaded shadow texture. Samples outside all four cascade spheres are treated as unshadowed rather than indexing an invalid cascade.
4. At each sample, evaluate the exponential height-density term exp(-hazeHeightFalloff * worldY) (the compiled form uses exp2(x * log2(e))), multiply by the shadow comparison result, and accumulate it along the ray.
5. Apply the haze scattering phase term. The code exposes the Henyey–Greenstein-like denominator through _DS_AirHazeParams.w and the light/view direction dot product. It multiplies the accumulated integral by _DS_BetaParams.x and _DS_BetaParams.y, then scales the result by _DS_LightColour.

The vertex program constructs the world-space ray from _ViewportCorner, _ViewportRight, and _ViewportUp, and also forwards screen coordinates for the depth and sampling-offset lookups. These vectors are populated by DS_HazeView from the camera's transform, field of view, aspect ratio and far clip distance.

### Consequence for the Android implementation

The original directional radiance is shadow-aware volumetric integration, not simply a sun-coloured gradient or a radial blur. The recovered `DS_HazeView` C# source shows the exact Built-in Render Pipeline scheduling:

- attach a command buffer to the directional light at `LightEvent.AfterShadowMap` and bind `BuiltinRenderTextureType.CurrentActive` as `_ShadowCascades`;
- attach the radiance blit to `LightEvent.AfterScreenspaceMask`, where the Unity shadow cascade constant buffer is available;
- let the shader select among the four cascade split spheres/radii and world-to-shadow matrices, then perform its 15 comparison samples.

This corrects the earlier assumption that the cascade data could not be obtained through the Built-in pipeline. It is not available as a reliable texture/matrix pair to an arbitrary `OnRenderImage` blit, but the original light-event command-buffer route is directly evidenced by the recovered source and is the correct route to reproduce. The Android implementation still needs to wire and validate that scheduling and the comparison-sampling shader on the target graphics backend.

## 9. Evidence and limits

The directional-pass observations above come from decompiler output for the DX11 SM4.0 pixel program paired with the original serialized pass table. They establish the texture bindings, cascade selection, repeated shadow comparisons and scattering accumulation, but they do not provide the original authored HLSL or guarantee identical behavior on GLES/Vulkan. In particular, the exact GPU cost on the target Android device still needs measurement.


## 10. Term-by-term reconstruction of the non-temporal composite (program 36)

The artifact's program-36 HLSL was re-opened and its register operations traced against the semantic mapping in `ORIGINAL-DEEPSKY-HAZE-MAP.md`. This resolves an ambiguity in the earlier high-level notes: air/Rayleigh, haze, and fog are separate contributions, with separate coefficients and phase parameters.

### Reconstructed ray quantities

- The full-resolution depth is linearized with `cb1[7]` (`_ZBufferParams`).
- Four half-resolution depth candidates are linearized and compared with the full-resolution depth. The radiance sample whose depth is closest is selected.
- The interpolated camera ray is scaled by the linear eye depth. Its world-space length is computed separately from its vertical displacement.
- The vertical density integrals use the ray's vertical displacement and total ray length, not one constant camera-height density for the whole segment.

For a density profile `rho(y) = rho0 * exp(-k * deltaY)`, the compiled integral has the stable form:

`I = rho0 * L * (1 - exp(-k * deltaY)) / (k * deltaY)`

with the linear limit `I = rho0 * L` when the denominator is near zero. Here `L` is the ray segment length and `deltaY` is its signed vertical displacement. The initial density `rho0` is supplied independently for air, haze, and fog by `_DS_InitialDensityParams.xyz`.

### Separate air, haze, and fog terms

| Term | DXBC register inputs | Reconstructed role |
|---|---|---|
| Air / Rayleigh | `_DS_RBetaS.rgb`, `_DS_InitialDensityParams.x`, `_DS_AirHazeParams.x` | RGB wavelength-dependent coefficient multiplied by the integrated air density; the composite applies the isotropic `1/(4π)` factor and light colour |
| Haze | `_DS_BetaParams.x`, `_DS_BetaParams.y`, `_DS_AirHazeParams.y`, `_DS_AirHazeParams.w` | Integrated haze density, Rayleigh-like phase using the haze anisotropy, scaled by haze strength and by `(1 - secondaryRatio)` in this composite term |
| Fog scattering | `_DS_BetaParams.z`, `_DS_FogParams.w`, `_DS_FogParams.z` | Separate phase term using fog anisotropy, scaled by fog-scattering strength and fog opacity |
| Fog extinction | `_DS_BetaParams.w`, `_DS_FogParams.x/y/z`, `_DS_InitialDensityParams.z` | Integrated fog density after start distance, converted to transmittance; opacity controls the blend toward fog lighting |

The air contribution is not controlled by `_DS_BetaParams.x`. It uses `_DS_RBetaS.rgb` directly. Conversely, `_DS_BetaParams.x` is the haze scattering scale (the C# controller supplies `hazeScale * 0.0021`). `_DS_BetaParams.y` is the secondary haze ratio; in the main composite the local haze phase term is multiplied by `(1 - y)`. The directional-radiance pass separately accumulates the primary shadowed haze contribution with `y`, so the two uses must not be collapsed into one scalar.

### Phase functions and lighting composition

The composite computes two phase functions from the normalized view ray and light direction:

- Haze phase: `(1 - g_haze²) / (4π * (1 - g_haze * μ)²)`, where `g_haze = _DS_AirHazeParams.w`.
- Fog phase: `(1 - g_fog²) / (4π * (1 - g_fog * μ)²)`, where `g_fog = _DS_FogParams.w`.

The air/Rayleigh contribution uses the isotropic `1/(4π)` factor with the RGB `_DS_RBetaS` coefficient. The compiled composite then forms the atmospheric base from the camera colour, selected radiance-buffer sample, air contribution and haze contribution. Separately it forms fog lighting from `_DS_FogAmbientLight + _DS_FogDirectLight * fogPhase`. The integrated fog extinction determines the interpolation between those two results. This is why a single `lerp(scene, _FogColor, fogAmount)` cannot reproduce the original composite.

### Directional radiance pass (program 1) versus composite

The pass-0 pixel shader uses the same haze anisotropy `_DS_AirHazeParams.w`, haze scale `_DS_BetaParams.x`, and secondary ratio `_DS_BetaParams.y`, but it performs a distinct operation:

1. It reconstructs the camera-to-surface ray segment.
2. It takes 15 samples at 1/16-segment intervals (the first sample begins at the camera and the final sample remains before the endpoint).
3. At each sample it evaluates `exp(-hazeFalloff * worldY)`, applies the selected directional-shadow-cascade comparison, and accumulates the result.
4. It applies the haze phase and the primary/secondary scale, then outputs directional radiance to the radiance target.

The main composite's haze term is therefore not a substitute for pass 0. Pass 0 creates shadow-aware directional radiance; pass 3 consumes the filtered radiance buffer and independently computes the remaining atmospheric terms.

### Corrections to earlier interpretation

- The earlier shorthand that described `_DS_RBetaS` as merely another haze coefficient was incorrect. It is the RGB air/Rayleigh coefficient.
- The earlier table's implication that `_DS_BetaParams.y` is only a generic secondary ratio was incomplete: in the main composite, the haze phase term is multiplied by `(1-y)`, while pass 0 accumulates the complementary primary contribution using `y`.
- The fog scattering term is not absent from the original pipeline: it is a distinct phase contribution in the composite. The mobile implementation currently does not calculate it.
- The existing mobile scalar `0.0021` is correctly the conversion factor for haze scale, not a replacement for the RGB `_DS_RBetaS` vector.
- The original camera-ray interpolation has now been recovered from both DXBC and the decompiled `DS_HazeView` C# source. The mobile implementation has been changed to use that same far-clip-scaled camera basis and UV-flip rule.

The original ray-basis implementation is in `MobileDistanceFog.cs` and `MobileDistanceFog.shader`; the source-contract workflow now checks its camera basis inputs and UV-flip behavior.

## 11. Camera-ray interpolation — vertex programs 0 and 32

The extracted DX11 vertex programs paired with both pass 0 (program 0) and the main composite (program 32) were inspected. Their ray-output logic is identical:

- `cb0[24]` maps to `_ViewportCorner`.
- `cb0[25]` maps to `_ViewportRight`.
- `cb0[26]` maps to `_ViewportUp`.
- `cb0[20].y < 0` controls the vertical UV flip.
- The interpolated ray output is `o3 = _ViewportCorner + _ViewportRight * u + _ViewportUp * v`, with `v` replaced by `1-v` when the source texture is vertically flipped.

The pixel programs then multiply this interpolated `TEXCOORD2` ray by linear eye depth. In pass 0, the resulting world-space position is camera position plus that scaled ray. This confirms that `TEXCOORD2` is a camera-depth ray basis (its forward component is the unit-depth reference), not a normalized direction vector to be multiplied by Euclidean distance.

The decompiled C# source in `haze-source/DeepSky.Haze.DS_HazeView.cs` from artifact `tabz-deepsky-haze-mapping` (workflow run `38304981439`) supplies the exact setter:

```csharp
float farClipPlane = m_Camera.farClipPlane;
float halfFov = m_Camera.fieldOfView * 0.5f;
float halfHeight = Mathf.Tan(halfFov * Mathf.Deg2Rad) * farClipPlane;
float halfWidth = halfHeight * m_Camera.aspect;
Vector3 forward = m_Transform.forward * farClipPlane;
Vector3 right = m_Transform.right * halfWidth;
Vector3 up = m_Transform.up * halfHeight;
_ViewportCorner = forward - right - up;
_ViewportRight = right * 2f;
_ViewportUp = up * 2f;
```

The vertex shader interpolates `corner + right*u + up*v`. It changes `v` to `1-v` when `_MainTex_TexelSize.y < 0`. The pixel shader multiplies this far-clip-sized ray by Linear01Depth; since `farClip * Linear01Depth = LinearEyeDepth`, the equivalent mobile world-space segment is `eyeDepth * (viewportRay / farClip)`.

`MobileDistanceFog.UpdateOriginalViewportRays()` now rebuilds these exact three vectors from the active camera's transform, FOV, aspect and far clip immediately before each blit. The shader uses the same linear interpolation and texture-Y flip, then derives world-ray direction and world-space segment length from that basis. This deliberately matches the original formula rather than supporting custom lens-shift/projection matrices that the original setter itself did not account for.

**Remaining validation:** the code-level formula is now recovered and wired. Unity compilation and a visual comparison on the Android device are still required; source-contract success alone cannot establish render-pixel parity.


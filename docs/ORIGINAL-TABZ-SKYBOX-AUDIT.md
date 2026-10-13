# Original TABZ skybox audit

## Scope

This audit treats the skybox and ambient environment independently from DeepSky Haze. It uses the original v5.1 AssetRipper-exported scene/material YAML and the original Skybox/Gradient Skybox DX11 SM4.0 vertex/pixel programs extracted from the Unity shader object.

Evidence:
- Original shader extraction/decompilation: workflow run [38479670451](https://github.com/Miguellbr/TABZ-port/actions/runs/38479670451), artifact tabz-decompiled-atmosphere-and-skybox-hlsl.
- Original scene/material extraction: workflow run [38479742128](https://github.com/Miguellbr/TABZ-port/actions/runs/38479742128), artifact tabz-original-haze-context-values.
- The exported ShaderLab text is explicitly marked invalid by AssetRipper and gives white dummy defaults. The compiled DXBC and serialized material values are the authoritative evidence used here.

## 1. Which skybox is used in gameplay?

The original v5.1 TABZ_World.unity RenderSettings block contains:

| Property | Serialized value |
|---|---|
| m_SkyboxMaterial | GUID c9256352e488ea0489f5ddd34d1e49c9 |
| m_AmbientMode | 0 = AmbientMode.Skybox |
| m_Fog | 0 = disabled |
| m_AmbientSkyColor | (1.5, 1.2838235, 0.95955884, 1) |
| m_AmbientEquatorColor | (0.114, 0.125, 0.133, 1) |
| m_AmbientGroundColor | (0.047, 0.043, 0.035, 1) |
| m_AmbientIntensity | 1 |

The GUID resolves to the recovered sunnyDay.mat in this project: its .meta GUID is the same c9256352e488ea0489f5ddd34d1e49c9. The material references shader GUID 928ed042166eac848ae7d193c1007671, which matches Skybox_Gradient Skybox.shader.meta.

The original TABZMainMenu.unity uses a different skybox material GUID (ed85448feae810043b3dc73b406a3861). The offline gameplay prototype correctly loads Environment/sunnyDay, not the menu skybox.

## 2. Exact gameplay material values

The original v5.1 Assets/Material/sunnyDay.mat serializes:

| Property | Value |
|---|---|
| _Color1 | (0.6544118, 0.8855981, 1, 0) |
| _Color2 | (0.020545002, 0.4725345, 0.6985294, 0) |
| _UpVector | (0, 1, 0, 0) |
| _Intensity | 1 |
| _Exponent | 1 |
| _UpVectorPitch | 0 |
| _UpVectorYaw | 0 |

These values match the sunnyDay.mat copied into MobilePort. The colors' alpha is zero; the shader outputs that alpha as-is, while the visible gradient comes from RGB.

## 3. Recovered DXBC behavior

The original v4 and v5 DX11 vertex/pixel programs are identical in their decompiled instruction bodies (only the generated timestamp comment differs).

### Vertex program 0

- Transforms input POSITION0 using Unity's object/view/projection constant buffers.
- Forwards input TEXCOORD0.xyz unchanged to pixel-stage TEXCOORD0.
- It does not derive the gradient direction from the POSITION value.

### Pixel program 1

The decompiled math is:

1. Normalize the interpolated TEXCOORD0 direction.
2. Compute height = dot(direction, _UpVector.xyz) * 0.5 + 0.5.
3. Compute gradient = pow(height, _Exponent) (DXBC expresses this as log2, multiply by exponent, then exp2).
4. Interpolate from _Color2 to _Color1 using gradient.
5. Multiply the complete RGBA result by _Intensity.

The compiled pixel program does not use _UpVectorPitch or _UpVectorYaw, does not normalize _UpVector, and does not saturate the dot-derived height. The actual gameplay material uses a unit up vector and zero pitch/yaw.

## 4. Corrections implemented in MobilePort

The mobile shader previously differed from the recovered program in several ways:
- It used vertex POSITION as the gradient direction instead of forwarding TEXCOORD0.
- It applied a custom pitch/yaw rotation that the compiled shader does not perform.
- It normalized the up vector and saturated the height before exponentiation.
- It multiplied RGB only and used fixed precision for the result.

The reconstructed shader now forwards TEXCOORD0, uses the original dot/half-offset/power/lerp sequence, leaves the serialized up vector unnormalized, and multiplies the complete float4 by intensity.

The runtime environment also previously forced AmbientMode.Trilight. That contradicted the original world scene's m_AmbientMode = 0 (Skybox), so OriginalTabzEnvironment now sets AmbientMode.Skybox. It retains the original serialized ambient sky/equator/ground values and disables Unity's built-in fog, matching the scene.

## 5. Remaining validation

The DXBC-to-mobile shader behavior and serialized scene/material values are now mapped and implemented. GitHub source-contract checks passed in [run 38479971062](https://github.com/Miguellbr/TABZ-port/actions/runs/38479971062), but this is not a Unity shader compilation or a device visual test.

Still required:
- Compile the Unity project with the mobile graphics backend.
- Confirm Unity's built-in skybox draw supplies the same TEXCOORD0 direction stream as the original shader on the target backend.
- Compare the sky gradient and ambient lighting in the Android prototype against the original game, with the camera and exposure matched.
- Only after the skybox baseline is confirmed should skybox integration from DeepSky Haze be considered. That is a separate feature and must not be used to compensate for a wrong base gradient.
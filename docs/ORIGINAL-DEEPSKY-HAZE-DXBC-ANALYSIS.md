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
- The depth-downsample pixel program samples four half-texel-offset positions and writes the maximum raw depth. This is the exact compiled behavior; do not replace it with an assumed average.

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

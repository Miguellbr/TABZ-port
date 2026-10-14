# Mobile DeepSky Haze pipeline

The active mobile shader has four passes:

1. Half-resolution directional haze radiance using the recovered 15-step ray sampling pattern and primary haze ratio.
2. Horizontal bilateral radiance blur.
3. Vertical bilateral radiance blur.
4. Full-resolution composite with closest-depth selection among four half-resolution radiance samples, plus separate air, secondary haze, fog-scattering and fog-extinction terms.

The C# renderer allocates two temporary half-resolution targets. It prefers ARGBHalf when supported and falls back to ARGB32, then releases both targets in a finally block. The mobile version packs linear eye depth into the radiance alpha channel; the original uses a separate RFloat half-resolution depth target.

## Bilateral blur — now recovered

The original DXBC uses a separable **15-tap** kernel: the center plus offsets ±1 through ±7. The recovered spatial weights are:

| Offset | Weight |
|---:|---:|
| 0 | 0.144464448 |
| ±1 | 0.144464448 |
| ±2 | 0.11153505 |
| ±3 | 0.0805530921 |
| ±4 | 0.0508756414 |
| ±5 | 0.0279815998 |
| ±6 | 0.0133245699 |
| ±7 | 0.00545095978 |

Each tap is multiplied by the exact bilateral depth term:

`exp(-m_GaussianDepthFalloff * abs(sampleEyeDepth - centerEyeDepth)^2)`

The original `DS_HazeView` defaults are `m_GaussianDepthFalloff = 500`, `m_TemporalRejectionScale = 0.1`, and `m_TemporalBlendFactor = 0.25`. The mobile shader now uses the original 15-tap coefficients and the original depth-falloff value. It also uses the effective depth-downsample output recovered from DXBC: the maximum of the two diagonal raw-depth samples that actually feed the output, then linearization.

## Cascaded shadows — original scheduling recovered

The original `DS_HazeView` does not fetch cascade data from an ordinary `OnRenderImage` call. It installs a command buffer on the directional light at `LightEvent.AfterShadowMap` to bind `BuiltinRenderTextureType.CurrentActive` as `_ShadowCascades`, then runs the radiance pass from `LightEvent.AfterScreenspaceMask`. The DXBC consumes the four Unity shadow split spheres/radii and the four world-to-shadow matrices from Unity's shadow constant buffer.

So cascaded shadows are **not inherently unavailable** in the Built-in Render Pipeline. The mobile port must reproduce this light-event command-buffer scheduling and ensure the light really renders cascaded shadows; simply sampling a global texture from `OnRenderImage` is not equivalent. The current mobile implementation is still unshadowed.

## Temporal accumulation — original algorithm recovered

The original temporal variant is enabled by default in the recovered `DS_HazeView` source. It uses two full-resolution ping-pong radiance targets plus a full-resolution RFloat previous-depth target. At the current pixel it reconstructs the world position, projects it through the previous view-projection matrix, checks that the previous UV is inside the viewport, samples previous depth, reconstructs the previous world position with the previous inverse view-projection matrix, and computes the world-position difference.

The history contribution is weighted by:

`temporalBlendFactor * exp(-temporalRejectionScale * worldPositionDifference)`

The defaults are blend factor `0.25` and rejection scale `0.1`. The shader writes the final scene to target 0 and the updated radiance history to target 1 (MRT). The C# component swaps the two history targets and updates both matrices after rendering. The mobile implementation does not yet contain this history path; it needs a deliberate history-buffer implementation and validation of the projection/depth conventions on Android.

## Remaining limitations

- Cascaded shadows are not yet wired into the mobile renderer.
- Temporal reprojection/history is not yet implemented.
- The mobile radiance/composite still differs from the original in other details and is not pixel-identical.
- ARGB32 fallback clamps HDR radiance on devices without ARGBHalf render-target support.
- Scattering remains disabled by default.
- Unity compilation, Android GPU compatibility, frame cost and visual parity still require the Unity Dashboard build and device comparison.

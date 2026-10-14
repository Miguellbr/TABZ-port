# Mobile DeepSky Haze pipeline

The active mobile shader has four passes:

1. Half-resolution directional haze radiance using the recovered 15-step ray sampling pattern and primary haze ratio.
2. Horizontal depth-aware radiance blur.
3. Vertical depth-aware radiance blur.
4. Full-resolution composite with closest-depth selection among four half-resolution radiance samples, plus separate air, secondary haze, fog-scattering and fog-extinction terms.

The C# renderer allocates two temporary half-resolution targets. It prefers ARGBHalf when supported and falls back to ARGB32, then releases both targets in a finally block.

## Recovered bilateral depth contract

The original DXBC blur programs use:

`weight = spatialWeight * exp(-samplingFalloff * (sampleDepth - centerDepth)^2)`

The original `DS_HazeView` default is `m_GaussianDepthFalloff = 500`, supplied as `_SamplingParams.x`. The mobile shader now uses that value and stores **linear eye depth** in the radiance alpha channel. It also reproduces the original half-resolution depth downsample rule: take the maximum of four raw depth samples, then linearize it.

The five-tap spatial coefficients remain the mobile kernel. The original DXBC confirms a separable spatial kernel, but the coefficients have not yet been independently verified from the extracted pixel program. The blur is therefore closer to the original, but not yet an exact kernel match.

## Known limitations

- The original radiance pass selects among four shadow cascades and performs 15 comparison samples per ray. The mobile pass remains unshadowed; no real cascade atlas or matrices are currently bound.
- Temporal accumulation remains unimplemented. The original temporal variant reprojects the current world position with previous view-projection matrices, validates against previous depth, and blends history using the original temporal rejection/blend settings.
- The current radiance integration and composite are still a mobile reconstruction, not pixel-identical parity with the original DXBC.
- ARGB32 fallback clamps HDR radiance on devices without ARGBHalf render-target support.
- Scattering remains disabled by default. It must be enabled through the existing scattering control to display the directional and analytic lighting contribution.
- Unity compilation, Android GPU compatibility, frame cost and visual parity still require the Unity Dashboard build and device comparison.

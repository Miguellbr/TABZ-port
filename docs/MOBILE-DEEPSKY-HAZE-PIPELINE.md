# Mobile DeepSky Haze pipeline

The active mobile shader now has four passes:

1. Half-resolution directional haze radiance using the recovered 15-step ray sampling pattern and primary haze ratio.
2. Horizontal depth-aware radiance blur.
3. Vertical depth-aware radiance blur.
4. Full-resolution composite with closest-depth selection among four half-resolution radiance samples, plus separate air, secondary haze, fog-scattering and fog-extinction terms.

The C# renderer allocates two temporary half-resolution targets. It prefers ARGBHalf when supported and falls back to ARGB32, then releases both targets in a finally block. The radiance alpha stores eye depth normalized by the camera far clip so the depth comparison remains usable in ARGB32.

## Known limitations

- The original radiance samples use cascaded shadow comparisons. This mobile version is unshadowed because the Built-in OnRenderImage path does not expose the original cascade atlas and matrices as a stable public contract.
- The blur retains the separable depth-aware structure, but uses a normalized-depth exponential weight. The original serialized sampling-falloff value was not recovered, so this is not a numerical match.
- This is a staged approximation of the DXBC behavior, not pixel-identical parity.
- ARGB32 fallback clamps HDR radiance on devices without ARGBHalf render-target support.
- Scattering remains disabled by default. It must be enabled through the existing scattering control to display the directional and analytic lighting contribution.
- Unity compilation, Android GPU compatibility, frame cost and visual parity still require the Unity Dashboard build and device comparison.

# Mobile TABZ haze — implementation status

This file records what is currently wired into MobilePort after recovering the original TABZ v4 and v5.1 scene data.

## Implemented

- TabzHazeContextData contains the serialized gameplay context values for the general map, Snow2, LightHouse, Snow, and darknes. It preserves separate air-scattering, haze-scattering, and fog parameters.
- Extinction is converted exactly as the original DS_HazeView does: serialized scale multiplied by 0.01.
- The original ambient fog colours, light colour, air/haze scattering scales and density falloffs, haze phase parameters, fog extinction, fog scattering, opacity, start distance and fog phase direction are retained in the context data. In the gameplay world, m_FogScatteringScale is 0 while m_HazeScatteringScale and m_AirScatteringScale are both 1; zero fog scattering does not mean zero atmospheric scattering.
- The three transition curves for MyDay_Copy, MyDay_Copy_Copy, and Night are evaluated in the original serialized order, with MyDay as the base context.
- The mobile shader uses depth-aware exponential extinction and the selected context's ambient colour. It does not use the previous hand-picked start/end-distance presets or a uniform grey sky wash.
- The fog toggle follows the v5.1 behavior: disabling fog moves its start distance to 5 rather than deleting the entire atmosphere.
- The context curves are cached once instead of allocating new curves every refresh.
- The original v4 and v5.1 gameplay zone components, context values and transforms were compared. All five serialized zone components are byte-for-byte identical. The v5.1-only difference relevant to fog is the fog-toggle start distance of 5 when disabled.
- TabzHazeZone reproduces the original transform-based volume test, priority ordering metadata, and edge blend-weight calculation. It does not require a Collider.
- MobileDistanceFog gathers all containing zones, sorts them by ascending priority and then ascending zone size, and sequentially blends each later context using that zone's original GetBlendWeight formula.
- The offline bootstrap creates the five original haze zones using the recovered DayAndNightCycle → DS_HazeController parent transforms and each child zone's serialized local position and scale. The camera begins inside the large General zone.

## Current prototype behavior and limits

- The offline prototype starts at normalized context time 0.5 (the daytime MyDay portion). It is not yet connected to the original DayNightCycle; the recovered original cycle duration is 3600 seconds, but the prototype has no ported day/night clock to supply its phase.
- The original map itself is still absent. The five haze-zone transforms are present in the offline sandbox, but the map geometry is not.
- The camera shader has a low-cost, optional directional haze-radiance experiment in addition to geometry extinction. It consumes the recovered haze scattering scale (0.0021 conversion), height falloff, phase direction and secondary-scattering ratio, and follows `RenderSettings.sun` direction/colour each frame. It integrates density with 8 midpoint samples along the camera ray.
- This is deliberately reduced: unlike original DX11 pass 0, it has no cascaded-shadow visibility (the original performs 15 shadow-comparison samples), interleaved sampling offsets, half-resolution radiance buffer, bilateral blur or skybox integration. It is an unshadowed directional haze approximation, not full Rayleigh/Mie parity.
- The exponential depth integration is a mobile approximation using the recovered extinction and camera-height density factor. It is not claimed to be bit-for-bit equivalent to the original decompiled PC shader.

## Files

- Assets/Scripts/Mobile/TabzHazeContextData.cs — recovered context values, time curves, and zone metadata.
- Assets/Scripts/Mobile/TabzHazeZone.cs — transform-based volume, priority ordering and edge blend weight matching original DS_HazeZone.
- Assets/Scripts/Mobile/MobileDistanceFog.cs — original overlapping-zone selection/blending and camera integration.
- Assets/Scripts/Mobile/OfflinePrototypeBootstrap.cs — original zone hierarchy and serialized transforms for the offline sandbox.
- Assets/Resources/MobileDistanceFog.shader — depth-based geometry extinction plus optional unshadowed directional haze radiance.

## Validation still required

The GitHub source-contract check is not a Unity compilation or an Android-device test. Unity compilation and visual validation on the Android device are still required. Scattering is disabled by default (`scatteringEnabled = false`) until its brightness, direction and frame cost are checked on-device. Enable it on the `MobileDistanceFog` component for the A/B test; `intensityMultiplier = 0` bypasses both effects. The first visual test should establish whether the geometry fog's extinction response is visually close to TABZ without desaturating the weapon or nearby ground. If it is not, adjust only after comparing a reproducible in-game test; do not restore arbitrary distance presets.

## Source record

The v4/v5.1 comparison, original zone transforms and extraction provenance are documented in docs/ORIGINAL-TABZ-WORLD-HAZE-CONTEXTS.md. Both builds were reconstructed with AssetRipper; the gameplay zone components are byte-for-byte identical. The main-menu context is documented separately and is not used as a gameplay-world zone.

# Mobile TABZ haze — implementation status

This file records what is currently wired into `MobilePort` after recovering the original v5.1 scene values.

## Implemented

- `TabzHazeContextData` contains the serialized gameplay context values for the general map, `Snow2`, `LightHouse`, `Snow`, and `darknes`.
- Extinction is converted exactly as the original `DS_HazeView` does: serialized scale multiplied by `0.01`.
- The original ambient fog colours, height falloff, opacity, start distance, scattering scale and scattering direction are retained in the context data.
- The three transition curves for `MyDay_Copy`, `MyDay_Copy_Copy`, and `Night` are evaluated in the original serialized order, with `MyDay` as the base context.
- The mobile shader now uses depth-aware exponential extinction and the selected context's ambient colour. It no longer uses the previous hand-picked start/end-distance presets or a uniform grey sky wash.
- The fog toggle follows the recovered original behavior: disabling fog moves its start distance to 5 rather than deleting the entire atmosphere.
- The context curves are cached once instead of allocating new curves every refresh.

## Current prototype behavior and limits

- The offline prototype starts at normalized context time `0.5` (the daytime `MyDay` portion). It is not yet connected to the original `DayNightCycle`; the recovered original cycle duration is 3600 seconds, but the prototype has no ported day/night clock to supply its phase.
- With no registered zone volume, the camera uses the original general `DS_HazeZone` context.
- `TabzHazeZone` is an attachable marker for recovered zone colliders. When such colliders exist, the camera selects the containing zone with the highest serialized priority.
- The current offline scene contains only a generated flat test ground. It does not contain the original map or the five original zone volumes. No zone coordinates or dimensions have been fabricated.
- Serialized blend ranges are recorded in `TabzHazeContextData.GetZoneInfo`, but spatial blending across zone boundaries is not implemented yet. The exact original zone-weight calculation must be recovered before reproducing it.
- The camera shader is a low-cost mobile reconstruction, not the original `Hidden/DS_Haze` multi-pass shader. It currently handles geometry extinction only; skybox integration, Rayleigh/Mie scattering, directional shafts, bilateral radiance blur, temporal reprojection, and local volumetric lights are not ported.
- The exponential depth integration is a mobile approximation using the recovered extinction and camera-height density factor. It is not claimed to be bit-for-bit equivalent to the original decompiled PC shader.

## Files

- `Assets/Scripts/Mobile/TabzHazeContextData.cs` — recovered context values and time curves.
- `Assets/Scripts/Mobile/TabzHazeZone.cs` — collider-backed zone marker.
- `Assets/Scripts/Mobile/MobileDistanceFog.cs` — context selection and camera integration.
- `Assets/Resources/MobileDistanceFog.shader` — low-cost depth-based geometry fog.

## Validation still required

Unity compilation and visual validation on the Android device are still required. In particular, the first test should establish whether the geometry fog's extinction response is visually close to TABZ without desaturating the weapon or nearby ground. If it is not, adjust only after comparing a reproducible in-game test; do not restore arbitrary distance presets.

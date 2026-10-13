# Original TABZ World DeepSky Haze context data

## Source and extraction

- Source build: original TABZ v5.1 Windows build.
- Recovered project: AssetRipper output from GitHub Actions run `38074403676`.
- Extraction: GitHub Actions run `38305654390`, artifact `tabz-original-haze-context-values`.
- The extractor scanned 3,281 recovered project files, found 4 DeepSky Haze C# scripts and 29 serialized object blocks, with 0 read errors.
- Scene source: `Assets/7 Scenes/TABZ_World.unity`.
- The values below are serialized scene values or values directly defined by the recovered DeepSky Haze source. They are not visual estimates.

## v4 versus v5.1 verification

The original v4 archive was reconstructed separately after removing its duplicate backup assembly (Assembly-Csharp - Copy.dll), which otherwise caused AssetRipper's Mono assembly manager to reject the duplicate Assembly-CSharp identity.

- v4 recovery: GitHub Actions run 38375244065.
- v4 extraction: 1,872 files scanned, 4 DeepSky Haze scripts recovered, 27 matching serialized blocks, 0 read errors.
- v5.1 extraction: GitHub Actions run 38305654390; 3,281 files scanned, 4 DeepSky Haze scripts recovered, 29 matching serialized blocks, 0 read errors.
- The five gameplay DS_HazeZone serialized component blocks are byte-for-byte identical between v4 and v5.1.
- DS_HazeZone.cs, DS_HazeContext.cs, DS_HazeContextItem.cs, DS_HazeCore.cs and DS_HazeLightVolume.cs are identical between the two recovered builds. DS_HazeView.cs is the relevant exception: v5.1 adds the TABZChat.fogToggle behavior that forces fog start distance to 5 when disabled. Static command-buffer name initialization was also moved into a static constructor.

This means the gameplay haze context values and zone math did not change between these two versions. The v5.1-only fog toggle behavior is a separate change and does not mean the world haze channel settings changed.

## Original zone transforms and volume behavior

The zones are not collider volumes. Each GameObject contains a Transform and a DS_HazeZone MonoBehaviour; the original DS_HazeZone.Setup() creates Bounds(Vector3.zero, transform.localScale). Contains() converts the camera position with InverseTransformPoint, scales that point by localScale, and tests it against those bounds.

All five zones are children of DS_HazeController, itself a child of DayAndNightCycle. Both parent rotations are identity and both parent scales are (1,1,1). Their serialized parent positions are:

- DayAndNightCycle: (-308.82278, -317.1259, 1399.1091)
- DS_HazeController local position: (727.6909, 317.1259, -815.5936)

The following child local transforms are identical in v4 and v5.1. World centers are calculated by composing the two verified parent positions with each child position.

| Zone | Child local position | Local scale (full bounds size) | World center |
|---|---|---|---|
| General DS_HazeZone | (0, 0, 0) | (2283.2312, 1200.8811, 2183.5205) | (418.86812, 0, 583.5155) |
| Snow2 | (89.7, 188.2, 391.5) | (242.87401, 251.06976, 264.7568) | (508.56812, 188.2, 975.0155) |
| LightHouse | (152.2, 201.41, -260.2) | (40.395077, 35.78625, 33.292164) | (571.06812, 201.41, 323.3155) |
| Snow | (123.5, 160.8, 803.2) | (204.26584, 180.96031, 203.59604) | (542.36812, 160.8, 1386.7155) |
| darknes | (90.7, 140.4, 369.5) | (56.68853, 91.9938, 51.398403) | (509.56812, 140.4, 953.0155) |

Original zone selection is more specific than "pick the highest priority":

1. Collect every enabled zone containing the camera position.
2. Sort by ascending priority; zones with equal priority sort by ascending squared extents size.
3. Use the first zone's context as the base.
4. Sequentially lerp each later zone's context into the result using that zone's own GetBlendWeight(cameraPosition).

The original blend weight is the minimum distance to any box edge, divided by min(extents) * blendRange, clamped to [0,1]. Therefore the weight is 0 at the boundary and reaches 1 after moving inward by the blend distance. This is the exact source behavior from DS_HazeZone.cs and DS_HazeCore.GetRenderContextAtPosition, not a guessed collider approximation.

## Important distinction: air, haze and fog are separate channels

Every gameplay-world context item inspected in `TABZ_World.unity` has:

| Serialized field | Original value |
|---|---:|
| `m_AirScatteringScale` | 1 |
| `m_AirScatteringMultiplier` | 3 = `Multiplier.One` |
| `m_AirDensityHeightFalloff` | 0.001 |
| `m_HazeScatteringScale` | 1 |
| `m_HazeScatteringMultiplier` | 3 = `Multiplier.One` |
| `m_HazeDensityHeightFalloff` | 0.003 |
| `m_HazeScatteringDirection` | 0.4 |
| `m_HazeSecondaryScatteringRatio` | 0.8 |
| `m_FogOpacity` | 1 |
| `m_FogScatteringScale` | 0 |
| `m_FogDensityHeightFalloff` | Usually 0.0001; general zone daytime contexts use 0.015 |
| `m_FogStartDistance` | 0 |
| `m_FogScatteringDirection` | 0.7 |
| `m_FogLightColour` | (1, 0.6653145, 0.44852942, 1) |

**The zero is only `m_FogScatteringScale`. It does not mean the atmosphere has no scattering.** The original world contexts have air scattering = 1 and haze scattering = 1. The haze channel has density falloff 0.003, direction 0.4 and secondary-scattering ratio 0.8. The prior mobile data model stored only the fog scattering field under the ambiguous name `scatteringScale`; this update separates and preserves the air, haze and fog values.

The serialized enum value 3 means `Multiplier.One` (1.0), as confirmed by `DS_HazeContextItem.MultiplierAsFloat` and `ParamWithMultiplier`. Therefore the world-context fog extinction values below are not multiplied by 3.

## Gameplay-world zones

The ambient values below are `m_FogAmbientColour`. All four time items in each cycling zone share the air/haze values listed above and use the same `m_FogLightColour`.

| Zone GameObject | Priority | Blend range | Fog extinction scale (day / warm / sunset / night) | Fog height falloff (day / warm / sunset / night) |
|---|---:|---:|---|---|
| General `DS_HazeZone` | 0 | 0.1 | 5 / 5 / 5 / 4.75 | 0.015 / 0.015 / 0.015 / 0.0001 |
| `Snow2` | 100 | 0.531 | 3 / 3 / 3 / 4.75 | 0.0001 / 0.0001 / 0.0001 / 0.0001 |
| `LightHouse` | 250 | 0.1 | 5 / 5 / 5 / 4.75 | 0.0001 / 0.0001 / 0.0001 / 0.0001 |
| `Snow` | 250 | 0.531 | 5 / 5 / 5 / 4.75 | 0.0001 / 0.0001 / 0.0001 / 0.0001 |
| `darknes` | 250 | 0.05 | 8 (single context item) | 0.0001 |

### Ambient fog colours by time item

| Zone | Day | Warm | Sunset | Night |
|---|---|---|---|---|
| General | (0.6911765, 0.93610543, 1) | (1, 0.7380324, 0.53676474) | (1, 0.42647058, 0.50953335) | (0.25, 0.25, 0.25) |
| Snow2 | (0.875, 0.9741379, 1) | (1, 0.92099386, 0.8602941) | (1, 0.78676474, 0.81764704) | (0.4705882, 0.4705882, 0.4705882) |
| LightHouse | (0.6911765, 0.93610543, 1) | (1, 0.7380324, 0.53676474) | (1, 0.42647058, 0.50953335) | (0.25, 0.25, 0.25) |
| Snow | (0.875, 0.9741379, 1) | (1, 0.92099386, 0.8602941) | (1, 0.78676474, 0.81764704) | (0.4705882, 0.4705882, 0.4705882) |
| darknes | (1, 0.11764705, 0.11764705) | — | — | — |

All listed colours have alpha 1 in the serialized data.

## Original time blending

Recovered `DS_HazeContext.GetContextItemBlended` starts with item 0 as the base, then iterates items 1..N in serialized order. For each item it evaluates that item's own `m_Weight` curve and lerps the current result toward that item. This is sequential lerping, not a normalized weighted average.

For the General, Snow2, LightHouse and Snow contexts, serialized item order is:
1. `MyDay` (base)
2. `MyDay_Copy` (warm transition)
3. `MyDay_Copy_Copy` (sunset transition)
4. `Night`

The curve keys are copied into `TabzHazeContextData` and retained in the mobile prototype. The source `DS_HazeContextItem.Lerp` lerps air/haze scales and falloffs, haze direction and secondary ratio, fog opacity/scattering/extinction/density/start/direction, and both fog colours.

## Main menu is a separate context

`TABZMainMenu.unity` has a separate single context item (serialized name is hex for “Morning”): air scale 1, haze scale 1, haze density 0.003, haze direction 0.4, secondary ratio 0.8, fog scattering scale 2, fog extinction scale 8, fog density 0.0001, fog ambient (0.36764705, 0.36764705, 0.36764705), and the same fog light colour. These are not the gameplay-world zone values and should not be substituted for them.

## Mobile-port status

- `TabzHazeContextData.Settings` now retains separate air, haze and fog parameters from the original serialized contexts.
- The current `MobileDistanceFog.shader` still uses only the geometry fog/extinction path. The newly preserved air/haze fields are data for the next atmospheric-scattering implementation; they are not yet rendered by that shader.
- The original DeepSky radiance pass, directional shadow-cascade sampling, blur and temporal history remain separate rendering work. Do not claim the current mobile pass reproduces those features.
- The v4-v5.1 comparison is complete: all five gameplay zone components, their context values, transforms, priorities and blend ranges are identical.
- The original transform hierarchy and exact volume test / priority / sequential blend algorithm are recovered and implemented in the mobile prototype. The offline bootstrap now creates the five zones at their serialized transforms. The original map itself is still not part of the flat offline sandbox.

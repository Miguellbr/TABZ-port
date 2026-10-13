# Original TABZ World DeepSky Haze context data

## Source and extraction

- Source build: original TABZ v5.1 Windows build.
- Recovered project: AssetRipper output from GitHub Actions run `38074403676`.
- Extraction: GitHub Actions run `38305654390`, artifact `tabz-original-haze-context-values`.
- The extractor scanned 3,281 recovered project files, found 4 DeepSky Haze C# scripts and 29 serialized object blocks, with 0 read errors.
- Scene source: `Assets/7 Scenes/TABZ_World.unity`.
- The values below are serialized scene values or values directly defined by the recovered DeepSky Haze source. They are not visual estimates.

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
- Zone priorities and blend ranges are recovered. Original zone volume transforms and their complete spatial behavior still need separate verification before reconstructing map placement or boundary blending.
- This extraction is from v5.1. A v4-v5.1 serialized-value comparison has not yet been completed.

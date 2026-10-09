# Reconstruction findings — original TABZ build

This report is based on UnityPy's serialized-object scan of the supplied Windows builds. It is a map for reconstruction, not an importable Unity scene.

## Strongest starting points

### Gameplay scene: `GAME_Data/level1`
Recovered hierarchy exposes these named systems:
- `WorldCreatorTerrain` — terrain and `WorldCreatorReference`
- `ServiceLocator`
- `SoundStuff` — `SoundEffectsManager`, `MusicHandler`
- `NetworkStuff/NetworkManager` — `NetworkManager`, `NetworkChunk`
- `NetworkStuff/Network` — `NetworkConnector`
- `ItemSpawns` — `WorldItemAdder`
- `Map/WorldSpawn`, `Map/SpawnPoints city1`, `Map/SpawnPoints city2`, `Map/singleSpawn` — spawn point managers and tags
- `DayAndNightCycle`
- `KnarkFixer`

The level contains 19,261 GameObjects, including 11,548 BoxColliders, 14,891 MeshRenderers, 390 Animators, 1,020 MonoBehaviours, and one Terrain. It is a large scene; the first mobile prototype should avoid loading every object until performance is measured.

### Shared resources: `GAME_Data/resources.assets`
Recovered GameObject/prefab names include:
- `TABS_Player_Network` and `Player`
- `TABZOMBIE`, `TABZOMBIEHEAD`, `TABZOMBIELONG`, `TABZOMBIETHIN`, `TABZOMBIETINY`
- `CameraHolder`, `CameraRot`, `CameraForce`, `Main Camera`
- `WeaponPosition`, `WeaponLeftHand`, `WeaponRightHand`
- `ProjectileBullet_1`, `RayCastBullet_1`
- `HotBar`, `Health`, `InteractionInfoHolder`, `UI_Canvas`
- `SteamManager`, `NetworkManager`, `SpawnPointManager`

These names suggest the original player/zombie models, weapon attachment points, camera, HUD, and multiplayer objects may be recoverable from resources. Actual component serialization and asset dependencies still need validation before reuse.

### Main menu: `GAME_Data/level0`
The recovered object names include `MainMenuCamera`, `ServerList`, `QuickMatchButton`, `JoinButton`, `SteamManager`, `EnterNamePrompt`, and options UI. This is not the first target: Steam/server browser dependencies make it less suitable than a standalone gameplay prototype.

## Recommended implementation order

1. Inspect and export the player, camera and zombie prefabs/resources.
2. Establish whether a reconstructed Unity project can import these assets while preserving scripts/material references.
3. Create a minimal offline test scene using original assets where possible.
4. Connect the existing virtual joystick/look area/action buttons to the recovered player input code.
5. Remove or isolate Steam/Photon startup paths for an offline Android prototype.
6. Only then address multiplayer, menus and full-world optimization.

## Important limitations

- A hierarchy inventory is not the original editable Unity project.
- `Unknown` component labels mean the parser did not resolve a component's concrete type.
- The old game uses Unity 5.6 beta; newer Unity versions can introduce serialization, shader, API and physics differences.
- Do not claim an Android build or functional port until an APK has been built and tested on a device.

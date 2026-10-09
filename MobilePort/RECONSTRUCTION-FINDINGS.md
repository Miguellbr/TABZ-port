# Reconstruction findings — original TABZ build

This report combines UnityPy's serialized-object scan with static analysis of the decompiled managed assemblies. It is a reconstruction map, not yet an importable Unity project.

## Confirmed source and asset facts

- Both supplied archives contain effectively matching serialized scene/resource inventories and managed game code.
- The main managed assembly decompiles successfully: about 1.46–1.49 MB of recovered C# per build, with roughly 543–546 classes.
- The game logic uses Unity MonoBehaviours, Rigidbody-based ragdoll physics, Animator, NavMesh, UI, Photon PUN 1-era networking and FMOD.
- The game scene `GAME_Data/level1` has 19,261 GameObjects, 14,891 MeshRenderers, 11,548 BoxColliders, 390 Animators and 1,020 MonoBehaviours. The scene is large enough that mobile performance must be tested early.

## Gameplay scene: `GAME_Data/level1`

Recovered roots and systems:
- `WorldCreatorTerrain` — terrain and `WorldCreatorReference`
- `ServiceLocator`
- `SoundStuff` — `SoundEffectsManager`, `MusicHandler`
- `NetworkStuff` — `NetworkManager`, `NetworkConnector`
- `ItemSpawns` — `WorldItemAdder`
- `Map/WorldSpawn`, `Map/SpawnPoints city1`, `Map/SpawnPoints city2`, `Map/singleSpawn`
- `DayAndNightCycle`, `ChunkManager`, `ChunkModifiers`, `SoundZones`, `KnarkFixer`

## Shared resources: `GAME_Data/resources.assets`

Recovered objects include:
- Player: `TABS_Player_Network`, `Player`, `CameraHolder`, `CameraRot`, `CameraForce`, `Main Camera`
- Zombie variants: `TABZOMBIE`, `TABZOMBIEHEAD`, `TABZOMBIELONG`, `TABZOMBIETHIN`, `TABZOMBIETINY`, `TABZOMBIEWIDE`, `TABZOMBIEKing`, `TABZOMBIEGOATGiant`
- Weapon mounts: `WeaponPosition`, `WeaponLeftHand`, `WeaponRightHand`
- Projectile objects: `ProjectileBullet_1`, `RayCastBullet_1`
- HUD: `HotBar`, `Health`, `InteractionInfoHolder`, `UI_Canvas`, `Items`, `AmmoUIText`
- Integration objects: `SteamManager`, `NetworkManager`, `SpawnPointManager`

The resource database also has many weapon and bullet prefab names, including `Repeater`, `EvilGun`, `HuntingKnife`, `BigSniper`, `Musket`, `Revolver`, `Axe` and multiple `Bullet_*` variants.

## Source analysis: what must change for touch

### Player movement — `PlayerHandler`
The player input path is not a generic Unity character controller. `PlayerHandler.Update()` checks keyboard keys directly:
- W/S for forward/back
- A/D for strafing
- Shift for run
- Space for jump

It then calls `MovementHandler` and `PhysicsAmimationController`. `MovementHandler` applies forces to a set of child Rigidbodies and respects AI/grounded/fallen states. This is valuable: we should adapt the input decision layer and preserve the existing physics/animation calls rather than replacing the ragdoll movement with a standard CharacterController.

The mobile adapter therefore needs a normalized movement vector plus run/jump states, and a patch at `PlayerHandler.Update()` or a compatibility input facade. A standalone touch HUD alone does not make the original player controllable.

### Camera — verify the actual player camera controller
The recovered `DeepSky.Haze.Demo.BasicMouseLookControl` reads `Input.GetAxis("Mouse X"/"Mouse Y")`; it also handles Escape. This is a camera-look candidate but is inside a demo namespace, so do not assume it is the only camera script. Another camera/controller block also reads mouse axes and scroll, and uses mouse buttons. Inspect serialized component bindings before patching the camera.

### Weapons — `Weapon`
The weapon class checks mouse-left for firing, mouse-left hold for automatic weapons, mouse-right for aiming in other player logic, and R for reload. Those paths need mobile action-state integration. The weapon itself includes Photon RPC calls and FMOD sound events, so a true offline prototype may need to isolate network/sound calls, not merely replace the input.

### Interaction and inventory
- `PlayerInteraction.Update()` raycasts from the screen center and uses E to pick up an item. Replace the keyboard requirement with a touch interaction action while preserving the center-screen raycast.
- Inventory uses I/Tab and number keys for opening/selecting hotbar slots. Mobile needs a visible inventory toggle and touch selection.
- Pause/menu code changes `Cursor.lockState` and `Cursor.visible`, assumptions that need conditional handling on Android.

### Platform/network/audio dependencies
- Photon calls are deeply woven through player, zombie, inventory and item spawning; offline play requires a defined local ownership/spawn path rather than only hiding the server browser.
- Steam is present in the menu/startup path and is Windows-specific in the shipped build. Disable/replace it for the first Android prototype.
- FMOD event calls appear throughout player and weapon logic. Windows FMOD binaries cannot be assumed to work on Android; we need either Android-compatible FMOD support and banks or a small Unity audio replacement for the first proof of concept.
- Some input systems already check `Input.touchCount`, but this appears in utility/UI code and does not establish that gameplay controls support touch.

## Main menu: `GAME_Data/level0`

Recovered names include `MainMenuCamera`, `ServerList`, `QuickMatchButton`, `JoinButton`, `SteamManager`, `EnterNamePrompt` and options UI. Avoid this as the first target: Steam and network dependencies make it less suitable than a small offline gameplay scene.

## Recommended investigation and implementation order

1. Map component script GUIDs to class names for the player, camera, weapon, zombie and world spawner prefabs.
2. Export and validate original player/zombie meshes, materials, textures, animation clips and weapon prefabs.
3. Determine which serialized assets UnityPy can export reliably and which need a different extraction path.
4. Choose an editor version after a compatibility/import experiment; do not assume a current Unity release can losslessly open Unity 5.6 beta data.
5. Build a minimal offline scene with one player and a limited environment before attempting the 19k-object world.
6. Patch player movement, look, fire, aim, reload, jump, interaction and inventory to read touch state.
7. Replace/isolate Photon, Steam and FMOD dependencies for offline Android testing.
8. Profile performance and then progressively restore terrain, zombies, weapons, audio and multiplayer.

## Limitations

- A hierarchy inventory is not the original editable Unity project.
- `Unknown` component labels mean the parser did not resolve a component's concrete type.
- Decompiled C# is approximate source recovery; serialized references and compiler-generated details need validation.
- The old game uses Unity 5.6 beta; newer Unity versions can introduce serialization, shader, API and physics differences.
- No APK or playable port exists until a build is produced and tested on a device.

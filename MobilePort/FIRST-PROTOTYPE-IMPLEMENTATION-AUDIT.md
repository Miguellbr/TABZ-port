# First playable prototype — implementation audit (v5.1 baseline)

This is the source-level implementation checklist for the first offline Android slice. The audit was run against both recovered managed assemblies; it is not a Unity build or device test.

## Decision: reuse the original inventory, rebuild the mobile input layer

The original game already has a real inventory system. Replacing it with a fake inventory would throw away useful behavior, so the prototype should retain `InventoryService`, `InventoryItem`, `InventoryItemWeapon`, `UIInventory` and `WeaponHandler` wherever possible. The mobile HUD should call into that system rather than maintain a separate, conflicting inventory.

### Confirmed inventory behavior

- `InventoryService` owns a 16-slot inventory and a 6-slot hotbar, supports add/remove/set/get and raises `OnItemAddedE`.
- `UIInventory` owns the original inventory UI, tooltip, hotbar visuals, selection and drag/drop logic. It reads I/Tab and number keys, and creates test items through `PhotonNetwork.Instantiate` in `Start()`.
- `InventoryItem.TryPickup()` is the pickup entry point; the player looks at items with a center-screen `Physics.SphereCast` and currently presses E.
- `InventoryItemWeapon` stores ammo type and magazine count. `Weapon.SetWeaponInventoryItem()` links the weapon to its inventory item and `Weapon.UpdateUI()` refreshes ammo UI.
- The original UI is tightly coupled to Photon ownership, Photon-based test-item spawning and FMOD click audio. These must be guarded/replaced in offline mode; merely showing the Canvas will not make the inventory work offline.

## Confirmed combat / AI dependencies

- `Weapon` implements semi-auto/automatic firing, reload and hit processing. Its firing path invokes `PhotonView.RPC("Fire", PhotonTargets.All, PhotonNetwork.player)`; that cannot be the only firing route in an offline build.
- `HealthHandler.TakeDamage(float, PhotonPlayer, bool)` updates health and triggers death. Zombie damage/death uses the same general handler, but initialization and death callbacks contain Photon assumptions.
- `ZombieFighting`, `ZombieBlackboard`, behavior-tree classes, `NavmeshBehavior`, `ZombieSpawner` and `NetworkZombieSpawner` form the zombie side. AI navigation requires a valid baked NavMesh in the test scene; recovered character meshes alone are not functioning enemies.
- Player movement is ragdoll-based. `PlayerHandler` reads WASD/Shift/Space and delegates to `MovementHandler` and `PhysicsAmimationController`. Keep those physics/animation methods; replace only the input decision layer.
- `CameraMovement` reads mouse deltas and mouse-right aim. Touch look and aim state must enter those same paths.

## Required implementation order

1. **Create a minimal offline gameplay scene** with a ground plane, camera, light, EventSystem, local player and a single enemy. Do not load the 19k-GameObject full map yet.
2. **Offline boot path:** no Steam initialization, no server browser, no required Photon room. Add local spawn/ownership branches and replace FMOD calls with Unity AudioSource/no-op wrappers for the prototype.
3. **Mobile input adapter:** movement vector, touch-look delta, fire-held plus fire-pressed, aim, jump, sprint, reload, interact, inventory toggle and six hotbar slot taps. Preserve keyboard/mouse behavior in editor/desktop builds.
4. **Pickup + original inventory:** use the original center-screen interaction ray; route USE to `TryPickup()`; route inventory/hotbar actions into `UIInventory`/ `InventoryService`. Remove or guard `UIInventory.Start()`'s Photon test-item spawning.
5. **Weapon loop:** local fire path, ammo/magazine UI, reload, projectile/raycast hit and damage. Keep Photon RPC path behind online mode.
6. **One zombie:** valid prefab, Animator/rig, collider/hitboxes, AI behavior, NavMesh, health, hit/death animation and despawn. Then add variants through the DEV spawn menu.
7. **Only after that:** test real inventory artwork/layout and import more original UI assets; expand enemy count and profile phone performance.

## UI decision

Port/reuse the original inventory/hotbar visuals if their serialized Canvas, fonts, sprites and prefab references survive extraction. If those references cannot be reconstructed reliably, use a compact runtime-generated mobile panel as a temporary shell, but still bind it to the original `InventoryService`. The inventory contents and equip logic must not be duplicated.

## Current blockers (specific, not generic)

- The repo contains recovered C# text and exported meshes/textures, not a valid editable Unity project with the original scene/prefab references.
- The mobile HUD scripts are scaffolding and are not yet wired into `PlayerHandler`, `CameraMovement`, `Weapon`, `PlayerInteraction` or `UIInventory`.
- The debug spawn menu has no verified working original prefabs assigned.
- No Android build has been compiled or tested yet.

## Acceptance criteria for prototype 0.1

- APK opens straight into an offline test scene without Steam/Photon login.
- Player moves and looks using two touch zones; desktop input still works in editor.
- USE picks up one real weapon item; the original hotbar/inventory reflects it.
- Fire, aim and reload respond to touch; ammo UI changes correctly.
- One zombie can approach, take damage, die, and be reset/spawned again from DEV menu.
- No map reconstruction or multiplayer lobby is required.

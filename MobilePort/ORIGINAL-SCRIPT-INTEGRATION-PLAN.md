# Original TABZ Script Integration — First Pass

## Verified source recovery

Actions run: [38073767323](https://github.com/Miguellbr/TABZ-port/actions/runs/38073767323)  
Artifact: `tabz-assetripper-v5.1-Unity.zip`

The export contains real decompiled C# source for the original game. Spot checks confirmed substantive method bodies in:

- `PlayerHandler.cs` — original character movement/state orchestration; also depends on `MovementHandler`, `StandHandler`, `PhysicsAmimationController`, Photon and FMOD.
- `MovementHandler.cs` — original physics/ragdoll movement and gravity; relies on the original character hierarchy and components.
- `CameraMovement.cs` — camera target/rotation behavior; uses Photon ownership and original camera transforms.
- `Weapon.cs` — weapon fire/reload state, projectile references, ammo/inventory and FMOD feedback.
- `InventoryItemWeapon.cs` — weapon ammo type and magazine state.
- `PlayerInteraction.cs` — center-screen sphere cast and pickup logic; currently checks keyboard key E.
- `NetworkManager.cs` — Photon room/network spawning and Steamworks references.

## Integration decision

Do **not** copy the recovered `Assembly-CSharp` scripts wholesale into `MobilePort/` yet. They are real source, but they are not a self-contained offline/mobile project: they reference Photon PUN, Steamworks, FMOD, original prefabs, animation rigs, and scene components. Copying them directly would create compile errors and would not make the original gameplay run.

The existing offline prototype already has:
- `MobileInputState` for movement/look/fire/aim/jump/reload/interact/hotbar events;
- `OfflinePrototypePlayer` for standalone movement and camera look;
- `OfflinePrototypeWeapon` for local hitscan, ammo and reload;
- `MobileControlsBootstrap` for generated touch controls.

## Ordered integration plan

1. **Preserve the runnable offline baseline.** Keep the current local controller and weapon as the fallback while porting individual systems.
2. **Use recovered source as the behavior reference.** Compare original weapon configuration and firing logic, then bring over data and safe logic that does not require Photon/FM​​OD.
3. **Connect actual recovered assets.** Map the original weapon and character prefab references to exported meshes/materials, first checking whether a mesh can be instantiated without network scripts.
4. **Add mobile input at the seams.** Patch the original movement, camera, interaction, and weapon input sites to consume `MobileInputState`; avoid trying to override Unity's static `Input` API.
5. **Strip online-only dependencies in a separate offline adapter.** Replace Photon ownership/RPC/spawning with local calls, and replace Steamworks/FMOD dependencies with Android-safe alternatives where needed.
6. **Compile and test in Unity before claiming an APK.** Current CI source checks are not equivalent to Unity compilation or an Android build.

## Known blockers

- AssetRipper reported an unresolved `WorldCreatorEngine.dll` reference and a UI `InputField` component parse issue.
- The export has not yet been imported and compiled in Unity Editor.
- No Android APK has been built or device-tested from this recovered project.

This report records what has been verified and prevents a successful export job from being mistaken for a working Android port.

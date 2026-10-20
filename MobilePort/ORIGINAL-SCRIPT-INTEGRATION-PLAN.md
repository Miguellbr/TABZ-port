# Original TABZ systems — integration requirements

This document defines how the original compiled game systems must be integrated into the Android port. It supersedes the earlier "first pass" plan that proposed using the simplified offline controller as a fallback or rebuilding original behavior incrementally from decompiled source.

## Non-negotiable integration decision

The target is the **original TABZ v5.1 player and gameplay systems**, running locally on Android after narrowly scoped compatibility changes.

Do not replace the original player with `OfflinePrototypePlayer`. Do not reconstruct the arms, IK, camera, movement, animation rig, weapon or zombie behavior from a superficial prefab inspection. The original player system is more interconnected than the serialized component list suggests.

The simplified prototype under `MobilePort` stays separate. Its touch HUD/build experiments may inform platform input work, but its player/weapon/zombie scripts are not the implementation of the original port.

## Original systems already identified

Prior AssetRipper/source inspections found substantive original implementations including:

- `PlayerHandler` — player state and movement orchestration; dependencies include movement/standing/physics-animation systems, Photon and FMOD.
- `MovementHandler` — original physics, movement and ragdoll behavior; depends on the original character hierarchy and components.
- `CameraMovement` — camera target/rotation behavior and original camera transforms; includes Photon ownership assumptions.
- `Weapon` — original firing/reload state, projectile references, ammunition/inventory and audio feedback.
- `InventoryItemWeapon` — weapon ammunition and magazine state.
- `PlayerInteraction` — pickup/interact logic; recovered implementation includes keyboard input that needs a mobile seam.
- `NetworkManager` — Photon room/network spawning and Steamworks references.

Prefab audit also identified player-side classes such as `PlayerHandler`, `MovementHandler`, `CameraMovement`, `WeaponHandler`, `UIInventory`; zombie-side classes such as `NetworkZombie`, `ZombieFighting`, `EyeSensor`, `HearSensor`; and AK components `InventoryItemWeapon` and `PhotonView`.

These names are an inventory starting point, not proof that every dependency or initialization path has been recovered. Use the latest AssetRipper export and DLL metadata as the source of truth.

## Required investigation before integration

Build a dependency/initialization map covering:

1. Original player spawn path and required scene services.
2. Photon PUN1 offline-mode initialization, ownership checks, instantiation and RPC behavior.
3. Player root, child hierarchy, arms, IK, animator/avatar, camera targets and physics/ragdoll relationships.
4. Movement and camera update order.
5. Inventory/hotbar initialization and how the AK is registered/equipped.
6. Weapon/projectile/ammo/reload/audio dependencies.
7. Zombie spawn path, sensors, behavior/AI, navigation, combat and ragdoll.
8. Terrain-related scene requirements, including `TerrainMaterialChecker`.
9. Native/platform dependencies from Steamworks and FMOD.

Inspect original DLLs and actual prefab/scene serialization. Do not infer missing behavior from names alone.

## DLL and script-reference policy

- Prefer the original compiled `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll` and required managed dependencies as the runtime implementation.
- Keep pristine originals separate from patched copies and record hashes.
- Patch only identified input/platform calls; do not rewrite the game logic wholesale.
- Preserve every original MonoBehaviour component. If AssetRipper cannot resolve a script reference, map the class to the exact assembly, namespace and class in the original DLL and restore the serialized reference.
- The MD4 fileID formula was checked against `WorldCreatorEngine.Utilities.WorldCreatorReference`. Validate all other mappings independently against DLL type metadata. The one known match is not blanket proof.
- Keep unresolved/ambiguous references intact and list them in a report. Never delete them, substitute empty scripts, or guess a mapping.
- Do not copy all decompiled C# into the Unity project as a replacement for the original assemblies.

## Android/offline compatibility policy

- Route touch input through a dedicated shim/adapter at verified input call sites; retain original behavior outside Android where possible.
- Stub Steamworks only where the Android build cannot provide the native API. Identify any remaining call paths that could fail at runtime.
- Check FMOD native library architecture/platform compatibility before retaining it. A silent stub is a compatibility fallback, not proof audio is ported.
- Keep Photon PUN1 and test its actual offline player-spawn flow. Offline mode does not automatically neutralize every ownership, RPC or room dependency.
- Use local adapters only for network/platform seams. They must call into the original player, zombie, inventory and weapon systems rather than implement substitutes.

## Minimal playable slice

Create a separate `TabzMinimal` project with:

- A real Terrain compatible with the original terrain-checking system.
- Runtime NavMesh generation for the original zombie, using settings verified from source/assets/API requirements.
- The original player with original arms, camera, movement and animation system.
- One original zombie with original AI/combat/ragdoll.
- A respawn-zombie button that invokes a verified spawn adapter.
- The original AK registered through the original inventory/hotbar path.
- A small on-screen diagnostic log with stack traces and a copy button.

Preserve source prefab/scene values. Do not invent transforms, scales, rotations, NavMesh settings, weapon offsets or component values. If an item cannot be established from the recovered data, document it and test rather than guessing.

## Current pipeline status

- AssetRipper discovery/export succeeded in run [39356526358](https://github.com/Miguellbr/TABZ-port/actions/runs/39356526358).
- The packaging run [39356526420](https://github.com/Miguellbr/TABZ-port/actions/runs/39356526420) successfully patched its DLL copies and completed the AssetRipper export.
- Packaging stopped because the referenced template directory `Tools/Minimal/project` was missing. No `TabzMinimal` project was published.
- The packaging script's scene-root selection and synthesized scene header have not yet been validated against the original scene; audit them before relying on generated scene data.
- No successful Unity import/compile or Android runtime validation of the original player, arms and offline spawn flow has yet been demonstrated.

## Acceptance order

1. Complete prefab/script/dependency mapping.
2. Validate DLL class mappings and serialized script references.
3. Correct the packaging template and verify scene/asset preservation.
4. Import and compile `TabzMinimal` in Unity.
5. Test original player initialization and offline spawn, including arms/camera/movement.
6. Test original AK/inventory and zombie AI/navigation.
7. Add diagnostics, build Android and test on device.

Never report a later gate as complete merely because an earlier static or packaging step succeeded.

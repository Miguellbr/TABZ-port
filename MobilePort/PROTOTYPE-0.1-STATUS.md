# Prototype 0.1 — status

Last source audit: 2026-10-10. Baseline: recovered TABZ v5.1 managed assembly.

## Done in repository

- Recovered and decompiled both managed assemblies; automated gameplay audit completed successfully.
- Confirmed the original inventory model: 16 inventory slots, 6 hotbar slots, item icons, tooltips, drag/drop, equip event, ammo UI.
- Extended `MobileInputState` with separate fire-pressed/fire-held states, inventory toggle and six hotbar slot taps.
- Extended the runtime touch HUD with INV and six hotbar buttons.
- Added `MobileInventoryBridge`, which calls the original `UIInventory.ToggleOpen` and hotbar-selection method through reflection so the original UI/InventoryService remains the source of truth.
- Fixed the touch HUD bootstrap so an existing original Canvas does not prevent the mobile HUD from being created.
- Added an isolated offline prototype scaffold: runtime ground/player/camera, touch HUD, hitscan rifle with ammo/reload, a damageable chasing zombie, a respawn button and a toggleable placeholder inventory panel.
- Added automated source-contract checks; GitHub Actions run `38007971246` passed.
- Added setup instructions in `MobilePort/OFFLINE-PROTOTYPE-README.md`.

## Not yet done / do not treat as working

- No complete Unity project has been reconstructed/imported and no Android APK has been compiled. The offline sandbox is source code that must still be compiled and run by Unity.
- The inventory bridge is not device-tested. It expects the recovered v5.1 private members `ToggleOpen`, `UpdateHotbarSelection`, and `m_selectedHotbarIndex`.
- Touch state is not yet consumed by the original `PlayerHandler`, `CameraMovement`, `Weapon`, or `PlayerInteraction` classes.
- Original item/zombie prefabs have not yet been proven to instantiate without Photon.
- The original inventory's test-item spawn path uses `PhotonNetwork.Instantiate` and must be disabled in offline mode.
- The offline sandbox is a test harness, not yet a playable TABZ port. Its weapon/zombie/inventory visuals are placeholders; the original inventory bridge is separate and untested.

## Next implementation steps

1. Patch the player input branch while preserving the original ragdoll movement calls.
2. Patch camera look/aim to consume touch look deltas and aim-held state.
3. Patch pickup interaction to consume USE and change the prompt to touch-friendly text.
4. Patch `UIInventory` so mobile inventory toggle and slot taps are first-class input (the reflection bridge is a compatibility fallback).
5. Add an explicit offline runtime mode around Photon, Steam and FMOD dependencies.
6. Reconstruct one weapon pickup and one zombie prefab, then connect local fire/damage/death.
7. Only after a minimal scene runs, attempt an Android build and test it on the phone.

## Acceptance target

A small offline test scene with touch movement/look, original inventory/hotbar, one working weapon and one killable zombie. The full map, multiplayer, Steam and broad content coverage are intentionally out of scope for prototype 0.1.

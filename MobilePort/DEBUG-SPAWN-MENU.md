# Test build: runtime spawn menu

The Android test build should include an optional **DEV** button that opens a spawn menu. It is a testing tool, not part of the final player-facing UI.

## First version

- Spawn any recovered prefab assigned in the Inspector (zombies, special enemies, player dummies, weapons/items).
- Spawn all assigned entries for quick stress tests.
- Remove everything spawned by the menu to reset a test.
- Display missing prefab assignments clearly instead of silently spawning nothing.
- Work with touch input on Android; keep **F1** as a desktop/editor toggle.
- Cap tracked spawned instances to reduce accidental performance/memory problems on the phone.

## Implementation status

`Assets/Scripts/Mobile/DebugSpawnMenu.cs` provides the runtime UI and generic prefab spawning. It is deliberately generic because we have not yet confirmed that the recovered TABZ character assets can be rebuilt as working prefabs with their original scripts, rigging, and AI. Unassigned entries remain disabled; this avoids pretending that a model export is a functioning zombie.

## Suggested menu groups once prefabs are recovered

1. **Enemies:** regular zombie variants, fast/thin/wide/tiny zombies, king/giant variants.
2. **Weapons & items:** each recovered weapon, ammo, pickup and interactable.
3. **Player tests:** movement, jump, sprint, camera look, fire/reload, interaction.
4. **World tests:** spawn distance, enemy count/performance, reset spawned objects.

The current script only implements generic prefab spawn/reset. Function-specific test buttons should be wired after the offline player and recovered gameplay components are running; health, weapon, and AI methods must not be guessed from asset names alone.

## How to enable it

Attach `DebugSpawnMenu` to a scene GameObject in a Unity development/test scene, then add `SpawnEntry` rows and assign actual prefabs. The script self-disables in non-editor, non-development builds. It has not yet been compiled in Unity or tested on a device.

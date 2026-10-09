# Test build: runtime spawn menu

The Android test build should include an optional **DEV** button that opens a spawn menu. It is a testing tool, not part of the final player-facing UI.

## Design assumption

TABZ appears to be heavily weapon/pickup-focused: most interactable objects seem to be weapons, with structures and enemies making up much of the remainder. Treat this as a working assumption until the recovered gameplay code and prefabs confirm the complete list. The menu should therefore prioritize spawning and testing weapons, rather than building a large generic interaction framework first.

## First version

- **Weapons first:** spawn recovered weapon pickups, then test pickup, equip, aim, fire, reload and drop if those actions exist in the recovered implementation.
- Spawn enemy variants and structures as separate categories.
- Spawn all assigned entries for quick stress tests.
- Remove everything spawned by the menu to reset a test.
- Display missing prefab assignments clearly instead of silently spawning nothing.
- Work with touch input on Android; keep **F1** as a desktop/editor toggle.
- Cap tracked spawned instances to reduce accidental performance/memory problems on the phone.

## Implementation status

`Assets/Scripts/Mobile/DebugSpawnMenu.cs` provides the runtime UI and generic prefab spawning. It is deliberately generic because we have not yet confirmed that the recovered TABZ character assets can be rebuilt as working prefabs with their original scripts, rigging, and AI. Unassigned entries remain disabled; this avoids pretending that a model export is a functioning zombie.

## Suggested menu groups once prefabs are recovered

1. **Weapons:** one entry per recovered weapon/pickup; use these to validate interaction, equip, firing, aiming, ammo and reload.
2. **Enemies:** regular zombie variants and special types (including thin/wide/tiny, king/giant if confirmed as actual spawnable prefabs).
3. **Structures & world objects:** recovered structures and objects that can be spawned independently.
4. **Player/function tests:** movement, jump, sprint, camera look, damage/health, fire/reload and pickup interaction.
5. **Stress/reset:** spawn multiple enemies or weapons, then remove all objects created by the menu.

The current script only implements generic prefab spawn/reset. Weapon-specific actions and enemy AI tests should be wired after the offline player and recovered gameplay components are running; health, weapon, and AI methods must not be guessed from asset names alone.

## How to enable it

Attach `DebugSpawnMenu` to a scene GameObject in a Unity development/test scene, then add `SpawnEntry` rows and assign actual prefabs. The script self-disables in non-editor, non-development builds. It has not yet been compiled in Unity or tested on a device.

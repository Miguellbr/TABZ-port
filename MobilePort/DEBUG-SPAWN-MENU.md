# Test build: runtime spawn menu

The Android test build should include an optional **DEV** button that opens a spawn menu. It is a testing tool, not part of the final player-facing UI.

## Scope decision

The first playable prototype focuses on **weapons and enemies only**. Full map/structure testing is deferred until later. The original gameplay scene inventory contains an `ItemSpawns` root with `WorldItemAdder`, plus `Map/WorldSpawn`, `Map/SpawnPoints city1`, `Map/SpawnPoints city2`, and `Map/singleSpawn`. This supports investigating the original spawn system, but does not yet prove the exact item distribution rules.

The working hypothesis is that most interactables are weapons and that ordinary weapon pickups are distributed around points of interest (POIs), with a possible special weapon tied to the demon mountain. This should be verified against the recovered `WorldItemAdder`/spawn scripts and serialized references before recreating the map's item placements. The special weapon's name and exact condition are not yet confirmed.

## First version

- **Weapons first:** spawn recovered weapon pickups, then test pickup, equip, aim, fire and reload.
- **Enemies second:** spawn individual enemy types and, once stable, several at once for combat and performance testing.
- Remove everything spawned by the menu to reset a test.
- Display missing prefab assignments clearly instead of silently spawning nothing.
- Work with touch input on Android; keep **F1** as a desktop/editor toggle.
- Cap tracked spawned instances to reduce accidental performance/memory problems on the phone.

## Suggested menu groups once prefabs are recovered

1. **Weapons:** one entry per recovered weapon/pickup; validate pickup, equip, fire, aim, ammo and reload.
2. **Enemies:** normal zombie types and confirmed special types (thin/wide/tiny, king/giant only if the serialized assets confirm them as usable spawnable objects).
3. **Combat tests:** enemy count, damage/death checks, weapon switching, reset.
4. **Later — map tests:** structures, POIs, normal item distribution and the suspected demon-mountain special weapon. Do not make this a blocker for the first playable prototype.

## Implementation status

`Assets/Scripts/Mobile/DebugSpawnMenu.cs` provides generic runtime UI and prefab spawning. It is deliberately generic because we have not yet confirmed that the recovered TABZ character assets can be rebuilt as working prefabs with their original scripts, rigging, and AI. Unassigned entries remain disabled; this avoids pretending that a model export is a functioning zombie.

The current script only implements generic prefab spawn/reset. Weapon-specific actions and enemy AI tests should be wired after the offline player and recovered gameplay components are running; their methods must not be guessed from asset names alone.

## How to enable it

Attach `DebugSpawnMenu` to a scene GameObject in a Unity development/test scene, then add `SpawnEntry` rows and assign actual prefabs. The script self-disables in non-editor, non-development builds. It has not yet been compiled in Unity or tested on a device.

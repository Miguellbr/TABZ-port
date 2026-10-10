# TABZ v5.1 AssetRipper recovery — first asset audit

Source: GitHub Actions run 38070704298, artifact `tabz-assetripper-v5.1-Unity`.
- AssetRipper completed export successfully (905/905 export units).
- Export contains 3,089 files and is about 606.7 MiB unpacked.
- The exported project targets Unity 5.6.0b9 (some files report 5.6.0b6).

## Important limitation discovered

The first run explicitly used `--script-content-level Level0`. AssetRipper's log says `Script import disabled by settings`. Therefore many recovered C# files for game-specific MonoBehaviours are placeholder/dummy classes, not decompiled implementations. Examples include `ZombieBlackboard`, `ZombieFighting`, `NetworkZombie`, `MovementHandler`, `PlayerHandler`, `PhysicsAmimationController`, `Weapon`, `InventoryItemWeapon`, `HealthHandler`, `EyeSensor`, and `HearSensor`.

This means the export is useful for Unity asset structure, prefabs, meshes, materials, animation clips, and scene data, but it is not yet a runnable offline game project. Do not rely on those placeholder scripts for gameplay.

The recovery workflow was updated in commit `96a11259615188317f02063d5b37aab31dc468f0` to request `Level2` script content on the next run. That run must be checked for actual non-placeholder code and successful script import before considering the issue solved.

## Useful recovered prefab inventory

### Player / character
- `Resources/TABS_Player_Network.prefab`
- `Resources/Player.prefab`
- `Resources/Corpse.prefab`

### Zombie variants
- `TABZOMBIE`
- `TABZOMBIECreepy`
- `TABZOMBIEGIANT`
- `TABZOMBIEGOAT`
- `TABZOMBIEGOATGiant`
- `TABZOMBIEHEAD`, `TABZOMBIEHEAD2`
- `TABZOMBIEKing`
- `TABZOMBIELONG`
- `TABZOMBIELine`
- `TABZOMBIERIDER`, `TABZOMBIERIDER2`
- `TABZOMBIESMALL`
- `TABZOMBIETHIN`
- `TABZOMBIETINY`
- `TABZOMBIETORSO`
- `TABZOMBIEWIDE`
- `TABZOMBIE_BOSS`

Most zombie prefabs contain many original gameplay components, colliders, mesh references, and child objects. The presence of Photon components and game-specific scripts means we should not instantiate them directly in an offline mobile scene without replacing/stripping network behavior.

### Weapon / item prefabs
Located in `Resources/items/`:
- `Ak74`, `Annoyer`, `Axe`, `BigSniper`, `DoubleBarrel`, `EvilGun`
- `Homerun Gun`, `HuntingKnife`, `HuntingSniper`, `Makarov`, `Mp5`
- `Musket`, `ÖverMusket`, `Pump`, `Repeater`, `Revolver`, `Scorpion`
- `SquareBrawl`, `TheGrabberShot`
- Ammunition prefabs: `AmmoBig`, `AmmoMedium`, `AmmoShell`, `AmmoSmall`

Weapon item prefabs have original mesh references and `InventoryItemWeapon` components. Their Photon components need an offline replacement, but the models are promising candidates for a first visual integration.

### Other useful assets
- 25 AnimationClip assets
- 4 AnimatorController assets
- 7 Avatar assets
- 265 Mesh assets
- 92 Material assets
- 49 Texture2D assets

## Recommended next implementation sequence

1. Re-run AssetRipper at Level2 and verify script reconstruction.
2. Produce a visual-only asset pack for a small initial set: one regular zombie, one special zombie, and 3 weapons.
3. Import those assets into the mobile Unity project while preserving their .meta GUIDs and dependencies.
4. Build an offline adapter layer: local zombie AI/health/damage and local weapon firing/inventory; do not carry Photon dependencies into Android.
5. Validate in Unity Editor before attempting an APK.

No Android build or in-Editor validation has been performed yet.

# Character recovery follow-up — 2026-10-10

Latest successful run: [TABZ character asset recovery](https://github.com/Miguellbr/TABZ-port/actions/runs/38046695491)

Artifact: [tabz-character-reference-map](https://github.com/Miguellbr/TABZ-port/actions/runs/38046695491/artifacts/11668095543)

## What improved

The updated scanner now resolves a SkinnedMeshRenderer's owning GameObject and records Transform parent/child links. The latest output contains 84 SkinnedMeshRenderer objects, 397 Animator objects and 33 AnimationClip objects per build, with zero container parse errors.

Examples of resolved skinned renderers:
- GameObject `Robot2` → mesh `Robot2`, 48 bone references, material `Robot_Color`.
- GameObject `Bip001 Pelvis` → mesh `construction_worker`, 54 bone references, material `constructor_done`.
- GameObject `Model` → mesh `RealHumanBean`, 11 bone references, often material `Zombie` or `ZombieMenu`.

Transform references now resolve through their GameObjects. For the Robot2 rig, the hierarchy includes `Ribs`, `Neck`, `Head`, and `Left_Shoulder_Joint_01`. The bone references in the renderer records still have blank Transform names, but the corresponding Transform records can be joined by container + path ID to recover their GameObject names.

## Limits

- The exported OBJ files are static geometry previews; they do not preserve Unity skin weights, rig binding or animation.
- The 33 clips include names such as `attack`, `die`, `fall`, `gothit`, `idle`, `jump` and walk/run variants, but this scan does not prove which clip is correctly bound to which zombie prefab.
- Animator controller pointers are often null or external in this scan. No original character has been imported into Unity or tested in motion yet.
- Both game archives have effectively matching asset inventories, so start with one version for the first import attempt.

## Next step

Run the [AssetRipper recovery workflow](https://github.com/Miguellbr/TABZ-port/actions/workflows/assetripper-recovery.yml) with `v5.1` and `Unity`. This is a separate, currently unvalidated reconstruction attempt. A successful export would still need to be opened and repaired in Unity before we can claim the character or project is usable.

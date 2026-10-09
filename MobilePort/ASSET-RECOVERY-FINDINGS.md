# TABZ Android port — asset recovery findings

## Current result

The automated Unity asset scan completed successfully for both supplied Windows builds.

- Each build exposed 110,962 serialized Unity objects across 11 containers.
- UnityPy exported 270 OBJ mesh files per build and 126 PNG textures per build in the initial sample.
- The scan also found 66 AnimationClip objects per build, 168 SkinnedMeshRenderer objects per build, 794 Animator objects per build, and 276 Texture2D objects per build.
- Useful named mesh assets include `RealHumanBean`, `construction_worker`, `Robot2`, `muktar_2`, `MP5`, `AK_47`, `Winchester_sniper`, `repeater`, `musket`, `pistol1`, `shotgun1`, and `knife2x`.
- Useful texture names include `CharacterSheet`, `PickCharTexture`, `Robot_Color`, and `muktarcolorgoed_1`.

Artifact: [TABZ Unity asset export](https://github.com/Miguellbr/TABZ-port/actions/runs/38005103326).

## Important caveats

1. An OBJ is static geometry. It does not preserve Unity's rig, skin weights, animation controller, or prefab/component wiring, so it is not enough by itself to recreate the animated player or zombies.
2. The initial exporter used path ID + object name for filenames. Unity assets can reuse those values in different containers, so exports could overwrite each other. This was corrected to include the source container in the filename.
3. The initial report's vertex counts were incorrectly reported as zero because it queried the wrong UnityPy property. The exporter has been updated to query the parsed mesh vertex data and is being rerun.
4. The serialized scene is not an editable Unity project. Rebuilding the complete 19k-object gameplay scene from references and components is a separate reconstruction task.
5. The two Windows builds have matching high-level asset counts, so v5.1 remains the main source baseline; v4 can be used to compare changed scripts and assets.

## Recommended reconstruction order

1. Preserve and export renderer-linked meshes/materials/textures, particularly skinned renderers for character models.
2. Map each player/zombie prefab-like object to its mesh, Animator, animation clips, colliders, and script references.
3. Build a small offline test scene with one player, one zombie, a floor, and a simple weapon. Do not import the whole map yet.
4. Adapt the first-person camera and player movement to the mobile input facade; connect touch fire/reload/use to the existing game logic.
5. Remove Steam startup requirements and isolate Photon-dependent code so an offline prototype can spawn locally. Replace or bypass FMOD only after confirming the required audio-bank/native-plugin dependencies.
6. Only after the scene and controls work in Unity should the Android build workflow produce an APK.

## Current status

The asset scan is evidence that reusable geometry, textures, and animation data exist in the shipped files. It is not yet proof that a complete playable port or APK can be assembled automatically. The touch UI scripts in `MobilePort/Assets/Scripts/Mobile` are scaffolding and still need to be wired into the recovered gameplay code.

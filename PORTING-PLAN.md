# TABZ Android port — reconstruction plan

## Current evidence

- The supplied builds are Windows builds made with Unity **5.6.0 beta** and Mono, not a source Unity project.
- The main gameplay assembly is managed C# (`Assembly-CSharp.dll`); static decompilation completed in GitHub Actions at roughly **56,000 lines** for the older build. The analysis report lists gameplay systems including zombie AI/behavior trees, inventory, weapons, movement, spawning, UI, saves, and Photon networking.
- The existing assemblies contain many direct Unity API references and extensive Photon usage. Windows-specific integration includes Steamworks and Windows FMOD plugins. These integrations must be isolated or replaced for Android.
- The builds contain Unity serialized asset containers, large shared asset/resource files, scenes/levels, and FMOD bank files. The asset inventory workflow is intended to identify which resources UnityPy can parse and export.
- The two versions are stored in branches `tabz-v4` and `tabz-v5.1`; archives are downloaded by Actions so the phone does not need to store the roughly 1 GB extracted payload.

## Strategy

1. **Inventory before rebuilding.** Produce per-version object inventories, scene/container reports, script type names, texture dimensions, audio export samples, and parser failures. Compare v4 and v5.1 to choose the better baseline.
2. **Recover project resources.** Export compatible textures/audio/meshes and map serialized object references. Keep large extracted payloads as temporary Actions artifacts instead of committing gigabytes into Git.
3. **Build a minimal Unity project scaffold.** Use the closest practical Unity version that can read these serialized assets; do not assume Unity 5.6 beta project compatibility with a current Editor. Preserve metadata and source DLLs separately for analysis.
4. **Make the first Android vertical slice.** Aim first for app launch, one scene/map, camera, touch controls, one player, and local zombie/weapon loop. Temporarily disable Steamworks and multiplayer instead of trying to port them all at once.
5. **Restore features in order.** Input/UI -> movement and physics -> zombies and spawning -> weapons/inventory -> audio -> save/settings -> Photon multiplayer, if the legacy networking setup can be made Android-compatible.
6. **Build and test on device.** Android APK creation is not evidence of a working port. We need successful Gradle/Unity builds and device tests for launch, graphics, touch input, performance, and crashes.

## Risks and non-goals

- A Windows player build is not itself an importable Unity project; serialized assets do not preserve all original editor-side authoring data in a directly reusable form.
- Decompiled C# can help reconstruct behavior but may need reference fixes and substantial scene/prefab wiring. Decompiled source is not automatically a clean, buildable project.
- Steam APIs, Windows native DLLs, FMOD platform libraries, input, graphics, and Photon configuration are likely porting blockers.
- The initial goal is a playable offline Android prototype, not feature parity or multiplayer on the first APK.
- Do not commit full extracted game payloads or decompiled source dumps to the public repository. Keep generated reports and reproducible scripts in Git; use expiring Actions artifacts for large intermediate exports.

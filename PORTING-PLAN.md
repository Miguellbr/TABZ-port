# TABZ Android port — official strategy

## Objective

Produce a first playable Android build by recovering and adapting the **original TABZ v5.1 systems**. This is not a redesign and not a replacement game built around simplified scripts.

The priority is to preserve the original player, arms, movement, camera, animation rig, inventory, weapons, zombie AI, combat and ragdoll. Adapt only the platform/network seams that demonstrably prevent local Android execution.

The existing simplified Android prototype remains separate. It is useful as a touch-control/build experiment, but it is **not** the player or gameplay foundation for the original-systems port.

## Source and recovery baseline

- Original baseline: TABZ v5.1, Unity 5.6.0 beta / Mono.
- Main managed assemblies include `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll` and `Photon3Unity3D.dll`.
- AssetRipper exports the serialized assets, scenes, prefabs, metadata and recovered script information. The export is not assumed to be a complete, ready-to-build Unity project.
- Large source archives and intermediate exports should remain in expiring GitHub Actions artifacts; keep reproducible tools, scripts and concise reports in Git.
- The current work is on `tools/il-patch-input`; the intended isolated output project/branch is `TabzMinimal` / `tabz-minimal`.

## Official implementation strategy

### 1. Discover and map before changing assets

Re-export v5.1 with AssetRipper when the previous artifact expires. Audit at minimum:

- `TABS_Player_Network`
- `TABZOMBIE`
- `Resources/items/Ak74` (confirm the actual exported path)

For each prefab, report its path, file size, GameObject/component counts, every MonoBehaviour class, source assembly, GUID/fileID, and dependencies (prefabs, models, materials, textures, animation clips/controllers, avatars, audio and other assets).

Trace the actual original runtime flow, not just serialized component names. In particular map player initialization/spawn, Photon ownership/offline behavior, arms/IK/animation, camera, movement/ragdoll, inventory/hotbar, weapon handling, zombie AI/sensors/combat and scene services.

### 2. Preserve original DLL-backed MonoBehaviours

Keep the original managed gameplay assemblies as the implementation of the game wherever feasible. Do not copy decompiled scripts wholesale into the Unity project as a substitute for the compiled assemblies.

When AssetRipper leaves a MonoBehaviour script reference unresolved, recover the correct assembly, namespace and class from the original DLL/type inventory, then remap the serialized script reference to the matching DLL plugin GUID and Unity script fileID.

The MD4 fileID calculation has been checked against the known `WorldCreatorEngine.Utilities.WorldCreatorReference` value. That single check validates the calculation for that example; it does **not** prove every class/assembly mapping or every remapped prefab is correct. Validate mappings against actual DLL type metadata and report every unresolved or ambiguous reference. Never fabricate a class association.

Do not delete missing-script components, replace them with empty scripts, or remove their serialized data merely to make a prefab import. The earlier AK component deletion is not an acceptable pattern.

### 3. Patch only proven platform seams

- Redirect identified legacy input calls to the Android touch-input shim while retaining desktop behavior where practical.
- Stub or isolate Steamworks calls that cannot exist on Android.
- Handle FMOD separately: the available native libraries/platform support must be checked before deciding whether to retain, replace or silence audio.
- Keep Photon and its gameplay logic. Configure PUN for offline mode and adapt only the specific online room/RPC/ownership/spawn paths that prevent local play.
- Do not assume that enabling Photon offline mode alone makes player spawning work. Trace and test the original initialization sequence.

Patches are experimental until the resulting assemblies load and the relevant gameplay path runs in Unity/Android. Preserve hashes of source and patched DLLs in reports.

### 4. Build an isolated minimal project: `TabzMinimal`

Create a new project directory, independent of `MobilePort` and its simplified prototype. Package only the verified dependency closure required for the first vertical slice, while retaining the original serialized components and assets.

The first scene should provide:

- A real Unity Terrain, because the original `TerrainMaterialChecker` expects Terrain-related data/components.
- A runtime-generated NavMesh for the original zombie navigation, using verified agent settings and compatible navigation APIs.
- The original player prefab and its original arms/animation/camera/movement systems.
- One original zombie prefab and a UI action to respawn it through a controlled spawn adapter.
- The original AK prefab available through the original inventory/hotbar flow.
- Minimal scene services required by the original systems, discovered from the original scene and code.

Do not invent transforms, rotations, scales, weapon offsets, agent parameters, serialized component values or scene behavior. Preserve values from the source assets where possible. If a required value is absent, identify the missing evidence and design a test rather than guessing.

Do not create a substitute player, arms rig, weapon implementation or zombie AI. A small adapter is acceptable only at a verified platform seam and must delegate to the original systems.

### 5. Diagnostics

Include an in-game log panel that captures Unity log messages and exceptions, displays stack traces, and has a button to copy the complete log. This is important because device screenshots and manual log extraction are inconvenient.

### 6. Build Automation

Once `TabzMinimal` exists and its project structure is verified, prepare a separate Build Automation configuration using a new branch and subfolder. Do not guess the user's exact Build Automation values; request/use the values the user supplies when the project is ready.

## Evidence and acceptance gates

Do not treat any of these as equivalent:

1. AssetRipper export succeeded.
2. Prefab/script/dependency audit succeeded.
3. DLL patching succeeded.
4. Unity imported the project and compiled the assemblies/assets.
5. The original player initialized in offline mode.
6. The original arms, camera and movement worked.
7. The original AK/inventory and zombie AI worked.
8. An Android build installed and ran on device.

Report each gate separately with logs/artifacts. A successful static audit or APK build is not proof of playable gameplay.

## Current status and known blockers (2026-10-20)

- The discovery workflow has successfully re-exported v5.1 and generated a prefab/dependency report (run [39356526358](https://github.com/Miguellbr/TABZ-port/actions/runs/39356526358)).
- The packaging workflow run [39356526420](https://github.com/Miguellbr/TABZ-port/actions/runs/39356526420) successfully built the IL tools, patched the DLLs, downloaded the original archive and completed AssetRipper export.
- That packaging run then failed before copying any project assets because `Tools/Minimal/project` did not exist. Therefore no `TabzMinimal` project was published by that run.
- The current packaging script contains additional unverified assumptions (including its default scene roots and a synthesized scene header). Review these against the exported scene and original serialized data before treating the generated scene as faithful. Do not silently accept generated/default values as original values.
- The ID calculation has one known validation against `WorldCreatorReference`; broader mapping validation remains necessary.
- No successful Unity Editor import/compile or Android device runtime test of the original player path has been established by these workflow results.

## Immediate next steps

1. Review the latest discovery report and inspect the exact three prefabs plus dependency closure.
2. Fix the missing project template and audit the packaging script's scene construction/asset-copy logic before rerunning it.
3. Validate every remapped MonoBehaviour reference against the correct DLL and report unresolved references.
4. Generate the isolated project without discarding original components or inventing serialized scene values.
5. Import/compile in the intended Unity Editor version, then test the original player/offline spawn path before adding the zombie and AK.
6. Add the in-game copyable log panel and proceed to Android build/device testing.

## Non-goals

- Do not continue developing the simplified prototype as if it were the original port.
- Do not rebuild the original player or arms from prefab inspection alone.
- Do not pursue full multiplayer, full map recovery, or visual polish before the minimal offline gameplay loop works.
- Do not spend time resolving assets or missing components that are proven irrelevant to this first playable slice.

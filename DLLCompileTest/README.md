# TABZ original DLL compile and Android runtime probe

This is a disposable Unity project, isolated from \`MobilePort\`, for checking whether the original TABZ v5.1 managed assemblies can be imported by Unity 2022.3 and inspected in an Android player.

## Included

- \`Assembly-CSharp.dll\`
- \`Assembly-CSharp-firstpass.dll\`
- \`Photon3Unity3D.dll\` (managed dependency required by \`Assembly-CSharp.dll\`)

The three DLLs are byte-for-byte references to the originals in the \`tabz-v5.1\` branch. Their UnityEngine references are resolved against the Unity 2022.3 editor/player assemblies, not by copying the old \`UnityEngine.dll\` into this project.

## Deliberately excluded

No Windows native plugins are imported here (\`fmodstudio.dll\`, \`CSteamworks.dll\`, \`steam_api.dll\`). The firstpass assembly declares P/Invoke calls to FMOD, CSteamworks and \`sdkencryptedappticket\`; those native dependencies are a separate Android porting problem and are not needed merely to test managed assembly import/compilation.

## Run in Unity Build Automation

Create a separate build configuration; do not change the existing MobilePort configuration:

- Repository: \`Miguellbr/TABZ-port\`
- Branch: \`audit/unity-dll-compile\`
- Unity project subfolder: \`DLLCompileTest\`
- Editor: use the Unity 2022.3.62 version selected for this build
- Target: Android / ARM64

Run an Android build. Keep the Unity Build Automation log as well as the on-device report.

After installing and opening the APK, the screen should show **TABZ DLL Runtime Audit** (the heading may still use the earlier short title). Press **COPIAR RELATÓRIO COMPLETO** to copy all diagnostic text to the Android clipboard, then paste it into the conversation.

The runtime report includes:
- Whether each managed assembly is loaded and its identity.
- All assembly references declared by each DLL, with an attempt to resolve each dependency in the Android runtime.
- Type names matching gameplay-related keywords (candidate names only; they are not automatically confirmed system mappings).
- Methods marked as native P/Invoke, with their declared native library names when readable.
- Reflection and method-scan errors.
- A separate structure audit of types in Assembly-CSharp, excluding common third-party namespaces using a heuristic filter.
- The names and base classes of types derived from MonoBehaviour (candidate Unity components).
- Declared fields, properties, method signatures, and implemented interfaces for up to 120 project-type candidates, prioritizing MonoBehaviour-derived types.
- Type-name hints for potentially platform-sensitive areas such as Windows, FMOD, Steam, Editor and DirectX.

The structure section is metadata-only. It does not read method bodies, establish which scripts are attached to original prefabs/scenes, or prove that a class is used in gameplay. The namespace filter can include some demo/helper classes and should be reviewed against the original game assets.

## Interpretation limits

- The previous successful baseline test showed that all three original managed assemblies were visible and their types enumerable on Android. The patched-input branch adds `TabzInputShim.dll` and replaces `Assembly-CSharp.dll`; it must be tested separately.
- This deeper scan inspects metadata. It does not intentionally invoke ordinary gameplay methods or initialize game systems.
- P/Invoke entries identify native calls declared by the assembly; they do not prove those calls are executed during startup.
- A successful metadata scan does not prove gameplay runtime compatibility, Android availability of native libraries, or that Windows Steam/FM0D functionality works on Android.

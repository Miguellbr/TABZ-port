# TABZ original DLL compile probe

This is a disposable Unity project, isolated from `MobilePort`, for checking whether the original TABZ v5.1 managed assemblies can be imported by Unity 2022.3.62f1 and included in an Android build.

## Included

- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll`
- `Photon3Unity3D.dll` (managed dependency required by `Assembly-CSharp.dll`)

The three DLLs are byte-for-byte references to the originals in the `tabz-v5.1` branch. Their UnityEngine references are intentionally resolved against the Unity 2022.3 editor/player assemblies, not by copying the old `UnityEngine.dll` into this project.

## Deliberately excluded

No Windows native plugins are imported here (`fmodstudio.dll`, `CSteamworks.dll`, `steam_api.dll`). The firstpass assembly declares P/Invoke calls to FMOD, CSteamworks and `sdkencryptedappticket`; those native dependencies are a separate Android porting problem and are not needed merely to test managed assembly import/compilation.

## Run in Unity Build Automation

Create a separate build configuration; do not change the existing MobilePort configuration:

- Repository: `Miguellbr/TABZ-port`
- Branch: `audit/unity-dll-compile`
- Unity project subfolder: `DLLCompileTest`
- Editor: `2022.3.62f1`
- Target: Android / ARM64

Run an Android build. The first useful result is the Unity import/compilation log, not whether a playable APK starts. The probe scene is intentionally minimal. A runtime diagnostic component is initialized automatically after the scene loads and draws its results on screen.

After the Android APK is installed and opened, the screen should show **TABZ DLL Runtime Probe**. It reports whether `Photon3Unity3D`, `Assembly-CSharp-firstpass`, and `Assembly-CSharp` are visible to the Android runtime and whether their types can be enumerated. Also retain the Unity Build Automation log. The Editor menu **TABZ DLL Probe > Run diagnostics** remains available for Editor-side checks.

## Interpretation

A successful import/build only proves that Unity 2022.3 can import and compile against these managed assemblies. It does not prove gameplay runtime compatibility, Android availability of the P/Invoke native libraries, or that Windows Steam/FM0D functionality works on Android.

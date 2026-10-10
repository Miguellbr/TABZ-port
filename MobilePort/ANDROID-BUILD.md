# Building the Android prototype

The Android APK is built on GitHub Actions; a PC is not required to trigger the workflow. The project targets Unity **2022.3.62f1** and ARM64 Android devices.

## Required one-time setup

Unity's editor build pipeline requires a valid Unity license in CI. Add a repository Actions secret named `UNITY_LICENSE` containing the license file text (`.ulf`) for an account/license permitted to build this project.

1. Open the repository's **Settings → Secrets and variables → Actions**.
2. Create a repository secret named `UNITY_LICENSE` and paste the contents of the Unity license file.
3. Open **Actions → Build TABZ Android prototype APK → Run workflow**.
4. When the run succeeds, open its summary and download the `TABZ-Mobile-Prototype-APK` artifact.

If no `UNITY_LICENSE` secret exists, the workflow stops early with an explicit message. This workflow does not bypass Unity licensing.

## What the build checks

Before compiling, the editor build method checks that the offline prototype scene exists and that `Resources/items/Ak74` loads with at least one enabled mesh renderer using a valid mesh. It then builds an ARM64 APK in landscape orientation.

A successful build proves the project compiled and an APK was produced. It does **not** prove that the weapon's position/orientation looks correct on a real phone; that still requires installing and visually testing the APK.

## Current limitations

- The scene still uses a placeholder capsule for the zombie.
- The AK-74 is a visual prefab; offline firing/damage remains handled by the prototype weapon script.
- The first APK is a development prototype, not a finished port of TABZ.

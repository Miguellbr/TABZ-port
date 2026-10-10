# Building the Android prototype

The APK is built on GitHub Actions, so you can trigger the build from your phone. The project uses Unity **2022.3.62f1** and targets ARM64 Android devices.

## License status: one required setup step

The old workflow that tried to generate a Unity `.alf` activation request no longer works. Do not run it again: GameCI's action now fails, and it cannot produce an artifact.

For a free **Unity Personal** license, the current supported route documented by Unity and GameCI is to activate through **Unity Hub on a computer**. There is no reliable official phone-only activation route for this workflow. Since you currently only have a phone, the practical option is to temporarily use a trusted Windows, macOS, or Linux computer (for example, borrow one). You should not need to keep that computer for building the APK; GitHub Actions does the build.

Official instructions:
- GameCI activation: https://game.ci/docs/github/activation/
- Unity license activation methods: https://docs.unity.com/en-us/engine/6000.0/manual/get-started/install-and-upgrade/licenses-and-activation/license-activation-methods

### One-time setup on a trusted computer

1. Install Unity Hub from https://unity.com/download and sign in to your own Unity account.
2. In Unity Hub, open **Preferences/Settings → Licenses → Add** and choose **Get a free Personal license**. Follow the prompts to complete activation.
3. Locate the generated `.ulf` file:
   - Windows: `C:\\ProgramData\\Unity\\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. On GitHub, open https://github.com/Miguellbr/TABZ-port/settings/secrets/actions and add these **repository secrets**:
   - `UNITY_LICENSE`: the complete text inside the `.ulf` file.
   - `UNITY_EMAIL`: the email address for your Unity account.
   - `UNITY_PASSWORD`: the password for that Unity account.
5. Sign out of Unity Hub on the borrowed computer and remove any local copy of the license file if you prefer not to leave account-related files there. Never commit these values to the repository or send them in chat.

Do not buy a paid Unity plan just for this prototype. If you cannot borrow a computer, we can continue checking and improving the code, but we should be honest that a real Unity APK build remains blocked until a supported license is available.

## Build the APK

After the secrets are configured:

1. Open **Actions → Build TABZ Android prototype APK → Run workflow**:
   https://github.com/Miguellbr/TABZ-port/actions/workflows/android-prototype-build.yml
2. Wait for the job to finish.
3. If successful, download `TABZ-Mobile-Prototype-APK` from the run's **Artifacts** section.

The build script checks that the prototype scene exists and that `Resources/items/Ak74` loads with at least one enabled mesh renderer using a valid mesh. It then builds an ARM64 APK in landscape orientation.

A successful build proves that Unity compiled the project and produced an APK. It does **not** prove that the weapon's position/orientation looks correct on a real phone; that still requires installing and visually testing the APK.

## Current limitations

- The scene still uses a placeholder capsule for the zombie.
- The AK-74 is a visual prefab; offline firing/damage remains handled by the prototype weapon script.
- This is an early development prototype, not a finished port of TABZ.

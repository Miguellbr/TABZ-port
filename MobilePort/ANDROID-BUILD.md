# Building the Android prototype

The APK is built on GitHub Actions; you do not need a PC to trigger the workflows. The project targets Unity **2022.3.62f1** and ARM64 Android devices.

## Step 1 — Request an activation file

1. Open **Actions → Request Unity license activation file → Run workflow**: https://github.com/Miguellbr/TABZ-port/actions/workflows/request-unity-license.yml
2. When the run succeeds, open it and download the `Unity-Activation-Request` artifact.
3. Extract the `.alf` file from the downloaded ZIP.
4. Open https://license.unity3d.com/ and submit the `.alf` file.
5. If Unity lets you obtain a license file, download the `.ulf` file.

**Important limitation:** Unity's current documentation says manual activation of Personal licenses may no longer be supported in the portal. If the portal does not offer Personal, do not buy a paid license just for this prototype; tell me what the page shows and we can evaluate the available legitimate options. GameCI documents this limitation and workaround notes here: https://game.ci/docs/3/github/activation/.

## Step 2 — Add GitHub Actions secrets

Open https://github.com/Miguellbr/TABZ-port/settings/secrets/actions and add these repository secrets:

- `UNITY_LICENSE` — the full text inside the `.ulf` file, not the file path.
- `UNITY_EMAIL` — the email address for your Unity account.
- `UNITY_PASSWORD` — the password for that Unity account.

Do not commit these values to the repository or paste them into chat. GitHub encrypts repository Actions secrets.

## Step 3 — Build the APK

1. Open **Actions → Build TABZ Android prototype APK → Run workflow**: https://github.com/Miguellbr/TABZ-port/actions/workflows/android-prototype-build.yml
2. Wait for the job to finish.
3. If successful, download `TABZ-Mobile-Prototype-APK` from the run's Artifacts section.

Before compiling, the editor build method checks that the prototype scene exists and that `Resources/items/Ak74` loads with at least one enabled mesh renderer using a valid mesh. It then builds an ARM64 APK in landscape orientation.

A successful build proves the project compiled and an APK was produced. It does **not** prove that the weapon's position/orientation looks correct on a real phone; that still requires installing and visually testing the APK.

## Current limitations

- The scene still uses a placeholder capsule for the zombie.
- The AK-74 is a visual prefab; offline firing/damage remains handled by the prototype weapon script.
- The first APK is a development prototype, not a finished port of TABZ.

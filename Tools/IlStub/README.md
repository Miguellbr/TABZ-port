# TabzIlStub

Turns the native (P/Invoke) methods of selected libraries into managed methods that simply return
`default(T)`. The game's own code then runs without those native libraries.

```
TabzIlStub <input.dll> <output.dll> <managed-dir> [--steam] [--fmod-silent] [--require-hits]
```

- `--steam`: stubs `CSteamworks` and `sdkencryptedappticket`, and makes
  `Steamworks.InteropHelp.TestIfPlatformSupported / TestIfAvailableClient / TestIfAvailableGameServer`
  no-ops (the original IL of those methods is printed in the report before it is replaced).
  Result: `SteamAPI.RestartAppIfNecessary` and `SteamAPI.Init` return `false`, so
  `SteamManager.Awake` logs an error instead of calling `Application.Quit()`.
- `--fmod-silent`: stubs every `fmod*` library. The game runs without sound. Use it only if the real
  ARMv7 FMOD 1.09.03 libraries are not in the project yet.

Workflow: `.github/workflows/il-stub-native.yml` (runs on push to this branch). Artifact
`tabz-il-stubbed-native` has `Assembly-CSharp-firstpass.dll` (Steam stubbed) and
`silent/Assembly-CSharp-firstpass.dll` (Steam + FMOD stubbed).

Not tested in Unity yet.

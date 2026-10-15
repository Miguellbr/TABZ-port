# Original TABZ input integration

This patch connects the recovered v5.1 gameplay scripts to the touch input state already used by the MobilePort HUD.

## What is included

- `TABZInput.cs`: compatibility layer. Physical keyboard/mouse still works; touch state is added to the original queries.
- `MobileInputState.cs`: held and one-frame touch states, including a consumable look delta.
- `MobileActionButton.cs`: emits press edges for jump and crouch as well as fire/reload/interact.
- `MobileControlsBootstrap.cs`: clears one-frame states at the end of the frame, after gameplay LateUpdate has had the chance to consume camera look.

## Applying to the decompiled v5.1 scripts

After extracting the workflow artifact, copy `TABZInput.cs` and `MobileInputState.cs` into the same Unity assembly as the original scripts. Then run:

```sh
python3 Tools/patch-original-input.py /path/to/decompiled
```

The patch updates these original classes only:

- `PlayerHandler`: WASD, sprint and jump.
- `CameraMovement`: aim and mouse-look.
- `Weapon`: fire, melee and reload.
- `WeaponHandler`: aim-hold and unarmed punches.
- `StandHandler`: crouch/prone state input.
- `PlayerInteraction`: pickup interaction.

The script is strict about the original camera Mouse X/Y code so it does not silently claim success against a different build.

## Important constraints

This is the input bridge, not a completed Android game build. The original scripts still depend on their original Unity/Photon/FMOD assemblies, prefabs, scenes, assets, and compatible project settings. Those will be brought together and validated as a separate integration/build stage. Do not replace the original physics/player controller with `OfflinePrototypePlayer`.

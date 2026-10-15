# Original TABZ input integration

This patch connects the recovered v5.1 gameplay scripts to the touch input state already used by the MobilePort HUD.

## What is included

- `TABZInput.cs`: compatibility layer. Physical keyboard/mouse still works; touch state is added to the original queries.
- `MobileInputState.cs`: held and one-frame touch states, including a consumable look delta.
- `MobileActionButton.cs`: emits press edges for jump, crouch, prone, special mode, fall, pause, reload and interact; held actions include fire, aim and sprint.
- `MobileControlsBootstrap.cs`: clears one-frame states at the end of the frame, after gameplay has consumed camera look, pause, and inventory events.
- `MobileInventoryBridge.cs`: routes the INV and six hotbar buttons to the original `UIInventory` methods; the patcher also forwards touch drag coordinates into the original inventory UI.

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
- `PhysicsAmimationController`: V/fall action.
- `PauseMenuHandler`: mobile pause button without routing it through Escape, which is also used by chat.
- `UIInventory` and `UIInventoryItem`: touch-based drag positioning while retaining the original inventory logic.

The script is strict about the original camera Mouse X/Y code so it does not silently claim success against a different build.

## Important constraints

This is the input bridge, not a completed Android game build. The original scripts still depend on their original Unity/Photon/FMOD assemblies, prefabs, scenes, assets, and compatible project settings. Those will be brought together and validated as a separate integration/build stage. Do not replace the original physics/player controller with `OfflinePrototypePlayer`.

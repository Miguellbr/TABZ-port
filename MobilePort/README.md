# TABZ mobile reconstruction scaffold

This is the first mobile-input layer for the Android port. It is deliberately separate from the extracted Windows player files: the repository does not yet contain a reconstructed Unity Editor project, and these scripts are not an APK by themselves.

## Runtime touch HUD

`MobileControlsBootstrap` creates a basic landscape HUD at runtime: left-thumb movement joystick, right-side drag-to-look, Fire, Aim, Jump, Sprint, Reload, and Use/Interact. Attach it to an active GameObject in a reconstructed test scene. Set `createOnDesktopForTesting` to true to preview the HUD in the Unity Editor. It creates a Canvas and EventSystem if no Canvas exists. If the recovered scene already has a Canvas, it currently exits to avoid duplicating UI; attach the individual controls manually in that case.

The UI uses a Canvas Scaler set to Scale With Screen Size. Controls are intended for simultaneous multi-touch. Touch layout can be refined after testing on an actual device.

## Hooking gameplay scripts into touch input

The old game uses `Input.GetAxis`, `Input.GetKey`, and `Input.GetButton` in many places. Unity's static `Input` API cannot be overridden. When integrating this scaffold into the recovered project, patch the relevant player/camera/weapon scripts to read `MobileInputState` on Android and preserve keyboard/mouse input on desktop. For example:

    float horizontal = Application.isMobilePlatform ? MobileInputState.GetAxis("Horizontal") : Input.GetAxis("Horizontal");
    float vertical = Application.isMobilePlatform ? MobileInputState.GetAxis("Vertical") : Input.GetAxis("Vertical");

Use the same platform conditional for look input and action buttons. ReloadPressed and InteractPressed are one-frame triggers; call MobileInputState.ResetFrame() once per frame from a single manager, not from every control. Do not reset held states from that manager.

## Integration still required

- The original gameplay scripts are not yet wired to this input layer.
- The Windows build does not include the original Unity project/scene hierarchy; scene reconstruction is separate work.
- The runtime HUD is an input layer, not a playable port on its own.
- Do not commit full extracted archives or a full decompilation dump to the public repository. Use expiring Actions artifacts for large intermediate exports.

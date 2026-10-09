# TABZ mobile reconstruction scaffold

This is the first mobile-input layer for the Android port. It is deliberately separate from the extracted Windows player files: the repository does not yet contain a reconstructed Unity Editor project, and these scripts are not an APK by themselves.

## Touch layout

Recommended landscape layout:
- **Left thumb:** floating movement joystick.
- **Right half:** transparent drag-look panel.
- **Right lower corner:** Fire and Aim.
- **Near movement joystick:** Jump and Sprint.
- **Small secondary buttons:** Reload, Interact, and Crouch.

Use a Canvas set to Screen Space - Overlay, Canvas Scaler set to Scale With Screen Size, a GraphicRaycaster, and an EventSystem with StandaloneInputModule. UI images that receive pointer events must have Raycast Target enabled; the transparent look panel needs an Image with a fully transparent color but Raycast Target enabled. Put action buttons above the look panel in the Canvas hierarchy so they receive touches first.

## Hooking gameplay scripts into touch input

The old game uses Input.GetAxis, Input.GetKey, and Input.GetButton in many places. Unity's static Input API cannot be overridden. When integrating this scaffold into the recovered project, patch the relevant player/camera/weapon scripts to read MobileInputState on Android and preserve keyboard/mouse input on desktop. For example:

    float horizontal = Application.isMobilePlatform ? MobileInputState.GetAxis("Horizontal") : Input.GetAxis("Horizontal");
    float vertical = Application.isMobilePlatform ? MobileInputState.GetAxis("Vertical") : Input.GetAxis("Vertical");

Use the same platform conditional for look input and action buttons. ReloadPressed and InteractPressed are one-frame triggers; call MobileInputState.ResetFrame() once per frame from a single manager, not from every control. Do not reset held states from that manager.

## Integration still required

- The original gameplay scripts are not yet wired to this input layer.
- The Windows build does not include the original Unity project/scene hierarchy; scene reconstruction is separate work.
- This provides simultaneous multi-touch controls, not global keyboard emulation.
- Do not commit full extracted archives or a full decompilation dump to the public repository. Use expiring Actions artifacts for large intermediate exports.

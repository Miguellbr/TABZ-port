# TABZ Android port — source-level input map

Generated from the recovered v5.1 managed assembly. These are the first classes to patch and test. This map does not claim the classes have already been patched.

## Primary gameplay path

### PlayerHandler
- Reads W/S/A/D directly using `Input.GetKey`.
- Reads LeftShift/RightShift for running.
- Reads Space for jump.
- Delegates to `MovementHandler` and `PhysicsAmimationController`.
- AI branch uses behavior-tree state instead of player keyboard input; preserve this distinction.

**Porting recommendation:** add a mobile branch that consumes a normalized 2D joystick vector, run state and jump-pressed state, then calls the same existing movement/animation methods. Avoid changing ragdoll force calculations until touch input is proven.

### CameraMovement
- Reads `Input.GetAxis("Mouse X")` and `Input.GetAxis("Mouse Y")`.
- Rotates `cameraRotation` and interpolates `mimicTarget` while the player is alive/active.
- Locks and hides the cursor as a desktop assumption.

**Porting recommendation:** inject a touch-look delta in the camera rotation path, keeping the existing rotation limits/lerps. Use a dedicated right-side look area so dragging over action buttons does not rotate the camera.

### Weapon
- Mouse-left presses trigger single fire; mouse-left hold supports automatic weapons.
- R triggers reload.
- Fire is relayed through Photon RPCs and weapon audio uses FMOD.

**Porting recommendation:** use held and pressed states separately; a single tap/hold boolean is insufficient for both semi-automatic and automatic weapons. For offline mode, first separate local firing from Photon synchronization.

### PlayerInteraction
- Casts from the center of `Camera.main` using `Physics.SphereCast`.
- E picks up the targeted item.
- UI tells the user to press E.

**Porting recommendation:** keep the existing center-screen interaction ray, add an on-screen USE button, and change the prompt to a mobile-friendly instruction.

### UIInventory / hotbar
- Uses I/Tab and number keys.
- Uses Photon ownership and FMOD click sounds.
- Inventory initialization and test-item spawning include Photon calls.

**Porting recommendation:** add touch UI selection and inventory toggle; do not start with full inventory if the minimal prototype can use one weapon and a simple pickup.

## Other classes with legacy desktop input

- `RPGCamera`: mouse wheel zoom, mouse drag rotation, Fire1/Fire2 mouse-button checks.
- `MainMenuHandler` and pause-related code: cursor visibility/lock state and Escape handling.
- `PlayerAnimatorManager` / `IdleRunJump`: legacy axes and Fire1/Fire2 buttons; inspect whether these are active gameplay components or demo/unused classes before patching.
- Some utility code checks `Input.touchCount`, but this is not evidence that the main player or camera is touch-compatible.

## Version comparison

The recovered v4 and v5.1 assemblies contain 524 and 526 distinct class names in the source scan. Only two names were unique to v5.1: `UpdateMarkerPos` and `UpdateMarkerPosByCamera`. `MovementHandler`, `PlayerInteraction`, `NetworkPlayer` and `BasicMouseLookControl` were identical in the source comparison. `PlayerHandler`, `Weapon`, `MainMenuHandler` and `NetworkManager` had differences. This suggests the main ragdoll movement core is stable across these builds, but we should use v5.1 as the primary baseline and selectively compare changes before patching.

## Build-critical unknowns still to resolve

1. Which Unity version can import/export the serialized content without breaking prefab/component references?
2. Can the required model, material, animation and texture assets be exported into an actual Unity project with stable references?
3. Can Photon and FMOD dependencies be stubbed/replaced cleanly for an offline first build?
4. What Android graphics/performance settings are viable for the very large level?
5. Which scripts are actually attached to the player prefab versus unused library/demo scripts?

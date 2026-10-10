# Offline prototype scaffold

## What this is

A first executable gameplay slice, created at runtime by Unity scripts. It is deliberately independent of Photon, Steamworks and FMOD so we can test the Android control loop before integrating the original TABZ components.

**It is not yet a port of the original models, UI or gameplay.** The zombie and rifle in this scene are placeholders. The original inventory UI integration remains a separate next step.

## Files

- `OfflinePrototypeBootstrap.cs`: creates the ground, player, camera, target, HUD, simple inventory panel and DEV respawn button.
- `OfflinePrototypePlayer.cs`: local first-person movement, gravity, jump, mouse/touch look.
- `OfflinePrototypeWeapon.cs`: local hitscan weapon, ammo, reserve ammo and reload.
- `OfflinePrototypeZombie.cs`: moving target with health, damage and death.
- `MobileControlsBootstrap.cs`: virtual joystick, look zone, fire/aim/jump/sprint/reload/use, inventory toggle and six hotbar buttons.
- `MobileInventoryBridge.cs`: optional bridge to the original `UIInventory` when it exists in the scene.

## How to test when a Unity Editor/project is available

1. Create a new **3D Unity project** (Unity 2021 LTS or newer recommended for the standalone scaffold).
2. Copy the files in `MobilePort/Assets/Scripts/Mobile/` into the project's `Assets/Scripts/Mobile/` folder.
3. Create an empty GameObject named `PrototypeBootstrap` in a new scene.
4. Add the `OfflinePrototypeBootstrap` component to it.
5. Press Play. Desktop controls: WASD, mouse look, left-click to fire, right-click to aim state, Space to jump, Shift to sprint, R to reload, I/Tab for inventory, 1–6 for hotbar. The DEV button respawns the target.
6. For Android, switch build target to Android, use landscape orientation, ensure the Unity UI module is available, and build an APK. Touch HUD appears automatically on mobile.

## Controls and limitations

- Left joystick: move; drag on right side: look.
- FIRE: shoot; AIM: reserved aim state; JUMP and SPRINT: movement; RELOAD: reload; USE is currently a placeholder.
- INV opens the prototype-only inventory panel; hotbar buttons select its one placeholder rifle.
- The target is a simple capsule, not a recovered TABZ zombie prefab.
- No build is claimed until Unity compiles the project. This repository does not currently include a complete reconstructed Unity project or a verified APK.

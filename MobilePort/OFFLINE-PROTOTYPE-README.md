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

## How to test when a Unity Editor is available

1. Open the `MobilePort/` folder as a Unity project using **Unity 2022.3 LTS** (the project version is recorded in `ProjectSettings/ProjectVersion.txt`).
2. Open `Assets/Scenes/OfflinePrototype.unity`; it is configured as the first build scene and already has `OfflinePrototypeBootstrap` attached.
3. Press Play. Desktop controls: WASD, mouse look, left-click to fire, right-click to aim state, Space to jump, Shift to sprint, R to reload, I/Tab for inventory, 1–6 for hotbar. The DEV button respawns the target.
4. For Android, switch build target to Android, use landscape orientation, let Unity resolve the `com.unity.ugui` dependency, and build an APK. Touch HUD appears automatically on mobile.

## Controls and limitations

- Left joystick: move; drag on right side: look.
- FIRE: shoot; AIM: reserved aim state; JUMP and SPRINT: movement; RELOAD: reload; USE is currently a placeholder.
- INV opens the prototype-only inventory panel; hotbar buttons select its one placeholder rifle.
- The target is a simple capsule, not a recovered TABZ zombie prefab.
- No build is claimed until Unity compiles the project. This is now a minimal Unity scaffold project, not a reconstruction of the original TABZ Unity project. No verified APK exists yet.

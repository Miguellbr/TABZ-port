from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "MobilePort" / "Assets" / "Scripts" / "Mobile"
FILES = {
    "MobileInputState.cs": ["class MobileInputState", "FirePressed", "InventoryTogglePressed", "HotbarSlotPressed"],
    "MobileControlsBootstrap.cs": ["class MobileControlsBootstrap", "MobileVirtualJoystick", "MobileLookArea", "Hotbar6", "Inventory"],
    "MobileInventoryBridge.cs": ["class MobileInventoryBridge", "UIInventory", "ToggleOpen", "UpdateHotbarSelection"],
    "OfflinePrototypeBootstrap.cs": ["class OfflinePrototypeBootstrap", "BuildWorld", "BuildHud", "BuildInventoryPanel", "SpawnZombie"],
    "OfflinePrototypePlayer.cs": ["class OfflinePrototypePlayer", "CharacterController", "ConsumeLookDelta", "Input.GetAxisRaw"],
    "OfflinePrototypeWeapon.cs": ["class OfflinePrototypeWeapon", "void Fire()", "void Reload()", "Physics.Raycast"],
    "OfflinePrototypeZombie.cs": ["class OfflinePrototypeZombie", "TakeDamage", "IsDead"],
    "OriginalWeaponVisualBridge.cs": ["class OriginalWeaponVisualBridge", "Resources.Load<GameObject>", "viewCamera.transform"],
}

def strip_csharp_literals_and_comments(source):
    out = []
    i = 0
    state = "code"
    while i < len(source):
        ch = source[i]
        nxt = source[i + 1] if i + 1 < len(source) else ""
        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"; out.extend("  "); i += 2; continue
            if ch == "/" and nxt == "*":
                state = "block_comment"; out.extend("  "); i += 2; continue
            if ch == '"':
                state = "string"; out.append(" "); i += 1; continue
            if ch == "'":
                state = "char"; out.append(" "); i += 1; continue
            out.append(ch); i += 1; continue
        if state == "line_comment":
            if ch == "\n":
                state = "code"; out.append("\n")
            else:
                out.append(" ")
            i += 1; continue
        if state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"; out.extend("  "); i += 2
            else:
                out.append("\n" if ch == "\n" else " "); i += 1
            continue
        if state in ("string", "char"):
            terminator = '"' if state == "string" else "'"
            if ch == "\\":
                out.extend("  "); i += 2; continue
            if ch == terminator:
                state = "code"
            out.append("\n" if ch == "\n" else " ")
            i += 1
    return "".join(out)

errors = []
for filename, required in FILES.items():
    path = BASE / filename
    if not path.exists():
        errors.append(f"Missing file: {path}")
        continue
    source = path.read_text(encoding="utf-8")
    for token in required:
        if token not in source:
            errors.append(f"{filename}: missing expected token {token!r}")
    stripped = strip_csharp_literals_and_comments(source)
    balance = 0
    for line_no, line in enumerate(stripped.splitlines(), 1):
        for ch in line:
            if ch == "{": balance += 1
            elif ch == "}":
                balance -= 1
                if balance < 0:
                    errors.append(f"{filename}:{line_no}: unexpected closing brace")
                    balance = 0
    if balance:
        errors.append(f"{filename}: {balance} unclosed brace(s)")

# The recovered AK-74 must be committed and the runtime bootstrap must actually attach its visual bridge.
required_assets = [
    ROOT / "MobilePort" / "Assets" / "Resources" / "items" / "Ak74.prefab",
    ROOT / "MobilePort" / "Assets" / "Mesh" / "AK_47.asset",
]
for asset in required_assets:
    if not asset.is_file():
        errors.append(f"Missing imported original weapon asset: {asset.relative_to(ROOT)}")

bootstrap = (BASE / "OfflinePrototypeBootstrap.cs").read_text(encoding="utf-8")
if "AddComponent<OriginalWeaponVisualBridge>" not in bootstrap:
    errors.append("OfflinePrototypeBootstrap.cs: original weapon visual bridge is not attached")
if 'resourcesPrefabPath = "items/Ak74"' not in bootstrap:
    errors.append("OfflinePrototypeBootstrap.cs: expected original AK-74 Resources path is missing")

offline_files = ["OfflinePrototypeBootstrap.cs", "OfflinePrototypePlayer.cs", "OfflinePrototypeWeapon.cs", "OfflinePrototypeZombie.cs"]
for filename in offline_files:
    source = (BASE / filename).read_text(encoding="utf-8")
    for forbidden in ("PhotonNetwork", "SteamManager", "FMODUnity"):
        if forbidden in source:
            errors.append(f"{filename}: offline scaffold unexpectedly depends on {forbidden}")

if errors:
    print("\n".join("ERROR: " + e for e in errors))
    sys.exit(1)
print(f"PASS: validated {len(FILES)} C# files, brace balance, input/UI contracts and offline dependency boundary.")

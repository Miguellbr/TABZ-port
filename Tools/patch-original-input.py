#!/usr/bin/env python3
"""Patch the decompiled TABZ v5.1 scripts to read the mobile input adapter.

Usage:
  python3 Tools/patch-original-input.py /path/to/decompiled

The script is deliberately strict: it fails if expected original input sites are
missing, so a different game build is not silently patched incorrectly.
"""
from pathlib import Path
import sys

if len(sys.argv) != 2:
    raise SystemExit("Usage: patch-original-input.py <decompiled-source-directory>")

root = Path(sys.argv[1])
files = [
    "PlayerHandler.cs",
    "CameraMovement.cs",
    "Weapon.cs",
    "WeaponHandler.cs",
    "StandHandler.cs",
    "PlayerInteraction.cs",
]
missing = [name for name in files if not (root / name).is_file()]
if missing:
    raise SystemExit("Missing expected decompiled files: " + ", ".join(missing))

counts = {}
for name in files:
    path = root / name
    source = path.read_text(encoding="utf-8")
    key_count = source.count("Input.GetKey(")
    down_count = source.count("Input.GetKeyDown(")
    source = source.replace("Input.GetKeyDown(", "TABZInput.GetKeyDown(")
    source = source.replace("Input.GetKey(", "TABZInput.GetKey(")
    counts[name] = (key_count, down_count)
    path.write_text(source, encoding="utf-8")

camera_path = root / "CameraMovement.cs"
camera = camera_path.read_text(encoding="utf-8")
old = (
    'cameraRotation.Rotate(Vector3.up * Input.GetAxis("Mouse X"), Space.World);\n'
    '                cameraRotation.Rotate(cameraRotation.right * (0f - Input.GetAxis("Mouse Y")), Space.World);'
)
new = (
    "Vector2 lookDelta = TABZInput.GetLookDelta();\n"
    "                cameraRotation.Rotate(Vector3.up * lookDelta.x, Space.World);\n"
    "                cameraRotation.Rotate(cameraRotation.right * (0f - lookDelta.y), Space.World);"
)
if old not in camera:
    raise SystemExit('Expected original Mouse X/Y camera input pair was not found; no camera patch applied.')
camera_path.write_text(camera.replace(old, new, 1), encoding="utf-8")

print("Patched original TABZ input sites:")
for name, (held, down) in counts.items():
    print(f"  {name}: {held} held-key reads, {down} key-down reads")
print("  CameraMovement.cs: replaced Mouse X/Y reads with one consumed touch delta")

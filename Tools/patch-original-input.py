#!/usr/bin/env python3
"""Patch the decompiled TABZ v5.1 scripts to read the mobile input adapter."""
from pathlib import Path
import re
import sys

if len(sys.argv) != 2:
    raise SystemExit("Usage: patch-original-input.py <decompiled-source-directory>")

root = Path(sys.argv[1])
files = [
    "PlayerHandler.cs", "CameraMovement.cs", "Weapon.cs",
    "WeaponHandler.cs", "StandHandler.cs", "PlayerInteraction.cs",
    "PhysicsAmimationController.cs", "UIInventory.cs", "UIInventoryItem.cs",
]
missing = [name for name in files if not (root / name).is_file()]
if missing:
    raise SystemExit("Missing expected decompiled files: " + ", ".join(missing))

inventory_path = root / "UIInventory.cs"
inventory_source = inventory_path.read_text(encoding="utf-8")
mouse_position_line = "RectTransformUtility.ScreenPointToLocalPointInRectangle(m_transform, Input.mousePosition, m_canvas.worldCamera, out var localPoint);"
mobile_position_line = "RectTransformUtility.ScreenPointToLocalPointInRectangle(m_transform, MobileInputState.InventoryPointerActive ? (Vector3)MobileInputState.InventoryPointerPosition : Input.mousePosition, m_canvas.worldCamera, out var localPoint);"
if mouse_position_line not in inventory_source:
    raise SystemExit("Expected UIInventory drag-position line was not found.")
inventory_path.write_text(inventory_source.replace(mouse_position_line, mobile_position_line, 1), encoding="utf-8")

item_path = root / "UIInventoryItem.cs"
item_source = item_path.read_text(encoding="utf-8")
empty_drag = "public void OnDrag(PointerEventData eventData)\n\t{\n\t}"
mobile_drag = "public void OnDrag(PointerEventData eventData)\n\t{\n\t\tMobileInputState.InventoryPointerPosition = eventData.position;\n\t\tMobileInputState.InventoryPointerActive = true;\n\t}"
if empty_drag not in item_source:
    raise SystemExit("Expected empty UIInventoryItem.OnDrag method was not found.")
item_source = item_source.replace(empty_drag, mobile_drag, 1)
end_drag_line = "m_uiInventory.OnEndDrag(this, m_pointerOver);"
if end_drag_line not in item_source:
    raise SystemExit("Expected UIInventoryItem.OnEndDrag call was not found.")
item_source = item_source.replace(end_drag_line, end_drag_line + "\n\t\tMobileInputState.InventoryPointerActive = false;", 1)
item_path.write_text(item_source, encoding="utf-8")

pause_path = root / "PauseMenuHandler.cs"
if not pause_path.is_file():
    raise SystemExit("Missing expected decompiled file: PauseMenuHandler.cs")

camera_path = root / "CameraMovement.cs"
camera = camera_path.read_text(encoding="utf-8")
pattern = re.compile(
    r'(?m)^([ \t]*)cameraRotation\.Rotate\(Vector3\.up \* Input\.GetAxis\("Mouse X"\), Space\.World\);\n'
    r'\1cameraRotation\.Rotate\(cameraRotation\.right \* \(0f - Input\.GetAxis\("Mouse Y"\)\), Space\.World\);'
)
match = pattern.search(camera)
if not match:
    raise SystemExit("Expected original Mouse X/Y camera input pair was not found; no files changed.")

indent = match.group(1)
replacement = (
    indent + "Vector2 lookDelta = TABZInput.GetLookDelta();\n"
    + indent + "cameraRotation.Rotate(Vector3.up * lookDelta.x, Space.World);\n"
    + indent + "cameraRotation.Rotate(cameraRotation.right * (0f - lookDelta.y), Space.World);"
)

counts = {}
for name in files:
    path = root / name
    source = path.read_text(encoding="utf-8")
    held = source.count("Input.GetKey(")
    down = source.count("Input.GetKeyDown(")
    source = source.replace("Input.GetKeyDown(", "TABZInput.GetKeyDown(")
    source = source.replace("Input.GetKey(", "TABZInput.GetKey(")
    path.write_text(source, encoding="utf-8")
    counts[name] = (held, down)

camera_path.write_text(pattern.sub(replacement, camera, count=1), encoding="utf-8")
pause_path = root / "PauseMenuHandler.cs"
pause_source = pause_path.read_text(encoding="utf-8")
pause_old = "if (Input.GetKeyDown(KeyCode.Escape))"
if pause_old not in pause_source:
    raise SystemExit("Expected PauseMenuHandler Escape check was not found.")
pause_path.write_text(
    pause_source.replace(pause_old, "if (Input.GetKeyDown(KeyCode.Escape) || MobileInputState.PausePressed)", 1),
    encoding="utf-8",
)

print("Patched original TABZ input sites:")
for name, (held, down) in counts.items():
    print(f"  {name}: {held} held-key reads, {down} key-down reads")
print("  CameraMovement.cs: replaced Mouse X/Y reads with one consumed touch delta")

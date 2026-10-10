import argparse, pathlib, re, shutil

parser = argparse.ArgumentParser(description="Import a sanitized original TABZ weapon prefab and its Unity dependencies.")
parser.add_argument("--assets", required=True, help="AssetRipper ExportedProject/Assets directory")
parser.add_argument("--destination", required=True, help="MobilePort/Assets/Resources/items")
parser.add_argument("--weapon", default="Ak74")
args = parser.parse_args()

source = pathlib.Path(args.assets).resolve()
destination = pathlib.Path(args.destination).resolve()
prefab = source / "Resources" / "items" / f"{args.weapon}.prefab"
if not prefab.is_file():
    raise SystemExit(f"Original prefab not found: {prefab}")

guid_paths = {}
for meta in source.rglob("*.meta"):
    try:
        data = meta.read_text(encoding="utf-8", errors="replace")
    except OSError:
        continue
    match = re.search(r"^guid:\\s*([0-9a-f]{32})\\s*$", data, re.M)
    if match:
        guid_paths[match.group(1)] = pathlib.Path(str(meta)[:-5])

# Strip original MonoBehaviour blocks and remove their fileIDs from GameObjects.
raw = prefab.read_text(encoding="utf-8", errors="replace")
chunks = re.split(r"(?=^--- !u!\\d+ &\\d+\\s*$)", raw, flags=re.M)
removed_ids = set()
kept = []
for chunk in chunks:
    header = re.match(r"^--- !u!(\\d+) &(\\d+)", chunk)
    if header and header.group(1) == "114":
        removed_ids.add(header.group(2))
    else:
        kept.append(chunk)
raw = "".join(kept)
if removed_ids:
    output, in_components = [], False
    for line in raw.splitlines(keepends=True):
        if re.match(r"^  m_Component:\\s*$", line):
            in_components = True
            output.append(line)
            continue
        if in_components:
            if re.match(r"^  [A-Za-z_]", line):
                in_components = False
            else:
                match = re.search(r"fileID:\\s*(\\d+)", line)
                if match and match.group(1) in removed_ids:
                    continue
        output.append(line)
    raw = "".join(output)

# Recursively follow GUID references from prefab -> mesh/material -> textures.
queue = [prefab]
seen = set()
while queue:
    current = queue.pop()
    if current in seen:
        continue
    seen.add(current)
    try:
        data = current.read_text(encoding="utf-8", errors="replace")
    except OSError:
        continue
    for guid in set(re.findall(r"guid:\\s*([0-9a-f]{32})", data)):
        dep = guid_paths.get(guid)
        if dep and dep.is_file() and dep.suffix.lower() not in {".cs", ".dll"} and dep not in seen:
            queue.append(dep)

files = set(seen)
for asset in list(files):
    meta = pathlib.Path(str(asset) + ".meta")
    if meta.is_file():
        files.add(meta)

project_assets = destination.parent.parent
for asset in sorted(files):
    relative = asset.relative_to(source)
    target = destination / asset.name if asset == prefab else project_assets / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    if asset == prefab:
        target.write_text(raw, encoding="utf-8")
    else:
        shutil.copy2(asset, target)

print(f"Imported {args.weapon}: {len(files)} files including metadata; removed {len(removed_ids)} original MonoBehaviour components.")
print("Copied assets:")
for asset in sorted(files):
    print(" - " + asset.relative_to(source).as_posix())

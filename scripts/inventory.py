#!/usr/bin/env python3
"""Create a metadata-only inventory of extracted TABZ build files.

The script does not copy game binaries into the repository or artifact.
It records relative paths, sizes, extensions, and SHA-256 hashes, plus
a small set of likely engine/build markers to guide technical assessment.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

MARKERS = (
    "UnityPlayer.dll",
    "GameAssembly.dll",
    "globalgamemanagers",
    "global-metadata.dat",
    "Assembly-CSharp.dll",
    "il2cpp_data",
    "MonoBleedingEdge",
    "Unity",
    "Unreal",
    "UE4",
    "UE5",
    "libunity.so",
    "libil2cpp.so",
    "libUE4.so",
    "libUnreal.so",
    "AndroidManifest.xml",
    "data.unity3d",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path, help="Directory containing extracted builds")
    parser.add_argument("--output", type=Path, default=Path("inventory"))
    args = parser.parse_args()

    root = args.root.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if not root.is_dir():
        raise SystemExit(f"Input directory does not exist: {root}")

    entries = []
    extensions = Counter()
    markers_found = []
    total_bytes = 0

    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        relative = path.relative_to(root).as_posix()
        size = path.stat().st_size
        ext = path.suffix.lower() or "[no extension]"
        extensions[ext] += 1
        total_bytes += size
        entries.append({
            "path": relative,
            "size_bytes": size,
            "extension": ext,
            "sha256": sha256(path),
        })
        basename = path.name
        for marker in MARKERS:
            if marker.lower() in basename.lower() or marker.lower() in relative.lower():
                markers_found.append({"marker": marker, "path": relative})

    report = {
        "purpose": "Metadata-only inventory; game binaries are not included.",
        "root_name": root.name,
        "file_count": len(entries),
        "total_size_bytes": total_bytes,
        "extensions": dict(sorted(extensions.items())),
        "engine_or_build_markers": markers_found,
        "files": entries,
    }

    (output / "inventory.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    lines = [
        "# TABZ build inventory",
        "",
        f"- Files: **{len(entries)}**",
        f"- Total extracted size: **{total_bytes:,} bytes**",
        "",
        "## Extensions",
        "",
        "| Extension | Count |",
        "|---|---:|",
    ]
    lines.extend(f"| `{ext}` | {count} |" for ext, count in sorted(extensions.items()))
    lines += ["", "## Likely engine/build markers", ""]
    if markers_found:
        lines.extend(f"- `{item['marker']}` — `{item['path']}`" for item in markers_found)
    else:
        lines.append("No known marker filenames were found. This does not rule out a particular engine.")
    lines += [
        "",
        "## Largest files",
        "",
        "| Size (bytes) | Path |",
        "|---:|---|",
    ]
    for item in sorted(entries, key=lambda x: x["size_bytes"], reverse=True)[:100]:
        lines.append(f"| {item['size_bytes']:,} | `{item['path'].replace('|', '\\|')}` |")
    lines += [
        "",
        "> This report contains metadata only. It does not contain the original game files.",
        "",
    ]
    (output / "REPORT.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"Inventoried {len(entries)} files ({total_bytes:,} bytes total).")
    print(f"Reports written to {output}")


if __name__ == "__main__":
    main()

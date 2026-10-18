#!/usr/bin/env python3
"""Static TABZ Unity/DLL audit. No Unity Editor or license required."""
from __future__ import annotations
import collections, json, re
from pathlib import Path

ROOT = Path(".")
SOURCE = Path("audit-work/ilspy")
UNITY_EXTS = {".unity", ".prefab", ".asset", ".controller", ".mat", ".anim", ".overridecontroller"}
GUID_RE = re.compile(r"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})")
CLASS_RE = re.compile(r"\b(class|struct)\s+(\w+)(?:\s*<[^>{}]+>)?\s*(?::\s*([^{\n]+))?")
GUID_META_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)

def main():
    report = ["# TABZ static Unity/DLL audit", "", "Generated without Unity Editor. This is metadata/source/YAML analysis only.", ""]
    dlls = sorted(Path("DLLCompileTest/Assets/Plugins/Managed").glob("*.dll"))
    report += ["## Managed assemblies", ""]
    for p in dlls:
        report.append(f"- \`{p}\` — {p.stat().st_size:,} bytes")
    if not dlls:
        report.append("- No managed DLLs found at expected path.")
    report += ["", "## MonoBehaviour-derived classes found by ILSpy", ""]
    class_rows = []
    for p in SOURCE.rglob("*.cs"):
        src = p.read_text(encoding="utf-8", errors="replace")
        for m in CLASS_RE.finditer(src):
            name, bases = m.group(2), (m.group(3) or "").strip()
            if "MonoBehaviour" in bases:
                class_rows.append((name, bases, str(p)))
    dedup = sorted(set(class_rows))
    for name, bases, path in dedup:
        report.append(f"- \`{name}\` : \`{bases}\` — \`{path}\`")
    report.append(f"\nTotal direct MonoBehaviour declarations: **{len(dedup)}**")
    report += ["", "## Unity serialized script references", ""]
    meta_by_guid = {}
    for meta in ROOT.rglob("*.meta"):
        if ".git" in meta.parts or "audit-work" in meta.parts:
            continue
        try:
            txt = meta.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        m = GUID_META_RE.search(txt)
        if m:
            meta_by_guid[m.group(1).lower()] = str(meta)[:-5]
    refs = collections.Counter()
    locations = collections.defaultdict(list)
    for p in ROOT.rglob("*"):
        if not p.is_file() or p.suffix.lower() not in UNITY_EXTS or ".git" in p.parts or "audit-work" in p.parts:
            continue
        try:
            txt = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for guid in GUID_RE.findall(txt):
            g = guid.lower()
            refs[g] += 1
            if len(locations[g]) < 12:
                locations[g].append(str(p))
    unresolved = [(g,c) for g,c in refs.most_common() if g not in meta_by_guid and g != "00000000000000000000000000000000"]
    report += [
        f"- Serialized m_Script GUID references: **{sum(refs.values())}**",
        f"- Distinct GUIDs: **{len(refs)}**",
        f"- GUIDs mapped to tracked .meta files: **{sum(1 for g in refs if g in meta_by_guid)}**",
        f"- GUIDs not mapped within this checkout: **{len(unresolved)}**", "",
        "### GUID reference map", ""
    ]
    for guid, count in refs.most_common():
        report.append(f"- \`{guid}\` — {count} reference(s) — \`{meta_by_guid.get(guid, 'NOT FOUND IN CHECKED-OUT REPOSITORY')}\`")
    report += ["", "### Unmapped GUID locations", ""]
    for guid, count in unresolved:
        report.append(f"- \`{guid}\` ({count} references)")
        report.extend(f"  - \`{loc}\`" for loc in locations[guid])
    report += [
        "", "## Scope and interpretation limits", "",
        "- DLL classes are identified from ILSpy-decompiled C# declarations; this is not Unity's Editor-generated MonoScript inventory.",
        "- Serialized GUIDs are matched only against .meta files present in this repository checkout. An unmapped GUID may belong to an asset omitted from the checkout, an external package, or a missing script; it is not automatically proof of a broken component.",
        "- This audit cannot recover Unity's DLL sub-asset local file IDs or prove which DLL type a particular serialized component resolves to.",
        "- It does not execute gameplay code or prove Android runtime compatibility.", ""
    ]
    out = Path("audit-output")
    out.mkdir(exist_ok=True)
    (out / "TABZ-STATIC-AUDIT.md").write_text("\n".join(report), encoding="utf-8")
    (out / "TABZ-STATIC-AUDIT.json").write_text(json.dumps({
        "dlls": [{"path": str(p), "size_bytes": p.stat().st_size} for p in dlls],
        "monobehaviour_candidates": [{"name": n, "base": b, "source": p} for n,b,p in dedup],
        "script_guid_reference_counts": dict(refs),
        "guid_to_meta_path": meta_by_guid,
        "unmapped_guids": [{"guid": g, "count": c, "locations": locations[g]} for g,c in unresolved],
        "limits": ["No Unity Editor", "No DLL MonoScript local file IDs", "No runtime proof"]
    }, indent=2), encoding="utf-8")
    print("\n".join(report[:80]))
    print(f"\nWrote {out}/TABZ-STATIC-AUDIT.md and .json")

if __name__ == "__main__":
    main()

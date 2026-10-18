#!/usr/bin/env python3
"""Discovery pass over an AssetRipper export (read-only).

Usage: discover.py <ExportedProject/Assets>

Prints:
  - top-level folders
  - how scripts were exported (guid -> namespace/class) and a sanity check of the Unity fileID formula
  - every prefab under Resources (the only ones PhotonNetwork.Instantiate can load)
  - for the player / zombie / weapon roots: components (script class names), dependency closure size
"""
import collections
import os
import pathlib
import re
import struct
import sys

assets = pathlib.Path(sys.argv[1]).resolve()


def _lrot(x, n):
    return ((x << n) | (x >> (32 - n))) & 0xffffffff


def md4(msg: bytes) -> bytes:
    msg = bytearray(msg)
    ml = len(msg) * 8
    msg.append(0x80)
    while len(msg) % 64 != 56:
        msg.append(0)
    msg += struct.pack('<Q', ml)
    a, b, c, d = 0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476
    F = lambda x, y, z: (x & y) | (~x & z)
    G = lambda x, y, z: (x & y) | (x & z) | (y & z)
    H = lambda x, y, z: x ^ y ^ z
    for i in range(0, len(msg), 64):
        X = list(struct.unpack('<16I', bytes(msg[i:i + 64])))
        aa, bb, cc, dd = a, b, c, d
        for k, s in zip(range(16), [3, 7, 11, 19] * 4):
            a = _lrot((a + F(b, c, d) + X[k]) & 0xffffffff, s)
            a, b, c, d = d, a, b, c
        for k, s in zip([0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15], [3, 5, 9, 13] * 4):
            a = _lrot((a + G(b, c, d) + X[k] + 0x5a827999) & 0xffffffff, s)
            a, b, c, d = d, a, b, c
        for k, s in zip([0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15], [3, 9, 11, 15] * 4):
            a = _lrot((a + H(b, c, d) + X[k] + 0x6ed9eba1) & 0xffffffff, s)
            a, b, c, d = d, a, b, c
        a, b, c, d = (a + aa) & 0xffffffff, (b + bb) & 0xffffffff, (c + cc) & 0xffffffff, (d + dd) & 0xffffffff
    return struct.pack('<4I', a, b, c, d)


def file_id(ns, name):
    return struct.unpack('<i', md4(("s\0\0\0" + ns + name).encode('utf-8'))[:4])[0]


print('fileID formula check (expected -711467253):', file_id('WorldCreatorEngine.Utilities', 'WorldCreatorReference'))
print('assets dir:', assets)
print('\n== top-level folders (files, MiB)')
for child in sorted(assets.iterdir()):
    if child.is_dir():
        files = [p for p in child.rglob('*') if p.is_file()]
        print('  %-28s %6d files %8.1f MiB' % (child.name, len(files), sum(p.stat().st_size for p in files) / 1048576))
    else:
        print('  (file)', child.name)

# guid -> path, and script guid -> (namespace, class)
guid_path = {}
for meta in assets.rglob('*.meta'):
    m = re.search(r'^guid:\s*([0-9a-f]{32})', meta.read_text(encoding='utf-8', errors='replace'), re.M)
    if m:
        guid_path[m.group(1)] = pathlib.Path(str(meta)[:-5])
script_class = {}
ns_rx = re.compile(r'^\s*namespace\s+([\w.]+)', re.M)
class_rx = re.compile(r'\bclass\s+(\w+)')
for guid, p in guid_path.items():
    if p.suffix == '.cs' and p.is_file():
        text = p.read_text(encoding='utf-8', errors='replace')
        ns = ns_rx.search(text)
        cls = class_rx.search(text)
        script_class[guid] = ((ns.group(1) if ns else ''), (cls.group(1) if cls else p.stem), p)
print('\nmeta files with guid: %d | exported .cs scripts: %d' % (len(guid_path), len(script_class)))
for g, (ns, cls, p) in list(script_class.items())[:6]:
    print('  sample script', g, ns or '<global>', cls, p.relative_to(assets).as_posix())

res = assets / 'Resources'
print('\n== prefabs under Resources')
prefabs = sorted(res.rglob('*.prefab')) if res.exists() else []
print('count:', len(prefabs))
for p in prefabs[:500]:
    print('  %-60s %8d bytes' % (p.relative_to(res).as_posix(), p.stat().st_size))

print('\n== all prefabs outside Resources whose name looks relevant')
rel = re.compile(r'player|zombie|tabz|ak74|ak47|camera|hand|arm', re.I)
for p in sorted(assets.rglob('*.prefab')):
    if res in p.parents:
        continue
    if rel.search(p.stem):
        print('  ', p.relative_to(assets).as_posix(), p.stat().st_size)


def components(prefab):
    text = prefab.read_text(encoding='utf-8', errors='replace')
    out = collections.Counter()
    for g in re.findall(r'm_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})', text):
        if g in script_class:
            ns, cls, _ = script_class[g]
            out[(ns + '.' if ns else '') + cls] += 1
        else:
            out['<unmapped %s>' % g[:8]] += 1
    return out


def closure(prefab):
    seen, queue = set(), [prefab]
    while queue:
        cur = queue.pop()
        if cur in seen:
            continue
        seen.add(cur)
        if cur.suffix.lower() in {'.png', '.jpg', '.tga', '.psd', '.wav', '.ogg', '.mp3', '.fbx', '.bank', '.dll'}:
            continue
        try:
            data = cur.read_text(encoding='utf-8', errors='replace')
        except OSError:
            continue
        for g in set(re.findall(r'guid:\s*([0-9a-f]{32})', data)):
            dep = guid_path.get(g)
            if dep and dep.is_file() and dep.suffix.lower() not in {'.cs', '.dll'} and dep not in seen:
                queue.append(dep)
    return seen


roots = [p for p in sorted(assets.rglob('*.prefab')) if re.fullmatch(r'TABS_Player_Network|TABZOMBIE|Ak74|ProjectileBullet_1|RayCastBullet_1|WeaponPosition', p.stem)]
print('\n== roots')
for p in roots:
    comps = components(p)
    files = closure(p)
    size = sum(f.stat().st_size for f in files if f.is_file())
    kinds = collections.Counter(f.suffix.lower() for f in files)
    print('\n%s  (%s)' % (p.relative_to(assets).as_posix(), p.stat().st_size))
    print('  closure: %d files, %.1f MiB; by type: %s' % (len(files), size / 1048576, dict(kinds.most_common(8))))
    print('  script components (%d):' % sum(comps.values()))
    for name, c in comps.most_common(60):
        print('     %3d x %s' % (c, name))

print('\n== which prefab has the original camera / HUD?')
for p in sorted(assets.rglob('*.prefab')):
    if re.search(r'^(UI_Canvas|HotBar|Health|InteractionInfoHolder|CameraHolder|SteamManager|NetworkManager|SpawnPointManager)$', p.stem):
        print('  ', p.relative_to(assets).as_posix(), p.stat().st_size)

print('\n== scenes exported')
for p in sorted(assets.rglob('*.unity')):
    print('  ', p.relative_to(assets).as_posix(), '%.1f MiB' % (p.stat().st_size / 1048576))

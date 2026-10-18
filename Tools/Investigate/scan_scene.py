#!/usr/bin/env python3
"""Read-only inspection of the original TABZ Unity serialized files (UnityPy).

Usage: scan_scene.py <GAME_Data directory>

Reports:
  1. build settings (scene list)
  2. every MonoScript grouped by assembly, flagging assemblies that are NOT in Managed/
  3. how many MonoBehaviours use each script (WorldCreator* highlighted)
  4. the GameObject(s) whose name contains 'WorldCreator': components, serialized fields kept by the
     build's type tree, hierarchy, Terrain / TerrainCollider / TerrainData summary
  5. a census of Terrain-related objects
Nothing is modified.
"""
import collections
import json
import os
import sys
import traceback

import UnityPy

root = sys.argv[1]
managed_dir = os.path.join(root, 'Managed')
managed = {f.lower() for f in os.listdir(managed_dir)} if os.path.isdir(managed_dir) else set()

names = ['globalgamemanagers', 'globalgamemanagers.assets', 'level0', 'level1',
         'sharedassets0.assets', 'sharedassets1.assets', 'resources.assets']
paths = [os.path.join(root, n) for n in names if os.path.exists(os.path.join(root, n))]
env = UnityPy.load(*paths)
byname = {os.path.basename(str(k)).lower(): v for k, v in env.files.items()}
print('UnityPy', getattr(UnityPy, '__version__', '?'))
print('Serialized files loaded:', sorted(byname.keys()))
print('Managed folder has:', sorted(managed))


def tt(o):
    try:
        return o.read_typetree()
    except Exception as e:  # noqa
        return {'__error__': repr(e)}


def resolve(af, fid, pid):
    if not pid:
        return None
    if fid == 0:
        return af.objects.get(pid)
    try:
        ext = af.externals[fid - 1]
        nm = os.path.basename(str(getattr(ext, 'path', '') or getattr(ext, 'name', ''))).lower()
    except Exception:  # noqa
        return None
    f = byname.get(nm)
    return f.objects.get(pid) if f is not None else None


def summarize(v, depth=0, maxdepth=2):
    if isinstance(v, dict):
        if depth >= maxdepth:
            return '{%d keys}' % len(v)
        return {k: summarize(x, depth + 1, maxdepth) for k, x in list(v.items())[:40]}
    if isinstance(v, (list, tuple)):
        return 'list[%d]' % len(v)
    if isinstance(v, (bytes, bytearray)):
        return 'bytes[%d]' % len(v)
    s = repr(v)
    return s if len(s) < 80 else s[:77] + '...'


def short(obj, limit=1500):
    s = json.dumps(obj, default=str, ensure_ascii=False)
    return s if len(s) <= limit else s[:limit] + '...(truncated)'


# ---------------------------------------------------------------- 1. build settings
print('\n===== 1. BUILD SETTINGS')
for o in env.objects:
    if o.type.name == 'BuildSettings':
        d = tt(o)
        print('scenes:', short(d.get('scenes', d), 800))

# ---------------------------------------------------------------- 2. MonoScripts
print('\n===== 2. MONOSCRIPTS BY ASSEMBLY')
script_info = {}
by_asm = collections.defaultdict(list)
for o in env.objects:
    if o.type.name != 'MonoScript':
        continue
    d = tt(o)
    key = (o.assets_file.name, o.path_id)
    info = (d.get('m_AssemblyName'), d.get('m_Namespace'), d.get('m_ClassName'))
    script_info[key] = info
    by_asm[info[0]].append(info)
for asm, items in sorted(by_asm.items(), key=lambda kv: str(kv[0])):
    present = str(asm).lower() in managed
    print('  %-40s %4d scripts   in Managed/: %s' % (asm, len(items), 'yes' if present else 'NO  <-- assembly file not shipped'))
print()
for asm, items in sorted(by_asm.items(), key=lambda kv: str(kv[0])):
    if str(asm).lower() not in managed:
        for info in sorted(items, key=lambda i: (str(i[1]), str(i[2])))[:60]:
            print('  missing-assembly script:', info)
wc_scripts = [i for items in by_asm.values() for i in items if 'worldcreator' in ('%s %s' % (i[1], i[2])).lower()]
print('\nScripts whose namespace/class contains WorldCreator:', wc_scripts)

# ---------------------------------------------------------------- 3. MonoBehaviour usage
print('\n===== 3. MONOBEHAVIOUR COUNT PER SCRIPT')
usage = collections.Counter()
unreadable = 0
unresolved = 0
wc_instances = []
cache = {}
for o in env.objects:
    if o.type.name != 'MonoBehaviour':
        continue
    d = tt(o)
    if '__error__' in d:
        unreadable += 1
        continue
    sp = d.get('m_Script') or {}
    ck = (o.assets_file.name, sp.get('m_FileID', 0), sp.get('m_PathID', 0))
    if ck not in cache:
        so = resolve(o.assets_file, sp.get('m_FileID', 0), sp.get('m_PathID', 0))
        cache[ck] = None if so is None else (so.assets_file.name, so.path_id)
    ref = cache[ck]
    if ref is None or ref not in script_info:
        unresolved += 1
        continue
    info = script_info[ref]
    usage[info] += 1
    if 'worldcreator' in ('%s %s' % (info[1], info[2])).lower():
        wc_instances.append((o.assets_file.name, o.path_id, d.get('m_GameObject')))
print('MonoBehaviours with unreadable type tree: %d | with unresolved script pointer: %d' % (unreadable, unresolved))
missing_total = sum(c for i, c in usage.items() if str(i[0]).lower() not in managed)
print('MonoBehaviours whose script assembly is NOT in Managed/: %d' % missing_total)
for info, c in usage.most_common():
    if str(info[0]).lower() not in managed or 'worldcreator' in ('%s %s' % (info[1], info[2])).lower():
        print('  %5d x %s' % (c, info))
print('Top scripts overall:')
for info, c in usage.most_common(12):
    print('  %5d x %s' % (c, info))
print('WorldCreator component instances:', len(wc_instances))

# ---------------------------------------------------------------- 4. target GameObjects
print('\n===== 4. GAMEOBJECTS NAMED *WorldCreator*')


def go_name(o):
    return tt(o).get('m_Name') if o is not None else None


def describe_component(af, co):
    t = co.type.name
    line = '    - %s (path_id %d, %d bytes)' % (t, co.path_id, co.byte_size)
    d = tt(co)
    if t == 'Transform':
        father = d.get('m_Father', {})
        fo = resolve(af, father.get('m_FileID', 0), father.get('m_PathID', 0))
        fgo = None
        if fo is not None:
            fgo_ptr = tt(fo).get('m_GameObject', {})
            fgo = go_name(resolve(fo.assets_file, fgo_ptr.get('m_FileID', 0), fgo_ptr.get('m_PathID', 0)))
        print(line)
        print('        position=%s scale=%s parent=%r children=%d' % (
            short(d.get('m_LocalPosition'), 120), short(d.get('m_LocalScale'), 120), fgo, len(d.get('m_Children', []))))
    elif t == 'MonoBehaviour':
        sp = d.get('m_Script', {})
        so = resolve(af, sp.get('m_FileID', 0), sp.get('m_PathID', 0))
        ident = script_info.get((so.assets_file.name, so.path_id)) if so is not None else None
        print(line)
        print('        script=%s enabled=%s' % (ident, d.get('m_Enabled')))
        if '__error__' in d:
            print('        type tree NOT readable:', d['__error__'])
        else:
            extra = {k: v for k, v in d.items() if k not in ('m_GameObject', 'm_Script')}
            print('        serialized fields kept by the build:', short(extra, 1800))
    elif t in ('Terrain', 'TerrainCollider'):
        print(line)
        print('        fields:', short(summarize(d, 0, 1), 1500))
        tdp = d.get('m_TerrainData', {})
        td = resolve(af, tdp.get('m_FileID', 0), tdp.get('m_PathID', 0))
        if td is None:
            print('        TerrainData pointer %s -> not resolved' % tdp)
        else:
            print('        TerrainData -> file %s, path_id %d, %d bytes' % (td.assets_file.name, td.path_id, td.byte_size))
            print('        TerrainData summary:', short(summarize(tt(td), 0, 3), 2500))
    else:
        print(line)


targets = []
for o in env.objects:
    if o.type.name != 'GameObject':
        continue
    d = tt(o)
    if 'worldcreator' in str(d.get('m_Name', '')).lower():
        targets.append((o, d))
print('GameObjects found:', len(targets))
for o, d in targets:
    print('  GameObject %r in %s (path_id %d) layer=%s tag=%s active=%s' % (
        d.get('m_Name'), o.assets_file.name, o.path_id, d.get('m_Layer'), d.get('m_Tag'), d.get('m_IsActive')))
    for c in d.get('m_Component', []):
        ptr = c.get('component') or c.get('second') or c
        co = resolve(o.assets_file, ptr.get('m_FileID', 0), ptr.get('m_PathID', 0))
        if co is None:
            print('    - unresolved component pointer', ptr)
            continue
        try:
            describe_component(o.assets_file, co)
        except Exception:  # noqa
            print('    - error describing component', co.type.name)
            traceback.print_exc(limit=1)
    # children of the Transform
    for c in d.get('m_Component', []):
        ptr = c.get('component') or c.get('second') or c
        co = resolve(o.assets_file, ptr.get('m_FileID', 0), ptr.get('m_PathID', 0))
        if co is not None and co.type.name == 'Transform':
            kids = []
            for ch in tt(co).get('m_Children', [])[:30]:
                cto = resolve(o.assets_file, ch.get('m_FileID', 0), ch.get('m_PathID', 0))
                if cto is not None:
                    gp = tt(cto).get('m_GameObject', {})
                    kids.append(go_name(resolve(cto.assets_file, gp.get('m_FileID', 0), gp.get('m_PathID', 0))))
            print('    children (first 30):', kids)

# ---------------------------------------------------------------- 5. census
print('\n===== 5. TERRAIN CENSUS')
count = collections.Counter()
for o in env.objects:
    if o.type.name in ('Terrain', 'TerrainCollider', 'TerrainData'):
        count[(o.assets_file.name, o.type.name)] += 1
for k, v in sorted(count.items()):
    print('  %-28s %-16s %d' % (k[0], k[1], v))
terrain_owner_names = []
for o in env.objects:
    if o.type.name == 'Terrain':
        d = tt(o)
        gp = d.get('m_GameObject', {})
        terrain_owner_names.append(go_name(resolve(o.assets_file, gp.get('m_FileID', 0), gp.get('m_PathID', 0))))
print('GameObjects that own a Terrain component:', terrain_owner_names[:20])

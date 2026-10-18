#!/usr/bin/env python3
"""Read-only inspection of the original TABZ Unity serialized files (UnityPy). Second revision.

Usage: scan_scene.py <GAME_Data directory>

Changes from revision 1:
  * robust cross-file pointer resolution (some entries in env.files are stream readers, not serialized files)
  * full tracebacks when a component cannot be described
  * MonoBehaviours whose type tree cannot be read are still attributed to a script by reading the raw
    bytes (layout in player builds: m_GameObject PPtr 12 bytes, m_Enabled padded to 4, m_Script PPtr 12 bytes)
  * census of the WorldCreatorTerrain subtree, and of a few gameplay/terrain related scripts
Nothing is modified.
"""
import collections
import json
import os
import struct
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

print('UnityPy', getattr(UnityPy, '__version__', '?'))
for k, v in env.files.items():
    print('  env.files[%r] -> %s%s' % (os.path.basename(str(k)), type(v).__name__, ' (has objects: %d)' % len(v.objects) if hasattr(v, 'objects') else ''))


def file_by_name(nm):
    nm = nm.lower()
    for k, v in env.files.items():
        if hasattr(v, 'objects') and os.path.basename(str(k)).lower() == nm:
            return v
    return None


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
    f = file_by_name(nm)
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


def go_name(o):
    return tt(o).get('m_Name') if o is not None else None


# ------------------------------------------------------------------ 1. build settings
print('\n===== 1. BUILD SETTINGS')
for o in env.objects:
    if o.type.name == 'BuildSettings':
        print('scenes:', short(tt(o).get('scenes'), 800))

# ------------------------------------------------------------------ 2. scripts
print('\n===== 2. MONOSCRIPT TABLE (asset files)')
script_info = {}
for o in env.objects:
    if o.type.name != 'MonoScript':
        continue
    d = tt(o)
    script_info[(o.assets_file.name, o.path_id)] = (d.get('m_AssemblyName'), d.get('m_Namespace'), d.get('m_ClassName'))
print('MonoScript objects:', len(script_info))
wc_scripts = sorted(i for i in script_info.values() if 'worldcreator' in ('%s %s' % (i[1], i[2])).lower())
print('WorldCreator scripts:')
for i in wc_scripts:
    print('   ', i)

# ------------------------------------------------------------------ 3. MonoBehaviour attribution
print('\n===== 3. MONOBEHAVIOURS PER SCRIPT (type tree, raw-byte fallback)')
usage = collections.Counter()
per_file = collections.defaultdict(lambda: [0, 0])  # file -> [typetree ok, raw fallback]
errs = collections.Counter()
unresolved = 0
missing_asm_instances = []
cache = {}
for o in env.objects:
    if o.type.name != 'MonoBehaviour':
        continue
    d = tt(o)
    if '__error__' in d:
        errs[d['__error__'][:110]] += 1
        per_file[o.assets_file.name][1] += 1
        try:
            raw = o.get_raw_data()
            go_ptr = {'m_FileID': struct.unpack_from('<i', raw, 0)[0], 'm_PathID': struct.unpack_from('<q', raw, 4)[0]}
            sp = {'m_FileID': struct.unpack_from('<i', raw, 16)[0], 'm_PathID': struct.unpack_from('<q', raw, 20)[0]}
        except Exception:  # noqa
            unresolved += 1
            continue
    else:
        per_file[o.assets_file.name][0] += 1
        go_ptr = d.get('m_GameObject')
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
    if str(info[0]).lower() not in managed:
        missing_asm_instances.append((o.assets_file.name, o.path_id, info, go_ptr))
print('per file [type tree ok, raw fallback]:', dict(per_file))
print('type-tree errors:', dict(errs))
print('MonoBehaviours with unresolved script pointer:', unresolved, '| attributed:', sum(usage.values()))
print('MonoBehaviours whose script assembly is NOT in Managed/:', len(missing_asm_instances))
for fname, pid, info, go_ptr in missing_asm_instances:
    print('   ', fname, pid, info, 'GameObject ptr', go_ptr)

print('\nCounts for terrain / world / spawn related scripts:')
wanted = ('terrain', 'tc_', 'chunk', 'cull', 'spawn', 'networkconnector', 'matchcomponent', 'worlditem', 'loadterrain', 'createterrain', 'steam', 'zombiespawner')
for info, c in sorted(usage.items(), key=lambda kv: (-kv[1], str(kv[0]))):
    if any(w in ('%s %s' % (info[1], info[2])).lower() for w in wanted):
        print('  %5d x %s' % (c, info))

# ------------------------------------------------------------------ 4. target GO
print('\n===== 4. GAMEOBJECT WorldCreatorTerrain')


def components_of(go_obj):
    res = []
    for c in tt(go_obj).get('m_Component', []):
        ptr = c.get('component') or c.get('second') or c
        co = resolve(go_obj.assets_file, ptr.get('m_FileID', 0), ptr.get('m_PathID', 0))
        if co is not None:
            res.append(co)
    return res


def go_of_transform(tobj):
    gp = tt(tobj).get('m_GameObject', {})
    return resolve(tobj.assets_file, gp.get('m_FileID', 0), gp.get('m_PathID', 0))


def subtree_census(root_go, limit=30000):
    census = collections.Counter()
    sample = []
    count = 0
    stack = [root_go]
    while stack and count < limit:
        g = stack.pop()
        count += 1
        if len(sample) < 10:
            sample.append(tt(g).get('m_Name'))
        for co in components_of(g):
            census[co.type.name] += 1
            if co.type.name == 'Transform':
                for ch in tt(co).get('m_Children', []):
                    cto = resolve(co.assets_file, ch.get('m_FileID', 0), ch.get('m_PathID', 0))
                    cg = go_of_transform(cto) if cto is not None else None
                    if cg is not None:
                        stack.append(cg)
    return count, census, sample


def describe_terrain_pointer(af, d, label):
    tdp = d.get('m_TerrainData', {})
    print('        %s.m_TerrainData raw pointer: %s' % (label, tdp))
    print('        externals of this file:', [str(getattr(e, 'path', getattr(e, 'name', e))) for e in getattr(af, 'externals', [])])
    td = resolve(af, tdp.get('m_FileID', 0), tdp.get('m_PathID', 0))
    if td is None:
        print('        TerrainData: NOT resolved')
        return
    print('        TerrainData -> file %s, path_id %d, type %s, %d bytes' % (td.assets_file.name, td.path_id, td.type.name, td.byte_size))
    print('        TerrainData summary:', short(summarize(tt(td), 0, 3), 3000))


def describe_component(af, co):
    t = co.type.name
    d = tt(co)
    print('    - %s (path_id %d, %d bytes)' % (t, co.path_id, co.byte_size))
    if t == 'Transform':
        father = d.get('m_Father', {})
        fo = resolve(af, father.get('m_FileID', 0), father.get('m_PathID', 0))
        fgo = go_name(go_of_transform(fo)) if fo is not None else None
        print('        position=%s scale=%s parent=%r children=%d' % (
            short(d.get('m_LocalPosition'), 120), short(d.get('m_LocalScale'), 120), fgo, len(d.get('m_Children', []))))
    elif t == 'MonoBehaviour':
        sp = d.get('m_Script', {})
        so = resolve(af, sp.get('m_FileID', 0), sp.get('m_PathID', 0))
        ident = script_info.get((so.assets_file.name, so.path_id)) if so is not None else None
        extra = {k: v for k, v in d.items() if k not in ('m_GameObject', 'm_Script')}
        print('        script=%s enabled=%s' % (ident, d.get('m_Enabled')))
        print('        serialized fields in the build (besides m_GameObject/m_Script):', short(extra, 1500))
        print('        object size: %d bytes (m_GameObject 12 + m_Enabled padded 4 + m_Script 12 + m_Name empty string 4 = 32)' % co.byte_size)
    elif t in ('Terrain', 'TerrainCollider'):
        print('        fields:', short(summarize(d, 0, 1), 1800))
        describe_terrain_pointer(af, d, t)


targets = []
for o in env.objects:
    if o.type.name == 'GameObject' and 'worldcreator' in str(tt(o).get('m_Name', '')).lower():
        targets.append(o)
print('GameObjects found:', len(targets))
for o in targets:
    d = tt(o)
    print('  GameObject %r in %s (path_id %d) layer=%s tag=%s active=%s' % (
        d.get('m_Name'), o.assets_file.name, o.path_id, d.get('m_Layer'), d.get('m_Tag'), d.get('m_IsActive')))
    for co in components_of(o):
        try:
            describe_component(o.assets_file, co)
        except Exception:  # noqa
            print('    - error describing', co.type.name)
            traceback.print_exc()
    n, census, sample = subtree_census(o)
    print('  subtree: %d GameObjects (including the root); first names: %s' % (n, sample))
    print('  subtree component census:', dict(census.most_common(15)))

# ------------------------------------------------------------------ 5. census
print('\n===== 5. TERRAIN CENSUS')
count = collections.Counter()
for o in env.objects:
    if o.type.name in ('Terrain', 'TerrainCollider', 'TerrainData'):
        count[(o.assets_file.name, o.type.name)] += 1
for k, v in sorted(count.items()):
    print('  %-28s %-16s %d' % (k[0], k[1], v))

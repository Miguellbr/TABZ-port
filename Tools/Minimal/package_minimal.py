#!/usr/bin/env python3
"""Package the TabzMinimal Unity project from an AssetRipper export.

Rules:
  * Original MonoBehaviours are NEVER removed. Their script references are re-pointed to the class that lives in the
    original DLL: guid = the DLL plugin guid written into its .meta, fileID = Unity's MD4 rule over namespace+class
    (validated here against WorldCreatorReference = -711467253).
  * A reference is rewritten only when (assembly, namespace, class) really exists in the DLL type list. Anything else is
    kept untouched and listed in RECOVERY-REPORT.md.
  * Nothing is invented: positions, scales, parameters inside the original assets are copied as exported.
"""
import argparse
import collections
import hashlib
import pathlib
import re
import shutil
import struct
import sys

ap = argparse.ArgumentParser()
ap.add_argument('--export', required=True, help='AssetRipper ExportedProject directory')
ap.add_argument('--types', required=True, help='types.txt from TabzIlTypes (Assembly|Namespace|Name|Base)')
ap.add_argument('--dlls', required=True, help='directory with the patched DLLs that ship in the project')
ap.add_argument('--template', required=True, help='Tools/Minimal/project')
ap.add_argument('--project', required=True, help='output TabzMinimal directory')
ap.add_argument('--roots', default='ServiceLocator,SoundStuff', help='original scene root GameObjects to carry over')
args = ap.parse_args()


# ---------------------------------------------------------------- MD4 and fileID
def _lrot(x, n):
    return ((x << n) | (x >> (32 - n))) & 0xffffffff


def md4(msg):
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


assert md4(b'abc').hex() == 'a448017aaf21d8525fc10ae87aa6729d', 'MD4 broken'
assert file_id('WorldCreatorEngine.Utilities', 'WorldCreatorReference') == -711467253, 'fileID formula no longer matches the validated value'


def plugin_guid(name):
    return hashlib.md5(('tabz-minimal:' + name).encode('utf-8')).hexdigest()


# ---------------------------------------------------------------- inputs
export = pathlib.Path(args.export).resolve()
assets = export / 'Assets'
project = pathlib.Path(args.project).resolve()
project_assets = project / 'Assets'
report = []


def R(line=''):
    report.append(line)


types = collections.defaultdict(set)
for line in pathlib.Path(args.types).read_text(encoding='utf-8').splitlines():
    parts = line.split('|')
    if len(parts) >= 3:
        types[parts[0].lower()].add((parts[1], parts[2]))

guid_path = {}
for meta in assets.rglob('*.meta'):
    m = re.search(r'^guid:\s*([0-9a-f]{32})', meta.read_text(encoding='utf-8', errors='replace'), re.M)
    if m:
        guid_path[m.group(1)] = pathlib.Path(str(meta)[:-5])

# script guid (AssetRipper .cs) -> class in the DLL
ns_rx = re.compile(r'^\s*namespace\s+([\w.]+)', re.M)
script_map = {}
scripts_dir = assets / 'Scripts'
for cs in scripts_dir.rglob('*.cs'):
    meta = cs.with_name(cs.name + '.meta')
    if not meta.is_file():
        continue
    m = re.search(r'^guid:\s*([0-9a-f]{32})', meta.read_text(encoding='utf-8', errors='replace'), re.M)
    if not m:
        continue
    asm = cs.relative_to(scripts_dir).parts[0]
    text = cs.read_text(encoding='utf-8', errors='replace')
    nm = ns_rx.search(text)
    ns = nm.group(1) if nm else ''
    cls = cs.stem
    ok = (ns, cls) in types.get(asm.lower(), set())
    script_map[m.group(1)] = dict(asm=asm, ns=ns, cls=cls, ok=ok, file_id=file_id(ns, cls),
                                  new_guid=plugin_guid(asm), rel=cs.relative_to(assets).as_posix())
ok_count = sum(1 for v in script_map.values() if v['ok'])

SCRIPT_REF = re.compile(r'\{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}')
remapped = collections.Counter()
unmapped = collections.Counter()
unmapped_where = {}


def remap(text, where):
    def sub(m):
        g = m.group(1)
        info = script_map.get(g)
        if info and info['ok']:
            remapped[(info['asm'], info['ns'], info['cls'])] += 1
            return '{fileID: %d, guid: %s, type: 3}' % (info['file_id'], info['new_guid'])
        unmapped[g] += 1
        unmapped_where.setdefault(g, where)
        return m.group(0)
    return SCRIPT_REF.sub(sub, text)


# ---------------------------------------------------------------- dependency closure
TEXT_SUFFIX = {'.prefab', '.mat', '.asset', '.controller', '.anim', '.unity', '.physicmaterial', '.overridecontroller',
               '.mask', '.flare', '.guiskin', '.rendertexture', '.cubemap', '.fontsettings', '.playable'}


def closure(roots):
    seen, queue = set(), list(roots)
    while queue:
        cur = queue.pop()
        if cur in seen:
            continue
        seen.add(cur)
        if cur.suffix.lower() not in TEXT_SUFFIX:
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


resources = assets / 'Resources'
resource_files = [p for p in resources.rglob('*') if p.is_file() and p.suffix != '.meta']
needed = closure(resource_files)

# ---------------------------------------------------------------- project skeleton
shutil.copytree(args.template, project, dirs_exist_ok=True)
scene_guid = plugin_guid('scene')
ebs = project / 'ProjectSettings' / 'EditorBuildSettings.asset'
ebs.write_text(ebs.read_text(encoding='utf-8').replace('__SCENE_GUID__', scene_guid), encoding='utf-8', newline='\n')

META_DLL = ('fileFormatVersion: 2\nguid: {guid}\nPluginImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  iconMap: {{}}\n'
            '  executionOrder: {{}}\n  defineConstraints: []\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n'
            '  validateReferences: 1\n  platformData:\n  - first:\n      Any:\n    second:\n      enabled: 1\n      settings: {{}}\n'
            '  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
plugins = project_assets / 'Plugins' / 'Managed'
plugins.mkdir(parents=True, exist_ok=True)
shipped = []
for dll in sorted(pathlib.Path(args.dlls).glob('*.dll')):
    shutil.copy2(dll, plugins / dll.name)
    (plugins / (dll.name + '.meta')).write_text(META_DLL.format(guid=plugin_guid(dll.stem)), encoding='utf-8', newline='\n')
    shipped.append((dll.name, dll.stat().st_size, plugin_guid(dll.stem)))

# ---------------------------------------------------------------- copy assets
MAX_FILE = 95 * 1024 * 1024
copied, skipped_big = 0, []
copied_bytes = 0
for src in sorted(needed):
    rel = src.relative_to(assets)
    dst = project_assets / rel
    if src.stat().st_size > MAX_FILE:
        skipped_big.append(rel.as_posix())
        continue
    dst.parent.mkdir(parents=True, exist_ok=True)
    if src.suffix.lower() in TEXT_SUFFIX:
        text = src.read_text(encoding='utf-8', errors='replace')
        dst.write_text(remap(text, rel.as_posix()), encoding='utf-8', newline='\n')
    else:
        shutil.copy2(src, dst)
    meta = pathlib.Path(str(src) + '.meta')
    if meta.is_file():
        shutil.copy2(meta, pathlib.Path(str(dst) + '.meta'))
    copied += 1
    copied_bytes += src.stat().st_size

# ---------------------------------------------------------------- original project settings (physics, tags, layers...)
ps_found = []
ps_dir = export / 'ProjectSettings'
for name in ('TagManager.asset', 'DynamicsManager.asset', 'TimeManager.asset', 'NavMeshAreas.asset', 'NavMeshProjectSettings.asset'):
    f = ps_dir / name
    if f.is_file():
        shutil.copy2(f, project / 'ProjectSettings' / name)
        ps_found.append(name)

# ---------------------------------------------------------------- carry over original scene root objects
SCENE_HEAD = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
              '--- !u!29 &1\nOcclusionCullingSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_OcclusionBakeSettings:\n'
              '    smallestOccluder: 5\n    smallestHole: 0.25\n    backfaceThreshold: 100\n  m_SceneGUID: 00000000000000000000000000000000\n'
              '  m_OcclusionCullingData: {fileID: 0}\n'
              '--- !u!104 &2\nRenderSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 9\n  m_Fog: 0\n  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}\n'
              '  m_FogMode: 3\n  m_FogDensity: 0.01\n  m_LinearFogStart: 0\n  m_LinearFogEnd: 300\n'
              '  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}\n  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}\n'
              '  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}\n  m_AmbientIntensity: 1\n  m_AmbientMode: 0\n  m_SkyboxMaterial: {fileID: 0}\n'
              '  m_HaloStrength: 0.5\n  m_FlareStrength: 1\n  m_FlareFadeSpeed: 3\n  m_IndirectSpecularColor: {r: 0.446, g: 0.454, b: 0.471, a: 1}\n'
              '  m_UseRadianceAmbientProbe: 0\n'
              '--- !u!157 &3\nLightmapSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 12\n  m_GIWorkflowMode: 1\n  m_GISettings:\n'
              '    serializedVersion: 2\n    m_BounceScale: 1\n    m_IndirectOutputScale: 1\n    m_AlbedoBoost: 1\n    m_EnvironmentLightingMode: 0\n'
              '    m_EnableBakedLightmaps: 0\n    m_EnableRealtimeLightmaps: 0\n  m_LightmapEditorSettings:\n    serializedVersion: 12\n    m_Resolution: 2\n'
              '    m_BakeResolution: 40\n    m_AtlasSize: 1024\n    m_AO: 0\n    m_AOMaxDistance: 1\n    m_CompAOExponent: 1\n    m_CompAOExponentDirect: 0\n'
              '    m_ExtractAmbientOcclusion: 0\n    m_Padding: 2\n    m_LightmapParameters: {fileID: 0}\n    m_LightmapsBakeMode: 1\n'
              '    m_TextureCompression: 1\n    m_ReflectionCompression: 2\n  m_LightingDataAsset: {fileID: 0}\n  m_LightingSettings: {fileID: 0}\n'
              '--- !u!196 &4\nNavMeshSettings:\n  serializedVersion: 2\n  m_ObjectHideFlags: 0\n  m_BuildSettings:\n    serializedVersion: 2\n'
              '    agentTypeID: 0\n    agentRadius: 0.5\n    agentHeight: 2\n    agentSlope: 45\n    agentClimb: 0.4\n    ledgeDropHeight: 0\n'
              '    maxJumpAcrossDistance: 0\n    minRegionArea: 2\n    manualCellSize: 0\n    cellSize: 0.16666667\n    tileSize: 256\n'
              '    accuratePlacement: 0\n    maxJobWorkers: 0\n    preserveTilesOutsideBounds: 0\n    debug:\n      m_Flags: 0\n  m_NavMeshData: {fileID: 0}\n')

scene_report = []
extracted_text = ''
root_names = [r.strip() for r in args.roots.split(',') if r.strip()]
world_scene = next(iter(assets.rglob('TABZ_World.unity')), None)
if world_scene is None:
    scene_report.append('TABZ_World.unity not found in the export; no original scene roots carried over.')
else:
    text = world_scene.read_text(encoding='utf-8', errors='replace')
    blocks = {}
    for part in re.split(r'(?m)^(?=--- !u!\d+ &\d+)', text):
        hm = re.match(r'--- !u!(\d+) &(\d+)', part)
        if hm:
            blocks[hm.group(2)] = (int(hm.group(1)), part)
    del text
    roots_found = {}
    for anchor, (cls, body) in blocks.items():
        if cls != 1:
            continue
        nm = re.search(r'^  m_Name:\s*(.*)$', body, re.M)
        if not nm or nm.group(1).strip() not in root_names:
            continue
        comps = re.findall(r'component:\s*\{fileID:\s*(\d+)\}', body)
        tf = [c for c in comps if c in blocks and blocks[c][0] in (4, 224)]
        if tf and re.search(r'^  m_Father:\s*\{fileID:\s*0\}', blocks[tf[0]][1], re.M):
            roots_found[nm.group(1).strip()] = anchor
    include, stack = set(), list(roots_found.values())
    while stack:
        aid = stack.pop()
        if aid in include or aid not in blocks:
            continue
        include.add(aid)
        cls, body = blocks[aid]
        if cls == 1:
            stack += re.findall(r'component:\s*\{fileID:\s*(\d+)\}', body)
        else:
            gm = re.search(r'^  m_GameObject:\s*\{fileID:\s*(\d+)\}', body, re.M)
            if gm:
                stack.append(gm.group(1))
            if cls in (4, 224):
                ch = re.search(r'^  m_Children:\n((?:  - \{fileID: \d+\}\n)*)', body, re.M)
                if ch:
                    stack += re.findall(r'fileID:\s*(\d+)', ch.group(1))
    nulled = collections.Counter()
    out_blocks = []
    for aid in sorted(include, key=int):
        cls, body = blocks[aid]

        def null_dangling(m, aid=aid):
            tgt = m.group(1)
            if tgt in blocks and tgt not in include:
                nulled[blocks[aid][0]] += 1
                return '{fileID: 0}'
            return m.group(0)
        body = re.sub(r'\{fileID: (\d+)\}', null_dangling, body)
        out_blocks.append(remap(body, 'scene:' + str(aid)))
    extracted_text = SCENE_HEAD + ''.join(out_blocks)
    scene_report.append('roots requested: ' + ', '.join(root_names))
    scene_report.append('roots found: ' + (', '.join('%s (anchor %s)' % kv for kv in roots_found.items()) or 'none'))
    scene_report.append('blocks carried over: %d (of %d in the original scene)' % (len(include), len(blocks)))
    scene_report.append('references to objects outside the carried subtree set to null: %d (by component class id: %s)' % (
        sum(nulled.values()), dict(nulled)))
    # assets referenced by the extracted blocks must travel too
    extra = set()
    for g in set(re.findall(r'guid:\s*([0-9a-f]{32})', extracted_text)):
        dep = guid_path.get(g)
        if dep and dep.is_file() and dep.suffix.lower() not in {'.cs', '.dll'}:
            extra |= closure([dep])
    extra -= needed
    for src in sorted(extra):
        rel = src.relative_to(assets)
        dst = project_assets / rel
        if src.stat().st_size > MAX_FILE:
            skipped_big.append(rel.as_posix())
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        if src.suffix.lower() in TEXT_SUFFIX:
            dst.write_text(remap(src.read_text(encoding='utf-8', errors='replace'), rel.as_posix()), encoding='utf-8', newline='\n')
        else:
            shutil.copy2(src, dst)
        meta = pathlib.Path(str(src) + '.meta')
        if meta.is_file():
            shutil.copy2(meta, pathlib.Path(str(dst) + '.meta'))
        copied += 1
        copied_bytes += src.stat().st_size
    scene_report.append('extra assets referenced by the carried objects: %d files' % len(extra))

scene_dir = project_assets / 'Scenes'
scene_dir.mkdir(parents=True, exist_ok=True)
(scene_dir / 'TabzMinimal.unity').write_text(extracted_text or SCENE_HEAD, encoding='utf-8', newline='\n')
(scene_dir / 'TabzMinimal.unity.meta').write_text('fileFormatVersion: 2\nguid: %s\n' % scene_guid, encoding='utf-8', newline='\n')

# ---------------------------------------------------------------- report
FUNCTIONAL = {'.prefab', '.controller', '.overridecontroller', '.anim', '.physicmaterial', '.mask'}
FUNCTIONAL_KINDS = {'MonoBehaviour', 'Avatar', 'AnimationClip', 'NavMeshData', 'TerrainData'}


def asset_kind(p):
    if p.suffix.lower() == '.asset':
        head = p.read_bytes()[:400].decode('utf-8', 'replace')
        m = re.search(r'\n([A-Za-z0-9_]+):\s*\n', head)
        return m.group(1) if m else 'asset'
    return p.suffix.lower()


def functional(p):
    k = asset_kind(p)
    return k in FUNCTIONAL or k in FUNCTIONAL_KINDS


def sample_fields(text, guid):
    i = text.find('guid: ' + guid)
    if i < 0:
        return []
    after = text[i:i + 1500]
    return re.findall(r'^  (\w+):', after, re.M)[:10]


R('# TabzMinimal recovery report')
R()
R('Generated by Tools/Minimal/package_minimal.py. Static packaging only: nothing here proves the project runs.')
R()
R('## Script reference recovery')
R()
R('- fileID formula validated against WorldCreatorReference (-711467253).')
R('- exported script classes: %d | present in the shipped DLLs (assembly + namespace + class): %d' % (len(script_map), ok_count))
R('- references rewritten to the DLL classes: %d' % sum(remapped.values()))
R('- references NOT rewritten (component kept as exported): %d' % sum(unmapped.values()))
R()
R('Shipped plugin DLLs (guid written into each .meta):')
for name, size, g in shipped:
    R('- `%s` %d bytes guid `%s`' % (name, size, g))
R()
R('Rewritten references by class (top 60):')
for (asm, ns, cls), c in remapped.most_common(60):
    R('- %5d x `%s%s` [%s] fileID %d' % (c, ns + '.' if ns else '', cls, asm, file_id(ns, cls)))
R()
R('Exported scripts that could NOT be matched to a DLL class (kept, not removed):')
bad = [(g, v) for g, v in script_map.items() if not v['ok'] and g in unmapped]
for g, v in sorted(bad, key=lambda kv: -unmapped[kv[0]])[:60]:
    R('- %5d x `%s%s` [assembly folder %s] guid %s (%s)' % (unmapped[g], v['ns'] + '.' if v['ns'] else '', v['cls'], v['asm'], g, v['rel']))
R()
R('Script guids that are not an exported script at all (built-in package scripts such as UnityEngine.UI keep their own guid):')
for g, c in unmapped.most_common(40):
    if g in script_map:
        continue
    first = unmapped_where[g]
    fields = ''
    try:
        p = project_assets / first if not first.startswith('scene:') else None
        if p and p.is_file():
            fields = ', '.join(sample_fields(p.read_text(encoding='utf-8', errors='replace'), g))
    except OSError:
        pass
    R('- %5d x guid %s first seen in `%s`; fields of that component: %s' % (c, g, first, fields or 'n/a'))
R()
R('## Assets')
R()
R('- files copied (all of Resources plus their dependencies): %d, %.1f MiB' % (copied, copied_bytes / 1048576))
if skipped_big:
    R('- SKIPPED (over 95 MiB, GitHub limit): ' + ', '.join(skipped_big))
R('- original ProjectSettings copied: ' + (', '.join(ps_found) or 'none found'))
R()
R('## Carried-over original scene objects')
R()
for line in scene_report:
    R('- ' + line)
R()
R('## Prefab audit')
for rel in ('TABS_Player_Network.prefab', 'TABZOMBIE.prefab', 'items/Ak74.prefab', 'NetworkStuff.prefab'):
    prefab = resources / rel
    R()
    R('### Resources/%s' % rel)
    if not prefab.is_file():
        R('not found')
        continue
    text = prefab.read_text(encoding='utf-8', errors='replace')
    go_count = len(re.findall(r'(?m)^--- !u!1 &', text))
    all_blocks = len(re.findall(r'(?m)^--- !u!\d+ &', text))
    mono = re.findall(r'(?m)^--- !u!114 &', text)
    R('- size %d bytes | GameObjects %d | components (all non-GameObject blocks) %d | MonoBehaviours %d' % (
        prefab.stat().st_size, go_count, all_blocks - go_count, len(mono)))
    per = collections.Counter(re.findall(r'm_Script:\s*\{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}', text))
    R('- scripts (class | assembly | original guid | new guid | fileID | status):')
    for g, c in per.most_common():
        info = script_map.get(g)
        if info:
            R('  - %3d x `%s%s` | %s | %s | %s | %d | %s' % (c, info['ns'] + '.' if info['ns'] else '', info['cls'], info['asm'], g,
                                                        info['new_guid'], info['file_id'], 'REMAPPED' if info['ok'] else 'NOT FOUND IN DLL - kept'))
        else:
            R('  - %3d x <not an exported script> | guid %s | kept as is' % (c, g))
    deps = sorted(closure([prefab]) - {prefab})
    func = [d for d in deps if functional(d)]
    vis = [d for d in deps if not functional(d)]
    R('- dependencies: %d files (%.1f MiB). Behaviour-related (prefabs, animation, controllers, avatars, physics, script assets): %d | visual/audio only (materials, textures, meshes, shaders, fonts): %d' % (
        len(deps), sum(d.stat().st_size for d in deps if d.is_file()) / 1048576, len(func), len(vis)))
    R('- behaviour-related dependencies:')
    for d in func[:80]:
        R('  - `%s`' % d.relative_to(assets).as_posix())
    kinds = collections.Counter(d.suffix.lower() for d in vis)
    R('- visual dependencies by type: %s' % dict(kinds))

out = project / 'RECOVERY-REPORT.md'
out.write_text('\n'.join(report) + '\n', encoding='utf-8', newline='\n')
print('\n'.join(report[:60]))
print('... full report written to', out)
print('script classes mapped %d/%d | refs rewritten %d | refs kept %d | files copied %d (%.1f MiB)' % (
    ok_count, len(script_map), sum(remapped.values()), sum(unmapped.values()), copied, copied_bytes / 1048576))

#!/usr/bin/env python3
"""
Author the NestedGyroidFlora species' four per-element configs and its Spawn Matrix row.

    python3 Tools/Build/author_nested_gyroid_flora_assets.py           # report
    python3 Tools/Build/author_nested_gyroid_flora_assets.py --check   # fail on drift
    python3 Tools/Build/author_nested_gyroid_flora_assets.py --write   # author the assets

The generator is the source and the assets are the build (Docs/TOOLING.md). Docs/ECOSYSTEM.md §58.6.

WHAT THE FOUR ELEMENTS ARE: the GYROID FLORA's four. The base sheet is that plant's own tiling
(NestedGyroidTemplate), and each element's prism is that plant's prism for the element - Variant.LeafSize and
Variant.LatticeScale QUOTED out of `Gyroid Flora <Element>.asset` (Time 9 x 3.4 x 1.5; Mass 7 x 4.5 x 3.5;
Space 40 x 1 x 1 on a 3.19x lattice; Charge 4.28 x 1.62 x 0.71, the plate the gyroid fitted to its shields),
scaled with NestedGyroidConfigSO.CellSize / 120. On top, the platform's element laws: Time grows fastest
(Flora.ResolveGrowPeriod), Charge is armoured (ShieldPeriod 1, which author_charge_flora_shields.py gates),
each drops its own element's heart. Re-run this after any edit to a Gyroid Flora element.

THE HEART IS PINNED, NOT SIZED - and that is a decision, stated so nobody mistakes it for an omission.
Tools/Build/author_lifeform_heart_sizes.py owns HeartWorldScale fleet-wide and solves K so the LARGEST
body lands on HEART_MAX. Its flora body model (a disc of N x footprint, N capped at 400) would measure
this species at ~270 against today's largest, Nerve, at 158 - so registering it would RE-PRICE every
heart in the game downward (a balance change: heart scale is the collect reward). So this species is
deliberately NOT in that tool's FLORA_PREFABS; its heart is authored here at exactly HEART_MAX, read out
of that tool so the two cannot disagree. It is the biggest lifeform in the project; the top of the band
is the honest place for it, and it re-prices nobody.

POPULATION: a specimen. One founder, one live plant per element - the plant is ~2,000 prisms and a whole
prismscape - and GrowthPerOffspring 0: it does not reproduce. With a cap of 1 any quota would be a number
nothing ever reads (the trap author_flora_populations.py's GrowthPerOffspring on lattice configs is), so
the asset says so outright. Consequence: the Squirrel's nourish joust declines on it (Flora.Nourish). In no SpawnProfile: the Spawn Matrix toy IS its
deployment (re-prove by grepping the four GUIDs across _SO_Assets before inheriting the claim).

THE MATRIX ROW goes immediately BEFORE the Borromean row: author_borromean_flora_assets.py deletes its own
row and re-appends it at the END of the flora list, so a row placed after it would make that tool's
--check report drift. author_mandelbulb_flora_assets.py replaces its rows in place.

GUIDs are md5("cosmicshore/nestedgyroid/<asset name>"), so a re-run is idempotent and --check compares
content; each is asserted to be owned by no other .meta before anything is written.
"""
import argparse, hashlib, importlib.util, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, 'Assets')), \
    f'repo root resolved to {ROOT}, which has no Assets/ - this script lives two levels down'
A = lambda *p: os.path.join(ROOT, 'Assets', *p)

PREFAB  = A('_Prefabs/FloraAndFauna/NestedGyroidFlora.prefab')
CONFIG  = A('_SO_Assets/Lifeforms/NestedGyroidConfig.asset')
LIFEDIR = A('_SO_Assets/Lifeforms')
MATRIX  = A('_SO_Assets/Toys/Toy_SpawnMatrix.asset')
FLORACS = A('_Scripts/Controller/Environment/FloraAndFauna/NestedGyroidFlora.cs')

FLORA_CONFIG_SCRIPT_GUID = 'a32a297a7606432885f4d3e1f83bea9a'   # FloraConfigurationSO.cs.meta
FLORA_COMPONENT_FILEID   = 8186157953239024492                  # the prefab's root NestedGyroidFlora

# Spawn Matrix order (every row in the toy lists Charge, Mass, Space, Time).
ELEMENTS = [('Charge', 1), ('Mass', 2), ('Space', 3), ('Time', 4)]
ROW_NAME = 'Nested Gyroid'

SEED_FLOOR = 1
CAP        = 1
COOLDOWN   = 5
MATURITY   = 0.5
SPREAD     = 480      # a plant is a 240-unit cube; an offspring belongs well clear of it


def guid(name):
    return hashlib.md5(f'cosmicshore/nestedgyroid/{name}'.encode()).hexdigest()


def meta_guid(path):
    m = re.search(r'^guid: ([0-9a-f]{32})$', open(path + '.meta').read(), re.M)
    if not m: sys.exit(f'{path}.meta has no guid')
    return m.group(1)


def heart_max():
    """HEART_MAX out of the fleet heart sizer itself, so the pin cannot drift from the band."""
    p = os.path.join(ROOT, 'Tools', 'Build', 'author_lifeform_heart_sizes.py')
    spec = importlib.util.spec_from_file_location('author_lifeform_heart_sizes', p)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    assert 'NestedGyroid' not in mod.FLORA_PREFABS, \
        'the heart sizer now measures NestedGyroid - stop pinning the heart here (see the docstring)'
    return mod.HEART_MAX


def config_budget():
    m = re.search(r'^  PrismBudget: (\d+)$', open(CONFIG).read(), re.M)
    if not m: sys.exit('PrismBudget not found in NestedGyroidConfig.asset')
    return int(m.group(1))


def gyroid_element(element):
    """The GYROID FLORA's own per-element prism - its config's Variant.LeafSize and Variant.LatticeScale - QUOTED,
    never retyped: the nested gyroid's base sheet is that plant's tiling, so its element proportions are that
    plant's too (Docs/ECOSYSTEM.md §58.7). LatticeScale -1 (absent) = the template's native period."""
    path = os.path.join(LIFEDIR, f'Gyroid Flora {element}.asset')
    if not os.path.exists(path): sys.exit(f'{os.path.relpath(path, ROOT)} is missing - it is the template')
    t = open(path).read()
    m = re.search(r'^    LeafSize: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}$', t, re.M)
    if not m: sys.exit(f'{os.path.relpath(path, ROOT)} has no Variant.LeafSize')
    ls = re.search(r'^    LatticeScale: ([-\d.e]+)$', t, re.M)
    return m.groups(), (ls.group(1) if ls else '-1')


def build_config(element, value, prefab_guid, heart, budget):
    leaf, lattice = gyroid_element(element)
    quota = 0                         # a specimen: does not reproduce (see the docstring)
    shield = 1 if element == 'Charge' else -1
    return (
        '%YAML 1.1\n'
        '%TAG !u! tag:unity3d.com,2011:\n'
        '--- !u!114 &11400000\n'
        'MonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n'
        '  m_CorrespondingSourceObject: {fileID: 0}\n'
        '  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n'
        '  m_GameObject: {fileID: 0}\n'
        '  m_Enabled: 1\n'
        '  m_EditorHideFlags: 0\n'
        f'  m_Script: {{fileID: 11500000, guid: {FLORA_CONFIG_SCRIPT_GUID}, type: 3}}\n'
        f'  m_Name: Nested Gyroid Flora {element}\n'
        '  m_EditorClassIdentifier:\n'
        f'  FloraPrefab: {{fileID: {FLORA_COMPONENT_FILEID}, guid: {prefab_guid}, type: 3}}\n'
        '  SpawnProbability: 1\n'
        f'  InitialSpawnCount: {SEED_FLOOR}\n'
        '  OverrideDefaultPlantPeriod: 0\n'
        '  NewPlantPeriod: 9999999\n'
        f'  PopulationSize: {SEED_FLOOR}\n'
        f'  MaxLivePopulation: {CAP}\n'
        f'  GrowthPerOffspring: {quota}\n'
        '  OffspringPerBirth: 1\n'
        f'  ReproductionCooldownSeconds: {COOLDOWN}\n'
        f'  MaturityFraction: {MATURITY}\n'
        f'  OffspringSpread: {SPREAD}\n'
        f'  Element: {value}\n'
        '  Variant:\n'
        '    Enabled: 1\n'
        f'    HeartWorldScale: {heart}\n'
        f'    LeafSize: {{x: {leaf[0]}, y: {leaf[1]}, z: {leaf[2]}}}\n'
        '    GrowPeriod: -1\n'
        f'    LatticeScale: {lattice}\n'
        f'    ShieldPeriod: {shield}\n'
        '    WitherRingInterval: -1\n'
        '    MaxTotalSpawnedObjects: -1\n'
        '    MaxTotalSpawnedObjectsScale: -1\n'
        '    ItemsPerGrow: -1\n'
        '    RandomItems: -1\n'
        '    MaturationSeconds: -1\n'
        '    MaxSpawnsPerFrame: -1\n'
        '    PlantRadiusCellFraction: -1\n'
        '    PlantRadiusCellFractionMin: -1\n')


def asset_meta(g):
    return ('fileFormatVersion: 2\n'
            f'guid: {g}\n'
            'NativeFormatImporter:\n'
            '  externalObjects: {}\n'
            '  mainObjectFileID: 11400000\n'
            '  userData:\n'
            '  assetBundleName:\n'
            '  assetBundleVariant:\n')


def upsert_matrix_row(text, cfg_guids):
    row = (f'  - Name: {ROW_NAME}\n'
           '    ElementConfigs:\n'
           + ''.join(f'    - {{fileID: 11400000, guid: {g}, type: 2}}\n' for g in cfg_guids))
    m = re.search(r'^  floraSpecies:\n((?:  - Name: .*\n(?:    .*\n)+)*)', text, re.M)
    if not m: sys.exit('floraSpecies list not found in Toy_SpawnMatrix.asset')
    flora = re.sub(rf'^  - Name: {re.escape(ROW_NAME)}\n(?:    .*\n)+', '', m.group(1), flags=re.M)
    at = flora.find('  - Name: Borromean\n')
    flora = flora + row if at < 0 else flora[:at] + row + flora[at:]
    return text[:m.start(1)] + flora + text[m.end(1):]


def owners_elsewhere(g, ours):
    hits = []
    for dirpath, _, names in os.walk(os.path.join(ROOT, 'Assets')):
        for n in names:
            if not n.endswith('.meta'): continue
            p = os.path.join(dirpath, n)
            if p in ours: continue
            if re.search(rf'^guid: {g}$', open(p, errors='replace').read(), re.M):
                hits.append(os.path.relpath(p, ROOT))
    return hits


def plan():
    prefab_guid = meta_guid(PREFAB)
    heart, budget = heart_max(), config_budget()
    files, guids = {}, []
    for element, value in ELEMENTS:
        name = f'Nested Gyroid Flora {element}'
        p = os.path.join(LIFEDIR, name + '.asset')
        g = guid(name + '.asset')
        guids.append(g)
        files[p] = build_config(element, value, prefab_guid, heart, budget)
        files[p + '.meta'] = asset_meta(g)
    files[MATRIX] = upsert_matrix_row(open(MATRIX).read(), guids)
    return files, guids, dict(prefab=prefab_guid, heart=heart, budget=budget)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--write', action='store_true')
    a = ap.parse_args()

    files, guids, info = plan()
    print(f'NestedGyroidFlora: 4 element configs on one lattice (budget {info["budget"]}), '
          f'heart pinned at HEART_MAX {info["heart"]}, seed {SEED_FLOOR} / cap {CAP} per element '
          f'({4 * CAP} heart colliders across the four), Charge armoured')

    for g in guids:
        dup = owners_elsewhere(g, set(files))
        if dup:
            print(f'  FAIL: guid {g} already owned by {dup}')
            return 1

    # The matrix must carry the row exactly once, with exactly these four, before Borromean if present.
    t = files[MATRIX]
    rows = re.findall(rf'^  - Name: {re.escape(ROW_NAME)}\n((?:    .*\n)+)', t, re.M)
    if len(rows) != 1 or re.findall(r'guid: ([0-9a-f]{32})', rows[0]) != guids:
        print('  FAIL: the Spawn Matrix row did not splice as one row of these four configs')
        return 1
    if '  - Name: Borromean\n' in t and t.index(f'  - Name: {ROW_NAME}\n') > t.index('  - Name: Borromean\n'):
        print('  FAIL: the row landed after Borromean, which author_borromean_flora_assets.py would reorder')
        return 1

    changed = [p for p, body in files.items() if not os.path.exists(p) or open(p).read() != body]
    if a.write:
        for p, body in files.items():
            open(p, 'w').write(body)
        print(f'wrote {len(files)} files ({len(changed)} changed)')
        return 0
    if a.check:
        if changed:
            print('FAIL: drifted from what this tool would author:')
            for p in sorted(changed): print('  ' + os.path.relpath(p, ROOT))
            return 1
        print('OK: all assets match')
        return 0
    print(f'(dry run - {len(changed)} of {len(files)} files would change; pass --write)')
    return 0


if __name__ == '__main__':
    sys.exit(main())

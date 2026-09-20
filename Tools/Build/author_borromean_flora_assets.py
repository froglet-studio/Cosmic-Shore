#!/usr/bin/env python3
"""
Author the BorromeanFlora species: its prefab, its four per-element configs, and the two
script `.meta` files.

    python3 Tools/Build/author_borromean_flora_assets.py           # report
    python3 Tools/Build/author_borromean_flora_assets.py --check   # fail on drift
    python3 Tools/Build/author_borromean_flora_assets.py --write   # author the assets

The generator is the source and the assets are the build (Docs/TOOLING.md).  Every number
it writes is READ OUT OF `BorromeanSurfaceData.cs` rather than retyped, so the leaf the
configs author and the leaf the plant grows on cannot drift: re-run
`measure_borromean_minimal_surface.py --write` and then this, and the pair stays consistent
by construction.

GUIDs are `md5("cosmicshore/borromean/<stable name>")`, so a re-run is idempotent and
`--check` compares CONTENT rather than identity.  Every one is asserted to be owned by
exactly one `.meta` repo-wide before anything is written.

POPULATIONS ARE AUTHORED HERE, not by `author_flora_populations.py`.  That script authors
the species a SpawnProfile references, and this one is in none (see below) - so it lists
this family under `OWNED_ELSEWHERE` and prints the handoff, rather than skipping it
silently.

DEPLOYMENT, stated plainly because the claim rots: as of this commit the species is in NO
`SpawnProfileSO`, and is reachable through the freestyle Lifeform Matrix toy.  That is the
worm colony's precedent - an opt-in species - and it is deliberate: adopting it into a cell
means re-deriving that cell's volume ladder against a 116-unit, 354-prism plant, which is a
tuning pass this branch has not done.  Re-prove the claim by grepping these configs' GUIDs
across `_SO_Assets` before inheriting it.
"""
import hashlib, os, re, sys, argparse

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, 'Assets')), \
    f'repo root resolved to {ROOT}, which has no Assets/ - this script lives two levels down'
A = lambda *p: os.path.join(ROOT, 'Assets', *p)

TABLE   = A('_Scripts/Controller/Environment/FloraAndFauna/BorromeanSurfaceData.cs')
FLORACS = A('_Scripts/Controller/Environment/FloraAndFauna/BorromeanFlora.cs')
PREFAB  = A('_Prefabs/FloraAndFauna/BorromeanFlora.prefab')
DONOR   = A('_Prefabs/FloraAndFauna/SchwarzPFlora.prefab')
LIFEDIR = A('_SO_Assets/Lifeforms')
MATRIX  = A('_SO_Assets/Toys/Toy_LifeformMatrix.asset')

FLORA_CONFIG_SCRIPT_GUID = 'a32a297a7606432885f4d3e1f83bea9a'   # FloraConfigurationSO.cs.meta
DONOR_SCRIPT_GUID        = 'd3651588f6d7bbd4ba59c18516075c9a'   # AssembledFlora.cs.meta
FLORA_COMPONENT_FILEID   = 8186157953239024492                  # the donor's root MonoBehaviour

ELEMENTS = [('Charge', 1), ('Mass', 2), ('Space', 3), ('Time', 4)]

# The donor's limb (AssemblyBranch) and this species' own (Branch) - see build_prefab.
DONOR_SPINDLE_GUID       = '76cd644e62b88cc43a93674d5596971e'
BRANCH_SPINDLE_GUID      = 'f7ec1bbfe690a184b935434a6e0dcb7a'
SPINDLE_COMPONENT_FILEID = '5157459880619768690'

# ---- population model (the shape author_flora_populations.py authors, applied by hand
#      because this species is in no SpawnProfile for that script to walk) -------------
SEED_FLOOR   = 1      # InitialSpawnCount - one founder; the surface is a whole object
POPULATION   = 1      # PopulationSize - the seeder's only job is extinction recovery
CAP          = 8      # MaxLivePopulation - a CRYSTAL count: 8 always-on heart colliders
                      # per element, 32 across the four. THE dial if a cell reads busy.
QUOTA_FRAC   = 0.35   # GrowthPerOffspring = budget x this (the non-lattice rule)
COOLDOWN     = 5
MATURITY     = 0.5


def guid(name):
    return hashlib.md5(f'cosmicshore/borromean/{name}'.encode()).hexdigest()


def read_table():
    """Read the numbers the measurement emitted - never retype them."""
    if not os.path.exists(TABLE):
        sys.exit('BorromeanSurfaceData.cs is missing - run '
                 'Tools/Build/measure_borromean_minimal_surface.py --write first')
    t = open(TABLE).read()
    def const(name, kind='int'):
        m = re.search(rf'public const {"int" if kind=="int" else "float"} {name} = ([-\d.]+)f?;', t)
        if not m: sys.exit(f'{name} not found in BorromeanSurfaceData.cs')
        return int(m.group(1)) if kind == 'int' else float(m.group(1))
    def vec(name):
        m = re.search(rf'{name}\s*= new\(([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\);', t)
        if not m: sys.exit(f'{name} not found in BorromeanSurfaceData.cs')
        return tuple(float(g) for g in m.groups())
    return dict(sites=const('SiteCount'), orbit=const('OrbitSize'),
                radius=const('PlantRadius', 'float'),
                leaf=vec('TimeLeafSize'),
                leaves={e: vec(f'{e}LeafSize') for e in ('Charge', 'Mass', 'Space', 'Time')})


def v3(t):
    f = lambda x: ('%.5f' % x).rstrip('0').rstrip('.') or '0'
    return '{x: %s, y: %s, z: %s}' % (f(t[0]), f(t[1]), f(t[2]))


def script_meta(g):
    return ('fileFormatVersion: 2\n'
            f'guid: {g}\n'
            'MonoImporter:\n'
            '  externalObjects: {}\n'
            '  serializedVersion: 2\n'
            '  defaultReferences: []\n'
            '  executionOrder: 0\n'
            '  icon: {instanceID: 0}\n'
            '  userData:\n'
            '  assetBundleName:\n'
            '  assetBundleVariant:\n')


def asset_meta(g):
    return ('fileFormatVersion: 2\n'
            f'guid: {g}\n'
            'NativeFormatImporter:\n'
            '  externalObjects: {}\n'
            '  mainObjectFileID: 11400000\n'
            '  userData:\n'
            '  assetBundleName:\n'
            '  assetBundleVariant:\n')


def prefab_meta(g):
    return ('fileFormatVersion: 2\n'
            f'guid: {g}\n'
            'PrefabImporter:\n'
            '  externalObjects: {}\n'
            '  userData:\n'
            '  assetBundleName:\n'
            '  assetBundleVariant:\n')


def build_prefab(tbl, flora_guid):
    """Donor-clone SchwarzPFlora: same structure, same fileIDs, same nested crystal - so
    the file's serializer version and the crystal wiring are correct by construction.  Only
    the identity fields and the AssembledFlora-specific tuning change."""
    src = open(DONOR).read()
    out = src.replace('m_Name: SchwarzPFlora', 'm_Name: BorromeanFlora')
    out = out.replace(f'guid: {DONOR_SCRIPT_GUID}, type: 3', f'guid: {flora_guid}, type: 3')
    # The nested crystal is renamed through the PrefabInstance MODIFICATION that names it,
    # not through an `m_Name:` key - a nested instance carries its name as an override on
    # the source prefab, so the obvious replace matches nothing and fails silently.
    out = re.sub(r'^(\s*value: )SchwarzPCrystal$', r'\1BorromeanCrystal', out, flags=re.M)
    out = out.replace('m_Name: SchwarzPCrystal', 'm_Name: BorromeanCrystal')

    # Replace the donor's AssembledFlora tuning block with this species' own fields.
    donor_tail = re.search(r'(  leafSize: .*?\n)(?=--- !u!1001)', out, re.S)
    if not donor_tail:
        sys.exit('donor tuning block not found - SchwarzPFlora.prefab has changed shape')
    tail = (f'  leafSize: {v3(tbl["leaf"])}\n'
            '  growPeriod: 0.8\n'
            '  PlantPeriod: 120\n'
            '  stunDuration: 2\n'
            '  plantRadiusCellFraction: 0.5\n'
            '  plantRadiusCellFractionMin: 0.25\n'
            f'  maxTotalSpawnedObjects: {tbl["sites"]}\n'
            '  surfaceScale: 1\n')
    out = out[:donor_tail.start(1)] + tail + out[donor_tail.end(1):]

    # THE SPINDLE PREFAB IS PART OF THE GROWTH RULE, so the clone re-points it.  The donor
    # wires AssemblyBranch, a SINGLE arm hanging off the spindle's local -y from 0.5 to 6.7
    # units - and because a lattice species poses its spindle at its own PRISM with that
    # prism's rotation, the arm points wherever the prism's -y happens to face rather than
    # at any particular neighbour.  That is why Schwarz P's limbs read poorly; the gyroid
    # avoids it with a MIRRORED PAIR meeting at the prism (Docs/ECOSYSTEM.md 34.12), which
    # covers the bond in both directions.
    #
    # A Borromean limb is a THIRD shape: it spans one named bond, from its PARENT to its
    # child, and is stretched to fit.  Branch.prefab - the branch BranchingFlora uses - runs
    # forward from the spindle's own origin along local +z, which is exactly what
    # LookRotation aims and what BorromeanFlora.StretchToBond scales.  Both prefabs happen
    # to carry the Spindle component at the same fileID, so only the guid moves - and that
    # is asserted rather than assumed.
    if f'guid: {DONOR_SPINDLE_GUID}' not in out:
        sys.exit('the donor no longer wires AssemblyBranch - re-derive the spindle swap')
    out = out.replace(f'guid: {DONOR_SPINDLE_GUID}, type: 3',
                      f'guid: {BRANCH_SPINDLE_GUID}, type: 3')
    if f'spindle: {{fileID: {SPINDLE_COMPONENT_FILEID}, guid: {BRANCH_SPINDLE_GUID}, type: 3}}' not in out:
        sys.exit('the spindle reference did not land - Branch.prefab has changed shape')

    # healthBlocksForMaturity: one orbit x10; minHealthBlocks: a stub is dead.
    out = re.sub(r'^  healthBlocksForMaturity: \d+$',
                 f'  healthBlocksForMaturity: {tbl["orbit"] * 10}', out, count=1, flags=re.M)
    out = re.sub(r'^  minHealthBlocks: \d+$', '  minHealthBlocks: 4', out, count=1, flags=re.M)
    # A donor name that survives the clone is the shape of a rename that matched nothing:
    # the file still loads, nothing dangles, and the object is simply called by the donor's
    # name forever.  Cheap to assert, invisible otherwise.
    if 'SchwarzP' in out:
        left = [l.strip() for l in out.splitlines() if 'SchwarzP' in l]
        sys.exit(f'donor name survived the clone: {left}')
    return out


def existing_heart_scale(path):
    """`HeartWorldScale` is owned by `author_lifeform_heart_sizes.py`, which solves the whole
    fleet's band at once - so this generator READS it back instead of authoring it, and an
    asset that has not been through that tool yet keeps the 0 sentinel (= the set's default).

    Two fitters must not own one asset (`Docs/ECOSYSTEM.md` 35): forcing 0 here would revert
    the measured heart on every run of this script, and the two tools would undo each other
    forever with both `--check`s passing in between."""
    if not os.path.exists(path): return '0'
    m = re.search(r'^    HeartWorldScale: (\S+)$', open(path).read(), re.M)
    return m.group(1) if m else '0'


def build_config(tbl, element, value, prefab_guid, heart='0'):
    # THE ELEMENT IS THE PLATE.  A lifeform is its species and its element and nothing
    # else (Docs/ECOSYSTEM.md 40), so every element states its own leaf here - Time the
    # measured optimum, Mass more volume, Space more aspect, Charge fitted to its own
    # shielded octahedra.  All four come out of the measurement; none is retyped.
    leaf = tbl['leaves'][element]
    budget = tbl['sites']
    quota = max(1, int(budget * QUOTA_FRAC + 0.5))
    # A plant is PlantRadius across, so an offspring belongs clear of its parent.
    spread = int(tbl['radius'] * 1.5 + 0.5)
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
        f'  m_Name: Borromean Flora {element}\n'
        '  m_EditorClassIdentifier:\n'
        f'  FloraPrefab: {{fileID: {FLORA_COMPONENT_FILEID}, guid: {prefab_guid}, type: 3}}\n'
        '  SpawnProbability: 1\n'
        f'  InitialSpawnCount: {SEED_FLOOR}\n'
        '  OverrideDefaultPlantPeriod: 0\n'
        '  NewPlantPeriod: 9999999\n'
        f'  PopulationSize: {POPULATION}\n'
        f'  MaxLivePopulation: {CAP}\n'
        f'  GrowthPerOffspring: {quota}\n'
        '  OffspringPerBirth: 1\n'
        f'  ReproductionCooldownSeconds: {COOLDOWN}\n'
        f'  MaturityFraction: {MATURITY}\n'
        f'  OffspringSpread: {spread}\n'
        f'  Element: {value}\n'
        '  Variant:\n'
        '    Enabled: 1\n'
        f'    HeartWorldScale: {heart}\n'
        f'    LeafSize: {v3(leaf)}\n'
        '    GrowPeriod: -1\n'
        '    LatticeScale: -1\n'
        f'    ShieldPeriod: {shield}\n'
        '    WitherRingInterval: -1\n'
        f'    MaxTotalSpawnedObjects: {budget}\n'
        '    MaxTotalSpawnedObjectsScale: -1\n'
        '    ItemsPerGrow: -1\n'
        '    RandomItems: -1\n'
        '    MaturationSeconds: -1\n'
        '    MaxSpawnsPerFrame: -1\n'
        '    PlantRadiusCellFraction: -1\n'
        '    PlantRadiusCellFractionMin: -1\n')


def serialized_fields(cs_path):
    """Field names Unity would serialize from a C# file (attributes stripped line-initially
    so `float[] x` survives - see the asset-surgery skill)."""
    out = set()
    for line in open(cs_path):
        s = re.sub(r'^(?:\s*\[[^\]\n]*\]\s*)+', '', line.strip())
        m = re.match(r'(?:public|protected|private|internal)?\s*'
                     r'(?:readonly\s+)?[\w<>,\[\]\.\?]+\s+(\w+)\s*(?:=|;)', s)
        if not m: continue
        if 'const ' in s or 'static ' in s or '(' in s.split('=')[0]: continue
        if line.strip().startswith('//'): continue
        if '[SerializeField]' in line or re.match(r'\s*public\s', line):
            out.add(m.group(1))
    return out


def register_in_matrix(prefab_guid_unused, cfg_guids):
    """Splice a Borromean row into the freestyle Lifeform Matrix toy's flora kingdom.

    The species is in no SpawnProfile, so the matrix IS its deployment - and a species
    nothing can reach is a species nobody can look at.  Done here rather than by hand so
    `--check` covers it: a hand-edit to that asset would otherwise be invisible to every
    gate this branch adds.  Idempotent by construction - the row is rebuilt from the guids
    each run and replaces any existing Borromean row.
    """
    if not os.path.exists(MATRIX):
        sys.exit('Toy_LifeformMatrix.asset is missing')
    t = open(MATRIX).read()
    row = ('  - Name: Borromean\n'
           '    ElementConfigs:\n'
           + ''.join(f'    - {{fileID: 11400000, guid: {g}, type: 2}}\n' for g in cfg_guids))
    # Drop any row we wrote before, then append to the FLORA list - located by its own
    # key and terminated by the next top-level key, so a new kingdom added above or below
    # cannot make this splice land in the wrong list.
    t = re.sub(r'^  - Name: Borromean\n(?:    .*\n)+', '', t, flags=re.M)
    m = re.search(r'^  floraSpecies:\n((?:  - Name: .*\n(?:    .*\n)+)*)', t, re.M)
    if not m:
        sys.exit('floraSpecies list not found in Toy_LifeformMatrix.asset')
    return t[:m.end(1)] + row + t[m.end(1):]


def plan():
    tbl = read_table()
    flora_guid  = guid('BorromeanFlora.cs')
    table_guid  = guid('BorromeanSurfaceData.cs')
    prefab_guid = guid('BorromeanFlora.prefab')
    files = {
        FLORACS + '.meta': script_meta(flora_guid),
        TABLE   + '.meta': script_meta(table_guid),
        PREFAB:            build_prefab(tbl, flora_guid),
        PREFAB  + '.meta': prefab_meta(prefab_guid),
    }
    cfg_guids = []
    for name, value in ELEMENTS:
        p = os.path.join(LIFEDIR, f'Borromean Flora {name}.asset')
        g = guid(f'Borromean Flora {name}.asset')
        cfg_guids.append(g)
        files[p] = build_config(tbl, name, value, prefab_guid, existing_heart_scale(p))
        files[p + '.meta'] = asset_meta(g)
    files[MATRIX] = register_in_matrix(prefab_guid, cfg_guids)
    gs = dict(flora=flora_guid, table=table_guid, prefab=prefab_guid)
    gs.update({f'config:{n}': g for (n, _), g in zip(ELEMENTS, cfg_guids)})
    return tbl, files, gs


def check_guid_uniqueness(guids, files):
    """Exactly one .meta OWNS a guid.  Count only OWNERSHIP lines; every other hit is a
    reference and is evidence the wiring worked."""
    bad = []
    for label, g in guids.items():
        owners = []
        for dirpath, _, names in os.walk(os.path.join(ROOT, 'Assets')):
            for n in names:
                if not n.endswith('.meta'): continue
                p = os.path.join(dirpath, n)
                if p in files: continue          # ours, about to be written
                try:
                    if re.search(rf'^guid: {g}$', open(p, errors='replace').read(), re.M):
                        owners.append(os.path.relpath(p, ROOT))
                except OSError:
                    pass
        if owners: bad.append((label, g, owners))
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--write', action='store_true')
    a = ap.parse_args()

    tbl, files, guids = plan()
    print(f'BorromeanFlora: {tbl["sites"]} sites in orbits of {tbl["orbit"]}, '
          f'plant radius {tbl["radius"]:.1f}')
    for e in ('Time', 'Mass', 'Space', 'Charge'):
        L = tbl['leaves'][e]
        vol = L[0] * L[1] * L[2]
        note = {'Time': 'the anchor', 'Mass': 'more volume', 'Space': 'more aspect',
                'Charge': 'fitted to its own shielded octahedra'}[e]
        print(f'  {e:<6} {v3(L)}  aspect {L[0]/L[1]:4.2f}  volume/prism {vol:6.2f}  '
              f'plant {vol*tbl["sites"]:8,.0f}   ({note})')
    print(f'  population: floor {SEED_FLOOR}, cap {CAP} per element '
          f'({CAP*4} heart colliders across the four), quota '
          f'{max(1,int(tbl["sites"]*QUOTA_FRAC+0.5))}')

    dup = check_guid_uniqueness(guids, files)
    if dup:
        for label, g, owners in dup:
            print(f'  FAIL: guid {g} ({label}) already owned by {owners}')
        return 1

    # Field parity: every key the FLORA COMPONENT's own document writes must be a field
    # something in its hierarchy declares.  Scoped to that one document by its m_Script guid
    # - a prefab holds many components (the nested crystal, the prism, the transforms), and
    # scraping keys across the whole file asks BorromeanFlora to account for theirs.
    flora_fields = serialized_fields(FLORACS)
    doc = re.search(rf'^--- !u!114 .*?\n(.*?guid: {guids["flora"]}.*?)(?=^--- !u!|\Z)',
                    files[PREFAB], re.M | re.S)
    if not doc:
        print('  FAIL: the flora component document is not in the authored prefab')
        return 1
    prefab_keys = set(re.findall(r'^  (\w+):', doc.group(1), re.M))
    unknown = {k for k in prefab_keys
               if not k.startswith('m_') and k != 'serializedVersion' and k not in flora_fields}
    # the donor's inherited Flora/LifeForm fields are legitimate - check them too
    inherited = (serialized_fields(A('_Scripts/Controller/Environment/FloraAndFauna/Flora.cs'))
                 | serialized_fields(A('_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs')))
    unknown -= inherited
    if unknown:
        print(f'  FAIL: prefab writes keys no BorromeanFlora field declares: {sorted(unknown)}')
        return 1
    print(f'  field parity OK ({len(prefab_keys)} prefab keys resolve)')

    changed = [p for p, t in files.items()
               if not os.path.exists(p) or open(p).read() != t]
    if a.write:
        for p, t in files.items():
            os.makedirs(os.path.dirname(p), exist_ok=True)
            open(p, 'w').write(t)
        print(f'\nwrote {len(files)} files ({len(changed)} changed)')
        return 0
    if a.check:
        if changed:
            print('\nFAIL: drifted from what this tool would author:')
            for p in sorted(changed): print('  ' + os.path.relpath(p, ROOT))
            return 1
        print('\nOK: all assets match')
        return 0
    print(f'\n(dry run - {len(changed)} of {len(files)} files would change; pass --write)')
    return 0


if __name__ == '__main__':
    sys.exit(main())

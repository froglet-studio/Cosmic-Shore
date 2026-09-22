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
by construction.  EVERY ELEMENT HAS ITS OWN TABLE - its own tessellation, its own prism
budget and its own fitted plate, because no prism may interpenetrate another and a plate
that clears is bounded by how far apart its neighbours are - so the per-element configs
differ in more than their leaf.  At runtime `BorromeanFlora` takes both the leaf and the
budget from its own element's table rather than from the config, which is what makes that
a GUARANTEE rather than an authored number; the configs carry the same values so the assets
are not silent about the plants they describe.

GUIDs are `md5("cosmicshore/borromean/<stable name>")`, so a re-run is idempotent and
`--check` compares CONTENT rather than identity.  Every one is asserted to be owned by
exactly one `.meta` repo-wide before anything is written.

POPULATIONS ARE AUTHORED HERE, not by `author_flora_populations.py` - EVERYWHERE, including
in the cells that adopt the species.  That script's model is `cap = old_single_plant_budget
/ patch`, which has no input to work from on a species whose budget is a MEASURED TABLE per
element, so it lists this family under `OWNED_ELSEWHERE` and prints the handoff.  Note its
match for this family is a SUBSTRING and not a prefix: a per-cell config is named for the
CELL first ("Rampage Borromean Flora Mass Config Data"), and a prefix rule would have
handed every adopting cell's copy silently back to it.

DEPLOYMENT, stated plainly because the claim rots: as of this commit the species grows in
RAMPAGE (as mass to destroy), WRECKING BALL, WILDLIFE BLITZ cells 1 and 2 and the
freestyle ARBORETUM (as a SPECIMEN - one of each element, the only cell that grows it to
be looked at rather than flown through), as well as being reachable through the freestyle
Lifeform Matrix toy.  `DEPLOYMENTS` below is this
tool's half of that; the other half is each cell's own generator, which owns the
`SupportedFloras` list and the volume ladder the adoption moves.  WRECKING BALL is
deliberately NOT in `DEPLOYMENTS` - it FORKS Rampage's configs, so it has one owner for its
forest.  Re-prove the claim by grepping these configs' GUIDs across `_SO_Assets` before
inheriting it.
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

# ---- DEPLOYMENT: the cells that grow this species, and what each of them asks for -----
#
# A cell adopts the species as FOUR configs, one per element, never as one rolled config.
# That is not a preference: a rolled config carries ONE `Variant` block, and the four
# elements differ in their prism BUDGET (180..360), their plate and their HEART
# (2.051..3.379) - so a rolled config would have to author one heart size for four plants
# whose spans run 108 to 222, and `author_lifeform_heart_sizes.py` would then be sizing an
# average rather than a lifeform.  Four configs cost four assets and say the truth.
#
# What a CELL authors, and all it authors, is HOW MANY and WHERE: the seed floor and the
# live cap (per element), and the planting band.  Everything else - the budget, the plate,
# the quota, the spread - is the species' and comes out of the measurement.
#
# `cap` is a CRYSTAL count: one always-on heart collider per live plant, culled by no phase
# (Docs/ECOSYSTEM.md 32.7), and it is multiplied by that cell's `FloraPopulationScale`.  It
# is THE dial if an adopting cell reads busy.
#
#   folder                                    prefix                seed cap  band
DEPLOYMENTS = [
    ('_SO_Assets/Cell Configs/Rampage Cell',      'Rampage',          2,  3, (0.25, 0.85)),
    # WRECKING BALL is deliberately absent: its generator FORKS Rampage's species configs
    # and re-maps their planting bands into its 720u court (author_wrecking_ball_assets.py
    # step 4), so authoring a second set here would give that cell two owners for one
    # forest. It takes the Borromean four the same way it takes the other five.
    ('_SO_Assets/Cell Configs/WildLife Blitz Cells/Cell 1', 'Wildlife Cell 1', 1, 2, (0.25, 0.85)),
    ('_SO_Assets/Cell Configs/WildLife Blitz Cells/Cell 2', 'Wildlife Cell 2', 1, 2, (0.25, 0.85)),
    # THE ARBORETUM is the one deployment that is not a forest: cap 1 means ONE specimen of
    # each element, which is that cell's whole proposition (Docs/ECOSYSTEM.md 57). Its band
    # is wider and starts further out than the others' because it has to clear the ~392u
    # nucleus - Flora.ResolvePlantRadius collapses a band authored inside a control zone to
    # one degenerate shell, and sixteen Mandelbulb specimens plus these four on one sphere
    # is not an arboretum. That cell's own generator owns its SupportedFloras list and its
    # volume ladder, and READS these four back rather than re-authoring them.
    ('_SO_Assets/Cell Configs/Arboretum Cell',    'Arboretum',        1,  1, (0.42, 0.92)),
]


def guid(name):
    return hashlib.md5(f'cosmicshore/borromean/{name}'.encode()).hexdigest()


def read_table():
    """Read the numbers the measurement emitted - never retype them.

    EVERY ELEMENT HAS ITS OWN TABLE, because a plate that does not interpenetrate its
    neighbours is bounded by how far apart they are, so an element whose body is bigger
    takes a coarser tessellation rather than a shrunken plate.  The prism BUDGET, the plant
    RADIUS and the plate are therefore all per element here."""
    if not os.path.exists(TABLE):
        sys.exit('BorromeanSurfaceData.cs is missing - run '
                 'Tools/Build/measure_borromean_minimal_surface.py --write first')
    t = open(TABLE).read()
    def const(name, kind='int'):
        m = re.search(rf'public const {"int" if kind=="int" else "float"} {name} = ([-\d.]+)f?;', t)
        if not m: sys.exit(f'{name} not found in BorromeanSurfaceData.cs')
        return int(m.group(1)) if kind == 'int' else float(m.group(1))
    def element(e):
        # Matched WITH its own array names, so a table wired to another element's points -
        # the one mistake four near-identical blocks invite - fails here rather than
        # authoring a config against the wrong plant.
        m = re.search(rf'SurfaceTable {e} = new SurfaceTable\(\s*'
                      rf'{e}Positions, {e}Rotations, {e}Parents,\s*'
                      rf'new Vector3\(([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\), '
                      rf'([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f,\s*([-\d.]+)f\);', t)
        if not m: sys.exit(f'the {e} SurfaceTable is missing from BorromeanSurfaceData.cs, '
                           f'or is not wired to its own {e}Positions/{e}Rotations/{e}Parents')
        g = [float(x) for x in m.groups()]
        b = re.search(rf'{e}Positions =\s*\{{\n(.*?)\n\s*\}};', t, re.S)
        if not b: sys.exit(f'{e}Positions not found in BorromeanSurfaceData.cs')
        return dict(leaf=tuple(g[:3]), radius=g[3], seat=g[6],
                    sites=len(re.findall(r'new\(', b.group(1))))
    els = {e: element(e) for e, _ in ELEMENTS}
    return dict(orbit=const('OrbitSize'), max_sites=const('MaxSiteCount'),
                anchor=els['Time'], elements=els)


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
    # The prefab carries the ANCHOR's numbers: it is the shape a plant with no element
    # yet would grow, and it is what the inspector shows.  At runtime BorromeanFlora takes
    # both from its own element's table (the size that clears is a function of the table and
    # no config field can know it), so these are the same numbers rather than a second
    # source of them.
    tail = (f'  leafSize: {v3(tbl["anchor"]["leaf"])}\n'
            '  growPeriod: 0.8\n'
            '  PlantPeriod: 120\n'
            '  stunDuration: 2\n'
            '  plantRadiusCellFraction: 0.5\n'
            '  plantRadiusCellFractionMin: 0.25\n'
            f'  maxTotalSpawnedObjects: {tbl["max_sites"]}\n'
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


def build_config(tbl, element, value, prefab_guid, heart='0',
                 name=None, seed=SEED_FLOOR, cap=CAP, band=None):
    # THE ELEMENT IS THE PLATE.  A lifeform is its species and its element and nothing
    # else (Docs/ECOSYSTEM.md 40), so every element states its own leaf here - Time the
    # measured optimum, Mass more volume, Space more aspect, Charge fitted to its own
    # shielded octahedra.  All four come out of the measurement; none is retyped.
    # THE ELEMENT IS THE PLATE - and, since no prism may interpenetrate another, the
    # TESSELLATION as well: Time the anchor, Mass more volume on a coarser tiling, Space
    # more aspect, Charge fitted to its own shielded octahedra on the coarsest of the four.
    # Every number here comes out of that element's own table; none is retyped.
    el = tbl['elements'][element]
    name = name or f'Borromean Flora {element}'
    leaf, budget = el['leaf'], el['sites']
    quota = max(1, int(budget * QUOTA_FRAC + 0.5))
    # A plant is PlantRadius across, so an offspring belongs clear of its parent.
    spread = int(el['radius'] * 1.5 + 0.5)
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
        f'  m_Name: {name}\n'
        '  m_EditorClassIdentifier:\n'
        f'  FloraPrefab: {{fileID: {FLORA_COMPONENT_FILEID}, guid: {prefab_guid}, type: 3}}\n'
        '  SpawnProbability: 1\n'
        f'  InitialSpawnCount: {seed}\n'
        '  OverrideDefaultPlantPeriod: 0\n'
        '  NewPlantPeriod: 9999999\n'
        f'  PopulationSize: {seed}\n'
        f'  MaxLivePopulation: {cap}\n'
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
        '    PlantRadiusCellFractionMin: -1\n'
        # The planting band is a property of the CELL, so it goes in the cell-level
        # override pair rather than in Variant - the mechanism FloraConfigurationSO
        # documents as "applied AFTER the rolled variant, so it wins over both the palette
        # sibling and the prefab", and the one every other per-cell config in the project
        # uses. Omitted entirely where there is no cell: an absent key keeps the field
        # initializer's -1 sentinel, which is how the four species-level configs say
        # "wherever this cell plants things".
        + (f'  PlantRadiusCellFractionMaxOverride: {band[1]}\n'
           f'  PlantRadiusCellFractionMinOverride: {band[0]}\n' if band else ''))


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

    # THE CELLS THAT GROW IT.  Four configs per cell, one per element - see DEPLOYMENTS for
    # why never one rolled config.  They are authored HERE and not by
    # `author_flora_populations.py` for the same reason the species-level four are: this
    # species' prism budget is a MEASURED TABLE per element, and that script's model
    # (cap = old_single_plant_budget / patch) has no input to work from.
    deploy = {}
    for folder, prefix, seed, cap, band in DEPLOYMENTS:
        for name, value in ELEMENTS:
            asset = f'{prefix} Borromean Flora {name} Config Data'
            path = A(folder, asset + '.asset')
            g = guid(asset + '.asset')
            files[path] = build_config(tbl, name, value, prefab_guid,
                                       existing_heart_scale(path), name=asset,
                                       seed=seed, cap=cap, band=band)
            files[path + '.meta'] = asset_meta(g)
            gs[f'config:{asset}'] = g
            deploy[(prefix, name)] = g
    return tbl, files, gs, deploy


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

    tbl, files, guids, deploy = plan()
    print(f'BorromeanFlora: one tessellation per ELEMENT, orbits of {tbl["orbit"]}, '
          f'largest table {tbl["max_sites"]} sites')
    for e in ('Time', 'Mass', 'Space', 'Charge'):
        el = tbl['elements'][e]
        L = el['leaf']
        vol = L[0] * L[1] * L[2]
        note = {'Time': 'the anchor - the finest membrane',
                'Mass': 'more volume, on a coarser tiling',
                'Space': 'more aspect at the anchor\'s volume',
                'Charge': 'fitted to its own shielded octahedra'}[e]
        print(f'  {e:<6} {el["sites"]:3d} plates  {v3(L)}  aspect {L[0]/L[1]:4.2f}  '
              f'volume/prism {vol:6.2f}  plant {vol*el["sites"]:8,.0f}   ({note})')
    print(f'  population: floor {SEED_FLOOR}, cap {CAP} per element '
          f'({CAP*4} heart colliders across the four), quota '
          + ' / '.join(f'{e} {max(1,int(tbl["elements"][e]["sites"]*QUOTA_FRAC+0.5))}'
                       for e in ('Time', 'Mass', 'Space', 'Charge')))
    sites = sum(tbl['elements'][e]['sites'] for e, _ in ELEMENTS)
    vol = sum(tbl['elements'][e]['sites'] * tbl['elements'][e]['leaf'][0]
              * tbl['elements'][e]['leaf'][1] * tbl['elements'][e]['leaf'][2]
              for e, _ in ELEMENTS)
    print('  DEPLOYED IN (four configs per cell, one per element - seeded / capped per element):')
    for folder, prefix, seed, cap, band in DEPLOYMENTS:
        print(f'    {prefix:<16} seed {seed}/el = {4*seed:2d} plants  cap {cap}/el = '
              f'{4*cap:2d} heart colliders  band {band[0]:.2f}..{band[1]:.2f}  '
              f'seeded {seed*sites:5,d} prisms / {seed*vol:9,.0f} volume, '
              f'at cap {cap*sites:5,d} / {cap*vol:9,.0f}')
    print(f'    (per FOUR-element set: {sites:,} prisms and {vol:,.0f} volume - and the four '
          f'differ {max(tbl["elements"][e]["sites"]*tbl["elements"][e]["leaf"][0]*tbl["elements"][e]["leaf"][1]*tbl["elements"][e]["leaf"][2] for e,_ in ELEMENTS) / min(tbl["elements"][e]["sites"]*tbl["elements"][e]["leaf"][0]*tbl["elements"][e]["leaf"][1]*tbl["elements"][e]["leaf"][2] for e,_ in ELEMENTS):.1f}x,')
    print('     so a cell that seeds all four is growing four VERY different plants)')

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

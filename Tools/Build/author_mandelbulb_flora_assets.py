#!/usr/bin/env python3
"""
Author every ASSET the Mandelbulb flora species needs, from the measured model.

    python3 Tools/Build/author_mandelbulb_flora_assets.py           # write
    python3 Tools/Build/author_mandelbulb_flora_assets.py --check   # CI: exit 1 on drift

WHAT IT OWNS (and therefore what must not be hand-edited)
--------------------------------------------------------
  * the two script .meta files, so the prefab's m_Script guids are stable on every machine
    (a script committed without its .meta gets a fresh guid per clone, and every prefab
    reference to it dangles on the next one)
  * Assets/_Prefabs/FloraAndFauna/MandelbulbFlora.prefab - the species prefab, cloned from a
    shipped flora so its CRYSTAL child (the heart, a nested prefab instance with its impactor
    and collider overrides) is structurally identical to one Unity itself authored
  * Assets/_SO_Assets/Lifeforms/Mandelbulb Flora {Charge,Mass,Space,Time}.asset - one config
    per element, whose per-plant budget is the element's OWN measured site count
  * the Mandelbulb row in Toy_LifeformMatrix.asset, which is how the species is reachable

Population numbers live here rather than in author_flora_populations.py, and that script is
told so by name (its OWNED_ELSEWHERE table) rather than silently skipping these files - a
hand-off you can read beats an exclusion you cannot.

HEART SIZE is deliberately NOT authored here: FloraVariantTuning.HeartWorldScale 0 means "use
ElementalCrystalSet.defaultHeartWorldScale", and the measured band is owned by
Tools/Build/author_lifeform_heart_sizes.py. Two tools must never own one field.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mandelbulb_flora_model as M  # noqa: E402
import measure_mandelbulb_flora as measure  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
PREFABS = ROOT / "Assets/_Prefabs/FloraAndFauna"
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
SCRIPTS = ROOT / "Assets/_Scripts/Controller/Environment/FloraAndFauna"
DONOR = PREFABS / "RosetteFlora.prefab"
TOY = ROOT / "Assets/_SO_Assets/Toys/Toy_LifeformMatrix.asset"
POPULATIONS = ROOT / "Tools/Build/author_flora_populations.py"

# Stable guids. Generated once and then FIXED - regenerating them dangles every reference.
GUID_FLORA_CS = "3d1f6c0ab8a74f5e9c2d47e1b60f8a31"
GUID_SURFACE_CS = "ae15a77fd141196624e55b833545b01c"
GUID_TABLES_CS = "4278eea1ad9158a0be0113fde4146748"
ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}

# TWO SPECIES on ONE growth family (Docs/ECOSYSTEM.md §52). They share the component, the
# surface bake and every tool; they differ in their curve rules and in one dial each - the
# foliage TWISTS, the bloom does not - so they are two PREFABS rather than two classes, the
# way the eight Hesperides phyllotactics are eight species on one class.
#
# Each needs its OWN component fileID: a wrong one in a FloraPrefab reference resolves to no
# component at all and the config grows nothing, silently (CLAUDE.md).
SPECIES_ASSETS = {
    "FractalFoliage": dict(
        prefab="MandelbulbFlora",
        asset_prefix="Mandelbulb Flora",
        toy_row="Mandelbulb",
        prefab_guid="c5a90e73b1284d6fa73e8c14d9026bf7",
        component_fileid="4820175933061942688",
        configs={
            "Charge": "6e2b8d41f09c4a17b3d5e08c71a4f962",
            "Mass":   "1a7c40d9e5b3486f92c1d70eb38a5c4d",
            "Space":  "84f13b6ac2de49518a0b7e3d5c96124f",
            "Time":   "d09e5a24c73b41f6b81c3ae07d52964b",
        },
    ),
    "CoralBloom": dict(
        prefab="CoralBloomFlora",
        asset_prefix="Coral Bloom Flora",
        toy_row="Coral Bloom",
        prefab_guid="357114a2002c2d4d4f59ec404f7f4bf6",
        component_fileid="498631696834916965",
        configs={
            "Charge": "13e434ec9c39beedb3f07d87eaf02e94",
            "Mass":   "c24f4b1142a4522b8a5d9ae5bd6de8b6",
            "Space":  "0d625e5d75948f82ecb7df85c9acd266",
            "Time":   "bf07113394fa63b1c9ede5b314a14f1d",
        },
    ),
    # The Watershed: the Morse-Smale skeleton of the height field - separatrices traced out of
    # every saddle along its Hessian eigen-directions to the peaks and pits, then THE FALL into
    # the heart. Same component, same bake; a third GrowthRules row (Docs/ECOSYSTEM.md section 47).
    "Watershed": dict(
        prefab="WatershedFlora",
        asset_prefix="Watershed Flora",
        toy_row="Watershed",
        prefab_guid="02650ac30b7148ad97d9a65519d35d96",
        component_fileid="8542379790884842332",
        configs={
            "Charge": "bd9ef8307c7d4a88862834d83fda588b",
            "Mass":   "3a6d142ae5ce4455831e5b0573a8e360",
            "Space":  "2b7b4bb40cb84e088f9595974f8d6be3",
            "Time":   "4b27f684cf9b4db6bcc4b78268e185e2",
        },
    ),
    # Apollonia: the spherical Apollonian gasket of rings - the self-similar species (§54).
    "Apollonia": dict(
        prefab="ApolloniaFlora",
        asset_prefix="Apollonia Flora",
        toy_row="Apollonia",
        prefab_guid="e51788f1f8c14517ab53cd376b3237e1",
        component_fileid="6193847520391746285",
        configs={
            "Charge": "4c7fb1015cf74d4da23d86614fd033bf",
            "Mass":   "f030f2cceed64578930cd5c47c405595",
            "Space":  "52d54f8478ac41449607e8b2f81e602f",
            "Time":   "e4ae88c4d7b4450a8c7b649336780938",
        },
    ),
}

FLORA_CONFIG_SCRIPT_GUID = "a32a297a7606432885f4d3e1f83bea9a"   # FloraConfigurationSO
PHYLLOTACTIC_SCRIPT_GUID = "bcdd7421354c7d0d46befa924daa94b6"   # the donor's component
DONOR_COMPONENT_FILEID = "7514956980722975813"
# This family's own component fileID, so a FloraPrefab reference can never be confused with the
# phyllotactic/branching families' shared one (CLAUDE.md: a wrong fileID resolves to no component
# at all and the config grows nothing, silently).
SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

PREFAB_META = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def plan(species):
    """Everything the assets say, measured rather than typed twice."""
    out = {"species": species, "elements": {}}
    for elem in ELEMENT_ID:
        r = measure.element_report(elem, species=species)
        out["elements"][elem] = {
            "prisms": r["prisms"],
            "curves": r["curves"],
            "volume": r["volume"],
            "dims": r["dims"],
        }
    return out


RULE_FIELD_NAMES = (
    ("Field", "%d"), ("SwirlDegrees", "%g"), ("FieldMix", "%g"), ("Momentum", "%g"),
    ("StepSize", "%g"), ("MaxSteps", "%d"), ("LanesPerSeed", "%d"), ("LaneGap", "%g"),
    ("HopSeek", "%g"), ("HopJitter", "%g"), ("SeedCount", "%d"), ("SeedSpreadDegrees", "%g"),
    ("MaxTurnDegrees", "%g"), ("RadiusMin", "%g"), ("RadiusMax", "%g"), ("MinRun", "%d"),
    ("LengthFactor", "%g"), ("GirthTaper", "%g"), ("TwistDegreesPerStep", "%g"),
    ("DiveCount", "%d"), ("DiveStepFraction", "%g"), ("DiveAngleDegrees", "%g"),
    ("DiveStopRadius", "%g"), ("DiveMaxSteps", "%d"), ("DiveSwirlDegrees", "%g"),
    ("DiveStrideCeiling", "%g"), ("DiveGirthFloor", "%g"), ("DiveAxisAlign", "%g"),
    ("DiveDescent", "%g"),
    ("SkeletonSeeds", "%d"), ("WalkStep", "%g"), ("MinPersistence", "%g"),
    ("GirthReference", "%g"),
    ("GasketLevels", "%d"), ("DiscSeeds", "%d"), ("DiscPad", "%g"), ("DiscMinRadius", "%g"),
    ("RingShrink", "%g"), ("RingFlatten", "%g"), ("RingGirthExponent", "%g"),
    ("RingSamples", "%d"), ("GasketOctave", "%g"), ("DiscRelaxRate", "%g"),
    ("RingGirthFloor", "%g"),
)


def rules_block(elem, indent, species):
    """MandelbulbSurface.GrowthRules as Unity serialises a nested [Serializable] struct - one
    line per field, in DECLARATION order, which is also Rules.FIELDS' order."""
    r = M.rules_for(elem, species)
    values = r.as_list()
    if len(values) != len(RULE_FIELD_NAMES):
        sys.exit("author_mandelbulb_flora_assets: RULE_FIELD_NAMES is out of step with Rules.FIELDS")
    pad = " " * indent
    return "\n".join(pad + name + ": " + (fmt % v) for (name, fmt), v in zip(RULE_FIELD_NAMES, values))


def flora_component_block(p):
    species = p["species"]
    spec = M.SPECIES[species]
    forms = []
    for elem in ELEMENT_ID:
        cx, cy = M.cross_section_for(elem, species)
        forms.append("  - Element: %d\n" % ELEMENT_ID[elem]
                     + "    CrossSection: {x: %g, y: %g}\n" % (cx, cy)
                     + "    Rules:\n" + rules_block(elem, 6, species))
    # The prefab's own seed prism is the ONLY prism that reads leafSize - every prism the
    # plant lays carries a size measured from its own curve (MandelbulbFlora.AddHealthBlock).
    scx, scy, scz, _ = M.elemental_prism(species, "Space")
    seed = (scx * M.SHELL_RADIUS, scy * M.SHELL_RADIUS, scz * M.SHELL_RADIUS)
    return (
        "  gameData: {fileID: 11400000, guid: b35f33752bb10a44cb5033b5670f50aa, type: 2}\n"
        "  cellData: {fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}\n"
        "  healthPrism: {fileID: 6313579230210663873, guid: 1488a2ac58b2b4c43b14f84206bd9195, type: 3}\n"
        "  spindle: {fileID: 5157459880619768690, guid: f7ec1bbfe690a184b935434a6e0dcb7a, type: 3}\n"
        "  healthBlocksForMaturity: 1\n"
        "  minHealthBlocks: 0\n"
        "  shieldPeriod: 0\n"
        "  autoInitialize: 1\n"
        "  domain: 0\n"
        "  onLifeFormCreated: {fileID: 11400000, guid: 0ec64678e3c91034faed17b6e66ded9d, type: 2}\n"
        "  onLifeFormDestroyed: {fileID: 11400000, guid: af79f31492a261e49826374c21ee2234, type: 2}\n"
        "  leafSize: {x: %.4g, y: %.4g, z: %.4g}\n" % seed +
        "  growPeriod: 0.5\n"
        "  PlantPeriod: 15\n"
        "  stunDuration: 1\n"
        "  plantRadiusCellFraction: 0.6\n"
        "  plantRadiusCellFractionMin: 0.25\n"
        "  formByElement:\n" + "\n".join(forms) + "\n"
        + "  shellRadius: %g\n" % M.SHELL_RADIUS
        + "  fieldWidth: %d\n" % M.FIELD_WIDTH
        + "  weightSpread: %g\n" % spec["weight_spread"]
        + "  weightSteps: %d\n" % M.WEIGHT_STEPS
        + "  maxTotalSpawnedObjects: %d\n" % M.PRISM_BUDGET
        + "  growthsPerTick: 8\n"
        + "  maxSpawnsPerFrame: 3\n"
        + "  plantRadius: 150\n")


def build_prefab(p):
    """Clone the donor flora, keeping its crystal child, and swap in this species' component."""
    assets = SPECIES_ASSETS[p["species"]]
    component_fileid = assets["component_fileid"]
    src = DONOR.read_text()
    start = src.index(f"--- !u!114 &{DONOR_COMPONENT_FILEID}")
    end = src.index("--- ", start + 10)
    head = (f"--- !u!114 &{component_fileid}\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 6774738432424273872}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {GUID_FLORA_CS}, type: 3}}\n"
            "  m_Name:\n"
            "  m_EditorClassIdentifier:\n")
    out = src[:start] + head + flora_component_block(p) + src[end:]
    out = out.replace(f"- component: {{fileID: {DONOR_COMPONENT_FILEID}}}",
                      f"- component: {{fileID: {component_fileid}}}")
    out = out.replace("m_Name: RosetteFlora", "m_Name: " + assets["prefab"])
    if PHYLLOTACTIC_SCRIPT_GUID in out:
        sys.exit("author_mandelbulb_flora_assets: the donor's component survived the swap")
    return out


def config_text(p, elem):
    assets = SPECIES_ASSETS[p["species"]]
    e = p["elements"][elem]
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {FLORA_CONFIG_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {assets['asset_prefix']} {elem}",
        "  m_EditorClassIdentifier:",
        f"  FloraPrefab: {{fileID: {assets['component_fileid']}, "
        f"guid: {assets['prefab_guid']}, type: 3}}",
        "  SpawnProbability: 1",
        "  InitialSpawnCount: 1",
        "  OverrideDefaultPlantPeriod: 0",
        "  NewPlantPeriod: 9999999",
        # A plant of this species is ~2,600 prisms and ~110,000 volume, an order of magnitude
        # past any other flora, so the population is deliberately tiny. It is in no SpawnProfile
        # either (opt-in from the Lifeform Matrix toy), so the cost lands only where a player
        # asked for it - but a cap of 3 still has to be affordable in the cell they ask in.
        "  PopulationSize: %d" % M.POPULATION_SIZE,
        "  MaxLivePopulation: %d" % M.MAX_LIVE_POPULATION,
        # One whole form per child: a plant funds a daughter only once it has grown itself.
        f"  GrowthPerOffspring: {p['elements'][elem]['prisms']}",
        "  OffspringPerBirth: 1",
        "  ReproductionCooldownSeconds: 5",
        "  MaturityFraction: 0.5",
        "  OffspringSpread: 120",
        f"  Element: {ELEMENT_ID[elem]}",
        "  Variant:",
        "    Enabled: 1",
        "    GrowPeriod: 0.5",
        # The budget IS the form: this element's own measured site count, so a mature plant is a
        # complete bulb and grazing frees exactly the budget regrowth needs.
        "    MaxTotalSpawnedObjects: %d" % M.PRISM_BUDGET,
        "    PlantRadiusCellFraction: 0.6",
        "    PlantRadiusCellFractionMin: 0.25",
    ]
    if elem == "Charge":
        lines.append("    ShieldPeriod: 1")

    # HeartWorldScale is owned by Tools/Build/author_lifeform_heart_sizes.py, which solves the
    # whole fleet's band at once. Carry whatever it wrote straight through: re-authoring it here
    # would make two tools own one field (the loser silently wins on whoever ran last), and
    # DROPPING it would be worse - the asset would quietly fall back to the set default and this
    # species' heart would stop tracking its body size, which is the exact non-monotone defect
    # that tool exists to fail the build on.
    # ... from THIS species' own asset - reading the first species' file here carried the
    # Mandelbulb's heart onto every sibling and silently undid the band tool's sizing.
    existing = LIFEFORMS / f"{assets['asset_prefix']} {elem}.asset"
    if existing.exists():
        m = re.search(r"^\s+HeartWorldScale: (\S+)$", existing.read_text(), re.M)
        if m:
            lines.insert(lines.index("    Enabled: 1") + 1, f"    HeartWorldScale: {m.group(1)}")
    return "\n".join(lines) + "\n"


def toy_row(p):
    assets = SPECIES_ASSETS[p["species"]]
    guids = [assets["configs"][e] for e in ("Charge", "Mass", "Space", "Time")]
    body = "\n".join(f"    - {{fileID: 11400000, guid: {g}, type: 2}}" for g in guids)
    return f"  - Name: {assets['toy_row']}\n    ElementConfigs:\n" + body + "\n"


def upsert_toy_row(toy, p):
    """Replace this species' row in the Lifeform Matrix toy, or append it once."""
    row = toy_row(p)
    header = row.split("\n")[0] + "\n"
    if header in toy:
        start = toy.index(header)
        rest = toy[start + len(header):]
        nxt = re.search(r"^  - Name: |^  \w", rest, re.M)
        end = start + len(header) + (nxt.start() if nxt else len(rest))
        return toy[:start] + row + toy[end:]
    m = re.search(r"^  floraSpecies:\n", toy, re.M)
    if not m:
        sys.exit("author_mandelbulb_flora_assets: floraSpecies not found in the toy asset")
    nxt = re.search(r"^  (?!- |  )\S", toy[m.end():], re.M)
    insert = m.end() + (nxt.start() if nxt else 0)
    return toy[:insert] + row + toy[insert:]


def write(path, text, changed, check):
    existing = path.read_text() if path.exists() else None
    if existing == text:
        return
    changed.append(path.relative_to(ROOT))
    if not check:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    if not DONOR.exists():
        sys.exit(f"author_mandelbulb_flora_assets: donor prefab missing: {DONOR}")

    changed = []
    write(SCRIPTS / "MandelbulbFlora.cs.meta", SCRIPT_META.format(guid=GUID_FLORA_CS), changed, args.check)
    write(SCRIPTS / "MandelbulbSurface.cs.meta", SCRIPT_META.format(guid=GUID_SURFACE_CS), changed, args.check)
    write(SCRIPTS / "MandelbulbSurfaceTables.cs.meta", SCRIPT_META.format(guid=GUID_TABLES_CS), changed, args.check)

    toy = TOY.read_text()
    plans = {}
    for species, assets in SPECIES_ASSETS.items():
        p = plan(species)
        plans[species] = p
        write(PREFABS / f"{assets['prefab']}.prefab", build_prefab(p), changed, args.check)
        write(PREFABS / f"{assets['prefab']}.prefab.meta",
              PREFAB_META.format(guid=assets["prefab_guid"]), changed, args.check)
        for elem in ("Charge", "Mass", "Space", "Time"):
            name = f"{assets['asset_prefix']} {elem}"
            write(LIFEFORMS / f"{name}.asset", config_text(p, elem), changed, args.check)
            write(LIFEFORMS / f"{name}.asset.meta",
                  ASSET_META.format(guid=assets["configs"][elem]), changed, args.check)
        # Both species are reachable ONLY from the Lifeform Matrix toy. Idempotent: the row is
        # replaced, never appended twice.
        toy = upsert_toy_row(toy, p)
    write(TOY, toy, changed, args.check)

    # Hand the population numbers over BY NAME rather than excluding these configs silently.
    pop = POPULATIONS.read_text()
    for assets in SPECIES_ASSETS.values():
        marker = f'    "{assets["asset_prefix"]} ": "Tools/Build/author_mandelbulb_flora_assets.py",\n'
        if marker not in pop:
            anchor = '    "Lattice ": "Tools/Build/author_lattice_cell.py",\n'
            if anchor not in pop:
                sys.exit("author_mandelbulb_flora_assets: OWNED_ELSEWHERE anchor not found")
            pop = pop.replace(anchor, anchor + marker)
    write(POPULATIONS, pop, changed, args.check)

    for species, assets in SPECIES_ASSETS.items():
        p = plans[species]
        spec = M.SPECIES[species]
        concept = spec.get("concept") or (f"helicoidal twist {spec['twist']:g} deg/step" if spec["twist"]
                                          else "smooth crossing curves, no twist")
        print(f"{spec['display']} ({species}) - {concept}")
        print(f"  prefab   {PREFABS.relative_to(ROOT)}/{assets['prefab']}.prefab  "
              f"(component fileID {assets['component_fileid']})")
        for elem in ("Charge", "Mass", "Space", "Time"):
            e = p["elements"][elem]
            print(f"  config   {assets['asset_prefix']} {elem:<7} {e['prisms']:>5} prisms over "
                  f"{e['curves']:>4} curves  dims {e['dims'][0]:.2f}..{e['dims'][1]:.2f}  "
                  f"volume {e['volume']:>9,.0f}")
        print(f"  toy row  '{assets['toy_row']}' in Toy_LifeformMatrix; in NO SpawnProfile - opt-in.\n")

    if changed:
        if args.check:
            print("\nFAIL - these assets differ from what the model authors:")
            for c in changed:
                print(f"  - {c}")
            return 1
        print("\nwrote:")
        for c in changed:
            print(f"  - {c}")
    else:
        print("\nOK - every asset matches the model.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

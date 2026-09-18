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
GUID_PREFAB = "c5a90e73b1284d6fa73e8c14d9026bf7"
GUID_CONFIG = {
    "Charge": "6e2b8d41f09c4a17b3d5e08c71a4f962",
    "Mass":   "1a7c40d9e5b3486f92c1d70eb38a5c4d",
    "Space":  "84f13b6ac2de49518a0b7e3d5c96124f",
    "Time":   "d09e5a24c73b41f6b81c3ae07d52964b",
}
ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}

FLORA_CONFIG_SCRIPT_GUID = "a32a297a7606432885f4d3e1f83bea9a"   # FloraConfigurationSO
PHYLLOTACTIC_SCRIPT_GUID = "bcdd7421354c7d0d46befa924daa94b6"   # the donor's component
DONOR_COMPONENT_FILEID = "7514956980722975813"
# This family's own component fileID, so a FloraPrefab reference can never be confused with the
# phyllotactic/branching families' shared one (CLAUDE.md: a wrong fileID resolves to no component
# at all and the config grows nothing, silently).
COMPONENT_FILEID = "4820175933061942688"

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


def plan():
    """Everything the assets say, measured rather than typed twice."""
    out = {"elements": {}}
    for elem in ELEMENT_ID:
        r = measure.element_report(elem)
        out["elements"][elem] = {
            "prisms": r["prisms"],
            "curves": r["curves"],
            "volume": r["volume"],
            "dims": r["dims"],
        }
    return out


def rules_block(elem, indent):
    """MandelbulbSurface.GrowthRules as Unity serialises a nested [Serializable] struct."""
    r = M.rules_for(elem)
    pad = " " * indent
    return "\n".join([
        pad + "Field: %d" % r.field,
        pad + "SwirlDegrees: %g" % r.swirl,
        pad + "FieldMix: %g" % r.field_mix,
        pad + "Momentum: %g" % r.momentum,
        pad + "StepSize: %g" % r.step,
        pad + "MaxSteps: %d" % r.max_steps,
        pad + "LanesPerSeed: %d" % r.lanes,
        pad + "LaneGap: %g" % r.lane_gap,
        pad + "HopSeek: %g" % r.hop_seek,
        pad + "HopJitter: %g" % r.hop_jitter,
        pad + "SeedCount: %d" % r.seeds,
        pad + "SeedSpreadDegrees: %g" % r.seed_spread,
        pad + "MaxTurnDegrees: %g" % r.max_turn,
        pad + "RadiusMin: %g" % r.r_min,
        pad + "RadiusMax: %g" % r.r_max,
        pad + "MinRun: %d" % r.min_run,
        pad + "LengthFactor: %g" % r.length_factor,
        pad + "GirthTaper: %g" % r.girth_taper,
    ])


def flora_component_block(p):
    forms = []
    for elem in ELEMENT_ID:
        cx, cy = M.CROSS_SECTION[elem]
        forms.append("  - Element: %d\n" % ELEMENT_ID[elem]
                     + "    CrossSection: {x: %g, y: %g}\n" % (cx, cy)
                     + "    Rules:\n" + rules_block(elem, 6))
    # The prefab's own seed prism is the ONLY prism that reads leafSize - every prism the
    # plant lays carries a size measured from its own curve (MandelbulbFlora.AddHealthBlock).
    sc = M.CROSS_SECTION["Space"]
    seed = (sc[0] * M.SHELL_RADIUS, sc[1] * M.SHELL_RADIUS,
            M.RULES["Space"][4] * M.SHELL_RADIUS)
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
        + "  weightSpread: %g\n" % M.WEIGHT_SPREAD
        + "  weightSteps: %d\n" % M.WEIGHT_STEPS
        + "  maxTotalSpawnedObjects: %d\n" % M.PRISM_BUDGET
        + "  growthsPerTick: 8\n"
        + "  maxSpawnsPerFrame: 3\n"
        + "  plantRadius: 150\n")


def build_prefab(p):
    """Clone the donor flora, keeping its crystal child, and swap in this species' component."""
    src = DONOR.read_text()
    start = src.index(f"--- !u!114 &{DONOR_COMPONENT_FILEID}")
    end = src.index("--- ", start + 10)
    head = (f"--- !u!114 &{COMPONENT_FILEID}\n"
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
                      f"- component: {{fileID: {COMPONENT_FILEID}}}")
    out = out.replace("m_Name: RosetteFlora", "m_Name: MandelbulbFlora")
    if PHYLLOTACTIC_SCRIPT_GUID in out:
        sys.exit("author_mandelbulb_flora_assets: the donor's component survived the swap")
    return out


def config_text(p, elem):
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
        f"  m_Name: Mandelbulb Flora {elem}",
        "  m_EditorClassIdentifier:",
        f"  FloraPrefab: {{fileID: {COMPONENT_FILEID}, guid: {GUID_PREFAB}, type: 3}}",
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
    existing = LIFEFORMS / f"Mandelbulb Flora {elem}.asset"
    if existing.exists():
        m = re.search(r"^\s+HeartWorldScale: (\S+)$", existing.read_text(), re.M)
        if m:
            lines.insert(lines.index("    Enabled: 1") + 1, f"    HeartWorldScale: {m.group(1)}")
    return "\n".join(lines) + "\n"


def toy_row(p):
    guids = [GUID_CONFIG[e] for e in ("Charge", "Mass", "Space", "Time")]
    body = "\n".join(f"    - {{fileID: 11400000, guid: {g}, type: 2}}" for g in guids)
    return "  - Name: Mandelbulb\n    ElementConfigs:\n" + body + "\n"


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

    p = plan()
    changed = []

    write(SCRIPTS / "MandelbulbFlora.cs.meta", SCRIPT_META.format(guid=GUID_FLORA_CS), changed, args.check)
    write(SCRIPTS / "MandelbulbSurface.cs.meta", SCRIPT_META.format(guid=GUID_SURFACE_CS), changed, args.check)
    write(SCRIPTS / "MandelbulbSurfaceTables.cs.meta", SCRIPT_META.format(guid=GUID_TABLES_CS), changed, args.check)
    write(PREFABS / "MandelbulbFlora.prefab", build_prefab(p), changed, args.check)
    write(PREFABS / "MandelbulbFlora.prefab.meta", PREFAB_META.format(guid=GUID_PREFAB), changed, args.check)

    for elem in ("Charge", "Mass", "Space", "Time"):
        write(LIFEFORMS / f"Mandelbulb Flora {elem}.asset", config_text(p, elem), changed, args.check)
        write(LIFEFORMS / f"Mandelbulb Flora {elem}.asset.meta",
              ASSET_META.format(guid=GUID_CONFIG[elem]), changed, args.check)

    # The species is reachable ONLY from the Lifeform Matrix toy - see the module docstring and
    # Docs/ECOSYSTEM.md. Idempotent: the row is replaced, never appended twice.
    toy = TOY.read_text()
    row = toy_row(p)
    if "  - Name: Mandelbulb\n" in toy:
        start = toy.index("  - Name: Mandelbulb\n")
        rest = toy[start + len(row.split("\n")[0]) + 1:]
        nxt = re.search(r"^  - Name: |^  \w", rest, re.M)
        end = start + len(row.split("\n")[0]) + 1 + (nxt.start() if nxt else len(rest))
        toy = toy[:start] + row + toy[end:]
    else:
        m = re.search(r"^  floraSpecies:\n", toy, re.M)
        if not m:
            sys.exit("author_mandelbulb_flora_assets: floraSpecies not found in the toy asset")
        nxt = re.search(r"^  (?!- |  )\S", toy[m.end():], re.M)
        insert = m.end() + (nxt.start() if nxt else 0)
        toy = toy[:insert] + row + toy[insert:]
    write(TOY, toy, changed, args.check)

    # Hand the population numbers over BY NAME rather than excluding these configs silently.
    pop = POPULATIONS.read_text()
    marker = '    "Mandelbulb Flora ": "Tools/Build/author_mandelbulb_flora_assets.py",\n'
    if marker not in pop:
        anchor = '    "Lattice ": "Tools/Build/author_lattice_cell.py",\n'
        if anchor not in pop:
            sys.exit("author_mandelbulb_flora_assets: OWNED_ELSEWHERE anchor not found")
        write(POPULATIONS, pop.replace(anchor, anchor + marker), changed, args.check)

    print("Mandelbulb flora assets")
    print(f"  prefab   {PREFABS.relative_to(ROOT)}/MandelbulbFlora.prefab  (component "
          f"fileID {COMPONENT_FILEID})")
    for elem in ("Charge", "Mass", "Space", "Time"):
        e = p["elements"][elem]
        print(f"  config   Mandelbulb Flora {elem:<7} {e['prisms']:>5} prisms over "
              f"{e['curves']:>4} curves  dims {e['dims'][0]:.2f}..{e['dims'][1]:.2f}  "
              f"volume {e['volume']:>9,.0f}")
    print(f"  reachable from Toy_LifeformMatrix (row 'Mandelbulb'); in NO SpawnProfile - opt-in.")

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

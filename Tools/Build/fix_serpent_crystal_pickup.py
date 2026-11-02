#!/usr/bin/env python3
"""The Serpent collects crystals (QA-P2-SERPENT-SKIMMER, ElementalAbilitySystem/BACKLOG.md item 10).

WHAT WAS WRONG. An elemental crystal is collected by a SKIMMER contact only
(`ElementalCrystalImpactor.AcceptImpactee` accepts a `SkimmerImpactor` and nothing else). The
Serpent's one skimmer, `VacuumSkimmer`, was a shell: its GameObject inactive, its trigger sphere
disabled, and no `SkimmerImpactor`, `ImpactCollider` or `Rigidbody` on it. So a Serpent flew
through every elemental crystal without taking one (play-tested 2026-10-08: "crystals hit the hull
and are not collected"), never levelled an element from the field, and the
`Audit Vessel Skimmers` tool carried it as a standing exception.

WHAT THIS DOES (Shombith, 2026-10-31: "the serpent is not collecting any crystals ... please fix"):
  * `VacuumSkimmer` becomes a live crystal catcher, the fleet's standard skimmer shape (the
    Sparrow's `DummySkimmer`): GameObject active, trigger sphere on (radius 25 u, its authored
    50 x 0.5), a kinematic Rigidbody, a `SkimmerImpactor`, an `ImpactCollider`, and the shared
    `EventOnSkimmerVesselCollision` channel (the impactor raises it when the sphere crosses
    another hull; a null there would throw).
  * It moves from the NEAR-field slot to the FAR-field slot: a skimmer with no prism effects is a
    crystal catcher, the Butterfly's far field is the precedent, and the Serpent's kit has never
    had a skim. The near-field slot is left empty, as the Termite, Falcon and Shrike's are.
  * Its container, `SerpentSkimmerImpactorDataContainer`, holds ONE crystal effect,
    `SkimmerHandsCrystalToHullEffect` (omni too): the crystal is handed to the hull as if the hull
    had touched it. The sphere strictly contains the hull, so without it the hull would never see an
    elemental crystal again, and the Serpent's own crystal chain (every crystal re-shields its seed
    walls, `SerpentWallShieldByCrystalEffect`) would be lost. The kit is unchanged: the hull runs the
    same chain it ran on contact; the crystal now also pays its element.

A one-shot that STANDS DOWN: each patch asserts on the old text only when the new text is absent,
so a re-run is a no-op and `--check` diffs the result.

    python3 Tools/Build/fix_serpent_crystal_pickup.py           # write
    python3 Tools/Build/fix_serpent_crystal_pickup.py --check   # validate + diff
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

PREFAB = "Assets/_Prefabs/Spacevessels/Serpent.prefab"
EFFECT_CS = ("Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Crystal Effects/"
             "SkimmerHandsCrystalToHullEffectSO.cs")
EFFECT_ASSET = "Assets/_SO_Assets/Effects/Skimmer Crystal Effects/SkimmerHandsCrystalToHullEffect.asset"
CONTAINER = ("Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/"
             "SerpentSkimmerImpactorDataContainer.asset")

G_EFFECT_SCRIPT = lib.guid("script/SkimmerHandsCrystalToHullEffectSO")
G_EFFECT = lib.guid("asset/SkimmerHandsCrystalToHullEffect")
G_CONTAINER = lib.guid("asset/SerpentSkimmerImpactorDataContainer")

REFERENCED = {
    "SkimmerImpactorDataContainerSO": "68b3e74284f14e32a6e21b5fb8c3e8ad",
    "SkimmerImpactor": "c1731f6481c2493aa107099246bb1af8",
    "ImpactCollider": "fafcc767febd41ccbad67c5457dc432d",
    "Skimmer": "f35ad70dcbb4e3b47af654e91d9387f8",
    "EventOnSkimmerVesselCollision": "0753d1e84dfca8a41a5bdc1e4c6e1746",
}

GO = "2695624631939139826"          # VacuumSkimmer
COLLIDER = "1866540621624778926"    # its SphereCollider
SKIMMER = "874579554510349383"      # its Skimmer
# New components (stable fileIDs, checked unique below).
RB = "4417203385218867712"
IMPACTOR = "4417203385218867713"
IMPACT_COLLIDER = "4417203385218867714"


def effect_asset() -> str:
    return (lib.header_for(G_EFFECT_SCRIPT, "SkimmerHandsCrystalToHullEffect")
            + "  alsoAppliesToOmniCrystals: 1\n")


def container_asset() -> str:
    return (lib.header_for(REFERENCED["SkimmerImpactorDataContainerSO"], "SerpentSkimmerImpactorDataContainer")
            + "  vesselSkimmerEffectsSO: []\n"
            + "  skimmerPrismEffectsSO: []\n"
            + "  skimmerCrystalEffectsSO:\n"
            + f"  - {{fileID: 11400000, guid: {G_EFFECT}, type: 2}}\n"
            + "  skimmerLifeformCrystalEffectsSO: []\n")


NEW_DOCS = f"""--- !u!54 &{RB}
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO}}}
  serializedVersion: 5
  m_Mass: 1
  m_LinearDamping: 0
  m_AngularDamping: 0.08
  m_CenterOfMass: {{x: 0, y: 0, z: 0}}
  m_InertiaTensor: {{x: 1, y: 1, z: 1}}
  m_InertiaRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ImplicitCom: 1
  m_ImplicitTensor: 1
  m_UseGravity: 0
  m_IsKinematic: 1
  m_Interpolate: 0
  m_Constraints: 0
  m_CollisionDetection: 0
--- !u!114 &{IMPACTOR}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {REFERENCED['SkimmerImpactor']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  skimmerImpactorDataContainer: {{fileID: 11400000, guid: {G_CONTAINER},
    type: 2}}
  skimmer: {{fileID: {SKIMMER}}}
  CombinedWeight: 0
  SqrSweetSpot: 0
--- !u!114 &{IMPACT_COLLIDER}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {REFERENCED['ImpactCollider']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  impactorObject: {{fileID: {IMPACTOR}}}
"""


def patch_prefab(text: str, errors: list) -> str:
    def once(old: str, new: str, label: str):
        nonlocal text
        if new in text:
            return
        if text.count(old) != 1:
            errors.append(f"{label}: expected the old text exactly once, found {text.count(old)}")
            return
        text = text.replace(old, new, 1)

    # 1. The GameObject: three new components, and active.
    comps_old = (f"  - component: {{fileID: {SKIMMER}}}\n  m_Layer: 7\n  m_Name: VacuumSkimmer\n")
    comps_new = (f"  - component: {{fileID: {SKIMMER}}}\n  - component: {{fileID: {RB}}}\n"
                 f"  - component: {{fileID: {IMPACTOR}}}\n  - component: {{fileID: {IMPACT_COLLIDER}}}\n"
                 f"  m_Layer: 7\n  m_Name: VacuumSkimmer\n")
    once(comps_old, comps_new, "VacuumSkimmer component list")
    go_block = re.search(rf"--- !u!1 &{GO}\n(?:(?!--- ).*\n)*", text)
    if go_block and "  m_IsActive: 0\n" in go_block.group(0):
        text = text.replace(go_block.group(0), go_block.group(0).replace("  m_IsActive: 0\n", "  m_IsActive: 1\n"), 1)

    # 2. The trigger sphere, enabled.
    col = re.search(rf"--- !u!135 &{COLLIDER}\n(?:(?!--- ).*\n)*", text)
    if not col:
        errors.append("VacuumSkimmer SphereCollider not found")
    elif "  m_Enabled: 0\n" in col.group(0):
        text = text.replace(col.group(0), col.group(0).replace("  m_Enabled: 0\n", "  m_Enabled: 1\n"), 1)

    # 3. The skimmer raises the shared skimmer-vs-vessel channel (Skimmer.ExecuteImpactOnShip derefs it).
    sk = re.search(rf"--- !u!114 &{SKIMMER}\n(?:(?!--- ).*\n)*", text)
    if not sk:
        errors.append("VacuumSkimmer Skimmer component not found")
    elif "  onSkimmerShipImpact: {fileID: 0}\n" in sk.group(0):
        text = text.replace(sk.group(0), sk.group(0).replace(
            "  onSkimmerShipImpact: {fileID: 0}\n",
            f"  onSkimmerShipImpact: {{fileID: 11400000, guid: {REFERENCED['EventOnSkimmerVesselCollision']},\n"
            f"    type: 2}}\n"), 1)

    # 4. The three new component documents, right after the Skimmer's.
    if f"--- !u!54 &{RB}\n" not in text:
        sk = re.search(rf"--- !u!114 &{SKIMMER}\n(?:(?!--- ).*\n)*", text)
        if sk:
            text = text.replace(sk.group(0), sk.group(0) + NEW_DOCS, 1)

    # 5. VesselStatus: the catcher is the FAR field; the near field is empty.
    once(f"  _nearFieldSkimmer: {{fileID: {SKIMMER}}}\n  _farFieldSkimmer: {{fileID: 0}}\n",
         f"  _nearFieldSkimmer: {{fileID: 0}}\n  _farFieldSkimmer: {{fileID: {SKIMMER}}}\n",
         "VesselStatus skimmer slots")
    return text


def validate(prefab: str, errors: list):
    ids = re.findall(r"^--- !u!\d+ &(-?\d+)", prefab, re.M)
    if len(ids) != len(set(ids)):
        errors.append("duplicate fileID in Serpent.prefab")
    go_block = re.search(rf"--- !u!1 &{GO}\n(?:(?!--- ).*\n)*", prefab).group(0)
    if "m_IsActive: 1" not in go_block:
        errors.append("VacuumSkimmer still inactive")
    for c in (RB, IMPACTOR, IMPACT_COLLIDER):
        if f"component: {{fileID: {c}}}" not in go_block:
            errors.append(f"component {c} not on VacuumSkimmer")
    col = re.search(rf"--- !u!135 &{COLLIDER}\n(?:(?!--- ).*\n)*", prefab).group(0)
    if "m_Enabled: 1" not in col or "m_IsTrigger: 1" not in col:
        errors.append("VacuumSkimmer sphere is not an enabled trigger")
    if NEW_DOCS not in prefab:
        errors.append("the catcher's Rigidbody / SkimmerImpactor / ImpactCollider differ from the authored blocks")
    if f"_farFieldSkimmer: {{fileID: {SKIMMER}}}" not in prefab:
        errors.append("VesselStatus far field does not point at VacuumSkimmer")
    # The hull's own chain still re-shields on every crystal (the effect this fix exists to keep).
    vessel = lib.ROOT + "/Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/SerpentImpactorDataContainer.asset"
    with open(vessel, encoding="utf-8") as fh:
        vtext = fh.read()
    if vtext.count("d68872316d5041e29b58ce917bffb015") != 5:
        errors.append("SerpentImpactorDataContainer no longer carries the wall re-shield in all five crystal lists")


def main():
    errors = []
    files = {}
    with open(os.path.join(lib.ROOT, PREFAB), encoding="utf-8") as fh:
        prefab = patch_prefab(fh.read(), errors)
    if not errors:
        validate(prefab, errors)
    files[PREFAB] = prefab
    files[EFFECT_CS + ".meta"] = lib.script_meta_text(G_EFFECT_SCRIPT)
    files[EFFECT_ASSET] = effect_asset()
    files[EFFECT_ASSET + ".meta"] = lib.asset_meta_text(G_EFFECT)
    files[CONTAINER] = container_asset()
    files[CONTAINER + ".meta"] = lib.asset_meta_text(G_CONTAINER)

    # Every referenced guid resolves; the minted ones collide with nothing else.
    owned = {os.path.normpath(os.path.join(lib.ROOT, r)) for r in files if r.endswith(".meta")}
    existing = set()
    for d, _, fns in os.walk(os.path.join(lib.ROOT, "Assets")):
        for fn in fns:
            if fn.endswith(".meta") and os.path.normpath(os.path.join(d, fn)) not in owned:
                with open(os.path.join(d, fn), encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
                if m:
                    existing.add(m.group(1))
    for name, g in REFERENCED.items():
        if g not in existing:
            errors.append(f"{name} guid {g} does not resolve")
    for g in (G_EFFECT_SCRIPT, G_EFFECT, G_CONTAINER):
        if g in existing:
            errors.append(f"minted guid {g} collides with an existing asset")

    if errors:
        print("VALIDATION FAILED:")
        for e in errors:
            print("  -", e)
        sys.exit(1)

    if lib.CHECK_ONLY:
        changed = lib.drift(files)
        if changed:
            print(f"--check: {len(changed)} file(s) differ:")
            for c in changed:
                print("  -", c)
            sys.exit(1)
        print(f"--check: OK, {len(files)} file(s) match.")
        return
    for rel, content in files.items():
        full = os.path.join(lib.ROOT, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(content)
    print(f"Wrote {len(files)} file(s).")


if __name__ == "__main__":
    main()

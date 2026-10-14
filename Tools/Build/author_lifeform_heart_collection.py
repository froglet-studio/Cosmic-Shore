#!/usr/bin/env python3
"""Every lifeform heart carries its collection pair - authored on the prefab, not just at runtime.

A lifeform's heart is a nested instance of one of the four elemental crystal prefabs
(`Resources/ElementalCrystalSet`: CrystalCharge / CrystalMass / CrystalSpace / CrystalTime). Those
standalone prefabs carry a `Crystal` and a trigger `SphereCollider` and NOTHING that collects them,
so each lifeform prefab adds two components to its heart instance:

  * `ElementalCrystalImpactor`  - `Crystal` -> the instance's Crystal, one collection effect
                                  (`elementalCrystalShipEffects`, the element-level powerup)
  * `ImpactCollider`            - `impactorObject` -> that impactor

and overrides the heart's `Crystal` component to DISABLED (`m_Enabled: 0`), so a living heart stays
out of `Crystal.Active` and its `Start` (the `crystalValue` stamp) runs when death enables it in
`Crystal.ActivateCrystal`.

A heart without the pair is the worst shape there is: the skimmer's vacuum reads the `Crystal` off
the trigger and drags the dropped heart after the vessel forever, while no contact can ever collect
it (Docs/ECOSYSTEM.md §60 - the QuadFish). `LifeFormCrystal.EnsureElementalCrystal` now wires the
pair at runtime as a backstop; this script is the authoring half, and `--check` is the gate that
keeps a new lifeform from shipping a bare heart again.

    python3 Tools/Build/author_lifeform_heart_collection.py           # author any missing pairs
    python3 Tools/Build/author_lifeform_heart_collection.py --check   # exit 1 if any heart lacks one

Scope: every prefab under the lifeform folders that instances an elemental crystal, minus SKIP.
Idempotent - a heart that already carries the pair is left byte-identical.

How the nested ids are derived (no editor needed): an object inside a nested prefab instance is
addressed in the host file by a STRIPPED stub whose fileID is
`(instanceFileID XOR sourceFileID) & 0x7FFF_FFFF_FFFF_FFFF` - the same rule Unity uses, verified
against every one of the 25 hearts that already carry the pair. The two NEW component documents get
deterministic ids hashed from (prefab path, instance id, role), collision-checked against the file.
"""
from __future__ import annotations

import argparse
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

LIFEFORM_DIRS = ["Assets/_Prefabs/FloraAndFauna", "Assets/_Models/Fauna"]

# Prefabs that instance a heart but are deliberately not authored. Each entry says why.
# Empty: oldWallFlora, the one entry this ever had (unreferenced), was deleted on 2026-10-14.
SKIP: dict[str, str] = {}

CRYSTAL_PREFAB_GUIDS = {
    "ba23c28d2d22a1b458f7b6e91c355d53": "Charge",
    "cccc6ba7985893f43841fccfbb53dc71": "Mass",
    "a4bde9d72595bfb43aa3b791d02f4db8": "Space",
    "e707773a458187444a9dfbf3ec06568f": "Time",
}

CRYSTAL_SCRIPT = "5a8f286a585edf943935c4316edeb2b6"
IMPACTOR_SCRIPT = "e3952a371fe646fcbe11bf76ed7434ac"      # ElementalCrystalImpactor
IMPACT_COLLIDER_SCRIPT = "fafcc767febd41ccbad67c5457dc432d"  # ImpactCollider
# SkimmerAdjustElementLevelByCrystalEffectSO - the one effect all 25 authored hearts carry.
COLLECTION_EFFECT = "0ac04a748b4c4ad8825e811431df45f4"

MASK = 0x7FFF_FFFF_FFFF_FFFF
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\n", re.M)


def docs(text: str) -> dict[str, tuple[str, bool, str]]:
    """fileID -> (classID, stripped, body)."""
    heads = list(DOC_RE.finditer(text))
    out = {}
    for i, m in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
        out[m.group(2)] = (m.group(1), bool(m.group(3)), text[m.end():end])
    return out


def source_ids(guid: str) -> tuple[str, str]:
    """(root GameObject fileID, Crystal MonoBehaviour fileID) inside an elemental crystal prefab."""
    meta = next(p for p in (ROOT / "Assets").rglob("*.prefab.meta") if f"guid: {guid}" in p.read_text())
    d = docs(meta.with_suffix("").read_text())
    for fid, (cls, _, body) in d.items():
        if cls == "114" and f"guid: {CRYSTAL_SCRIPT}" in body:
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1)
            return go, fid
    raise SystemExit(f"no Crystal component in crystal prefab {guid}")


def mint(path: str, instance: str, role: str, taken: set[str]) -> str:
    salt = 0
    while True:
        h = hashlib.sha256(f"{path}|{instance}|{role}|{salt}".encode()).digest()
        fid = str(int.from_bytes(h[:8], "little") & MASK)
        if fid not in taken and len(fid) >= 15:
            taken.add(fid)
            return fid
        salt += 1


def target_line(fid: str, guid: str) -> str:
    # Unity wraps the target reference after the guid; match it so the file reads as Unity's own.
    return f"    - target: {{fileID: {fid}, guid: {guid},\n        type: 3}}\n"


def author(path: Path) -> tuple[str, list[str]]:
    """Returns (new text, list of hearts changed)."""
    rel = path.relative_to(ROOT).as_posix()
    text = path.read_text()
    d = docs(text)
    taken = set(d)
    changed = []

    for inst, (cls, _, body) in list(d.items()):
        if cls != "1001":
            continue
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", body)
        if not src or src.group(1) not in CRYSTAL_PREFAB_GUIDS:
            continue
        guid = src.group(1)
        if "addedObject" in body and IMPACTOR_SCRIPT in "".join(
                d[a][2] for a in re.findall(r"addedObject: \{fileID: (-?\d+)\}", body) if a in d):
            continue  # already carries the pair

        src_go, src_crystal = source_ids(guid)
        go = str((int(inst) ^ int(src_go)) & MASK)
        crystal = str((int(inst) ^ int(src_crystal)) & MASK)
        impactor = mint(rel, inst, "impactor", taken)
        collider = mint(rel, inst, "impact-collider", taken)

        new_body = body
        # 1. the Crystal component authored disabled, as on every working heart
        if not re.search(rf"target: \{{fileID: {src_crystal}, guid: {guid},\s*type: 3\}}\s*\n\s*propertyPath: m_Enabled\n",
                         new_body):
            mod = (target_line(src_crystal, guid) +
                   "      propertyPath: m_Enabled\n      value: 0\n      objectReference: {fileID: 0}\n")
            new_body = new_body.replace("    m_RemovedComponents:", mod + "    m_RemovedComponents:", 1)
        # 2. the two added components
        added = "".join(
            f"    - targetCorrespondingSourceObject: {{fileID: {src_go}, guid: {guid},\n"
            f"        type: 3}}\n      insertIndex: -1\n      addedObject: {{fileID: {fid}}}\n"
            for fid in (impactor, collider))
        if "    m_AddedComponents: []\n" in new_body:
            new_body = new_body.replace("    m_AddedComponents: []\n", "    m_AddedComponents:\n" + added, 1)
        else:
            new_body = new_body.replace("  m_SourcePrefab:", added + "  m_SourcePrefab:", 1)
        assert new_body.count(f"addedObject: {{fileID: {impactor}}}") == 1, f"{rel}: added list edit missed"
        text = text.replace(body, new_body, 1)

        # 3. stripped stubs for the objects the new components point at
        extra = []
        if go not in d:
            extra.append(f"--- !u!1 &{go} stripped\nGameObject:\n"
                         f"  m_CorrespondingSourceObject: {{fileID: {src_go}, guid: {guid},\n    type: 3}}\n"
                         f"  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
        if crystal not in d:
            extra.append(f"--- !u!114 &{crystal} stripped\nMonoBehaviour:\n"
                         f"  m_CorrespondingSourceObject: {{fileID: {src_crystal}, guid: {guid},\n    type: 3}}\n"
                         f"  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
                         f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 0\n  m_EditorHideFlags: 0\n"
                         f"  m_Script: {{fileID: 11500000, guid: {CRYSTAL_SCRIPT}, type: 3}}\n"
                         f"  m_Name: \n  m_EditorClassIdentifier: \n")
        extra.append(f"--- !u!114 &{impactor}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
                     f"  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
                     f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n"
                     f"  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {IMPACTOR_SCRIPT}, type: 3}}\n"
                     f"  m_Name: \n  m_EditorClassIdentifier: \n  Crystal: {{fileID: {crystal}}}\n"
                     f"  elementalCrystalShipEffects:\n"
                     f"  - {{fileID: 11400000, guid: {COLLECTION_EFFECT}, type: 2}}\n")
        extra.append(f"--- !u!114 &{collider}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
                     f"  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
                     f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n"
                     f"  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {IMPACT_COLLIDER_SCRIPT}, type: 3}}\n"
                     f"  m_Name: \n  m_EditorClassIdentifier: \n  impactorObject: {{fileID: {impactor}}}\n")
        if not text.endswith("\n"):
            text += "\n"
        text += "".join(extra)
        changed.append(f"{CRYSTAL_PREFAB_GUIDS[guid]} heart (instance {inst})")
    return text, changed


def lifeform_prefabs() -> list[Path]:
    out = []
    for folder in LIFEFORM_DIRS:
        for p in sorted((ROOT / folder).rglob("*.prefab")):
            if p.relative_to(ROOT).as_posix() in SKIP:
                continue
            t = p.read_text()
            if any(f"m_SourcePrefab: {{fileID: 100100000, guid: {g}" in t for g in CRYSTAL_PREFAB_GUIDS):
                out.append(p)
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="write nothing; exit 1 if any heart lacks its pair")
    args = ap.parse_args()

    prefabs = lifeform_prefabs()
    pending = []
    for p in prefabs:
        new, changed = author(p)
        if not changed:
            continue
        rel = p.relative_to(ROOT).as_posix()
        pending.append((rel, changed))
        if not args.check:
            p.write_text(new)

    for rel, changed in pending:
        print(f"{'MISSING' if args.check else 'authored'}: {rel}: {', '.join(changed)}")
    for rel, why in SKIP.items():
        print(f"skipped:  {rel} - {why}")
    if args.check and pending:
        print(f"FAIL: {len(pending)} lifeform prefab(s) carry a heart with no ElementalCrystalImpactor + "
              f"ImpactCollider - run without --check to author them (Docs/ECOSYSTEM.md §60)")
        return 1
    print(f"OK: {len(prefabs)} lifeform prefab(s) scanned, every heart carries its collection pair")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""
Authors the MASS crystal's look: one geometry, one motion, and the elemental crystals' colours.

    python3 Tools/Build/author_mass_crystal_look.py [--check]

The Mass crystal is four nested shells of `MassCrystalExport1_8-21-25.fbx` running the Shepard
tone (each shell's scale sweeps a band once per period, alpha = (1.05 - s) * _Opacity). Before
this it had two defects, both in the asset data rather than the code:

1. ITS COLOURS DID NOT READ LIKE THE OTHER ELEMENTALS. The shells were on `ShepardGraph`, whose
   colour is lerp(dull, bright, (1 - N.V)^4) - a hairline rim over a flat body - painted at
   runtime by `Crystal`'s property-block tint. Space and Time are on `SpreadFresnelShader`:
   lerp(bright, dark, (1 + N.V) / 2) on their own material pair (blue-white over deep navy while
   embedded, lime over near-black once free), a broad gradient the tint does not reach. So the
   eight shell materials move onto `OmniShepardFresnelShader` - ShepardGraph's band motion and
   alpha transcribed, with SpreadFresnelShader's colour formula - and take their colour pair from
   `BlueCrystalFresnelMateriall` / `LimeCrystalFresnelMaterial`, exactly as the omni crystal's
   tone triangles already do (`author_omni_crystal_triangles.py`).

2. ITS GEOMETRY CHANGED WITH ITS STATE. Four lifeforms (Gyroid, Tadpole, Mass Shark, Mass
   Brittlestar) nested `CrystalMass` but removed its four shell MeshRenderers and added a
   SkinnedMeshRenderer on the SPACE crystal's `spacecrystalanim.fbx` plus a `SpaceCrystalAnimator`
   per shell. Every other Mass crystal - every free pickup, every runtime-provisioned heart, the
   Codex and the toys - is the base prefab's static Mass shells, so a Mass crystal spun on the
   Space crystal's blocks on those four species and did not anywhere else. Element identity is
   SHAPE (Docs/PALETTE.md 2.2); those overrides are stripped so the base prefab's shells apply.

The spent husk keeps its shatter. `Impact` drives `_velocity`, which only ShepardGraph reads, so
each Mass prefab's `explodingMaterial` points at `ExplodingMassCrystalMaterial[ n]` - the four
shells' previous ShepardGraph materials, carried verbatim - the same split Space and Time have
(their exploding material is not their default material).

`--check` fails on any drift: a shell material that is not what this script authors, a Mass
prefab whose exploding material cannot shatter, or a nested `CrystalMass` that swaps its shells.
"""

import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_omni_crystal_triangles import _donor, _tone_material, MAT_DIR, ROOT  # noqa: E402

CRYSTAL_MASS_GUID = "cccc6ba7985893f43841fccfbb53dc71"     # CrystalMass.prefab
SHEPARD_GRAPH_GUID = "71fa822036d425446afa6c9c07046aef"    # ShepardGraph.shadergraph
SPACE_ANIMATOR_GUID = "445bd9b09ae11aa40a872cdb1a636881"   # SpaceCrystalAnimator.cs
SPACE_MESH_GUID = "39e205c7c7716094991df8c57e3e0753"       # spacecrystalanim.fbx

ACTIVE_DONOR = "LimeCrystalFresnelMaterial"     # a free Space/Time crystal's pair
INACTIVE_DONOR = "BlueCrystalFresnelMateriall"  # an embedded Space/Time heart's pair

# One row per shell, in crystalModels order: (shell, active, inactive, exploding, start, stop,
# scale distance, queue). The bands, scaling and draw order are the Mass crystal's own, unchanged.
SHELLS = [
    ("innerShell",  "ActiveMassCrystalMaterial",   "BlueMassCrystalMaterial",   "ExplodingMassCrystalMaterial",   0.33, 0,    1, 2999),
    ("secondShell", "ActiveMassCrystalMaterial 1", "BlueMassCrystalMaterial 1", "ExplodingMassCrystalMaterial 1", 0.66, 0.33, 1, 3000),
    ("easedShell",  "ActiveMassCrystalMaterial 2", "BlueMassCrystalMaterial 2", "ExplodingMassCrystalMaterial 2", 1,    0.66, 1, 3001),
    ("outerShell",  "ActiveMassCrystalMaterial 3", "BlueMassCrystalMaterial 3", "ExplodingMassCrystalMaterial 3", 1.03, 0.98, 0, 3001),
]

# Prefabs whose crystalModels are the Mass shells, and the husks whose authored material is one.
MASS_CRYSTAL_PREFABS = [
    "Assets/_Prefabs/Environment/CrystalMass.prefab",
    "Assets/_Prefabs/Environment/ActiveCrystalMass.prefab",
    "Assets/_Prefabs/Environment/Spawners/SpawnedSegments.prefab",
]
HUSK_PREFABS = [
    "Assets/_Prefabs/Environment/MassDandruff.prefab",
    "Assets/_Prefabs/Environment/Crystal Explosion Dummy.prefab",
]
LIFEFORM_ROOTS = ["Assets/_Prefabs", "Assets/_Models"]

# The base prefab's four shell MeshRenderers - what the space-mesh swap removed.
SHELL_RENDERERS = {"4867199500739826565", "6301062769599518322",
                   "6207000290512909467", "7842243008615492145"}


def guid_of(rel):
    with open(os.path.join(ROOT, rel + ".meta")) as f:
        return re.search(r"^guid: (\w+)$", f.read(), re.M).group(1)


def mat_rel(name):
    return f"{MAT_DIR}/{name}.mat"


def fmt(x):
    return str(x)


def material_texts():
    active, inactive = _donor(ACTIVE_DONOR), _donor(INACTIVE_DONOR)
    out = {}
    for _shell, act, ina, _exp, start, stop, scale, queue in SHELLS:
        out[mat_rel(act)] = _tone_material(act, ACTIVE_DONOR, active, fmt(start), fmt(stop), scale, queue)
        out[mat_rel(ina)] = _tone_material(ina, INACTIVE_DONOR, inactive, fmt(start), fmt(stop), scale, queue)
    return out


def guid_ref(guid):
    return f"{{fileID: 2100000, guid: {guid}, type: 2}}"


def repoint_exploding(text):
    """Every explodingMaterial that names a shell's ACTIVE material names its exploding one."""
    for _shell, act, _ina, exp, *_ in SHELLS:
        text = text.replace(f"explodingMaterial: {guid_ref(guid_of(mat_rel(act)))}",
                            f"explodingMaterial: {guid_ref(guid_of(mat_rel(exp)))}")
    return text


def repoint_husk(text):
    """A husk's authored material is the innermost shell's exploding material (HandleImpact
    replaces it per pickup; this is only what the pooled renderer holds between pickups)."""
    exp0 = guid_ref(guid_of(mat_rel(SHELLS[0][3])))
    for _shell, act, ina, *_ in SHELLS:
        for name in (act, ina):
            text = text.replace(f"  - {guid_ref(guid_of(mat_rel(name)))}\n", f"  - {exp0}\n")
    return text


# ── the space-mesh swap on a nested CrystalMass ──

ADDED_RE = re.compile(
    r"    - targetCorrespondingSourceObject: \{fileID: -?\d+, guid: \w+,?\s*type: 3\}\n"
    r"      insertIndex: -?\d+\n"
    r"      addedObject: \{fileID: (-?\d+)\}\n")
ANIMATOR_MOD_RE = re.compile(
    r"    - target: \{fileID: -?\d+, guid: \w+,?\s*type: 3\}\n"
    r"      propertyPath: crystalModels\.Array\.data\[\d+\]\.spaceCrystalAnimator\n"
    r"      value:.*\n"
    r"      objectReference: \{fileID: -?\d+\}\n")


def split_docs(text):
    head, *docs = text.split("\n--- ")
    return head, docs


def doc_id(doc):
    return re.match(r"!u!\d+ &(-?\d+)", doc).group(1)


def is_space_swap(doc):
    """A SkinnedMeshRenderer on the space crystal's mesh, or a SpaceCrystalAnimator."""
    if doc.startswith("!u!137 "):
        return f"guid: {SPACE_MESH_GUID}," in doc
    if doc.startswith("!u!114 "):
        return f"m_Script: {{fileID: 11500000, guid: {SPACE_ANIMATOR_GUID}, type: 3}}" in doc
    return False


def strip_space_swap(text):
    """Returns (text, n) with every nested CrystalMass's space-mesh swap removed."""
    head, docs = split_docs(text)
    by_id = {doc_id(d): d for d in docs}
    dropped = set()
    stripped = 0
    for i, doc in enumerate(docs):
        if not doc.startswith("!u!1001 ") or f"m_SourcePrefab: {{fileID: 100100000, guid: {CRYSTAL_MASS_GUID}" not in doc:
            continue
        new = doc

        def keep_added(m):
            fid = m.group(1)
            if fid in by_id and is_space_swap(by_id[fid]):
                dropped.add(fid)
                return ""
            return m.group(0)

        new = ADDED_RE.sub(keep_added, new)
        new = ANIMATOR_MOD_RE.sub("", new)
        for fid in SHELL_RENDERERS:
            new = re.sub(rf"    - \{{fileID: {fid}, guid: {CRYSTAL_MASS_GUID}, type: 3\}}\n", "", new)
        new = new.replace("    m_RemovedComponents:\n    m_RemovedGameObjects:",
                          "    m_RemovedComponents: []\n    m_RemovedGameObjects:")
        new = new.replace("    m_AddedComponents:\n  m_SourcePrefab:", "    m_AddedComponents: []\n  m_SourcePrefab:")
        if new != doc:
            docs[i] = new
            stripped += 1
    if not stripped:
        return text, 0

    docs = [d for d in docs if doc_id(d) not in dropped]
    # A shell GameObject is declared `stripped` only so the added components could hang off it;
    # once nothing references it, it goes too.
    while True:
        body = "\n--- ".join([head] + docs)
        orphan = [d for d in docs if re.match(r"!u!1 &-?\d+ stripped\n", d)
                  and body.count(f"{{fileID: {doc_id(d)}}}") == 0]
        if not orphan:
            break
        docs = [d for d in docs if d not in orphan]
    out = "\n--- ".join([head] + docs)
    if text.endswith("\n") and not out.endswith("\n"):
        out += "\n"  # the dropped last document carried the file's final newline
    for fid in dropped:
        if f"fileID: {fid}}}" in out:
            raise SystemExit(f"strip_space_swap: {fid} is still referenced after its removal")
    return out, stripped


def lifeform_prefabs():
    for root in LIFEFORM_ROOTS:
        for dirpath, _dirs, files in os.walk(os.path.join(ROOT, root)):
            for f in files:
                if f.endswith(".prefab"):
                    path = os.path.join(dirpath, f)
                    with open(path) as fh:
                        if CRYSTAL_MASS_GUID in fh.read():
                            yield os.path.relpath(path, ROOT)


def read(rel):
    with open(os.path.join(ROOT, rel)) as f:
        return f.read()


def invariant_errors(want):
    """What must hold of the authored output, independent of how it got there."""
    errs = []
    exploding = {guid_of(mat_rel(row[3])) for row in SHELLS}
    for rel in MASS_CRYSTAL_PREFABS:
        text = want.get(rel, read(rel))
        for g in re.findall(r"explodingMaterial: \{fileID: 2100000, guid: (\w+), type: 2\}", text):
            if g not in exploding:
                errs.append(f"{rel}: explodingMaterial {g} is not an ExplodingMassCrystalMaterial "
                            f"(the husk's shatter needs ShepardGraph's _velocity)")
    for _shell, _act, _ina, exp, *_ in SHELLS:
        if f"guid: {SHEPARD_GRAPH_GUID}," not in read(mat_rel(exp)):
            errs.append(f"{mat_rel(exp)} is not on ShepardGraph, so the husk cannot shatter")
    for rel in lifeform_prefabs():
        text = want.get(rel, read(rel))
        _h, docs = split_docs(text)
        swap = [doc_id(d) for d in docs if is_space_swap(d)]
        nested = [d for d in docs if d.startswith("!u!1001 ") and CRYSTAL_MASS_GUID in d]
        if any(any(f"addedObject: {{fileID: {s}}}" in n for s in swap) for n in nested):
            errs.append(f"{rel}: a nested CrystalMass wears the Space crystal's mesh/animator")
        for n in nested:
            removed = re.search(r"    m_RemovedComponents:(.*?)\n    m_RemovedGameObjects:", n, re.S)
            if removed and any(fid in removed.group(1) for fid in SHELL_RENDERERS):
                errs.append(f"{rel}: a nested CrystalMass removes its shell renderers")
    return errs


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true",
                    help="verify the shipped assets match what this script would author")
    args = ap.parse_args()

    want = dict(material_texts())
    for rel in MASS_CRYSTAL_PREFABS:
        want[rel] = repoint_exploding(read(rel))
    for rel in HUSK_PREFABS:
        want[rel] = repoint_husk(read(rel))
    for rel in lifeform_prefabs():
        text, n = strip_space_swap(read(rel))
        if n:
            print(f"  space-mesh swap on {n} nested CrystalMass in {rel}")
        want[rel] = text

    failures = []
    for rel, text in want.items():
        if read(rel) == text:
            print(f"  ok      {rel}")
            continue
        if args.check:
            failures.append(rel)
            print(f"  DRIFT   {rel}")
        else:
            with open(os.path.join(ROOT, rel), "w") as f:
                f.write(text)
            print(f"  wrote   {rel}")

    errs = invariant_errors(want)
    for e in errs:
        print(f"  FAIL    {e}")
    if failures or errs:
        raise SystemExit("author_mass_crystal_look: " +
                         (", ".join(failures) + " differ from what this script authors. "
                          "Run it without --check." if failures else "") +
                         (f" {len(errs)} invariant failure(s)." if errs else ""))


if __name__ == "__main__":
    main()

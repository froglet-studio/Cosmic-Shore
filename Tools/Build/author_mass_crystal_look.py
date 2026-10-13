#!/usr/bin/env python3
"""
Authors the MASS crystal's look: one geometry, one motion, and the elemental crystals' colours.

    python3 Tools/Build/author_mass_crystal_look.py [--check]

SINCE 2026-10-12 (Omni Shepard Lab round 8, the "Depth ratchet"): the four shells are retired. The inner
shell of `CrystalMass` / `ActiveCrystalMass` is the ONE prism renderer: `MassCrystalPrisms.asset` (the 60
prisms baked SEATED and 6.25 thick - grown inward, BAKED INTO the mesh since 2026-10-13 so the materials ship
`_Thickness 1` and hull fusion can move the vertices - TEXCOORD2 = plate centre + index) on
`MassDepthPrisms[Inactive].mat` (`OmniDepthPrismShader`), driven by `CrystalDepthPrisms` with the omni's profile;
`to_prisms()` deletes the other three shells (their consumers reference only the root objects - swept)
and is idempotent. The shell materials below stay authored; the husk keeps `ExplodingMassCrystalMaterial`.
NOTE (pre-existing, 2026-10-12): the eight shell materials were since re-saved by the Unity Editor
(sorted properties, keywords), so `--check` reports them as DRIFT; that is editor normalisation of the
retired materials, not a change in look. Re-running this script without `--check` rewrites them in the
generator's order - harmless, but noisy.

The Mass crystal was four nested shells of `MassCrystalExport1_8-21-25.fbx` running the Shepard
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
(their exploding material is not their default material). A dropped heart keeps it too: since
2026-10-08 `Crystal.LerpCrystalMaterialCoroutine` hands a model settling onto its OWN default
material its authored husk back instead of overwriting it with the default.

`--check` fails on any drift: a shell material that is not what this script authors, a Mass
prefab whose exploding material cannot shatter, or a nested `CrystalMass` that swaps its shells.
"""

import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hashlib  # noqa: E402

import author_omni_crystal_triangles as OMNI  # noqa: E402
from author_omni_crystal_triangles import _donor, _tone_material, MAT_DIR, ROOT  # noqa: E402

CRYSTAL_MASS_GUID = "cccc6ba7985893f43841fccfbb53dc71"     # CrystalMass.prefab
SHEPARD_GRAPH_GUID = "71fa822036d425446afa6c9c07046aef"    # ShepardGraph.shadergraph
SPACE_ANIMATOR_GUID = "445bd9b09ae11aa40a872cdb1a636881"   # SpaceCrystalAnimator.cs
SPACE_MESH_GUID = "39e205c7c7716094991df8c57e3e0753"       # spacecrystalanim.fbx

ACTIVE_DONOR = "LimeCrystalFresnelMaterial"     # a free Space/Time crystal's pair
INACTIVE_DONOR = "BlueCrystalFresnelMateriall"  # an embedded Space/Time heart's pair

# Fresnel contrast, tuned from play (2026-10-08: "the inactive was too white, not enough blue; the
# active was too lime, not enough dark"). The shells are SEE-THROUGH, so their back faces show, and
# on SpreadFresnelShader's ramp a back face is always at the bright end: measured by compiling the
# shader's fragment with clang over a sphere, 76% of every back face read bright (26% of the
# front). _FaceForward measures N.V on the camera side, so every face is dark at its centre front
# or back; _RimPower 3 then pulls the bright colour out to the silhouette - about a quarter bright
# on both sides. Both are OmniShepardFresnelShader opt-ins, inert at the defaults the omni uses.
RIM_POWER = 3
FACE_FORWARD = 1

# Colour pairs. Free (lime) keeps the Space/Time pair exactly, so the three free elementals stay one
# lime family; the curve above is what makes it read dark. Embedded (blue) leaves the blue-white
# pair for a saturated blue over a deeper navy: the same hue family, less white, more depth.
ACTIVE_COLORS = None  # the donor's own _BrightColor / _DarkColor
INACTIVE_COLORS = ("{r: 0.2, g: 0.4, b: 1, a: 1}", "{r: 0, g: 0.005, b: 0.18, a: 1}")

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


# ── THE LOOK since the Omni Shepard Lab round 7 (decision 2026-10-12): the Depth ratchet ──
# The four transparent shells are retired. The inner shell becomes ONE opaque prism renderer - the 60 prisms
# of MassCrystalExport1, baked SEATED into a native mesh with each prism's centre and index in TEXCOORD2 -
# on OmniDepthPrismShader, driven by CrystalDepthPrisms with the omni's profile. The other three shells go.
# The Mass has no body, so there are no holes to cut; its prisms are the crystal. The lab's pick for the Mass:
# the omni's recipe at thickness 6.25 (inward).
MASS_FBX = "Assets/_Models/MassCrystalExport1_8-21-25.fbx"
MASS_PRISM_MESH = "Assets/_Models/MassCrystalPrisms.asset"
MASS_PRISM_GUID = hashlib.md5(b"CosmicShore/MassCrystal/MassCrystalPrisms.asset").hexdigest()
MASS_DEPTH_THICKNESS = 6.25
MASS_DEPTH_NAME = "MassDepthPrisms"
PRISM_PREFABS = {   # prefab -> is its renderer authored in the free (lime) state
    "Assets/_Prefabs/Environment/CrystalMass.prefab": False,
    "Assets/_Prefabs/Environment/ActiveCrystalMass.prefab": True,
}
KEPT_SHELL = "innerShell"                                   # becomes the prism renderer
RETIRED_SHELLS = ("secondShell", "easedShell", "outerShell")
PRISM_GO_NAME = MASS_DEPTH_NAME
DEPTH_COMPONENT_ID = 6380255250831503961                    # CrystalDepthPrisms on the kept shell
INNER_EXPLODING = "ExplodingMassCrystalMaterial"


def mass_prism_mesh():
    verts, tangents, indices = OMNI.bake_prism_mesh(MASS_FBX, 60)
    return verts, tangents, indices


def mass_prism_mesh_text():
    verts, tangents, indices = mass_prism_mesh()
    return OMNI.prism_mesh_text(OMNI.thicken(verts, MASS_DEPTH_THICKNESS), tangents, indices, "MassCrystalPrisms")


MASS_PRISM_META = OMNI.TRI_MESH_META.replace(OMNI.TRI_MESH_GUID, MASS_PRISM_GUID)


def depth_material_texts():
    omni = OMNI.material_texts()
    body = omni[f"{MAT_DIR}/OmniCrystalBody.mat"]
    body_inactive = omni[f"{MAT_DIR}/OmniCrystalBodyInactive.mat"]
    top_h, split = OMNI.prism_shape(mass_prism_mesh()[0])
    return {
        mat_rel(MASS_DEPTH_NAME): OMNI.depth_material(MASS_DEPTH_NAME, "OmniCrystalBody", body,
                                                      OMNI.MATERIAL_THICKNESS, top_h, split),
        mat_rel(MASS_DEPTH_NAME + "Inactive"): OMNI.depth_material(MASS_DEPTH_NAME + "Inactive", "OmniCrystalBodyInactive",
                                                                   body_inactive, OMNI.MATERIAL_THICKNESS, top_h, split),
    }


def material_meta(rel):
    return OMNI.material_meta(os.path.basename(rel)[:-4]).replace(
        OMNI.mat_guid(os.path.basename(rel)[:-4]), mass_mat_guid(os.path.basename(rel)[:-4]))


def mass_mat_guid(name):
    return hashlib.md5(f"CosmicShore/MassCrystal/{name}.mat".encode()).hexdigest()


def to_prisms(text, free):
    """A Mass crystal prefab with its four shells reduced to the one depth-prism renderer. Idempotent."""
    head, docs = split_docs(text)
    names = {}
    for d in docs:
        m = re.match(r"!u!1 &(-?\d+)\nGameObject:\n(?:.*\n)*?  m_Name: (.*)\n", d)
        if m:
            names[m.group(1)] = m.group(2)
    by_name = {v: k for k, v in names.items()}
    kept_go = by_name.get(KEPT_SHELL) or by_name.get(PRISM_GO_NAME)
    if kept_go is None:
        raise SystemExit("to_prisms: neither the inner shell nor the prism renderer is in the prefab")
    doomed_go = {by_name[n] for n in RETIRED_SHELLS if n in by_name}

    def owner(d):
        m = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}$", d, re.M)
        return m.group(1) if m else None
    doomed = {doc_id(d) for d in docs if doc_id(d) in doomed_go or owner(d) in doomed_go}
    docs = [d for d in docs if doc_id(d) not in doomed]
    out = []
    mat = mass_mat_guid(MASS_DEPTH_NAME if free else MASS_DEPTH_NAME + "Inactive")
    has_depth = any(doc_id(d) == str(DEPTH_COMPONENT_ID) for d in docs)
    for d in docs:
        i = doc_id(d)
        if i == kept_go:
            d = re.sub(r"^  m_Name: .*$", f"  m_Name: {PRISM_GO_NAME}", d, count=1, flags=re.M)
            if f"component: {{fileID: {DEPTH_COMPONENT_ID}}}" not in d:
                d = re.sub(r"(^  m_Component:\n(?:  - component: .*\n)+)",
                           lambda m: m.group(1) + f"  - component: {{fileID: {DEPTH_COMPONENT_ID}}}\n", d, count=1, flags=re.M)
        elif owner(d) == kept_go and d.startswith("!u!33 "):
            d = re.sub(r"^  m_Mesh: \{.*\}$", f"  m_Mesh: {{fileID: 4300000, guid: {MASS_PRISM_GUID}, type: 2}}",
                       d, count=1, flags=re.M)
        elif owner(d) == kept_go and d.startswith("!u!23 "):
            d = re.sub(r"(^  m_Materials:\n)  - \{[^}]*\}\n", lambda m: m.group(1) + f"  - {guid_ref(mat)}\n",
                       d, count=1, flags=re.M)
        elif i == str(DEPTH_COMPONENT_ID):
            continue   # rewritten below, so a retune of the component reaches every prefab
        elif d.startswith("!u!4 ") and re.search(r"^  m_Father: \{fileID: 0\}$", d, re.M):
            for t in doomed:
                d = d.replace(f"  - {{fileID: {t}}}\n", "")
        elif d.startswith("!u!114 ") and "crystalModels:" in d:
            row = (f"  crystalModels:\n  - model: {{fileID: {kept_go}}}\n"
                   f"    defaultMaterial: {guid_ref(mass_mat_guid(MASS_DEPTH_NAME))}\n"
                   f"    explodingMaterial: {guid_ref(guid_of(mat_rel(INNER_EXPLODING)))}\n"
                   f"    inactiveMaterial: {guid_ref(mass_mat_guid(MASS_DEPTH_NAME + 'Inactive'))}\n"
                   f"    spaceCrystalAnimator: {{fileID: 0}}\n")
            d, n = re.subn(r"^  crystalModels:\n(?:  - model:.*\n(?:    .*\n)*)+", row, d, count=1, flags=re.M)
            assert n == 1, "crystalModels block"
        out.append(d)
    depth_doc = OMNI.depth_prisms_doc(DEPTH_COMPONENT_ID, kept_go, False)[len("--- "):]
    out.append(depth_doc)
    result = "\n--- ".join([head] + out)
    if not result.endswith("\n"):
        result += "\n"
    result = result.replace("\n\n--- ", "\n--- ")
    for t in doomed:
        if re.search(rf"\b{t}\b", result):
            raise SystemExit(f"to_prisms: retired shell object {t} is still referenced")
    return result


def guid_of(rel):
    with open(os.path.join(ROOT, rel + ".meta")) as f:
        return re.search(r"^guid: (\w+)$", f.read(), re.M).group(1)


def mat_rel(name):
    return f"{MAT_DIR}/{name}.mat"


def fmt(x):
    return str(x)


def _sub1(text, pattern, repl, label):
    out, n = re.subn(pattern, repl, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"mass material: {label} not found exactly once")
    return out


def _contrast(text, name, colors):
    """The tone material plus the Mass crystal's fresnel contrast (and, optionally, its own pair)."""
    text = _sub1(text, r"^    - _Opacity: 1\n",
                 lambda m: m.group(0) + f"    - _FaceForward: {FACE_FORWARD}\n", name + " _FaceForward")
    text = _sub1(text, r"^    - _ReceiveShadows: .*\n",
                 lambda m: m.group(0) + f"    - _RimPower: {RIM_POWER}\n", name + " _RimPower")
    if colors:
        text = _sub1(text, r"^    - _BrightColor: \{.*\}$", f"    - _BrightColor: {colors[0]}", name + " _BrightColor")
        text = _sub1(text, r"^    - _DarkColor: \{.*\}$", f"    - _DarkColor: {colors[1]}", name + " _DarkColor")
    return text


def material_texts():
    active, inactive = _donor(ACTIVE_DONOR), _donor(INACTIVE_DONOR)
    out = {}
    for _shell, act, ina, _exp, start, stop, scale, queue in SHELLS:
        out[mat_rel(act)] = _contrast(
            _tone_material(act, ACTIVE_DONOR, active, fmt(start), fmt(stop), scale, queue), act, ACTIVE_COLORS)
        out[mat_rel(ina)] = _contrast(
            _tone_material(ina, INACTIVE_DONOR, inactive, fmt(start), fmt(stop), scale, queue), ina, INACTIVE_COLORS)
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
    want[MASS_PRISM_MESH] = mass_prism_mesh_text()
    want[MASS_PRISM_MESH + ".meta"] = MASS_PRISM_META
    for rel, text in depth_material_texts().items():
        want[rel] = text
        want[rel + ".meta"] = material_meta(rel)
    for rel in MASS_CRYSTAL_PREFABS:
        text = repoint_exploding(read(rel))
        want[rel] = to_prisms(text, PRISM_PREFABS[rel]) if rel in PRISM_PREFABS else text
    for rel in HUSK_PREFABS:
        want[rel] = repoint_husk(read(rel))
    for rel in lifeform_prefabs():
        text, n = strip_space_swap(read(rel))
        if n:
            print(f"  space-mesh swap on {n} nested CrystalMass in {rel}")
        want[rel] = text

    failures = []
    for rel, text in want.items():
        if os.path.exists(os.path.join(ROOT, rel)) and read(rel) == text:
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

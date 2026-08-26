#!/usr/bin/env python3
"""Author every lifeform's HEART SIZE, from its own measured body.

Docs/ECOSYSTEM.md §39.2.  Run with --check in CI; --write to author.

    python3 Tools/Build/author_lifeform_heart_sizes.py            # report only
    python3 Tools/Build/author_lifeform_heart_sizes.py --check    # fail on drift
    python3 Tools/Build/author_lifeform_heart_sizes.py --write    # author the assets

WHY THIS EXISTS
---------------
Lifeform LEVELS are retired (§39).  A lifeform is its species and its ELEMENT, and
nothing else — so everything an element states about itself, it states exactly once, in
its own variant tuning block.  That now includes the size of its HEART, the elemental
crystal it drops on death.

Before §39, every heart in the game was ONE size (3.5 world scale, ×1.05 per level).
That uniformity was itself a fix: §33 had found the per-prefab scales it replaced were an
accident nobody authored — 0.7 on a tadpole up to 4.0 on a gyroid, a 5.7× reward spread
that had never been a decision.  Flattening removed the accident and created a different
one.  A heart's rendered extent is `rootScale × perElementApparentExtent` (2.03 Charge /
2.71 Mass / 2.10 Space / 1.96 Time units per unit of root scale), so a uniform 3.5 renders
**6.8–9.5 units across** — which is 3.6× a Mass tadpole's own width, 1.1× a piranha's
ENTIRE LENGTH, and 11% of a shark.  One number cannot be right for both.

So the band is AUTHORED now, and it is authored from a MEASUREMENT rather than by eye.

THE LAW
-------
A heart's linear size scales as the SQUARE ROOT of its lifeform's linear size:

    heart = K · bodyDiameter ^ HEART_EXPONENT          (HEART_EXPONENT = 0.5)

That is ordinary allometry — an organ does not scale 1:1 with body length — and it is
what makes the whole roster fit one reward band.  A shark is 19× a piranha; its heart is
4.3× as big, not 19×.

`K` is not authored.  It is SOLVED so the largest lifeform in the project lands exactly on
`HEART_MAX`, which means:

  * the band is always fully used — no species is squashed into the bottom of it, and
  * **nothing can ever clip the reward cap**, because the top of the band IS the anchor.

That second property is load-bearing.  Heart world scale is read AS GAMEPLAY in five
places (see below); the collect reward is `min(worldScale × 0.1, 0.5)`, so it saturates at
world scale 5.0.  Past that point two visibly different hearts pay the same — a size the
player can see and a reward they cannot.  `HEART_MAX` sits under it with margin, and this
script FAILS if any authored heart escapes.  Do NOT answer an overshoot by retuning
`levelPerUnitScale`: that constant is shared with every non-lifeform elemental crystal
(the Wanderway conveyor, Dog Fight's arena scatter).  Compress the mapping instead.

WHAT "SIZE" MEANS, PER KINGDOM
------------------------------
The measure is the lifeform's **settled body diameter** — how big the thing is when you
look at it — because that is the question the player is actually asking.

  * FAUNA: measured off the prefab, by walking the transform hierarchy and taking the
    farthest reach of any body prism or model mesh.  Then scaled by the element's own
    `Variant.BaseBodyScale`, which REPLACES the prefab's root scale — that is why the
    Charge/Space tadpoles (0.70) measure nearly twice the Mass/Time ones (0.40), and why
    the Astro-League piranha (0.22) is the smallest lifeform in the game.

  * FLORA: derived from the two numbers each element authors for itself — its leaf PRISM
    footprint and its per-plant prism BUDGET.  Every flora growth form in this project is
    a surface or a branch skeleton rather than a solid, so `N` prisms of footprint `A`
    settle into a disc of radius `sqrt(N·A/π)`.  Two numbers, both already authored per
    element, and the four elements of one species genuinely differ by them (the Space
    gyroid grows 40×1×1 needles where Mass grows 7×4.5×3.5 slabs).

  * `FLORA_BUDGET_CEILING` is the one honest fudge, and it is called out rather than
    hidden.  Four species (Branching / Cacti / Nerve / Pine) carry a per-plant budget of
    5000 or 400 that is an UNBOUNDED sentinel, not a target — the cells that actually
    plant them override it to 150–190 (Rampage) and the largest genuinely-authored budget
    in the canonical set is Arbor Mass at 312.  Sizing against 5000 would let four
    outliers set `K` for everybody and squash the other eighteen species into the bottom
    of the band.  So the sizing budget is capped; the PLANTING budget is untouched.

WHERE HEART WORLD SCALE IS READ AS GAMEPLAY (all five — check before retuning)
-----------------------------------------------------------------------------
  A. collect reward     SkimmerAdjustElementLevelByCrystalEffectSO.Execute
                        `min(|lossyScale.x| × 0.1, 0.5)` element levels
  B. live domain buff   DomainFaunaBuffSystem.ComputeHeartValue — the same function,
                        summed over every LIVING heart of a domain
  C. pickup radius      every crystal prefab's root SphereCollider is radius 1, so the
                        world trigger radius EQUALS the root world scale
  D. vacuum speed       Crystal.Vacuum divides by lossyScale.x — a bigger heart is drawn
                        in more slowly, which is a reasonable read of "heavier"
  E. capture flourish   ElementalCrystalImpactor.RunCapture's recoil radii and husk scale

A is the one with a cap.  C is the one to keep an eye on at the bottom of the band: the
skimmer sphere is 15–30 units, so the crystal's own collider is a small addend, but
`HEART_MIN` exists so it never becomes a hairline.
"""

from __future__ import annotations

import argparse
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
LIFEFORMS = ASSETS / "_SO_Assets" / "Lifeforms"
CELL_CONFIGS = ASSETS / "_SO_Assets" / "Cell Configs"

# --- The law -----------------------------------------------------------------------

HEART_EXPONENT = 0.5      # allometric: heart ~ sqrt(body). See the module docstring.
HEART_MAX = 4.6           # the largest lifeform's heart. Under MaxSafeHeartWorldScale.
HEART_MIN = 1.0           # a heart must stay a visible, skimmable pickup (read C above).

# Mirrors ElementalCrystalSetSO.MaxSafeHeartWorldScale — the reward cap with 4% margin.
# Kept as a literal here on purpose: this script's whole job is to prove the C# constant,
# so reading it back from the C# would make the check circular. If they disagree the
# assertion below fails, which is exactly what should happen.
MAX_SAFE_HEART_WORLD_SCALE = 4.8

# The reward saturates here (levelPerUnitScale 0.1 / maxLevelGainPerCrystal 0.5).
REWARD_CAP_WORLD_SCALE = 5.0

# See FLORA_BUDGET_CEILING in the docstring. 400 is Nerve's authored budget and the
# largest number in the canonical set that is a real per-plant target rather than a
# "no limit" sentinel; Arbor Mass's 312 is the largest that is unambiguously one.
FLORA_BUDGET_CEILING = 400

FLORA_SCRIPT_GUID = "a32a297a7606432885f4d3e1f83bea9a"
FAUNA_SCRIPT_GUID = "c778cfbe4dfc4c5c8401e40c17802311"

ELEMENTS = {1: "Charge", 2: "Mass", 3: "Space", 4: "Time"}

# Per-element apparent extent, in Unity units per unit of ROOT scale — measured from each
# crystal FBX's Vertices bounds normalized by its UnitScaleFactor, times that prefab's
# model-child correction (Charge 1.00 / Mass 1.38 / Space 1.34 / Time 1.42). Reported
# only, never applied: the root scale is the gameplay number and the model child is where
# a per-element size fix belongs (LifeFormCrystal's header).
APPARENT_EXTENT = {1: 2.032, 2: 2.705, 3: 2.097, 4: 1.955}

# Species prefab guid -> the prefab that IS that species' body. Resolved once (each guid
# is owned by exactly one .meta) and pinned so a rename is a loud failure, not a silent
# re-resolution to some other asset (the head-1 trap, /asset-surgery).
FAUNA_PREFABS = {
    "Tadpole":      "Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab",
    "Brittlestar":  "Assets/_Models/Fauna/MassBrittlestarFauna.prefab",
    "Shark":        "Assets/_Models/Fauna/MassSharkFauna.prefab",
    "QuadFish":     "Assets/_Prefabs/FloraAndFauna/QuadFish.prefab",
    "Clawfish":     "Assets/_Prefabs/FloraAndFauna/Clawfish.prefab",
    # The colony ROOT is an empty population anchor (deliberately heartless, §23.3); the
    # body that carries a heart is a SEGMENT, and the head is the largest of the three.
    "WormColony":   "Assets/_Prefabs/FloraAndFauna/WormHeadSegment.prefab",
}

FLORA_PREFABS = {
    "Arbor":        "Assets/_Prefabs/FloraAndFauna/ArborFlora.prefab",
    "Branching":    "Assets/_Prefabs/FloraAndFauna/BranchingFlora.prefab",
    "Cacti":        "Assets/_Prefabs/FloraAndFauna/CactiFlora.prefab",
    "Coral":        "Assets/_Prefabs/FloraAndFauna/CoralFlora.prefab",
    "Frond":        "Assets/_Prefabs/FloraAndFauna/FrondFlora.prefab",
    "Gyroid":       "Assets/_Prefabs/FloraAndFauna/GyroidFlora.prefab",
    "Lantern":      "Assets/_Prefabs/FloraAndFauna/LanternFlora.prefab",
    "Nerve":        "Assets/_Prefabs/FloraAndFauna/NerveFlora.prefab",
    "Pine":         "Assets/_Prefabs/FloraAndFauna/PineFlora.prefab",
    "Quasicrystal": "Assets/_Prefabs/FloraAndFauna/QuasicrystalFlora.prefab",
    "Reed":         "Assets/_Prefabs/FloraAndFauna/ReedFlora.prefab",
    "Rosette":      "Assets/_Prefabs/FloraAndFauna/RosetteFlora.prefab",
    "SchwarzP":     "Assets/_Prefabs/FloraAndFauna/SchwarzPFlora.prefab",
    "Spire":        "Assets/_Prefabs/FloraAndFauna/SpireFlora.prefab",
    "Tendril":      "Assets/_Prefabs/FloraAndFauna/TendrilFlora.prefab",
    "Wall":         "Assets/_Prefabs/FloraAndFauna/WallFlora.prefab",
}


# --- Unity YAML ---------------------------------------------------------------------

DOC_RE = re.compile(r"--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=\n--- !u!|\Z)", re.S)


def split_docs(text: str):
    """{fileID: (classId, body)} for one Unity YAML file.

    Split FIRST, query WITHIN one document — a line-anchored regex with re.S over the
    whole file happily pairs one document's header with another's body (/asset-surgery).
    """
    return {m.group(2): (m.group(1), m.group(3)) for m in DOC_RE.finditer(text)}


def field(body: str, key: str):
    m = re.search(r"^\s+%s: (.*)$" % re.escape(key), body, re.M)
    return m.group(1).strip() if m else None


def vec3(body: str, key: str):
    m = re.search(
        r"^\s+%s: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}" % re.escape(key),
        body, re.M)
    return (float(m.group(1)), float(m.group(2)), float(m.group(3))) if m else None


def parse_vec3_inline(s: str):
    m = re.match(r"\{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}", s or "")
    return (float(m.group(1)), float(m.group(2)), float(m.group(3))) if m else None


# --- Fauna body measurement ----------------------------------------------------------

_MESH_BOUNDS_CACHE = {}
_META_INDEX = None


def _meta_index():
    """{guid: asset path} over the whole project. One .meta OWNS a guid.

    Built with `grep -c "^guid: $g"`-equivalent semantics rather than "first file that
    mentions it": an FBX .meta can carry an externalObjects material remap naming ANOTHER
    FBX's guid, so a first-hit resolution returns a plausible wrong model (/asset-surgery).
    """
    global _META_INDEX
    if _META_INDEX is not None:
        return _META_INDEX
    _META_INDEX = {}
    for meta in ASSETS.rglob("*.meta"):
        try:
            head = meta.read_text(encoding="utf-8", errors="replace")[:400]
        except OSError:
            continue
        m = re.search(r"^guid: (\w+)", head, re.M)
        if m and m.group(1) not in _META_INDEX:
            _META_INDEX[m.group(1)] = meta.with_suffix("")
    return _META_INDEX


def _fbx_mesh_extent(fbx_path: Path):
    """Largest bounding-box dimension of an FBX's geometry, in UNITY units.

    Raw FBX extents from two files are NOT comparable: normalize by the file's own
    `UnitScaleFactor` first (/asset-surgery). One project model declares 1 where the rest
    declare 100, an 80x difference that reads as a defect and is a unit declaration.

    Returns None for anything this codec cannot read (FBX 7.5+, a non-FBX model), which
    the caller degrades to a transform-only measurement rather than failing on.
    """
    key = str(fbx_path)
    if key in _MESH_BOUNDS_CACHE:
        return _MESH_BOUNDS_CACHE[key]

    extent = None
    try:
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        import fbx_binary                                   # noqa: PLC0415
        nodes, _version, _footer = fbx_binary.read(str(fbx_path))

        unit = 1.0
        for top in nodes:
            if top.name != "GlobalSettings":
                continue
            props = top.first("Properties70")
            for prop in (props.find("P") if props else []):
                vals = [v for _t, v in prop.props]
                if vals and vals[0] == "UnitScaleFactor":
                    unit = float(vals[-1])

        best = 0.0
        for top in nodes:
            if top.name != "Objects":
                continue
            for geo in top.find("Geometry"):
                verts = geo.first("Vertices")
                if not verts or not verts.props:
                    continue
                flat = verts.props[0][1]
                for axis in range(3):
                    comp = flat[axis::3]
                    if comp:
                        best = max(best, max(comp) - min(comp))
        # Unity's importer applies the cm->m conversion, so a file declaring 100 lands
        # 1:1 and a file declaring 1 lands at 1/100 of its raw numbers.
        if best > 0.0:
            extent = best * unit / 100.0
    except Exception:                                        # noqa: BLE001
        extent = None
    finally:
        if sys.path and sys.path[0] == str(Path(__file__).resolve().parent):
            sys.path.pop(0)

    _MESH_BOUNDS_CACHE[key] = extent
    return extent


def _node_mesh_extents(text: str, docs: dict):
    """{transformFileID: largest mesh dimension in Unity units, at localScale 1}.

    A creature's REACH is not all in its transforms: the tadpole and the clawfish draw
    their whole bodies from one FBX mesh, so a transform-only walk measures them at a
    seventh of their real size while correctly measuring the rigged species (whose bones
    ARE transforms). This is what closes that gap.
    """
    idx = _meta_index()
    # GameObject fileID -> its Transform fileID, so a mesh found on a GameObject can be
    # placed in the transform tree.
    go_to_xform = {}
    for fid, (cls, body) in docs.items():
        if cls != "4":
            continue
        m = re.match(r"\{fileID: (\d+)\}", field(body, "m_GameObject") or "")
        if m:
            go_to_xform[m.group(1)] = fid

    out = {}
    for _fid, (cls, body) in docs.items():
        if cls not in ("33", "137"):          # MeshFilter, SkinnedMeshRenderer
            continue
        gm = re.match(r"\{fileID: (\d+)\}", field(body, "m_GameObject") or "")
        if not gm or gm.group(1) not in go_to_xform:
            continue
        key = "m_Mesh" if cls == "33" else "m_Mesh"
        mm = re.search(r"%s: \{fileID: -?\d+, guid: (\w+)" % key, body)
        if not mm:
            continue
        model = idx.get(mm.group(1))
        if not model or model.suffix.lower() != ".fbx":
            continue
        ext = _fbx_mesh_extent(model)
        if ext:
            xf = go_to_xform[gm.group(1)]
            out[xf] = max(out.get(xf, 0.0), ext)
    return out


def _instance_transforms(text: str, docs: dict):
    """Transforms contributed by NESTED PREFAB INSTANCES (`!u!1001` blocks).

    A nested instance is not a `!u!4` document — its pose lives as `m_LocalPosition.*` /
    `m_LocalScale.*` rows inside its own `m_Modifications` list, and its parent is that
    block's `m_TransformParent` (/asset-surgery: an instance is reachable that way ALWAYS,
    and through the parent's `m_Children` only when the parent is a plain Transform).

    Returns {fileID: (parent, localPos, localScale, sourceGuid)}. The guid matters as much
    as the pose: a nested instance contributes its SOURCE prefab's whole body, and that
    body is not in this file at all. Skipping either half is not a small loss — the
    Tadpole's entire body and the Clawfish's entire model are nested instances, so a walk
    that ignores them measures those two species at a seventh of their real size (§4.9).
    """
    out = {}
    for fid, (cls, body) in docs.items():
        if cls != "1001":
            continue
        m = re.search(r"m_TransformParent: \{fileID: (\d+)\}", body)
        parent = m.group(1) if m else "0"
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", body)
        pos = [0.0, 0.0, 0.0]
        scale = [1.0, 1.0, 1.0]
        # propertyPath / value rows WRAP, so walk the lines. A one-line regex over an
        # m_Modifications list matches zero entries and reads as "no overrides".
        lines = body.split("\n")
        for i, line in enumerate(lines):
            pm = re.match(r"\s+propertyPath: m_Local(Position|Scale)\.([xyz])\s*$", line)
            if not pm or i + 1 >= len(lines):
                continue
            vm = re.match(r"\s+value: ([-\d.eE]+)\s*$", lines[i + 1])
            if not vm:
                continue
            axis = "xyz".index(pm.group(2))
            (pos if pm.group(1) == "Position" else scale)[axis] = float(vm.group(1))
        out[fid] = (parent, tuple(pos), tuple(scale), src.group(1) if src else None)
    return out


def _measure_reach(prefab_path: Path, include_root_scale: bool, seen=None) -> float:
    """Radius of the sphere containing this prefab's body, about its own root.

    Walks the transform tree DOWN from the root, composing
    `world = parentPos + parentScale (x) localPos`, and takes the farthest reach of any
    node: its offset plus half its own extent. A node's extent is the larger of what it
    SCALES (a body prism carries its size as its localScale), what it DRAWS (a model
    mesh's bounds, which no transform records), and — for a nested prefab instance — the
    whole body of its SOURCE prefab, measured recursively.

    Rotation is deliberately ignored: it cannot change the distance of a node's ORIGIN
    from the root, and can only redistribute an extent between axes, so the estimate stays
    a bound either way and stays comparable across species.

    `include_root_scale` is False at the TOP call only, because a fauna variant's
    `BaseBodyScale` REPLACES the root's own scale (`Fauna.ApplyVariantTuning`) and the
    caller applies the element's value instead. It is True for every nested source, whose
    root scale is a real part of the parent's geometry.
    """
    seen = seen or set()
    key = str(prefab_path)
    if key in seen or not prefab_path.exists():
        return 0.0                     # cycle, or a source outside the project
    seen = seen | {key}

    text = prefab_path.read_text(encoding="utf-8", errors="replace")
    docs = split_docs(text)

    nodes = {}                         # fileID -> (parent, localPos, localScale)
    nested = {}                        # fileID -> source prefab path
    for fid, (cls, body) in docs.items():
        if cls != "4":
            continue
        m = re.match(r"\{fileID: (\d+)\}", field(body, "m_Father") or "")
        nodes[fid] = (m.group(1) if m else "0",
                      vec3(body, "m_LocalPosition") or (0.0, 0.0, 0.0),
                      vec3(body, "m_LocalScale") or (1.0, 1.0, 1.0))
    for fid, (parent, pos, scale, guid) in _instance_transforms(text, docs).items():
        nodes[fid] = (parent, pos, scale)
        src = _meta_index().get(guid) if guid else None
        if src and src.suffix.lower() == ".prefab":
            nested[fid] = src

    if not nodes:
        return 0.0

    mesh_extent = _node_mesh_extents(text, docs)

    children = {}
    roots = []
    for fid, (parent, _, _) in nodes.items():
        (children.setdefault(parent, []).append(fid) if parent in nodes
         else roots.append(fid))

    reach = 0.0
    stack = [(r, (0.0, 0.0, 0.0), (1.0, 1.0, 1.0), True) for r in roots]
    visited = set()
    while stack:
        fid, ppos, pscale, is_root = stack.pop()
        if fid in visited:
            continue
        visited.add(fid)
        _, lp, ls = nodes[fid]

        if is_root and not include_root_scale:
            wpos, wscale = (0.0, 0.0, 0.0), (1.0, 1.0, 1.0)
        else:
            wpos = tuple(ppos[i] + pscale[i] * lp[i] for i in range(3))
            wscale = tuple(pscale[i] * ls[i] for i in range(3))
            biggest = max(abs(v) for v in wscale)
            own = max(biggest, mesh_extent.get(fid, 0.0) * biggest)
            if fid in nested:
                # The instance's whole source body, scaled by the pose it is placed at.
                own = max(own, 2.0 * _measure_reach(nested[fid], True, seen) * biggest)
            reach = max(reach, math.dist(wpos, (0.0, 0.0, 0.0)) + 0.5 * own)

        for c in children.get(fid, ()):
            stack.append((c, wpos, wscale, False))

    return reach


def measure_fauna_body_diameter(prefab_path: Path) -> float:
    """Settled body diameter of one creature, at root scale 1 (BaseBodyScale applies on top)."""
    reach = _measure_reach(prefab_path, include_root_scale=False)
    if reach <= 0.0:
        raise SystemExit(f"{prefab_path}: measured a zero body - the walk found nothing")
    return 2.0 * reach


# --- Config census -------------------------------------------------------------------

class Variant:
    __slots__ = ("path", "species", "element", "kind", "body", "heart", "enabled",
                 "leaf", "budget", "doc_id")

    def __init__(self, **kw):
        for k, v in kw.items():
            setattr(self, k, v)


def species_of(asset_name: str) -> str:
    """'Arbor Flora Charge' -> 'Arbor';  'Worm Colony Mass' -> 'WormColony'."""
    stem = re.sub(r" (Charge|Mass|Space|Time)$", "", asset_name)
    stem = re.sub(r" (Flora|Fauna)$", "", stem)
    return stem.replace(" ", "")


def _prefab_guid_index():
    """{prefab guid: species key} for every species this script knows how to measure."""
    idx = _meta_index()
    by_path = {}
    for guid, path in idx.items():
        by_path.setdefault(str(path), guid)
    out = {}
    for species, rel in list(FAUNA_PREFABS.items()) + list(FLORA_PREFABS.items()):
        full = str(ROOT / rel)
        g = by_path.get(full)
        if g:
            out[g] = species
    # The colony ROOT is what a config references; the body that carries a heart is a
    # SEGMENT (FAUNA_PREFABS points at the head). Map the root's guid across by hand.
    root = by_path.get(str(ROOT / "Assets/_Prefabs/FloraAndFauna/WormColony.prefab"))
    if root:
        out[root] = "WormColony"
    return out


def read_deployment_variants():
    """Per-cell configs that author their OWN variant instead of a canonical palette.

    A config with `SpreadElements` and an `ElementPalette` reads its whole variant block —
    heart included — from the palette sibling, so it inherits the canonical heart and needs
    nothing here. A config that authors its own block does NOT, and would silently take the
    platform default: that is the Astro-League piranha, which is the SMALLEST lifeform in
    the game and therefore the single case this whole resize exists to fix.

    Element is often None on these (they keep the prefab's authored crystal), so they are
    sized once per config rather than per element.
    """
    prefab_species = _prefab_guid_index()
    out = []
    for path in sorted(CELL_CONFIGS.rglob("*.asset")):
        text = path.read_text(encoding="utf-8", errors="replace")
        docs = split_docs(text)
        mb = [(fid, b) for fid, (c, b) in docs.items() if c == "114"]
        if not mb:
            continue
        fid, body = mb[0]
        guid = re.search(r"m_Script:.*?guid: (\w+)", body, re.S)
        if not guid:
            continue
        kind = ("flora" if guid.group(1) == FLORA_SCRIPT_GUID
                else "fauna" if guid.group(1) == FAUNA_SCRIPT_GUID else None)
        if kind is None:
            continue

        # Inherits its heart from the canonical palette - nothing to author.
        if field(body, "SpreadElements") == "1" and "\n  ElementPalette:\n  - " in body:
            continue

        pref = re.search(r"%sPrefab: \{fileID: -?\d+, guid: (\w+)"
                         % ("Flora" if kind == "flora" else "Fauna"), body)
        if not pref:
            continue                       # FaunaPrefab {fileID: 0}: a dead config
        species = prefab_species.get(pref.group(1))
        if not species:
            continue                       # a prefab this script has no measurement for

        el = field(body, "Element")
        out.append(Variant(
            path=path, species=species,
            element=int(el) if el and int(el) in ELEMENTS else 2,
            kind=kind, doc_id=fid,
            enabled=field(body, "Enabled") == "1",
            leaf=parse_vec3_inline(field(body, "LeafSize") or ""),
            budget=field(body, "MaxTotalSpawnedObjects"),
            body=None, heart=None,
        ))
    return out


def read_canonical_variants():
    """The 88 canonical (species × element) assets in _SO_Assets/Lifeforms."""
    out = []
    for path in sorted(LIFEFORMS.glob("*.asset")):
        text = path.read_text(encoding="utf-8", errors="replace")
        docs = split_docs(text)
        mb = [(fid, b) for fid, (c, b) in docs.items() if c == "114"]
        if not mb:
            continue
        fid, body = mb[0]
        guid = re.search(r"m_Script:.*?guid: (\w+)", body, re.S)
        kind = "flora" if guid and guid.group(1) == FLORA_SCRIPT_GUID else "fauna"

        el = field(body, "Element")
        if el is None or int(el) not in ELEMENTS:
            continue                          # WormColonyConfig / …FaunaConfig: no element

        species = species_of(path.stem)
        prefabs = FLORA_PREFABS if kind == "flora" else FAUNA_PREFABS
        if species not in prefabs:
            continue

        out.append(Variant(
            path=path, species=species, element=int(el), kind=kind, doc_id=fid,
            enabled=field(body, "Enabled") == "1",
            leaf=parse_vec3_inline(field(body, "LeafSize") or ""),
            budget=field(body, "MaxTotalSpawnedObjects"),
            body=None, heart=None,
        ))
    return out


def prefab_flora_defaults(path: Path):
    text = path.read_text(encoding="utf-8", errors="replace")
    leaf = None
    budget = None
    for _, (cls, b) in split_docs(text).items():
        if cls != "114":
            continue
        if leaf is None:
            leaf = vec3(b, "leafSize")
        if budget is None:
            v = field(b, "maxTotalSpawnedObjects")
            if v is not None:
                budget = int(v)
    return leaf, budget


# --- Sizing --------------------------------------------------------------------------

def compute_body_diameters(variants):
    """Fill Variant.body — the settled body diameter, in world units."""
    fauna_base = {s: measure_fauna_body_diameter(ROOT / p) for s, p in FAUNA_PREFABS.items()}
    flora_default = {s: prefab_flora_defaults(ROOT / p) for s, p in FLORA_PREFABS.items()}

    for v in variants:
        if v.kind == "fauna":
            # BaseBodyScale REPLACES the prefab's root scale (Fauna.ApplyVariantTuning),
            # so it is a straight multiplier on a diameter measured with the root
            # excluded. 0 (or a disabled block) means "keep the prefab's own root".
            root_scale = None
            raw = read_field_from_asset(v.path, "BaseBodyScale") if v.enabled else None
            if raw is not None and float(raw) > 0:
                root_scale = float(raw)
            if root_scale is None:
                root_scale = prefab_root_scale(ROOT / FAUNA_PREFABS[v.species])
            v.body = fauna_base[v.species] * root_scale
        else:
            leaf, budget = flora_default[v.species]
            if v.enabled and v.leaf and any(abs(c) > 1e-6 for c in v.leaf):
                leaf = v.leaf
            if v.enabled and v.budget is not None and int(v.budget) > 0:
                budget = int(v.budget)
            if not leaf or not budget:
                raise SystemExit(f"{v.path.name}: cannot resolve leaf size / prism budget")
            # A flora growth form is a surface or a branch skeleton, never a solid: N
            # prisms of footprint A settle into a disc of radius sqrt(N·A/π).
            footprint = abs(leaf[0]) * abs(leaf[1])
            n = min(budget, FLORA_BUDGET_CEILING)
            v.body = 2.0 * math.sqrt(n * footprint / math.pi)


def prefab_root_scale(path: Path) -> float:
    text = path.read_text(encoding="utf-8", errors="replace")
    for _, (cls, b) in split_docs(text).items():
        if cls == "4" and re.search(r"^\s+m_Father: \{fileID: 0\}", b, re.M):
            s = vec3(b, "m_LocalScale") or (1.0, 1.0, 1.0)
            return max(abs(c) for c in s)
    return 1.0


def read_field_from_asset(path: Path, key: str):
    text = path.read_text(encoding="utf-8", errors="replace")
    for _, (cls, b) in split_docs(text).items():
        if cls == "114":
            return field(b, key)
    return None


def solve_scale_constant(variants) -> float:
    """K such that the LARGEST lifeform's heart lands exactly on HEART_MAX.

    Anchoring on the top rather than authoring K outright is what makes it impossible for
    any species to clip the reward cap: the top of the band IS the anchor.
    """
    biggest = max(v.body for v in variants)
    return HEART_MAX / (biggest ** HEART_EXPONENT)


def assign_hearts(variants):
    k = solve_scale_constant(variants)
    for v in variants:
        v.heart = round(k * (v.body ** HEART_EXPONENT), 3)
    return k


# --- Authoring -----------------------------------------------------------------------

HEART_KEY = "HeartWorldScale"


def render_variant_block_edit(body: str, heart: float) -> str:
    """Write HeartWorldScale into a Variant block, enabling it if it was off.

    Unity YAML is name-KEYED, so a key the file lacks deserializes to the C# initializer —
    which for HeartWorldScale is 0, the "not authored" sentinel. So an INSERT is enough
    for an asset that has never carried the field.

    The Enabled flip is the load-bearing half: CellLifeSpawnerBase only applies a variant
    tuning block when `Enabled` is true, so a heart authored into a disabled block would
    never be read. Five flora species and every un-migrated fauna ship with Enabled: 0 on
    three of their four elements, so this is the common case, not the edge case.
    """
    if re.search(r"^\s+%s: " % HEART_KEY, body, re.M):
        body = re.sub(r"^(\s+%s: ).*$" % HEART_KEY, lambda m: m.group(1) + fmt(heart),
                      body, count=1, flags=re.M)
    else:
        m = re.search(r"^(\s+)Enabled: [01]$", body, re.M)
        if m:
            indent = m.group(1)
            body = (body[:m.end()] + f"\n{indent}{HEART_KEY}: {fmt(heart)}" + body[m.end():])
        else:
            # No Variant block at all — a config that never overrode anything, which
            # includes the `SpreadElements: 1` + EMPTY `ElementPalette` shape (it rolls the
            # element but keeps its OWN tuning, so it inherits no canonical heart).
            #
            # Unity YAML is name-KEYED, so placement is cosmetic and any of these anchors is
            # correct; they are tried in Unity's own declaration order so the file still
            # reads the way Unity would rewrite it.
            block = f"  Variant:\n    Enabled: 1\n    {HEART_KEY}: {fmt(heart)}"
            em = re.search(r"^  Element: -?\d+$", body, re.M)
            sm = re.search(r"^  SpreadElements: [01]$", body, re.M)
            if em:
                body = body[:em.end()] + "\n" + block + body[em.end():]
            elif sm:
                body = body[:sm.start()] + block + "\n" + body[sm.start():]
            else:
                body = body.rstrip("\n") + "\n" + block + "\n"

    body = re.sub(r"^(\s+)Enabled: 0$", r"\1Enabled: 1", body, count=1, flags=re.M)
    return body


def fmt(x: float) -> str:
    s = f"{x:.3f}".rstrip("0").rstrip(".")
    return s or "0"


def author(variants, write: bool):
    """Returns [(path, before, after)] for every asset whose text would change."""
    changes = []
    for v in variants:
        text = v.path.read_text(encoding="utf-8", errors="replace")
        docs = split_docs(text)
        cls, body = docs[v.doc_id]
        new_body = render_variant_block_edit(body, v.heart)
        if new_body == body:
            continue
        # Reassemble by replacing the document body in place — never a slice on unverified
        # anchor order (/asset-surgery: an empty slice shreds the file).
        assert body in text, f"{v.path.name}: document body not found verbatim"
        assert text.count(body) == 1, f"{v.path.name}: ambiguous document body"
        new_text = text.replace(body, new_body)
        changes.append((v.path, text, new_text))
        if write:
            v.path.write_text(new_text, encoding="utf-8")
    return changes


# --- Report --------------------------------------------------------------------------

def report(variants, k, deployments=()):
    dep = {id(d) for d in deployments}
    by_species = {}
    for v in variants:
        if id(v) in dep:
            continue
        by_species.setdefault(v.species, {})[v.element] = v

    print(f"\nheart = {k:.5f} · bodyDiameter^{HEART_EXPONENT}"
          f"   (K solved so the largest lifeform lands on HEART_MAX {HEART_MAX})\n")
    print(f"{'species':14s} {'kind':6s} " + " ".join(
        f"{ELEMENTS[e]:>18s}" for e in sorted(ELEMENTS)))
    print(f"{'':14s} {'':6s} " + " ".join(f"{'body → heart':>18s}" for _ in ELEMENTS))
    print("-" * 96)
    for s in sorted(by_species):
        row = by_species[s]
        kind = next(iter(row.values())).kind
        cells = []
        for e in sorted(ELEMENTS):
            v = row.get(e)
            cells.append(f"{v.body:8.1f} →{v.heart:6.2f}" if v else f"{'—':>18s}")
        print(f"{s:14s} {kind:6s} " + " ".join(f"{c:>18s}" for c in cells))

    if deployments:
        print("\nper-cell deployments that author their own variant block "
              "(these do NOT inherit a canonical heart):")
        for d in sorted(deployments, key=lambda v: v.heart):
            print(f"    {d.heart:5.2f}  body {d.body:7.1f}  "
                  f"{d.path.relative_to(ASSETS / '_SO_Assets')}")

    lo = min(variants, key=lambda v: v.heart)
    hi = max(variants, key=lambda v: v.heart)
    print(f"\nband  {lo.heart:.2f} ({lo.species} {ELEMENTS[lo.element]}, body {lo.body:.1f})"
          f"  →  {hi.heart:.2f} ({hi.species} {ELEMENTS[hi.element]}, body {hi.body:.1f})"
          f"   spread {hi.heart / lo.heart:.2f}×")
    print(f"collect reward   {lo.heart * 0.1:.3f} → {hi.heart * 0.1:.3f} element levels"
          f"   (cap {0.5:.2f} at world scale {REWARD_CAP_WORLD_SCALE})")
    print("rendered extent, per element (root × apparent extent):")
    for e in sorted(ELEMENTS):
        ex_lo, ex_hi = lo.heart * APPARENT_EXTENT[e], hi.heart * APPARENT_EXTENT[e]
        print(f"    {ELEMENTS[e]:7s} {ex_lo:5.2f} → {ex_hi:5.2f} units across")


# Lattice flora carry a HARD upper bound the reward cap knows nothing about: a plant's
# heart sits in an authored ALCOVE and must not burst it. `QuasicrystalAssembler.
# heartSeatInset` is an absolute world-unit clearance that deliberately does NOT scale with
# the lattice (`ApplyLatticeScale` leaves it alone), and Tools/Build/
# fit_quasicrystal_strut_sizes.py --check gates the strut fit on it. Schwarz P records the
# same ceiling (Docs/ECOSYSTEM.md §34 — a per-species CrystalScalePerLevel of 1.2 once made
# the heart burst its own seat from level 3 up).
#
# NOTE a genuine disagreement between two measurements of the same thing, recorded rather
# than resolved: the shipped fitter derives its need as `worldScale x ~0.6` apparent radius,
# while measuring the four crystal FBXs directly (Vertices bounds / UnitScaleFactor, times
# each prefab's model-child correction) gives 1.02-1.35 per unit of world scale — a 2x
# spread. This gate uses the SHIPPED fitter's convention, because that is the number the
# repo's own CI is calibrated against and a second convention here would just be a third
# opinion. Whichever is right, the band this script authors is well under both.
LATTICE_SPECIES = ("Gyroid", "SchwarzP", "Quasicrystal", "Wall")
LATTICE_SEAT_APPARENT_RADIUS_PER_UNIT = 0.6   # fit_quasicrystal_strut_sizes.py's convention
LATTICE_SEAT_CLEARANCE = 2.55                 # its CRYSTAL_NEED, in world units


def assert_invariants(variants):
    hi = max(v.heart for v in variants)
    lo = min(v.heart for v in variants)
    fails = []
    if hi > MAX_SAFE_HEART_WORLD_SCALE:
        fails.append(
            f"largest heart {hi:.3f} exceeds MaxSafeHeartWorldScale "
            f"{MAX_SAFE_HEART_WORLD_SCALE} - it would clip the collect reward cap. "
            f"Compress the mapping (lower HEART_EXPONENT or HEART_MAX); do NOT retune "
            f"levelPerUnitScale, which is shared with non-lifeform crystals.")
    if hi >= REWARD_CAP_WORLD_SCALE:
        fails.append(f"largest heart {hi:.3f} reaches the reward cap "
                     f"{REWARD_CAP_WORLD_SCALE} - the top of the band pays nothing extra.")
    if lo < HEART_MIN:
        fails.append(
            f"smallest heart {lo:.3f} is under HEART_MIN {HEART_MIN} - its pickup "
            f"trigger (radius == world scale) becomes a hairline. Raise HEART_EXPONENT.")

    # A bigger lifeform must never carry a smaller heart. This holds by construction for a
    # positive exponent, so it is asserted as a proof that the MEASUREMENT did not invert -
    # a body-size bug shows up here and nowhere else.
    order = sorted(variants, key=lambda v: v.body)
    for a, b in zip(order, order[1:]):
        if b.heart + 1e-6 < a.heart:
            fails.append(f"non-monotone: {b.species} {ELEMENTS[b.element]} "
                         f"(body {b.body:.1f}) has a smaller heart than {a.species} "
                         f"{ELEMENTS[a.element]} (body {a.body:.1f})")

    seat_cap = LATTICE_SEAT_CLEARANCE / LATTICE_SEAT_APPARENT_RADIUS_PER_UNIT
    for v in variants:
        if v.species in LATTICE_SPECIES and v.heart > seat_cap:
            fails.append(
                f"{v.species} {ELEMENTS[v.element]} heart {v.heart:.3f} bursts its lattice "
                f"heart seat (max {seat_cap:.2f} at clearance {LATTICE_SEAT_CLEARANCE})")
    return fails


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--write", action="store_true", help="author the assets")
    ap.add_argument("--check", action="store_true",
                    help="fail if any asset differs from what this script would author")
    args = ap.parse_args()

    canonical = read_canonical_variants()
    deployments = read_deployment_variants()
    variants = canonical + deployments
    if len(canonical) != 88:
        print(f"WARNING: found {len(canonical)} canonical (species x element) assets, "
              f"expected 88 (22 species x 4 elements)", file=sys.stderr)
    print(f"{len(canonical)} canonical variants + {len(deployments)} per-cell deployments "
          f"that author their own variant block", file=sys.stderr)
    compute_body_diameters(variants)
    k = assign_hearts(variants)
    report(variants, k, deployments)

    fails = assert_invariants(variants)
    if fails:
        print("\nFAIL:")
        for f in fails:
            print("  * " + f)
        return 1

    changes = author(variants, write=args.write)
    if args.write:
        print(f"\nwrote {len(changes)} assets")
        return 0
    if args.check:
        if changes:
            print(f"\nFAIL: {len(changes)} assets differ from what this script authors:")
            for path, _, _ in changes:
                print("  * " + str(path.relative_to(ROOT)))
            print("  run with --write")
            return 1
        print("\nOK: every lifeform heart matches the authored band")
        return 0
    print(f"\n{len(changes)} assets would change (run --write to author)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

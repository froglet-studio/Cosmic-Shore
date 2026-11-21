#!/usr/bin/env python3
"""Offline twin of FrogletTools > Vessels > Audit Vessel Skimmers.

WHY THIS EXISTS.

`VesselController` initializes ONLY the skimmers reachable through
`VesselStatus.NearFieldSkimmer` / `FarFieldSkimmer`, and `SkimmerImpactor` drops every
contact while `skimmer.IsInitialized` is false. A hull whose status points at a
different, inactive or half-wired skimmer therefore skims nothing, with no error
anywhere (the Dolphin shipped that way: Docs/.../DOLPHIN_ENERGY_ECONOMY.md section 5).
The editor audit reads the prefabs and says so; it needs an open editor, and QA's
2026-10-09 run of it (DT-002) came back as six hull names and no per-hull reason,
because the reasons sit in a console a session cannot read.

This script answers the same questions from the YAML alone, so a session can name
the fault per hull and prove a fix before anybody opens Unity. It follows the
reference through NESTED prefab instances (Rhino's skimmer is a variant of a variant
of Skimmer.prefab) using Unity's composition rule - a nested object's fileID in the
outer prefab is its source fileID XOR the PrefabInstance's fileID, masked to 63 bits -
and applies each instance level's m_Modifications on top of the base values, so an
override that nulls a container or deactivates a GameObject is seen where the
inspector would show it.

WHAT IT CHECKS, per hull prefab under Assets/_Prefabs/Spacevessels (any depth, as the editor's
FindAssets recurses; a hull is a prefab whose ROOT carries a VesselStatus, declared there or
inherited from the base hull a Prefab Variant is made from), mirroring
Assets/_Scripts/Editor/VesselSkimmerAudit.cs check for check. A prefab with no VesselStatus is
listed at the end as not audited, never silently dropped:
  - the slot is assigned (the far field is optional);
  - every GameObject from the skimmer up to the hull root is active;
  - the skimmer's GameObject carries a SkimmerImpactor whose `skimmer` reference is
    THIS skimmer, with an effect container that holds prism effects (a far field
    with none is a crystal catcher, reported as such, not a fault);
  - when the container asks for the forcefield crackle, a ForcefieldCrackleController
    with an overlay renderer is on the same GameObject;
  - an ImpactCollider, a Rigidbody and a trigger collider are present.

WHAT IT DOES NOT CHECK: runtime. A perfectly wired skimmer can still be dead if
`VesselController` never runs its initializer; that is the editor's half. And the
trigger check is the editor's heuristic, kept identical on purpose: it looks only at
the skimmer's own colliders, so the Rhino's sword (a non-trigger capsule, the base
trigger sphere removed by the instance) reads "no trigger collider" although prism
colliders are all triggers and the shell tier probes the capsule by name. Whether the
sphere's removal was deliberate is a human call recorded on DT-002 (Docs/QA/DEV_TASKS.md);
a hull exception here would hide a later real break on the same hull.

Validated 2026-10-10 against the editor audit's QA run of 2026-10-09 (DT-002): the same six
hulls, with the reason per hull the editor could only print to a console.

    python3 Tools/Build/audit_vessel_skimmers.py              # report every hull
    python3 Tools/Build/audit_vessel_skimmers.py --check      # exit 1 on a fault outside --allow
    python3 Tools/Build/audit_vessel_skimmers.py --self-test  # negative controls on synthetic prefabs

A READER tool: it never writes an asset.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
import tempfile
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HULL_FOLDER = "Assets/_Prefabs/Spacevessels"
MASK = (1 << 63) - 1

# Component classes the audit names, by script file name (resolved to guids at run time).
SCRIPTS = {
    "VesselStatus", "Skimmer", "SkimmerImpactor", "ImpactCollider", "ForcefieldCrackleController",
    "SkimmerForcefieldCracklePrismEffectSO",
}
COLLIDER_CLASSES = {64, 65, 135, 136}   # Mesh, Box, Sphere, Capsule
RIGIDBODY = 54
GAMEOBJECT = 1
TRANSFORM = 4
RECT_TRANSFORM = 224
MONO = 114
PREFAB_INSTANCE = 1001

DEFAULT_ALLOW = ()   # the Serpent was the one standing exception until 2026-10-31 (fix_serpent_crystal_pickup.py)


# ---------------------------------------------------------------- YAML -----

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)", re.S | re.M)
MOD_RE = re.compile(
    r"- target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}),\s*type: 3\}\s*"
    r"propertyPath: (\S*)\s*value: ?(.*?)\s*objectReference: (\{[^}]*\})", re.S)


class Prefab:
    def __init__(self, path: Path):
        self.path = path
        self.docs: dict[int, tuple[int, str, bool]] = {}
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in DOC_RE.finditer(text):
            self.docs[int(m.group(2))] = (int(m.group(1)), m.group(4), bool(m.group(3)))
        # PrefabInstance id -> (source guid, {target fid: {propertyPath: (value, objectReference)}})
        self.instances: dict[int, tuple[str, dict[int, dict[str, tuple[str, str]]]]] = {}
        self.removed: dict[int, set[int]] = {}
        for fid, (cls, body, _) in self.docs.items():
            if cls != PREFAB_INSTANCE:
                continue
            src = re.search(r"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32})", body)
            mods: dict[int, dict[str, tuple[str, str]]] = {}
            for t, _g, prop, val, ref in MOD_RE.findall(body):
                mods.setdefault(int(t), {})[prop] = (val.strip(), ref)
            self.instances[fid] = (src.group(1) if src else "", mods)
            # Components and GameObjects the instance REMOVED from its source. Unity keeps the
            # source's object and records the removal here, so a reader that ignores this list
            # sees a component the editor does not (the Manta removes the base impactor and
            # adds its own; the audit must see one impactor, as GetComponent does).
            removed: set[int] = set()
            for key in ("m_RemovedComponents", "m_RemovedGameObjects"):
                m = re.search(rf"{key}:\n((?:\s+- .*\n(?:\s+type: 3\}}\n)?)*)", body)
                if m:
                    removed.update(int(x) for x in re.findall(r"fileID: (-?\d+)", m.group(1)))
            self.removed[fid] = removed

    def body(self, fid: int) -> str:
        return self.docs[fid][1] if fid in self.docs else ""

    def cls(self, fid: int) -> int:
        return self.docs[fid][0] if fid in self.docs else 0

    def stripped(self, fid: int) -> bool:
        return fid in self.docs and self.docs[fid][2]


def field(body: str, name: str) -> str | None:
    m = re.search(rf"^\s*{re.escape(name)}: (.*)$", body, re.M)
    return m.group(1).strip() if m else None


def fileid(v: str | None) -> int:
    m = re.search(r"fileID: (-?\d+)", v or "")
    return int(m.group(1)) if m else 0


def guid_of(v: str | None) -> str | None:
    m = re.search(r"guid: ([0-9a-f]{32})", v or "")
    return m.group(1) if m else None


# --------------------------------------------------------------- index -----

class AssetIndex:
    """guid -> asset path, for the asset kinds the audit follows."""

    def __init__(self, root: Path):
        self.root = root
        self.by_guid: dict[str, Path] = {}
        self.script_guid: dict[str, str] = {}
        for dp, dns, fns in os.walk(root / "Assets"):
            dns[:] = [d for d in dns if d not in ("Plugins",)]
            for fn in fns:
                if not fn.endswith(".meta"):
                    continue
                if not fn.endswith((".prefab.meta", ".asset.meta", ".cs.meta")):
                    continue
                p = Path(dp) / fn
                try:
                    head = p.read_text(encoding="utf-8", errors="replace")[:400]
                except OSError:
                    continue
                m = re.search(r"^guid: ([0-9a-f]{32})", head, re.M)
                if not m:
                    continue
                self.by_guid[m.group(1)] = p.with_suffix("")
                if fn.endswith(".cs.meta"):
                    name = fn[:-8]
                    if name in SCRIPTS:
                        self.script_guid[name] = m.group(1)

    @lru_cache(maxsize=None)
    def prefab(self, guid: str) -> Prefab | None:
        p = self.by_guid.get(guid)
        return Prefab(p) if p and p.suffix == ".prefab" else None

    def script_name(self, g: str | None) -> str:
        for name, sg in self.script_guid.items():
            if sg == g:
                return name
        p = self.by_guid.get(g or "")
        return p.stem if p else (g or "?")


# ----------------------------------------------------------- composition ----

class Obj:
    """One object seen from the hull prefab: the chain of (prefab, fid) from the hull down
    to the prefab that really declares it, with every instance level's overrides."""

    def __init__(self, levels: list[tuple[Prefab, int, int]]):
        # levels[i] = (prefab, fid in that prefab, instance fid that contains it there, or 0)
        self.levels = levels

    @property
    def base(self) -> tuple[Prefab, int]:
        p, f, _ = self.levels[-1]
        return p, f

    @property
    def cls(self) -> int:
        p, f = self.base
        return p.cls(f)

    def value(self, prop: str) -> tuple[str | None, str | None]:
        """(value, objectReference) with the outermost override winning, else the base field."""
        for i in range(len(self.levels) - 1):
            outer, _fid, inst = self.levels[i]
            _src, mods = outer.instances[inst]
            inner_fid = self.levels[i + 1][1]
            if inner_fid in mods and prop in mods[inner_fid]:
                return mods[inner_fid][prop]
        p, f = self.base
        raw = field(p.body(f), prop)
        return raw, raw

    def ref_id_at_top(self, prop: str) -> int:
        """A same-prefab object reference, composed up to the hull's id space."""
        for i in range(len(self.levels) - 1):
            outer, _fid, inst = self.levels[i]
            _src, mods = outer.instances[inst]
            inner_fid = self.levels[i + 1][1]
            if inner_fid in mods and prop in mods[inner_fid]:
                fid = fileid(mods[inner_fid][prop][1])
                return self._compose(fid, i) if fid else 0
        p, f = self.base
        fid = fileid(field(p.body(f), prop))
        return self._compose(fid, len(self.levels) - 1) if fid else 0

    def _compose(self, fid: int, level: int) -> int:
        """Map a fid in levels[level]'s id space up to levels[0]'s."""
        for i in range(level - 1, -1, -1):
            fid = (fid ^ self.levels[i][2]) & MASK
        return fid


def _declaring_instance(index: AssetIndex, p: Prefab, f: int, depth: int = 0) -> int:
    """The PrefabInstance in `p` that `f` lives under, for an object Unity emitted NO
    stripped document for (it only writes one when something in the file references the
    object). Each instance is tried by composition: the source prefab must declare, or
    itself be able to resolve, `f ^ instance`."""
    if depth > 16:
        return 0
    for inst, (src_guid, _mods) in p.instances.items():
        src = index.prefab(src_guid)
        if src is None:
            continue
        s = (f ^ inst) & MASK
        if s in src.docs:
            if not src.stripped(s):
                return inst
            inner = fileid(field(src.body(s), "m_PrefabInstance"))
            if inner in src.instances:
                return inst
        elif _declaring_instance(index, src, s, depth + 1):
            return inst
    return 0


def resolve(index: AssetIndex, prefab: Prefab, fid: int) -> Obj | None:
    """Follow a (possibly stripped, possibly undeclared) fid down through nested instances
    to the prefab that really declares it."""
    levels: list[tuple[Prefab, int, int]] = []
    p, f = prefab, fid
    for _ in range(16):
        if f in p.docs and not p.stripped(f):
            levels.append((p, f, 0))
            return Obj(levels)
        if f in p.docs:
            inst = fileid(field(p.body(f), "m_PrefabInstance"))
        else:
            inst = _declaring_instance(index, p, f)
        if inst not in p.instances:
            return None
        levels.append((p, f, inst))
        src_guid, _ = p.instances[inst]
        src = index.prefab(src_guid)
        if src is None:
            return None
        p, f = src, (f ^ inst) & MASK
    return None


def removed_at_some_level(obj: Obj) -> bool:
    """True when an instance level between the hull and the declaring prefab removed this object."""
    for i in range(len(obj.levels) - 1):
        outer, _fid, inst = obj.levels[i]
        if obj.levels[i + 1][1] in outer.removed.get(inst, ()):
            return True
    return False


def sibling_components(index: AssetIndex, obj: Obj) -> list[Obj]:
    """Every component on the object's GameObject (see components_on)."""
    go = game_object(index, obj)
    return components_on(index, go) if go is not None else []


def components_on(index: AssetIndex, go: Obj) -> list[Obj]:
    """Every component on this GameObject as the hull sees it: the declaring prefab's own list,
    plus the ones ADDED at any outer level, minus the ones an outer level REMOVED.

    Every component is resolved FROM THE HULL, whatever level declares it, so each one carries
    the full chain of instance levels: the hull's overrides apply to it, the hull's removals
    are honoured, and its object references compose into the hull's id space. A component
    added on an intermediate prefab (the Rhino's ForceFieldSkimmer Variant adds two) used to be
    resolved from that prefab alone, which read its base values and compared its references
    in the wrong id space; the self-test plants both shapes."""
    hull = go.levels[0][0]
    out: list[Obj] = []
    base_p, base_f = go.base
    for comp in re.findall(r"- component: \{fileID: (-?\d+)\}", base_p.body(base_f)):
        r = resolve(index, hull, go._compose(int(comp), len(go.levels) - 1))
        if r is not None and not removed_at_some_level(r):
            out.append(r)
    # Components added on an outer prefab point their m_GameObject at the stripped GO there,
    # and their own fid is in THAT prefab's id space: compose it up to the hull's.
    for i in range(len(go.levels) - 1):
        outer, go_fid, _inst = go.levels[i]
        for fid, (cls, body, stripped) in outer.docs.items():
            if stripped or cls in (GAMEOBJECT, TRANSFORM, PREFAB_INSTANCE):
                continue
            if fileid(field(body, "m_GameObject")) == go_fid:
                r = resolve(index, hull, go._compose(fid, i))
                if r is not None and not removed_at_some_level(r):
                    out.append(r)
    return out


def root_game_object(index: AssetIndex, prefab: Prefab) -> Obj | None:
    """The prefab's root GameObject. In a Prefab Variant (or any prefab whose root is itself
    a nested instance) the root is the source's root seen through the one instance with no
    parent transform; otherwise it is the GameObject of the Transform with no father."""
    for fid, (cls, body, stripped) in prefab.docs.items():
        if cls in (TRANSFORM, RECT_TRANSFORM) and not stripped and not fileid(field(body, "m_Father")):
            return resolve(index, prefab, fileid(field(body, "m_GameObject")))
    for inst, (src_guid, _mods) in prefab.instances.items():
        if fileid(field(prefab.body(inst), "m_TransformParent")):
            continue
        src = index.prefab(src_guid)
        inner = root_game_object(index, src) if src is not None else None
        if inner is None:
            return None
        return resolve(index, prefab, (inner.levels[0][1] ^ inst) & MASK)
    return None


def vessel_status(index: AssetIndex, prefab: Prefab) -> Obj | None:
    """The VesselStatus on the prefab's ROOT, as the editor audit's root.TryGetComponent finds
    it: declared in the file, or inherited from a base hull this prefab is a variant of."""
    go = root_game_object(index, prefab)
    if go is None:
        return None
    status_guid = index.script_guid.get("VesselStatus")
    return next((c for c in components_on(index, go)
                 if c.cls == MONO and guid_of(c.value("m_Script")[0]) == status_guid), None)


def game_object(index: AssetIndex, comp: Obj) -> Obj | None:
    base_p, base_f = comp.base
    go_fid = fileid(field(base_p.body(base_f), "m_GameObject"))
    if not go_fid:
        return None
    top = comp._compose(go_fid, len(comp.levels) - 1)
    return resolve(index, comp.levels[0][0], top)


def transform_of(index: AssetIndex, go: Obj) -> Obj | None:
    base_p, base_f = go.base
    for comp in re.findall(r"- component: \{fileID: (-?\d+)\}", base_p.body(base_f)):
        c = int(comp)
        if base_p.cls(c) == TRANSFORM:
            top = go._compose(c, len(go.levels) - 1)
            return resolve(index, go.levels[0][0], top)
    return None


def ancestor_chain(index: AssetIndex, go: Obj) -> list[tuple[str, bool]]:
    """(name, active) from the object up to the hull root, crossing nested instances."""
    out: list[tuple[str, bool]] = []
    cur = go
    for _ in range(64):
        name = cur.value("m_Name")[0] or "?"
        active = (cur.value("m_IsActive")[0] or "1").strip() == "1"
        out.append((name, active))
        t = transform_of(index, cur)
        if t is None:
            return out
        father = t.ref_id_at_top("m_Father")
        if not father:
            # Root of its own prefab: the containing instance's parent transform, one level up.
            if len(t.levels) <= 1:
                return out
            # Find the deepest level whose base root this is: walk from the inside out.
            for i in range(len(t.levels) - 2, -1, -1):
                outer, _f, inst = t.levels[i]
                parent_t = fileid(field(outer.body(inst), "m_TransformParent"))
                if parent_t:
                    hull = t.levels[0][0]
                    top = parent_t
                    for j in range(i - 1, -1, -1):
                        top = (top ^ t.levels[j][2]) & MASK
                    pt = resolve(index, hull, top)
                    father = top if pt is not None else 0
                    break
            if not father:
                return out
        pt = resolve(index, go.levels[0][0], father)
        if pt is None:
            return out
        nxt = game_object(index, pt)
        if nxt is None:
            return out
        cur = nxt
    return out


# --------------------------------------------------------------- audit -----

def audit_slot(index: AssetIndex, hull: Prefab, hull_name: str, status: Obj, slot: str,
               optional: bool) -> tuple[int, str]:
    target = status.ref_id_at_top(slot)
    if not target:
        return 0, f"   {slot}: (none)" + ("" if optional else " - this vessel has no skimmer at all")
    skimmer = resolve(index, hull, target)
    if skimmer is None:
        return 1, f"   {slot}: *** reference {target} is not part of this vessel's hierarchy"
    skimmer_script = index.script_name(guid_of(skimmer.value("m_Script")[0]))
    go = game_object(index, skimmer)
    go_name = go.value("m_Name")[0] if go else "?"
    problems: list[str] = []
    crystal_only = False

    if go is not None:
        for name, active in ancestor_chain(index, go):
            if not active:
                problems.append(f"'{name}' is INACTIVE")

    comps = sibling_components(index, skimmer) if go is not None else []
    by_name: dict[str, list[Obj]] = {}
    for c in comps:
        n = index.script_name(guid_of(c.value("m_Script")[0])) if c.cls == MONO else f"class{c.cls}"
        by_name.setdefault(n, []).append(c)

    impactor = (by_name.get("SkimmerImpactor") or [None])[0]
    if impactor is None:
        problems.append("no SkimmerImpactor")
    else:
        if impactor.ref_id_at_top("skimmer") != target:
            problems.append("SkimmerImpactor.skimmer points at a different Skimmer")
        _v, ref = impactor.value("skimmerImpactorDataContainer")
        cguid = guid_of(ref)
        if not cguid or cguid not in index.by_guid:
            problems.append("SkimmerImpactor has no effect container")
        else:
            cpath = index.by_guid[cguid]
            ctext = cpath.read_text(encoding="utf-8", errors="replace")
            m = re.search(r"^  skimmerPrismEffectsSO:\n((?:  - .*\n)*)", ctext, re.M)
            effects = re.findall(r"guid: ([0-9a-f]{32})", m.group(1)) if m else []
            if not effects:
                if optional:
                    crystal_only = True
                else:
                    problems.append(f"container '{cpath.stem}' has no prism effects")
            else:
                wants_crackle = False
                crackle_guid = index.script_guid.get("SkimmerForcefieldCracklePrismEffectSO")
                for eg in effects:
                    ep = index.by_guid.get(eg)
                    if ep and ep.exists():
                        et = ep.read_text(encoding="utf-8", errors="replace")
                        if crackle_guid and guid_of(field(et, "m_Script")) == crackle_guid:
                            wants_crackle = True
                if wants_crackle:
                    crackle = (by_name.get("ForcefieldCrackleController") or [None])[0]
                    if crackle is None:
                        problems.append("container asks for the forcefield crackle but the skimmer has no "
                                        "ForcefieldCrackleController")
                    elif not crackle.ref_id_at_top("overlayRenderer"):
                        problems.append("ForcefieldCrackleController has no overlayRenderer (crackle draws nothing)")

    if "ImpactCollider" not in by_name:
        problems.append("no ImpactCollider (the other side cannot resolve this impactor)")
    if not any(c.cls == RIGIDBODY for c in comps):
        problems.append("no Rigidbody (trigger callbacks need one on at least one side)")
    if not any(c.cls in COLLIDER_CLASSES and (c.value("m_IsTrigger")[0] or "0").strip() == "1" for c in comps):
        problems.append("no trigger collider")

    if not problems:
        tail = " (crystal pickup only - no prism effects)" if crystal_only else ""
        return 0, f"   {slot}: '{go_name}' [{skimmer_script}] OK{tail}"
    return 1, f"   {slot}: '{go_name}' [{skimmer_script}] *** " + "; ".join(problems)


def audit(index: AssetIndex, hull_folder: Path) -> tuple[int, dict[str, int], list[str]]:
    """Every prefab under the hull folder, at any depth, as the editor's FindAssets does. A
    prefab with no VesselStatus on its root is not a hull (the Components/ parts); it is named
    after the hulls rather than dropped, so a hull the twin cannot read never just vanishes
    from the report."""
    lines: list[str] = []
    not_hulls: list[str] = []
    per_hull: dict[str, int] = {}
    total = 0
    for path in sorted(hull_folder.rglob("*.prefab")):
        name = path.relative_to(hull_folder).with_suffix("").as_posix()
        hull = Prefab(path)
        status = vessel_status(index, hull)
        if status is None:
            not_hulls.append(f"-- {name}: no VesselStatus found")
            continue
        lines.append(f"-- {name}")
        n1, l1 = audit_slot(index, hull, name, status, "_nearFieldSkimmer", optional=False)
        n2, l2 = audit_slot(index, hull, name, status, "_farFieldSkimmer", optional=True)
        lines += [l1, l2, ""]
        per_hull[name] = n1 + n2
        total += n1 + n2
    if not_hulls:
        lines += ["Not audited (no VesselStatus on the prefab root; the editor skips these too):"]
        lines += not_hulls + [""]
    return total, per_hull, lines


# ------------------------------------------------------------ self-test -----
#
# The fixtures are SYNTHETIC: small Unity-shaped prefabs and assets written into a temp project,
# with their own script guids. The self-test gates both static CI jobs, so it must not move when
# a hull is re-wired, renamed or flattened; an earlier version pinned the shipped Squirrel and
# read its skimmer one nested level deep, so an unrelated gameplay edit could turn CI red.

def _g(n: int) -> str:
    return f"{n:032x}"


SYN_SCRIPTS = {name: _g(0x5C0 + i) for i, name in enumerate(sorted(SCRIPTS | {
    "SkimmerImpactorDataContainerSO", "SomePrismEffectSO"}))}
SYN_GUID = {"skimmer": _g(0xA1), "bare": _g(0xA2), "variant": _g(0xA3), "alpha": _g(0xB1),
            "beta": _g(0xB2), "gamma": _g(0xB3), "delta": _g(0xB4), "loose": _g(0xB5),
            "effects": _g(0xC1), "empty": _g(0xC2), "crackle": _g(0xC3), "effect": _g(0xD1),
            "crackle_effect": _g(0xD2)}
YAML_HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


def _doc(cls: int, fid: int, kind: str, lines: list[str]) -> str:
    head = [f"--- !u!{cls} &{fid}", f"{kind}:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}"]
    return "\n".join(head + lines) + "\n"


def _go(fid: int, name: str, comps: list[int], active: int = 1) -> str:
    return _doc(GAMEOBJECT, fid, "GameObject", ["  serializedVersion: 6", "  m_Component:"]
                + [f"  - component: {{fileID: {c}}}" for c in comps]
                + ["  m_Layer: 0", f"  m_Name: {name}", "  m_TagString: Untagged", f"  m_IsActive: {active}"])


def _transform(fid: int, go: int, father: int) -> str:
    return _doc(TRANSFORM, fid, "Transform", [f"  m_GameObject: {{fileID: {go}}}", "  serializedVersion: 2",
                                              "  m_Children: []", f"  m_Father: {{fileID: {father}}}"])


def _mono(fid: int, go: int, script: str, fields: list[str]) -> str:
    return _doc(MONO, fid, "MonoBehaviour", [f"  m_GameObject: {{fileID: {go}}}", "  m_Enabled: 1",
                                             f"  m_Script: {{fileID: 11500000, guid: {SYN_SCRIPTS[script]}, type: 3}}",
                                             "  m_Name: ", "  m_EditorClassIdentifier: "] + fields)


def _physics(rb: int, col: int, go: int, trigger: int = 1) -> str:
    return (_doc(RIGIDBODY, rb, "Rigidbody", [f"  m_GameObject: {{fileID: {go}}}", "  m_IsKinematic: 1"])
            + _doc(135, col, "SphereCollider", [f"  m_GameObject: {{fileID: {go}}}", f"  m_IsTrigger: {trigger}"]))


def _stripped(cls: int, kind: str, fid: int, src_fid: int, src_guid: str, inst: int) -> str:
    return (f"--- !u!{cls} &{fid} stripped\n{kind}:\n"
            f"  m_CorrespondingSourceObject: {{fileID: {src_fid}, guid: {src_guid}, type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


def _instance(inst: int, parent: int, src_guid: str, mods: list[tuple[int, str, str, str]],
              removed: list[int]) -> str:
    """A PrefabInstance; mods are (source fid, propertyPath, value, objectReference), written in
    Unity's wrapped form; removed are source component fids."""
    out = [f"--- !u!{PREFAB_INSTANCE} &{inst}", "PrefabInstance:", "  m_ObjectHideFlags: 0",
           "  serializedVersion: 2", "  m_Modification:", "    serializedVersion: 3",
           f"    m_TransformParent: {{fileID: {parent}}}"]
    out += ["    m_Modifications:"] if mods else ["    m_Modifications: []"]
    for t, prop, val, ref in mods:
        out += [f"    - target: {{fileID: {t}, guid: {src_guid},", "        type: 3}",
                f"      propertyPath: {prop}", f"      value: {val}", f"      objectReference: {ref}"]
    out += ["    m_RemovedComponents:"] if removed else ["    m_RemovedComponents: []"]
    out += [f"    - {{fileID: {r}, guid: {src_guid}, type: 3}}" for r in removed]
    out += ["    m_RemovedGameObjects: []", "    m_AddedGameObjects: []", "    m_AddedComponents: []",
            f"  m_SourcePrefab: {{fileID: 100100000, guid: {src_guid}, type: 3}}"]
    return "\n".join(out) + "\n"


def _container(script: str, effects: list[str]) -> str:
    body = _doc(MONO, 11400000, "MonoBehaviour", [
        "  m_GameObject: {fileID: 0}", "  m_Enabled: 1",
        f"  m_Script: {{fileID: 11500000, guid: {SYN_SCRIPTS[script]}, type: 3}}", "  m_Name: Synthetic"])
    if script == "SkimmerImpactorDataContainerSO":
        body += ("  skimmerPrismEffectsSO:\n" + "".join(f"  - {{fileID: 11400000, guid: {e}, type: 2}}\n"
                                                     for e in effects)) if effects else "  skimmerPrismEffectsSO: []\n"
    return YAML_HEAD + body


def _asset_ref(key: str) -> str:
    return f"{{fileID: 11400000, guid: {SYN_GUID[key]}, type: 2}}"


# fids of the synthetic parts, and of the instances that nest them
SK_GO, SK_T, SK_SKIM, SK_IMP, SK_IC, SK_RB, SK_COL = 100, 101, 102, 103, 104, 105, 106
BARE_GO, BARE_T, BARE_SKIM, BARE_IC, BARE_RB, BARE_COL = 300, 301, 302, 304, 305, 306
VAR_INST, VAR_IMP = 7000, 400        # ImpactorVariant: an instance of BareSkimmer + an ADDED impactor
HULL_GO, HULL_T, HULL_STATUS = 1, 2, 3
ALPHA_INST, BETA_INST, GAMMA_INST = 5000, 6000, 8000


def _hull(name: str, src: str, inst: int, skimmer_fid_in_src: int, near: bool = True,
          mods: list | None = None, removed: list | None = None) -> str:
    """A hull: root GameObject + VesselStatus, its near field a stripped Skimmer of a nested
    instance of `src`."""
    top = (skimmer_fid_in_src ^ inst) & MASK
    return (YAML_HEAD
            + _go(HULL_GO, name, [HULL_T, HULL_STATUS])
            + _transform(HULL_T, HULL_GO, 0)
            + _mono(HULL_STATUS, HULL_GO, "VesselStatus",
                    [f"  _nearFieldSkimmer: {{fileID: {top if near else 0}}}", "  _farFieldSkimmer: {fileID: 0}"])
            + _instance(inst, HULL_T, SYN_GUID[src], mods or [], removed or [])
            + _stripped(MONO, "MonoBehaviour", top, skimmer_fid_in_src, SYN_GUID[src], inst))


def _write(root: Path, rel: str, text: str, guid: str) -> None:
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    Path(str(path) + ".meta").write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8")


def _synthetic_project(root: Path, alpha: dict | None = None, beta: dict | None = None,
                       gamma_mods: list | None = None) -> Path:
    """Write the synthetic project; return its hull folder. Every hull passes by default:

      Alpha        a skimmer nested one instance deep (Skimmer.prefab), the plain case
      Beta         nests ImpactorVariant, which ADDS the SkimmerImpactor on top of BareSkimmer:
                   a component declared one level below the hull, in that prefab's id space
      Gamma        a Prefab Variant of Alpha: its VesselStatus lives in the base hull
      Sub/Delta    a byte copy of Alpha in a subfolder
      Loose        a prefab with no VesselStatus at the top of the hull folder (not a hull)
    """
    for name, guid in SYN_SCRIPTS.items():
        _write(root, f"Assets/Scripts/{name}.cs", "", guid)
    _write(root, "Assets/SO/Effect.asset", _container("SomePrismEffectSO", []), SYN_GUID["effect"])
    _write(root, "Assets/SO/CrackleEffect.asset", _container("SkimmerForcefieldCracklePrismEffectSO", []),
           SYN_GUID["crackle_effect"])
    _write(root, "Assets/SO/Effects.asset", _container("SkimmerImpactorDataContainerSO", [SYN_GUID["effect"]]),
           SYN_GUID["effects"])
    _write(root, "Assets/SO/Empty.asset", _container("SkimmerImpactorDataContainerSO", []), SYN_GUID["empty"])
    _write(root, "Assets/SO/Crackle.asset", _container("SkimmerImpactorDataContainerSO",
                                                       [SYN_GUID["crackle_effect"]]), SYN_GUID["crackle"])
    _write(root, "Assets/Parts/Skimmer.prefab", YAML_HEAD
           + _go(SK_GO, "Skimmer", [SK_T, SK_SKIM, SK_IMP, SK_IC, SK_RB, SK_COL])
           + _transform(SK_T, SK_GO, 0)
           + _mono(SK_SKIM, SK_GO, "Skimmer", [])
           + _mono(SK_IMP, SK_GO, "SkimmerImpactor", [f"  skimmer: {{fileID: {SK_SKIM}}}",
                                                       f"  skimmerImpactorDataContainer: {_asset_ref('effects')}"])
           + _mono(SK_IC, SK_GO, "ImpactCollider", [])
           + _physics(SK_RB, SK_COL, SK_GO), SYN_GUID["skimmer"])
    bare = (YAML_HEAD + _go(BARE_GO, "BareSkimmer", [BARE_T, BARE_SKIM, BARE_IC, BARE_RB, BARE_COL])
            + _transform(BARE_T, BARE_GO, 0) + _mono(BARE_SKIM, BARE_GO, "Skimmer", [])
            + _mono(BARE_IC, BARE_GO, "ImpactCollider", []) + _physics(BARE_RB, BARE_COL, BARE_GO))
    _write(root, "Assets/Parts/BareSkimmer.prefab", bare, SYN_GUID["bare"])
    v_go, v_skim = (BARE_GO ^ VAR_INST) & MASK, (BARE_SKIM ^ VAR_INST) & MASK
    _write(root, "Assets/Parts/ImpactorVariant.prefab", YAML_HEAD
           + _instance(VAR_INST, 0, SYN_GUID["bare"], [(BARE_GO, "m_Name", "ImpactorVariant", "{fileID: 0}")], [])
           + _stripped(GAMEOBJECT, "GameObject", v_go, BARE_GO, SYN_GUID["bare"], VAR_INST)
           + _stripped(MONO, "MonoBehaviour", v_skim, BARE_SKIM, SYN_GUID["bare"], VAR_INST)
           + _mono(VAR_IMP, v_go, "SkimmerImpactor", [f"  skimmer: {{fileID: {v_skim}}}",
                                                      f"  skimmerImpactorDataContainer: {_asset_ref('effects')}"]),
           SYN_GUID["variant"])
    folder = root / "Assets/Spacevessels"
    alpha_text = _hull("Alpha", "skimmer", ALPHA_INST, SK_SKIM, **(alpha or {}))
    _write(root, "Assets/Spacevessels/Alpha.prefab", alpha_text, SYN_GUID["alpha"])
    _write(root, "Assets/Spacevessels/Beta.prefab", _hull("Beta", "variant", BETA_INST, v_skim, **(beta or {})),
           SYN_GUID["beta"])
    _write(root, "Assets/Spacevessels/Gamma.prefab", YAML_HEAD
           + _instance(GAMMA_INST, 0, SYN_GUID["alpha"],
                       [(HULL_GO, "m_Name", "Gamma", "{fileID: 0}")] + (gamma_mods or []), []),
           SYN_GUID["gamma"])
    _write(root, "Assets/Spacevessels/Sub/Delta.prefab", alpha_text.replace("m_Name: Alpha", "m_Name: Delta"),
           SYN_GUID["delta"])
    _write(root, "Assets/Spacevessels/Loose.prefab", bare, SYN_GUID["loose"])
    return folder


def self_test() -> int:
    """Negative controls on synthetic hulls: the audit must pass the clean ones and see each
    planted fault, including the shapes the editor audit sees and a naive YAML reader does not
    (a component added on an intermediate prefab, a hull authored as a Prefab Variant, a hull
    in a subfolder)."""
    ok = True
    total, _, _ = audit(AssetIndex(ROOT), ROOT / HULL_FOLDER)
    print(f"  live tree: {total} fault(s) across the fleet (informational, not asserted)")

    def run(**kw) -> tuple[int, dict[str, int], list[str]]:
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            folder = _synthetic_project(root, **kw)
            return audit(AssetIndex(root), folder)

    def slot(lines: list[str], hull: str) -> str:
        i = lines.index(f"-- {hull}") if f"-- {hull}" in lines else -1
        return lines[i + 1] if i >= 0 else f"(no '-- {hull}' block)"

    t, per, lines = run()
    ok &= _expect("every clean synthetic hull passes",
                  t == 0 and per == {"Alpha": 0, "Beta": 0, "Gamma": 0, "Sub/Delta": 0})
    ok &= _expect("an impactor ADDED on an intermediate prefab is read in the hull's id space",
                  slot(lines, "Beta").endswith("OK"))
    ok &= _expect("a hull authored as a Prefab Variant is audited through its base's VesselStatus",
                  slot(lines, "Gamma").endswith("OK"))
    ok &= _expect("a hull in a subfolder is audited", slot(lines, "Sub/Delta").endswith("OK"))
    ok &= _expect("a prefab with no VesselStatus is named, not dropped",
                  "-- Loose: no VesselStatus found" in lines)

    _t, per, lines = run(alpha={"near": False})
    ok &= _expect("an unassigned near field is reported as none, not a fault",
                  per.get("Alpha") == 0 and "no skimmer at all" in slot(lines, "Alpha"))

    _t, per, lines = run(alpha={"mods": [(SK_GO, "m_IsActive", "0", "{fileID: 0}")]})
    ok &= _expect("an m_IsActive=0 override on the nested skimmer is a fault",
                  per.get("Alpha") == 1 and "INACTIVE" in slot(lines, "Alpha"))

    _t, per, lines = run(alpha={"mods": [(SK_IMP, "skimmerImpactorDataContainer", "", "{fileID: 0}")]})
    ok &= _expect("a nulled container override on the nested impactor is a fault",
                  per.get("Alpha") == 1 and "no effect container" in slot(lines, "Alpha"))

    _t, per, lines = run(alpha={"mods": [(SK_IMP, "skimmerImpactorDataContainer", "", _asset_ref("empty"))]})
    ok &= _expect("a near field whose container holds no prism effects is a fault",
                  per.get("Alpha") == 1 and "has no prism effects" in slot(lines, "Alpha"))

    _t, per, lines = run(alpha={"mods": [(SK_IMP, "skimmerImpactorDataContainer", "", _asset_ref("crackle"))]})
    ok &= _expect("a crackle container with no ForcefieldCrackleController is a fault",
                  per.get("Alpha") == 1 and "ForcefieldCrackleController" in slot(lines, "Alpha"))

    _t, per, lines = run(alpha={"removed": [SK_IMP]})
    ok &= _expect("an impactor the hull's instance REMOVES is gone",
                  per.get("Alpha") == 1 and "no SkimmerImpactor" in slot(lines, "Alpha"))

    _t, per, lines = run(alpha={"removed": [SK_COL]})
    ok &= _expect("a removed trigger collider is a fault",
                  per.get("Alpha") == 1 and "no trigger collider" in slot(lines, "Alpha"))

    _t, per, lines = run(beta={"mods": [(VAR_IMP, "skimmerImpactorDataContainer", "", "{fileID: 0}")]})
    ok &= _expect("the hull's override on an impactor added one level down applies",
                  per.get("Beta") == 1 and "no effect container" in slot(lines, "Beta"))

    _t, per, lines = run(beta={"removed": [VAR_IMP]})
    ok &= _expect("the hull's removal of an impactor added one level down is honoured",
                  per.get("Beta") == 1 and "no SkimmerImpactor" in slot(lines, "Beta"))

    _t, per, lines = run(gamma_mods=[(HULL_STATUS, "_nearFieldSkimmer", "", "{fileID: 0}")])
    ok &= _expect("a variant hull's override on the inherited VesselStatus applies",
                  per.get("Gamma") == 0 and "no skimmer at all" in slot(lines, "Gamma"))
    print("self-test:", "OK" if ok else "FAILED")
    return 0 if ok else 1


def _expect(label: str, cond: bool) -> bool:
    print(f"  {'ok  ' if cond else 'FAIL'} {label}")
    return cond


# ---------------------------------------------------------------- main -----

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 when a hull outside --allow has a fault")
    ap.add_argument("--allow", action="append", default=None, metavar="HULL",
                    help=f"hull whose faults are a standing exception (default: {', '.join(DEFAULT_ALLOW)})")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    index = AssetIndex(ROOT)
    total, per_hull, lines = audit(index, ROOT / HULL_FOLDER)
    print("Vessel skimmer audit (offline) - VesselStatus.NearFieldSkimmer / FarFieldSkimmer")
    print()
    print("\n".join(lines))
    allow = set(args.allow if args.allow is not None else DEFAULT_ALLOW)
    blocking = {h: n for h, n in per_hull.items() if n and h not in allow}
    print(f"{total} fault(s)" + (f"; standing exceptions: {', '.join(sorted(allow))}" if allow else ""))
    if args.check and blocking:
        print(f"FAIL: {len(blocking)} hull(s) outside the exception list do not skim: {', '.join(sorted(blocking))}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

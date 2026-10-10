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

WHAT IT CHECKS, per hull prefab under Assets/_Prefabs/Spacevessels, mirroring
Assets/_Scripts/Editor/VesselSkimmerAudit.cs check for check:
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
MONO = 114
PREFAB_INSTANCE = 1001

DEFAULT_ALLOW = ("Serpent",)   # QA-AUDIT-TOOLS' standing exception (QA-P2-SERPENT-SKIMMER)


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
    """Every component on the object's GameObject, including ones ADDED at an outer level and
    excluding ones an outer level REMOVED."""
    go = game_object(index, obj)
    if go is None:
        return []
    out: list[Obj] = []
    base_p, base_f = go.base
    for comp in re.findall(r"- component: \{fileID: (-?\d+)\}", base_p.body(base_f)):
        c = int(comp)
        top = go._compose(c, len(go.levels) - 1)
        r = resolve(index, go.levels[0][0], top) if len(go.levels) > 1 else resolve(index, base_p, c)
        if r is not None and not removed_at_some_level(r):
            out.append(r)
    # Components added on an outer prefab point their m_GameObject at the stripped GO there.
    for i in range(len(go.levels) - 1):
        outer, go_fid, _inst = go.levels[i]
        for fid, (cls, body, stripped) in outer.docs.items():
            if stripped or cls in (GAMEOBJECT, TRANSFORM, PREFAB_INSTANCE):
                continue
            if fileid(field(body, "m_GameObject")) == go_fid:
                r = resolve(index, go.levels[0][0], fid) if i == 0 else resolve(index, outer, fid)
                if r is not None:
                    out.append(r)
    return out


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
    lines: list[str] = []
    per_hull: dict[str, int] = {}
    total = 0
    status_guid = index.script_guid.get("VesselStatus")
    for path in sorted(hull_folder.glob("*.prefab")):
        hull = Prefab(path)
        status_fid = next((f for f, (c, b, s) in hull.docs.items()
                           if c == MONO and not s and guid_of(field(b, "m_Script")) == status_guid), 0)
        if not status_fid:
            continue
        status = resolve(index, hull, status_fid)
        lines.append(f"-- {path.stem}")
        n1, l1 = audit_slot(index, hull, path.stem, status, "_nearFieldSkimmer", optional=False)
        n2, l2 = audit_slot(index, hull, path.stem, status, "_farFieldSkimmer", optional=True)
        lines += [l1, l2, ""]
        per_hull[path.stem] = n1 + n2
        total += n1 + n2
    return total, per_hull, lines


# ------------------------------------------------------------ self-test -----

def self_test() -> int:
    """Negative controls on a synthetic hull: the audit must see each planted fault."""
    import shutil
    ok = True
    src = ROOT / HULL_FOLDER / "Squirrel.prefab"
    index = AssetIndex(ROOT)
    base_total, _, _ = audit(index, ROOT / HULL_FOLDER)
    print(f"  live tree: {base_total} fault(s) across the fleet (informational)")

    with tempfile.TemporaryDirectory() as td:
        folder = Path(td) / "Spacevessels"
        folder.mkdir()
        shutil.copy(src, folder / "Squirrel.prefab")
        t0, per, _ = audit(index, folder)
        ok &= _expect("the shipped Squirrel passes", per.get("Squirrel") == 0)

        # Null the near-field reference: a hull with no skimmer is reported, not faulted.
        text = src.read_text(encoding="utf-8")
        nulled = re.sub(r"_nearFieldSkimmer: \{fileID: -?\d+\}", "_nearFieldSkimmer: {fileID: 0}", text)
        (folder / "Squirrel.prefab").write_text(nulled, encoding="utf-8")
        _t, per, lines = audit(index, folder)
        ok &= _expect("an unassigned near field is reported as none, not a fault",
                      per.get("Squirrel") == 0 and any("no skimmer at all" in l for l in lines))

        # Deactivate the nested skimmer's GameObject through an instance override, planted
        # inside THAT instance's own m_Modifications block (the hull has several instances).
        m = re.search(r"_nearFieldSkimmer: \{fileID: (-?\d+)\}", text)
        hull = Prefab(src)
        sk = resolve(index, hull, int(m.group(1)))
        go = game_object(index, sk)
        outer, _f, inst = go.levels[0]
        inner_fid = go.levels[1][1]
        src_guid, _ = outer.instances[inst]

        def plant(target: int, prop: str, value: str, ref: str) -> str:
            """Set one override on the skimmer's instance: replace the existing entry for
            (target, prop) when the shipped file already carries one (one property has one
            override per instance), else insert a fresh one at the top of the list."""
            head = f"--- !u!1001 &{inst}\n"
            i = text.index(head)
            end = text.find("\n--- !u!", i + 1)
            block = text[i:end if end > 0 else len(text)]
            entry = re.compile(
                rf"    - target: \{{fileID: {target}, guid: {src_guid},\s*type: 3\}}\n"
                rf"      propertyPath: {re.escape(prop)}\n      value: ?.*?\n      objectReference: \{{[^}}]*\}}\n", re.S)
            fresh = (f"    - target: {{fileID: {target}, guid: {src_guid}, type: 3}}\n"
                     f"      propertyPath: {prop}\n      value: {value}\n      objectReference: {ref}\n")
            if entry.search(block):
                block = entry.sub(fresh, block, count=1)
            else:
                k = block.index("    m_Modifications:\n") + len("    m_Modifications:\n")
                block = block[:k] + fresh + block[k:]
            return text[:i] + block + text[end if end > 0 else len(text):]

        (folder / "Squirrel.prefab").write_text(plant(inner_fid, "m_IsActive", "0", "{fileID: 0}"), encoding="utf-8")
        _t, per, lines = audit(index, folder)
        ok &= _expect("an m_IsActive=0 override on the nested skimmer is a fault",
                      per.get("Squirrel") == 1 and any("INACTIVE" in l for l in lines))

        # Null the container through an override on the nested impactor.
        comps = sibling_components(index, sk)
        imp = next(c for c in comps if index.script_name(guid_of(c.value("m_Script")[0])) == "SkimmerImpactor")
        imp_inner = imp.levels[1][1]
        # The shipped Squirrel already overrides the container on this impactor; a planted
        # override earlier in the list wins in this reader (outermost-first, first match), which
        # is also the editor's reading: one property has one override per instance.
        (folder / "Squirrel.prefab").write_text(
            plant(imp_inner, "skimmerImpactorDataContainer", "", "{fileID: 0}"), encoding="utf-8")
        _t, per, lines = audit(index, folder)
        ok &= _expect("a nulled container override on the nested impactor is a fault",
                      per.get("Squirrel") == 1 and any("no effect container" in l for l in lines))
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

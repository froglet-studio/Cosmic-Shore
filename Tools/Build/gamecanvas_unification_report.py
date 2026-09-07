#!/usr/bin/env python3
"""
GameCanvas unification — the read-only report, and the post-migration gate.

Two GameCanvas prefabs exist (``CORE/GameCanvas.prefab`` and the hard-copied
``GameCanvas-SkimRace.prefab``) and the fork's scenes each carry ~1,770 unapplied overrides
plus a set of STRUCTURAL edits (removed objects, removed components, scene-added components)
that the earlier audits never counted. This script measures all of it straight out of the
YAML, without Unity, so the numbers in ``Docs/GAMECANVAS.md`` can be re-derived on any tree:

    python3 Tools/Build/gamecanvas_unification_report.py            # the report
    python3 Tools/Build/gamecanvas_unification_report.py --check    # the gate (exit 1 until done)
    python3 Tools/Build/gamecanvas_unification_report.py --json out.json

What ``--check`` asserts is the END state the in-editor unifier (FrogletTools > Game Modes >
GameCanvas Unifier) drives every scene toward:

  * the fork prefab no longer exists and no scene or prefab references its guid;
  * every gameplay scene's canvas instance is on ``CORE/GameCanvas.prefab``;
  * no migrated scene carries structural edits on that instance (no removed objects or
    components, no scene-added components duplicating a prefab one), and no property
    override outside the small allow-list below.

Parsing notes (each cost a pass in an earlier session):
  * ``- target:`` wraps across two lines in scene YAML, so every flow map is unwrapped before
    parsing; a one-line regex reports a 1,774-override instance as clean.
  * Overrides against objects INSIDE a nested prefab are addressed with the OUTER prefab's guid
    and a synthesised fileID that appears nowhere in the outer prefab's YAML. They resolve to
    ``[nested]`` here — 1,390 of the 1,771 overrides in a fork scene are of that kind (the
    GameOverPanel's layout), which is why "just look at the prefab" never found the wall.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CORE_PATH = "Assets/_Prefabs/CORE/GameCanvas.prefab"
FORK_PATH = "Assets/_Prefabs/GameCanvas-SkimRace.prefab"
CORE_GUID = "65bf1ed35b752374ca46ae214710e41c"
FORK_GUID = "abd30ad4cfca9ae4a8aecfde9f650cf3"

# The scenes the unifier migrates: every scene that was on the fork when this was written.
# A scene that joins later is picked up by the guid scan; this list only scopes the gate.
MIGRATED_SCENES = [
    "MinigameAstroLeague", "MinigameBends", "MinigameBroodRush", "MinigameDogFight",
    "MinigameDrumfire", "MinigameHijack", "MinigameJoust_Gameplay", "MinigamePeelTheCage",
    "MinigameRampage", "MinigameSalvo", "MinigameScarabScramble",
    "MinigameScurryMultiplayer_Gameplay", "MinigameSkimRace", "MinigameSwitchback",
    "MinigameWildlifeLiberation",
]

# Overrides the gate tolerates on a migrated scene's canvas instance. Root placement and
# the instance name are "default overrides" Unity always records; everything else must be
# in the prefab or in config. (statsToTrack moved to Resources/GameModeStatsProfile.)
ALLOWED_OVERRIDE_PREFIXES = (
    "m_Name", "m_RootOrder", "m_LocalPosition", "m_LocalRotation", "m_LocalEulerAnglesHint",
    "m_LocalScale", "m_ConstrainProportionsScale",
)

HDR = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", re.M)
REF = re.compile(r"\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?(?:,\s*type:\s*\d+)?\}")
LAYOUT = re.compile(r"^(m_AnchoredPosition|m_SizeDelta|m_AnchorMin|m_AnchorMax|m_Pivot|m_LocalPosition|"
                    r"m_LocalRotation|m_LocalScale|m_LocalEulerAnglesHint|m_OffsetMin|m_OffsetMax|"
                    r"m_RootOrder|m_ConstrainProportionsScale)")

KNOWN_SCRIPTS = {
    "fe87c0e1cc204ed48ad3b37840f39efc": "Image", "f4688fdb7df04437aeb418b961361dc5": "TextMeshProUGUI",
    "4e29b1a8efbd4b44bb3f3716e73f07ff": "Button", "30649d3a9faa99c48a7b1166b86bf2a0": "HorizontalLayoutGroup",
    "0cd44c1031e13a943bb63640046fad76": "CanvasScaler", "dc42784cf147c0c48a680349fa168899": "GraphicRaycaster",
    "59f8146938fff824cb5fd77236b75775": "VerticalLayoutGroup", "8a8695521f0d02e499659fee002a26c2": "GridLayoutGroup",
    "3245ec927659c4140ac4f8d17403cc18": "LayoutElement", "306cc8c2b49d7114eaa3623786fc2126": "Toggle",
    "1aa08ab6e0800fa44ae55d278d1423e3": "Slider", "31a19414c41e5ae4aae2af33fee712f6": "RectMask2D",
    "3312d7739989d2b4e91e6319e9a96d76": "ContentSizeFitter",
}


# ── YAML primitives ───────────────────────────────────────────────────────────

def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as f:
        return f.read()


def unwrap(s: str) -> str:
    """Collapse newlines inside {...} so a flow map is always one line."""
    out, depth, i, n = [], 0, 0, len(s)
    while i < n:
        c = s[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth = max(0, depth - 1)
        if c == "\n" and depth > 0:
            out.append(" ")
            i += 1
            while i < n and s[i] in " \t":
                i += 1
            continue
        if c != "\r":
            out.append(c)
        i += 1
    return "".join(out)


class Doc:
    __slots__ = ("cls", "fid", "stripped", "type", "body")

    def __init__(self, cls, fid, stripped, body):
        self.cls, self.fid, self.stripped = int(cls), int(fid), bool(stripped)
        nl = body.find("\n")
        self.type = body[:nl].rstrip(":").strip()
        self.body = body[nl + 1:]


def parse_docs(text: str) -> dict[int, Doc]:
    text = unwrap(text)
    ms = list(HDR.finditer(text))
    docs = {}
    for i, m in enumerate(ms):
        end = ms[i + 1].start() if i + 1 < len(ms) else len(text)
        d = Doc(m.group(1), m.group(2), m.group(3), text[m.end():end].lstrip("\n"))
        docs[d.fid] = d
    return docs


def field(d: Doc, name: str):
    m = re.search(r"^\s*" + re.escape(name) + r":\s*(.*)$", d.body, re.M)
    return m.group(1).strip() if m else None


def ref(d: Doc, name: str):
    v = field(d, name)
    if v is None:
        return None
    m = REF.search(v)
    return (int(m.group(1)), m.group(2)) if m else None


def list_refs(d: Doc, name: str):
    m = re.search(r"^(\s*)" + re.escape(name) + r":\s*(.*)$", d.body, re.M)
    if not m:
        return []
    indent, out = len(m.group(1)), []
    for line in d.body[m.end():].split("\n"):
        if not line.strip():
            continue
        ind = len(line) - len(line.lstrip())
        if ind < indent or (ind == indent and not line.lstrip().startswith("-")):
            break
        r = REF.search(line)
        if line.lstrip().startswith("-") and r:
            out.append((int(r.group(1)), r.group(2)))
    return out


_scripts: dict[str, str] | None = None


def script_names() -> dict[str, str]:
    global _scripts
    if _scripts is None:
        _scripts = dict(KNOWN_SCRIPTS)
        for dp, _, fns in os.walk(os.path.join(ROOT, "Assets")):
            for fn in fns:
                if fn.endswith(".cs.meta"):
                    m = re.search(r"^guid: ([0-9a-f]{32})", open(os.path.join(dp, fn)).read(), re.M)
                    if m:
                        _scripts[m.group(1)] = fn[:-8]
    return _scripts


def comp_name(d: Doc) -> str:
    if d.cls == 114:
        r = ref(d, "m_Script")
        if r and r[1]:
            return script_names().get(r[1], "Script:" + r[1][:8])
        return "MonoBehaviour"
    return d.type


# ── Prefab model ──────────────────────────────────────────────────────────────

class Prefab:
    def __init__(self, rel: str):
        self.path = rel
        self.docs = parse_docs(read(rel))
        self.go: dict[int, dict] = {}
        self.tf2go: dict[int, int] = {}
        self.pseudo: dict[int, dict] = {}   # stripped transforms = roots of nested instances
        for d in self.docs.values():
            if d.cls == 1 and not d.stripped:
                self.go[d.fid] = dict(name=field(d, "m_Name"),
                                      comps=[c for c, _ in list_refs(d, "m_Component")],
                                      tf=None, parent=None, path=None)
        for d in self.docs.values():
            if d.cls in (4, 224) and not d.stripped:
                g = ref(d, "m_GameObject")
                if g and g[0] in self.go:
                    self.go[g[0]]["tf"] = d.fid
                    self.tf2go[d.fid] = g[0]
            elif d.cls in (4, 224) and d.stripped:
                pi = ref(d, "m_PrefabInstance")
                if pi and pi[0] in self.docs:
                    inst = self.docs[pi[0]]
                    tp = ref(inst, "m_TransformParent")
                    mm = re.search(r"propertyPath: m_Name\s+value: (.*?)\s+objectReference", inst.body)
                    self.pseudo[d.fid] = dict(name=(mm.group(1).strip() if mm else "?nested"),
                                              parent_tf=tp[0] if tp else None, inst=pi[0])
        for gid, g in self.go.items():
            if g["tf"] is None:
                continue
            f = ref(self.docs[g["tf"]], "m_Father")
            if not f:
                continue
            if f[0] in self.tf2go:
                g["parent"] = self.tf2go[f[0]]
            elif f[0] in self.pseudo:
                g["parent"] = ("nested", f[0])

        def ppath(tfid):
            ps = self.pseudo[tfid]
            par = ps["parent_tf"]
            if par in self.tf2go:
                return pth(self.tf2go[par]) + "/" + ps["name"]
            if par in self.pseudo:
                return ppath(par) + "/" + ps["name"]
            return ps["name"]

        def pth(gid):
            g = self.go[gid]
            if g["path"] is None:
                par = g["parent"]
                pre = "" if par is None else (ppath(par[1]) + "/" if isinstance(par, tuple) else pth(par) + "/")
                g["path"] = pre + (g["name"] or "?")
            return g["path"]

        for gid in self.go:
            pth(gid)

    def norm_path(self, gid: int) -> str:
        p = self.go[gid]["path"]
        i = p.find("/")
        return p[i + 1:] if i >= 0 else ""

    def comps_of(self, gid: int):
        return [(c, comp_name(self.docs[c])) for c in self.go[gid]["comps"] if c in self.docs]

    def owner(self, fid: int):
        """(path, 'GameObject' | 'Type#k') for a plain object of this prefab, else (None, None)."""
        if fid in self.go:
            return self.norm_path(fid), "GameObject"
        d = self.docs.get(fid)
        if d is None or d.stripped:
            return None, None
        g = ref(d, "m_GameObject")
        if g and g[0] in self.go:
            comps = self.comps_of(g[0])
            n = comp_name(d)
            same = [c for c, nn in comps if nn == n]
            return self.norm_path(g[0]), f"{n}#{same.index(fid) if fid in same else 0}"
        return None, None


# ── Scene model ───────────────────────────────────────────────────────────────

class Override:
    __slots__ = ("tfid", "tguid", "prop", "value", "oref")

    def __init__(self):
        self.tfid = self.tguid = self.prop = self.value = self.oref = None

    @property
    def key(self):
        return f"{self.tguid}|{self.tfid}|{self.prop}"

    @property
    def valuekey(self):
        if self.oref and self.oref[0] != 0:
            return f"asset:{self.oref[1]}:{self.oref[0]}" if self.oref[1] else "sceneref"
        return f"v:{self.value}"


def unquote(v: str) -> str:
    if len(v) >= 2 and ((v[0] == "'" and v[-1] == "'") or (v[0] == '"' and v[-1] == '"')):
        return v[1:-1].replace("''", "'")
    return v


def parse_instance(d: Doc):
    ovs, cur, section = [], None, None
    removed_go, removed_comp, added = [], [], []
    for raw in d.body.split("\n"):
        t = raw.strip()
        if not t:
            continue
        if t == "m_Modifications:":
            section, cur = "mods", None
            continue
        for tag, sec in (("m_RemovedComponents:", "rc"), ("m_RemovedGameObjects:", "rg"),
                         ("m_AddedGameObjects:", "ag"), ("m_AddedComponents:", "ac"), ("m_SourcePrefab:", None)):
            if t.startswith(tag):
                section, cur = sec, None
                break
        else:
            if section == "mods":
                if t.startswith("- target:"):
                    if cur and cur.prop is not None:
                        ovs.append(cur)
                    cur = Override()
                    r = REF.search(t)
                    if r:
                        cur.tfid, cur.tguid = int(r.group(1)), r.group(2)
                elif cur is not None and t.startswith("propertyPath:"):
                    cur.prop = unquote(t[len("propertyPath:"):].strip())
                elif cur is not None and t.startswith("value:"):
                    cur.value = unquote(t[len("value:"):].strip())
                elif cur is not None and t.startswith("objectReference:"):
                    r = REF.search(t)
                    cur.oref = (int(r.group(1)), r.group(2)) if r else None
                    if cur.prop is not None:
                        ovs.append(cur)
                    cur = None
            elif section == "rc" and t.startswith("- "):
                r = REF.search(t)
                removed_comp.append((int(r.group(1)), r.group(2)) if r else (0, None))
            elif section == "rg" and t.startswith("- "):
                r = REF.search(t)
                removed_go.append((int(r.group(1)), r.group(2)) if r else (0, None))
            elif section in ("ag", "ac") and t.startswith("- targetCorrespondingSourceObject:"):
                added.append([section, None, None])
                r = REF.search(t)
                if r:
                    added[-1][1] = (int(r.group(1)), r.group(2))
            elif section in ("ag", "ac") and t.startswith("addedObject:") and added:
                r = REF.search(t)
                added[-1][2] = int(r.group(1)) if r else None
    if cur and cur.prop is not None:
        ovs.append(cur)
    return ovs, removed_go, removed_comp, added


class Scene:
    def __init__(self, rel: str):
        self.path = rel
        self.name = os.path.splitext(os.path.basename(rel))[0]
        self.docs = parse_docs(read(rel))
        self.instances = {}
        for d in self.docs.values():
            if d.cls == 1001:
                src = ref(d, "m_SourcePrefab")
                if not src:
                    continue
                ovs, rg, rc, added = parse_instance(d)
                self.instances[d.fid] = dict(guid=src[1], overrides=ovs, removed_go=rg,
                                             removed_comp=rc, added=added)

    def canvas_instances(self):
        return {k: v for k, v in self.instances.items() if v["guid"] in (CORE_GUID, FORK_GUID)}

    def added_component_names(self, inst):
        out = []
        for sec, tgt, obj in inst["added"]:
            if sec != "ac" or obj is None or obj not in self.docs:
                continue
            out.append((tgt, comp_name(self.docs[obj])))
        return out

    def added_go_roots(self, inst):
        out = []
        for sec, tgt, obj in inst["added"]:
            if sec != "ag" or obj is None:
                continue
            d = self.docs.get(obj)
            if d is None:
                out.append((tgt, "?"))
                continue
            if d.stripped:
                cs = ref(d, "m_CorrespondingSourceObject")
                out.append((tgt, "nested-prefab " + (cs[1][:8] if cs and cs[1] else "?")))
            else:
                g = ref(d, "m_GameObject")
                gd = self.docs.get(g[0]) if g else None
                out.append((tgt, field(gd, "m_Name") if gd else "?"))
        return out


def find_scenes():
    out = []
    for dp, _, fns in os.walk(os.path.join(ROOT, "Assets")):
        for fn in fns:
            if fn.endswith(".unity"):
                out.append(os.path.relpath(os.path.join(dp, fn), ROOT).replace(os.sep, "/"))
    return sorted(out)


def files_referencing(guid: str, exts=(".unity", ".prefab", ".asset")):
    hits = []
    for dp, _, fns in os.walk(os.path.join(ROOT, "Assets")):
        for fn in fns:
            if fn.endswith(exts):
                p = os.path.join(dp, fn)
                with open(p, encoding="utf-8", errors="replace") as f:
                    if ("guid: " + guid) in f.read():
                        hits.append(os.path.relpath(p, ROOT).replace(os.sep, "/"))
    return sorted(hits)


# ── Analysis ──────────────────────────────────────────────────────────────────

def resolve(prefabs: dict[str, Prefab], guid: str | None, fid: int) -> str:
    pf = prefabs.get(guid or "")
    if pf is None:
        return f"<{(guid or '?')[:8]}:{fid}>"
    p, c = pf.owner(fid)
    return f"{p} :: {c}" if p is not None else f"[nested #{fid}]"


def classify(entries):
    """entries: list of (scene, inst). Returns (byKey, sample, uniform_all, differ_all, partial)."""
    byk, sample = collections.defaultdict(dict), {}
    for sc, inst in entries:
        for o in inst["overrides"]:
            byk[o.key][sc.name] = o.valuekey
            sample[o.key] = o
    n = len(entries)
    ua = [k for k, v in byk.items() if len(v) == n and len(set(v.values())) == 1]
    da = [k for k, v in byk.items() if len(v) == n and len(set(v.values())) > 1]
    pa = [k for k, v in byk.items() if len(v) < n]
    return byk, sample, ua, da, pa


def build():
    prefabs = {}
    for guid, rel in ((CORE_GUID, CORE_PATH), (FORK_GUID, FORK_PATH)):
        if os.path.exists(os.path.join(ROOT, rel)):
            prefabs[guid] = Prefab(rel)
    scenes = []
    for rel in find_scenes():
        t = read(rel)
        if CORE_GUID in t or FORK_GUID in t:
            scenes.append(Scene(rel))
    return prefabs, scenes


def structural_delta(core: Prefab, fork: Prefab):
    cp = {core.norm_path(g): g for g in core.go}
    fp = {fork.norm_path(g): g for g in fork.go}
    only_fork = sorted(set(fp) - set(cp))
    only_core = sorted(set(cp) - set(fp))
    comp_diff = []
    for p in sorted(set(cp) & set(fp)):
        a = [n for _, n in core.comps_of(cp[p])]
        b = [n for _, n in fork.comps_of(fp[p])]
        if a != b:
            comp_diff.append((p, a, b))
    return only_fork, only_core, comp_diff


def report(out=sys.stdout, as_json=None):
    prefabs, scenes = build()
    core, fork = prefabs.get(CORE_GUID), prefabs.get(FORK_GUID)
    data = {"scenes": [], "fork_exists": fork is not None}
    p = lambda *a: print(*a, file=out)

    p("== GameCanvas unification report ==")
    p(f"CORE : {CORE_PATH}  {'(' + str(len(core.go)) + ' GameObjects)' if core else 'MISSING'}")
    p(f"FORK : {FORK_PATH}  {'(' + str(len(fork.go)) + ' GameObjects)' if fork else 'absent'}")
    p(f"fork guid referenced by: {len(files_referencing(FORK_GUID))} file(s)")
    p()

    rows = []
    for sc in scenes:
        for fid, inst in sc.canvas_instances().items():
            fam = "FORK" if inst["guid"] == FORK_GUID else "CORE"
            over = [o for o in inst["overrides"] if not any(o.prop.startswith(x) for x in ALLOWED_OVERRIDE_PREFIXES)]
            added_c = sc.added_component_names(inst)
            added_g = sc.added_go_roots(inst)
            rows.append((fam, sc, inst, len(inst["overrides"]), len(over), added_c, added_g))
            data["scenes"].append(dict(scene=sc.path, family=fam, overrides=len(inst["overrides"]),
                                       non_default_overrides=len(over),
                                       removed_gameobjects=len(inst["removed_go"]),
                                       removed_components=len(inst["removed_comp"]),
                                       added_components=[n for _, n in added_c],
                                       added_gameobjects=[n for _, n in added_g]))
    p(f"{'family':6} {'scene':46} {'mods':>5} {'nonDef':>6} {'remGO':>5} {'remCmp':>6}  added")
    for fam, sc, inst, tot, nd, ac, ag in sorted(rows, key=lambda r: (r[0], r[1].name)):
        p(f"{fam:6} {sc.name:46} {tot:5} {nd:6} {len(inst['removed_go']):5} {len(inst['removed_comp']):6}  "
          f"comps={[n for _, n in ac]} gos={[n for _, n in ag]}")
    p()

    fork_entries = [(sc, inst) for fam, sc, inst, *_ in rows if fam == "FORK"]
    if fork_entries:
        byk, sample, ua, da, pa = classify(fork_entries)
        p(f"-- fork-family override classification over {len(fork_entries)} scene(s) --")
        p(f"distinct keys {len(byk)} | identical in every scene {len(ua)} | present everywhere, values differ {len(da)} | "
          f"present in some scenes {len(pa)}")
        data["fork_classification"] = dict(distinct=len(byk), uniform=len(ua), differ=len(da), partial=len(pa))
        p("\n   present everywhere, values differ:")
        for k in sorted(da, key=lambda k: (sample[k].tfid, sample[k].prop)):
            o = sample[k]
            vals = collections.Counter(byk[k].values())
            p(f"     {resolve(prefabs, o.tguid, o.tfid)} . {o.prop}")
            for v, c in vals.most_common():
                who = [s for s, vv in byk[k].items() if vv == v]
                p(f"          {c:2}x {v[:70]:70} {who if c <= 3 else ''}")
        p("\n   present in some scenes only (value : scene count):")
        for k in sorted(pa, key=lambda k: (sample[k].tfid, sample[k].prop)):
            o = sample[k]
            vals = collections.Counter(byk[k].values())
            p(f"     {resolve(prefabs, o.tguid, o.tfid)} . {o.prop}  [{len(byk[k])} scene(s)]  "
              + "; ".join(f"{v[:40]}:{c}" for v, c in vals.most_common()))
        p()

    if core and fork:
        only_fork, only_core, comp_diff = structural_delta(core, fork)
        p(f"-- prefab structural delta (by hierarchy path) --")
        p(f"only in FORK: {len(only_fork)}   only in CORE: {len(only_core)}   shared paths with different components: {len(comp_diff)}")
        for x in only_fork:
            p("   +fork  " + x)
        for x in only_core:
            p("   +core  " + x)
        for path, a, b in comp_diff:
            p(f"   ≠      {path or '(root)'}\n            CORE {a}\n            FORK {b}")
        data["delta"] = dict(only_fork=only_fork, only_core=only_core,
                             comp_diff=[dict(path=x, core=a, fork=b) for x, a, b in comp_diff])
        # Cross-asset references: overrides inside the fork whose objectReference points into CORE.
        dangling = re.findall(r"propertyPath: (\S+)\s+value:[^\n]*\s+objectReference: \{fileID: (-?\d+), guid: "
                              + CORE_GUID, unwrap(read(FORK_PATH)))
        p(f"\n-- fork overrides pointing INTO the CORE asset (dangling by construction): {len(dangling)}")
        for prop, fid in dangling:
            p(f"   {prop} -> CORE#{fid}")

    if as_json:
        with open(as_json, "w") as f:
            json.dump(data, f, indent=2)
        p(f"\njson written to {as_json}")
    return data


CONTRACT_RESOLUTION = (1920, 1080)


def core_contract_problems(core: "Prefab") -> list[str]:
    """CORE's root CanvasScaler must say 1920x1080 / ScaleWithScreenSize and the root must carry AdaptiveCanvasScaler."""
    problems = []
    roots = [g for g in core.go if core.norm_path(g) == ""]
    if len(roots) != 1:
        return [f"{CORE_PATH}: expected one root GameObject, found {len(roots)}."]
    root = roots[0]
    names = [n for _, n in core.comps_of(root)]
    if "AdaptiveCanvasScaler" not in names:
        problems.append(f"{CORE_PATH}: root has no AdaptiveCanvasScaler ({names}).")
    scaler = next((d for d in core.docs.values() if d.cls == 114 and comp_name(d) == "CanvasScaler"
                   and (ref(d, "m_GameObject") or (None,))[0] == root), None)
    if scaler is None:
        problems.append(f"{CORE_PATH}: root has no CanvasScaler.")
        return problems
    m = re.search(r"m_ReferenceResolution: \{x: ([-\d.]+), y: ([-\d.]+)\}", scaler.body)
    res = (float(m.group(1)), float(m.group(2))) if m else None
    if res is None or abs(res[0] - CONTRACT_RESOLUTION[0]) > 0.5 or abs(res[1] - CONTRACT_RESOLUTION[1]) > 0.5:
        problems.append(f"{CORE_PATH}: CanvasScaler reference resolution is {res}, contract is {CONTRACT_RESOLUTION}.")
    if (field(scaler, "m_UiScaleMode") or "") .strip() != "1":
        problems.append(f"{CORE_PATH}: CanvasScaler uiScaleMode is not ScaleWithScreenSize (1).")
    return problems


def check() -> int:
    """The post-migration gate. Exit 1 while any invariant is unmet."""
    problems = []
    prefabs, scenes = build()

    if os.path.exists(os.path.join(ROOT, FORK_PATH)):
        problems.append(f"{FORK_PATH} still exists — delete it once no scene references it.")
    refs = files_referencing(FORK_GUID)
    for r in refs:
        problems.append(f"{r} still references the fork guid {FORK_GUID}.")

    # The canvas contract: CORE is authored at 1920x1080, Scale-With-Screen-Size, with an
    # AdaptiveCanvasScaler on its root. Both prefab assets were authored at 800x450 and only
    # scene overrides ever said 1920x1080, which is how the first re-point handed a scene an
    # 800x450 canvas.
    core = prefabs.get(CORE_GUID)
    if core is None:
        problems.append(f"{CORE_PATH} is missing.")
    else:
        problems.extend(core_contract_problems(core))

    by_name = {sc.name: sc for sc in scenes}
    for name in MIGRATED_SCENES:
        sc = by_name.get(name)
        if sc is None:
            problems.append(f"{name}: no canvas instance found in any scene of that name.")
            continue
        insts = sc.canvas_instances()
        if len(insts) != 1:
            problems.append(f"{sc.path}: expected exactly one GameCanvas instance, found {len(insts)}.")
            continue
        inst = next(iter(insts.values()))
        if inst["guid"] != CORE_GUID:
            problems.append(f"{sc.path}: canvas instance is not on {CORE_PATH}.")
        if inst["removed_go"]:
            problems.append(f"{sc.path}: {len(inst['removed_go'])} removed GameObject(s) on the canvas instance.")
        if inst["removed_comp"]:
            problems.append(f"{sc.path}: {len(inst['removed_comp'])} removed component(s) on the canvas instance.")
        added_c = sc.added_component_names(inst)
        if added_c:
            problems.append(f"{sc.path}: scene-added component(s) on the canvas instance: {[n for _, n in added_c]}.")
        bad = [o for o in inst["overrides"] if not any(o.prop.startswith(x) for x in ALLOWED_OVERRIDE_PREFIXES)]
        if bad:
            sample = ", ".join(f"{resolve(prefabs, o.tguid, o.tfid)}.{o.prop}" for o in bad[:4])
            problems.append(f"{sc.path}: {len(bad)} non-default override(s) on the canvas instance (e.g. {sample}).")

    if problems:
        print("GameCanvas unification gate: FAIL")
        for pr in problems[:80]:
            print("  - " + pr)
        if len(problems) > 80:
            print(f"  ... {len(problems) - 80} more")
        return 1
    print(f"GameCanvas unification gate: OK ({len(MIGRATED_SCENES)} scenes on {CORE_PATH}, fork gone, no override walls)")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="post-migration gate; exit 1 until every invariant holds")
    ap.add_argument("--json", metavar="PATH", help="also write the report as JSON")
    args = ap.parse_args()
    if args.check:
        return check()
    report(as_json=args.json)
    return 0


if __name__ == "__main__":
    sys.exit(main())

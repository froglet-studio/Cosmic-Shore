#!/usr/bin/env python3
"""No cell config may build a cytoplasm far denser than a standard cell's.

WHY THIS EXISTS.

`SnowChanger.Initialize` (Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs) fills the
membrane sphere with shards at a fixed SPACING, so the count is

    round((4/3)·π·R³ / spacing³ · GraphicsCuts.CytoplasmDensity)

- a CUBE of the membrane radius. A standard 1,200 cell at the prefab's 120 spacing builds ~4,189
shards. Cleave intensities 1 and 2 put a 3,600 membrane on the same spacing and built ~113,000
(27x), every one a transparent instance drawn and reoriented, until PR #1573 set
`CytoplasmShardDistance: 360` from `Tools/Build/author_cleave_assets.py`. Nothing flagged it: the
config carried a legal membrane, the spacing field was optional, and the only symptom was frame
time in one mode.

WHAT THIS CHECKS.

For every `CellConfigDataSO` asset under Assets/ it works the count out from the shipped YAML, the
way the game does:

- R is the membrane prefab's radius as `Cell.MembraneRadius` reads it: the root's
  `CapsuleMembrane.radius` when the root carries one, else the root transform's `m_LocalScale.x`.
  A prefab VARIANT is resolved through its source chain, applying its `radius` / `m_LocalScale.x`
  modifications (BigMembraneVariant is MembraneBase at 1,600).
- spacing is the config's `CytoplasmShardDistance` when it is above 0, else the config's cytoplasm
  prefab's `shardDistance` (120 in SnowChanger.prefab), variants resolved the same way.
- `GraphicsCuts.CytoplasmDensity` is taken at 1 (the full tier): a cut may only thin the field.
- No `CytoplasmPrefab` means no cytoplasm (`Cell.SpawnCytoplasm` returns): count 0.

It fails when a config's count is above LIMIT_FACTOR x the standard cell's, unless the config is in
ALLOWED_OVER with a reason. An allow-listed config fails again the moment anything references it
(a scene, a game card, a mode list - any asset that names its guid), because "nothing loads it" is
the only reason an entry is allowed.

WHAT THIS DOES NOT PROVE.

That a count under the limit is cheap enough on every device, or that a config's membrane is the
one a scene actually shows (a mode may scale a preview shell, ModePreviewArena does). A component a
prefab VARIANT adds to its root (m_AddedComponents) is not read: no shipped membrane or cytoplasm
variant does that, and a variant that adds a CapsuleMembrane would be read by its scale instead. It is the
static cube law against the shipped YAML, nothing more. The fix it points at is the config's
`CytoplasmShardDistance` (scale it with the radius: 120 x R / 1200 keeps the standard count).

Read-only. Run:  python3 Tools/Build/check_cytoplasm_shards.py [--self-test] [--all]
"""
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# A ROOT one dirname too shallow resolves to Tools/ and every path under it silently misses, which
# reads as "nothing found" rather than as a bug. Assert the tree is under it.
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"
ASSETS = os.path.join(ROOT, "Assets")

CELL_CONFIG_SCRIPT = "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"
CAPSULE_MEMBRANE_SCRIPT = "Assets/_Scripts/Game/Environment/CapsuleMembrane.cs"
SNOW_CHANGER_SCRIPT = "Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs"

STANDARD_RADIUS = 1200.0      # the standard cell (CapsuleMembrane.prefab)
STANDARD_SPACING = 120.0      # SnowChanger.prefab's shardDistance
LIMIT_FACTOR = 3.0            # fail above 12,567 shards


def shard_count(radius, spacing):
    """SnowChanger.Initialize at full density: Mathf.Max(1, Mathf.RoundToInt(volume / spacing³))."""
    if spacing <= 0:
        return 0
    volume = (4.0 / 3.0) * math.pi * radius ** 3
    return max(1, int(round(volume / spacing ** 3)))


STANDARD_COUNT = shard_count(STANDARD_RADIUS, STANDARD_SPACING)
LIMIT = int(LIMIT_FACTOR * STANDARD_COUNT)

# Configs allowed over the limit, path (relative to the repo) -> reason. The only acceptable reason
# is that nothing loads the config; the gate re-checks that on every run. A config somebody wires
# up again must get a CytoplasmShardDistance instead of staying here.
# Empty as of 2026-10-26: the largest unreferenced configs, Wildlife Blitz Cell 3 and 4
# (BigMembraneVariant, radius 1,600), build ~9,929, under the limit, so they need no entry.
ALLOWED_OVER = {}

GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[^\n]*$", re.M)


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def read(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        return fh.read()


def meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})", read(path + ".meta"), re.M)
    return m.group(1) if m else None


def ref(text, field):
    """`  field: {fileID: N, guid: G, type: 3}` (Unity wraps long refs onto the next line)."""
    m = re.search(r"^  " + field + r": \{fileID: (-?\d+)(?:,\s*guid: ([0-9a-f]{32}))?", text, re.M)
    if not m or m.group(1) == "0":
        return None
    return int(m.group(1)), m.group(2)


def number_field(text, field):
    m = re.search(r"^  " + field + r": (-?[\d.eE+]+)\s*$", text, re.M)
    return float(m.group(1)) if m else None


def documents(text):
    """[(classID, fileID, stripped, body)] for every YAML document in a prefab."""
    heads = list(DOC_RE.finditer(text))
    out = []
    for i, h in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
        out.append((int(h.group(1)), int(h.group(2)), bool(h.group(3)), text[h.end():end]))
    return out


class Prefabs:
    """Resolves a prefab's root fields through its variant chain. `files` maps guid -> text."""

    def __init__(self, files, capsule_guid, snow_guid):
        self.files = files
        self.capsule_guid = capsule_guid
        self.snow_guid = snow_guid
        self._cache = {}

    def root(self, guid):
        """{'tf': root transform fileID, 'scale_x', 'mono': {script guid: (fileID, {field: value})}}
        for the prefab's ROOT GameObject, or None when the guid is not a prefab we can read."""
        if guid in self._cache:
            return self._cache[guid]
        self._cache[guid] = None        # a cycle resolves to "unreadable", never recurses forever
        text = self.files.get(guid)
        result = self._resolve(text) if text is not None else None
        self._cache[guid] = result
        return result

    def _resolve(self, text):
        docs = documents(text)
        # A plain prefab: the root transform is the one with no father.
        for cls, fid, stripped, body in docs:
            if cls in (4, 224) and not stripped and re.search(r"^  m_Father: \{fileID: 0\}", body, re.M):
                go = ref(body, "m_GameObject")
                sm = re.search(r"m_LocalScale: \{x: (-?[\d.eE+]+)", body)
                monos = {}
                for c2, f2, s2, b2 in docs:
                    if c2 != 114 or s2:
                        continue
                    owner = ref(b2, "m_GameObject")
                    script = ref(b2, "m_Script")
                    if owner and go and owner[0] == go[0] and script and script[1]:
                        monos[script[1]] = (f2, b2)
                return {"tf": fid, "scale_x": float(sm.group(1)) if sm else 1.0,
                        "mono": {g: (f, self._fields(b)) for g, (f, b) in monos.items()}}
        # A variant: the root PrefabInstance has no parent; resolve its source, then apply its mods.
        for cls, fid, stripped, body in docs:
            if cls != 1001 or not re.search(r"m_TransformParent: \{fileID: 0\}", body):
                continue
            src = re.search(r"m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32})", body)
            base = self.root(src.group(1)) if src else None
            if base is None:
                return None
            mods = self._modifications(body, src.group(1))
            # Stripped stubs name the fileID a source object has in this file; Unity writes them
            # for anything referenced, and older files do not always follow the XOR rule.
            stubs = {}
            for _, f2, s2, b2 in docs:
                corr = ref(b2, "m_CorrespondingSourceObject") if s2 else None
                inst = ref(b2, "m_PrefabInstance") if s2 else None
                if corr and inst and inst[0] == fid:
                    stubs[corr[0]] = f2
            scale_x = mods.get((base["tf"], "m_LocalScale.x"), base["scale_x"])
            mono = {}
            for g, (f, fields) in base["mono"].items():
                fields = dict(fields)
                for (target, path), value in mods.items():
                    if target == f and path in fields:
                        fields[path] = value
                mono[g] = (stubs.get(f) or self._local_id(fid, f), fields)
            return {"tf": stubs.get(base["tf"]) or self._local_id(fid, base["tf"]), "scale_x": scale_x, "mono": mono}
        return None

    @staticmethod
    def _local_id(instance_id, source_id):
        # The fileID a source object has inside a variant (what a variant OF this variant targets):
        # Unity derives it as (instance ^ source) & 0x7fffffffffffffff when no stripped stub says.
        return (instance_id ^ source_id) & 0x7FFFFFFFFFFFFFFF

    @staticmethod
    def _modifications(body, src_guid):
        mods = {}
        for m in re.finditer(r"- target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}),\s*type: 3\}\s*\n"
                             r"\s*propertyPath: (\S+)\s*\n\s*value: ([^\n]*)", body):
            if m.group(2) != src_guid:
                continue
            try:
                mods[(int(m.group(1)), m.group(3))] = float(m.group(4))
            except ValueError:
                pass
        return mods

    @staticmethod
    def _fields(body):
        return {k: float(v) for k, v in re.findall(r"^  (radius|shardDistance): (-?[\d.eE+]+)\s*$", body, re.M)}

    def membrane_radius(self, guid):
        """Cell.MembraneRadius for an instance of this prefab: CapsuleMembrane.Radius, else scale.x."""
        r = self.root(guid)
        if r is None:
            return None
        cm = r["mono"].get(self.capsule_guid)
        if cm is not None:
            return cm[1].get("radius", 500.0)    # CapsuleMembrane's field initializer
        return r["scale_x"]

    def shard_distance(self, guid):
        r = self.root(guid)
        if r is None:
            return None
        sc = r["mono"].get(self.snow_guid)
        if sc is None:
            return None
        return sc[1].get("shardDistance", 100.0)  # SnowChanger's field initializer


def evaluate(configs, prefabs):
    """configs: [(path, text)]. Returns [(path, radius, spacing, count, note)]; None fields when the
    YAML could not be resolved (note says why). Pure, so --self-test can drive it."""
    rows = []
    for path, text in configs:
        cyto = ref(text, "CytoplasmPrefab")
        if cyto is None:
            rows.append((path, None, None, 0, "no CytoplasmPrefab"))
            continue
        mem = ref(text, "MembranePrefab")
        radius = prefabs.membrane_radius(mem[1]) if mem and mem[1] else 0.0
        if radius is None:
            rows.append((path, None, None, None, f"membrane prefab {mem[1]} not readable"))
            continue
        override = number_field(text, "CytoplasmShardDistance") or 0
        spacing = override if override > 0 else prefabs.shard_distance(cyto[1])
        if spacing is None:
            rows.append((path, radius, None, None, f"cytoplasm prefab {cyto[1]} has no SnowChanger"))
            continue
        rows.append((path, radius, spacing, shard_count(radius, spacing),
                     "config spacing" if override > 0 else "prefab spacing"))
    return rows


def failures(rows, referenced, allowed=ALLOWED_OVER, limit=LIMIT):
    """referenced: path -> [referrer paths]. Returns a list of failure strings."""
    out = []
    for path, radius, spacing, count, note in rows:
        if count is None:
            out.append(f"{path}: could not work the count out ({note})")
            continue
        if count <= limit:
            continue
        if path in allowed:
            refs = referenced.get(path, [])
            if refs:
                out.append(f"{path}: {count:,} shards (R {radius:g}, spacing {spacing:g}) is allow-listed as "
                           f"unreferenced, but {len(refs)} asset(s) reference it, e.g. {refs[0]}. "
                           f"Set its CytoplasmShardDistance (about {spacing_for(radius):g}) and drop the entry.")
            continue
        out.append(f"{path}: {count:,} shards (R {radius:g}, spacing {spacing:g}, {note}) is over the limit "
                   f"{limit:,} ({LIMIT_FACTOR:g}x a standard cell's {STANDARD_COUNT:,}). "
                   f"Set CytoplasmShardDistance to about {spacing_for(radius):g} (from its generator if it has one).")
    counts = {r[0]: r[3] for r in rows}
    for path in allowed:
        if path not in counts:
            out.append(f"ALLOWED_OVER names {path}, which is not a CellConfigDataSO asset any more: drop the entry")
        elif counts[path] is not None and counts[path] <= limit:
            out.append(f"ALLOWED_OVER names {path}, which builds {counts[path]:,}, under the limit: drop the entry")
    return out


def spacing_for(radius):
    """The spacing that keeps the standard count at this radius."""
    return round(STANDARD_SPACING * radius / STANDARD_RADIUS)


def load():
    capsule_guid = meta_guid(os.path.join(ROOT, CAPSULE_MEMBRANE_SCRIPT))
    snow_guid = meta_guid(os.path.join(ROOT, SNOW_CHANGER_SCRIPT))
    config_guid = meta_guid(os.path.join(ROOT, CELL_CONFIG_SCRIPT))
    assert capsule_guid and snow_guid and config_guid, "a script .meta lost its guid"

    prefab_files, configs, guid_of = {}, [], {}
    yaml_files = []
    for dirpath, _, names in os.walk(ASSETS):
        for name in names:
            if not name.endswith((".prefab", ".asset", ".unity")):
                continue
            path = os.path.join(dirpath, name)
            yaml_files.append(path)
            if name.endswith(".prefab"):
                g = meta_guid(path) if os.path.exists(path + ".meta") else None
                if g:
                    prefab_files[g] = read(path)
            elif name.endswith(".asset"):
                text = read(path)
                if re.search(r"^  m_Script: \{fileID: 11500000, guid: " + config_guid, text, re.M):
                    configs.append((rel(path), text))
                    guid_of[meta_guid(path)] = rel(path)

    # Who references each config: any other YAML asset that names its guid.
    referenced = {}
    for path in yaml_files:
        r = rel(path)
        for g in set(GUID_RE.findall(read(path))):
            target = guid_of.get(g)
            if target and target != r:
                referenced.setdefault(target, []).append(r)
    # A config under Resources/ is loadable by path, which no guid scan sees.
    for _, target in guid_of.items():
        if "/Resources/" in target:
            referenced.setdefault(target, []).append("(under a Resources/ folder)")

    return sorted(configs), Prefabs(prefab_files, capsule_guid, snow_guid), referenced


def self_test():
    ok = True

    def check(name, cond):
        nonlocal ok
        print(("PASS " if cond else "FAIL ") + name)
        ok = ok and cond

    check(f"standard 1,200 cell at 120 builds 4,189 (got {STANDARD_COUNT})", STANDARD_COUNT == 4189)
    check("Cleave's old 3,600 at 120 built ~113,000", shard_count(3600, 120) == 113097)
    check("Cleave's 3,600 at 360 is the standard count", shard_count(3600, 360) == STANDARD_COUNT)

    # A synthetic tree: a capsule membrane prefab, a scaled base, a variant of it, a variant of the
    # variant, and a cytoplasm prefab - the shapes the shipped YAML takes.
    CAP, SNOW = "c" * 32, "d" * 32

    def plain(go, tf, scale, mono=None):
        out = (f"--- !u!1 &{go}\nGameObject:\n  m_Name: x\n"
               f"--- !u!4 &{tf}\nTransform:\n  m_GameObject: {{fileID: {go}}}\n"
               f"  m_LocalScale: {{x: {scale}, y: {scale}, z: {scale}}}\n  m_Father: {{fileID: 0}}\n")
        if mono:
            script, fid, fields = mono
            out += (f"--- !u!114 &{fid}\nMonoBehaviour:\n  m_GameObject: {{fileID: {go}}}\n"
                    f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n"
                    + "".join(f"  {k}: {v}\n" for k, v in fields.items()))
        return out

    def variant(inst, src, mods):        # Unity wraps each target ref, as the shipped variants do
        return (f"--- !u!1001 &{inst}\nPrefabInstance:\n  m_Modification:\n    m_TransformParent: {{fileID: 0}}\n"
                f"    m_Modifications:\n"
                + "".join(f"    - target: {{fileID: {t}, guid: {src},\n        type: 3}}\n      propertyPath: {p}\n"
                          f"      value: {v}\n      objectReference: {{fileID: 0}}\n" for t, p, v in mods)
                + f"  m_SourcePrefab: {{fileID: 100100000, guid: {src}, type: 3}}\n")

    files = {
        "1" * 32: plain(10, 11, 1, (CAP, 12, {"radius": 1200})),                # CapsuleMembrane
        "2" * 32: plain(20, 21, 1000),                                           # MembraneBase
        "3" * 32: variant(30, "2" * 32, [(21, "m_LocalScale.x", 1600)]),         # BigMembraneVariant
        "4" * 32: variant(40, "3" * 32, [((30 ^ 21) & 0x7FFFFFFFFFFFFFFF, "m_LocalScale.x", 2000)]),
        "5" * 32: variant(50, "1" * 32, [(12, "radius", 3600)]),                 # a capsule variant
        "6" * 32: plain(60, 61, 1, (SNOW, 62, {"shardDistance": 120})),          # SnowChanger
        # a variant whose stripped stub gives its root transform a fileID the XOR rule would not
        "7" * 32: variant(70, "2" * 32, [(21, "m_LocalScale.x", 1500)])
                  + ("--- !u!4 &999 stripped\nTransform:\n"
                     f"  m_CorrespondingSourceObject: {{fileID: 21, guid: {'2' * 32},\n    type: 3}}\n"
                     "  m_PrefabInstance: {fileID: 70}\n"),
        "8" * 32: variant(80, "7" * 32, [(999, "m_LocalScale.x", 2500)]),
    }
    pf = Prefabs(files, CAP, SNOW)
    check("CapsuleMembrane.radius wins over scale", pf.membrane_radius("1" * 32) == 1200)
    check("a plain membrane uses its root scale.x", pf.membrane_radius("2" * 32) == 1000)
    check("a variant's m_LocalScale.x modification applies", pf.membrane_radius("3" * 32) == 1600)
    check("a variant of a variant resolves through the chain", pf.membrane_radius("4" * 32) == 2000)
    check("a stripped stub's fileID wins over the XOR rule", pf.membrane_radius("8" * 32) == 2500)
    check("a variant's radius modification applies", pf.membrane_radius("5" * 32) == 3600)
    check("cytoplasm prefab spacing is read", pf.shard_distance("6" * 32) == 120)

    def cfg(membrane, spacing=None, cyto="6" * 32):
        return ("MonoBehaviour:\n"
                f"  MembranePrefab: {{fileID: 1, guid: {membrane}, type: 3}}\n"
                + (f"  CytoplasmPrefab: {{fileID: 2, guid: {cyto},\n    type: 3}}\n" if cyto
                   else "  CytoplasmPrefab: {fileID: 0}\n")
                + (f"  CytoplasmShardDistance: {spacing}\n" if spacing is not None else ""))

    rows = evaluate([
        ("std.asset", cfg("1" * 32)),
        ("big.asset", cfg("3" * 32)),
        ("cleave_old.asset", cfg("5" * 32, 0)),          # the negative control: 3,600 at 120
        ("cleave_new.asset", cfg("5" * 32, 360)),
        ("bare.asset", cfg("5" * 32, cyto=None)),
    ], pf)
    counts = {r[0]: r[3] for r in rows}
    check("standard config counts 4,189", counts["std.asset"] == 4189)
    check("an unreadable membrane is a failure, not a pass",
          bool(failures(evaluate([("x.asset", cfg("9" * 32))], pf), {}, {})))
    check("no cytoplasm prefab is 0 shards", counts["bare.asset"] == 0)
    check("the wrapped CytoplasmPrefab ref is read", counts["cleave_new.asset"] == STANDARD_COUNT)

    fails = failures(rows, {}, allowed={})
    check("the negative control (3,600 membrane at 120) fails", any("cleave_old" in f for f in fails))
    check("Cleave at 360 passes", not any("cleave_new" in f for f in fails))
    check("a 1,600 membrane at 120 (~9,929) is under the limit", not any("big.asset" in f for f in fails))

    allowed = {"cleave_old.asset": "test"}
    check("an allow-listed, unreferenced config passes", not any("cleave_old" in f for f in failures(rows, {}, allowed)))
    check("an allow-listed config that something references fails",
          any("cleave_old" in f for f in failures(rows, {"cleave_old.asset": ["scene.unity"]}, allowed)))
    check("a stale allow-list entry fails", any("gone.asset" in f for f in failures(rows, {}, {"gone.asset": "x"})))
    check("an allow-list entry under the limit fails", any("std.asset" in f for f in failures(rows, {}, {"std.asset": "x"})))

    # The shipped tree: the real numbers this gate exists for.
    configs, prefabs, referenced = load()
    real = {r[0]: r for r in evaluate(configs, prefabs)}
    cleave = [r for p, r in real.items() if "/Cleave Cell/" in p]
    check(f"the shipped tree has CellConfigDataSO assets ({len(real)})", len(real) > 20)
    check("the shipped Cleave configs resolve", bool(cleave) and all(r[3] is not None for r in cleave))
    std = [r for r in real.values() if r[1] == STANDARD_RADIUS and r[2] == STANDARD_SPACING]
    check("some shipped config is a standard 1,200 / 120 cell", bool(std))
    check("Cleave's real 3,600 membrane resolves through CleaveMembrane.prefab",
          any(r[1] == 3600 for r in cleave))
    big = [r for p, r in real.items() if "/WildLife Blitz Cells/Cell 3/" in p]
    check("Wildlife Blitz Cell 3 resolves BigMembraneVariant's wrapped scale modification (1,600)",
          bool(big) and abs(big[0][1] - 1600) < 0.01)
    # Negative control on the real tree: strip Cleave's override and the gate must go red.
    stripped = [(p, re.sub(r"(?m)^  CytoplasmShardDistance: \d+", "  CytoplasmShardDistance: 0", t))
                for p, t in configs]
    check("the real tree with Cleave's spacing removed fails",
          any("/Cleave Cell/" in f for f in failures(evaluate(stripped, prefabs), referenced)))
    print("self-test " + ("passed" if ok else "FAILED"))
    return 0 if ok else 1


def main():
    if "--self-test" in sys.argv:
        return self_test()
    configs, prefabs, referenced = load()
    rows = evaluate(configs, prefabs)
    if "--all" in sys.argv:
        for path, radius, spacing, count, note in sorted(rows, key=lambda r: -(r[3] or 0)):
            refs = len(referenced.get(path, []))
            print(f"{count if count is not None else '?':>9}  R {radius if radius is not None else '?':>6}  "
                  f"spacing {spacing if spacing is not None else '?':>5}  refs {refs:>3}  {path}  ({note})")
    fails = failures(rows, referenced)
    over = sum(1 for r in rows if r[3] is not None and r[3] > LIMIT)
    print(f"cytoplasm shards: {len(rows)} cell configs, standard cell {STANDARD_COUNT:,}, "
          f"limit {LIMIT:,}; {over} over the limit, {len(ALLOWED_OVER)} allow-listed")
    for f in fails:
        print("FAIL " + f)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())

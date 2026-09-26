#!/usr/bin/env python3
"""Fail the build on an ElementalFloat that ADVERTISES scaling nothing can read.

`ElementalFloat` is the project's one element->parameter scaling channel
(`Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md`). It scales in exactly two
ways:

  * `EvaluateLive(status)` -- the unified read, valid anywhere (its sibling
    `EvaluateReplicated(status)` reads the owner's REPLICATED level instead, for a value
    every peer must agree on, and counts as the same kind of read); and
  * the legacy BOUND path, where `ElementalShipComponent.BindElementalFloats` subscribes
    `ScaleValueWithLevel` so a consumer reading the raw `.Value` field sees the scaled
    number without asking.

The binder reflects over MonoBehaviour fields ONLY. So an ElementalFloat declared on a
**ScriptableObject** and read as `.Value` can never scale, whatever its asset says -- and an
asset that authors it `Enabled: 1` with `Min != Max` is a declaration the build silently
contradicts. Three shipped that way and each had never run once:

    Rhino   GrowTrailAction.maxSize      Mass   4 -> 8     (live channel: x1 -> x1.5)
    Rhino   GrowSkimmerAction.shrinkRate Charge 6 -> 2     (no live channel at all)
    Sparrow FullAutoAction.speedValue    Space  375 -> 4875 (live channel: x1 -> x9)

None of them is visible to a compiler, a test or a code review: the data is well-formed, the
field is the right type, and the number in the inspector is exactly what a designer would
expect to feel. Only the pairing of "declared on an SO" with "read as .Value" is wrong, and
that pairing spans two files.

Usage:
    python3 Tools/Build/check_elemental_floats.py            # report
    python3 Tools/Build/check_elemental_floats.py --check    # exit 1 on any finding
    python3 Tools/Build/check_elemental_floats.py --self-test
"""
import argparse
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
SCRIPTS = os.path.join(ASSETS, "_Scripts")

ELEMENTS = {0: "None", 1: "Charge", 2: "Mass", 3: "Space", 4: "Time"}

# `=(?!>)` so a forwarding ACCESSOR (`public ElementalFloat Foo => foo;`) is not counted
# as a second declaration of the same float. Harmless for findings (an accessor name never
# appears in YAML) and it stops the inventory double-counting.
FIELD_RE = re.compile(r"\bElementalFloat\s+(\w+)\s*(?:=(?!>)|;)")
CLASS_RE = re.compile(r"\bclass\s+(\w+)\s*:\s*([\w<>, .]+)")
# Unity writes an ElementalFloat as a contiguous block, in C# declaration order. UseFloor and
# Floor were added after the fleet's assets were last written, so most blocks stop at
# `element:` -- the pattern must not require them.
BLOCK_RE = re.compile(
    r"^([ \t]*)(\w+):[ \t]*\n"
    r"\1[ \t]+Enabled:[ \t]*(\d+)[ \t]*\n"
    r"\1[ \t]+Value:[ \t]*(-?[\d.eE+]+)[ \t]*\n"
    r"\1[ \t]+Min:[ \t]*(-?[\d.eE+]+)[ \t]*\n"
    r"\1[ \t]+Max:[ \t]*(-?[\d.eE+]+)[ \t]*\n"
    r"\1[ \t]+element:[ \t]*(\d+)[ \t]*$", re.M)


def code_only(src: str) -> str:
    """`src` with comment bodies and string-literal bodies replaced by spaces.

    Not optional. Both scanners below match a declaration or a call by PATTERN, and prose
    quotes code: the very comment that recorded `AOERadialBlocks.depthScale`'s removal —
    "a `private ElementalFloat depthScale = new(1f)` used to scale these two here" — read as a
    live declaration and put the deleted field straight back into this tool's inventory. A
    `[Tooltip]` string naming a field does the same. Offsets are preserved, so line numbers
    still map."""
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c in '"\'':
            q, i = c, i + 1
            while i < n and src[i] != q:
                if src[i] != "\n":
                    out[i] = " "
                i += 2 if src[i] == "\\" else 1
            i += 1
        elif c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                out[i] = " "
                i += 1
        elif c == "/" and i + 1 < n and src[i + 1] == "*":
            while i < n and not (src[i] == "*" and i + 1 < n and src[i + 1] == "/"):
                if src[i] != "\n":
                    out[i] = " "
                i += 1
            for j in range(i, min(i + 2, n)):
                out[j] = " "
            i += 2
        else:
            i += 1
    return "".join(out)


def is_scriptable_object(src: str, cls: str) -> bool:
    """Does `cls` (declared in `src`) compile to a ScriptableObject?

    A name test, deliberately, rather than a resolved inheritance chain: every SO base in
    this project is either `ScriptableObject` itself or a name ending in `SO`
    (`ShipActionSO`, `VesselActionSO`, ...), and `[CreateAssetMenu]` is decisive on its own.
    A false NEGATIVE here only means a finding is not reported; a false positive would cry
    wolf, which is worse."""
    if "[CreateAssetMenu" in src:
        return True
    for m in CLASS_RE.finditer(src):
        if m.group(1) != cls:
            continue
        for base in (b.strip() for b in m.group(2).split(",")):
            if base == "ScriptableObject" or base.endswith("SO"):
                return True
    return False


def script_guids(scripts_dir):
    """{guid: relative .cs path} from the `.meta` beside each script."""
    out = {}
    for dirpath, _dirs, files in os.walk(scripts_dir):
        for name in files:
            if not name.endswith(".cs.meta"):
                continue
            meta = os.path.join(dirpath, name)
            try:
                text = open(meta, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            m = re.search(r"^guid:\s*([0-9a-f]{32})", text, re.M)
            if m:
                out[m.group(1)] = os.path.relpath(meta[:-5], ROOT)
    return out


def scan_declarations(scripts_dir):
    """{(relative .cs path, field name): hosted-on-a-ScriptableObject?}

    Keyed on the FILE as well as the field, because a field name alone is not an identity:
    `maxSize` is declared on two ScriptableObjects AND on the MonoBehaviour `GrowActionBase`,
    and a name-keyed check that skipped any field with a MonoBehaviour host anywhere in the
    tree silently exempted both SOs. The asset says which script it is an instance of; ask it."""
    out = {}
    for dirpath, _dirs, files in os.walk(scripts_dir):
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            try:
                src = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if "ElementalFloat" not in src:
                continue
            src = code_only(src)
            cls = os.path.splitext(name)[0]
            so = is_scriptable_object(src, cls)
            for m in FIELD_RE.finditer(src):
                field = m.group(1)
                if field in ("Multiplier",):          # the static factory, not a field
                    continue
                head = src[src.rfind("\n", 0, m.start()) + 1:m.start()]
                if "(" in head:                        # a parameter, not a field
                    continue
                out[(os.path.relpath(path, ROOT), field)] = so
    return out


def scan_evaluations(scripts_dir):
    """Every field name the tree ever calls `EvaluateLive` / `EvaluateReplicated` on, from ANY file.

    Project-wide on purpose: a `[SerializeField]` on an SO is routinely read by its executor
    through a public accessor or the field itself, so a per-file search would report a live
    float as dead -- which is exactly the direction that gets a gate switched off."""
    live = set()
    rx = re.compile(r"\b(\w+)\s*\.\s*Evaluate(?:Live|Replicated)\s*\(")
    for dirpath, _dirs, files in os.walk(scripts_dir):
        for name in files:
            if not name.endswith(".cs"):
                continue
            try:
                src = open(os.path.join(dirpath, name), encoding="utf-8",
                           errors="replace").read()
            except OSError:
                continue
            for m in rx.finditer(code_only(src)):
                live.add(m.group(1))
                live.add(m.group(1)[0].lower() + m.group(1)[1:])   # Accessor -> field
    return live


def scan_assets(assets_dir):
    """(asset path, script guid, field, enabled, value, lo, hi, element) per block."""
    out = []
    for dirpath, _dirs, files in os.walk(assets_dir):
        for name in files:
            if not name.endswith(".asset"):
                continue
            path = os.path.join(dirpath, name)
            try:
                text = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if "Enabled:" not in text:
                continue
            gm = re.search(r"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})", text)
            guid = gm.group(1) if gm else None
            for m in BLOCK_RE.finditer(text):
                _i, field, enabled, value, lo, hi, element = m.groups()
                out.append((os.path.relpath(path, ROOT), guid, field, enabled == "1",
                            float(value), float(lo), float(hi), int(element)))
    return out


def findings(scripts_dir, assets_dir):
    decls = scan_declarations(scripts_dir)
    live = scan_evaluations(scripts_dir)
    guids = script_guids(scripts_dir)
    out = []
    for path, guid, field, enabled, _value, lo, hi, element in scan_assets(assets_dir):
        if not enabled or lo == hi:
            continue                      # nothing claimed
        if field in live:
            continue                      # somebody evaluates it
        host = guids.get(guid) if guid else None
        if host is None:
            continue                      # an asset whose script this tree does not own
        so = decls.get((host, field))
        if so is not True:
            continue                      # not declared here, or a MonoBehaviour (bound path)
        out.append({
            "asset": path, "field": field, "lo": lo, "hi": hi,
            "element": ELEMENTS.get(element, str(element)), "declared": [host],
        })
    return sorted(out, key=lambda f: (f["asset"], f["field"]))


def report(found):
    if not found:
        print("check_elemental_floats: OK — every enabled ElementalFloat on a "
              "ScriptableObject is read through EvaluateLive.")
        return
    print("check_elemental_floats: FOUND "
          f"{len(found)} ElementalFloat(s) authored Enabled that nothing can evaluate\n")
    for f in found:
        print(f"  {f['asset']}")
        print(f"      {f['field']}: {f['element']} {f['lo']:g} -> {f['hi']:g}")
        print(f"      declared on a ScriptableObject in {', '.join(f['declared'])}")
        print("      and read as .Value, so the ramp never runs. Either evaluate it with "
              "EvaluateLive(status)")
        print("      (a BALANCE change — say so) or author Enabled: 0 so the data stops "
              "claiming it.\n")


SELF_TEST_CS = {
    # host kind, source
    "InertOnSO.cs": """
[CreateAssetMenu] public class InertOnSO : ShipActionSO {
    [SerializeField] ElementalFloat deadRamp = new(4f);
    [SerializeField] ElementalFloat sharedName = new(4f);
    public float DeadRamp => deadRamp.Value;
    public float Shared => sharedName.Value;
}""",
    "LiveOnSO.cs": """
[CreateAssetMenu] public class LiveOnSO : ShipActionSO {
    [SerializeField] ElementalFloat liveRamp = new(1f);
    public float Live(IVesselStatus s) => liveRamp.EvaluateLive(s);
}""",
    "ReplicatedOnSO.cs": """
[CreateAssetMenu] public class ReplicatedOnSO : ShipActionSO {
    [SerializeField] ElementalFloat netRamp = new(1f);
    public float Net(IVesselStatus s) => netRamp.EvaluateReplicated(s);
}""",
    "BoundOnComponent.cs": """
public class BoundOnComponent : ElementalShipComponent {
    [SerializeField] ElementalFloat boundRamp = new(1f);
    [SerializeField] ElementalFloat sharedName = new(1f);
    public float Bound => boundRamp.Value;
}""",
}


def _block(field, enabled, value, lo, hi, element):
    return (f"  {field}:\n    Enabled: {enabled}\n    Value: {value}\n"
            f"    Min: {lo}\n    Max: {hi}\n    element: {element}\n")


def _asset(guid, block):
    return ("%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" + block)


SELF_TEST_GUIDS = {
    "InertOnSO.cs":        "a" * 32,
    "LiveOnSO.cs":         "b" * 32,
    "ReplicatedOnSO.cs":   "d" * 32,
    "BoundOnComponent.cs": "c" * 32,
}

SELF_TEST_ASSETS = {
    # must FIRE: enabled, a real ramp, SO-hosted, read as .Value
    "inert.asset": _asset("a" * 32, _block("deadRamp", 1, 4, 4, 8, 2)),
    # must NOT fire: somebody calls EvaluateLive on it
    "live.asset":  _asset("b" * 32, _block("liveRamp", 1, 1, 1, 2.5, 2)),
    # must NOT fire: EvaluateReplicated is the same read against the replicated level
    "net.asset":   _asset("d" * 32, _block("netRamp", 1, 5, 5, 15, 2)),
    # must NOT fire: MonoBehaviour host, so the legacy bound path keeps .Value in step
    "bound.asset": _asset("c" * 32, _block("boundRamp", 1, 1, 1, 2, 4)),
    # must NOT fire: authored off
    "off.asset":   _asset("a" * 32, _block("deadRamp", 0, 4, 4, 8, 2)),
    # must NOT fire: claims nothing (Min == Max)
    "flat.asset":  _asset("a" * 32, _block("deadRamp", 1, 4, 4, 4, 2)),
    # must NOT fire: the field name collides with a MonoBehaviour's, but the ASSET says which
    # script it is -- the hole a name-keyed check had, reproduced here so it stays closed.
    "collide.asset": _asset("a" * 32, _block("sharedName", 1, 4, 4, 8, 2)),
}


def self_test():
    import shutil
    import tempfile
    tmp = tempfile.mkdtemp(prefix="ef-selftest-")
    try:
        cs_dir = os.path.join(tmp, "scripts")
        as_dir = os.path.join(tmp, "assets")
        os.makedirs(cs_dir)
        os.makedirs(as_dir)
        for name, src in SELF_TEST_CS.items():
            open(os.path.join(cs_dir, name), "w", encoding="utf-8").write(src)
            open(os.path.join(cs_dir, name + ".meta"), "w", encoding="utf-8").write(
                f"fileFormatVersion: 2\nguid: {SELF_TEST_GUIDS[name]}\n")
        for name, body in SELF_TEST_ASSETS.items():
            open(os.path.join(as_dir, name), "w", encoding="utf-8").write(body)

        got = findings(cs_dir, as_dir)
        hit = {(os.path.basename(f["asset"]), f["field"]) for f in got}
        want = {("inert.asset", "deadRamp"), ("collide.asset", "sharedName")}
        ok = hit == want
        print("self-test:", "PASS" if ok else "FAIL")
        if not ok:
            print("  expected exactly:", sorted(want))
            print("  got:             ", sorted(hit))
        else:
            print("  both real shapes fire (a dead ramp, and one whose field name also "
                  "exists on a")
            print("  MonoBehaviour); five negative controls -- evaluated live, evaluated "
                  "replicated,")
            print("  MonoBehaviour-hosted, authored off, Min == Max -- all stay silent.")
        return 0 if ok else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 on any finding")
    ap.add_argument("--self-test", action="store_true", help="run the negative controls")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    if not os.path.isdir(SCRIPTS) or not os.path.isdir(ASSETS):
        print(f"check_elemental_floats: ERROR — cannot find the project at {ROOT}",
              file=sys.stderr)
        return 2

    # A check that scanned nothing reports OK, which is indistinguishable from a clean tree.
    # This gate shipped with ROOT off by one directory and passed vacuously until a live
    # negative control caught it, so it now states what it read.
    blocks = scan_assets(ASSETS)
    if not blocks:
        print("check_elemental_floats: ERROR — scanned 0 ElementalFloat blocks under "
              f"{os.path.relpath(ASSETS, ROOT)}; the scan is broken, not the tree.",
              file=sys.stderr)
        return 2

    found = findings(SCRIPTS, ASSETS)
    print(f"scanned {len(blocks)} serialized ElementalFloat block(s)")
    report(found)
    return 1 if (args.check and found) else 0


if __name__ == "__main__":
    sys.exit(main())

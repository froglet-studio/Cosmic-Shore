#!/usr/bin/env python3
"""No vessel leaves a shared ASSET channel empty that every other vessel wires.

    python3 Tools/Build/check_vessel_shared_channels.py             # fail on an unwired channel
    python3 Tools/Build/check_vessel_shared_channels.py --self-test # prove it fires

The fleet's cross-system wiring is almost entirely SOAP events and config SOs — one asset per
channel, referenced identically by every hull — and the platform's policy is to FAIL LOUD on a
missing one rather than guard it (CLAUDE.md: "Do not add if-null guards on ScriptableEvent
serialized fields"). That is the right call and it has a cost: an unwired channel is a
NullReferenceException at the moment something subscribes, and on a vessel that is INSIDE the
spawn or the swap, where the only thing the console says is the name of a method in flight code.

The Butterfly shipped with SEVEN empty: `R_VesselActionHandler._onButtonPressed` /
`_onButtonReleased` / `onAbilityExecuted`, `VesselTransformer.boostChanged`, `AIPilot.cellData` /
`OnCellItemsUpdated`, `VesselCameraCustomizer.OnInitializePlayerCamera`. What reached the player
was a hull that could rotate, had no throttle and whose triggers did nothing — every symptom
pointing at flight code, none at the seven empty fields. A hand-written list of "the references a
new vessel needs" is what let that happen, so this derives the list from the fleet instead.

The rule is UNANIMITY-EXCEPT-YOU, which is what keeps it quiet: a vessel is reported only when
it is the ONLY carrier of that component leaving the field empty and at least two others wire it.
Two hulls agreeing is a coincidence; the whole rest of the fleet agreeing is a contract. A field
several hulls legitimately leave empty is a per-vessel question this file cannot answer, so it
says nothing - which is the difference between a gate people read and a gate people mute.

Scope: `{fileID: N, guid: G, type: T}` — a reference to a project ASSET. A reference to a
prefab-internal object has no guid and is deliberately out of scope: it cannot be shared between
hulls, so its absence is a per-vessel question this file cannot answer.
"""
import glob
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VESSEL_DIR = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")

ASSET_REF = re.compile(r"^\{fileID: \d+, guid: [0-9a-f]{32}, type: \d\}$")
EMPTY_REF = "{fileID: 0}"
# Unity's own prefab bookkeeping, on every MonoBehaviour and never wired by anyone.
BOOKKEEPING = {"m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
               "m_Script", "m_Icon"}


def script_names():
    out = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets", "**", "*.cs.meta"), recursive=True):
        with io.open(meta, encoding="utf-8", errors="ignore") as fh:
            m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
        if m:
            out[m.group(1)] = os.path.basename(meta)[: -len(".cs.meta")]
    return out


def components(text, names):
    """[(component_name, {field: value})] for every MonoBehaviour block in a prefab."""
    out = []
    for m in re.finditer(r"^--- !u!114 &\d+\n(.*?)(?=\n--- |\Z)", text, re.S | re.M):
        body = m.group(1)
        g = re.search(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})", body)
        if not g:
            continue
        # Unity wraps a long reference over two lines; join them before reading fields.
        joined = re.sub(r",\n\s+type: (\d)\}", r", type: \1}", body)
        fields = {}
        for line in joined.splitlines():
            f = re.match(r"^  (\w+): (\{fileID:[^\n]*\})$", line)
            if f and f.group(1) not in BOOKKEEPING:
                fields[f.group(1)] = f.group(2)
        out.append((names.get(g.group(1), g.group(1)), fields))
    return out


def audit(vessels):
    """vessels: {vessel_name: [(component, {field: value})]}."""
    wired, empty = {}, {}          # (component, field) -> {vessel: value} / {vessel}
    for vessel, comps in vessels.items():
        for comp, fields in comps:
            for field, value in fields.items():
                key = (comp, field)
                if ASSET_REF.match(value):
                    wired.setdefault(key, {})[vessel] = value
                elif value == EMPTY_REF:
                    empty.setdefault(key, set()).add(vessel)

    findings = []
    for key, missing in sorted(empty.items()):
        have = wired.get(key, {})
        # EVERY OTHER CARRIER WIRES IT, and there are at least two of them. Two vessels agreeing
        # is a coincidence; the whole rest of the fleet agreeing is a contract. Anything short of
        # that - a field several hulls legitimately leave empty - is a per-vessel question this
        # file cannot answer, so it says nothing.
        if len(missing) != 1 or len(have) < 2:
            continue
        vessel = next(iter(missing))
        findings.append(
            f"{vessel}: {key[0]}.{key[1]} is empty - wired on all {len(have)} other "
            f"vessel(s) that carry {key[0]} (e.g. {sorted(have)[0]})")
    return findings


def self_test():
    ref = "{fileID: 11400000, guid: " + "a" * 32 + ", type: 2}"
    def h(v): return [("R_VesselActionHandler", {"_onButtonPressed": v})]
    assert audit({"A": h(ref), "B": h(ref)}) == []
    # the shipped shape: every other carrier wires it, one does not
    bad = audit({"A": h(ref), "B": h(ref), "C": h(EMPTY_REF)})
    assert len(bad) == 1 and bad[0].startswith("C:"), bad
    # a field NOBODY wires is not a shared channel
    assert audit({"A": [("X", {"f": EMPTY_REF})], "B": [("X", {"f": EMPTY_REF})]}) == []
    # a field TWO hulls leave empty is not a contract - silence, not two findings
    assert audit({"A": [("X", {"f": ref})], "B": [("X", {"f": EMPTY_REF})],
                  "C": [("X", {"f": EMPTY_REF})]}) == []
    # one carrier wiring it is a coincidence, not a contract
    assert audit({"A": [("X", {"f": ref})], "B": [("X", {"f": EMPTY_REF})]}) == []
    # a component only ONE vessel carries can never be reported
    assert audit({"A": [("Y", {"f": EMPTY_REF})]}) == []
    # a prefab-internal reference (no guid) is out of scope
    assert audit({"A": [("Z", {"f": "{fileID: 123}"})], "B": [("Z", {"f": "{fileID: 9}"})],
                  "C": [("Z", {"f": EMPTY_REF})]}) == []
    print("self-test OK (1 clean, 1 fault, 5 non-faults)")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()

    names = script_names()
    with io.open(CONTAINER, encoding="utf-8") as fh:
        registered = set(re.findall(r"- \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}", fh.read()))

    vessels = {}
    for meta in sorted(glob.glob(os.path.join(VESSEL_DIR, "*.prefab.meta"))):
        with io.open(meta, encoding="utf-8") as fh:
            guid = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M).group(1)
        if guid not in registered:
            continue                                  # only hulls the fleet actually flies
        path = meta[: -len(".meta")]
        with io.open(path, encoding="utf-8", errors="ignore") as fh:
            vessels[os.path.basename(path)] = components(fh.read(), names)

    findings = audit(vessels)
    if findings:
        print("FAIL: shared asset channels left empty on a vessel:")
        for f in findings:
            print("  - " + f)
        return 1
    print(f"OK: {len(vessels)} vessels, every unanimous shared channel wired.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

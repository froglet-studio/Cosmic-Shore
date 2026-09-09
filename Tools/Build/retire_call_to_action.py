#!/usr/bin/env python3
"""Retire the call-to-action badge surface from every scene and prefab that carries it.

WHY IT IS BEING RETIRED (Docs/UI_ARCHITECTURE_AUDIT.md F7, product decision 2026-09-08).

The badges were wired into game cards, hangar cards and Arcade tabs, and could never
light. The audit called it "the server fetch is a TODO"; measured, the failure is deeper
and is not one a TODO would fix:

  * `QuestSystem` - the ONLY caller of `CallToActionSystem.AddCallToAction` - is in no
    scene and no prefab. Neither is `TutorialFlowController`, the only subscriber to the
    CTA click event. `UserActionSystem`, which drives DISMISSAL, lives on
    `DailyChallengeSystem.prefab`, the inert PlayFab-era cluster.
  * The addressing data is stale past repair. On the live 16-mode roster, SEVEN modes
    share `404 = PlayGameRampage`, four carry `0` (not a member - `None` is -1), Wildlife
    Liberation points at `PlayGameWildlifeBlitz`, and only SkimRace / Joust / Scurry are
    right. None of the 13 modes added since the enum was written has an entry at all.
  * `CallToActionTargetType` encodes the pre-2026 roster: it names DolphinDarts,
    BlockBandit, Multipass and a dozen other retired modes, and none of the current ones.

So "seed it locally" was never a one-line change - it was a feature build on a dead
foundation, and a future "new mode unlocked" badge wants designing against the roster
that exists. The code is recoverable from git; this removes the surface.

WHAT IT REMOVES, and the safety property that makes it safe:
  * every `CallToActionTarget` component, and its `CallToActionIndicator` GameObject.
    Every one of those indicators ships `m_IsActive: 0` and is a LEAF (verified before
    each delete), so removing the driver cannot leave a badge stuck on - which is the one
    way this retirement could have made things worse rather than better.
  * the `CallToActionManager` prefab instance from Bootstrap.
  * serialized references and prefab-instance overrides left pointing at either.

Idempotent: re-running prints "already retired" and exits 0.

    python3 Tools/Build/retire_call_to_action.py [--check]

--check exits 1 if the retirement has not been applied (for CI).
"""
from __future__ import annotations

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

TARGET_SCRIPT_GUID = "f3f9680b50bcba24981b16c041b793da"   # CallToActionTarget.cs
INDICATOR_NAME = "CallToActionIndicator"
INDICATOR_PREFAB = "Assets/_Prefabs/UI Elements/CallToActionIndicator.prefab"

ASSETS = [
    "Assets/_Scenes/Menu_Main.unity",
    "Assets/_Prefabs/UI Elements/GameCard.prefab",
    "Assets/_Prefabs/UI Elements/HangarScreen/HangarVesselSelectionCard.prefab",
    "Assets/_Prefabs/UI Elements/HangarScreen/Hangar Screen 2.0.prefab",
    "Assets/_Prefabs/UI Elements/HangarScreen/Hangar Screen 2.0 1.prefab",
    "Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/Screens.prefab",
    "Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab",
    "Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab",
]
BOOTSTRAP = "Assets/_Scenes/Bootstrap.unity"
MANAGER_PREFAB = "Assets/_Prefabs/CORE/CallToActionManager.prefab"

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)(.*)$")


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as fh:
        return fh.read()


def write(rel: str, text: str) -> None:
    with open(os.path.join(ROOT, rel), "w", encoding="utf-8") as fh:
        fh.write(text)


def split_docs(text: str):
    """(header, [(class, fileID, suffix, body)]) - body EXCLUDES the '--- ' line."""
    lines = text.splitlines(keepends=True)
    header, docs, cur = [], [], None
    for line in lines:
        m = DOC_RE.match(line.rstrip("\n"))
        if m:
            if cur:
                docs.append(cur)
            cur = [m.group(1), m.group(2), m.group(3), ""]
        elif cur is None:
            header.append(line)
        else:
            cur[3] += line
    if cur:
        docs.append(cur)
    return "".join(header), docs


def join(header: str, docs) -> str:
    out = [header]
    for cls, fid, suffix, body in docs:
        out.append(f"--- !u!{cls} &{fid}{suffix}\n{body}")
    return "".join(out)


def guid_of(rel: str) -> str:
    m = re.search(r"^guid: (\w+)", read(rel + ".meta"), re.M)
    return m.group(1) if m else ""


def retire_file(rel: str) -> "tuple[int, int]":
    """Returns (components removed, indicator objects removed)."""
    header, docs = split_docs(read(rel))
    by_id = {d[1]: d for d in docs}

    # 1. The badge components.
    comps = [d for d in docs
             if d[0] == "114" and f"guid: {TARGET_SCRIPT_GUID}" in d[3]]
    doomed = {d[1] for d in comps}

    # 2. Their indicator objects. Named rather than followed from the component's field,
    #    because an indicator whose component was already removed by hand would otherwise
    #    survive - and a leftover slot is exactly what this retirement is removing.
    indicators = [d for d in docs
                  if d[0] == "1" and re.search(rf"m_Name: {INDICATOR_NAME}\s*$", d[3], re.M)]
    for go in indicators:
        comp_ids = re.findall(r"component: \{fileID: (-?\d+)\}", go[3])
        # Leaf check. A subtree delete is a different and much riskier operation; if one
        # of these ever grows a child, fail loudly rather than orphan it.
        for cid in comp_ids:
            c = by_id.get(cid)
            if c and c[0] in ("4", "224"):
                kids = re.search(r"m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", c[3])
                n = len(re.findall(r"fileID: (-?\d+)", kids.group(1))) if kids else 0
                if n:
                    raise SystemExit(
                        f"error: {rel}: {INDICATOR_NAME} &{go[1]} has {n} child(ren). "
                        f"This tool only deletes leaves - resolve by hand.")
        doomed.add(go[1])
        doomed.update(comp_ids)

    # 2b. Nested INSTANCES of the indicator prefab. A by-name scan cannot find these: a
    #     nested instance is a PrefabInstance doc carrying an m_Name MODIFICATION, not a
    #     GameObject doc with an m_Name field, so the first version of this tool removed
    #     54 plain indicators from Menu_Main and left 5 prefabs' worth of nested ones
    #     untouched. Same class of miss GAMECANVAS.md records for override scanning.
    ind_guid = guid_of(INDICATOR_PREFAB) if os.path.isfile(
        os.path.join(ROOT, INDICATOR_PREFAB + ".meta")) else None
    if ind_guid:
        for d in docs:
            if d[0] == "1001" and f"m_SourcePrefab: {{fileID: 100100000, guid: {ind_guid}" in d[3]:
                doomed.add(d[1])
        for d in docs:
            if "stripped" in d[2] and any(
                    f"m_PrefabInstance: {{fileID: {i}}}" in d[3] for i in doomed):
                doomed.add(d[1])

    if not doomed and "CallToActionTarget: {fileID:" not in read(rel):
        return 0, 0

    # 3. Drop the doomed documents.
    kept = [d for d in docs if d[1] not in doomed]

    # 4. Unlink them from every surviving document: component lists, child lists, and any
    #    serialized reference (a null is what Unity would leave, and it keeps the YAML
    #    parseable where dropping the key outright could empty a required sequence).
    for d in kept:
        body = d[3]
        body = re.sub(r"^\s*- component: \{fileID: (?:" + "|".join(map(re.escape, doomed))
                      + r")\}\n", "", body, flags=re.M)
        body = re.sub(r"^\s*- \{fileID: (?:" + "|".join(map(re.escape, doomed))
                      + r")\}\n", "", body, flags=re.M)
        body = re.sub(r"\{fileID: (?:" + "|".join(map(re.escape, doomed)) + r")\}",
                      "{fileID: 0}", body)
        # The serialized key of HangarVesselSelectNavLink's deleted field. Unity drops an
        # unknown key on its next write, but only for assets it happens to re-serialize.
        body = re.sub(r"^\s*CallToActionTarget: \{fileID: -?\d+\}\n", "", body, flags=re.M)
        d[3] = body

    write(rel, join(header, kept))
    # Count the nested INSTANCES too, or the tool under-reports its own work: the run that
    # removed 26 nested indicators from a prefab printed "0 indicator objects".
    nested = sum(1 for d in docs if d[0] == "1001" and d[1] in doomed)
    write(rel, join(header, kept))
    return len(comps), len(indicators) + nested


def retire_bootstrap() -> int:
    """Remove the CallToActionManager prefab instance from Bootstrap."""
    # The prefab (and its .meta, which carries the guid the instance is matched by) may
    # already be deleted - by an earlier run, or by the code half of the retirement. With
    # no guid there is nothing to match, and Bootstrap was cleaned when there was.
    if not os.path.isfile(os.path.join(ROOT, MANAGER_PREFAB + ".meta")):
        return 0
    guid = guid_of(MANAGER_PREFAB)
    header, docs = split_docs(read(BOOTSTRAP))
    inst = [d for d in docs if d[0] == "1001" and f"guid: {guid}" in d[3]]
    if not inst:
        return 0
    doomed = {d[1] for d in inst}
    # Stripped transforms belong to their instance and go with it.
    for d in docs:
        if "stripped" in d[2] and any(f"m_PrefabInstance: {{fileID: {i}}}" in d[3]
                                      for i in doomed):
            doomed.add(d[1])
    kept = [d for d in docs if d[1] not in doomed]
    pat = "|".join(map(re.escape, doomed))
    for d in kept:
        d[3] = re.sub(r"^\s*- \{fileID: (?:" + pat + r")\}\n", "", d[3], flags=re.M)
        d[3] = re.sub(r"\{fileID: (?:" + pat + r")\}", "{fileID: 0}", d[3])
    write(BOOTSTRAP, join(header, kept))
    return len(inst)


def remaining() -> "list[str]":
    """Every asset still carrying the surface."""
    out = []
    for rel in ASSETS:
        if not os.path.isfile(os.path.join(ROOT, rel)):
            continue
        text = read(rel)
        if (TARGET_SCRIPT_GUID in text
                or re.search(rf"m_Name: {INDICATOR_NAME}\s*$", text, re.M)
                or "CallToActionTarget: {fileID:" in text
                or (os.path.isfile(os.path.join(ROOT, INDICATOR_PREFAB + ".meta"))
                    and guid_of(INDICATOR_PREFAB) in text)):
            out.append(rel)
    # Bootstrap carries the MANAGER, not a badge - a different signature, and checking it
    # for the badge guid would report Bootstrap clean while the singleton still spawns.
    if os.path.isfile(os.path.join(ROOT, MANAGER_PREFAB)) and \
            guid_of(MANAGER_PREFAB) in read(BOOTSTRAP):
        out.append(BOOTSTRAP + " (CallToActionManager instance)")
    for prefab in (MANAGER_PREFAB, INDICATOR_PREFAB):
        if os.path.isfile(os.path.join(ROOT, prefab)):
            out.append(prefab + " (prefab asset still present)")
    if os.path.isdir(os.path.join(ROOT, "Assets/_Scripts/System/CallToAction")):
        out.append("Assets/_Scripts/System/CallToAction/ (code still present)")
    return out


def main() -> int:
    check = "--check" in sys.argv
    left = remaining()

    if check:
        if left:
            print("call-to-action retirement: NOT APPLIED", file=sys.stderr)
            for rel in left:
                print(f"  still carries it: {rel}", file=sys.stderr)
            return 1
        print("call-to-action retirement: OK (surface is gone)")
        return 0

    if not left:
        print("already retired")
        return 0

    total_c = total_i = 0
    for rel in ASSETS:
        if not os.path.isfile(os.path.join(ROOT, rel)):
            continue
        c, i = retire_file(rel)
        if c or i:
            print(f"  {rel}: {c} component(s), {i} indicator object(s)")
        total_c += c
        total_i += i
    n = retire_bootstrap()
    if n:
        print(f"  {BOOTSTRAP}: {n} CallToActionManager instance(s)")
    print(f"\nremoved {total_c} components and {total_i} indicator objects.")
    print("The C# files, the enum and CallToActionManager.prefab are deleted separately "
          "(git rm) - this tool owns the scene/prefab half only.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

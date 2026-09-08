#!/usr/bin/env python3
"""Audit every PERSISTENT UnityEvent listener whose class carries an [Inject] field.

WHY THIS EXISTS
---------------
`UnityEvent.Invoke` builds its call list as PERSISTENT-then-RUNTIME
(`InvokableCallList.PrepareInvoke`: `m_ExecutingCalls = m_PersistentCalls + m_RuntimeCalls`)
and guards neither.  So a persistent listener that throws is a KILL SWITCH for every runtime
listener behind it -- the button still highlights, still reports `interactable`, still passes a
raycast, and does nothing.

That shipped.  `MenuAudio.PlayAudio` dereferences an `[Inject] AudioSystem` and is the one
persistent onClick listener on every arcade `GameCard`.  `ArcadeExploreView.EnsureGridCapacity`
creates a card row at RUNTIME; Reflex injects objects present at SCENE LOAD, so that field was
null there, `PlayAudio` threw, and `() => SelectGame(game)` never ran.  The 13th arcade card
rendered perfectly and opened nothing, and it took five rounds to find because nothing about the
authored asset is different -- the difference exists only at runtime.

So the rule is:

    A class wired as a PERSISTENT listener must never dereference an injected field without a
    fallback, because it cannot know whether the object it lives on was injected.

This audit does not try to prove the dereference is unguarded -- that needs dataflow.  It reports
the SURFACE: every (class, method) pair that is both persistent-wired and injection-dependent.
Today that surface is small enough to review once and freeze, which is what the allow-list is.

Adding a pair means answering one question: if this object were never injected, does the press
still do its job?  If yes, add it.  If no, fix the class (fall back and warn, like `MenuAudio`)
or the creating site (`GameObjectInjector.InjectRecursive`, like `ArcadeExploreView`,
`ProfileIconSelectView` and `ProjectilePoolManager`).

USAGE
    python3 Tools/Build/audit_persistent_listener_injection.py           # report
    python3 Tools/Build/audit_persistent_listener_injection.py --check   # fail on a new pair
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")

# Pairs reviewed and accepted: each of these still does its job on an un-injected object,
# because the class falls back (MenuAudio -> AudioSystem.Instance, warn once) or because the
# injected field is not on the path the listener takes.  Re-review before extending.
# REVIEWED: checked by hand, and the class now works with the injected field null.
REVIEWED = {
    # Falls back to AudioSystem.Instance and warns once (Assets/_Scripts/UI/MenuAudio.cs).
    ("MenuAudio", "PlayAudio"),
}

# BASELINE: the set that already existed when this audit was written. FROZEN, NOT REVIEWED --
# each of these is still a class carrying an [Inject] field wired as a persistent listener, and
# any one of them can eat the runtime listeners behind it on an object nobody injected. They are
# here so --check ratchets (it passes today and fails on anything NEW) rather than presenting a
# wall of pre-existing debt that gets switched off. Burning this list down is real work: for each,
# ask "if this object were never injected, does the control still do its job?", harden or inject
# accordingly, then move the pair up into REVIEWED.
BASELINE = {
    ("ArcadeExploreView", "PlaySelectedGame"),
    ("ArcadeGameConfigureModal", "ModalWindowOut"),
    ("ArcadeGameConfigureModal", "OnBackFromGameSelectView"),
    ("ArcadeGameConfigureModal", "OnBackFromSquadMateSelectionClicked"),
    ("ArcadeGameConfigureModal", "OnBackFromVesselSelectionClicked"),
    ("ArcadeGameConfigureModal", "OnConfirmConfiguration"),
    ("ArcadeGameConfigureModal", "OnNextShipClicked"),
    ("ArcadeGameConfigureModal", "OnPreviousShipClicked"),
    ("ArcadeGameConfigureModal", "OnStartGameClicked"),
    ("ArcadeGameConfigureModal", "ToggleFavorite"),
    ("ArcadeLoadoutView", "OnClickPlayButton"),
    ("CaptainUpgradeSelectionCard", "OnClick"),
    ("GameplayRewardButton", "ClaimReward"),
    ("MenuVesselSelectionPanelController", "OnResumeButtonClicked"),
    ("ModalStackEntry", "OnClickArcadeNav"),
    ("ModalStackEntry", "OnClickPortNav"),
    ("ModalWindowManager", "ModalWindowOut"),
    ("NavLink", "OnClick"),
    ("PauseMenu", "OnClickMainMenu"),
    ("PauseMenu", "OnClickResumeGameButton"),
    ("PauseMenu", "Show"),
    ("ProfileIconSelectButton", "OnClick"),
    ("ProfileIconSelectView", "ModalWindowIn"),
    ("ProfileIconSelectView", "ModalWindowOut"),
    ("ProfileIconSelectView", "OpenAvatar"),
    ("ProfileIconSelectView", "OpenDisplayName"),
    ("SceneLoader", "ReturnToMainMenu"),
    ("WeeklyChallengeLeaderboardModal", "ModalWindowIn"),
}

ALLOWED = REVIEWED | BASELINE

GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
INJECT_RE = re.compile(r"^\s*\[Inject\]", re.M)
CLASS_RE = re.compile(r"\b(?:class|struct)\s+([A-Za-z_]\w*)")

# One persistent call entry: the target object, the method, and the serialized type name.
CALL_RE = re.compile(
    r"- m_Target: \{fileID: (?P<target>-?\d+)(?:, guid: (?P<tguid>[0-9a-f]{32}))?[^}]*\}"
    r".*?m_MethodName: (?P<method>\S*)"
    r".*?m_TargetAssemblyTypeName: (?P<type>[^\n]*)",
    re.S,
)


def script_guid_index():
    """guid -> (path, class names declared, declares an [Inject] field)."""
    index = {}
    for base, _dirs, files in os.walk(ASSETS):
        for f in files:
            if not f.endswith(".cs.meta"):
                continue
            meta = os.path.join(base, f)
            src = meta[:-5]
            if not os.path.exists(src):
                continue
            try:
                g = GUID_RE.search(open(meta, encoding="utf-8", errors="replace").read())
                if not g:
                    continue
                text = open(src, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            index[g.group(1)] = (
                os.path.relpath(src, ROOT),
                set(CLASS_RE.findall(text)),
                bool(INJECT_RE.search(text)),
            )
    return index


def scan(index):
    """Every (class, method, where) whose target component's script declares [Inject]."""
    found = {}
    unresolved = set()   # persistent calls whose target is in another asset
    dead = set()         # wirings naming a method the resolved script does not declare
    for base, _dirs, files in os.walk(ASSETS):
        for f in files:
            if not (f.endswith(".unity") or f.endswith(".prefab")):
                continue
            path = os.path.join(base, f)
            try:
                text = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if "m_PersistentCalls" not in text:
                continue

            # fileID -> script guid, for components declared in this same document set.
            local = {}
            for m in re.finditer(r"^--- !u!114 &(\d+)", text, re.M):
                start = m.end()
                end = text.find("--- !u!", start)
                body = text[start:end if end != -1 else len(text)]
                g = re.search(r"m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32})", body)
                if g:
                    local[m.group(1)] = g.group(1)

            for block in re.finditer(r"m_PersistentCalls:\n(.*?)(?=\n  [a-zA-Z_]|\Z)", text, re.S):
                for call in CALL_RE.finditer(block.group(1)):
                    method = call.group("method")
                    if not method:
                        continue
                    guid = local.get(call.group("target"))
                    if not guid:
                        # The target lives in another asset, so its component -- and therefore its
                        # real class -- cannot be resolved from here. m_TargetAssemblyTypeName is
                        # deliberately NOT used as a fallback: Unity resolves a persistent call
                        # from the LIVE target's type (UnityEventBase.FindMethod), and this scene
                        # serialises 190 stale names under a `CosmicShore.App.*` namespace that
                        # exists in zero first-party files. Guessing from it invented pairs like
                        # `MenuAudio.SetActive`, which is not a method on MenuAudio. Counted and
                        # reported, never guessed.
                        unresolved.add((os.path.relpath(path, ROOT), method))
                        continue
                    entry = index.get(guid)
                    if not entry or not entry[2]:
                        continue
                    src, names, _inj = entry

                    # Pick the declared class that actually HAS this method. The serialized type
                    # name is only a tiebreak hint, for the same staleness reason as above.
                    try:
                        body = open(os.path.join(ROOT, src), encoding="utf-8", errors="replace").read()
                    except OSError:
                        body = ""
                    if not re.search(r"\b" + re.escape(method) + r"\s*\(", body):
                        # The wiring names a method this script does not declare -- a dead listener,
                        # not an injection hazard. Reported separately so it is not silently lost.
                        dead.add((sorted(names)[0] if names else "?", method, os.path.relpath(path, ROOT)))
                        continue

                    tn = (call.group("type") or "").split(",")[0].strip()
                    cls = tn.rsplit(".", 1)[-1] if tn else ""
                    if cls not in names:
                        cls = sorted(names)[0] if names else "?"
                    found.setdefault((cls, method), set()).add(os.path.relpath(path, ROOT))
    return found, unresolved, dead


def main():
    check = "--check" in sys.argv
    index = script_guid_index()
    found, unresolved, dead = scan(index)

    unreviewed = {k: v for k, v in found.items() if k not in ALLOWED}

    print("persistent listeners whose class declares [Inject]: %d pair(s)" % len(found))
    for (cls, method) in sorted(found):
        mark = "OK " if (cls, method) in ALLOWED else "NEW"
        wheres = sorted(found[(cls, method)])
        print("  [%s] %s.%s  (%d asset%s, e.g. %s)"
              % (mark, cls, method, len(wheres), "" if len(wheres) == 1 else "s", wheres[0]))

    if unresolved:
        print()
        print("(%d persistent call(s) target a component in another asset and were not resolved -- "
              "not guessed from the stale serialized type name)" % len(unresolved))
    if dead:
        print("(%d persistent call(s) name a method their resolved script does not declare -- "
              "dead wirings, e.g. %s)" % (len(dead), sorted(dead)[0][0] + "." + sorted(dead)[0][1]))

    if not check:
        return 0

    if unreviewed:
        print()
        print("FAIL: %d persistent-listener pair(s) are NEW since this audit's baseline." % len(unreviewed))
        print("A persistent listener runs BEFORE every runtime listener on the same UnityEvent and")
        print("is not guarded, so if it throws on an un-injected object the control silently does")
        print("nothing. Either make the class fall back (see MenuAudio), inject at the creating")
        print("site (GameObjectInjector.InjectRecursive), or add the pair to ALLOWED once you have")
        print("checked the press still works with the injected field null.")
        return 1

    print()
    print("--check: OK, no NEW persistent-listener/[Inject] pair since the baseline "
          "(%d reviewed, %d frozen-but-unreviewed)." % (len(REVIEWED), len(BASELINE)))
    return 0


if __name__ == "__main__":
    sys.exit(main())

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

WHAT THIS AUDIT GOT WRONG FOR ITS WHOLE LIFE (fixed 12 Sep 2026)
---------------------------------------------------------------
It read 279 of the project's 992 persistent calls -- 71.4% invisible -- and of the 279 it did
read, 236 carried the WRONG type name.  One regex caused both:

    r"- m_Target: \{...\}"  r".*?m_MethodName: (?P<method>\S*)"  r".*?m_TargetAssemblyTypeName: ..."

Unity serialises a call as m_Target -> m_TargetAssemblyTypeName -> m_MethodName, i.e. the TYPE
comes BEFORE the method.  Asking for the method first and the type second makes each match run
past its own entry into the NEXT one: every call was labelled with its successor's type name, and
because `finditer` resumes after the match, the successor's own `- m_Target` had already been
consumed, so alternate calls were skipped entirely.

It surfaced as a wrong CLASS NAME, which is why it looked like a naming nit.  With the type name
belonging to a different component, `cls not in names` was true, and the fallback picked the
alphabetically first type declared in the file -- so ToyConfigureModal.ModalWindowOut reported as
`Layer.ModalWindowOut` (a nested struct) and ScreenSwitcher's two nav handlers reported, and were
FROZEN INTO THE BASELINE, as `ModalStackEntry` -- a nested struct that can never be a
MonoBehaviour.

Three rules came out of it, and they generalise past this file:

  * A REGEX OVER SERIALISED YAML MUST NOT ASSUME FIELD ORDER.  Split the list into entries first,
    then read each entry's own fields.  `.*?` across an entry boundary is silent: it does not
    fail, it attributes one record's data to another.
  * COUNT WHAT YOU PARSED AGAINST SOMETHING INDEPENDENT.  `- m_Target: {` and `m_MethodName:` each
    occur exactly 992 times across Assets/; either number would have exposed this on day one.
    --check now asserts that agreement on every run, so the parser cannot silently narrow again.
  * A FALLBACK THAT GUESSES IS A DEFECT AMPLIFIER.  Picking `sorted(names)[0]` turned a parse bug
    into a plausible-looking class name, which is what got two wrong rows written down as fact.
    The resolver now prefers the FILENAME (Unity requires a serialised MonoBehaviour's class to
    match its file), and every remaining guess is counted and reported rather than presented as an
    answer.

The BASELINE therefore GREW at the same commit.  That is the audit finally seeing its input --
not new debt.

USAGE
    python3 Tools/Build/audit_persistent_listener_injection.py             # report
    python3 Tools/Build/audit_persistent_listener_injection.py --check     # fail on a new pair
    python3 Tools/Build/audit_persistent_listener_injection.py --self-test # prove it still fires
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
    # ModalWindowManager.PlayMenuAudio falls back to AudioSystem.Instance and warns once, so a
    # persistent ModalWindowIn/Out on an un-injected modal loses its sting, not its close.
    # Re-checked 12 Sep 2026, because that reason only covered the AUDIO and ToyConfigureModal
    # injects two more things: every use of its `gameData` and `freestyleEvents` is behind a
    # truthiness check, and the close path (ModalWindowOut -> PlayMenuAudio + OnModalClosed ->
    # HandleSelfClosed -> DrawRows) dereferences neither unguarded. The window also lives in
    # Menu_Main at scene load, so Reflex injects it anyway -- the guards are the belt.
    ("ToyConfigureModal", "ModalWindowOut"),
    ("ToyboxModal", "ModalWindowOut"),
}

# BASELINE: the pairs that exist now that the parser can actually READ the assets. FROZEN, NOT
# REVIEWED -- each is still a class carrying an [Inject] field wired as a persistent listener, and
# any one of them can eat the runtime listeners behind it on an object nobody injected. They are
# here so --check ratchets (it passes today and fails on anything NEW) rather than presenting a
# wall of pre-existing debt that gets switched off. Burning this list down is real work: for each,
# ask "if this object were never injected, does the control still do its job?", harden or inject
# accordingly, then move the pair up into REVIEWED.
#
# IT GREW FROM 28 TO 56 ON 12 SEP 2026, AND NOT ONE ROW IS NEW DEBT. The parser was reading 279 of
# 992 persistent calls and mislabelling most of what it did read (see the doc block); these are the
# rows it was blind to. Two things also LEFT the list, and both were artefacts rather than fixes:
#
#   * ("ModalStackEntry", "OnClickArcadeNav") and ("ModalStackEntry", "OnClickPortNav") were
#     ScreenSwitcher all along -- ModalStackEntry is a nested struct in ScreenSwitcher.cs and can
#     never be a MonoBehaviour. They appear below under their real name.
#   * Six ("ArcadeGameConfigureModal", "OnBack*"/"OnConfirmConfiguration"/"On*ShipClicked") rows are
#     now classified as DEAD WIRINGS -- the asset names a method that script does not declare. They
#     are reported separately, because a listener that cannot resolve is not an injection hazard.
#
# Where to start burning it down: the rows whose object is created at RUNTIME are the ones that can
# actually be un-injected, because Reflex injects what is present at scene load. Projectile,
# AOEExplosion and the pooled effects are that shape -- though note ProjectilePoolManager,
# ProjectileDetonatorSO and ExplosionHelper all call GameObjectInjector.InjectRecursive at their
# creating sites, so those may already be safe; that is a check, not an assumption. A row wired
# into a scene or a prefab that ships whole is far less likely to bite.
BASELINE = {
    ("AOEExplosion", "CancelExplosionAndDestroy"),
    ("ArcadeExploreView", "PlaySelectedGame"),
    ("ArcadeGameConfigureModal", "ModalWindowOut"),
    ("ArcadeGameConfigureModal", "OnStartGameClicked"),
    ("ArcadeGameConfigureModal", "OpenMaelstrom"),
    ("ArcadeGameConfigureModal", "ToggleFavorite"),
    ("ArcadeLoadoutView", "OnClickChangeActivePlayerCount"),
    ("ArcadeLoadoutView", "OnClickChangeClass"),
    ("ArcadeLoadoutView", "OnClickChangeGameMode"),
    ("ArcadeLoadoutView", "OnClickPlayButton"),
    ("ArcadeLoadoutView", "OnClickedChangeActiveIntensity"),
    ("ArcadeScreen", "ToggleView"),
    ("AudioSystem", "PlayGameplaySFX"),
    ("CaptainUpgradeSelectionCard", "OnClick"),
    ("DailyRewardCard", "Purchase"),
    ("EpisodeScreen", "HidePanel"),
    ("EpisodeScreen", "ShowPanel"),
    ("GameplayRewardButton", "ClaimReward"),
    ("HangarCaptainsView", "OnClickBuy"),
    ("HangarVesselDetailView", "CloseUnlockPanel"),
    ("MenuCrystalClickHandler", "ToggleTransition"),
    ("MenuMiniGameHUD", "Show"),
    ("MenuVesselSelectionPanelController", "OnCloseButtonClicked"),
    ("MenuVesselSelectionPanelController", "OnResumeButtonClicked"),
    ("MiniGameHUD", "OnPipInitialized"),
    ("MiniGameHUD", "ToggleReadyButton"),
    ("MiniGameHUD", "UpdateTurnMonitorDisplay"),
    ("ModalWindowManager", "ModalWindowOut"),
    ("NavLink", "OnClick"),
    ("PauseMenu", "Hide"),
    ("PauseMenu", "OnClickMainMenu"),
    ("PauseMenu", "OnClickPauseGameButton"),
    ("PauseMenu", "OnClickResumeGameButton"),
    ("PauseMenu", "Show"),
    ("ProfileIconSelectButton", "OnClick"),
    ("ProfileIconSelectView", "ModalWindowIn"),
    ("ProfileIconSelectView", "ModalWindowOut"),
    ("ProfileIconSelectView", "OpenAvatar"),
    ("ProfileIconSelectView", "OpenDisplayName"),
    ("ProfileModal", "CancelPlayerNameChange"),
    ("ProfileModal", "GenerateRandomNameButton_OnClicked"),
    ("ProfileModal", "ShowDisplayNameChangeButtons"),
    ("Projectile", "ReturnToFactory"),
    ("PurchaseConfirmationModal", "Confirm"),
    ("SceneLoader", "ReturnToMainMenu"),
    ("ScreenSwitcher", "OnClickArcadeNav"),
    ("ScreenSwitcher", "OnClickArkNav"),
    ("ScreenSwitcher", "OnClickHangarNav"),
    ("ScreenSwitcher", "OnClickHomeNav"),
    ("ScreenSwitcher", "OnClickLeftArrow"),
    ("ScreenSwitcher", "OnClickPortNav"),
    ("ScreenSwitcher", "OnClickProfileModal"),
    ("ScreenSwitcher", "OnClickProfileNav"),
    ("ScreenSwitcher", "OnClickRightArrow"),
    ("ScreenSwitcher", "OnClickStoreNav"),
    ("WeeklyChallengeLeaderboardModal", "ModalWindowIn"),
}

ALLOWED = REVIEWED | BASELINE

GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
INJECT_RE = re.compile(r"^\s*\[Inject\]", re.M)
CLASS_RE = re.compile(r"\b(?:class|struct)\s+([A-Za-z_]\w*)")

# The START of one persistent call entry. Everything else about the entry is read from the slice
# that follows, because Unity's field ORDER is not ours to assume -- see the doc block.
CALL_START_RE = re.compile(
    r"^[ \t]*- m_Target: \{fileID: (?P<target>-?\d+)(?:, guid: (?P<tguid>[0-9a-f]{32}))?[^}]*\}",
    re.M,
)
METHOD_RE = re.compile(r"^\s*m_MethodName: (?P<method>\S*)\s*$", re.M)
TYPENAME_RE = re.compile(r"^\s*m_TargetAssemblyTypeName: (?P<type>[^\n]*)$", re.M)
CALLSTATE_RE = re.compile(r"^\s*m_CallState:", re.M)

# Comments and string literals, so a sentence like "the routes this class is not covering" cannot
# be read as a type declaration. It could: CLASS_RE found `is` in ToyConfigureModal.cs that way,
# and `is` sorted ahead of the real class under the old alphabetical fallback.
COMMENT_OR_STRING_RE = re.compile(
    r"//[^\n]*|/\*.*?\*/|@\"(?:[^\"]|\"\")*\"|\"(?:\\.|[^\"\\])*\"",
    re.S,
)


def declared_types(source_text):
    """Type names DECLARED in a C# file, with comments and strings removed first."""
    return set(CLASS_RE.findall(COMMENT_OR_STRING_RE.sub(" ", source_text)))


def parse_calls(text):
    """Every persistent call in one asset, as (target_fileID, method, serialized type name).

    Entry-delimited, never field-ordered: each call's fields are read from ITS OWN slice, which
    ends at the next entry or at the `m_CallState` that terminates this one, whichever is first.
    """
    starts = list(CALL_START_RE.finditer(text))
    calls = []
    for i, m in enumerate(starts):
        stop = starts[i + 1].start() if i + 1 < len(starts) else len(text)
        end = CALLSTATE_RE.search(text, m.end(), stop)
        slice_ = text[m.end():end.start() if end else stop]
        method = METHOD_RE.search(slice_)
        typename = TYPENAME_RE.search(slice_)
        calls.append((
            m.group("target"),
            method.group("method") if method else "",
            (typename.group("type") or "").strip() if typename else "",
        ))
    return calls


def resolve_class(stem, names, serialized_type_name):
    """Which declared type this wiring's component actually is.

    FILENAME FIRST. Unity refuses to serialise a MonoBehaviour whose class name does not match its
    file name, so the stem is the only type in the file that can BE a persistent target -- it
    outranks the serialized name, which this scene carries 190 stale copies of. The alphabetical
    fallback is kept only for a file that declares nothing matching either, and the caller counts
    those: it is a guess, and the last time a guess went unlabelled it was written into BASELINE.
    """
    if stem in names:
        return stem, False
    short = (serialized_type_name or "").split(",")[0].strip().rsplit(".", 1)[-1]
    if short and short in names:
        return short, False
    return (sorted(names)[0] if names else "?"), True


def script_guid_index():
    """guid -> (path, declared type names, declares an [Inject] field, filename stem)."""
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
                declared_types(text),
                bool(INJECT_RE.search(text)),
                os.path.splitext(f)[0][:-3],   # "Foo.cs.meta" -> "Foo"
            )
    return index


def scan(index):
    """Every (class, method, where) whose target component's script declares [Inject].

    Also returns the TOTAL number of persistent calls parsed, which --check cross-checks against
    an independent count of the raw markers. That agreement is the only thing standing between
    this audit and the silent 71.4% blind spot it shipped with.
    """
    found = {}
    unresolved = set()   # persistent calls whose target is in another asset
    dead = set()         # wirings naming a method the resolved script does not declare
    guessed = set()      # class resolved by neither filename nor serialized name -- a guess
    parsed = 0
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
                start_ = m.end()
                end_ = text.find("--- !u!", start_)
                body = text[start_:end_ if end_ != -1 else len(text)]
                g = re.search(r"m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32})", body)
                if g:
                    local[m.group(1)] = g.group(1)

            for target, method, typename in parse_calls(text):
                parsed += 1
                if not method:
                    continue
                guid = local.get(target)
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
                src, names, _inj, stem = entry

                try:
                    body = open(os.path.join(ROOT, src), encoding="utf-8", errors="replace").read()
                except OSError:
                    body = ""
                if not re.search(r"\b" + re.escape(method) + r"\s*\(", body):
                    # The wiring names a method this script does not declare -- a dead listener,
                    # not an injection hazard. Reported separately so it is not silently lost.
                    dead.add((stem or "?", method, os.path.relpath(path, ROOT)))
                    continue

                cls, is_guess = resolve_class(stem, names, typename)
                if is_guess:
                    guessed.add((cls, method, os.path.relpath(path, ROOT)))
                found.setdefault((cls, method), set()).add(os.path.relpath(path, ROOT))
    return found, unresolved, dead, guessed, parsed


def count_raw_calls():
    """An INDEPENDENT count of persistent calls: the raw `- m_Target: {` markers.

    Deliberately not the parser's own logic. A parser that narrows is invisible to every check
    written in terms of that parser -- which is exactly how 71.4% of this audit's input went
    unread for its whole life.
    """
    total = 0
    marker = re.compile(r"^[ \t]*- m_Target: \{", re.M)
    for base, _dirs, files in os.walk(ASSETS):
        for f in files:
            if not (f.endswith(".unity") or f.endswith(".prefab")):
                continue
            try:
                text = open(os.path.join(base, f), encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if "m_PersistentCalls" not in text:
                continue
            total += len(marker.findall(text))
    return total


def self_test():
    """Negative controls. A gate nobody has watched fail is a gate nobody should trust -- and this
    one spent its whole life failing WRONGLY, which is worse, because a wrong answer looks like an
    answer. Each case below reproduces one of the three defects fixed on 12 Sep 2026."""
    failures = []

    def ok(label):
        print("  ok  " + label)

    def expect(label, got, want):
        if got != want:
            failures.append("%s: expected %r, got %r" % (label, want, got))
        else:
            ok("%s -> %r" % (label, got))

    # 1. FIELD ORDER. Unity writes m_TargetAssemblyTypeName BEFORE m_MethodName. A parser that
    #    reads the method first and the type second attributes each call's type to its successor
    #    AND swallows every other call. This is the real Menu_Main shape, two calls in one list.
    yaml = (
        "  m_OnClick:\n"
        "    m_PersistentCalls:\n"
        "      m_Calls:\n"
        "      - m_Target: {fileID: 8758355337821682526}\n"
        "        m_TargetAssemblyTypeName: CosmicShore.UI.ToyConfigureModal, Assembly-CSharp\n"
        "        m_MethodName: ModalWindowOut\n"
        "        m_Mode: 1\n"
        "        m_CallState: 2\n"
        "      - m_Target: {fileID: 1386824072}\n"
        "        m_TargetAssemblyTypeName: MenuAudio, Assembly-CSharp\n"
        "        m_MethodName: PlayAudio\n"
        "        m_Mode: 1\n"
        "        m_CallState: 2\n"
    )
    calls = parse_calls(yaml)
    expect("both calls are parsed (the old parser saw one)", len(calls), 2)
    expect("call 1 keeps its OWN type name",
           (calls[0][1], calls[0][2].split(",")[0]),
           ("ModalWindowOut", "CosmicShore.UI.ToyConfigureModal"))
    expect("call 2 keeps its OWN type name",
           (calls[1][1], calls[1][2].split(",")[0]), ("PlayAudio", "MenuAudio"))

    # The old regex, verbatim, so the failure is demonstrated rather than described.
    old = re.compile(r"- m_Target: \{fileID: (-?\d+)[^}]*\}"
                     r".*?m_MethodName: (\S*)"
                     r".*?m_TargetAssemblyTypeName: ([^\n]*)", re.S)
    old_hits = old.findall(yaml)
    if len(old_hits) == 2 or (old_hits and old_hits[0][2].strip().startswith("CosmicShore")):
        failures.append("negative control did not fire: the OLD regex parsed this correctly")
    else:
        ok("negative control: the old regex saw %d of 2 calls and labelled the first %r"
           % (len(old_hits), old_hits[0][2].strip().split(",")[0] if old_hits else None))

    # A call list indented one level deeper (nested prefab overrides) must parse too -- 96 of the
    # project's 992 calls are at that depth, and an indent-specific split silently drops them.
    expect("a deeper-indented call list still parses",
           len(parse_calls(yaml.replace("      - m_Target", "        - m_Target"))), 2)

    # 2. NESTED TYPES + the filename rule. Unity refuses to serialise a MonoBehaviour whose class
    #    name does not match its file, so the stem is the only candidate that can BE the target.
    expect("nested struct sorts first, filename wins",
           resolve_class("ToyConfigureModal", {"Layer", "ToyConfigureModal", "is"},
                         "CosmicShore.UI.ToyConfigureModal, Assembly-CSharp"),
           ("ToyConfigureModal", False))
    expect("filename wins even when the serialized name is stale",
           resolve_class("ScreenSwitcher", {"ModalStackEntry", "ScreenEntry", "ScreenSwitcher"},
                         "CosmicShore.App.ScreenSwitcher, Assembly-CSharp"),
           ("ScreenSwitcher", False))
    expect("no filename match falls back to the serialized name, and says it is not a guess",
           resolve_class("SomeOtherFile", {"Alpha", "Zeta"}, "Whatever.Zeta, Assembly-CSharp"),
           ("Zeta", False))
    expect("neither matches -> alphabetical, FLAGGED as a guess",
           resolve_class("SomeOtherFile", {"Alpha", "Zeta"}, ""), ("Alpha", True))
    expect("nothing declared -> '?', flagged", resolve_class("X", set(), ""), ("?", True))

    # 3. PROSE IS NOT A DECLARATION. `// ... the routes this class is not covering` made `is` a
    #    declared type, and `is` beat the real class under the old alphabetical fallback.
    src = ('public class ToyConfigureModal : ModalWindowManager {\n'
           '    readonly struct Layer { }\n'
           '    // it is the only place that covers the routes this class is not\n'
           '    string s = "class Fake";\n'
           '}\n')
    expect("comments and strings are not scanned for declarations",
           declared_types(src), {"ToyConfigureModal", "Layer"})
    if "is" not in set(CLASS_RE.findall(src)):
        failures.append("negative control did not fire: the raw regex no longer finds `is`")
    else:
        ok("negative control: the raw regex does read `is` out of that comment")

    # And against the SHIPPED tree, because a synthetic case only proves the function.
    index = script_guid_index()
    for path, want in (("Assets/_Scripts/UI/Modals/ToyConfigureModal.cs", "ToyConfigureModal"),
                       ("Assets/_Scripts/UI/ScreenSwitcher.cs", "ScreenSwitcher")):
        hit = next((v for v in index.values() if v[0] == path), None)
        if hit is None:
            failures.append("%s is not in the script index" % path)
            continue
        expect("shipped: " + os.path.basename(path),
               resolve_class(hit[3], hit[1], "")[0], want)

    if failures:
        print("\nself-test FAILED:")
        for line in failures:
            print("   " + line)
        return 1
    print("\nself-test OK -- field order, nested types and prose all reproduce, and all three fail "
          "under the old logic.")
    return 0


def main():
    if "--self-test" in sys.argv:
        return self_test()

    check = "--check" in sys.argv
    index = script_guid_index()
    found, unresolved, dead, guessed, parsed = scan(index)
    raw = count_raw_calls()

    unreviewed = {k: v for k, v in found.items() if k not in ALLOWED}

    print("persistent calls parsed: %d (independent marker count: %d)" % (parsed, raw))
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
    if guessed:
        print("(%d class name(s) matched neither the filename nor the serialized type and were "
              "GUESSED alphabetically, e.g. %s -- treat as unresolved, not as fact)"
              % (len(guessed), sorted(guessed)[0][0] + "." + sorted(guessed)[0][1]))

    if not check:
        return 0

    # The parser must see everything the raw markers do. This is the check that would have caught
    # the 71.4% blind spot on day one, and it is cheap enough to run every time.
    if parsed != raw:
        print()
        print("FAIL: the parser read %d persistent calls but there are %d in the assets." % (parsed, raw))
        print("A narrowing parser is invisible to every finding written in terms of it -- this audit")
        print("shipped for its whole life reading 279 of 992. Fix parse_calls() before trusting any")
        print("row above.")
        return 1

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

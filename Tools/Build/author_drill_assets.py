#!/usr/bin/env python3
"""Author the microgame drill's seed assets (Docs/ModePreview/TRAINING_PLAN.md §4.2, §4.2.1).

Writes:
  Assets/Resources/DrillLibrary.asset                         the root: Lessons, pacing, chrome text
  Assets/_SO_Assets/Drills/Lessons/LessonTemplate_TwoThumb.asset
  Assets/_SO_Assets/Drills/Lessons/LessonTemplate_OneThumb.asset

The text here is SEED content: every line becomes an ordinary serialized field a writer edits in
the inspector afterwards. So this script authors an asset only while it does not exist, and
`--check` then verifies the shipped assets rather than demanding they still equal the seed - a
human edit is the point, not drift. What `--check` asserts:
  * every asset and folder this script owns exists with its .meta, and references resolve;
  * every player-facing string is ASCII (the UI font has 97 glyphs; anything else is tofu);
  * every {token} in a prompt is one the runtime resolver knows (DrillTokens.IsKnownToken);
  * each Lesson template names its own scheme, and every step id is unique and non-empty.
`--force` re-seeds over existing assets (destroys human edits - only for a fresh branch).
`--self-test` proves the checks fire.

Usage: author_drill_assets.py [--check | --force | --self-test]
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")
assert os.path.isdir(ASSETS), f"Assets/ not under {ROOT}"
SCRIPTS = os.path.join(ASSETS, "_Scripts", "Controller", "Arcade", "Preview", "Drill")

LIBRARY = "Assets/Resources/DrillLibrary.asset"
LESSON_DIR = "Assets/_SO_Assets/Drills/Lessons"
TWO_THUMB = f"{LESSON_DIR}/LessonTemplate_TwoThumb.asset"
ONE_THUMB = f"{LESSON_DIR}/LessonTemplate_OneThumb.asset"
FOLDERS = ["Assets/_SO_Assets/Drills", LESSON_DIR]

# Enum values (asserted against the C# below so a renumbering cannot slip past).
ELEMENT = {"None": 0, "Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
ANCHOR = {"Window": 0, "AbilityRow": 1}
CUE = {"None": 0, "PulseAbilityRow": 1}
APPLIES = {"Always": 0, "HullDrifts": 1, "AbilityHasInput": 2}
SCHEME = {"TwoThumb": 0, "OneThumb": 1}
CONTROL = {"Steer": 0, "Throttle": 1, "Drift": 2}
HINT = {"PadButtonSouth": 1, "PadButtonEast": 3, "PadButtonWest": 4, "PadRightShoulder": 6,
        "PadLeftTrigger": 7, "PadRightTrigger": 8}

KNOWN_TOKEN = re.compile(
    r"^(vessel|mode|(ability|abilityDescription):(Charge|Mass|Space|Time)|"
    r"glyph:(Charge|Mass|Space|Time|Steer|Throttle|Drift))$")
# The ability tip template's element placeholder (DrillLibrarySO.AbilityTipElementPlaceholder).
KNOWN_TEMPLATE_TOKEN = re.compile(r"^(ability|abilityDescription|glyph):E$")

# ── Seed content ────────────────────────────────────────────────────────────────────────────

TIME_STEP = dict(id="time-ability", element="Time", anchor="AbilityRow", cue="PulseAbilityRow",
                 applies="AbilityHasInput",
                 prompt="Press {glyph:Time} to use {ability:Time}.",
                 hint="{ability:Time}: {abilityDescription:Time}",
                 cond=("AbilityActivatedCondition", dict(Element=ELEMENT["Time"], Count=1)))
DRIFT_STEP = dict(id="drift", applies="HullDrifts",
                  prompt="Hold {glyph:Drift} to drift through a turn.",
                  hint="Hold {glyph:Drift} while you turn.",
                  cond=("DriftHeldCondition", dict(Seconds=0.75)))

LESSONS = {
    TWO_THUMB: ("TwoThumb", [
        dict(id="steer", prompt="Push {glyph:Steer} the same way to turn.",
             hint="Both sticks together steer the ship.",
             cond=("SteerHeldCondition", dict(MinDeflection=0.5, Seconds=1))),
        dict(id="throttle", prompt="Pull {glyph:Throttle} apart to speed up.",
             hint="Sticks apart is fast. Sticks together is slow.",
             cond=("ThrottleHeldCondition", dict(MinThrottle=0.8, Seconds=1))),
        TIME_STEP, DRIFT_STEP]),
    ONE_THUMB: ("OneThumb", [
        dict(id="steer", prompt="Steer with {glyph:Steer}.",
             hint="Your ship flies where you point it.",
             cond=("SteerHeldCondition", dict(MinDeflection=0.5, Seconds=1))),
        TIME_STEP, DRIFT_STEP]),
}

STRINGS = dict(
    LessonTitle="LEARN TO FLY",
    MentorTitle="FLIGHT TIPS",
    SkipLabel="SKIP",
    NextLabel="NEXT",
    MentorClosingLine="That is everything. Press Play when you are ready.",
    PlayLockedCaption="Finish the lesson to play",
)
PAD_LABELS = [("PadLeftTrigger", "LT"), ("PadRightTrigger", "RT"), ("PadButtonSouth", "A"),
              ("PadButtonEast", "B"), ("PadButtonWest", "X"), ("PadRightShoulder", "RB")]
# (control, scheme, keyboard, label)
FLIGHT_LABELS = [
    ("Steer", "TwoThumb", False, "both sticks"), ("Steer", "TwoThumb", True, "WASD and PL;'"),
    ("Throttle", "TwoThumb", False, "the sticks"), ("Throttle", "TwoThumb", True, "A and '"),
    ("Drift", "TwoThumb", False, "both triggers"), ("Drift", "TwoThumb", True, "both Shift keys"),
    ("Steer", "OneThumb", False, "the left stick"), ("Steer", "OneThumb", True, "the mouse"),
]
# No one-thumb Drift label on purpose: which control a one-thumb hull drifts on has not been
# verified, and a missing label drops the drift step (an honest absence) where a guessed one
# would teach the wrong button. Add the rows once the binding is confirmed in the editor.
ABILITY_TIP = "Did you know? {ability:E} is on {glyph:E}. {abilityDescription:E}"

# ── YAML helpers ────────────────────────────────────────────────────────────────────────────


def guid_for(path):
    return hashlib.md5(("cosmic-shore-drill:" + path).encode()).hexdigest()


def script_guid(name):
    meta = os.path.join(SCRIPTS, name + ".cs.meta")
    m = re.search(r"^guid: (\w+)", open(meta).read(), re.M)
    assert m, f"no guid in {meta}"
    return m.group(1)


def q(s):
    return "" if s == "" else "'" + s.replace("'", "''") + "'"


def header(name, script):
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid(script)}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: \n")


def lesson_asset(name, scheme, steps):
    out = [header(name, "LessonTemplateSO"), f"  scheme: {SCHEME[scheme]}\n  steps:\n"]
    refs = []
    for i, s in enumerate(steps):
        rid = 1000 + i
        out.append(
            f"  - Id: {s['id']}\n    Prompt: {q(s['prompt'])}\n"
            f"    Element: {ELEMENT[s.get('element', 'None')]}\n"
            f"    Anchor: {ANCHOR[s.get('anchor', 'Window')]}\n    Cue: {CUE[s.get('cue', 'None')]}\n"
            f"    Applicability: {APPLIES[s.get('applies', 'Always')]}\n"
            f"    Condition:\n      rid: {rid}\n    HintAfterSeconds: 0\n    HintPrompt: {q(s.get('hint', ''))}\n")
        cls, data = s["cond"]
        body = "".join(f"        {k}: {v}\n" for k, v in data.items())
        refs.append(f"    - rid: {rid}\n      type: {{class: {cls}, ns: CosmicShore.Gameplay, asm: Assembly-CSharp}}\n"
                    f"      data:\n{body}")
    out.append("  references:\n    version: 2\n    RefIds:\n" + "".join(refs))
    return "".join(out)


def library_asset():
    s = [header("DrillLibrary", "DrillLibrarySO"),
         f"  twoThumbLesson: {{fileID: 11400000, guid: {guid_for(TWO_THUMB)}, type: 2}}\n",
         f"  oneThumbLesson: {{fileID: 11400000, guid: {guid_for(ONE_THUMB)}, type: 2}}\n",
         "  lessonSkipDelaySeconds: 3\n  defaultHintAfterSeconds: 8\n",
         "  metricTips: []\n  modeTips: []\n  advancedTips: {fileID: 0}\n",
         f"  abilityTipTemplate: {q(ABILITY_TIP)}\n",
         "  mentorDwellSeconds: 6\n  mentorGapSeconds: 8\n  momentTimeoutSeconds: 14\n",
         "  hullOverrides: []\n  strings:\n"]
    for k, v in STRINGS.items():
        s.append(f"    {k}: {q(v)}\n")
    s.append("    PadControlLabels:\n")
    for b, label in PAD_LABELS:
        s.append(f"    - Binding: {HINT[b]}\n      Label: {q(label)}\n")
    s.append("    FlightControlLabels:\n")
    for c, sch, kb, label in FLIGHT_LABELS:
        s.append(f"    - Control: {CONTROL[c]}\n      Scheme: {SCHEME[sch]}\n      Keyboard: {int(kb)}\n"
                 f"      Label: {q(label)}\n")
    return "".join(s)


def asset_meta(path):
    return (f"fileFormatVersion: 2\nguid: {guid_for(path)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def folder_meta(path):
    return (f"fileFormatVersion: 2\nguid: {guid_for(path)}\nfolderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def outputs():
    files = {}
    for path, (scheme, steps) in LESSONS.items():
        files[path] = lesson_asset(os.path.basename(path)[:-6], scheme, steps)
    files[LIBRARY] = library_asset()
    return files


# ── Checks (run over whatever is on disk) ───────────────────────────────────────────────────

TEXT_KEYS = ("Prompt", "HintPrompt", "Label", "abilityTipTemplate") + tuple(STRINGS)


def check_enums():
    errors = []
    beats = open(os.path.join(SCRIPTS, "DrillBeats.cs")).read()
    tokens = open(os.path.join(SCRIPTS, "DrillTokens.cs")).read()
    for table, src in ((ANCHOR, beats), (CUE, beats), (APPLIES, beats), (SCHEME, beats), (CONTROL, tokens)):
        for name, value in table.items():
            if not re.search(rf"\b{name} = {value},", src):
                errors.append(f"enum value {name} = {value} not found in the C#")
    return errors


def check_text(path, text):
    errors = []
    for i, line in enumerate(text.splitlines(), 1):
        if any(ord(ch) > 126 for ch in line):
            errors.append(f"{path}:{i}: non-ASCII character (renders as tofu)")
        m = re.match(r"\s*-?\s*(\w+): '(.*)'$", line)
        if not m or m.group(1) not in TEXT_KEYS:
            continue
        for tok in re.findall(r"\{([^{}]*)\}", m.group(2).replace("''", "'")):
            ok = KNOWN_TEMPLATE_TOKEN.match(tok) if m.group(1) == "abilityTipTemplate" else KNOWN_TOKEN.match(tok)
            if not ok:
                errors.append(f"{path}:{i}: unknown token {{{tok}}}")
    return errors


def check_lesson(path, text, scheme):
    errors = []
    m = re.search(r"^  scheme: (\d+)$", text, re.M)
    if not m or int(m.group(1)) != SCHEME[scheme]:
        errors.append(f"{path}: scheme is not {scheme}")
    ids = re.findall(r"^  - Id: (.*)$", text, re.M)
    if not ids or any(not i.strip() for i in ids):
        errors.append(f"{path}: a step has no id")
    if len(ids) != len(set(ids)):
        errors.append(f"{path}: duplicate step ids {ids}")
    for rid in re.findall(r"^      rid: (\d+)$", text, re.M):
        if not re.search(rf"^    - rid: {rid}$", text, re.M):
            errors.append(f"{path}: condition rid {rid} has no RefIds entry")
    return errors


def check(read):
    errors = check_enums()
    for folder in FOLDERS:
        if read(folder + ".meta") is None:
            errors.append(f"{folder}.meta missing")
    for path in [LIBRARY] + list(LESSONS):
        text, meta = read(path), read(path + ".meta")
        if text is None or meta is None:
            errors.append(f"{path} (or its .meta) missing")
            continue
        errors += check_text(path, text)
        if path in LESSONS:
            errors += check_lesson(path, text, LESSONS[path][0])
    lib = read(LIBRARY) or ""
    for path in LESSONS:
        meta = read(path + ".meta") or ""
        g = re.search(r"^guid: (\w+)", meta, re.M)
        if not g or g.group(1) not in lib:
            errors.append(f"{LIBRARY} does not reference {path}")
    return errors


def disk(path):
    full = os.path.join(ROOT, path)
    return open(full).read() if os.path.exists(full) else None


def self_test():
    files = outputs()
    store = {}
    for p, t in files.items():
        store[p], store[p + ".meta"] = t, asset_meta(p)
    for f in FOLDERS:
        store[f + ".meta"] = folder_meta(f)
    clean = check(store.get)
    assert not clean, f"seed content fails its own checks: {clean}"

    def fires(mutate, what):
        s = dict(store)
        mutate(s)
        assert check(s.get), f"negative control did not fire: {what}"

    fires(lambda s: s.__setitem__(TWO_THUMB, s[TWO_THUMB].replace("to speed up", "to speed up — fast")), "non-ASCII")
    fires(lambda s: s.__setitem__(ONE_THUMB, s[ONE_THUMB].replace("{glyph:Steer}", "{glyph:Stear}")), "token typo")
    fires(lambda s: s.__setitem__(LIBRARY, s[LIBRARY].replace("{ability:E}", "{ability:Time}")), "template token")
    fires(lambda s: s.__setitem__(ONE_THUMB, s[ONE_THUMB].replace("Id: drift", "Id: steer")), "duplicate id")
    fires(lambda s: s.__setitem__(ONE_THUMB, s[ONE_THUMB].replace("scheme: 1", "scheme: 0")), "wrong scheme")
    fires(lambda s: s.__setitem__(TWO_THUMB, s[TWO_THUMB].replace("    - rid: 1001\n", "    - rid: 9\n")), "dangling rid")
    fires(lambda s: s.pop(LIBRARY), "missing library")
    print("self-test: clean seed passes; 7 negative controls fire")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        force = "--force" in argv
        for folder in FOLDERS:
            full = os.path.join(ROOT, folder)
            os.makedirs(full, exist_ok=True)
            if not os.path.exists(full + ".meta"):
                open(full + ".meta", "w").write(folder_meta(folder))
        for path, text in outputs().items():
            full = os.path.join(ROOT, path)
            if os.path.exists(full) and not force:
                print(f"kept (human-owned once authored): {path}")
                continue
            open(full, "w").write(text)
            open(full + ".meta", "w").write(asset_meta(path))
            print(f"wrote {path}")
    errors = check(disk)
    for e in errors:
        print("ERROR: " + e)
    print("drill assets: " + ("FAIL" if errors else "ok"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

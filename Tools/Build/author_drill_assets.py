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
TIP_DIR = "Assets/_SO_Assets/Drills/Tips"
FOLDERS = ["Assets/_SO_Assets/Drills", LESSON_DIR, TIP_DIR]

# Enum values (asserted against the C# below so a renumbering cannot slip past).
ELEMENT = {"None": 0, "Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
ANCHOR = {"Window": 0, "AbilityRow": 1}
CUE = {"None": 0, "PulseAbilityRow": 1}
APPLIES = {"Always": 0, "HullDrifts": 1, "AbilityHasInput": 2}
SCHEME = {"TwoThumb": 0, "OneThumb": 1}
CONTROL = {"Steer": 0, "Throttle": 1, "Drift": 2}
TIER = {"ShipBasics": 0, "WinTheMode": 1, "WinFaster": 2}
METRIC = {"SwitchesThreaded": 9}
MODE = {"Switchback": 45, "Headlong": 49, "Breakwater": 50, "Skein": 51, "Redline": 53,
        "Regatta": 56, "Waystation": 58}
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
# Mentor tips (§4.4). Two rules every line below is written to:
#  * A MOMENT changes when a tip is said, never which, and one that does not come within the
#    library's timeout lets the tip go anyway - so no line may depend on its moment having
#    happened ("Lap done!" would be read out after a timeout with no lap done).
#  * A mode tip names only what that mode's own hull does; Regatta seats every hull, so its tip
#    is about the arena, not a ship.
# Moment shapes: GATE_SOON = the next ring is coming up and roughly ahead; STRAIGHT = it is dead
# ahead and a long way off at this speed; THREADED / LAP = right after one.
GATE_SOON = ("GateAheadCondition", dict(MaxAngle=60, MinSeconds=0, MaxSeconds=3))
STRAIGHT = ("GateAheadCondition", dict(MaxAngle=10, MinSeconds=3, MaxSeconds=30))
THREADED = ("GatesThreadedCondition", dict(Count=1))
LAP = ("LapsCompletedCondition", dict(Count=1))

RACE_TIPS = f"{TIP_DIR}/Tips_SwitchesThreaded.asset"
TIP_LISTS = {
    RACE_TIPS: ("metric", "SwitchesThreaded", [
        dict(id="race-next-ring", tier="WinTheMode", prio=0,
             prompt="Rings count in order. Fly through the highlighted one next."),
        dict(id="race-line-up", tier="WinTheMode", prio=10, moment=GATE_SOON,
             prompt="Line up early. Come at a ring straight on, not from the side."),
        dict(id="race-best-pilot", tier="WinTheMode", prio=20,
             prompt="Your team scores with its best pilot, not its total. Race for the lead."),
        dict(id="race-turn-early", tier="WinFaster", prio=0, moment=THREADED,
             prompt="Start the next turn before you reach the ring, not after it."),
        dict(id="race-straights", tier="WinFaster", prio=10, moment=STRAIGHT,
             prompt="A long straight is free speed. Keep the nose steady and stay on the throttle."),
        dict(id="race-learn-it", tier="WinFaster", prio=20, moment=LAP,
             prompt="The course repeats. Learn where it turns, then fly it tighter."),
    ]),
    f"{TIP_DIR}/Tips_Switchback.asset": ("mode", "Switchback", [
        dict(id="switchback-drift", tier="WinTheMode", prio=50, moment=GATE_SOON,
             prompt="Hold {glyph:Drift} to carve a corner too tight to turn, then speed up out of it."),
    ]),
    f"{TIP_DIR}/Tips_Headlong.asset": ("mode", "Headlong", [
        dict(id="headlong-ramp", tier="WinTheMode", prio=50, moment=STRAIGHT,
             prompt="Your boost builds on a straight with the stick centred. Turning eases it off."),
        dict(id="headlong-corners", tier="WinFaster", prio=50, moment=GATE_SOON,
             prompt="Every corner has a top speed. Lift just enough to make it, then wind back up."),
    ]),
    f"{TIP_DIR}/Tips_Redline.asset": ("mode", "Redline", [
        dict(id="redline-soar", tier="WinTheMode", prio=50, moment=GATE_SOON,
             prompt="Easing off {ability:Time} turns you harder. Every corner asks how much speed it is worth."),
    ]),
    f"{TIP_DIR}/Tips_Breakwater.asset": ("mode", "Breakwater", [
        dict(id="breakwater-doors", tier="WinTheMode", prio=50, moment=GATE_SOON,
             prompt="Each station is woven shut. Blast a door, stop and saw one open, or thread the gap in the middle."),
    ]),
    f"{TIP_DIR}/Tips_Skein.asset": ("mode", "Skein", [
        dict(id="skein-ride", tier="WinTheMode", prio=50,
             prompt="Ride the rails - you are far faster on them than off."),
        dict(id="skein-jump", tier="WinFaster", prio=50,
             prompt="Every rail breaks. Each break is aimed at another strand - jump it and keep riding."),
    ]),
    f"{TIP_DIR}/Tips_Waystation.asset": ("mode", "Waystation", [
        dict(id="waystation-fold", tier="WinTheMode", prio=50,
             prompt="{ability:Time} jumps you along the way you are facing, and cannot be aimed once it starts."),
        dict(id="waystation-exit", tier="WinFaster", prio=50, moment=GATE_SOON,
             prompt="Thread a cluster's last ring lined up on the next cluster. That line is your jump."),
    ]),
    f"{TIP_DIR}/Tips_Regatta.asset": ("mode", "Regatta", [
        dict(id="regatta-rails", tier="WinTheMode", prio=50,
             prompt="Each team has a rail in its own colour. If your ship can ride or skim, stay on yours."),
    ]),
}

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


def tip_asset(name, tips):
    out = [header(name, "TipListSO"), "  tips:\n"]
    refs = []
    null_used = False
    for i, t in enumerate(tips):
        rid = 1000 + i
        moment = t.get("moment")
        out.append(
            f"  - Id: {t['id']}\n    Prompt: {q(t['prompt'])}\n"
            f"    Element: {ELEMENT[t.get('element', 'None')]}\n    Anchor: 0\n    Cue: 0\n"
            f"    Tier: {TIER[t['tier']]}\n    Priority: {t['prio']}\n    DwellSeconds: 0\n"
            f"    Moment:\n      rid: {rid if moment else -2}\n")
        if not moment:
            null_used = True
            continue
        cls, data = moment
        body = "".join(f"        {k}: {v}\n" for k, v in data.items())
        refs.append(f"    - rid: {rid}\n      type: {{class: {cls}, ns: CosmicShore.Gameplay, asm: Assembly-CSharp}}\n"
                    f"      data:\n{body}")
    if null_used:
        refs.append("    - rid: -2\n      type: {class: , ns: , asm: }\n")
    out.append("  references:\n    version: 2\n    RefIds:\n" + "".join(refs))
    return "".join(out)


def ref(path):
    return f"{{fileID: 11400000, guid: {guid_for(path)}, type: 2}}"


def library_rows(kind):
    """The library's metricTips / modeTips rows for the seed tip lists."""
    rows = []
    for path, (k, key, _) in TIP_LISTS.items():
        if k != kind:
            continue
        if kind == "metric":
            rows.append(f"  - Metric: {METRIC[key]}\n    Tips: {ref(path)}\n")
        else:
            rows.append(f"  - Mode: {MODE[key]}\n    Tips: {ref(path)}\n    ReplacesMetricTips: 0\n")
    return rows


def merge_library_rows(text):
    """Add each seed tip list's row to an EXISTING library, without touching anything a human
    wrote: a row is only added when no row names that metric / mode yet."""
    for kind, field, keyname, table in (("metric", "metricTips", "Metric", METRIC),
                                        ("mode", "modeTips", "Mode", MODE)):
        m = re.search(rf"^  {field}:( \[\])?\n((?:  - .*\n(?:    .*\n)*)*)", text, re.M)
        assert m, f"no {field} in the library"
        present = set(int(v) for v in re.findall(rf"^  - {keyname}: (\d+)$", m.group(2), re.M))
        add = [r for r in library_rows(kind)
               if int(re.match(rf"  - {keyname}: (\d+)", r).group(1)) not in present]
        if not add:
            continue
        block = f"  {field}:\n" + m.group(2) + "".join(add)
        text = text[:m.start()] + block + text[m.end():]
    return text


def library_asset():
    s = [header("DrillLibrary", "DrillLibrarySO"),
         f"  twoThumbLesson: {{fileID: 11400000, guid: {guid_for(TWO_THUMB)}, type: 2}}\n",
         f"  oneThumbLesson: {{fileID: 11400000, guid: {guid_for(ONE_THUMB)}, type: 2}}\n",
         "  lessonSkipDelaySeconds: 3\n  defaultHintAfterSeconds: 8\n",
         "  metricTips:\n" + "".join(library_rows("metric")),
         "  modeTips:\n" + "".join(library_rows("mode")),
         "  advancedTips: {fileID: 0}\n",
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
    for path, (_, _, tips) in TIP_LISTS.items():
        files[path] = tip_asset(os.path.basename(path)[:-6], tips)
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


CONDITION_CLASSES = ("GateAheadCondition", "LapsCompletedCondition", "GatesThreadedCondition")


def check_tips(path, text, all_ids):
    errors = []
    ids = re.findall(r"^  - Id: (.*)$", text, re.M)
    if not ids or any(not i.strip() for i in ids):
        errors.append(f"{path}: a tip has no id")
    for i in ids:
        if i in all_ids:
            errors.append(f"{path}: tip id '{i}' is used twice (seen-memory would merge them)")
        all_ids.add(i)
    for rid in re.findall(r"^      rid: (-?\d+)$", text, re.M):
        if not re.search(rf"^    - rid: {rid}$", text, re.M):
            errors.append(f"{path}: moment rid {rid} has no RefIds entry")
    return errors


def check(read):
    errors = check_enums()
    conditions = open(os.path.join(SCRIPTS, "DrillConditions.cs")).read()
    for cls in CONDITION_CLASSES:
        if f"class {cls} " not in conditions:
            errors.append(f"condition class {cls} not found in DrillConditions.cs")
    modes = open(os.path.join(ASSETS, "_Scripts", "Data", "Enums", "GameModes.cs")).read()
    for name, value in MODE.items():
        if not re.search(rf"\b{name} = {value},", modes):
            errors.append(f"GameModes.{name} is not {value}")
    metrics = open(os.path.join(ASSETS, "_Scripts", "Data", "Enums", "ScoringMetric.cs")).read()
    for name, value in METRIC.items():
        if not re.search(rf"\b{name} = {value},", metrics):
            errors.append(f"ScoringMetric.{name} is not {value}")
    tier_src = open(os.path.join(SCRIPTS, "DrillBeats.cs")).read()
    for name, value in TIER.items():
        if not re.search(rf"\b{name} = {value},", tier_src):
            errors.append(f"MentorTier.{name} is not {value}")
    tip_ids = set()
    for path in TIP_LISTS:
        text, meta = read(path), read(path + ".meta")
        if text is None or meta is None:
            errors.append(f"{path} (or its .meta) missing")
            continue
        errors += check_text(path, text)
        errors += check_tips(path, text, tip_ids)
        g = re.search(r"^guid: (\w+)", meta, re.M)
        if not g or g.group(1) not in (read(LIBRARY) or ""):
            errors.append(f"{LIBRARY} does not reference {path}")
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
    fires(lambda s: s.__setitem__(RACE_TIPS, s[RACE_TIPS].replace("Id: race-learn-it", "Id: race-next-ring")), "dup tip id")
    fires(lambda s: s.__setitem__(RACE_TIPS, s[RACE_TIPS].replace("    - rid: 1001\n", "    - rid: 7\n")), "dangling moment")
    fires(lambda s: s.__setitem__(LIBRARY, s[LIBRARY].replace(guid_for(RACE_TIPS), "0" * 32)), "unmapped tip list")
    fires(lambda s: s.__setitem__(RACE_TIPS, s[RACE_TIPS].replace("in order.", "in order \u2014 always.")), "non-ASCII tip")

    # The merge adds a missing row to a hand-edited library and leaves everything else alone.
    empty = store[LIBRARY]
    empty = re.sub(r"^  metricTips:\n(?:  - .*\n(?:    .*\n)*)*", "  metricTips: []\n", empty, flags=re.M)
    edited = empty.replace("FLIGHT TIPS", "TIPS BY A WRITER")
    merged = merge_library_rows(edited)
    assert "TIPS BY A WRITER" in merged and guid_for(RACE_TIPS) in merged, "merge lost a human edit or a row"
    assert merge_library_rows(merged) == merged, "merge is not idempotent"
    print("self-test: clean seed passes; 11 negative controls fire; library merge keeps human edits")
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
                if path == LIBRARY:
                    old = open(full).read()
                    merged = merge_library_rows(old)
                    if merged != old:
                        open(full, "w").write(merged)
                        print(f"added tip-list rows to {path} (nothing else touched)")
                        continue
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

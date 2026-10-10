#!/usr/bin/env python3
"""Anti-cheat gate: AI code may steer a vessel ONLY through its controller inputs.

    python3 Tools/Build/check_ai_no_state_writes.py            # scan, exit 1 on any finding
    python3 Tools/Build/check_ai_no_state_writes.py --check    # same (CI spelling)
    python3 Tools/Build/check_ai_no_state_writes.py --self-test

The rule (Docs/SKIM_RACE_AI.md, "Input-only contract"): an AI - hand-written policy or evolved
genome - may READ anything a pilot can see, and may WRITE only the vessel's input channels
(IInputStatus XSum/YSum/YDiff/XDiff, EasedLeftJoystickPosition, LeftTriggerAnalog - the Skim Race
pilot's drift depth, a full pull - all within human ranges at the actuation point) and press its
own bound controls (PerformShipControllerActions /
StopShipControllerActions). It must never write the vessel's pose or motion, its speed, course
or boost, any crystal, score, winner or race state, or the time scale. Any such write would let
a policy "solve" the race by reaching past the vessel's physics, which is the one thing a
trained policy must not be able to discover.

What is scanned: every .cs under the AI trees below, except tests. Comments and string literals
are removed first. Two shapes are reported:
  - an assignment (=, +=, -=, *=, /=, ++, --) to a forbidden MEMBER on any receiver
    (e.g. `vesselStatus.Speed = 300`, `crystal.transform.position = p`, `Time.timeScale = 4`);
  - a call to a forbidden METHOD (e.g. `vessel.SetPose(...)`, `rb.AddForce(...)`).
A receiver that is the AI's OWN data (an observation/context struct it fills from reads) is
allowed only through ALLOW below, one reviewed row per file + receiver + member, with a reason.
Reflection (SetValue, BindingFlags, SendMessage) is reported outright: it can reach anything.

What it does NOT prove: that a value written to an input channel is clamped (that is enforced at
the two actuation points and covered by tests), or that code outside these trees is honest.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
assert os.path.isdir(os.path.join(ROOT, "Assets")), "ROOT must contain Assets/"

SCOPES = [
    "Assets/_Scripts/Controller/AI/SkimRace",
    "Assets/_Scripts/Utility/AITraining",
    "Assets/_Scripts/Editor/AI",
    # The parity harness's replay player writes IInputStatus and raises InputEvents, and its
    # probe only READS pose and score into channel files (Port/parity/README.md). Both must
    # obey the same input-only contract as a pilot: a replay that teleported a vessel would
    # record a result the vessel never flew.
    "Assets/_Scripts/Utility/Replay",
]

# Members whose assignment changes the world rather than the pilot's input.
FORBIDDEN_MEMBERS = {
    # Unity pose / motion
    "position", "localPosition", "rotation", "localRotation", "eulerAngles", "localEulerAngles",
    "forward", "up", "right", "localScale", "velocity", "linearVelocity", "angularVelocity",
    # vessel state (VesselStatus / VesselTransformer)
    "Speed", "Course", "BoostMultiplier", "IsBoosting", "IsDrifting", "IsStationary",
    "blockRotation", "IsSlowed", "IsTranslationRestricted",
    # race / crystal / score state
    "CrystalsCollected", "Score", "WinnerName", "WinnerDomain", "CrystalTargetCount",
    "IsTurnRunning", "ownDomain", "IsEmbedded",
    # time
    "timeScale", "fixedDeltaTime", "captureDeltaTime",
}

FORBIDDEN_CALLS = {
    "SetPose", "SetInitialSpeed", "ModifyThrottle", "ModifyVelocity", "SetPositionAndRotation",
    "Translate", "Rotate", "RotateAround", "LookAt", "MovePosition", "MoveRotation", "AddForce",
    "AddRelativeForce", "AddTorque", "AddExplosionForce", "CollectBy", "Explode",
    "InvokeWinnerCalculated", "InvokeMiniGameEnd", "ChangeTeam", "SetResourceLevels",
    "AdjustLevel", "GrantPetals",
}

REFLECTION = re.compile(r"\.SetValue\s*\(|\bBindingFlags\b|\bSendMessage\s*\(|\bBroadcastMessage\s*\(")

# (path suffix, receiver regex, member, reason) - each a reviewed exception.
ALLOW = [
    ("Utility/AITraining/Pilot/TrainingPilot.cs", r"^_ctx$", "Speed",
     "_ctx is the genome's own DecisionContext, filled from a READ of _status.Speed"),
    ("Utility/AITraining/Pilot/TrainingPilot.cs", r"^_ctx$", "IsBoosting",
     "_ctx is the genome's own DecisionContext, filled from a READ"),
    ("Utility/AITraining/Pilot/TrainingPilot.cs", r"^_ctx$", "IsDrifting",
     "_ctx is the genome's own DecisionContext, filled from a READ"),
    ("Utility/AITraining/Pilot/TrainingPilot.cs", r"^_ctx$", "IsStationary",
     "_ctx is the genome's own DecisionContext, filled from a READ"),
    ("Utility/AITraining/Runner/TrainingAutoLauncher.cs", r"^Time$", "timeScale",
     "overnight TRAINING batches only (never a benchmark); applies to every pilot equally, "
     "refused while another client is connected, restored after the batch"),
]

ASSIGN = re.compile(
    r"(?P<recv>[A-Za-z_][\w\.\[\]]*?)\s*\.\s*(?P<member>[A-Za-z_]\w*)\s*"
    r"(?P<op>\+\+|--|[+\-*/]?=(?!=))")
CALL = re.compile(r"(?:\.|\b)(?P<name>[A-Za-z_]\w*)\s*\(")


def strip(text):
    """Remove comments and string/char literals, keeping line structure."""
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        if text.startswith("//", i):
            j = text.find("\n", i)
            i = n if j < 0 else j
            continue
        if text.startswith("/*", i):
            j = text.find("*/", i + 2)
            seg = text[i:(n if j < 0 else j + 2)]
            out.append("\n" * seg.count("\n"))
            i = n if j < 0 else j + 2
            continue
        if c == '@' and i + 1 < n and text[i + 1] == '"':
            j = i + 2
            while j < n:
                if text[j] == '"' and (j + 1 >= n or text[j + 1] != '"'):
                    break
                j += 2 if text[j] == '"' else 1
            out.append('""' + "\n" * text[i:j].count("\n"))
            i = j + 1
            continue
        if c in "\"'":
            j = i + 1
            while j < n and text[j] != c and text[j] != "\n":
                j += 2 if text[j] == "\\" else 1
            out.append(c + c)
            i = j + 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def allowed(rel, recv, member):
    for suffix, rx, mem, _ in ALLOW:
        if rel.endswith(suffix) and mem == member and re.match(rx, recv):
            return True
    return False


def scan_text(rel, text):
    findings = []
    for ln, line in enumerate(strip(text).split("\n"), 1):
        for m in ASSIGN.finditer(line):
            member, recv = m.group("member"), m.group("recv")
            if member in FORBIDDEN_MEMBERS and not allowed(rel, recv, member):
                findings.append((rel, ln, f"writes {recv}.{member} ({m.group('op')})"))
        for m in CALL.finditer(line):
            name = m.group("name")
            if name in FORBIDDEN_CALLS:
                # A method DECLARATION with that name is not a call into the world.
                before = line[:m.start()]
                if re.search(r"\b(void|bool|float|int|static|public|private|protected|internal)\s*$", before):
                    continue
                findings.append((rel, ln, f"calls {name}(...)"))
        if REFLECTION.search(line):
            findings.append((rel, ln, "uses reflection/messaging, which can write any state"))
    return findings


def files():
    for scope in SCOPES:
        base = os.path.join(ROOT, scope)
        for dp, _, fns in os.walk(base):
            if "/Tests" in dp.replace(os.sep, "/"):
                continue
            for fn in fns:
                if fn.endswith(".cs"):
                    p = os.path.join(dp, fn)
                    yield os.path.relpath(p, ROOT).replace(os.sep, "/"), p


def run():
    findings, count = [], 0
    for rel, p in files():
        count += 1
        with open(p, encoding="utf-8-sig") as fh:
            findings += scan_text(rel, fh.read())
    for rel, ln, msg in findings:
        print(f"{rel}:{ln}: {msg}")
    print(f"ai-no-state-writes: {'OK' if not findings else str(len(findings)) + ' FINDING(S)'} "
          f"({count} files, {len(ALLOW)} reviewed exceptions)")
    return 1 if findings else 0


def self_test():
    rel = "Assets/_Scripts/Controller/AI/SkimRace/Fake.cs"
    must_fire = {
        "transform write": "void F(){ vessel.Transform.position = p; }",
        "rotation write": "transform.rotation = q;",
        "speed write": "_status.Speed = 300f;",
        "speed compound": "status.Speed *= 2f;",
        "course write": "vesselStatus.Course = v;",
        "boost write": "status.BoostMultiplier = 5f;",
        "boost increment": "status.BoostMultiplier++;",
        "crystal move": "crystal.transform.position = pilotPos;",
        "score write": "stats.CrystalsCollected += 1;",
        "winner write": "gameData.WinnerName = name;",
        "timescale": "Time.timeScale = 4f;",
        "setpose": "vessel.SetPose(pose);",
        "addforce": "rb.AddForce(f);",
        "modify throttle": "vessel.VesselTransformer.ModifyThrottle(2f, 1f);",
        "reflection": "field.SetValue(status, 9f);",
        "allow-list is per file": "_ctx.Speed = s;",
    }
    must_not_fire = {
        "comparison": "if (status.Speed == 0f) return;",
        "read": "float s = status.Speed; var p = transform.position;",
        "observation initializer": "var o = new Obs { Speed = s, Position = p };",
        "input write": "input.XSum = a.Yaw; input.XDiff = a.Throttle;",
        "comment": "// status.Speed = 300f;",
        "string": "Debug.Log(\"status.Speed = 300\");",
        "local struct field (capitalised Position)": "o.Position = pos;",
        "method named like a call": "static void Rotate(int x) {}",
        "inequality": "if (a.Speed != b.Speed) {}",
    }
    bad = 0
    for name, code in must_fire.items():
        if not scan_text(rel, code):
            print(f"SELF-TEST FAIL: did not fire on {name}: {code}")
            bad += 1
    for name, code in must_not_fire.items():
        f = scan_text(rel, code)
        if f:
            print(f"SELF-TEST FAIL: fired on {name}: {code} -> {f}")
            bad += 1
    # The allow-list must work exactly where it is declared.
    if scan_text("Assets/_Scripts/Utility/AITraining/Pilot/TrainingPilot.cs", "_ctx.Speed = s;"):
        print("SELF-TEST FAIL: reviewed exception not honoured")
        bad += 1
    if not scan_text("Assets/_Scripts/Utility/AITraining/Pilot/TrainingPilot.cs", "_status.Speed = s;"):
        print("SELF-TEST FAIL: exception leaked to another receiver")
        bad += 1
    print("self-test", "OK" if not bad else f"FAILED ({bad})")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(self_test() if "--self-test" in sys.argv else run())

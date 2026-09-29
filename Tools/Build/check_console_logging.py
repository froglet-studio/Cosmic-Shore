#!/usr/bin/env python3
"""Keep the Unity console professional: no raw Debug.Log in runtime code, no rich text in logs.

The rule (CLAUDE.md, "What Claude Code Should Never Do"): info-level logging in first-party
runtime code goes through `CosmicShore.Utility.CSDebug`, and a finished system's bring-up
telemetry lives on a `CSLogChannel` (`CSDebug.LogVerbose`), which is OFF by default. A raw
`UnityEngine.Debug.Log` bypasses both the runtime log level and the release-build strip, and a
`<color=...>` / emoji-decorated message is the tell of a trace somebody left on while building.

The 2026-09 cleanup measured 778 info-level call sites (198 of them raw `Debug.Log`) and a
console at 999+ before a match started; this gate is what stops that from growing back.

Fails on, in first-party RUNTIME C# (Assets/_Scripts, Assets/FTUE, loose Assets/*.cs):
  1. a raw `Debug.Log(` / `Debug.LogFormat(` / `UnityEngine.Debug.Log(` call (info level only —
     raw LogWarning / LogError are tolerated, though CSDebug is preferred);
  2. `<color=` rich text inside any CSDebug.* call statement;
  3. a `CSDebug.Log(` / `CSDebug.LogVerbose(` message containing an emoji code point.

Deliberately NOT scanned: `Editor/` folders and `Tests/` (editor assembly), and the tool /
benchmark / stress-test / tester scripts whose console output IS their deliverable
(TOOL_PATHS below). `CSDebug.cs` itself is the one sanctioned Debug.Log caller.

Usage:
    check_console_logging.py [--self-test] [paths...]      (default: the whole runtime tree)
"""

import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCAN_ROOTS = ["Assets/_Scripts", "Assets/FTUE"]
LOOSE_ROOT = "Assets"  # top-level loose .cs files only

# Console output is the deliverable of these; they are run on purpose by a human.
TOOL_PATHS = (
    "Assets/_Scripts/Utility/Tools/",
    "Assets/_Scripts/Utility/PerformanceBenchmark/",
    "Assets/_Scripts/Utility/ScreenShots/",
    "Assets/_Scripts/Utility/Recording/",
    "Assets/_Scripts/Utility/GamepadDebugger.cs",
    "Assets/_Scripts/Utility/CSDebug.cs",
    "Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs",
    "Assets/_Scripts/Controller/ECS/Rendering/PrismRenderStressTest.cs",
    "Assets/_Scripts/ScriptableObjects/SnowStressTestSpawner.cs",
    "Assets/_Scripts/Controller/Vessel/PrismOctahedronShieldTester.cs",
    "Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShieldTester.cs",
    "Assets/_Scripts/UI/UniversalStatsProviderEditor.cs",
    "Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDefinitionEditor.cs",  # #if UNITY_EDITOR inspector button
    "Assets/_Scripts/Game/IO/",            # generated input-actions doc comments
    "Assets/_Scripts/Controller/IO/_Input Mapping/",
)

RAW_INFO = re.compile(r"(?<![\w.])(?:UnityEngine\.)?Debug\.Log(?:Format)?\s*\(")
CS_CALL = re.compile(r"CSDebug\.(Log|LogFormat|LogVerbose|LogWarning|LogWarningFormat|LogError|LogErrorFormat)\s*\(")
EMOJI = re.compile("[\U0001F300-\U0001FAFF☀-➿⭐✅❌]")


def strip_comments(text):
    """Blank out // and /* */ comments while keeping line numbers intact."""
    out = []
    i, n = 0, len(text)
    while i < n:
        if text.startswith("//", i):
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i)); i = j
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", text[i:j])); i = j
        elif text[i] in "\"'":
            q = text[i]
            j = i + 1
            verbatim = i > 0 and text[i - 1] == "@"
            while j < n:
                if text[j] == "\\" and not verbatim:
                    j += 2; continue
                if text[j] == q:
                    if verbatim and text[j + 1:j + 2] == q:
                        j += 2; continue
                    break
                j += 1
            body = re.sub(r"[^\n]", " ", text[i + 1:j])
            out.append(text[i] + body + text[j:j + 1]); i = j + 1
        else:
            out.append(text[i]); i += 1
    return "".join(out)


def statement_end(text, start):
    """Index just past the `;` that ends the call statement starting at `start`."""
    depth = 0
    i = start
    n = len(text)
    while i < n:
        c = text[i]
        if c in "\"'":
            q = c
            j = i + 1
            while j < n and text[j] != q:
                if text[j] == "\\":
                    j += 1
                j += 1
            i = j + 1
            continue
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
        elif c == ";" and depth <= 0:
            return i + 1
        i += 1
    return n


def check_text(rel, raw):
    findings = []
    text = strip_comments(raw)
    for m in RAW_INFO.finditer(text):
        line = text.count("\n", 0, m.start()) + 1
        findings.append((rel, line, "raw Debug.Log - route through CSDebug (LogVerbose on a channel for telemetry)"))
    for m in CS_CALL.finditer(text):
        end = statement_end(text, m.end())
        stmt = raw[m.start():end]
        line = text.count("\n", 0, m.start()) + 1
        if "<color=" in stmt:
            findings.append((rel, line, "rich text <color=> inside a CSDebug call"))
        if m.group(1) in ("Log", "LogFormat", "LogVerbose") and EMOJI.search(stmt):
            findings.append((rel, line, "emoji inside a CSDebug info log"))
    return findings


def is_scanned(rel):
    rel = rel.replace(os.sep, "/")
    if not rel.endswith(".cs"):
        return False
    if "/Editor/" in rel or "/Tests/" in rel:
        return False
    return not any(rel.startswith(p) for p in TOOL_PATHS)


def iter_files():
    for root in SCAN_ROOTS:
        for dirpath, _, files in os.walk(os.path.join(ROOT, root)):
            for f in files:
                rel = os.path.relpath(os.path.join(dirpath, f), ROOT)
                if is_scanned(rel):
                    yield rel
    for f in os.listdir(os.path.join(ROOT, LOOSE_ROOT)):
        rel = os.path.join(LOOSE_ROOT, f)
        if f.endswith(".cs") and is_scanned(rel):
            yield rel


def self_test():
    cases = [
        ('Debug.Log("x");', 1, "bare raw"),
        ('UnityEngine.Debug.Log("x");', 1, "qualified raw"),
        ('CSDebug.Log("x");', 0, "CSDebug info is fine"),
        ('CSDebug.LogVerbose(CSLogChannel.Boot, "x");', 0, "channelled is fine"),
        ('CSDebug.Log("<color=red>x</color>");', 1, "rich text"),
        ('CSDebug.Log($"<color=#FF00FF>[X] {a}" +\n    "</color>");', 1, "multi-line rich text"),
        ('CSDebug.LogWarning("<color=yellow>x</color>");', 1, "rich text on a warning"),
        ('CSDebug.Log("\U0001F4CA stats");', 1, "emoji"),
        ('// Debug.Log("x");', 0, "commented out"),
        ('/* Debug.Log("x"); */', 0, "block-commented"),
        ('Debug.LogWarning("x");', 0, "raw warning tolerated"),
        ('var s = "Debug.Log(";', 0, "inside a string literal"),
        ('MyDebug.Log("x");', 0, "different class"),
    ]
    ok = True
    for src, expected, label in cases:
        got = len(check_text("t.cs", src))
        mark = "ok  " if got == expected else "FAIL"
        if got != expected:
            ok = False
        print(f"  {mark} {label}: expected {expected}, got {got}")
    print("self-test " + ("passed" if ok else "FAILED"))
    return 0 if ok else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    paths = [a for a in argv if not a.startswith("--")]
    files = [os.path.relpath(os.path.abspath(p), ROOT) for p in paths] if paths else list(iter_files())
    findings = []
    for rel in files:
        if not is_scanned(rel):
            continue
        with open(os.path.join(ROOT, rel), encoding="utf-8-sig", errors="replace") as fh:
            findings.extend(check_text(rel, fh.read()))
    for rel, line, msg in findings:
        print(f"{rel}:{line}: {msg}")
    print(f"console-logging check: {len(findings)} PROBLEM(S) ({len(files)} files)")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

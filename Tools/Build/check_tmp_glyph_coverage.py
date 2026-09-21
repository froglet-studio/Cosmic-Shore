#!/usr/bin/env python3
"""Fail the build on a shipped UI string the project's font cannot draw.

The project has effectively ONE UI font — `ALDRICH-REGULAR SDF` — and it carries 97
glyphs with an EMPTY fallback table. A character outside that set does not fail, does
not warn, and does not fall back: TextMeshPro draws an empty box (tofu). So a `·` in a
status line is indistinguishable, in code review and in every static check, from a
character that works.

This gate reads the coverage off the FONT ASSET rather than off a prose list, because
the prose list is a copy and a copy drifts. It then scans first-party C# for string
literals carrying a codepoint the font does not have, and scans prefab/scene YAML for
the same in `m_text:` values.

What is deliberately NOT flagged, because none of it reaches the font:
  * comments and doc comments
  * `[Tooltip]`, `[Header]`, `[CreateAssetMenu]` and friends — inspector chrome
  * logging (`Debug.*`, `CSDebug.*`) and `throw new ...Exception(...)`
  * anything under an `Editor/` folder — editor UI draws with the editor's own fonts
  * `Tools/`, `Docs/` — not shipped strings

Usage:
    python3 Tools/Build/check_tmp_glyph_coverage.py --check
    python3 Tools/Build/check_tmp_glyph_coverage.py --self-test
"""

from __future__ import annotations

import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"

FONT = os.path.join(
    ROOT, "Assets", "Unity Assests", "TextMesh Pro", "Resources",
    "Fonts & Materials", "ALDRICH-REGULAR SDF.asset",
)

SCAN_DIRS = [os.path.join(ROOT, "Assets", "_Scripts")]
YAML_DIRS = [os.path.join(ROOT, "Assets", "_Prefabs"), os.path.join(ROOT, "Assets", "_Scenes")]

# Attribute and log contexts never reach a TMP_Text.
_ATTR = re.compile(r'\[\s*(?:Tooltip|Header|CreateAssetMenu|MenuItem|HelpURL|InspectorName|Space)\b')
_LOG = re.compile(r'\b(?:Debug|CSDebug|Logger)\s*\.\s*Log\w*|\bthrow\s+new\b|\bAssert\b')


def font_codepoints(path: str) -> set[int]:
    with open(path, encoding="utf-8", errors="replace") as fh:
        return {int(m) for m in re.findall(r"m_Unicode:\s*(\d+)", fh.read())}


def strip_comments(src: str) -> str:
    """Blank out comments, preserving offsets so line numbers survive."""
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if c == '"':                                    # string literal: copy verbatim
            j, verbatim = i + 1, src[max(0, i - 1)] == '@'
            while j < n:
                if not verbatim and src[j] == "\\":
                    j += 2
                    continue
                if src[j] == '"':
                    if verbatim and j + 1 < n and src[j + 1] == '"':
                        j += 2
                        continue
                    break
                j += 1
            out.append(src[i:j + 1]); i = j + 1; continue
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            j = src.find("\n", i); j = n if j < 0 else j
            out.append(" " * (j - i)); i = j; continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            j = src.find("*/", i + 2); j = n if j < 0 else j + 2
            out.append("".join(ch if ch == "\n" else " " for ch in src[i:j])); i = j; continue
        out.append(c); i += 1
    return "".join(out)


_STR = re.compile(r'"(?:[^"\\\n]|\\.)*"')
# A text-setting statement: the one path we can PROVE reaches a TMP_Text.
_SETTER = re.compile(r"\.text\s*\+?=|\bSetText\s*\(")
_STMT_BREAK = ";{}"


def _string_mask(src: str) -> list[bool]:
    """True at every offset that lies INSIDE a string literal.

    Needed because an interpolated string carries `{` and `}` of its own:
    `$"... {new string('.', dots)} ... \u00b7 ..."` puts a brace between the
    literal and the `.text =` that owns it, so a naive backward scan for a
    statement break stops inside the string and never sees the assignment.
    That is not hypothetical - it hid two live defects on the load screen.
    """
    mask = [False] * len(src)
    i, n = 0, len(src)
    while i < n:
        if src[i] != '"':
            i += 1
            continue
        verbatim = i > 0 and src[i - 1] == "@"
        j = i + 1
        while j < n:
            if not verbatim and src[j] == "\\":
                j += 2
                continue
            if src[j] == '"':
                if verbatim and j + 1 < n and src[j + 1] == '"':
                    j += 2
                    continue
                break
            j += 1
        for k in range(i, min(j + 1, n)):
            mask[k] = True
        i = j + 1
    return mask


def _statement_window(src: str, mask: list[bool], start: int) -> str:
    """Source from the start of the enclosing statement up to the literal.

    Scanning back to the nearest `;`, `{` or `}` that is NOT itself inside a
    string rejoins a literal to its own statement even when that statement is
    split across lines by `+` concatenation - which is exactly what a
    multi-line `[Tooltip("a" + \n "b")]` looks like without it.
    """
    i = start
    while i > 0 and not (src[i - 1] in _STMT_BREAK and not mask[i - 1]):
        i -= 1
    return src[i:start + 1]


def scan_cs(path: str, allowed: set[int]):
    """Return (line, bad_chars, severity, context) for each unrenderable literal.

    severity is "error" for a literal on a proven text-setting statement and
    "warn" for one that is merely in shipped runtime code. The gate fails on
    errors only — a warn cannot be proven to reach the font from here.
    """
    with open(path, encoding="utf-8", errors="replace") as fh:
        raw = fh.read()
    src = strip_comments(raw)
    mask = _string_mask(src)
    hits = []
    for m in _STR.finditer(src):
        lit = m.group(0)
        bad = sorted({ch for ch in lit if ord(ch) > 126 and ord(ch) not in allowed})
        if not bad:
            continue
        window = _statement_window(src, mask, m.start())
        if _ATTR.search(window) or _LOG.search(window):
            continue
        sev = "error" if _SETTER.search(window) else "warn"
        ln = src.count("\n", 0, m.start()) + 1
        ctx = raw.split("\n")[ln - 1].strip()[:110]
        hits.append((ln, "".join(bad), sev, ctx))
    return hits


def scan_yaml(path: str, allowed: set[int]):
    hits = []
    with open(path, encoding="utf-8", errors="replace") as fh:
        for ln, raw in enumerate(fh, 1):
            m = re.match(r"\s*m_[Tt]ext:\s*(.*)$", raw)
            if not m:
                continue
            bad = sorted({ch for ch in m.group(1) if ord(ch) > 126 and ord(ch) not in allowed})
            if bad:
                hits.append((ln, "".join(bad), "error", raw.strip()[:110]))
    return hits


def walk(dirs, exts):
    for d in dirs:
        for base, subdirs, files in os.walk(d):
            subdirs[:] = [s for s in subdirs if s != "Editor"]
            for f in files:
                if f.endswith(exts):
                    yield os.path.join(base, f)


def run_check() -> int:
    if not os.path.isfile(FONT):
        print(f"tmp-glyph check: FONT NOT FOUND at {FONT}", file=sys.stderr)
        return 2
    allowed = font_codepoints(FONT)
    print(f"tmp-glyph check: {len(allowed)} glyphs in {os.path.basename(FONT)}")

    errors, warns = [], []
    for p_ in walk(SCAN_DIRS, (".cs",)):
        for ln, bad, sev, ctx in scan_cs(p_, allowed):
            (errors if sev == "error" else warns).append((os.path.relpath(p_, ROOT), ln, bad, ctx))
    for p_ in walk(YAML_DIRS, (".prefab", ".unity")):
        for ln, bad, sev, ctx in scan_yaml(p_, allowed):
            errors.append((os.path.relpath(p_, ROOT), ln, bad, ctx))

    def show(rows):
        for rel, ln, bad, ctx in rows:
            codes = " ".join(f"U+{ord(c):04X}({c})" for c in bad)
            print(f"  {rel}:{ln}\n      missing: {codes}\n      {ctx}")

    if warns:
        print(f"\n{len(warns)} literal(s) in shipped runtime code carry a glyph the font lacks,")
        print("but no text assignment is visible on their statement. Reported, NOT failed:\n")
        show(warns[:25])
        if len(warns) > 25:
            print(f"  ... and {len(warns) - 25} more")

    if errors:
        print(f"\ntmp-glyph check: FAIL - {len(errors)} string(s) assigned to a text field")
        print("that the font cannot draw. Each renders as an empty box.\n")
        show(errors)
        print("\nUse ASCII, or add the glyph to the font asset (its fallback table is empty).")
        return 1

    print("\ntmp-glyph check: OK - no string assigned to a text field uses a missing glyph.")
    return 0


def run_self_test() -> int:
    """Negative control: the gate must FIRE on tofu and stay silent on what is safe."""
    allowed = font_codepoints(FONT) if os.path.isfile(FONT) else set(range(32, 127)) | {0xA0, 0x2026}
    cases = [
        ('t.text = "a \u00b7 b";',                  "error", "middle dot assigned to .text"),
        ('t.SetText($"x \u2192 y");',               "error", "arrow through SetText"),
        ('t.text = "loading\u2026";',               None,    "ellipsis IS in the font"),
        ('t.text = "plain ascii";',                  None,    "ascii"),
        ('// a comment with \u00b7 in it',          None,    "comment"),
        ('/* block \u2192 comment */',              None,    "block comment"),
        ('[Tooltip("dots \u00b7 here")] int x;',    None,    "tooltip is inspector chrome"),
        ('[Tooltip("first line " +\n  "second \u2014 line")] int x;', None,
                                                              "MULTI-LINE tooltip continuation"),
        ('Debug.Log("failed \u2192 retry");',       None,    "logging never reaches the font"),
        ('/// <summary>\u2014 doc</summary>',       None,    "doc comment"),
        ('string reason = "bad \u2014 value";',     "warn",  "loose literal: reported, not failed"),
        # Regression control for the interpolation-brace bug: the `{...}` inside the
        # first interpolated string used to stop the backward statement scan, so the
        # `.text =` was never seen and a live load-screen defect scored only "warn".
        ('t.text = $"a{new string(0x2e, n)}b" +\n  $"c \u00b7 {x:F0}s";',
                                                     "error", "interpolated braces before the literal"),
    ]
    import tempfile
    ok = True
    for src_, expect, why in cases:
        with tempfile.NamedTemporaryFile("w", suffix=".cs", delete=False, encoding="utf-8") as fh:
            fh.write(src_); tmp = fh.name
        hits = scan_cs(tmp, allowed)
        os.unlink(tmp)
        got = hits[0][2] if hits else None
        good = got == expect
        ok &= good
        print(f"  [{'ok ' if good else 'BAD'}] got={str(got):<6} expected={str(expect):<6} {why}")
    print("\nself-test:", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    sys.exit(run_self_test() if a.self_test else run_check())

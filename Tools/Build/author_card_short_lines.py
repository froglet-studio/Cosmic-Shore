#!/usr/bin/env python3
"""Write each arcade card's short description (SO_Game.ShortDescription) from card_short_lines.json.

Shombith, 2026-11-23: "the description is too big there should be a short description". The Poster card's reveal panel
(KitGameCard.descriptionText, Docs/MENU_REDESIGN_B.md section 222) shows this line, not the long Description.

    python3 Tools/Build/author_card_short_lines.py            # write the lines into the card assets
    python3 Tools/Build/author_card_short_lines.py --check    # fail if any card's line differs from the table
    python3 Tools/Build/author_card_short_lines.py --self-test

The table is the one place the words are written; the field stays editable in the inspector, and --check names any
card whose line was changed there so the table can take the edit.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TABLE = ROOT / "Tools/Build/card_short_lines.json"
GAMES = ROOT / "Assets/_SO_Assets/Games"
ROSTER = GAMES / "GameLists/ArcadeGames.asset"
MAX_LEN = 48
_FIELD = re.compile(r"^  ShortDescription:.*\n(?:    .*\n)*", re.M)
_ANCHOR = re.compile(r"^  Description:.*\n(?:    .*\n)*", re.M)


def scalar(line):
    """The line as Unity writes a plain scalar. The table keeps to words that need no quoting."""
    assert not re.search(r"[:#'\"]|^[-?\[\]{}&*!|>%@`]", line), f"needs YAML quoting, reword it: {line!r}"
    return line


def with_line(text, line):
    entry = f"  ShortDescription: {scalar(line)}\n" if line else "  ShortDescription: \n"
    if _FIELD.search(text):
        return _FIELD.sub(lambda m: entry, text, count=1)
    m = _ANCHOR.search(text)
    assert m, "no Description field to place ShortDescription after"
    return text[:m.end()] + entry + text[m.end():]


_LINES = None


def line_for(stem):
    """The table's line for card `stem` (ArcadeGameSkimRace), or None when the card is not in the table."""
    global _LINES
    if _LINES is None:
        _LINES = json.loads(TABLE.read_text())["lines"]
    return _LINES.get(stem)


def roster():
    guids = re.findall(r"guid: (\w+)", ROSTER.read_text())
    by_guid = {}
    for meta in GAMES.glob("*.asset.meta"):
        g = re.search(r"^guid: (\w+)$", meta.read_text(), re.M)
        if g:
            by_guid[g.group(1)] = meta.with_suffix("")
    return [by_guid[g] for g in guids if g in by_guid]


def validate(lines, cards):
    errors = []
    names = {p.stem for p in cards}
    for stem in sorted(names - set(lines)):
        errors.append(f"{stem}: on the Arcade roster with no short line in card_short_lines.json")
    for stem in sorted(set(lines) - names):
        errors.append(f"{stem}: in card_short_lines.json but not on the Arcade roster")
    for stem, line in lines.items():
        if len(line) > MAX_LEN:
            errors.append(f"{stem}: {len(line)} characters, over {MAX_LEN}")
    return errors


def run(check):
    lines = json.loads(TABLE.read_text())["lines"]
    cards = roster()
    errors = validate(lines, cards)
    if errors:
        print("\n".join("FAIL " + e for e in errors))
        return 1
    stale = []
    for path in cards:
        text = path.read_text()
        want = with_line(text, lines[path.stem])
        if want != text:
            stale.append(path.stem)
            if not check:
                path.write_text(want)
    if check and stale:
        print(f"card short lines: STALE {stale} - run without --check (or take an inspector edit into the table)")
        return 1
    print(f"card short lines: {'OK' if check else 'wrote'} {len(cards)} cards"
          + ("" if check else f" ({len(stale)} changed)"))
    return 0


def self_test():
    base = "  DisplayName: X\n  Description: a long\n    line\n  IconActive: {fileID: 0}\n"
    once = with_line(base, "Short.")
    assert "  Description: a long\n    line\n  ShortDescription: Short.\n  IconActive" in once, once
    assert with_line(once, "Short.") == once, "not idempotent"
    assert "ShortDescription: Other.\n" in with_line(once, "Other.")
    caught = 0
    for bad in ("A: colon", "it's", "x" * (MAX_LEN + 1)):
        try:
            scalar(bad)
            if len(bad) > MAX_LEN and validate({"A": bad}, [Path("A")]):
                caught += 1
        except AssertionError:
            caught += 1
    assert caught == 3, caught
    assert validate({}, [Path("ArcadeGameNew")]), "a new card with no line must fail"
    print("self-test OK: placement, idempotence, quoting, length and a missing card all caught")
    return 0


if __name__ == "__main__":
    a = sys.argv[1:]
    sys.exit(self_test() if "--self-test" in a else run("--check" in a))

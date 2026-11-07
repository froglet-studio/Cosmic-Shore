#!/usr/bin/env python3
"""ui_lessons.py - the UI lessons table behind .claude/skills/frogma/references/UI_LESSONS.md, mined mechanically
from Docs/MENU_REDESIGN_B.md (every '## N. ...' section: its date, the screens its heading names, the pick line, the
owner's quoted words, the rejection words, and whether the result was run in the editor), so the taste the 43 mockup
rounds recorded can be read before a first draft is drawn, and so a round that was never decided is noticed.

A READER tool: it reads Docs/MENU_REDESIGN_B.md and writes nothing, except the generated table of UI_LESSONS.md
between its two markers when asked with --md (the hand-written part above the markers is never touched).

    python3 Tools/Build/ui_lessons.py                 # the table on stdout
    python3 Tools/Build/ui_lessons.py --md            # write the table into UI_LESSONS.md between the markers
    python3 Tools/Build/ui_lessons.py --check         # exit 1 on a 'Rev N mockups' round dated more than 3 days
                                                      #   before today with neither a pick line nor an 'awaiting' marker
    python3 Tools/Build/ui_lessons.py --screens       # per-screen count of sections and rounds (the rebuild count)
    python3 Tools/Build/ui_lessons.py --json          # every section's parsed fields
    python3 Tools/Build/ui_lessons.py --self-test     # a temp doc with three rounds; the negative control

What is read, and how (all of it mechanical; nothing is typed in by hand):
  section       a line '## N. title'; the number, the title and the first YYYY-MM-DD in the title (else the first
                date in the body). '### N.M' sub-sections belong to their '## N.' section. Fenced code is skipped.
  round         a section whose heading says 'Rev N' and 'mockups' or 'boards': the sections --check judges.
  kind          round, built (the heading says built or Item N), fix (fix / fixes / playtest / polish), other.
  screens       the screen names the HEADING names, from the SCREENS vocabulary below; a heading that names none
                counts for no screen (so the per-screen count is 'sections titled after the screen').
  pick line     a line (heading or body) matching a pick pattern: 'Picked (', 'go with', 'Lets go', 'I approve',
                'Approved', 'Go ahead', 'picked'/'approved' and the rest of PICK_STRONG / PICK_WEAK.
  his words     the quoted spans ("...") of the section that carry a pick pattern, e.g. "go with C, the deck".
  rejected      the quoted spans that carry a rejection pattern: 'do not like', 'don't like', 'hate', 'too much noise',
                'scrap', 'up your game', 'not love', 'make it modern', 'can look better', 'Remove', 'noisy' and the
                rest of REJECT. Only QUOTED text counts, so a build note that says 'removed' is not a rejection.
  run in editor 'yes' when the heading says approved in editor; 'no: NOT RUN IN EDITOR' on that marker; 'awaiting'
                on 'awaiting in-editor approval' (or Shombith's approval / a two-client test); else blank.
  awaiting      any 'awaiting' in the section (heading or body): the marker that excuses a round from --check.
  decided       a pick line, or an explicit 'superseded' / 'scrapped' / 'Rejected (...)': a round decided against is
                decided too (Rev 39 was superseded by the Rev 40 pick; Rev 34 was rejected and redrawn as Rev 36).
  rev           a section whose heading names 'Rev N' at all (a round, or the build from one): the --screens 'revs'.

Exit 0; 1 when --check finds an undecided round or the self-test fails; 2 on a setup error (doc or markers missing).
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DOC = os.path.join(ROOT, "Docs", "MENU_REDESIGN_B.md")
LESSONS = os.path.join(ROOT, ".claude", "skills", "frogma", "references", "UI_LESSONS.md")
BEGIN = "<!-- ui-lessons:begin -->"
END = "<!-- ui-lessons:end -->"
STALE_DAYS = 3  # a round older than this with no pick and no 'awaiting' was never decided

HEADING = re.compile(r"^## (\d+)\. (.+?)\s*$")
OTHER_HEADING = re.compile(r"^## ")
DATE = re.compile(r"20\d\d-\d\d-\d\d")
ROUND = re.compile(r"\b[Rr]ev \d+(?: ?/ ?\d+)?\b.*\b(mockups?|boards)\b|\b(mockups?|boards)\b.*\b[Rr]ev \d+\b")
REV = re.compile(r"\b[Rr]ev \d+\b")
BUILT = re.compile(r"\b[Bb]uilt\b|^Item \d+\b|\bItem \d+:|awaiting in-editor|\b[Aa]pproved\b")
DECIDED_OTHER = [re.compile(p) for p in (r"\bsuperseded\b", r"\bscrapped\b", r"^\*\*Rejected\b", r"\brejected\b")]
FIX = re.compile(r"\b[Ff]ix(?:es|ed)?\b|\b[Pp]lay-?test\b|\b[Pp]olish\b")
NOT_RUN = "NOT RUN IN EDITOR"
APPROVED_IN_EDITOR = re.compile(r"approved in (?:the )?editor", re.I)
AWAITING = re.compile(r"\bawaiting\b", re.I)
AWAITING_EDITOR = re.compile(r"awaiting (?:in-editor|Shombith's approval|a two-client)", re.I)
QUOTE = re.compile(r"[\"“]([^\"“”]{3,}?)[\"”]")

# A pick: the owner chose. Strong patterns are what the table shows; weak ones also count for --check.
PICK_STRONG = [
    re.compile(p)
    for p in (
        r"^\*\*(?:Picked|Approved)\b",  # the bold key: **Picked (Shombith, date):** "..."
        r"\bPicked \(",
        r"\b[Gg]o with\b",
        r"\b[Ll]ets? go\b",
        r"\bI approve\b",
        r"\bGo ahead\b",
        r"\b[Ll]ets implement\b",
        r"\bmove ahead\b",
        r"\bBuild [A-F]\b",
        r"\bImplement [a-fA-F]\b",
        r"\bI liked? (?:this|design|the|[A-F]\b)",
        r"\b(?:Shombith|Garrett|Yash|Caleb|the owner|the user|the owners) (?:picked|approved)\b",
        r"\bpicked (?:board |option )?[A-J]\b",
        r"\bMockup approved\b",
        r"\b[Aa]pproved (?:in (?:the )?editor|by|with|20\d\d|\()",
    )
]
PICK_WEAK = [re.compile(r"\bpicked\b", re.I), re.compile(r"\bapproved\b", re.I)]
PICK_QUOTE = [
    re.compile(p, re.I)
    for p in (
        r"\bgo with\b",
        r"\blets? go\b",
        r"\bI approve\b",
        r"\bapproved\b",
        r"\bgo ahead\b",
        r"\blets implement\b",
        r"\bmove ahead\b",
        r"\bbuild [a-f]\b",
        r"\bimplement [a-f]\b",
        r"\bI liked?\b",
        r"\bimplement\b",
    )
]
# A rejection, in his words: only a QUOTED span counts (see the docstring).
REJECT = [
    re.compile(p, re.I if not cs else 0)
    for p, cs in (
        (r"do not like", False),
        (r"don'?t like", False),
        (r"dont like", False),
        (r"\bhate\b", False),
        (r"too much noise", False),
        (r"\bscrap\b", False),
        (r"up your game", False),
        (r"not love", False),
        (r"make it modern", False),
        (r"can look better", False),
        (r"\bRemove\b", True),
        (r"\bREMOVE\b", True),
        (r"\bnoisy\b", False),
        (r"too (?:empty|transparent|black|big|cramped|laggy|small)\b", False),
        (r"\bworst\b", False),
        (r"\bmessed up\b", False),
        (r"\bweird\b", False),
        (r"not at all good", False),
        (r"\bslop\b", False),
        (r"useless text", False),
        (r"too much text", False),
        (r"\bnot working\b", False),
        (r"\boverlap(?:ping|s)?\b", False),
        (r"\bcut off\b", False),
    )
]

# The screens a heading can name. First match wins per entry; a heading may name several screens.
SCREENS: list[tuple[str, re.Pattern]] = [
    (name, re.compile(pat))
    for name, pat in (
        ("Hangar", r"\bHangar(?:'s)?\b"),
        ("Home", r"\bHome\b"),
        ("Arcade", r"\bArcade(?:'s)?\b|ALL GAMES"),
        ("Configure", r"\b[Cc]onfigure\b|\bconfiguration\b|launch panel|mode's preview|(?<!Hangar )\bpreview\b|"
                      r"\b[Ii]ntensity\b|\bpodium\b|\bpedestals?\b|AI seat"),
        ("Profile", r"\bProfile(?:'s)?\b|pilot record|[Cc]olour sets"),
        ("Ark", r"\bArk(?:'s)?\b"),
        ("Squads", r"\bSquads?\b|\bdeck\b"),
        ("Fleet Codex", r"Fleet Codex"),
        ("Codex", r"(?<!Fleet )\b(?:Codex|CODEX)\b"),
        ("Port", r"\bPort(?:'s)?\b|faction messages"),
        ("Voyage", r"\b[Vv]oyages?\b"),
        ("Mission", r"\bMission\b"),
        ("Scoreboard", r"\b[Ss]coreboard\b"),
        ("Friends / party", r"\b[Ff]riends\b|\b[Pp]arty\b"),
        ("Settings", r"\b[Ss]ettings\b"),
        ("Credits", r"\bCredits\b"),
        ("Toy Box", r"\bToy ?[Bb]ox\b|\btoy cards?\b"),
        ("Arena", r"\bArena\b"),
        ("Mode windows", r"mode windows|four windows|hub windows"),
        ("Start flow / sign-in", r"start flow|[Ss]ign-in|[Ss]igning in|[Ss]plash|[Bb]ootstrap|welcome card|providers"),
        ("Connection", r"[Cc]onnection\b|[Cc]onnecting\b|rejoin"),
        ("Loading", r"[Ll]oading|boot world|into a game mode"),
        ("In-game HUD", r"\bHUD\b|in-game UI|\bin-game\b"),
        ("Maelstrom", r"\bMaelstrom\b"),
        ("Toasts", r"\b[Tt]oasts?\b"),
        ("Top nav", r"top nav|top bar|\bnav\b"),
        ("FTUE", r"\bFTUE\b|first-login|[Tt]utorial"),
        ("Game cards", r"\b(?:game|toy|[Aa]rcade|[Aa]rena) cards\b|game and toy cards|hull lit"),
        ("Window backgrounds", r"sea lines|\blattice\b|frosted|\bbackgrounds?\b|\bbg\b"),
        ("Hover", r"\b[Hh]over\b"),
        ("Kit", r"component kit|[Rr]eusable components|kit label|[Gg]lowing borders|\bkit supersedes\b"),
        ("Closed surfaces / purchase", r"closed surfaces|[Pp]urchase"),
        ("Compute console", r"Compute console|Computer artifact"),
        ("Analytics console", r"\bAnalytics\b"),
        ("Frogma (the board)", r"\bFrogma\b|design board|[Tt]he board\b"),
        ("Style guide", r"[Ss]tyle guide"),
        ("Replays", r"\b[Rr]eplays?\b"),
        ("Palette / elemental UI", r"elemental UI|palette"),
    )
]


class Section:
    __slots__ = ("number", "title", "line", "lines", "heading_date")

    def __init__(self, number: int, title: str, line: int):
        self.number = number
        self.title = title
        self.line = line
        self.lines: list[tuple[int, str]] = []  # body lines (lineno, text), the heading excluded
        m = DATE.search(title)
        self.heading_date = m.group(0) if m else ""

    @property
    def body(self) -> str:
        return "\n".join(t for _, t in self.lines)

    @property
    def date(self) -> str:
        if self.heading_date:
            return self.heading_date
        m = DATE.search(self.body)
        return m.group(0) if m else ""

    @property
    def is_round(self) -> bool:
        return bool(ROUND.search(self.title))

    @property
    def is_rev(self) -> bool:
        return bool(REV.search(self.title))

    @property
    def kind(self) -> str:
        if self.is_round:
            return "round"
        if FIX.search(self.title):
            return "fix"
        if BUILT.search(self.title):
            return "built"
        return "other"

    @property
    def screens(self) -> list[str]:
        return [name for name, pat in SCREENS if pat.search(self.title)]

    def pick_lines(self) -> tuple[list[tuple[int, str]], list[tuple[int, str]]]:
        """(strong, weak) pick lines, each (lineno, text): the body first (its Picked line is the record), then the
        heading (which may only say 'Shombith picked B')."""
        strong: list[tuple[int, str]] = []
        weak: list[tuple[int, str]] = []
        for lineno, text in self.lines + [(self.line, self.title)]:
            if any(p.search(text) for p in PICK_STRONG):
                strong.append((lineno, text))
            elif any(p.search(text) for p in PICK_WEAK):
                weak.append((lineno, text))
        return strong, weak

    @property
    def decided(self) -> bool:
        """A pick was recorded, or the round was explicitly superseded, scrapped or rejected."""
        strong, weak = self.pick_lines()
        if strong or weak:
            return True
        return any(p.search(t) for p in DECIDED_OTHER for _, t in self.lines + [(self.line, self.title)])

    @property
    def awaiting(self) -> bool:
        return bool(AWAITING.search(self.title) or AWAITING.search(self.body))

    def quotes(self) -> list[str]:
        """Every quoted span of the section, paragraph by paragraph (a quote may wrap over lines)."""
        out: list[str] = []
        para: list[str] = []

        def flush() -> None:
            if para:
                joined = " ".join(s.strip() for s in para)
                out.extend(q.strip() for q in QUOTE.findall(joined))
                para.clear()

        for _, text in self.lines:
            if text.strip():
                para.append(text)
            else:
                flush()
        flush()
        return out

    def pick_words(self) -> list[str]:
        return [q for q in self.quotes() if any(p.search(q) for p in PICK_QUOTE)]

    def rejected_words(self) -> list[str]:
        return [q for q in self.quotes() if any(p.search(q) for p in REJECT)]

    @property
    def run_in_editor(self) -> str:
        text = self.title + "\n" + self.body
        if NOT_RUN in text:
            return "no: " + NOT_RUN
        if APPROVED_IN_EDITOR.search(self.title):
            return "yes"
        if AWAITING_EDITOR.search(text):
            return "awaiting"
        return ""

    def picked_cell(self) -> str:
        strong, weak = self.pick_lines()
        if strong:
            return plain(strong[0][1])
        if weak:
            return plain(weak[0][1])
        if self.awaiting:
            return "awaiting"
        return ""

    def display_title(self) -> str:
        """The title without its trailing status parenthetical, e.g. '(2026-10-27, awaiting a pick)'."""
        t = self.title
        m = re.search(r"\s*\(([^()]*)\)\s*$", t)
        if m and (DATE.search(m.group(1)) or re.search(r"awaiting|built|approved|picked|NOT RUN|rejected", m.group(1), re.I)):
            t = t[: m.start()]
        return t.strip()

    def as_dict(self) -> dict:
        strong, weak = self.pick_lines()
        return {
            "number": self.number,
            "title": self.title,
            "line": self.line,
            "date": self.date,
            "kind": self.kind,
            "round": self.is_round,
            "rev": self.is_rev,
            "screens": self.screens,
            "pick_line": plain((strong or weak)[0][1]) if (strong or weak) else "",
            "pick_words": self.pick_words(),
            "rejected_words": self.rejected_words(),
            "run_in_editor": self.run_in_editor,
            "awaiting": self.awaiting,
            "decided": self.decided,
        }


def plain(text: str) -> str:
    """Markdown stripped and whitespace collapsed, for a table cell."""
    text = re.sub(r"\*\*|__|`", "", text)
    text = re.sub(r"^\s*[-*]\s+", "", text)
    return re.sub(r"\s+", " ", text).strip()


def parse(path: str = DOC) -> list[Section]:
    with open(path, encoding="utf-8", errors="replace") as fh:
        lines = fh.read().splitlines()
    sections: list[Section] = []
    current: Section | None = None
    fenced = False
    for i, raw in enumerate(lines, 1):
        if raw.startswith("```"):
            fenced = not fenced
        if not fenced:
            m = HEADING.match(raw)
            if m:
                current = Section(int(m.group(1)), m.group(2), i)
                sections.append(current)
                continue
            if OTHER_HEADING.match(raw):
                current = None  # an unnumbered '## ' heading ends the numbered section
                continue
        if current is not None:
            current.lines.append((i, raw))
    return sections


def cell(text: str, width: int) -> str:
    text = plain(text).replace("|", "/")
    if len(text) > width:
        text = text[: width - 3].rstrip() + "..."
    return text


def table(sections: list[Section]) -> str:
    rows = ["| section | date | screens | picked | his words | rejected words | run in editor |", "|---|---|---|---|---|---|---|"]
    for s in sections:
        rows.append(
            "| "
            + " | ".join(
                (
                    cell(f"{s.number} {s.display_title()}", 90),
                    s.date,
                    cell(", ".join(s.screens), 60),
                    cell(s.picked_cell(), 160),
                    cell(" / ".join(f'"{q}"' for q in s.pick_words()), 220),
                    cell(" / ".join(f'"{q}"' for q in s.rejected_words()), 220),
                    s.run_in_editor,
                )
            )
            + " |"
        )
    return "\n".join(rows)


def screen_counts(sections: list[Section]) -> list[dict]:
    counts: dict[str, dict] = {}
    for s in sections:
        for name in s.screens:
            c = counts.setdefault(
                name, {"screen": name, "sections": 0, "rounds": 0, "revs": 0, "built": 0, "fixes": 0, "numbers": []}
            )
            c["sections"] += 1
            c["numbers"].append(s.number)
            if s.is_rev:
                c["revs"] += 1
            if s.kind == "round":
                c["rounds"] += 1
            elif s.kind == "built":
                c["built"] += 1
            elif s.kind == "fix":
                c["fixes"] += 1
    return sorted(counts.values(), key=lambda c: (-c["sections"], c["screen"]))


def render_screens(counts: list[dict]) -> str:
    out = [
        "sections: '## N.' sections whose heading names the screen (the rebuild count); rounds: 'Rev N mockups / boards'",
        "sections among them; revs: sections naming any 'Rev N' (a round, or the build from one); built / fixes by heading.",
        f"{'screen':<28} {'sections':>8} {'rounds':>6} {'revs':>4} {'built':>5} {'fixes':>5}  sections",
    ]
    for c in counts:
        nums = ", ".join(str(n) for n in c["numbers"])
        out.append(
            f"{c['screen']:<28} {c['sections']:>8} {c['rounds']:>6} {c['revs']:>4} {c['built']:>5} {c['fixes']:>5}  {nums}"
        )
    return "\n".join(out)


def undecided(sections: list[Section], today: _dt.date) -> list[Section]:
    """Rounds dated more than STALE_DAYS before today with neither a pick line nor an 'awaiting' marker."""
    out: list[Section] = []
    for s in sections:
        if not s.is_round or not s.date:
            continue
        try:
            when = _dt.date.fromisoformat(s.date)
        except ValueError:
            continue
        if (today - when).days <= STALE_DAYS:
            continue
        if s.decided or s.awaiting:
            continue
        out.append(s)
    return out


def write_md(sections: list[Section], path: str = LESSONS, doc_path: str = DOC) -> str:
    """Replace the text between the markers with the generated table; the hand-written part stays byte for byte."""
    if not os.path.exists(path):
        raise FileNotFoundError(path)
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    if BEGIN not in text or END not in text or text.index(BEGIN) > text.index(END):
        raise ValueError(f"{path} lacks the markers {BEGIN} ... {END} (in that order)")
    head, rest = text.split(BEGIN, 1)
    _, tail = rest.split(END, 1)
    rounds = sum(1 for s in sections if s.is_round)
    rel = os.path.relpath(doc_path, ROOT) if doc_path.startswith(ROOT) else doc_path
    generated = (
        f"{BEGIN}\n"
        f"Generated by `python3 Tools/Build/ui_lessons.py --md` from `{rel}` "
        f"({len(sections)} sections, {rounds} mockup rounds). Do not edit between the markers; edit the doc and re-run.\n\n"
        f"{table(sections)}\n"
        f"{END}"
    )
    new = head + generated + tail
    if new != text:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(new)
    return new


SELF_TEST_DOC = """# A test doc

Preamble that belongs to no section; "go with nothing" here must not count.

## 1. Rev 2 mockups: the Hangar, three ways (2026-10-01, awaiting a pick)

**Asked (Shombith, 2026-10-01):** "I do not like this UI,
too much noise." Then: "keep the orb".

**On the canvas:** three boards (A, B, C), the picked seat marked by one thin line.

```
## 99. a heading inside a code fence is not a section; "go with Z" neither
```

**Picked (Shombith, 2026-10-02): "go with C, the deck".**

### 1.1 A sub-section (2026-10-03, awaiting in-editor approval)

Built from the pick; the old plate removed.

## 2. Rev 3 mockups: the Arcade cards (2026-10-05, awaiting a pick)

**Asked (Shombith):** "The game cards are still noisy, make them brighter."

## 3. Rev 4 boards: the Profile (2026-10-01)

**Asked (Shombith):** "give me a few better modern mockups, I do not love these".

## 4. Built: the Hangar list's card art (2026-10-03, NOT RUN IN EDITOR)

Shombith: "Go ahead with the colourful card renders". The (REMOVE) button stays.

## 5. Rev 5 mockups: the Voyage window (2026-10-01; superseded by the Rev 6 pick)

Six layouts; none taken. "I didn't much like the mockup ideas".

## Unnumbered heading

Lines here belong to no section; "Lets go with X" must not count.
"""


def self_test() -> int:
    import tempfile

    with tempfile.TemporaryDirectory() as tmp:
        doc = os.path.join(tmp, "doc.md")
        with open(doc, "w", encoding="utf-8") as fh:
            fh.write(SELF_TEST_DOC)
        secs = parse(doc)
        assert [s.number for s in secs] == [1, 2, 3, 4, 5], [s.number for s in secs]
        assert [s.date for s in secs] == ["2026-10-01", "2026-10-05", "2026-10-01", "2026-10-03", "2026-10-01"], [
            s.date for s in secs
        ]
        doc_lines = SELF_TEST_DOC.splitlines()
        expect = [i for i, t in enumerate(doc_lines, 1) if HEADING.match(t) and not t.startswith("## 99.")]
        assert [s.line for s in secs] == expect, ([s.line for s in secs], expect)
        assert [s.kind for s in secs] == ["round", "round", "round", "built", "round"], [s.kind for s in secs]
        one, two, three, four, five = secs
        # the decided round: the pick line, his words, the rejection, the screens, the sub-section's marker
        assert one.is_round and one.decided and one.awaiting, one.as_dict()
        assert one.pick_words() == ["go with C, the deck"], one.pick_words()
        assert one.rejected_words() == ["I do not like this UI, too much noise."], one.rejected_words()
        assert one.screens == ["Hangar"], one.screens
        assert one.picked_cell().startswith("Picked (Shombith, 2026-10-02)"), one.picked_cell()
        assert one.run_in_editor == "awaiting", one.run_in_editor
        assert one.display_title() == "Rev 2 mockups: the Hangar, three ways", one.display_title()
        # the awaiting round: no pick, excused by its heading
        assert not two.decided and two.awaiting, two.as_dict()
        assert two.rejected_words() == ["The game cards are still noisy, make them brighter."], two.rejected_words()
        assert two.screens == ["Arcade", "Game cards"], two.screens
        assert two.picked_cell() == "awaiting", two.picked_cell()
        # the old, undecided round (the negative control)
        assert not three.decided and not three.awaiting, three.as_dict()
        assert three.rejected_words() == ["give me a few better modern mockups, I do not love these"], three.rejected_words()
        # the build: run-in-editor marker, a strong pick in his words, an unquoted REMOVE ignored
        assert four.kind == "built" and four.run_in_editor == "no: " + NOT_RUN, four.as_dict()
        assert four.pick_words() == ["Go ahead with the colourful card renders"], four.pick_words()
        assert four.rejected_words() == [], four.rejected_words()
        assert four.screens == ["Hangar", "Game cards"], four.screens
        # the superseded round: decided against, no pick line, not awaiting; its dislike is still read
        assert five.decided and not five.awaiting and not any(five.pick_lines()), five.as_dict()
        assert five.rejected_words() == ["I didn't much like the mockup ideas"], five.rejected_words()
        # --check: section 3 fails when it is older than three days, and only it
        bad = undecided(secs, _dt.date(2026, 10, 20))
        assert [s.number for s in bad] == [3], [s.number for s in bad]
        assert undecided(secs, _dt.date(2026, 10, 4)) == [], "a round two or three days old is not stale yet"
        assert undecided(secs, _dt.date(2026, 10, 5)) and undecided(secs, _dt.date(2026, 10, 5))[0].number == 3
        # --screens
        counts = {c["screen"]: c for c in screen_counts(secs)}
        assert counts["Hangar"] == {
            "screen": "Hangar", "sections": 2, "rounds": 1, "revs": 1, "built": 1, "fixes": 0, "numbers": [1, 4]
        }, counts["Hangar"]
        assert counts["Game cards"]["sections"] == 2 and counts["Profile"]["rounds"] == 1, counts
        # --md keeps the hand-written part, replaces only what is between the markers, and is idempotent
        md = os.path.join(tmp, "UI_LESSONS.md")
        hand = "# Hand-written\n\nShombith's words stay.\n\n"
        with open(md, "w", encoding="utf-8") as fh:
            fh.write(hand + BEGIN + "\nSTALE TABLE\n" + END + "\n\nA footer.\n")
        new = write_md(secs, md, doc)
        assert new.startswith(hand + BEGIN) and new.endswith(END + "\n\nA footer.\n"), new[:80]
        assert "STALE TABLE" not in new and "| 1 Rev 2 mockups: the Hangar, three ways | 2026-10-01 | Hangar |" in new, new
        assert write_md(secs, md, doc) == new, "--md is not idempotent"
        with open(os.path.join(tmp, "nomarkers.md"), "w", encoding="utf-8") as fh:
            fh.write("no markers\n")
        try:
            write_md(secs, os.path.join(tmp, "nomarkers.md"), doc)
            raise AssertionError("a file without markers must be refused")
        except ValueError:
            pass
        # the table never breaks a row: a pipe in a quote becomes a slash
        assert cell("a | b", 20) == "a / b" and cell("x" * 30, 10) == "xxxxxxx...", (cell("a | b", 20), cell("x" * 30, 10))
    # the real doc, when it is there: it parses, and the Rev 29 pick is read as a pick
    if os.path.exists(DOC):
        real = parse(DOC)
        assert len(real) >= 50, len(real)
        decks = [s for s in real if any("the deck" in q for q in s.pick_words())]
        assert decks and all(s.decided for s in decks), "the Rev 29 pick 'go with C, the deck' was not read"
        assert any(s.run_in_editor == "yes" for s in real), "no section reads as approved in editor"
    print("ui_lessons: self-test OK")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--doc", default=DOC, help="the doc to read (default Docs/MENU_REDESIGN_B.md)")
    ap.add_argument("--out", default=LESSONS, help="the lessons file --md writes into (default the frogma reference)")
    ap.add_argument("--today", default=None, help="YYYY-MM-DD for --check (default today)")
    ap.add_argument("--md", action="store_true", help="write the table between the markers of the lessons file")
    ap.add_argument("--check", action="store_true", help="fail on a round older than 3 days with no pick and no 'awaiting'")
    ap.add_argument("--screens", action="store_true", help="per-screen counts of sections and rounds")
    ap.add_argument("--json", action="store_true", help="every section's fields as JSON")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        try:
            return self_test()
        except AssertionError as exc:
            print(f"ui_lessons: self-test FAILED: {exc}", file=sys.stderr)
            return 1
    if not os.path.exists(args.doc):
        print(f"ui_lessons: no doc at {args.doc}", file=sys.stderr)
        return 2
    try:
        today = _dt.date.fromisoformat(args.today) if args.today else _dt.date.today()
    except ValueError:
        print(f"ui_lessons: --today wants YYYY-MM-DD, not {args.today}", file=sys.stderr)
        return 2
    sections = parse(args.doc)
    rc = 0
    did = False
    if args.json:
        print(json.dumps([s.as_dict() for s in sections], indent=2))
        did = True
    if args.screens:
        print(render_screens(screen_counts(sections)))
        did = True
    if args.md:
        try:
            write_md(sections, args.out, args.doc)
        except (FileNotFoundError, ValueError) as exc:
            print(f"ui_lessons: {exc}", file=sys.stderr)
            return 2
        rounds = sum(1 for s in sections if s.is_round)
        print(f"ui_lessons: wrote {len(sections)} sections ({rounds} rounds) into {os.path.relpath(args.out, ROOT)}")
        did = True
    if args.check:
        rounds = [s for s in sections if s.is_round]
        bad = undecided(sections, today)
        for s in bad:
            print(f"ui_lessons: UNDECIDED section {s.number} (line {s.line}, dated {s.date}): {s.display_title()}")
        if bad:
            print(
                f"ui_lessons: --check FAILED: {len(bad)} of {len(rounds)} rounds dated more than {STALE_DAYS} days before "
                f"{today} record neither a pick nor 'awaiting'. Record the pick in the round's section "
                f"(**Picked (name, date): \"...\"**) or say what it awaits."
            )
            rc = 1
        else:
            print(f"ui_lessons: --check OK: every one of the {len(rounds)} rounds records a pick or says what it awaits ({today})")
        did = True
    if not did:
        print(table(sections))
    return rc


if __name__ == "__main__":
    sys.exit(main())

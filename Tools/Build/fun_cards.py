#!/usr/bin/env python3
"""The one-minute fun card: what a person said after flying a vessel's game, turned into elemental-loop sign-offs.

Garrett, 2026-11-12 (Docs/ElementalLoop/PLAYBOOK.md section 7, missing scaffolding 1): step 1 of the loop ends when
ONE human flew the vessel's game and signs it off as fun, and step 2 when one human played the four rungs and signs
them off. Until now that sign-off was typed by hand with `elemental_loop.py set`, so nobody recorded one. The card
asks it where the flight happens: when Play stops after an arcade match, the editor's Fun Card
(Assets/_Scripts/Editor/Diagnostics/FunCard.cs) asks three taps, writes
`funcard_<Mode>_<Vessel>_<time>.json` beside the flight record and sends it to the person's bugledger/<user> branch
with the Bug Hunt send. This script reads those answers and records what they sign off.

    python3 Tools/Build/fun_cards.py                 every answer, newest first (local folders and every bugledger branch)
    python3 Tools/Build/fun_cards.py apply           record the sign-offs they make into loop.json (and STATUS.md)
    python3 Tools/Build/fun_cards.py apply --dry-run say what apply would record, write nothing
    python3 Tools/Build/fun_cards.py --dir <folder>  read cards from a folder too (repeatable)
    python3 Tools/Build/fun_cards.py --self-test     known answers and negative controls

What a card signs off (the latest answer per person, vessel, game and rung wins):
- step 1 (fun) for the vessel and the game's element: a "fun: yes" at any rung.
- step 2 (the ladder): step 1 is done and the same person answered "fun: yes" and "rung: right" on I1, I2, I3 and I4.
A card never takes a sign-off back. A later "almost" or "no" on a signed-off step is printed as a warning for the
person to decide (`elemental_loop.py set ... redo`), because one bad run is not a verdict.

The element is the game's PRIMARY element (the game's own ModeGenre answer, read through elemental_loop.cards()).

Standard library only.
"""
import argparse
import glob
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import elemental_loop as loop  # noqa: E402

SCHEMA = "cosmic-shore.funcard/1"
FUN = ("yes", "almost", "no")
RUNG = ("easy", "right", "hard")          # too easy, right for the rung's goal, too hard
WEAKEST = ("Verb", "Flow", "Feedback", "Rivals", "Fairness")   # TIME_GAME_FUN_SCORES.md; Ladder is the rung answer
RUNG_GOAL = {1: "intro", 2: "fun", 3: "challenging", 4: "very hard"}
REPORT_DIR = "BugLedger/shared/reports"

# bugledger/<slug> -> the person (the slug is the editor machine's user name; Docs/TEAM.json names the people).
PEOPLE = {"gradi": "Garrett", "belac": "Caleb", "yskha": "Yash", "shombith": "Shombith"}


# ---------------------------------------------------------------- reading the cards


def problems_of(card):
    """What is wrong with one card, as sentences. Empty when it can sign something off."""
    out = []
    if card.get("schema") != SCHEMA:
        out.append(f"schema is '{card.get('schema')}', not {SCHEMA}")
    for key in ("mode", "hull", "intensity", "fun", "date"):
        if card.get(key) in (None, ""):
            out.append(f"no '{key}'")
    if card.get("fun") not in FUN + (None, ""):
        out.append(f"fun '{card.get('fun')}' is not one of {', '.join(FUN)}")
    if card.get("rung") not in RUNG + (None, ""):
        out.append(f"rung '{card.get('rung')}' is not one of {', '.join(RUNG)}")
    if card.get("weakest") not in WEAKEST + (None, ""):
        out.append(f"weakest '{card.get('weakest')}' is not one of {', '.join(WEAKEST)}")
    if not isinstance(card.get("intensity"), int) or not 1 <= card.get("intensity", 0) <= 4:
        out.append(f"intensity '{card.get('intensity')}' is not 1-4")
    return out


def person_of(card, slug):
    return card.get("person") or PEOPLE.get(slug) or slug or "unknown"


def from_dirs(dirs):
    out = []
    for d in dirs:
        for path in sorted(glob.glob(os.path.join(d, "funcard_*.json"))):
            try:
                with open(path, encoding="utf-8") as fh:
                    card = json.load(fh)
            except (OSError, ValueError):
                continue
            card["_source"] = path
            card["_person"] = person_of(card, card.get("by", ""))
            out.append(card)
    return out


def _git(*args):
    return subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True, check=False)


def from_branches(fetch=True):
    """Every funcard_*.json on every origin/bugledger/* branch."""
    if fetch:
        _git("fetch", "-q", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*")
    refs = _git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/bugledger/").stdout.split()
    out = []
    for ref in refs:
        slug = ref.rsplit("/", 1)[-1]
        names = _git("ls-tree", "-r", "--name-only", ref, f"{REPORT_DIR}/").stdout.split("\n")
        for name in names:
            if "/fun/" not in name or not os.path.basename(name).startswith("funcard_"):
                continue
            shown = _git("show", f"{ref}:{name}")
            try:
                card = json.loads(shown.stdout)
            except ValueError:
                continue
            card["_source"] = f"{ref}:{name}"
            card["_person"] = person_of(card, slug)
            out.append(card)
    return out


# ---------------------------------------------------------------- what the cards sign off


def element_of(mode, hull, all_cards):
    """The game's primary element, when the game is one of the hull's ARCADE games (an arena card such as Regatta
    signs nothing off: steps 1-2 are the vessel's own game)."""
    card = next((c for c in all_cards if c["mode"] == mode and "ArcadeGames" in c["lists"] and hull in c["hulls"]), None)
    return card["elements"][0] if card and card["elements"] else None


def latest(cards):
    """The latest valid answer per (person, vessel, mode, intensity)."""
    best = {}
    for c in cards:
        if problems_of(c):
            continue
        key = (c["_person"], c["hull"], c["mode"], c["intensity"])
        if key not in best or c["date"] > best[key]["date"]:
            best[key] = c
    return best


def signoffs(cards, all_cards, data):
    """(records, warnings). A record is (vessel, element, step, by, evidence); warnings are sentences."""
    best = latest(cards)
    groups = {}
    for (person, hull, mode, intensity), c in best.items():
        element = element_of(mode, hull, all_cards)
        if element is None:
            continue
        groups.setdefault((person, hull, element, mode), {})[intensity] = c

    records, warnings = [], []
    signed1 = set()
    for (person, hull, element, mode), rungs in sorted(groups.items()):
        have = data["vessels"].get(hull, {}).get(element, {})
        yes = sorted(i for i, c in rungs.items() if c["fun"] == "yes")
        if yes:
            signed1.add((hull, element))
            if have.get("1", {}).get("state") != "done":
                c = rungs[yes[-1]]
                records.append((hull, element, "1", person,
                                f"fun card: {person} flew {mode} at I{c['intensity']} and said fun "
                                f"({c['date']}{'; ' + c['note'] if c.get('note') else ''})"))
        for i, c in sorted(rungs.items()):
            if c["fun"] != "yes" and have.get("1", {}).get("state") == "done":
                warnings.append(f"{hull} {element}: step 1 is signed off, but {person}'s latest {mode} I{i} card says "
                                f"'{c['fun']}'" + (f" (weakest: {c['weakest']})" if c.get("weakest") else "")
                                + f". Decide: elemental_loop.py set {hull} {element} 1 redo ...")
        ladder = all(i in rungs and rungs[i]["fun"] == "yes" and rungs[i].get("rung") == "right" for i in (1, 2, 3, 4))
        step1 = have.get("1", {}).get("state") == "done" or (hull, element) in signed1
        if ladder and step1 and have.get("2", {}).get("state") != "done":
            records.append((hull, element, "2", person,
                            f"fun cards: {person} flew {mode} I1-I4, each fun and right for its rung "
                            f"(last {max(c['date'] for c in rungs.values())})"))
    # One sign-off per vessel, element and step (two people, or two games of one element, sign it once).
    seen, unique = set(), []
    for r in records:
        if r[:3] not in seen:
            seen.add(r[:3])
            unique.append(r)
    return unique, warnings


def apply(records, data, today=None):
    for hull, element, step, by, evidence in sorted(records, key=lambda r: r[2]):
        loop.set_step(data, hull, element, step, "done", by, evidence, "human", data.get("current_loop", 1), today)
    return data


# ---------------------------------------------------------------- self-test


def _card(**kw):
    c = {"schema": SCHEMA, "mode": "Switchback", "hull": "Dolphin", "intensity": 2, "fun": "yes", "rung": "right",
         "weakest": None, "date": "2026-11-12 10:00:00", "_person": "Garrett", "_source": "test"}
    c.update(kw)
    return c


def self_test():
    failures = []

    def expect(cond, what):
        if not cond:
            failures.append(what)

    data = loop.load()
    all_cards = loop.cards()
    vessels = loop.roster()
    expect(element_of("Switchback", "Dolphin", all_cards) == "Time", "Switchback is the Dolphin's Time game")
    expect(element_of("NoSuchMode", "Dolphin", all_cards) is None, "an unknown mode has no element")
    expect(element_of("Regatta", "Sparrow", all_cards) is None, "an arena game (Regatta) signs off no vessel step")
    expect(element_of("Switchback", "Rhino", all_cards) is None, "a game that does not seat the hull signs nothing off")

    blank = json.loads(json.dumps(data))
    blank["vessels"].pop("Dolphin", None)
    # A "yes" signs off step 1; nothing else.
    recs, _ = signoffs([_card()], all_cards, blank)
    expect([r[:3] for r in recs] == [("Dolphin", "Time", "1")], f"one fun 'yes' signs off step 1 only: {recs}")
    expect(recs and recs[0][3] == "Garrett", "the sign-off names the person")
    # Negative controls.
    recs, _ = signoffs([_card(fun="almost", weakest="Feedback")], all_cards, blank)
    expect(recs == [], "an 'almost' signs nothing off")
    recs, _ = signoffs([_card(schema="cosmic-shore.funcard/0")], all_cards, blank)
    expect(recs == [], "a card of another schema signs nothing off")
    recs, _ = signoffs([_card(intensity=7)], all_cards, blank)
    expect(recs == [], "a card with no real rung signs nothing off")
    recs, _ = signoffs([_card(), _card(fun="no", date="2026-11-12 11:00:00")], all_cards, blank)
    expect(recs == [], "the latest answer wins: a later 'no' on the same rung replaces an earlier 'yes'")
    # The ladder: four rungs, each fun and right.
    four = [_card(intensity=i, date=f"2026-11-12 1{i}:00:00") for i in (1, 2, 3, 4)]
    recs, _ = signoffs(four, all_cards, blank)
    expect(sorted(r[2] for r in recs) == ["1", "2"], f"four fun, right rungs sign off steps 1 and 2: {recs}")
    three = four[:3] + [_card(intensity=4, rung="hard", date="2026-11-12 14:00:00")]
    recs, _ = signoffs(three, all_cards, blank)
    expect(sorted(r[2] for r in recs) == ["1"], "a rung that was too hard keeps step 2 open")
    other = four[:3] + [_card(intensity=4, _person="Yash")]
    recs, _ = signoffs(other, all_cards, blank)
    expect(sorted(r[2] for r in recs) == ["1"], "the four rungs must be one person's")
    # Apply validates, and never takes a sign-off back.
    applied = apply(signoffs(four, all_cards, blank)[0], json.loads(json.dumps(blank)), "2026-11-12")
    expect(not loop.validate(applied, vessels), "applied sign-offs validate: " + "; ".join(loop.validate(applied, vessels)))
    expect(applied["vessels"]["Dolphin"]["Time"]["2"]["kind"] == "human", "a card's sign-off is a human's")
    recs, warns = signoffs([_card(fun="no", weakest="Rivals")], all_cards, applied)
    expect(recs == [] and warns and "redo" in warns[0], "a later 'no' on a signed-off step warns, never undoes")
    recs, _ = signoffs(four, all_cards, applied)
    expect(recs == [], "an already signed-off step is not signed twice")
    if failures:
        print("SELF-TEST FAILED:\n  " + "\n  ".join(failures))
        return 1
    print("self-test OK (element lookup, step 1 and step 2 sign-offs, 9 negative controls, apply validates)")
    return 0


# ---------------------------------------------------------------- main


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("command", nargs="?", default="show", choices=("show", "apply"))
    p.add_argument("--dir", action="append", default=[], help="a folder of funcard_*.json to read too")
    p.add_argument("--no-fetch", action="store_true", help="read the bugledger branches as already fetched")
    p.add_argument("--no-branches", action="store_true", help="read only --dir folders")
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args(argv)
    if a.self_test:
        return self_test()

    cards = from_dirs(a.dir) + ([] if a.no_branches else from_branches(fetch=not a.no_fetch))
    data = loop.load()
    all_cards = loop.cards()
    bad = [(c["_source"], problems_of(c)) for c in cards if problems_of(c)]

    if a.command == "show":
        if not cards:
            print("no fun cards yet: fly an arcade game in the editor and answer the card when Play stops")
            return 0
        for c in sorted(cards, key=lambda c: c.get("date", ""), reverse=True):
            if problems_of(c):
                continue
            extra = "".join([f"  rung {c['rung']}" if c.get("rung") else "",
                             f"  weakest {c['weakest']}" if c.get("weakest") else "",
                             f"  \"{c['note']}\"" if c.get("note") else ""])
            print(f"{c['date']}  {c['_person']:<9} {c['hull']:<11} {c['mode']:<16} I{c['intensity']} "
                  f"({RUNG_GOAL[c['intensity']]})  fun {c['fun']}{extra}")
        for src, why in bad:
            print(f"skipped {src}: {'; '.join(why)}")
        return 0

    records, warnings = signoffs(cards, all_cards, data)
    for w in warnings:
        print("warning: " + w)
    if not records:
        print("nothing new to sign off")
        return 0
    for hull, element, step, by, evidence in records:
        print(f"{'would record' if a.dry_run else 'recording'} {hull} {element} step {step} done by {by}: {evidence}")
    if a.dry_run:
        return 0
    data = apply(records, data)
    problems = loop.validate(data, loop.roster())
    if problems:
        print("not written:\n  " + "\n  ".join(problems))
        return 1
    loop.save(data)
    with open(loop.STATUS_MD, "w", encoding="utf-8") as fh:
        fh.write(loop.markdown(loop.build(data=data)))
    print("loop.json and STATUS.md written")
    return 0


if __name__ == "__main__":
    sys.exit(main())

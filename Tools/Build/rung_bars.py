#!/usr/bin/env python3
"""Per-rung bars: whether each arcade game's I1-I4 ladder does what its rungs promise, checked from recorded flights.

Docs/ElementalLoop/PLAYBOOK.md step 2 and section 7 item 2 (Garrett, 2026-11-12: "defaults, then learned"). Each rung
has a bar: the share of human flights that finish, and a band for how long a finished run takes. The bars start as the
defaults in Docs/ElementalLoop/rung_bars.json (a game may override them there) and are LEARNED from the one-minute fun
card (Tools/Build/fun_cards.py): once a game's rung has MIN_LEARN runs a person rated "right" for that rung, its bar
becomes what those runs showed. The flights are every human flight record on the bugledger branches
(HumanFlightRecorder, sent after Play by the Bug Hunt send) plus any --dir folders.

    python3 Tools/Build/rung_bars.py                    every vessel and arcade game with flights: rung by rung
    python3 Tools/Build/rung_bars.py --vessel Dolphin   one vessel
    python3 Tools/Build/rung_bars.py --no-fetch         read the bugledger branches as already fetched
    python3 Tools/Build/rung_bars.py --self-test        known answers and negative controls

A rung reads: pass (finish rate and median time inside the bar), FAIL (which and by how much), or "few" (fewer than
MIN_FLIGHTS flights: fly it). The ladder also fails when a higher rung is finished MORE often than the rung below it
(a rung with no added demand). A check informs step 2; one human's fun sign-off still closes it (PLAYBOOK step 2).

Standard library only.
"""
import argparse
import glob
import json
import os
import statistics
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import elemental_loop as loop  # noqa: E402
import fun_cards  # noqa: E402
from learn_drives_from_flights import on_shipped_family  # noqa: E402  - Switchback: Blast Run flights only

BARS_JSON = os.path.join(REPO, "Docs", "ElementalLoop", "rung_bars.json")
MIN_FLIGHTS = 3       # fewer human flights than this on a rung: no verdict
MIN_LEARN = 3         # "right" fun cards on a rung before its bar is learned
LEARN_MARGIN = 0.15   # a learned time band is the "right" runs' range widened by this share
LEARN_FINISH_SLACK = 0.10


# ---------------------------------------------------------------- flights


def _head(text, n=6000):
    """The flight's header fields (they come first; samples follow)."""
    out = {}
    for key in ("schema", "mode", "hull", "intensity", "pilot", "finished", "raceSeconds", "date", "ended", "funType"):
        i = text.find(f'"{key}":', 0, n)
        if i < 0:
            continue
        j = i + len(key) + 3
        if text[j] == '"':
            out[key] = text[j + 1:text.index('"', j + 1)]
        else:
            k = j
            while k < len(text) and text[k] not in ",}\n":
                k += 1
            raw = text[j:k]
            out[key] = True if raw == "true" else False if raw == "false" else float(raw) if raw else None
    return out


def flights_from_branches(fetch=True):
    git = lambda *a: subprocess.run(["git", "-C", REPO, *a], capture_output=True, check=False)  # noqa: E731
    if fetch:
        git("fetch", "-q", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*")
    refs = git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/bugledger/").stdout.decode().split()
    objects = []
    for ref in refs:
        for line in git("ls-tree", "-r", ref, f"{fun_cards.REPORT_DIR}/").stdout.decode().splitlines():
            meta, name = line.split("\t", 1)
            if "/flights/flight_" in name:
                objects.append((meta.split()[2], f"{ref}:{name}", ref.rsplit("/", 1)[-1]))
    if not objects:
        return []
    seen, out = set(), []
    proc = subprocess.Popen(["git", "-C", REPO, "cat-file", "--batch"], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    proc.stdin.write("".join(f"{sha}\n" for sha, _, _ in objects).encode())
    proc.stdin.close()
    for sha, source, slug in objects:
        header = proc.stdout.readline().split()
        size = int(header[2])
        blob = proc.stdout.read(size)
        proc.stdout.read(1)
        if sha in seen:
            continue
        seen.add(sha)
        f = _head(blob[:6000].decode("utf-8", "replace"))
        f["_source"], f["_person"] = source, fun_cards.PEOPLE.get(slug, slug)
        out.append(f)
    proc.wait()
    return out


def flights_from_dirs(dirs):
    out = []
    for d in dirs:
        for path in glob.glob(os.path.join(d, "flight_*.json")):
            with open(path, encoding="utf-8", errors="replace") as fh:
                f = _head(fh.read(6000))
            f["_source"], f["_person"] = path, "local"
            out.append(f)
    return out


def human(f):
    """A race a person flew to its end (the AI holding the hull the whole race is 'ai'; 'mixed' is a person with the
    toggle). A run left early ('play stopped', 'left before the end') is a person testing something, not a verdict on
    the rung, so it is not counted. A mode whose course family changed counts its shipped family only (Switchback:
    Blast Run since 2026-10-29; a Classic race says nothing about today's rung, learn_drives_from_flights.SHIPPED_FAMILY)."""
    if not on_shipped_family(dict(f, name=str(f.get("_source", "")).rsplit("/", 1)[-1])):
        return False
    return f.get("pilot", "human") != "ai" and f.get("ended", "race over") == "race over" and f.get("mode") and f.get("hull") not in (None, "", "Unknown") \
        and isinstance(f.get("intensity"), (int, float)) and 1 <= int(f["intensity"]) <= 4


# ---------------------------------------------------------------- bars


def load_bars(path=BARS_JSON):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def bar_for(bars, mode, rung, cards=(), flights=()):
    """(bar, source). Source is 'default', 'game' (rung_bars.json override) or 'learned' (fun cards rated right)."""
    bar = dict(bars["defaults"][str(rung)])
    source = "default"
    override = bars.get("games", {}).get(mode, {}).get(str(rung))
    if override:
        bar.update(override)
        source = "game"
    right = [c for c in cards if c["mode"] == mode and c["intensity"] == rung and c.get("rung") == "right"
             and not fun_cards.problems_of(c)]
    if len(right) >= MIN_LEARN:
        by_file = {os.path.basename(f["_source"].split(":")[-1]): f for f in flights}
        runs = [by_file.get(c.get("flight")) for c in right]
        runs = [f for f in runs if f]
        timed = [f["raceSeconds"] for f in runs if f.get("finished") and f.get("raceSeconds")]
        finish = sum(1 for c in right if c.get("finished")) / len(right)
        bar["min_finish"] = round(max(0.0, finish - LEARN_FINISH_SLACK), 2)
        if timed:
            lo, hi = min(timed), max(timed)
            bar["seconds"] = [round(lo * (1 - LEARN_MARGIN)), round(hi * (1 + LEARN_MARGIN))]
        source = f"learned from {len(right)} 'right' cards"
    return bar, source


def check(flights, bars, cards=(), vessel=None):
    """{(hull, mode): {rung: verdict}} plus ladder problems. A verdict is a dict with n, finish, median, bar, state."""
    arcade = {}
    for c in loop.cards():
        if "ArcadeGames" in c["lists"]:
            for h in c["hulls"]:
                arcade[(h, c["mode"])] = c
    groups = {}
    for f in flights:
        if not human(f):
            continue
        key = (f["hull"], f["mode"])
        if key not in arcade or (vessel and f["hull"].lower() != vessel.lower()):
            continue
        groups.setdefault(key, {}).setdefault(int(f["intensity"]), []).append(f)
    out = {}
    for key, rungs in sorted(groups.items()):
        res = {}
        for rung in (1, 2, 3, 4):
            runs = rungs.get(rung, [])
            bar, source = bar_for(bars, key[1], rung, cards, flights)
            v = {"n": len(runs), "bar": bar, "source": source, "problems": []}
            if len(runs) < MIN_FLIGHTS:
                v["state"] = "few"
            else:
                v["finish"] = sum(1 for f in runs if f.get("finished")) / len(runs)
                timed = [f["raceSeconds"] for f in runs if f.get("finished") and f.get("raceSeconds")]
                v["median"] = statistics.median(timed) if timed else None
                if v["finish"] < bar["min_finish"]:
                    v["problems"].append(f"finish {v['finish']:.0%} < {bar['min_finish']:.0%}")
                if "max_finish" in bar and v["finish"] > bar["max_finish"]:
                    v["problems"].append(f"finish {v['finish']:.0%} > {bar['max_finish']:.0%} (no challenge)")
                lo, hi = bar["seconds"]
                if v["median"] is not None and not lo <= v["median"] <= hi:
                    v["problems"].append(f"median {v['median']:.0f}s outside {lo}-{hi}s")
                v["state"] = "FAIL" if v["problems"] else "pass"
            res[rung] = v
        ladder = []
        measured = [(r, res[r]["finish"]) for r in (1, 2, 3, 4) if "finish" in res[r]]
        for (r1, a), (r2, b) in zip(measured, measured[1:]):
            if b > a + 0.05:
                ladder.append(f"I{r2} is finished more often than I{r1} ({b:.0%} > {a:.0%}): no added demand")
        out[key] = {"rungs": res, "ladder": ladder, "element": (arcade[key]["elements"] or ["?"])[0]}
    return out


def text(result):
    lines = []
    for (hull, mode), r in result.items():
        lines.append(f"{hull} {mode} ({r['element']})")
        for rung, v in r["rungs"].items():
            goal = fun_cards.RUNG_GOAL[rung]
            if v["state"] == "few":
                lines.append(f"  I{rung} {goal:<11} few     {v['n']} flight(s): fly it")
                continue
            med = f"{v['median']:.0f}s" if v.get("median") is not None else "-"
            why = f"  {'; '.join(v['problems'])}" if v["problems"] else ""
            lines.append(f"  I{rung} {goal:<11} {v['state']:<7} {v['n']:>3} flights, finish {v['finish']:.0%}, "
                         f"median {med}  (bar: {v['source']}){why}")
        for problem in r["ladder"]:
            lines.append(f"  ladder: {problem}")
    return "\n".join(lines) or "no human flights on any arcade game yet"


# ---------------------------------------------------------------- self-test


def self_test():
    failures = []

    def expect(cond, what):
        if not cond:
            failures.append(what)

    bars = load_bars()
    for r in ("1", "2", "3", "4"):
        expect(r in bars["defaults"] and "min_finish" in bars["defaults"][r] and len(bars["defaults"][r]["seconds"]) == 2,
               f"rung_bars.json has a default bar for I{r}")
    expect(_head('{"mode":"Switchback","hull":"Dolphin","intensity":2,"finished":true,"raceSeconds":221.18,')
           == {"mode": "Switchback", "hull": "Dolphin", "intensity": 2.0, "finished": True, "raceSeconds": 221.18},
           "the header reader reads a flight's fields")

    def fl(rung, finished=True, secs=100.0, hull="Dolphin", mode="Switchback", pilot="human", name=None):
        return {"mode": mode, "hull": hull, "intensity": rung, "finished": finished, "raceSeconds": secs, "pilot": pilot,
                "funType": "BlastRun" if mode == "Switchback" else None,
                "_source": f"x:{name or 'flight_%s_%d_%f.json' % (mode, rung, secs)}", "_person": "t"}

    good = [fl(r, secs=60 + 20 * r + i) for r in (1, 2, 3, 4) for i in range(3)]
    res = check(good, bars)[("Dolphin", "Switchback")]
    expect(all(res["rungs"][r]["state"] == "pass" for r in (1, 2, 3, 4)), f"a sound ladder passes: {res}")
    expect(res["element"] == "Time", "Switchback reads as the Dolphin's Time game")
    # Negative controls.
    bad = [fl(1, finished=i == 0) for i in range(3)] + good[3:]
    expect(check(bad, bars)[("Dolphin", "Switchback")]["rungs"][1]["state"] == "FAIL", "an I1 most people fail is caught")
    slow = [fl(2, secs=9000) for _ in range(3)] + good[:3]
    expect(check(slow, bars)[("Dolphin", "Switchback")]["rungs"][2]["state"] == "FAIL", "a rung that runs far too long is caught")
    inverted = [fl(1, finished=i < 2) for i in range(3)] + [fl(2) for _ in range(3)]
    expect(check(inverted, bars)[("Dolphin", "Switchback")]["ladder"], "a higher rung finished more often is caught")
    expect(check([fl(1), fl(1)], bars)[("Dolphin", "Switchback")]["rungs"][1]["state"] == "few", "two flights give no verdict")
    expect(not check([fl(1, pilot="ai") for _ in range(3)], bars), "AI-flown records are not human flights")
    left = [dict(fl(1, finished=False), ended="play stopped") for _ in range(3)]
    expect(not check(left, bars), "a run left early (Play stopped) is not counted")
    expect(not check([fl(1, mode="Regatta") for _ in range(3)], bars), "an arena game (Regatta) is not a rung check")
    expect(not check([dict(fl(1), funType=None) for _ in range(3)], bars),
           "a Switchback flight on the retired Classic ladder (no funType) is not counted")
    # Learned: three "right" cards replace the default band.
    runs = [fl(3, secs=s, name=f"flight_Switchback_Dolphin_{i}.json") for i, s in enumerate((200, 210, 220))]
    cards = [fun_cards._card(intensity=3, flight=f"flight_Switchback_Dolphin_{i}.json", finished=True) for i in range(3)]
    bar, source = bar_for(bars, "Switchback", 3, cards, runs)
    expect(source.startswith("learned") and bar["seconds"] == [170, 253], f"three 'right' cards learn the band: {bar} {source}")
    bar, source = bar_for(bars, "Switchback", 3, cards[:2], runs)
    expect(source != "learned" and not source.startswith("learned"), "two 'right' cards do not learn a bar yet")
    if failures:
        print("SELF-TEST FAILED:\n  " + "\n  ".join(failures))
        return 1
    print("self-test OK (defaults, header reader, a sound ladder, 9 negative controls, a learned bar)")
    return 0


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--vessel")
    p.add_argument("--dir", action="append", default=[])
    p.add_argument("--no-fetch", action="store_true")
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args(argv)
    if a.self_test:
        return self_test()
    flights = flights_from_dirs(a.dir) + flights_from_branches(fetch=not a.no_fetch)
    cards = fun_cards.from_dirs(a.dir) + fun_cards.from_branches(fetch=False)
    print(text(check(flights, load_bars(), cards, a.vessel)))
    return 0


if __name__ == "__main__":
    sys.exit(main())

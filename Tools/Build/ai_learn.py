#!/usr/bin/env python3
"""ai_learn.py - the AIs relearn from every new race people fly, with nobody running anything (Yash, 2026-10-26).

Yash's rule: "I don't wanna click any commands or anything. Never. ... every time I'm playing a new game, the record
should just come here and then you evaluate what type of data the new game has ... and then relearn its AI navigation
in order to win the race." Unity already sends every recorded race to origin/bugledger/<user> after Play
(HumanFlightRecorder + BugHuntAutoSend). This is the other end: the scheduled "AI learning sweep" (a Claude scheduled
task, Docs/AI_SYSTEM/AUTO_LEARNING.md) runs it, and it

  1. finds the flights it has not learned from yet (Docs/AI_SYSTEM/ai_learning_seen.json is the memory);
  2. for every mode with new flights, runs each learner that mode feeds, writing:
       learn_drives_from_flights.py --mode <Mode> --write   every hull whose prefab names a boost policy the learner has a
                                                            measure for (Sparrow, Scarab, Serpent, Squirrel, Rhino, Dolphin,
                                                            ...), plus the Manta's Soar and the Stoat's pair: <asset>_<Mode>
                                                            variants + LEARNED_DRIVES[_<Mode>].md; Regatta also its race lines
       learn_from_flights.py [--mode SkimRace] --write      the Squirrel's skim line (Regatta writes; Skim Race reports)
     then each learner's own --check; a hull that cannot be written is reported with the reason;
  3. measures how the AI rivals raced in the new flights against the person (regatta_rival_report.py);
  4. writes Docs/Reports/AI_LEARNING.md: what was new, what each learner changed, the person's and the AI's pace, and
     every vessel/mode whose flights no learner reads yet (the session's to-do).

A learner writes nothing until it has three finished races of a hull (its own rule), so a single race only counts.
Modes on HOLD are reported, never written (another collaborator's call).

    python3 Tools/Build/ai_learn.py             # status: what is new since the last learn (changes nothing)
    python3 Tools/Build/ai_learn.py --run       # learn from it, write the report and the memory
    python3 Tools/Build/ai_learn.py --self-test
"""
import argparse, collections, datetime, hashlib, json, os, re, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import learn_from_flights as lf  # noqa: E402  - the one reader of the flights on every branch
import learn_drives_from_flights as ld  # noqa: E402  - which hull a drive learner covers (its prefab's boost policy)

SEEN = os.path.join(REPO, "Docs", "AI_SYSTEM", "ai_learning_seen.json")
REPORT = os.path.join(REPO, "Docs", "Reports", "AI_LEARNING.md")
SKIM_LINE_MODES = ("Regatta", "SkimRace")   # learn_from_flights.py: the Squirrel's skim line (SkimRace: report only)

# Modes whose AI is someone else's call: reported, never written.
# Warpline was held for Garrett's Stoat configuration (issue 1198) until Yash lifted it on 2026-10-26 ("re train Stoat").
HOLD = {}


def py(*args, timeout=900):
    r = subprocess.run([sys.executable, *args], cwd=REPO, capture_output=True, text=True, timeout=timeout)
    return r.returncode, (r.stdout + r.stderr).strip()


def git(*args):
    r = subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True)
    return r.stdout if r.returncode == 0 else ""


def load_flights():
    """[{path, mode, hull, intensity, finished, seconds, race, date, text}] for every flight on every origin branch."""
    git("fetch", "-q", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*")
    out = []
    for path, text in lf.flights_on_branches():
        try:
            d = json.loads(text)
        except ValueError:
            continue
        out.append(dict(path=path, mode=d.get("mode") or "Unknown", hull=d.get("hull") or "Unknown",
                        intensity=d.get("intensity"), finished=bool(d.get("finished")),
                        seconds=d.get("raceSeconds") or 0.0, race=bool(d.get("ringList")),
                        date=d.get("date") or "", result=d.get("result"), text=text))
    return out


LEARNERS = ("ai_learn.py", "learn_drives_from_flights.py", "learn_from_flights.py", "author_race_lines.py")


def learners_fingerprint():
    """The learners' own code: when it changes (a new hull, a better measure), every flight is learned from again."""
    h = hashlib.sha1()
    for name in LEARNERS:
        with open(os.path.join(HERE, name), "rb") as fh:
            h.update(fh.read())
    return h.hexdigest()[:16]


def load_seen():
    """(learned, waiting): the flights already learned from by THESE learners, and the flights no learner could read
    yet with the reason ({path: why}). A learner that changed since makes every flight new again."""
    if not os.path.exists(SEEN):
        return set(), {}
    data = json.load(open(SEEN, encoding="utf-8"))
    if data.get("learners") != learners_fingerprint():
        return set(), {}
    return set(data.get("flights", [])), dict(data.get("waiting", {}))


def save_seen(paths, waiting):
    data = {"note": "Every flight ai_learn.py has learned from (paths on origin/bugledger/*), and the learners it used: "
                    "when a learner changes, every flight is learned from again. Delete a line to relearn it. "
                    "'waiting' holds the flights no learner read yet (mode on hold, hull with no learner) and why: "
                    "each is learned from once its reason goes away. A flight whose learner failed is in neither, so "
                    "the next sweep tries it again.",
            "learners": learners_fingerprint(), "flights": sorted(paths), "waiting": dict(sorted(waiting.items()))}
    with open(SEEN, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(data, fh, indent=1)
        fh.write("\n")


def waiting_reason(f):
    """Why no learner reads this flight now (the mode is on hold, or no learner covers its hull), or None when one does."""
    if HOLD.get(f["mode"]):
        return "hold: " + HOLD[f["mode"]]
    if f["hull"] == "Squirrel" and f["mode"] in SKIM_LINE_MODES:
        return None
    # Regatta's drive learner also learns every hull's race line (author_race_lines.py).
    if f["mode"] == "Regatta":
        return None
    kind, why = ld.coverage(f["hull"], f["mode"])
    return None if kind == "drives" else f"{kind}: {why}"


def plan(flights, seen, waiting=None):
    """What the new flights feed: {mode: {"hulls", "learners", "report" {hull: why}, "unread", "hold", "race"}}. Which
    learner covers a hull comes from the learners themselves (learn_drives_from_flights.coverage reads each vessel's
    prefab), so a new vessel with a boost policy is learned from its first races with no edit here. A flight left
    waiting is new again only once its reason changed (the hold lifted, a learner now covers its hull)."""
    waiting = waiting or {}
    race_modes = {f["mode"] for f in flights if f["race"]}
    new = [f for f in flights if f["path"] not in seen and f["mode"] not in ("Unknown", "Random") and f["hull"] != "Unknown"
           and (f["path"] not in waiting or waiting[f["path"]] != waiting_reason(f))]
    out = {}
    for f in new:
        m = out.setdefault(f["mode"], {"hulls": collections.Counter(), "learners": [], "unread": set(), "report": {},
                                       "hold": HOLD.get(f["mode"]), "race": f["mode"] in race_modes})
        m["hulls"][f["hull"]] += 1
    for mode, m in out.items():
        for hull in sorted(m["hulls"]):
            if hull == "Squirrel" and mode in SKIM_LINE_MODES:
                if "skim line" not in m["learners"]:
                    m["learners"].append("skim line")
                continue
            kind, why = ld.coverage(hull, mode)
            if kind == "drives":
                if "drives" not in m["learners"]:
                    m["learners"].insert(0, "drives")
            elif kind == "report":
                m["report"][hull] = why
            else:
                m["unread"].add(hull)
        # Regatta's drive learner also learns every hull's race line (author_race_lines.py).
        if mode == "Regatta" and set(m["hulls"]) - {"Squirrel"} and "drives" not in m["learners"]:
            m["learners"].insert(0, "drives")
    return new, out


def pace(flights, mode):
    """The person's finished races and the AI rivals' (regatta_rival_report) in this mode, as report lines."""
    mine = [f for f in flights if f["mode"] == mode]
    by = collections.defaultdict(list)
    for f in mine:
        by[(f["hull"], f["intensity"])].append(f)
    lines = []
    for (hull, i), fs in sorted(by.items(), key=lambda kv: (kv[0][0], kv[0][1] or 0)):
        done = sorted(f["seconds"] for f in fs if f["finished"])
        best = f"{done[0]:.0f} s" if done else "-"
        last = max((f["date"] for f in fs), default="")
        results = [f["result"] for f in fs if f.get("result")]
        won = f"{sum(1 for r in results if r.get('youWon'))} of {len(results)}" if results else "-"
        lines.append(f"| {hull} | I{i} | {len(fs)} | {len(done)} | {best} | {won} | {last[:16]} |")
    with tempfile.TemporaryDirectory() as tmp:
        for f in mine:
            open(os.path.join(tmp, os.path.basename(f["path"])), "w", encoding="utf-8").write(f["text"])
        code, text = py(os.path.join(HERE, "regatta_rival_report.py"), "--dir", tmp, "--mode", mode, timeout=300)
    return lines, (text if code == 0 else f"(rival report failed: {text[-400:]})")


def snapshot():
    """{path: blob hash} of every dirty or untracked file, so a file changed again by a learner still counts."""
    paths = [l[3:].strip('"') for l in git("status", "--porcelain", "-uall").splitlines()]
    out = {}
    for p in paths:
        full = os.path.join(REPO, p)
        out[p] = git("hash-object", full).strip() if os.path.isfile(full) else ""
    return out


def run_learners(mode, m):
    results = []
    if m["hold"]:
        return [("hold", 0, m["hold"])]
    if "drives" in m["learners"]:
        code, text = py(os.path.join(HERE, "learn_drives_from_flights.py"), "--mode", mode, "--write")
        written = re.findall(r"^Written: (.*)$", text, re.M)
        results.append(("learn_drives_from_flights --write", code, "; ".join(written) or text[-300:]))
        code, text = py(os.path.join(HERE, "learn_drives_from_flights.py"), "--mode", mode, "--check")
        results.append(("learn_drives_from_flights --check", code, text.splitlines()[-1] if text else ""))
        if mode == "Regatta":
            code, text = py(os.path.join(HERE, "author_race_lines.py"), "--check")
            results.append(("author_race_lines --check", code, text.splitlines()[-1] if text else ""))
    if "skim line" in m["learners"]:
        args = ["--write"] if mode == "Regatta" else ["--mode", mode, "--write"]
        code, text = py(os.path.join(HERE, "learn_from_flights.py"), *args)
        results.append((f"learn_from_flights {' '.join(args)}", code, text.splitlines()[-1] if text else ""))
    return results


def write_report(now, new, modes, outcomes, paces, changed):
    L = [f"# AI learning: what the last races taught", "",
         f"Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) "
         f"on {now}. Nobody runs it by hand: every race flown in Unity reaches it on its own.", "",
         f"**{len(new)} new flight(s)** since the last learn.", ""]
    for mode in sorted(modes):
        m = modes[mode]
        L += [f"## {mode}", "",
              "New flights: " + ", ".join(f"{h} {n}" for h, n in sorted(m['hulls'].items())) + ".", ""]
        if m["hold"]:
            L += [f"**On hold, not written:** {m['hold']}.", ""]
        for name, code, text in outcomes.get(mode, []):
            if name != "hold":
                L.append(f"- `{name}`: {'OK' if code == 0 else 'FAILED'}. {text}")
        if outcomes.get(mode):
            L.append("")
        for hull, why in sorted(m["report"].items()):
            L.append(f"- **{hull}: measured, not written** - {why}.")
        if m["report"]:
            L.append("")
        if m["unread"]:
            L += ["**No learner reads these yet** (the sweep's to-do): " + ", ".join(sorted(m["unread"])) + ".", ""]
        if mode in paces:
            rows, rivals = paces[mode]
            L += ["The person, every recorded race in this mode:", "",
                  "| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |", "|---|---|---:|---:|---:|---:|---|", *rows, "",
                  "The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last "
                  "changed: `git log -1 --format=%cI -- <the AI's files>`):", "", "```", rivals, "```", ""]
    L += ["## Files the learners changed", ""]
    L += [f"- `{p}`" for p in sorted(changed)] or ["- none (no hull had three finished races yet, or nothing moved)"]
    L.append("")
    with open(REPORT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(L))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()

    flights = load_flights()
    seen, waiting = load_seen()
    new, modes = plan(flights, seen, waiting)
    print(f"ai_learn: {len(flights)} flight(s) on the branches, {len(new)} new.")
    for mode in sorted(modes):
        m = modes[mode]
        print(f"  {mode}: " + ", ".join(f"{h} {n}" for h, n in sorted(m["hulls"].items()))
              + (f"  learners: {', '.join(m['learners'])}" if m["learners"] else "")
              + (f"  HOLD: {m['hold']}" if m["hold"] else "")
              + (f"  reported only: {', '.join(sorted(m['report']))}" if m["report"] else "")
              + (f"  no learner: {', '.join(sorted(m['unread']))}" if m["unread"] else ""))
    if not a.run:
        return 0
    if not new:
        print("ai_learn: nothing new - nothing written.")
        return 0

    before = snapshot()
    outcomes, paces = {}, {}
    for mode, m in sorted(modes.items()):
        outcomes[mode] = run_learners(mode, m)
        # Every mode: the person's table (with who won, since flights record their result); the AI rivals' rings for a race.
        paces[mode] = pace(flights, mode)
    after = snapshot()
    changed = {p for p, h in after.items() if before.get(p) != h}
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    write_report(now, new, modes, outcomes, paces, changed)
    failed = [(mode, n) for mode, rs in outcomes.items() for n, code, _ in rs if code != 0]
    learned, now_waiting = settle(new, {mode for mode, _ in failed})
    waiting = {p: w for p, w in waiting.items() if p not in learned}
    waiting.update(now_waiting)
    save_seen(seen | learned, waiting)
    print(f"ai_learn: report {os.path.relpath(REPORT, REPO)}; {len(changed)} learned file(s) changed"
          + (f"; FAILED: {failed}" if failed else "") + ".")
    return 1 if failed else 0


def settle(new, failed_modes):
    """(learned paths, {waiting path: why}) for this run's new flights. A flight counts as learned from only when a
    learner read it and every learner of its mode succeeded; one in a failed mode is neither, so the next sweep retries it."""
    learned, waiting = set(), {}
    for f in new:
        why = waiting_reason(f)
        if why:
            waiting[f["path"]] = why
        elif f["mode"] not in failed_modes:
            learned.add(f["path"])
    return learned, waiting


def self_test():
    fl = [dict(path="a/flight_Switchback_Dolphin_1.json", mode="Switchback", hull="Dolphin", race=True),
          dict(path="a/flight_Switchback_Dolphin_2.json", mode="Switchback", hull="Dolphin", race=True),
          dict(path="a/flight_Breakwater_Sparrow_1.json", mode="Breakwater", hull="Sparrow", race=True),
          dict(path="a/flight_Regatta_Squirrel_1.json", mode="Regatta", hull="Squirrel", race=True),
          dict(path="a/flight_Regatta_Sparrow_1.json", mode="Regatta", hull="Sparrow", race=True),
          dict(path="a/flight_SkimRace_Squirrel_1.json", mode="SkimRace", hull="Squirrel", race=False),
          dict(path="a/flight_Warpline_Stoat_1.json", mode="Warpline", hull="Stoat", race=True),
          dict(path="a/flight_Bloomrush_Manta_1.json", mode="Bloomrush", hull="Manta", race=False),
          dict(path="a/flight_GrizzlyCharge_Grizzly_1.json", mode="GrizzlyCharge", hull="Grizzly", race=False),
          dict(path="a/flight_WrasseRun_Wrasse_1.json", mode="WrasseRun", hull="Wrasse", race=True),
          dict(path="a/flight_Tapestry_Ghost_1.json", mode="Tapestry", hull="Ghost", race=False),
          dict(path="a/flight_Random_Unknown_1.json", mode="Random", hull="Unknown", race=False)]
    bad = []
    HOLD["Bloomrush"] = "held for the self-test"   # the mechanism, on a mode the case below also reads unheld
    try:
        held_new, held = plan(fl, set())
        held_learned, held_waiting = settle(held_new, set())
    finally:
        del HOLD["Bloomrush"]
    if not held["Bloomrush"]["hold"]:
        bad.append("a mode on HOLD must be reported as held")
    if any("Bloomrush" in p for p in held_learned) or not any("Bloomrush" in p for p in held_waiting):
        bad.append("a flight in a mode on hold waits, it is not learned")
    new, modes = plan(fl, {"a/flight_Switchback_Dolphin_1.json"})
    if len(new) != 10: bad.append(f"new {len(new)} (the seen flight and the Unknown hull are not new)")
    if modes["Switchback"]["hulls"]["Dolphin"] != 1 or modes["Switchback"]["learners"] != ["drives"]:
        bad.append(f"Switchback {modes['Switchback']}")
    if modes["Breakwater"]["learners"] != ["drives"]:
        bad.append(f"the Sparrow's boost policy is learned in Breakwater {modes['Breakwater']}")
    if modes["Regatta"]["learners"] != ["drives", "skim line"] or modes["Regatta"]["unread"]:
        bad.append(f"Regatta {modes['Regatta']}")
    if modes["SkimRace"]["learners"] != ["skim line"]:
        bad.append(f"Skim Race's Squirrel is measured by the skim learner {modes['SkimRace']}")
    if modes["Warpline"]["hold"] or modes["Warpline"]["learners"] != ["drives"]:
        bad.append(f"Warpline's Stoat is learned since 2026-10-26 {modes['Warpline']}")
    if modes["Bloomrush"]["learners"] != ["drives"]:
        bad.append("the Manta's Soar is learned in any mode")
    if "Grizzly" not in modes["GrizzlyCharge"]["report"] or "Wrasse" not in modes["WrasseRun"]["report"]:
        bad.append("a hull that cannot be written is reported with its reason")
    if modes["Tapestry"]["unread"] != {"Ghost"}:
        bad.append("a hull nothing covers is listed as unread")
    if plan(fl, {f["path"] for f in fl})[0]:
        bad.append("everything seen must leave nothing new")
    # Only a flight a learner read, in a mode whose learners all succeeded, is marked learned (Codex, PR 1558).
    learned, waiting = settle(new, {"Breakwater"})
    if not any("Warpline" in p for p in learned):
        bad.append("Warpline's Stoat flights are learned now that its hold is lifted")
    if any("Ghost" in p for p in learned) or not any("Ghost" in p for p in waiting):
        bad.append("a flight no learner covers waits, it is not learned")
    if any("Breakwater" in p for p in learned | set(waiting)):
        bad.append("a flight whose learner failed is retried next sweep (neither learned nor waiting)")
    if not any("Switchback_Dolphin_2" in p for p in learned):
        bad.append("a flight a learner read is learned")
    if any(f["path"] in waiting for f in plan(fl, learned, waiting)[0]):
        bad.append("a waiting flight whose reason has not changed is not new again")
    stale = {p: "hold: an old reason" for p in waiting}
    if not any(f["path"] in waiting for f in plan(fl, learned, stale)[0]):
        bad.append("a waiting flight whose reason changed (hold lifted, learner added) is new again")
    print("ai_learn self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main())

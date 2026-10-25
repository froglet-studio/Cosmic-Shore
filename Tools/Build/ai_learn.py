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
HOLD = {
    "Warpline": "the Stoat's AI waits for Garrett's configuration (issue 1198)",
}


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
                        date=d.get("date") or "", text=text))
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
    """The flights already learned from, by THESE learners; a learner that changed since makes every flight new again."""
    if not os.path.exists(SEEN):
        return set()
    data = json.load(open(SEEN, encoding="utf-8"))
    if data.get("learners") != learners_fingerprint():
        return set()
    return set(data.get("flights", []))


def save_seen(paths):
    data = {"note": "Every flight ai_learn.py has learned from (paths on origin/bugledger/*), and the learners it used: "
                    "when a learner changes, every flight is learned from again. Delete a line to relearn it.",
            "learners": learners_fingerprint(), "flights": sorted(paths)}
    with open(SEEN, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(data, fh, indent=1)
        fh.write("\n")


def plan(flights, seen):
    """What the new flights feed: {mode: {"hulls", "learners", "report" {hull: why}, "unread", "hold", "race"}}. Which
    learner covers a hull comes from the learners themselves (learn_drives_from_flights.coverage reads each vessel's
    prefab), so a new vessel with a boost policy is learned from its first races with no edit here."""
    race_modes = {f["mode"] for f in flights if f["race"]}
    new = [f for f in flights if f["path"] not in seen and f["mode"] not in ("Unknown", "Random") and f["hull"] != "Unknown"]
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


def seen_after(flights, new, modes, outcomes, seen):
    """The memory after a run: everything already seen, every flight the plan never treats as new (an Unknown hull or
    mode), and each NEW flight only once it was really learned from - its mode not on hold, every learner of its mode
    exited 0, and its hull covered by a learner that writes (not listed as unread or report-only). The rest stays new,
    so the next sweep retries a failed learner, and a mode or hull gains its races back when its learner arrives
    (Codex on #1558: marking every flight seen dropped them for good)."""
    new_paths = {f["path"] for f in new}
    keep = set(seen) | {f["path"] for f in flights if f["path"] not in new_paths}
    for f in new:
        m = modes.get(f["mode"])
        if not m or m["hold"] or f["hull"] in m["unread"] or f["hull"] in m["report"]:
            continue
        if any(code != 0 for _, code, _ in outcomes.get(f["mode"], [])):
            continue
        keep.add(f["path"])
    return keep


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
        lines.append(f"| {hull} | I{i} | {len(fs)} | {len(done)} | {best} | {last[:16]} |")
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
                  "| Hull | Intensity | Races | Finished | Fastest | Last raced |", "|---|---|---:|---:|---:|---|", *rows, "",
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
    seen = load_seen()
    new, modes = plan(flights, seen)
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
        if m["race"]:
            paces[mode] = pace(flights, mode)
    after = snapshot()
    changed = {p for p, h in after.items() if before.get(p) != h}
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    write_report(now, new, modes, outcomes, paces, changed)
    save_seen(seen_after(flights, new, modes, outcomes, seen))
    failed = [(mode, n) for mode, rs in outcomes.items() for n, code, _ in rs if code != 0]
    print(f"ai_learn: report {os.path.relpath(REPORT, REPO)}; {len(changed)} learned file(s) changed"
          + (f"; FAILED: {failed}" if failed else "") + ".")
    return 1 if failed else 0


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
    if not modes["Warpline"]["hold"]:
        bad.append("Warpline must be on hold")
    if modes["Bloomrush"]["learners"] != ["drives"]:
        bad.append("the Manta's Soar is learned in any mode")
    if "Grizzly" not in modes["GrizzlyCharge"]["report"] or "Wrasse" not in modes["WrasseRun"]["report"]:
        bad.append("a hull that cannot be written is reported with its reason")
    if modes["Tapestry"]["unread"] != {"Ghost"}:
        bad.append("a hull nothing covers is listed as unread")
    if plan(fl, {f["path"] for f in fl})[0]:
        bad.append("everything seen must leave nothing new")
    # seen_after: only what was really learned from is remembered
    outcomes = {mode: [("learner", 0, "")] for mode in modes}
    outcomes["Breakwater"] = [("learn_drives_from_flights --write", 1, "boom")]
    kept = seen_after(fl, new, modes, outcomes, {"a/flight_Switchback_Dolphin_1.json"})
    if "a/flight_Switchback_Dolphin_2.json" not in kept: bad.append("a learned flight must be remembered")
    if "a/flight_Breakwater_Sparrow_1.json" in kept: bad.append("a failed learner's flights must stay new")
    if "a/flight_Warpline_Stoat_1.json" in kept: bad.append("a held mode's flights must stay new")
    if "a/flight_Tapestry_Ghost_1.json" in kept: bad.append("an unread hull's flights must stay new")
    if "a/flight_GrizzlyCharge_Grizzly_1.json" in kept: bad.append("a report-only hull's flights must stay new")
    if "a/flight_Random_Unknown_1.json" not in kept or "a/flight_Switchback_Dolphin_1.json" not in kept:
        bad.append("what was seen, or is never new, stays remembered")
    print("ai_learn self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""vessel_mode_chart.py - which vessels fly in which game modes: the card that offers them, their AI, their abilities and
stats, and every flight recorded so far (Yash, 2026-10-17: "doc the chart of vessels, their ability and stats, the
record they flew in game modes... so we can track which vessels flew in which game modes, their tracks and all").

A READER: it writes nothing but the chart. Everything comes from the repo and the stored flights, never from memory:
- the cards (Assets/_SO_Assets/Games/ArcadeGame*.asset): mode, display name, the hulls each offers, Arcade / Arena list;
- each hull's abilities (element_ability_table.py --json) and flight numbers off its prefab (regatta_balance.read_vessel);
- the AI wiring found in the tree: boost policies (Assets/_SO_Assets/AI Boost Policies), per-mode variants
  (Resources/AIBoostPolicies/*_<Mode>, Resources/SkimRaceAIConfig_<Mode>), the gate-race steering every ring race shares;
- the flights: every flight_*.json on origin/bugledger/* (Store AI record), as the pilot (hand-flown) and as the AI
  rivals in it (their hull and the rings they had when the race ended).

    python3 Tools/Build/vessel_mode_chart.py              # print the chart
    python3 Tools/Build/vessel_mode_chart.py --write      # also write Docs/AI_SYSTEM/VESSEL_MODE_CHART.md + .json
    python3 Tools/Build/vessel_mode_chart.py --no-fetch   # use the bugledger branches as last fetched
"""
import argparse
import glob
import json
import os
import re
import statistics
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
OUT_MD = os.path.join(REPO, "Docs", "AI_SYSTEM", "VESSEL_MODE_CHART.md")
OUT_JSON = os.path.join(REPO, "Docs", "AI_SYSTEM", "vessel_mode_chart.json")

# What each hull's AI is, in words. The tree says THAT a drive exists (checked below); this says what it does.
# Source: Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md §1 and ARCHITECTURE.md §2.
AI_DRIVE = {
    "Squirrel": ("skims a rail for boost", "SkimRacePilot (Skim Race crystals; Regatta: RegattaRingObjective)"),
    "Stoat": ("black-hole / white-hole pair: warp the path and fly it", "dipole autopilot in StoatDipole/Sling executors"),
    "Manta": ("Soar: boost costs yaw", "Soar drive in MantaAnalogTurnBoostExecutor + MantaSoarAITuningSO"),
    "Rhino": ("ramp: hold straight to wind up speed", "RampBoostAIPolicySO (ramp / brake / off)"),
    "Urchin": ("rides its own rail", "UrchinAutopilotDriver (rail modes: Regatta, Skein)"),
    "Dolphin": ("drift charge, then discharge", "ChargeBoostAIPolicySO"),
    "Scarab": ("throttle ceiling + Snap Dash", "SnapDashAIPolicySO"),
    "Serpent": ("4 boost pellets", "PelletBoostAIPolicySO"),
    "Sparrow": ("hold-to-boost", "HoldBoostAIPolicySO"),
    "BrittleStar": ("speed mode", "BrittleStarSpeedModeAIPolicySO"),
}
POLICY_ASSET = {  # hull -> its drive's asset/code file, checked to exist
    "Squirrel": "Assets/_Scripts/Controller/AI/SkimRace/SkimRacePilot.cs",
    "Stoat": "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/StoatDipoleExecutor.cs",
    "Manta": "Assets/_Scripts/Controller/AI/Boost/MantaSoarAITuningSO.cs",
    "Rhino": "Assets/_SO_Assets/AI Boost Policies/RhinoAIBoostPolicy.asset",
    "Urchin": "Assets/_Scripts/Controller/AI/Urchin",
    "Dolphin": "Assets/_SO_Assets/AI Boost Policies/DolphinAIBoostPolicy.asset",
    "Scarab": "Assets/_SO_Assets/AI Boost Policies/ScarabAIBoostPolicy.asset",
    "Serpent": "Assets/_SO_Assets/AI Boost Policies/SerpentAIBoostPolicy.asset",
    "Sparrow": "Assets/_SO_Assets/AI Boost Policies/SparrowAIBoostPolicy.asset",
    "BrittleStar": "Assets/_SO_Assets/AI Boost Policies/BrittleStarAIBoostPolicy.asset",
}
REGATTA_AI = ["Squirrel", "Stoat", "Manta", "Rhino", "Urchin", "Dolphin"]   # the six (REGATTA_AI_ROADMAP.md §1)


def read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8", errors="replace") as fh:
        return fh.read()


def guid_index():
    """guid -> asset path, for the class SOs."""
    out = {}
    for meta in glob.glob(os.path.join(REPO, "Assets", "_SO_Assets", "Classes", "*.asset.meta")):
        m = re.search(r"^guid: (\w+)", open(meta).read(), re.M)
        if m:
            out[m.group(1)] = meta[:-5]
    return out


def class_name(path):
    m = re.search(r"^\s+Name: (.+)$", open(path, encoding="utf-8").read(), re.M)
    return m.group(1).strip() if m else os.path.basename(path)


def game_modes():
    text = read("Assets/_Scripts/Data/Enums/GameModes.cs")
    return {int(v): k for k, v in re.findall(r"^\s+(\w+)\s*=\s*(\d+)", text, re.M)}


def list_guids(rel):
    return set(re.findall(r"guid: (\w+)", read(rel)))


def cards():
    modes, classes = game_modes(), guid_index()
    lists = {name: list_guids(f"Assets/_SO_Assets/Games/GameLists/{name}.asset") for name in ("ArcadeGames", "ArenaGames")}
    out = []
    for path in sorted(glob.glob(os.path.join(REPO, "Assets", "_SO_Assets", "Games", "ArcadeGame*.asset"))):
        text = open(path, encoding="utf-8").read()
        guid = re.search(r"^guid: (\w+)", open(path + ".meta").read(), re.M).group(1)
        mode = int(re.search(r"^\s+Mode: (\d+)", text, re.M).group(1))
        block = re.search(r"^\s+Vessels:\n((?:\s+- \{.*\n)*)", text, re.M)
        hulls = [class_name(classes[g]) for g in re.findall(r"guid: (\w+)", block.group(1) if block else "") if g in classes]
        name = re.search(r"^\s+DisplayName: (.+)$", text, re.M)
        out.append({"mode": modes.get(mode, str(mode)), "name": name.group(1).strip() if name else modes.get(mode),
                    "hulls": hulls, "lists": [k for k, v in lists.items() if guid in v]})
    return out


def gate_race_modes():
    m = re.search(r"GateRaceModes\s*=\s*\{(.*?)\}", read("Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs"), re.S)
    return re.findall(r"GameModes\.(\w+)", m.group(1)) if m else []


def tuned_modes():
    """hull-or-asset -> modes with a per-mode AI variant in Resources."""
    out = {}
    for p in glob.glob(os.path.join(REPO, "Assets", "Resources", "AIBoostPolicies", "*_*.asset")):
        base, mode = os.path.basename(p)[:-6].rsplit("_", 1)
        out.setdefault(base, []).append(mode)
    for p in glob.glob(os.path.join(REPO, "Assets", "Resources", "SkimRaceAIConfig_*.asset")):
        suffix = os.path.basename(p)[len("SkimRaceAIConfig_"):-6]
        # SkimRaceAIConfig_I<n> is Skim Race's own per-intensity config; SkimRaceAIConfig_<Mode> is another mode's.
        out.setdefault("SkimRaceAIConfig", []).append(f"SkimRace {suffix}" if re.fullmatch(r"I\d", suffix) else suffix)
    return out


def abilities():
    r = subprocess.run([sys.executable, os.path.join(HERE, "element_ability_table.py"), "--json"], capture_output=True, text=True)
    out = {}
    try:
        for row in json.loads(r.stdout):
            out.setdefault(row["vessel"], {})[row["element"]] = row.get("ability") or ""
    except (ValueError, KeyError):
        pass
    return out


def stats(hull):
    try:
        import regatta_balance as rb
        return rb.read_vessel(hull)
    except Exception:   # a hull without a prefab or a key: say so in the chart, never guess
        return None


def flights(fetch):
    if fetch:
        subprocess.run(["git", "-C", REPO, "fetch", "-q", "origin", "refs/heads/bugledger/*:refs/remotes/origin/bugledger/*"],
                       capture_output=True)
    branches = subprocess.run(["git", "-C", REPO, "branch", "-r", "--list", "origin/bugledger/*"], capture_output=True,
                              text=True).stdout.split()
    out, seen = [], set()
    for b in branches:
        files = subprocess.run(["git", "-C", REPO, "ls-tree", "-r", "--name-only", b], capture_output=True, text=True).stdout.split("\n")
        for path in files:
            name = os.path.basename(path)
            if not re.match(r"flight_.*\.json$", name) or name in seen:
                continue
            seen.add(name)
            raw = subprocess.run(["git", "-C", REPO, "show", f"{b}:{path}"], capture_output=True, text=True).stdout
            try:
                f = json.loads(raw)
            except ValueError:
                continue
            rv, n = f.get("rivals") or [], len(f.get("rivalHulls") or [])
            last = rv[-1] if rv else []
            out.append({"file": name, "pilot": b.split("/")[-1], "mode": f.get("mode"), "hull": f.get("hull"),
                        "intensity": f.get("intensity"), "finished": bool(f.get("finished")) or finished_old(f),
                        "seconds": f.get("raceSeconds"), "rings": f.get("rings"), "target": f.get("raceTarget"),
                        "rivalDifficulty": f.get("rivalDifficulty") or "", "date": f.get("date") or "",
                        "rivals": [{"hull": h, "rings": int(last[4 * j + 3]) if len(last) > 4 * j + 3 else None}
                                   for j, h in enumerate(f.get("rivalHulls") or [])]})
    return out


def finished_old(f):
    """Flights before 2026-10-16's recorder fix stopped one sample before the last ring (learn_from_flights.finished_of)."""
    try:
        import learn_from_flights as lff
        return lff.finished_of(f)
    except Exception:
        return False


def build(fetch):
    cs, fl, ab, tuned, gates = cards(), flights(fetch), abilities(), tuned_modes(), gate_race_modes()
    hulls = sorted({h for c in cs for h in c["hulls"]} | set(AI_DRIVE))
    live = [c for c in cs if c["lists"]]
    rows = []
    for h in hulls:
        offered = [c["mode"] for c in live if h in c["hulls"]]
        mine = [x for x in fl if (x["hull"] or "").lower() == h.lower()]
        as_rival = [(x, r) for x in fl for r in x["rivals"] if r["hull"] == h.lower()]
        modes_flown = sorted({x["mode"] for x in mine})
        drive = AI_DRIVE.get(h)
        has_drive = bool(drive) and os.path.exists(os.path.join(REPO, POLICY_ASSET.get(h, "")))
        # Every one of the six was built and tuned in Regatta (REGATTA_AI_ROADMAP.md §1); per-mode assets add the rest.
        tuned_for = sorted(set(tuned.get(f"{h}SoarAITuning", []) + tuned.get(f"{h}AIBoostPolicy", [])
                               + (tuned.get("SkimRaceAIConfig", []) if h == "Squirrel" else [])
                               + (["Regatta"] if h in REGATTA_AI else [])))
        rows.append({
            "hull": h, "abilities": ab.get(h, {}), "stats": stats(h),
            "verb": drive[0] if drive else "", "aiDrive": drive[1] if (drive and has_drive) else "",
            "regattaAI": h in REGATTA_AI, "tunedFor": tuned_for,
            "offeredIn": offered, "gateRacesOffered": [m for m in offered if m in gates],
            "flown": {m: summarize([x for x in mine if x["mode"] == m]) for m in modes_flown},
            "asRival": summarize_rival(as_rival),
        })
    return {"schema": "cosmic-shore.vessel-mode-chart/1", "cards": cs, "gateRaceModes": gates, "hulls": rows,
            "flights": len(fl), "pilots": sorted({x["pilot"] for x in fl})}


def summarize(xs):
    fin = [x for x in xs if x["finished"] and x["seconds"]]
    return {"races": len(xs), "finished": len(fin), "best": round(min(x["seconds"] for x in fin), 1) if fin else None,
            "median": round(statistics.median(x["seconds"] for x in fin), 1) if fin else None,
            "intensities": sorted({x["intensity"] for x in xs if x["intensity"]}), "pilots": sorted({x["pilot"] for x in xs}),
            "rivalDifficulty": sorted({x["rivalDifficulty"] for x in xs if x["rivalDifficulty"]}),
            "last": max((x["date"] for x in xs), default="")}


def summarize_rival(pairs):
    by = {}
    for x, r in pairs:
        d = by.setdefault(x["mode"], {"races": 0, "ringsAtEnd": [], "target": x["target"], "difficulty": set(), "intensities": set()})
        d["races"] += 1
        if r["rings"] is not None:
            d["ringsAtEnd"].append(r["rings"])
        d["difficulty"].add(x["rivalDifficulty"])
        d["intensities"].add(x["intensity"])
    return {m: {"races": d["races"], "medianRings": round(statistics.median(d["ringsAtEnd"])) if d["ringsAtEnd"] else None,
                "target": d["target"], "difficulty": sorted(x for x in d["difficulty"] if x),
                "intensities": sorted(i for i in d["intensities"] if i)} for m, d in by.items()}


def markdown(C):
    L = ["# Vessels and game modes: who flies where", "",
         "Generated by `python3 Tools/Build/vessel_mode_chart.py --write`. Do not edit by hand; re-run it. It reads the",
         "cards, the prefabs, the ability maps, the AI wiring in the tree and every flight stored with Store AI record",
         f"(`origin/bugledger/*`: {C['flights']} flights from {', '.join(C['pilots']) or 'nobody'}).", "",
         "## 1. The AIs built for Regatta, and whether they are ready elsewhere", "",
         "Every hull's AI has two halves:",
         "- **Steering** comes from the MODE. Every ring race (" + ", ".join(C["gateRaceModes"]) + ") shares",
         "  `GateRaceController`'s steering. Other modes give the platform autopilot their own objectives.",
         "- **The drive** (when to spend the hull's speed verb) belongs to the HULL and works in any mode.",
         "",
         "Regatta-only extras: the commit-from-approach-point fix, no crystal detour, the Urchin's rail aim, and the",
         "`_Regatta` tuning variants.", "",
         "So a Regatta AI hull is **ready to fly** any mode whose card offers it. It is **only tuned and tested** in the",
         "modes listed under *Tuned and tested in* (Regatta, plus Skim Race for the Squirrel).", "",
         "| Hull | Speed verb | AI drive | Tuned and tested in | Other live cards that offer it | Of those, ring races |",
         "|---|---|---|---|---|---|"]
    for h in C["hulls"]:
        if not h["regattaAI"]:
            continue
        other = [m for m in h["offeredIn"] if m != "Regatta"]
        L.append(f"| **{h['hull']}** | {h['verb']} | {h['aiDrive'] or '-'} | {', '.join(h['tunedFor']) or '-'} | "
                 f"{', '.join(other) or 'none'} | {', '.join(m for m in h['gateRacesOffered'] if m != 'Regatta') or 'none'} |")
    L += ["", "## 2. Every hull: abilities, flight numbers, cards", "",
          "Stats are read off the hull's prefab:",
          "- **Throttle**: `DefaultThrottleScaler`, roughly the top cruise speed.",
          "- **Pitch / Yaw**: the turn scalers.",
          "- **Boost**: `boostMultiplier`.", "",
          "| Hull | Charge | Mass | Space | Time | Throttle | Pitch / Yaw | Boost | Live cards that offer it |",
          "|---|---|---|---|---|---:|---|---:|---|"]
    for h in C["hulls"]:
        a, s = h["abilities"], h["stats"]
        st = (f"{s['ts']:g} | {s['pitch']:g} / {s['yaw']:g} | {s['boost']:g}" if s else "- | - | -")
        L.append(f"| {h['hull']} | {a.get('Charge', '-')} | {a.get('Mass', '-')} | {a.get('Space', '-')} | {a.get('Time', '-')} | "
                 f"{st} | {', '.join(h['offeredIn']) or '-'} |")
    L += ["", "## 3. What has flown where (the record)", "",
          "**Hand-flown**: one row per hull and mode a person flew with the recorder on. Best and median count finished",
          "races only.", "",
          "| Hull | Mode | Races | Finished | Best s | Median s | Intensities | Rival AI level | Pilots | Last |",
          "|---|---|---:|---:|---:|---:|---|---|---|---|"]
    any_flown = False
    for h in C["hulls"]:
        for m, d in h["flown"].items():
            any_flown = True
            L.append(f"| {h['hull']} | {m} | {d['races']} | {d['finished']} | {d['best'] or '-'} | {d['median'] or '-'} | "
                     f"{', '.join('I' + str(i) for i in d['intensities'])} | {', '.join(d['rivalDifficulty']) or '-'} | "
                     f"{', '.join(d['pilots'])} | {d['last'][:10]} |")
    if not any_flown:
        L.append("| - | - | 0 | | | | | | | |")
    L += ["", "**As an AI rival**: the AI seats in those same races, with the rings each had when the race ended (the person",
          "finishing ends it). This is the only AI record so far; no AI-only race is recorded.", "",
          "| Hull | Mode | Races | Median rings at the end | Of | Intensities | AI level |", "|---|---|---:|---:|---:|---|---|"]
    for h in C["hulls"]:
        for m, d in h["asRival"].items():
            L.append(f"| {h['hull']} | {m} | {d['races']} | {d['medianRings'] if d['medianRings'] is not None else '-'} | "
                     f"{d['target'] or '-'} | {', '.join('I' + str(i) for i in d['intensities'])} | {', '.join(d['difficulty']) or '-'} |")
    L += ["", "**Tracks:** each stored flight carries its own course: every ring, every rail, the course seed and intensity.",
          "`racing_line.py` turns a hull × mode × intensity's flights into a line per leg (`Docs/AI_SYSTEM/racing_lines/`).",
          "", "## 4. Live cards and the hulls they offer", "", "| Card | Mode | List | Hulls |", "|---|---|---|---|"]
    for c in C["cards"]:
        if c["lists"]:
            L.append(f"| {c['name']} | {c['mode']} | {', '.join(x.replace('Games', '') for x in c['lists'])} | {', '.join(c['hulls']) or '-'} |")
    return "\n".join(L) + "\n"


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--no-fetch", action="store_true")
    a = ap.parse_args()
    C = build(not a.no_fetch)
    md = markdown(C)
    print(md)
    if a.write:
        open(OUT_MD, "w").write(md)
        json.dump(C, open(OUT_JSON, "w"), indent=1, default=list)
        print(f"wrote {os.path.relpath(OUT_MD, REPO)} and {os.path.relpath(OUT_JSON, REPO)}")


if __name__ == "__main__":
    main()

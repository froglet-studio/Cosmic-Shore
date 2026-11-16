#!/usr/bin/env python3
"""maelstrom_tiers.py - which arcade games each Maelstrom intensity deals, curated from measured evidence.

Garrett, 2026-11-16: "lets curate and organize the maelstrom intensity levels ... Find a way to discover the beginner
games, the great games, the good games. you can use the data on those four games and see how they fit a model. then
try to organize the other games accordingly." And: "i should be able to play a maelstrom with all our arcade games
playable in 1 of the four maelstrom intensities".

The four intensities are four BUCKETS of games for now (a development mindset), not a difficulty ladder:

    1  Beginner  only games a new player can pick up and play        (Garrett's example: Scarab Rally, the Scarab Time
                                                                      game - just a throttle)
    2  Great     games we are afraid to change, they are so polished (example: Skim Race)
    3  Good      already very fun, room to improve: we want feedback (example: Joust)
    4  Floor     everything else - played to find the games we can raise into 1-3

A Maelstrom run at intensity N deals ONLY tier N's games (MaelstromDataSO.GamesForIntensity), and every arcade card
is in exactly one tier. "Once we empty 4 we can refill it with everything" - a card may then be listed in several
tiers; this script writes one tier per card until a person changes the rule.

How a game is placed (the model; docs: Docs/MaelstromSystem/INTENSITY_TIERS.md). Every number is MEASURED - from the
cards, every human flight record and launch sent to the origin/bugledger/* branches, the full history of
origin/bleeding-edge (matched to the game the same way /swimlanes matches its game lanes), the elemental loop's
sign-offs and the fun cards. The rules, first match wins:

    a person's verdict           `set` records it; the three examples are Garrett's verdicts
    a known blocker -> 4         a defect that stops a round in a Maelstrom (blockers in the json, with evidence)
    great (2)                    mature AND played by 3+ people over 20+ flights AND settled (<= 10% of its commits
                                 in the last 14 days)
    good (3)                     a person signed it off as fun (loop step 1 done naming it, or a fun card "yes")
    beginner (1)                 human flights prove it needs no ability button (<= 0.5 ability presses a minute over
                                 at least a minute of recorded input) AND people have finished or replayed it
    good (3)                     mature (the weakest evidence: the team kept investing in it and people played it)
    floor (4)                    everything else, including every game nobody has flown yet

    maturity = 0.25 commits (log, 200 = 1) + 0.15 authors (8 = 1) + 0.20 card age (180 days = 1)
             + 0.25 people who played it (4 = 1) + 0.15 human flights (log, 25 = 1)
    mature   = maturity >= 0.60

The order is the strength of the evidence. A person's word beats everything; "great" and a fun sign-off are
measured or human evidence of polish and fun; "pick up and play" is measured straight from how people fly; maturity is
only a proxy for "already very fun", so it places a game last. Garrett's examples fit: Skim Race needs no ability
button either but is measured great first, and Joust (also button-free) has one person's short flight, so it is not
proven pick-up and its maturity makes it good. Run with the verdicts set aside, the model puts all three examples
where Garrett did (INTENSITY_TIERS.md, "How the examples fit the model").

    python3 Tools/Build/maelstrom_tiers.py                    the board (from the json)
    python3 Tools/Build/maelstrom_tiers.py measure [--no-fetch]
                                                              re-measure every card, re-place it, write the json + doc
    python3 Tools/Build/maelstrom_tiers.py set <game> <1|2|3|4|clear> --by <who> [--note "..."]
                                                              a person's placement; beats the model
    python3 Tools/Build/maelstrom_tiers.py --write            write MaelstromData.asset (GameQueue + IntensityTiers);
                                                              a card new to the roster joins the floor, offline
    python3 Tools/Build/maelstrom_tiers.py --check            exit 1 when the asset or doc disagrees with the json, a
                                                              card is in no tier or two, or a verdict is not honoured
    python3 Tools/Build/maelstrom_tiers.py --self-test        known answers and negative controls

Standard library only.
"""
import argparse
import collections
import datetime
import glob
import json
import math
import os
import re
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
DATA = os.path.join(REPO, "Docs", "MaelstromSystem", "maelstrom_tiers.json")
DOC = os.path.join(REPO, "Docs", "MaelstromSystem", "INTENSITY_TIERS.md")
ASSET = os.path.join(REPO, "Assets", "_SO_Assets", "Maelstrom", "MaelstromData.asset")
ROSTER = os.path.join(REPO, "Assets", "_SO_Assets", "Games", "GameLists", "ArcadeGames.asset")
GAMES_DIR = os.path.join(REPO, "Assets", "_SO_Assets", "Games")
GAME_MODES = os.path.join(REPO, "Assets", "_Scripts", "Data", "Enums", "GameModes.cs")
LOOP_JSON = os.path.join(REPO, "Docs", "ElementalLoop", "loop.json")
LANES_DIR = os.path.join(REPO, ".claude", "skills", "swimlanes")
MAELSTROM_MODE = 36

TIERS = {1: "Beginner", 2: "Great", 3: "Good", 4: "Floor"}
TIER_MEANING = {
    1: "only games a new player can pick up and play",
    2: "games we are afraid to change because they are so polished",
    3: "already very fun, room to improve: we want feedback",
    4: "everything else: played to find games we can raise into 1-3",
}
ABILITY_BUTTONS = ("Button1Action", "Button2Action", "Button3Action", "Button4Action", "PayloadAction", "FlipAction")

# The model's thresholds. Changing one is a model change: say so in the doc's log.
MATURE = 0.60
GREAT_PEOPLE = 3
GREAT_FLIGHTS = 20
SETTLED_SHARE = 0.10
SIMPLE_RATE = 0.5
SIMPLE_MINUTES = 1.0


# --- the model --------------------------------------------------------------------------------------------------------

def maturity(f):
    """0..1 - how much the team has put into a game and how much people have played it."""
    commits = min(math.log1p(f.get("commits", 0)) / math.log1p(200), 1.0)
    authors = min(f.get("authors", 0) / 8.0, 1.0)
    age = min(f.get("cardAgeDays", 0) / 180.0, 1.0)
    people = min(f.get("people", 0) / 4.0, 1.0)
    flights = min(math.log1p(f.get("flights", 0)) / math.log1p(25), 1.0)
    return round(0.25 * commits + 0.15 * authors + 0.20 * age + 0.25 * people + 0.15 * flights, 3)


def model_tier(f):
    """(tier, why) from the measurements alone - no verdict, no blocker."""
    m = maturity(f)
    commits = f.get("commits", 0)
    share = f.get("commits14", 0) / commits if commits else 1.0
    mature = m >= MATURE
    if mature and f.get("people", 0) >= GREAT_PEOPLE and f.get("flights", 0) >= GREAT_FLIGHTS and share <= SETTLED_SHARE:
        return 2, (f"great: maturity {m:.2f}, {f['people']} people over {f['flights']} flights, "
                   f"settled ({share:.0%} of its commits in 14 days)")
    if f.get("funSignoff"):
        return 3, f"good: signed off as fun ({f['funSignoff']})"
    rate, minutes = f.get("abilityRate"), f.get("inputMinutes", f.get("humanMinutes", 0))
    replayed = f.get("finished", 0) >= 1 or f.get("people", 0) >= 2
    simple = rate is not None and minutes >= SIMPLE_MINUTES and rate <= SIMPLE_RATE
    if simple and replayed:
        return 1, (f"beginner: {rate:.1f} ability presses a minute over {minutes:.0f} minutes of human input "
                   f"(sticks and throttle only), maturity {m:.2f}")
    if mature:
        return 3, f"good: maturity {m:.2f} (mature, not proven settled and widely played)"
    if rate is None or minutes < SIMPLE_MINUTES:
        return 4, f"floor: maturity {m:.2f}, no human flight with recorded input long enough to judge it"
    if rate > SIMPLE_RATE:
        return 4, f"floor: maturity {m:.2f}, {rate:.1f} ability presses a minute (not a pick-up game)"
    return 4, f"floor: maturity {m:.2f}, simple but nobody has finished or replayed it"


def place(f, verdict=None, blocker=None):
    """(tier, why, source): a person's verdict, then a blocker, then the model."""
    if verdict:
        note = f": {verdict['note']}" if verdict.get("note") else ""
        return verdict["tier"], f"{TIERS[verdict['tier']].lower()}: {verdict['by']} said so{note}", "verdict"
    if blocker:
        return 4, f"floor: blocker - {blocker['what']}", "blocker"
    tier, why = model_tier(f)
    return tier, why, "model"


# --- the cards --------------------------------------------------------------------------------------------------------

def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def guid_of(path):
    m = re.search(r"guid: (\w+)", read(path + ".meta"))
    return m.group(1) if m else ""


def mode_names():
    """GameModes value -> enum name."""
    out = {}
    for name, value in re.findall(r"^\s*(\w+)\s*=\s*(\d+)", read(GAME_MODES), re.M):
        out.setdefault(int(value), name)
    return out


def field(text, key):
    m = re.search(rf"^  {key}:[ \t]*(.*)$", text, re.M)
    return m.group(1).strip().strip("'\"") if m else ""


def cards():
    """The arcade roster in ArcadeGames.asset order, minus Maelstrom itself -> [card dict]."""
    by_guid = {}
    for path in glob.glob(os.path.join(GAMES_DIR, "ArcadeGame*.asset")):
        by_guid[guid_of(path)] = path
    names = mode_names()
    vessel_guid = {}
    for path in glob.glob(os.path.join(REPO, "Assets", "**", "SO_Class_*.asset"), recursive=True):
        vessel_guid[guid_of(path)] = os.path.basename(path)[len("SO_Class_"):-len(".asset")]
    out = []
    for guid in re.findall(r"guid: (\w+), type: 2", read(ROSTER)):
        path = by_guid.get(guid)
        if not path:
            continue
        text = read(path)
        mode = int(field(text, "Mode") or -1)
        if mode == MAELSTROM_MODE:
            continue
        block = re.search(r"^  Vessels:\n((?:  - .*\n)*)", text, re.M)
        hulls = [vessel_guid.get(g, "?") for g in re.findall(r"guid: (\w+)", block.group(1))] if block else []
        file_name = os.path.basename(path)[len("ArcadeGame"):-len(".asset")]
        out.append({"key": file_name.lower(), "name": field(text, "DisplayName") or file_name, "guid": guid,
                    "mode": mode, "modeName": names.get(mode, str(mode)), "scene": field(text, "SceneName"),
                    "hulls": hulls, "card": os.path.relpath(path, REPO)})
    return out


# --- measuring --------------------------------------------------------------------------------------------------------

def git(*args, check=False):
    r = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if check and r.returncode:
        sys.exit(f"git {' '.join(args)} failed: {r.stderr.strip()}")
    return r.stdout


def cat_files(specs):
    """{'<ref>:<path>': text} through one `git cat-file --batch`."""
    if not specs:
        return {}
    p = subprocess.run(["git", "cat-file", "--batch"], cwd=REPO, input=("\n".join(specs) + "\n").encode(),
                       capture_output=True)
    out, data, i = {}, p.stdout, 0
    for spec in specs:
        nl = data.index(b"\n", i)
        header = data[i:nl].decode()
        i = nl + 1
        if header.endswith("missing"):
            continue
        size = int(header.split()[2])
        out[spec] = data[i:i + size].decode("utf-8", errors="ignore")
        i += size + 1
    return out


def harvest_bugledger(fetch=True):
    """Every human flight record, launch report and fun card on origin/bugledger/* -> (flights, launches, funs)."""
    if fetch:
        git("fetch", "-q", "--depth=1", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*")
    refs = git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/bugledger/").split()
    specs, kinds = [], {}
    for ref in refs:
        user = ref.rsplit("/", 1)[-1]
        for path in git("ls-tree", "-r", "--name-only", ref).splitlines():
            if path.startswith("Library/") or not path.endswith(".json"):
                continue
            kind = ("flight" if "/flights/" in path else "load" if "/load/" in path
                    else "fun" if "funcard_" in path else None)
            if kind:
                spec = f"{ref}:{path}"
                specs.append(spec)
                kinds[spec] = (kind, user)
    flights, launches, funs = [], [], []
    for spec, text in cat_files(specs).items():
        kind, user = kinds[spec]
        try:
            d = json.loads(text)
        except ValueError:
            continue
        if kind == "flight":
            pilot = d.get("pilot")
            if pilot not in (None, "human", "mixed"):
                continue                                  # a named AI pilot flew it, not a person
            names = {v: k for k, v in (d.get("buttonNames") or {}).items()}
            presses = collections.Counter(names.get(e[1], str(e[1])) for e in d.get("buttonEvents") or []
                                          if len(e) > 2 and e[2] == 1)
            flights.append({"user": user, "mode": d.get("mode"), "seconds": d.get("raceSeconds") or 0,
                            "finished": bool(d.get("finished")), "presses": dict(presses)})
        elif kind == "load" and d.get("loadKind") == "launch" and d.get("gameMode"):
            launches.append({"user": user, "mode": d["gameMode"]})
        elif kind == "fun":
            funs.append(d)
    return flights, launches, funs


def harvest_git(ref="origin/bleeding-edge"):
    """[(unix time, author, [files])] for every non-merge commit on ref."""
    if git("rev-parse", "--is-shallow-repository").strip() == "true":
        git("fetch", "-q", "--unshallow", "--filter=blob:none", "origin", ref.split("/", 1)[1])
    commits, cur = [], None
    for line in git("log", ref, "--no-merges", "--format=@@%ct|%an", "--name-only").splitlines():
        if line.startswith("@@"):
            ts, author = line[2:].split("|", 1)
            cur = (int(ts), author, [])
            commits.append(cur)
        elif line and cur:
            cur[2].append(line)
    return commits


def game_lanes():
    """lane key -> Lane, the matcher /swimlanes uses for its game lanes."""
    sys.path.insert(0, LANES_DIR)
    import swimlanes  # noqa: E402
    catalog = json.load(open(os.path.join(LANES_DIR, "lanes.json")))
    return {ln.key: ln for ln in swimlanes.load_lanes(REPO, catalog) if ln.kind == "game"}


def fun_signoffs(funs):
    """mode name -> how it was signed off as fun (loop step 1 done naming the game, or a fun card 'yes')."""
    out = {}
    for card in funs:
        if str(card.get("fun", "")).lower() == "yes" and card.get("mode"):
            out.setdefault(card["mode"], f"fun card yes by {card.get('by', '?')} {str(card.get('date', ''))[:10]}")
    return out


def loop_signoffs(all_cards):
    """card key -> loop step 1 'done' whose evidence names the game."""
    out = {}
    if not os.path.exists(LOOP_JSON):
        return out
    loop = json.load(open(LOOP_JSON))
    for vessel, elements in (loop.get("vessels") or {}).items():
        for element, steps in (elements or {}).items():
            step = (steps or {}).get("1") or {}
            if step.get("state") != "done":
                continue
            evidence = step.get("evidence", "")
            for c in all_cards:
                if re.search(rf"\b{re.escape(c['name'])}\b", evidence):
                    out[c["key"]] = f"loop step 1 done by {step.get('by', '?')} {step.get('date', '')}"
    return out


def measure(fetch=True, now=None):
    now = now or time.time()
    all_cards = cards()
    flights, launches, funs = harvest_bugledger(fetch)
    commits = harvest_git()
    lanes = game_lanes()
    funs_by_mode = fun_signoffs(funs)
    loop_done = loop_signoffs(all_cards)
    games = []
    for c in all_cards:
        mine = [f for f in flights if f["mode"] == c["modeName"]]
        people = {f["user"] for f in mine} | {l["user"] for l in launches if l["mode"] == c["modeName"]}
        minutes = sum(f["seconds"] for f in mine) / 60.0
        # Only flights whose recorder logged input count as evidence of HOW a game is played: Skein's rail ride
        # records no button events at all, which would otherwise read as "needs no ability button".
        heard = [f for f in mine if f["presses"]]
        heard_minutes = sum(f["seconds"] for f in heard) / 60.0
        ability = sum(f["presses"].get(b, 0) for f in heard for b in ABILITY_BUTTONS)
        lane = lanes.get(c["key"])
        hits = [x for x in commits if lane and any(lane.matches(p) for p in x[2])]
        # Age from the first commit that touched the game at all: a card file is often renamed or moved (Joust's
        # card reads 74 days old, the game 286), and the cap at 180 days keeps an old name reused for a new game
        # from counting for more than a full share.
        born = [x[0] for x in hits] or [x[0] for x in commits if c["card"] in x[2]]
        f = {
            "commits": len(hits),
            "commits14": sum(1 for x in hits if now - x[0] < 14 * 86400),
            "authors": len({x[1] for x in hits}),
            "cardAgeDays": round((now - min(born)) / 86400) if born else 0,
            "people": len(people),
            "flights": len(mine),
            "launches": sum(1 for l in launches if l["mode"] == c["modeName"]),
            "finished": sum(1 for f in mine if f["finished"]),
            "humanMinutes": round(minutes, 1),
            "inputMinutes": round(heard_minutes, 1),
            "abilityRate": round(ability / heard_minutes, 2) if heard_minutes > 0 else None,
            "funSignoff": loop_done.get(c["key"]) or funs_by_mode.get(c["modeName"]) or "",
        }
        f["maturity"] = maturity(f)
        games.append({**c, "features": f})
    return games, {"flights": len(flights), "launches": len(launches), "funCards": len(funs),
                   "people": len({f["user"] for f in flights} | {l["user"] for l in launches}),
                   "commits": len(commits)}


# --- the json ---------------------------------------------------------------------------------------------------------

def load():
    return json.load(open(DATA)) if os.path.exists(DATA) else {}


def save(data):
    with open(DATA, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1, ensure_ascii=False)
        fh.write("\n")


def placed(data):
    """[(game, tier, why, source)] for every game in the json, verdicts and blockers applied."""
    verdicts, blockers = data.get("verdicts", {}), data.get("blockers", {})
    return [(g, *place(g["features"], verdicts.get(g["key"]), blockers.get(g["key"]))) for g in data.get("games", [])]


def find_game(data, name):
    want = re.sub(r"[^a-z0-9]", "", name.lower())
    for g in data.get("games", []):
        if want in (g["key"], re.sub(r"[^a-z0-9]", "", g["name"].lower()), g["modeName"].lower()):
            return g
    return None


# --- the asset --------------------------------------------------------------------------------------------------------

def ref(guid):
    return f"{{fileID: 11400000, guid: {guid}, type: 2}}"


def asset_block(rows):
    """GameQueue + IntensityTiers YAML for [(game, tier, ...)] (roster order inside each tier)."""
    lines = ["  GameQueue:"] + [f"  - {ref(g['guid'])}" for g, *_ in rows]
    lines.append("  IntensityTiers:")
    for tier in sorted(TIERS):
        lines.append(f"  - Intensity: {tier}")
        members = [g for g, t, *_ in rows if t == tier]
        if members:
            lines.append("    Games:")
            lines += [f"    - {ref(g['guid'])}" for g in members]
        else:
            lines.append("    Games: []")
    return "\n".join(lines) + "\n"


def asset_text(rows, text):
    pattern = re.compile(r"^  GameQueue:\n(?:  - .*\n)*  IntensityTiers:\n(?:  - Intensity: .*\n(?:    .*\n)*)*", re.M)
    if not pattern.search(text):
        sys.exit(f"{os.path.relpath(ASSET, REPO)}: GameQueue / IntensityTiers block not found")
    return pattern.sub(lambda _: asset_block(rows), text, count=1)


def asset_tiers(text):
    """tier -> [guid] as the asset has it, plus the GameQueue guids."""
    queue = re.search(r"^  GameQueue:\n((?:  - .*\n)*)", text, re.M)
    q = re.findall(r"guid: (\w+)", queue.group(1)) if queue else []
    tiers = {}
    for t, body in re.findall(r"^  - Intensity: (\d+)\n((?:    .*\n)*)", text, re.M):
        tiers[int(t)] = re.findall(r"guid: (\w+)", body)
    return q, tiers


# --- the doc ----------------------------------------------------------------------------------------------------------

def doc_text(data, rows):
    s = data.get("summary", {})
    anchors = [(g, t) for g, t, _, src in rows if src == "verdict"]
    lines = [
        "# Maelstrom intensity tiers (generated)",
        "",
        "Generated by `python3 Tools/Build/maelstrom_tiers.py measure` - do not edit by hand; a person's placement is",
        "`maelstrom_tiers.py set <game> <tier> --by <you>`, then `--write`. The model, its fit and the rules are in the",
        "script's docstring and in [`ARCHITECTURE.md` section 1.1](ARCHITECTURE.md). Measured "
        f"{data.get('measured', '?')} from {s.get('flights', 0)} human flight records and {s.get('launches', 0)} launches",
        f"by {s.get('people', 0)} people (origin/bugledger/*), {s.get('funCards', 0)} fun card(s) and "
        f"{s.get('commits', 0)} commits of origin/bleeding-edge.",
        "",
        "A Maelstrom at intensity N deals only tier N's games. Every arcade card is in exactly one tier.",
        "",
        "| Intensity | Tier | Means | Games |",
        "|---|---|---|---:|",
    ]
    for t in sorted(TIERS):
        lines.append(f"| {t} | {TIERS[t]} | {TIER_MEANING[t]} | {sum(1 for _, x, *_ in rows if x == t)} |")
    lines += ["", "## How the examples fit the model", "",
              "Each example scored by the model alone (its verdict and any blocker set aside):", "",
              "| Game | Garrett's tier | Model alone | Why |", "|---|---|---|---|"]
    for g, t in anchors:
        mt, why = model_tier(g["features"])
        lines.append(f"| {g['name']} | {t} {TIERS[t]} | {mt} {TIERS[mt]} | {why} |")
    for t in sorted(TIERS):
        members = [(g, why, src) for g, x, why, src in rows if x == t]
        lines += ["", f"## {t} {TIERS[t]} ({len(members)})", "",
                  "| Game | Hull | Maturity | People | Flights | Ability/min | Commits (14 d) | Placed by | Why |",
                  "|---|---|---:|---:|---:|---:|---:|---|---|"]
        members.sort(key=lambda m: -m[0]["features"]["maturity"])
        for g, why, src in members:
            f = g["features"]
            rate = "-" if f["abilityRate"] is None else f"{f['abilityRate']:.1f}"
            lines.append(f"| {g['name']} | {', '.join(g['hulls'])} | {f['maturity']:.2f} | {f['people']} | "
                         f"{f['flights']} | {rate} | {f['commits']} ({f['commits14']}) | {src} | {why} |")
    return "\n".join(lines) + "\n"


# --- commands ---------------------------------------------------------------------------------------------------------

def print_board(data):
    rows = placed(data)
    for t in sorted(TIERS):
        members = sorted([(g, why) for g, x, why, _ in rows if x == t], key=lambda m: -m[0]["features"]["maturity"])
        print(f"\n{t} {TIERS[t].upper()} - {TIER_MEANING[t]} ({len(members)})")
        for g, why in members:
            print(f"  {g['name']:<22} {why}")


def cmd_measure(fetch):
    data = load()
    games, summary = measure(fetch)
    data.update({"schema": "froglet.maelstrom-tiers/1", "doc": "Docs/MaelstromSystem/INTENSITY_TIERS.md",
                 "measured": datetime.date.today().isoformat(), "summary": summary, "games": games})
    data.setdefault("verdicts", {})
    data.setdefault("blockers", {})
    save(data)
    rows = placed(data)
    with open(DOC, "w", encoding="utf-8") as fh:
        fh.write(doc_text(data, rows))
    print_board(data)
    print(f"\nwrote {os.path.relpath(DATA, REPO)} and {os.path.relpath(DOC, REPO)}; run --write to update the asset")


def cmd_set(name, tier, by, note):
    data = load()
    g = find_game(data, name)
    if not g:
        sys.exit(f"no arcade game called {name!r}")
    verdicts = data.setdefault("verdicts", {})
    if tier == "clear":
        verdicts.pop(g["key"], None)
    else:
        verdicts[g["key"]] = {k: v for k, v in {"tier": int(tier), "by": by, "on": datetime.date.today().isoformat(),
                                                  "note": note}.items() if v}
    save(data)
    with open(DOC, "w", encoding="utf-8") as fh:
        fh.write(doc_text(data, placed(data)))
    print(f"{g['name']}: {tier} - written; run --write to update the asset, then commit both")


def add_unmeasured(data):
    """Put every roster card the json has never seen into it, unmeasured (so the model sends it to the floor), and
    drop cards that left the roster - so a new arcade mode joins the Maelstrom with `--write` alone, offline.
    Returns the names added."""
    roster = cards()
    known = {g["guid"] for g in data.get("games", [])}
    keep = {c["guid"] for c in roster}
    added = []
    games = [g for g in data.get("games", []) if g["guid"] in keep]
    for c in roster:
        if c["guid"] not in known:
            f = {"commits": 0, "commits14": 0, "authors": 0, "cardAgeDays": 0, "people": 0, "flights": 0,
                 "launches": 0, "finished": 0, "humanMinutes": 0, "inputMinutes": 0, "abilityRate": None,
                 "funSignoff": ""}
            f["maturity"] = maturity(f)
            games.append({**c, "features": f})
            added.append(c["name"])
    order = {c["guid"]: i for i, c in enumerate(roster)}
    games.sort(key=lambda g: order[g["guid"]])
    changed = added or len(games) != len(data.get("games", [])) or \
        [g["guid"] for g in games] != [g["guid"] for g in data.get("games", [])]
    data["games"] = games
    return added, bool(changed)


def cmd_write():
    data = load()
    added, changed = add_unmeasured(data)
    if changed:
        save(data)
        with open(DOC, "w", encoding="utf-8") as fh:
            fh.write(doc_text(data, placed(data)))
        if added:
            print(f"added unmeasured (floor until measured): {', '.join(added)}")
    rows = placed(data)
    text = read(ASSET)
    new = asset_text(rows, text)
    if new != text:
        with open(ASSET, "w", encoding="utf-8") as fh:
            fh.write(new)
    counts = collections.Counter(t for _, t, *_ in rows)
    print(f"wrote {os.path.relpath(ASSET, REPO)}: " + ", ".join(f"I{t} {counts[t]}" for t in sorted(TIERS)))


def problems(data, asset, roster_guids):
    rows = placed(data)
    out = []
    queue, tiers = asset_tiers(asset)
    want_queue = [g["guid"] for g, *_ in rows]
    if queue != want_queue:
        out.append("MaelstromData.asset GameQueue differs from the json (run --write)")
    for t in sorted(TIERS):
        want = [g["guid"] for g, x, *_ in rows if x == t]
        if tiers.get(t, []) != want:
            out.append(f"MaelstromData.asset tier {t} differs from the json (run --write)")
        if not want:
            out.append(f"tier {t} ({TIERS[t]}) is empty: a Maelstrom at intensity {t} would deal the whole roster")
    seen = collections.Counter(g for gs in tiers.values() for g in gs)
    for guid in roster_guids:
        if seen[guid] != 1:
            out.append(f"arcade card {guid} is in {seen[guid]} tiers (every card is in exactly one)")
    for key, v in data.get("verdicts", {}).items():
        hit = [t for g, t, *_ in rows if g["key"] == key]
        if hit and hit[0] != v["tier"]:
            out.append(f"{key}: {v['by']} placed it at {v['tier']}, the json places it at {hit[0]}")
    return out


def cmd_check():
    data = load()
    if not data.get("games"):
        sys.exit(f"{os.path.relpath(DATA, REPO)} has no games: run measure")
    roster = [c["guid"] for c in cards()]
    out = problems(data, read(ASSET), roster)
    json_guids = {g["guid"] for g in data["games"]}
    for guid in roster:
        if guid not in json_guids:
            out.append(f"arcade card {guid} is on the roster but not in the json (run --write: it joins the floor)")
    if os.path.exists(DOC) and read(DOC) != doc_text(data, placed(data)):
        out.append(f"{os.path.relpath(DOC, REPO)} is stale (run measure or set)")
    for p in out:
        print("FAIL", p)
    if out:
        return 1
    print(f"OK: {len(roster)} arcade cards, each in exactly one Maelstrom tier; asset and doc match the json")
    return 0


def self_test():
    bad = []

    def expect(cond, what):
        if not cond:
            bad.append(what)

    simple = {"commits": 8, "commits14": 5, "authors": 2, "cardAgeDays": 20, "people": 2, "flights": 8,
              "finished": 3, "humanMinutes": 15, "abilityRate": 0.0, "funSignoff": ""}
    polished = {"commits": 193, "commits14": 9, "authors": 7, "cardAgeDays": 161, "people": 4, "flights": 26,
                "finished": 10, "humanMinutes": 34, "abilityRate": 0.0, "funSignoff": ""}
    worked = {"commits": 206, "commits14": 3, "authors": 8, "cardAgeDays": 286, "people": 1, "flights": 1,
              "finished": 0, "humanMinutes": 2, "abilityRate": 0.0, "funSignoff": ""}
    expect(model_tier(simple)[0] == 1, "a young throttle-only game that people replay is a beginner game")
    expect(model_tier(polished)[0] == 2, "a mature, widely played, settled game is great")
    expect(model_tier(worked)[0] == 3, "a mature game few people play is good, not great")
    expect(model_tier(dict(worked, people=3))[0] == 1, "a mature button-free game people replay is a beginner game")
    expect(model_tier(dict(simple, funSignoff="loop step 1 done"))[0] == 3, "a fun sign-off beats pick-up")
    churning = dict(polished, commits14=60)
    expect(model_tier(churning)[0] != 2, "negative control: a game still churning is not great")
    buttons = dict(simple, abilityRate=8.0)
    expect(model_tier(buttons)[0] == 4, "negative control: a game that needs ability buttons is not a beginner game")
    unflown = dict(simple, abilityRate=None, humanMinutes=0, flights=0, people=0, finished=0)
    expect(model_tier(unflown)[0] == 4, "negative control: a game nobody has flown cannot be proven simple")
    signed = dict(unflown, funSignoff="loop step 1 done")
    expect(model_tier(signed)[0] == 3, "a game a person signed off as fun is good")
    expect(place(polished, blocker={"what": "x"})[0] == 4, "a blocker sends a game to the floor")
    expect(place(polished, verdict={"tier": 1, "by": "G"}, blocker={"what": "x"})[0] == 1, "a verdict beats a blocker")
    # the asset round trip, and the check catching a card in two tiers
    g1 = {"key": "a", "guid": "a" * 32, "features": simple}
    g2 = {"key": "b", "guid": "b" * 32, "features": polished}
    g3 = {"key": "c", "guid": "c" * 32, "features": worked}
    g4 = {"key": "d", "guid": "d" * 32, "features": unflown}
    data = {"games": [g1, g2, g3, g4], "verdicts": {}, "blockers": {}}
    text = "x\n  GameQueue:\n  - old\n  IntensityTiers:\n  - Intensity: 1\n    Games: []\n  LobbySceneName: M\n"
    new = asset_text(placed(data), text)
    expect(new.endswith("  LobbySceneName: M\n") and new.startswith("x\n"), "the writer keeps the rest of the asset")
    expect(not problems(data, new, [g["guid"] for g in data["games"]]), "a written asset passes the check")
    twice = new.replace(f"  - Intensity: 2\n    Games:\n", f"  - Intensity: 2\n    Games:\n    - {ref('a' * 32)}\n")
    expect(any("2 tiers" in p for p in problems(data, twice, [g["guid"] for g in data["games"]])),
           "negative control: the check catches a card in two tiers")
    data["verdicts"]["d"] = {"tier": 2, "by": "G"}
    expect(any("differs" in p for p in problems(data, new, [g["guid"] for g in data["games"]])),
           "negative control: the check catches an asset written before a verdict")
    for b in bad:
        print("FAIL", b)
    print("self-test:", "FAILED" if bad else "ok")
    return 1 if bad else 0


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("command", nargs="*", help="measure | set <game> <1-4|clear>")
    p.add_argument("--no-fetch", action="store_true", help="measure from the bugledger branches already fetched")
    p.add_argument("--by", help="set: who decided")
    p.add_argument("--note", default="", help="set: why")
    p.add_argument("--write", action="store_true")
    p.add_argument("--check", action="store_true")
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args(argv)
    if a.self_test:
        return self_test()
    if a.check:
        return cmd_check()
    if a.write:
        cmd_write()
        return 0
    if a.command[:1] == ["measure"]:
        cmd_measure(not a.no_fetch)
        return 0
    if a.command[:1] == ["set"]:
        if len(a.command) != 3 or a.command[2] not in ("1", "2", "3", "4", "clear") or not a.by:
            p.error("set <game> <1|2|3|4|clear> --by <who> [--note ...]")
        cmd_set(a.command[1], a.command[2], a.by, a.note)
        return 0
    data = load()
    if not data.get("games"):
        sys.exit("no measurements yet: run `maelstrom_tiers.py measure`")
    print_board(data)
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""The elemental vessel loop's board: where every vessel stands in Garrett's five-step loop, and what to do next.

Garrett, 2026-11-12: "I need your help in making sure that pushing every vessel through this plan in a systematic way
remains the highest priority for anyone who has any cycles." The process is Docs/ElementalLoop/PLAYBOOK.md; this script
is its board.

    python3 Tools/Build/elemental_loop.py                 print the board
    python3 Tools/Build/elemental_loop.py next [--n 12]   the ranked next actions for anyone with cycles
    python3 Tools/Build/elemental_loop.py vessel <Name>   one vessel, every element, every step
    python3 Tools/Build/elemental_loop.py set <Vessel|Arena> <Element> <step> <state> --by <who> --evidence "..."
                                                          record a step (state: todo, doing, built, done, redo, n/a)
    python3 Tools/Build/elemental_loop.py --write         write Docs/ElementalLoop/STATUS.md
    python3 Tools/Build/elemental_loop.py --check         exit 1 when STATUS.md is stale or loop.json is invalid
    python3 Tools/Build/elemental_loop.py --self-test     known answers and negative controls

Two sources, never typed twice:
- MEASURED from the shipped assets: which arcade cards seat each vessel and what element each card is (the game's own
  answer, ModeGenre.TryElementsFor, read by Tools/Analytics/server/mode_elements.py), each card's intensity range, and
  which arena games seat each vessel. That is step 0 (the game kit) and the floor of steps 1-2: a game that exists is
  at least "built".
- RECORDED in Docs/ElementalLoop/loop.json: what a person or a session signed off (steps 1-3 per vessel and element,
  steps 4-5 per arena game), with who, when, the evidence, and whether the sign-off was a human's or automatic. Loop
  after loop, the share of automatic sign-offs is how "less human involvement" is measured.

Standard library only.
"""
import argparse
import datetime
import glob
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(REPO, "Tools", "Analytics", "server"))

import mode_elements  # noqa: E402  - the game's own answer to "what element is this mode"

LOOP_JSON = os.path.join(REPO, "Docs", "ElementalLoop", "loop.json")
STATUS_MD = os.path.join(REPO, "Docs", "ElementalLoop", "STATUS.md")

ELEMENTS = ("Time", "Charge", "Mass", "Space")
VESSEL_STEPS = ("1", "2", "3")          # per vessel and element: fun, intensities, AI
ARENA_STEPS = ("4", "5")                # per arena game: environment creator, vessel imbalance
HUMAN_GATED = ("1", "2")                # one human signs these off as fun (Garrett, 2026-11-12)
STATES = ("todo", "doing", "built", "done", "redo", "n/a")
STEP_NAMES = {
    "0": "game kit",
    "1": "fun (abilities + game, one intensity)",
    "2": "intensities I1-I4",
    "3": "AI on I1-I4",
    "4": "environment creator",
    "5": "vessel imbalance",
}
MARK = {"todo": ".", "doing": "~", "built": "b", "done": "D", "redo": "R", "n/a": "-", "nogame": "x"}


# ---------------------------------------------------------------- measured: the shipped cards


def _read(path):
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def class_index(root=REPO):
    """class SO guid -> roster key (SO_Class_<Key>.asset)."""
    out = {}
    for meta in glob.glob(os.path.join(root, "Assets", "_SO_Assets", "Classes", "SO_Class_*.asset.meta")):
        m = re.search(r"^guid: (\w+)", _read(meta), re.M)
        if m:
            out[m.group(1)] = os.path.basename(meta)[len("SO_Class_"):-len(".asset.meta")]
    return out


def roster(root=REPO):
    """Every vessel with an element kit: one Assets/Resources/ElementalAbilityMaps/<Vessel>.asset each."""
    return sorted(os.path.basename(p)[:-6] for p in
                  glob.glob(os.path.join(root, "Assets", "Resources", "ElementalAbilityMaps", "*.asset")))


def game_modes(root=REPO):
    text = _read(os.path.join(root, "Assets", "_Scripts", "Data", "Enums", "GameModes.cs"))
    return {int(v): k for k, v in re.findall(r"^\s+(\w+)\s*=\s*(\d+)", text, re.M)}


def cards(root=REPO):
    """Every SO_ArcadeGame: mode, display name, hulls (roster keys), lists it is on, intensity range, elements."""
    modes, classes = game_modes(root), class_index(root)
    genres = mode_elements.build()["modes"] if root == REPO else {}
    lists = {}
    for name in ("ArcadeGames", "ArenaGames"):
        path = os.path.join(root, "Assets", "_SO_Assets", "Games", "GameLists", f"{name}.asset")
        lists[name] = set(re.findall(r"guid: (\w+)", _read(path))) if os.path.exists(path) else set()
    out = []
    for path in sorted(glob.glob(os.path.join(root, "Assets", "_SO_Assets", "Games", "ArcadeGame*.asset"))):
        text = _read(path)
        guid = re.search(r"^guid: (\w+)", _read(path + ".meta"), re.M).group(1)
        mode = modes.get(int(re.search(r"^\s+Mode: (\d+)", text, re.M).group(1)))
        block = re.search(r"^\s+Vessels:\n((?:\s+- \{.*\n)*)", text, re.M)
        hulls = [classes[g] for g in re.findall(r"guid: (\w+)", block.group(1) if block else "") if g in classes]
        name = re.search(r"^\s+DisplayName: (.+)$", text, re.M)
        lo = re.search(r"^\s+MinIntensity: (\d+)", text, re.M)
        hi = re.search(r"^\s+MaxIntensity: (\d+)", text, re.M)
        out.append({
            "mode": mode,
            "name": name.group(1).strip() if name else mode,
            "hulls": hulls,
            "lists": sorted(k for k, v in lists.items() if guid in v),
            "intensities": [int(lo.group(1)) if lo else 1, int(hi.group(1)) if hi else 1],
            "elements": [e for e in (genres.get(mode) or []) if e in ELEMENTS],
        })
    return out


def kit(vessel, all_cards, no_game_ok):
    """Step 0 for one vessel: its live arcade games per element, and whether the kit meets the rule
    (a Time game, a Charge game, and a Mass or a Space game)."""
    games = {e: [] for e in ELEMENTS}
    for c in all_cards:
        if "ArcadeGames" not in c["lists"] or vessel not in c["hulls"]:
            continue
        for e in c["elements"]:
            games[e].append({"mode": c["mode"], "name": c["name"], "shared": len(c["hulls"]) > 1,
                             "full_ladder": c["intensities"][0] <= 1 and c["intensities"][1] >= 4})
    excused = set(no_game_ok.get(vessel, []))
    missing = []
    if not games["Time"]:
        missing.append("Time")
    if not games["Charge"]:
        missing.append("Charge")
    if not games["Mass"] and not games["Space"]:
        missing.append("Mass or Space" if not excused else ("Space" if "Mass" in excused else "Mass"))
    return games, missing


def arenas_for(vessel, all_cards, arena_modes):
    seated = {}
    for element, mode in arena_modes.items():
        card = next((c for c in all_cards if c["mode"] == mode), None)
        seated[element] = bool(card and vessel in card["hulls"])
    return seated


# ---------------------------------------------------------------- recorded: loop.json


def load(path=LOOP_JSON):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def validate(data, vessels):
    """Problems with loop.json, as sentences. Empty when it is sound."""
    problems = []
    for key in ("schema", "arena_games", "vessels", "arenas", "no_game_ok", "loops"):
        if key not in data:
            problems.append(f"loop.json has no '{key}'")
    if problems:
        return problems

    def check_entry(where, entry):
        if entry.get("state") not in STATES:
            problems.append(f"{where}: state '{entry.get('state')}' is not one of {', '.join(STATES)}")
        if entry.get("state") in ("done", "redo", "doing", "built", "n/a") and not entry.get("evidence"):
            problems.append(f"{where}: a recorded state needs its evidence (a doc, PR, report or playtest)")
        if entry.get("state") == "done":
            if not entry.get("by") or not entry.get("date"):
                problems.append(f"{where}: 'done' needs who signed it off ('by') and when ('date')")
            if entry.get("kind") not in ("human", "auto"):
                problems.append(f"{where}: 'done' needs kind 'human' or 'auto' (how human involvement is measured)")

    for vessel, elements in data["vessels"].items():
        if vessel not in vessels:
            problems.append(f"vessels.{vessel}: not on the roster (Assets/Resources/ElementalAbilityMaps)")
        for element, steps in elements.items():
            if element not in ELEMENTS:
                problems.append(f"vessels.{vessel}.{element}: not an element")
            for step, entry in steps.items():
                if step not in VESSEL_STEPS:
                    problems.append(f"vessels.{vessel}.{element}.{step}: vessel steps are 1, 2, 3")
                check_entry(f"vessels.{vessel}.{element}.{step}", entry)
                # Garrett, 2026-11-12: one human's fun sign-off gates step 1 -> 2 and step 2 -> 3.
                if entry.get("state") == "done" and step in HUMAN_GATED and entry.get("kind") != "human":
                    problems.append(f"vessels.{vessel}.{element}.{step}: step {step} is signed off as fun by a human "
                                    f"(--kind human), never automatically")
                below = str(int(step) - 1)
                if entry.get("state") == "done" and step in ("2", "3") and \
                        steps.get(below, {}).get("state") != "done":
                    problems.append(f"vessels.{vessel}.{element}.{step}: done before step {below} is signed off; "
                                    f"step {below} needs a human's fun sign-off first")
    for arena, steps in data["arenas"].items():
        if arena not in data["arena_games"]:
            problems.append(f"arenas.{arena}: not one of arena_games")
        for step, entry in steps.items():
            if step not in ARENA_STEPS:
                problems.append(f"arenas.{arena}.{step}: arena steps are 4, 5")
            check_entry(f"arenas.{arena}.{step}", entry)
    for vessel, excused in data["no_game_ok"].items():
        if vessel not in vessels:
            problems.append(f"no_game_ok.{vessel}: not on the roster")
        if [e for e in excused if e not in ("Mass", "Space")]:
            problems.append(f"no_game_ok.{vessel}: only Mass or Space may be excused (Time and Charge are required)")
    return problems


# ---------------------------------------------------------------- the board


def state_of(data, vessel, element, step, games):
    """A vessel step's state: what loop.json records, else what the assets prove (a game that exists is 'built')."""
    entry = data["vessels"].get(vessel, {}).get(element, {}).get(step)
    if entry:
        return entry["state"], entry
    if not games[element]:
        excused = element in data["no_game_ok"].get(vessel, [])
        return ("n/a" if excused else "nogame"), None
    if step == "1":
        return "built", None
    if step == "2":
        return ("built" if any(g["full_ladder"] for g in games[element]) else "todo"), None
    return "todo", None


def build(root=REPO, data=None):
    data = data if data is not None else load()
    all_cards = cards(root)
    vessels = roster(root)
    rows = []
    for v in vessels:
        games, missing = kit(v, all_cards, data["no_game_ok"])
        steps = {e: {s: state_of(data, v, e, s, games) for s in VESSEL_STEPS} for e in ELEMENTS}
        rows.append({"vessel": v, "games": games, "missing": missing,
                     "arenas": arenas_for(v, all_cards, data["arena_games"]), "steps": steps})
    return {"data": data, "cards": all_cards, "rows": rows}


def element_complete(board, element, step="3"):
    """Whether every vessel seated in this element's arena has finished steps 1..step for it."""
    arena_elements = [element] if element in ("Time", "Charge") else ["Mass", "Space"]
    for r in board["rows"]:
        if not r["arenas"][element]:
            continue
        for e in arena_elements:
            for s in VESSEL_STEPS[: int(step)]:
                if r["steps"][e][s][0] not in ("done", "n/a", "nogame"):
                    return False
    return True


ACTION = {
    "1": "Fun pass on {game}: score the {v}'s {e} abilities and the game together at one intensity (PLAYBOOK step 1), "
         "raise the lowest floor, then ask a person to fly it in the editor and answer the fun card (fun_cards.py apply records it)",
    "2": "Intensity pass on {game}: check I1-I4 serve intro, fun, challenging and very hard and each shows what the {v} "
         "can do (PLAYBOOK step 2; rung_bars.py checks the rungs from the flights), fix the worst rung, then ask a person "
         "to fly I1-I4 and answer the fun card on each",
    "3": "Train the {v}'s universal {e} AI on {game} I1-I4 (one genome, AI_PLAN rule 2), check it in the editor",
}


def actions(board):
    """Every open action, ranked. Lower tuple sorts first."""
    data = board["data"]
    order = {e: i for i, e in enumerate(data.get("element_order", ELEMENTS))}
    out = []
    # The arena steps, when their element's vessels are through step 3.
    for element, mode in data["arena_games"].items():
        rec = data["arenas"].get(element, {})
        for step in ARENA_STEPS:
            st = rec.get(step, {}).get("state", "todo")
            if st == "done":
                continue
            ready = element_complete(board, element)
            out.append({
                "rank": (order.get(element, 9), 0 if ready else 1, int(step), ""),
                "who": "session", "vessel": "-", "element": element, "step": step, "state": st,
                "blocked": None if ready else f"waits on step 3 for every vessel {mode} seats",
                "text": (f"{mode}: train the environment creator against the fixed {element} AI pool until it plateaus"
                         if step == "4" else
                         f"{mode}: read the plateau's vessel imbalance, propose vessel / element tunings (a person "
                         f"approves), send the tuned vessels back to steps 2-4"),
            })
            break  # one open arena step at a time
    # The vessel steps: the earliest open step of each vessel and element.
    for r in board["rows"]:
        for e in ELEMENTS:
            arena_e = e if e in ("Time", "Charge") else "Mass"   # Mass and Space share one arena (Brood Rush)
            seated = r["arenas"].get(arena_e, False)
            for s in VESSEL_STEPS:
                st, entry = r["steps"][e][s]
                if st in ("done", "n/a", "nogame"):
                    continue
                game = ", ".join(g["name"] for g in r["games"][e]) or "its game"
                who = "session, a person signs off" if s in ("1", "2") else "session"
                out.append({
                    "rank": (order.get(arena_e, 9), 0 if seated else 1, int(s), r["vessel"]),
                    "who": who, "vessel": r["vessel"], "element": e, "step": s, "state": st, "blocked": None,
                    "text": ACTION[s].format(game=game, v=r["vessel"], e=e)
                            + (f" (now: {entry['evidence']})" if entry and entry.get("evidence") and st != "todo" else ""),
                })
                break
        for m in r["missing"]:
            out.append({
                "rank": (8, 0 if any(r["arenas"].values()) else 1, 0, r["vessel"]),
                "who": "session", "vessel": r["vessel"], "element": m, "step": "0", "state": "todo", "blocked": None,
                "text": f"Build the {r['vessel']}'s {m} arcade game (/arcadegame; Docs/MassAndSpaceGames/PLAN.md for "
                        f"Mass and Space): the kit needs a Time, a Charge and a Mass or Space game",
            })
    out.sort(key=lambda a: a["rank"])
    return out


# ---------------------------------------------------------------- what the other tools read: lanes and rework


def vessel_lanes(board):
    """The loop as swim lanes (Garrett, 2026-11-12: /swimlanes "should be advancing each vessel as a high priority
    parallel lane through this process"). Per vessel: its open loop actions in `next` order, the rank of its first,
    a one-line progress (each element's steps 1-3 as board marks) and the arenas it is seated in."""
    acts = actions(board)
    out = {}
    for r in board["rows"]:
        mine = [a for a in acts if a["vessel"] == r["vessel"]]
        progress = ", ".join(f"{e} " + "".join(MARK.get(r["steps"][e][s][0], "?") for s in VESSEL_STEPS)
                             for e in ELEMENTS)
        out[r["vessel"]] = {"actions": mine, "rank": mine[0]["rank"] if mine else None, "progress": progress,
                            "seated": [board["data"]["arena_games"][e] for e, v in r["arenas"].items() if v]}
    return out


STEP_COST = {"1": 1, "2": 2, "3": 3, "4": 4}     # a retraining (3) and an environment creator (4) cost the most to redo
LIVE = ("doing", "built", "done")                 # work that exists, so a late catch throws it away


def rework(board, vessel, elements=None, from_step="1"):
    """What a problem caught NOW in this vessel (against its spirit, at `from_step`) sends back, and what it costs.
    Every loop step at or after `from_step` that is already doing, built or done for the named elements (all four when
    none are named) has to be redone, and so does the arena's step 4 when the vessel is seated there and it is under
    way. A signed-off step costs double (a person's playtest is lost too). The later a catch comes, the higher this is:
    /approve sorts by it so a stakeholder catches the issue before more is built on it (Garrett, 2026-11-12).
    Returns (score, [sentences])."""
    row = next((r for r in board["rows"] if r["vessel"].lower() == vessel.lower()), None)
    if row is None:
        return 0, []
    score, hits, arenas = 0, [], set()
    for e in (elements or ELEMENTS):
        if e not in ELEMENTS:
            continue
        for s in VESSEL_STEPS:
            if int(s) < int(from_step):
                continue
            st = row["steps"][e][s][0]
            if st in LIVE:
                cost = STEP_COST[s] * (2 if st == "done" else 1)
                score += cost
                hits.append(f"{row['vessel']} {e} step {s} {('fun', 'intensities', 'AI')[int(s) - 1]} ({st})")
        arena_e = e if e in ("Time", "Charge") else "Mass"
        if row["arenas"].get(arena_e):
            arenas.add(arena_e)
    for a in sorted(arenas):
        st = board["data"]["arenas"].get(a, {}).get("4", {}).get("state", "todo")
        if st in LIVE:
            score += STEP_COST["4"] * (2 if st == "done" else 1)
            hits.append(f"{board['data']['arena_games'][a]} step 4 environment creator ({st}), trained against this AI")
    return score, hits


def human_share(data):
    """Per loop: how many sign-offs were a person's and how many were automatic."""
    counts = {}

    def add(entry):
        if entry.get("state") == "done":
            loop = entry.get("loop", 1)
            c = counts.setdefault(loop, {"human": 0, "auto": 0})
            c[entry.get("kind", "human")] += 1
    for elements in data["vessels"].values():
        for steps in elements.values():
            for entry in steps.values():
                add(entry)
    for steps in data["arenas"].values():
        for entry in steps.values():
            add(entry)
    return counts


def markdown(board):
    data, rows = board["data"], board["rows"]
    L = []
    L.append("# The elemental loop: where every vessel stands")
    L.append("")
    L.append("Generated by `python3 Tools/Build/elemental_loop.py --write`. Do not edit by hand: record a step with "
             "`elemental_loop.py set ...` (it writes `loop.json`) and re-run. The process is [`PLAYBOOK.md`](PLAYBOOK.md). "
             "What to do next: `python3 Tools/Build/elemental_loop.py next`.")
    L.append("")
    L.append("Marks: `D` done (signed off), `b` built (exists, not yet signed off against the playbook's exit "
             "criteria), `~` doing, `.` todo, `R` redo (step 5 sent it back), `x` no game for this element yet, "
             "`-` not needed (an excused Mass or Space). Steps: 1 fun, 2 intensities, 3 AI.")
    L.append("")
    L.append("## 1. Vessels")
    L.append("")
    L.append("| Vessel | Time 1 2 3 | Charge 1 2 3 | Mass 1 2 3 | Space 1 2 3 | Kit missing | Arenas |")
    L.append("|---|---|---|---|---|---|---|")
    for r in rows:
        cells = []
        for e in ELEMENTS:
            cells.append("`" + " ".join(MARK[r["steps"][e][s][0]] for s in VESSEL_STEPS) + "`")
        arenas = ", ".join(data["arena_games"][k] for k, v in r["arenas"].items() if v) or "-"
        L.append(f"| {r['vessel']} | " + " | ".join(cells) + f" | {', '.join(r['missing']) or '-'} | {arenas} |")
    L.append("")
    L.append("## 2. Arena games")
    L.append("")
    L.append("| Element | Arena game | Vessels seated | Their steps 1-3 complete | Step 4 environment creator | "
             "Step 5 vessel imbalance |")
    L.append("|---|---|---|---|---|---|")
    for element, mode in data["arena_games"].items():
        seated = [r["vessel"] for r in rows if r["arenas"][element]]
        rec = data["arenas"].get(element, {})

        def cell(step):
            e = rec.get(step)
            return f"{e['state']}: {e['evidence']}" if e else "todo"
        label = element if element in ("Time", "Charge") else "Mass + Space"
        L.append(f"| {label} | {mode} | {', '.join(seated)} | {'yes' if element_complete(board, element) else 'no'} "
                 f"| {cell('4')} | {cell('5')} |")
    L.append("")
    L.append("## 3. Each vessel's arcade games (measured from the cards)")
    L.append("")
    L.append("A shared card (several hulls) counts for every hull it seats; `*` marks a card without the full I1-I4 "
             "ladder.")
    L.append("")
    L.append("| Vessel | Time | Charge | Mass | Space |")
    L.append("|---|---|---|---|---|")
    for r in rows:
        cols = []
        for e in ELEMENTS:
            gs = r["games"][e]
            cols.append(", ".join(g["name"] + (" (shared)" if g["shared"] else "") + ("" if g["full_ladder"] else "*")
                                  for g in gs) or ("excused" if e in data["no_game_ok"].get(r["vessel"], []) else "-"))
        L.append(f"| {r['vessel']} | " + " | ".join(cols) + " |")
    L.append("")
    L.append("## 4. Recorded steps")
    L.append("")
    L.append("| Where | Step | State | By | Date | Kind | Evidence |")
    L.append("|---|---|---|---|---|---|---|")
    for vessel, elements in sorted(data["vessels"].items()):
        for element, steps in elements.items():
            for step, e in sorted(steps.items()):
                L.append(f"| {vessel} {element} | {step} {STEP_NAMES[step]} | {e['state']} | {e.get('by', '')} | "
                         f"{e.get('date', '')} | {e.get('kind', '')} | {e.get('evidence', '')} |")
    for element, steps in data["arenas"].items():
        for step, e in sorted(steps.items()):
            L.append(f"| {data['arena_games'][element]} | {step} {STEP_NAMES[step]} | {e['state']} | "
                     f"{e.get('by', '')} | {e.get('date', '')} | {e.get('kind', '')} | {e.get('evidence', '')} |")
    L.append("")
    L.append("## 5. Human involvement per loop")
    L.append("")
    share = human_share(data)
    if not share:
        L.append("No step has been signed off `done` yet, so there is nothing to count. Every sign-off records "
                 "`kind` (human or auto); this table is how the playbook measures that each loop needs fewer people.")
    else:
        L.append("| Loop | Human sign-offs | Automatic | Automatic share |")
        L.append("|---|---:|---:|---:|")
        for loop, c in sorted(share.items()):
            total = c["human"] + c["auto"]
            L.append(f"| {loop} | {c['human']} | {c['auto']} | {c['auto'] / total:.0%} |")
    L.append("")
    L.append("## 6. Next, for anyone with cycles (top 15)")
    L.append("")
    L.append("| # | Who can do it | Vessel | Element | Step | State | Action |")
    L.append("|---:|---|---|---|---|---|---|")
    for i, a in enumerate(actions(board)[:15], 1):
        blocked = f" **Blocked:** {a['blocked']}." if a["blocked"] else ""
        L.append(f"| {i} | {a['who']} | {a['vessel']} | {a['element']} | {a['step']} | {a['state']} | "
                 f"{a['text']}.{blocked} |")
    L.append("")
    return "\n".join(L)


def board_text(board):
    lines = []
    for r in board["rows"]:
        cells = "  ".join(f"{e[:2]} " + "".join(MARK[r["steps"][e][s][0]] for s in VESSEL_STEPS) for e in ELEMENTS)
        miss = f"  missing: {', '.join(r['missing'])}" if r["missing"] else ""
        lines.append(f"{r['vessel']:<12} {cells}{miss}")
    return "\n".join(lines)


# ---------------------------------------------------------------- set


def set_step(data, where, element, step, state, by, evidence, kind, loop, today=None):
    if state not in STATES:
        raise SystemExit(f"state must be one of {', '.join(STATES)}")
    entry = {"state": state, "by": by, "date": today or datetime.date.today().isoformat(), "evidence": evidence}
    if state == "done":
        entry["kind"] = kind
        entry["loop"] = loop
    if where in data["arena_games"].values() or where in ("Arena", "arena"):
        key = next((k for k, v in data["arena_games"].items() if v == where), element)
        if step not in ARENA_STEPS:
            raise SystemExit("arena steps are 4 and 5")
        data["arenas"].setdefault(key, {})[step] = entry
    else:
        if step not in VESSEL_STEPS:
            raise SystemExit("vessel steps are 1, 2 and 3")
        if element not in ELEMENTS:
            raise SystemExit(f"element must be one of {', '.join(ELEMENTS)}")
        data["vessels"].setdefault(where, {}).setdefault(element, {})[step] = entry
    return data


def save(data, path=LOOP_JSON):
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1, ensure_ascii=False)
        fh.write("\n")


# ---------------------------------------------------------------- self-test


def self_test():
    failures = []

    def expect(cond, what):
        if not cond:
            failures.append(what)

    data = load()
    vessels = roster()
    expect(not validate(data, vessels), "the shipped loop.json validates: " + "; ".join(validate(data, vessels)))
    # Negative controls: each of these must be caught.
    bad = json.loads(json.dumps(data))
    bad["vessels"]["Dolphin"] = {"Time": {"1": {"state": "done", "evidence": "x"}}}
    expect(any("needs who signed it off" in p for p in validate(bad, vessels)), "a 'done' with no signer is caught")
    bad = json.loads(json.dumps(data))
    bad["vessels"]["Nobody"] = {}
    expect(any("not on the roster" in p for p in validate(bad, vessels)), "an unknown vessel is caught")
    bad = json.loads(json.dumps(data))
    bad["no_game_ok"]["Dolphin"] = ["Time"]
    expect(any("only Mass or Space" in p for p in validate(bad, vessels)), "excusing a Time game is caught")
    bad = json.loads(json.dumps(data))
    bad["vessels"]["Dolphin"] = {"Time": {"2": {"state": "built"}}}
    expect(any("needs its evidence" in p for p in validate(bad, vessels)), "a state with no evidence is caught")

    bad = json.loads(json.dumps(data))
    bad["vessels"]["Dolphin"] = {"Time": {"1": {"state": "done", "by": "gate", "date": "2026-01-01", "kind": "auto",
                                              "evidence": "x", "loop": 1}}}
    expect(any("signed off as fun by a human" in p for p in validate(bad, vessels)),
           "an automatic fun sign-off is caught")
    bad = json.loads(json.dumps(data))
    bad["vessels"]["Dolphin"] = {"Time": {"2": {"state": "done", "by": "Garrett", "date": "2026-01-01", "kind": "human",
                                              "evidence": "x", "loop": 1}}}
    expect(any("done before step 1" in p for p in validate(bad, vessels)), "a step 2 done before step 1 is caught")

    board = build(data=data)
    rows = {r["vessel"]: r for r in board["rows"]}
    # Known answers from the shipped cards (Docs/MassAndSpaceGames/PLAN.md section 2).
    expect("Switchback" in [g["mode"] for g in rows["Dolphin"]["games"]["Time"]], "Dolphin's Time game is Switchback")
    expect("Rampage" in [g["mode"] for g in rows["Dolphin"]["games"]["Space"]], "Dolphin's Space game is Rampage")
    expect(not rows["Dolphin"]["games"]["Mass"], "Dolphin has no Mass game")
    expect(rows["Dolphin"]["steps"]["Mass"]["1"][0] == "n/a", "Dolphin's Mass is excused, not missing")
    expect("Scurry" in [g["mode"] for g in rows["Squirrel"]["games"]["Mass"]], "Squirrel's Mass game is Scurry")
    expect(rows["Squirrel"]["arenas"]["Time"], "Regatta seats the Squirrel")
    expect(not rows["Rhino"]["arenas"]["Charge"], "Broadside does not seat the Rhino")
    expect("Charge" in rows["Rhino"]["missing"], "the Rhino has no Charge game")
    acts = actions(board)
    expect(acts and acts[0]["element"] == "Time", "the first action is on the Time element (element order)")
    # A recorded 'done' moves the step on.
    trial = set_step(json.loads(json.dumps(data)), "Dolphin", "Time", "1", "done", "test", "t", "human", 1, "2026-01-01")
    trial = set_step(trial, "Dolphin", "Time", "2", "done", "test", "t", "human", 1, "2026-01-01")
    expect(not validate(trial, vessels), "a human-signed step 1 then 2 validates")
    trial = set_step(trial, "Dolphin", "Time", "3", "done", "test", "t", "auto", 1, "2026-01-01")
    b2 = build(data=trial)
    d2 = {r["vessel"]: r for r in b2["rows"]}["Dolphin"]
    expect(d2["steps"]["Time"]["1"][0] == "done", "set marks a step done")
    expect(human_share(trial).get(1) == {"human": 2 + human_share(data).get(1, {}).get("human", 0),
                                         "auto": 1 + human_share(data).get(1, {}).get("auto", 0)},
           "human_share counts human and automatic sign-offs")
    expect(markdown(board).startswith("# The elemental loop"), "the board renders")
    # Rework: a late catch costs more than an early one, and a signed-off step costs double.
    early = rework(board, "Dolphin", ["Time"], "1")[0]
    late = rework(b2, "Dolphin", ["Time"], "1")[0]
    expect(late > early > 0, f"signing steps off raises what a late catch sends back ({early} -> {late})")
    expect(rework(board, "Dolphin", ["Mass"], "1") == (0, []), "an excused element sends nothing back")                                      # negative control
    expect(rework(board, "NoSuchVessel") == (0, []), "an unknown vessel sends nothing back")
    expect(rework(board, "Dolphin", ["Time"], "3")[0] < early, "a catch at step 3 spares steps 1-2")
    lanes = vessel_lanes(board)
    expect(set(lanes) == set(rows) and lanes["Dolphin"]["actions"][0]["vessel"] == "Dolphin",
           "every vessel is its own lane, carrying its own next actions")
    if failures:
        print("SELF-TEST FAILED:\n  " + "\n  ".join(failures))
        return 1
    print("self-test OK (validation, 6 negative controls, known answers from the cards, set, the board, rework, lanes)")
    return 0


# ---------------------------------------------------------------- main


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("command", nargs="?", default="board", choices=("board", "next", "vessel", "set"))
    p.add_argument("args", nargs="*")
    p.add_argument("--n", type=int, default=12)
    p.add_argument("--by", default="")
    p.add_argument("--evidence", default="")
    p.add_argument("--kind", choices=("human", "auto"), default="human")
    p.add_argument("--loop", type=int, default=None)
    p.add_argument("--write", action="store_true")
    p.add_argument("--check", action="store_true")
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args(argv)

    if a.self_test:
        return self_test()
    data = load()
    problems = validate(data, roster())
    if problems:
        print("loop.json is invalid:\n  " + "\n  ".join(problems))
        return 1

    if a.command == "set":
        if len(a.args) != 4 or not a.evidence or not a.by:
            print("usage: set <Vessel|ArenaMode> <Element> <step> <state> --by <who> --evidence '...'")
            return 2
        where, element, step, state = a.args
        data = set_step(data, where, element, step, state, a.by, a.evidence, a.kind,
                        a.loop or data.get("current_loop", 1))
        problems = validate(data, roster())
        if problems:
            print("not written:\n  " + "\n  ".join(problems))
            return 1
        save(data)
        with open(STATUS_MD, "w", encoding="utf-8") as fh:
            fh.write(markdown(build(data=data)))
        print(f"recorded {where} {element} step {step} = {state}; STATUS.md rewritten")
        return 0

    board = build(data=data)
    if a.check:
        want = markdown(board)
        have = _read(STATUS_MD) if os.path.exists(STATUS_MD) else ""
        if want != have:
            print("Docs/ElementalLoop/STATUS.md is stale: run python3 Tools/Build/elemental_loop.py --write")
            return 1
        print("elemental loop: loop.json valid, STATUS.md current")
        return 0
    if a.write:
        with open(STATUS_MD, "w", encoding="utf-8") as fh:
            fh.write(markdown(board))
        print(f"wrote {os.path.relpath(STATUS_MD, REPO)}")
        return 0
    if a.command == "next":
        for i, act in enumerate(actions(board)[: a.n], 1):
            blocked = f"  [blocked: {act['blocked']}]" if act["blocked"] else ""
            print(f"{i:>2}. [{act['who']}] {act['vessel']} {act['element']} step {act['step']} ({act['state']}): "
                  f"{act['text']}{blocked}")
        return 0
    if a.command == "vessel":
        name = (a.args or [""])[0]
        row = next((r for r in board["rows"] if r["vessel"].lower() == name.lower()), None)
        if not row:
            print(f"no vessel '{name}'. Roster: {', '.join(r['vessel'] for r in board['rows'])}")
            return 2
        print(f"{row['vessel']}  arenas: " + ", ".join(board['data']['arena_games'][k] for k, v in row['arenas'].items() if v))
        for e in ELEMENTS:
            games = ", ".join(g["name"] for g in row["games"][e]) or "-"
            print(f"  {e:<7} games: {games}")
            for s in VESSEL_STEPS:
                st, entry = row["steps"][e][s]
                ev = f"  ({entry['evidence']})" if entry else ""
                print(f"           {s} {STEP_NAMES[s]:<38} {st}{ev}")
        if row["missing"]:
            print(f"  kit missing: {', '.join(row['missing'])}")
        return 0
    print(board_text(board))
    print("\nnext: python3 Tools/Build/elemental_loop.py next")
    return 0


if __name__ == "__main__":
    sys.exit(main())

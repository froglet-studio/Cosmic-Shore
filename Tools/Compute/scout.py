"""The scout: what is worth the team's idle hardware next - `compute.py scout [--write] [--propose]`.

It reads the repo (the session's checkout) through LENSES, each a different way of noticing a search problem nobody
has queued. Every finding is an OPPORTUNITY with a value (0-10), a reason, and one of four kinds:

    job       mechanical: an evaluator for it exists; `--propose` writes the job file (status ready)
    build     needs session work first: an offline model, an evaluator, a port (status proposed when written)
    promote   a converged answer is waiting to reach the game (its owner decides)
    fix       a job is broken or starving

The lenses:

    coverage     every vessel x its Time (race) games x I1-I4: where people fly (the AI learns by imitation), where an
                 AI drive was learned, where an offline race model exists, and so where self-play is the ONLY way an AI
                 will ever learn that cell (an underplayed minigame or intensity)
    regatta      the three-stage Regatta balance plan (Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md): which stage is live,
                 what blocks the next
    bioforms     how many bodies a swarm can switch into, per element, and creatures trained but not in the game
    sweeps       hand-rolled searches already in Tools/Build (a `--sweep` grid, an `itertools.product`, a `tune`
                 command): a person already wrote the loss function - it only needs a queue
    labs         labs with a headless scorecard (runBatch): a scorecard IS a loss function
    by-hand      numbers the docs say were set by hand, by eye or reasoned-not-measured: someone's problem a search
                 could answer, though they may never have thought of it as one
    plans        every arena card is a mixed fleet to balance: is its plan (Regatta's, Broadside's in SILENCE.md 6) queued?
    board        converged-not-promoted, broken evaluators, top jobs no machine is on

Writes Docs/Compute/OPPORTUNITIES.md and Docs/Compute/opportunities.json (generated; never hand-edit).
"""
import glob
import json
import os
import re
import subprocess
import sys
import time

import compute_lib as L

REPO = L.REPO
OPP_MD = "Docs/Compute/OPPORTUNITIES.md"
OPP_JSON = "Docs/Compute/opportunities.json"

# The Time genre (ModeGenre.cs): a race. Metric numbers are ScoringMetric.cs's.
TIME_METRICS = {0, 1, 2, 4, 9}            # Crystals, OmniCrystals, ElementalCrystals, Goals, SwitchesThreaded
NOT_TIME_MODES = {"Scurry", "Tollway", "BroodRush", "Maelstrom"}   # ModeGenre's own rows (and Maelstrom: no genre)
REGATTA_SIX = ("Squirrel", "Stoat", "Manta", "Rhino", "Urchin", "Dolphin")
HELD_HULLS = {"Stoat": "its AI waits for Garrett's configuration (issue 1198): ask Garrett before building for it"}

# Offline race models a drive_fit job can use today: (hull, mode) -> (race model, steering, shipped drive asset or "").
MODELS = {
    ("Rhino", "Headlong"): ("rhino_drive", "Headlong", ""),
    ("Rhino", "Regatta"): ("rhino_drive", "Regatta", "Assets/Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta.asset"),
    ("Manta", "Redline"): ("manta_drive", "Redline", ""),
    ("Manta", "Regatta"): ("manta_drive", "Regatta", "Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Regatta.asset"),
    ("Dolphin", "Regatta"): ("dolphin_drive", "Regatta", ""),
}
# Models that exist but are not wired as compute evaluators yet (a `build` opportunity each).
MODEL_CANDIDATES = {
    ("Dolphin", "Switchback"): "Tools/Build/switchback_dolphin_sim.py (needs SwitchbackCourse ported like course_port.py; it races recorded courses today)",
    ("Squirrel", "SkimRace"): "Tools/Build/skimrace_sim_harness (C#, dotnet8; its `tuneall` is already a search: wrap it as a command job)",
    ("Scarab", "Regatta"): "Tools/Build/scarab_drive_sim.py (SnapDash drive; add it to race_models.py)",
}


def _read(rel):
    try:
        with open(os.path.join(REPO, rel), encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError:
        return ""


def _game_modes():
    out = {}
    for m in re.finditer(r"^\s+([A-Za-z0-9_]+)\s*=\s*(\d+)\s*,", _read("Assets/_Scripts/Data/Enums/GameModes.cs"), re.M):
        out[int(m.group(2))] = m.group(1)
    return out


def time_games():
    """{mode: metric} for every mode whose genre is Time, from the shipped preview definitions."""
    modes = _game_modes()
    out = {}
    for p in glob.glob(os.path.join(REPO, "Assets", "_SO_Assets", "Mode Previews", "ModePreview_*.asset")):
        t = open(p, encoding="utf-8", errors="replace").read()
        mm = re.search(r"^\s+Mode:\s*(\d+)", t, re.M)
        om = re.search(r"^\s+ObjectiveMetric:\s*(\d+)", t, re.M)
        if not mm or not om:
            continue
        mode = modes.get(int(mm.group(1)))
        if mode and int(om.group(1)) in TIME_METRICS and mode not in NOT_TIME_MODES:
            out[mode] = int(om.group(1))
    return out


def _learned_variants():
    names = [os.path.basename(p)[:-6] for p in glob.glob(os.path.join(REPO, "Assets", "Resources", "AIBoostPolicies", "*.asset"))]
    names += ["RaceLine:" + os.path.basename(p)[:-6] for p in glob.glob(os.path.join(REPO, "Assets", "Resources", "RaceLines", "*.asset"))]
    return names


def _holds():
    m = re.search(r"^HOLD\s*=\s*\{(.*?)^\}", _read("Tools/Build/ai_learn.py"), re.M | re.S)
    return set(re.findall(r'"([A-Za-z]+)"\s*:', m.group(1))) if m else set()


def _queued(jobs, **match):
    for j in jobs.values():
        a = (j.get("evaluator") or {}).get("args") or {}
        if all(a.get(k) == v for k, v in match.items()):
            return j["id"]
    return None


# ── lenses ───────────────────────────────────────────────────────────────────

def lens_coverage(jobs):
    chart = json.loads(_read("Docs/AI_SYSTEM/vessel_mode_chart.json") or "{}")
    games = time_games()
    hulls = {h["hull"]: h for h in chart.get("hulls", [])}
    variants = _learned_variants()
    holds = _holds()
    matrix, opps = [], []
    for card in chart.get("cards", []):
        mode = card["mode"]
        if mode not in games:
            continue
        for hull in card["hulls"]:
            flown = ((hulls.get(hull) or {}).get("flown") or {}).get(mode) or {}
            races = int(flown.get("races", 0))
            played = set(flown.get("intensities", []))
            learned = [v for v in variants if v.endswith("_" + mode) or re.search(rf"_{mode}_I\d$", v)]
            learned = [v for v in learned if v.startswith(hull) or v.startswith("RaceLine:" + hull)]
            model = MODELS.get((hull, mode))
            row = {"hull": hull, "mode": mode, "races": races, "played": sorted(played), "learned": learned,
                   "model": model[0] if model else None, "held": mode in holds,
                   "queued": _queued(jobs, hull=hull, mode=mode)}
            matrix.append(row)
            missing = [i for i in (1, 2, 3, 4) if i not in played]
            if row["held"] or hull in HELD_HULLS:
                continue
            if model and not row["queued"]:
                value = 5 + (2 if hull in REGATTA_SIX else 0) + (2 if not played else 1 if missing else 0)
                opps.append({"lens": "coverage", "kind": "job", "value": min(10, value),
                             "id": f"drive-{hull.lower()}-{mode.lower()}",
                             "title": f"{hull} AI drive for {mode}, I1-I4 (self-play)",
                             "why": (f"{races} human race(s), played at I{sorted(played)}; " if races else "nobody has raced it; ")
                                    + f"no person-learned drive at I{missing}" + ("" if missing else " - and a search checks the person-learned numbers"),
                             "spec": {"hull": hull, "mode": mode, "model": model[0], "steering": model[1], "shipped": model[2]}})
            elif not model and missing:
                cand = MODEL_CANDIDATES.get((hull, mode))
                opps.append({"lens": "coverage", "kind": "build", "value": 4 + (2 if hull in REGATTA_SIX else 0) + (1 if not played else 0),
                             "id": f"model-{hull.lower()}-{mode.lower()}",
                             "title": f"An offline race model for the {hull} in {mode}",
                             "why": f"{races} human race(s) (I{sorted(played) or '-'}); I{missing} have no race to imitate and no model to self-play. "
                                    + (f"Start from {cand}." if cand else "No model exists: write one the way rhino_drive_sim.py models the Rhino.")})
    return matrix, opps


def lens_regatta(jobs, board):
    opps = []
    stage = {1: [], 2: [], 3: []}
    for j in jobs.values():
        if j.get("plan") == "regatta-balance":
            stage.setdefault(int(j.get("stage", 0)), []).append(j)
    state = {}
    for s, js in stage.items():
        conv = [j for j in js if j["status"] in ("converged", "promoted") or _converged_on_board(board, j["id"])]
        state[s] = {"jobs": [j["id"] for j in js], "done": [j["id"] for j in conv]}
    if not stage[1]:
        opps.append({"lens": "regatta", "kind": "build", "value": 9, "id": "regatta-stage1-jobs",
                     "title": "Queue stage 1 of the Regatta plan", "why": "no stage-1 drive jobs exist"})
    live = next((s for s in (1, 2, 3) if stage[s] and len(state[s]["done"]) < len(stage[s])), None)
    chart = json.loads(_read("Docs/AI_SYSTEM/vessel_mode_chart.json") or "{}")
    games = time_games()
    own = {}
    for card in chart.get("cards", []):
        if card["mode"] in games and len(card["hulls"]) == 1:
            own.setdefault(card["hulls"][0], []).append(card["mode"])
    holds = _holds()
    for hull in REGATTA_SIX:
        for mode in own.get(hull, []):
            if (hull, mode) in MODELS or mode in holds:
                continue
            if hull in HELD_HULLS:
                opps.append({"lens": "regatta", "kind": "build", "value": 3, "id": f"model-{hull.lower()}-{mode.lower()}",
                             "title": f"(held) an offline race model for the {hull} in {mode}", "why": HELD_HULLS[hull]})
                continue
            cand = MODEL_CANDIDATES.get((hull, mode))
            opps.append({"lens": "regatta", "kind": "build", "value": 8, "id": f"model-{hull.lower()}-{mode.lower()}",
                         "title": f"Stage 1 needs an offline race model: the {hull} in {mode}, its own Time game",
                         "why": "the plan trains every Regatta hull's AI on its own Time game at I1-I4; "
                                + (f"start from {cand}" if cand else "no model exists yet (rhino_drive_sim.py is the pattern)")})
    state["own_time_games"] = own
    return state, live, opps


def _converged_on_board(board, jid):
    for j in (board or {}).get("jobs", []):
        if j["id"] == jid:
            return bool(j.get("converged"))
    return False


def lens_bioforms(jobs):
    opps = []
    plans = glob.glob(os.path.join(REPO, "Assets", "_SO_Assets", "Swarm Fauna", "**", "SwarmPlan_*.json"), recursive=True)
    travel = [p for p in plans if not re.search(r"_(feed|coil|wrap|eight|rear|strike|gape|snap)\.json$", p)]
    census = {"charge": 0, "mass": 0, "space": 0, "time": 0}
    for p in travel:
        try:
            d = json.load(open(p, encoding="utf-8"))
            census[["charge", "mass", "space", "time"][int(d.get("major", 1))]] += 1
        except Exception:
            pass
    forge = [p for p in travel if "forge" in os.path.basename(p)]
    if not any(((j.get("evaluator") or {}).get("module")) == "bioform_forms" for j in jobs.values()):
        opps.append({"lens": "bioforms", "kind": "job", "value": 7, "id": "bioform-forge",
                     "title": "Grow the swarm bestiary: MAP-Elites over body genomes",
                     "why": f"{len(travel)} travel bodies today (by majority element {census}), every one designed by hand; {len(forge)} forged"})
    thin = [e for e, n in census.items() if n <= 2]
    if thin:
        opps.append({"lens": "bioforms", "kind": "job", "value": 6, "id": "bioform-forge-" + "-".join(thin),
                     "title": "Forge bodies for the thin elements: " + ", ".join(thin),
                     "why": f"a swarm whose majority is {', '.join(thin)} has {min(census[e] for e in thin)} body to switch into"})
    species = re.findall(r'"name":\s*"([A-Za-z]+)"', _read("Tools/Build/author_nca_creatures.py"))
    research = L.git_ok("ls-tree", "--name-only", "origin/cece/gifted-curie-x2cpd0", "Tools/NCA/results/") or ""
    trained = sorted({m.group(1) for m in re.finditer(r"results/([a-z]+)3d_swim", research)})
    pending = [t for t in trained if t.capitalize() not in species and t != "lizard"]
    if pending:
        opps.append({"lens": "bioforms", "kind": "build", "value": 6, "id": "nca-port-" + "-".join(pending),
                     "title": "Trained NCA creatures not in the game yet: " + ", ".join(pending),
                     "why": "the research branch finished their training; author_nca_creatures.py SPECIES is where a row goes (Docs/NCA_CREATURES.md)"})
    opps.append({"lens": "bioforms", "kind": "job", "value": 5, "id": "nca-train-forged",
                 "title": "Train 3D NCA bodies for the best forged bioforms (torch)",
                 "why": "nca3d.py trains any prism target (--prisms); a forged elite exported as prisms.json is one more creature that grows, swims and heals"})
    return census, opps


SWEEP_PAT = re.compile(r"(def sweep\b|--sweep|--bands|itertools\.product\(|\btuneall\b|def _sweep_one)")


def lens_sweeps(jobs):
    opps = []
    covered = {((j.get("evaluator") or {}).get("args") or {}).get("model") for j in jobs.values()}
    for p in sorted(glob.glob(os.path.join(REPO, "Tools", "Build", "*.py")) + glob.glob(os.path.join(REPO, "Tools", "Build", "*", "run.sh"))):
        t = open(p, encoding="utf-8", errors="replace").read()
        hits = SWEEP_PAT.findall(t)
        if not hits:
            continue
        rel = os.path.relpath(p, REPO)
        name = os.path.basename(p)
        if name.replace("_sim.py", "_drive") in covered or name.startswith(("ai_learn", "who_works", "team_")):
            continue
        opps.append({"lens": "sweeps", "kind": "build", "value": 4, "id": "sweep-" + L.slug(name.split(".")[0]),
                     "title": f"{rel} already searches by hand ({', '.join(sorted(set(hits)))})",
                     "why": "its author wrote the loss; a grid on one machine becomes a search on every machine once it is an evaluator (/compute-job)"})
    return opps


def lens_labs():
    opps = []
    for p in sorted(glob.glob(os.path.join(REPO, "Docs", "Studios", "*.md"))):
        t = open(p, encoding="utf-8", errors="replace").read()
        if re.search(r"runBatch\(", t) and not p.endswith(("README.md", "_VISION.md", "_PLAN.md")):
            rel = os.path.relpath(p, REPO)
            opps.append({"lens": "labs", "kind": "build", "value": 3, "id": "lab-" + L.slug(os.path.basename(p)[:-3]),
                         "title": f"{rel}: its headless scorecard is a loss function",
                         "why": "a node evaluator around the lab's runBatch lets machines search the lab's dials (/compute-job, 'a lab as a loss')"})
    return opps


BY_HAND = re.compile(r"(set by hand|by eye|hand[- ]tuned|reasoned, not measured|not measured|eyeballed|a guess\b|guessed)", re.I)


def lens_by_hand(days=30):
    opps = []
    since = time.time() - days * 86400
    for p in glob.glob(os.path.join(REPO, "Docs", "**", "*.md"), recursive=True) + glob.glob(os.path.join(REPO, "Assets", "_Scripts", "**", "*.md"), recursive=True):
        if os.path.getmtime(p) < since and "AI_SYSTEM" not in p:
            continue
        rel = os.path.relpath(p, REPO)
        if rel.startswith("Docs/Compute/"):
            continue
        for n, line in enumerate(open(p, encoding="utf-8", errors="replace"), 1):
            if BY_HAND.search(line) and not re.search(r"not a guess|is not measured against", line, re.I):
                opps.append({"lens": "by-hand", "kind": "build", "value": 2, "id": f"byhand-{L.slug(rel)}-{n}",
                             "title": f"{rel}:{n}", "why": line.strip()[:220]})
    opps.sort(key=lambda o: o["title"])
    return opps[:40]


# A balance plan per arena game: train each hull's AI to a plateau, then level the hulls. Regatta (Time) is the first one
# on the queue; Garrett set the same shape for Charge in Docs/ElementalAbilitySystem/SILENCE.md section 6 (Broadside).
KNOWN_PLANS = {
    "Regatta": ("regatta-balance", "Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md"),
    "Broadside": ("combat-balance", "Docs/ElementalAbilitySystem/SILENCE.md section 6 (Garrett 2026-10-20)"),
}


def lens_plans(jobs):
    """Every arena card is a mixed fleet that has to be balanced; the plan shape is the same for each genre."""
    opps = []
    chart = json.loads(_read("Docs/AI_SYSTEM/vessel_mode_chart.json") or "{}")
    queued_plans = {j.get("plan") for j in jobs.values()}
    for card in chart.get("cards", []):
        if "ArenaGames" not in card.get("lists", []):
            continue
        plan, where = KNOWN_PLANS.get(card["mode"], (f"{card['mode'].lower()}-balance", None))
        if plan in queued_plans:
            continue
        if where:
            opps.append({"lens": "plans", "kind": "build", "value": 7, "id": f"plan-{plan}",
                         "title": f"Put the {card['mode']} balance plan on the queue ({where})",
                         "why": f"a plan already set for {len(card['hulls'])} hulls, with no compute job yet: it needs an offline "
                                "model of the mode's score (petals per minute in mirror and mixed matches) - /compute-job"})
        else:
            opps.append({"lens": "plans", "kind": "build", "value": 4, "id": f"plan-{plan}",
                         "title": f"A balance plan for {card['mode']} ({len(card['hulls'])} hulls), the Regatta way",
                         "why": "every arena card is a mixed fleet: train each hull's AI to a plateau, search what levels them, "
                                "then the smallest elemental tuning (/regatta-balance is the worked example); nobody has proposed one"})
    return opps


def lens_board(board):
    opps = []
    for j in (board or {}).get("jobs", []):
        if j.get("converged") and j["status"] == "ready":
            opps.append({"lens": "board", "kind": "promote", "value": 8, "id": "promote-" + j["id"],
                         "title": f"{j['id']} converged: promote it", "why": f"owner: {j.get('owner') or '-'}; `compute.py promote {j['id']}`"})
        if j.get("best") is None and j["evals"] > 200:
            opps.append({"lens": "board", "kind": "fix", "value": 7, "id": "fix-" + j["id"],
                         "title": f"{j['id']}: {j['evals']} evals and no valid result", "why": "its evaluator fails on every point: read the machines' summary errors"})
    if board and board.get("jobs"):
        top = board["jobs"][0]
        if not top.get("blocked") and not [m for m in board.get("machines", []) if m.get("active")]:
            opps.append({"lens": "board", "kind": "fix", "value": 6, "id": "no-machines",
                         "title": "No machine is working", "why": f"the top job ({top['id']}) waits; /compute sets a machine up in two commands"})
    return opps


def run(args):
    jobs = L.load_jobs(local=True)
    board = L.read_json(os.path.join(REPO, "Docs", "Compute", "board.json"), {})
    opps, report, errors = [], {}, []
    for name, fn in (("coverage", lambda: lens_coverage(jobs)), ("regatta", lambda: lens_regatta(jobs, board)),
                     ("bioforms", lambda: lens_bioforms(jobs)), ("sweeps", lambda: lens_sweeps(jobs)),
                     ("labs", lens_labs), ("by-hand", lens_by_hand), ("plans", lambda: lens_plans(jobs)),
                     ("board", lambda: lens_board(board))):
        try:
            r = fn()
            if isinstance(r, tuple):
                report[name] = r[:-1]
                opps += r[-1]
            else:
                opps += r
        except Exception as e:
            errors.append(f"lens {name}: {type(e).__name__}: {e}")
    queued = set(jobs)
    opps = [o for o in opps if o["id"] not in queued]
    opps.sort(key=lambda o: (-o["value"], o["lens"], o["id"]))
    md = markdown(opps, report, errors)
    if args.write:
        with open(os.path.join(REPO, OPP_MD), "w", encoding="utf-8") as fh:
            fh.write(md)
        L.write_json(os.path.join(REPO, OPP_JSON), {"generated": L.now(), "opportunities": opps, "errors": errors,
                                                     "coverage": report.get("coverage", [[]])[0]})
        print(f"wrote {OPP_MD} ({len(opps)} opportunities)")
    else:
        print(md)
    if args.propose:
        for o in opps:
            if o["kind"] == "job" and o.get("spec"):
                path = propose_drive_job(o)
                if path:
                    print("proposed " + os.path.relpath(path, REPO))
    return 1 if errors else 0


def markdown(opps, report, errors):
    lines = ["# Compute opportunities", "",
             "GENERATED by `python3 Tools/Compute/compute.py scout --write` (the lenses: `Tools/Compute/scout.py`). "
             "Do not edit by hand. A `job` is mechanical (its evaluator exists: `scout --propose` queues it); a `build` "
             "needs a session first (an offline model, an evaluator); `promote` waits on an owner. Skill: `/compute-scout`.", ""]
    if errors:
        lines += ["**Lens errors:** " + "; ".join(errors), ""]
    lines += ["| Value | Kind | Lens | Opportunity | Why |", "|---:|---|---|---|---|"]
    for o in opps:
        lines.append(f"| {o['value']} | {o['kind']} | {o['lens']} | `{o['id']}` {o['title']} | {o['why']} |")
    cov = (report.get("coverage") or [None])[0]
    if cov:
        lines += ["", "## Coverage: every vessel's Time games, I1-I4", "",
                  "People fly a cell -> the hourly sweep learns the AI by imitation (AUTO_LEARNING.md). Nobody flies it -> "
                  "self-play on an offline model is the only teacher. `-` = no human race at that intensity.", "",
                  "| Hull | Time game | Human races | I1 | I2 | I3 | I4 | Learned AI variants | Offline model | Queued |",
                  "|---|---|---:|---|---|---|---|---|---|---|"]
        for r in sorted(cov, key=lambda r: (r["hull"], r["mode"])):
            cells = ["flown" if i in r["played"] else "-" for i in (1, 2, 3, 4)]
            lines.append(f"| {r['hull']} | {r['mode']}{' (held)' if r['held'] else ''} | {r['races']} | " + " | ".join(cells)
                         + f" | {', '.join(r['learned']) or '-'} | {r['model'] or '-'} | {r['queued'] or '-'} |")
    reg = report.get("regatta")
    if reg:
        state, live = reg
        lines += ["", "## The Regatta balance plan", "", f"Live stage: **{live or '-'}**.", ""]
        for s in (1, 2, 3):
            st = state.get(s) or {"jobs": [], "done": []}
            lines.append(f"- stage {s}: {len(st['done'])}/{len(st['jobs'])} jobs converged ({', '.join(st['jobs']) or 'none queued'})")
    bio = report.get("bioforms")
    if bio:
        lines += ["", "## Bioforms", "", "Travel bodies a swarm can switch into, by majority element: " + json.dumps(bio[0])]
    return "\n".join(lines) + "\n"


def propose_drive_job(o):
    """Write a drive_fit job for a coverage opportunity (status ready: the evaluator is a reviewed kind, and its result
    is only ever a proposal to the AI owner)."""
    s = o["spec"]
    sys.path.insert(0, os.path.join(L.HERE, "evaluators"))
    import race_models as rm
    shipped = rm.read_asset_fields(REPO, s["shipped"]) if s.get("shipped") else {}
    space = drive_space(s["model"], shipped)
    job = {"schema": L.SCHEMA, "id": o["id"], "title": o["title"], "goal": "Stage 1 of the Regatta balance plan: an AI that "
           f"races the {s['hull']}'s own Time game well at every intensity, learned where people do not play.",
           "asked_by": "the scout (coverage lens)", "area_owner": "Yash (vessel AI)", "plan": "regatta-balance", "stage": 1,
           "kind": "search", "status": "ready", "value": o["value"], "updated": time.strftime("%Y-%m-%d"),
           "ref": "origin/Ys-bleeding-edge", "requires": ["python3", "numpy"], "after": [],
           "evaluator": {"module": "drive_fit", "args": {"hull": s["hull"], "model": s["model"], "mode": s["steering"],
                                                         "intensities": [1, 2, 3, 4], "seeds": [1, 2, 3],
                                                         "shipped": s.get("shipped", ""), "limit": 400}},
           "space": space, "algorithm": {"name": "sep-cmaes", "sigma0": 0.2},
           "convergence": {"min_evals": 1500, "window_evals": 1200, "min_improvement": 0.002},
           "promote": "A PROPOSAL for the AI owner (Yash): if they agree, write the best fields into "
                      f"Assets/Resources/AIBoostPolicies/<the {s['hull']}'s policy>_{s['mode']}.asset with the learner's writer "
                      "(learn_drives_from_flights.py), never by hand; the AI learning sweep keeps imitating people on top of it."}
    path = os.path.join(REPO, L.JOBS_DIR, o["id"] + ".json")
    if os.path.exists(path):
        return None
    L.write_json(path, job)
    return path


DRIVE_SPACES = {
    "rhino_drive": [("cornerSpeed", 150, 1500, 600), ("minCornerSpeed", 60, 400, 160), ("brakeMarginUnits", 0, 400, 120),
                    ("brakeLeadSeconds", 0.0, 1.0, 0.25), ("minSpeedGain", 0, 300, 50), ("cornerReleaseDegrees", 10, 120, 30),
                    ("engageRunwaySeconds", 0.2, 3.0, 1.5), ("releaseRunwaySeconds", 0.1, 2.0, 0.5),
                    ("alignDegrees", 4, 40, 12), ("releaseAlignDegrees", 8, 60, 25), ("stickBand", 0.1, 0.9, 0.35),
                    ("releaseStickBand", 0.2, 1.0, 0.6), ("throttleSlewPerSecond", 0.5, 10, 4), ("brakeSteerFloor", 0.0, 0.8, 0.3)],
    "manta_drive": [("BoostStickBand", 0.05, 1.0, 0.35), ("YastriStickBand", 0.3, 1.0, 0.75)],
    "dolphin_drive": [("alignDegrees", 4, 45, 12), ("engageRunwaySeconds", 0.2, 3.5, 2.5), ("spendAtCharge01", 0.3, 1.0, 0.95),
                      ("maxHoldOffSeconds", 0.5, 8.0, 4.0), ("peakMarginUnits", 0.0, 30.0, 3.0),
                      ("minDischargeSeconds", 0.05, 1.0, 0.2), ("minPulseGainUnits", 0.0, 120.0, 30.0)],
}


def drive_space(model, shipped):
    return [{"name": n, "lo": lo, "hi": hi, "init": min(hi, max(lo, shipped.get(n, init)))} for n, lo, hi, init in DRIVE_SPACES[model]]

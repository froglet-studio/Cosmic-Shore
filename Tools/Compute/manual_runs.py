"""Manual runs: one person's instructions for one search - `compute.py request ...` and `compute.py run --request`.
(Not named requests.py: that would shadow the HTTP library for anything on this path.)

A request names an existing search job and says how to run it differently, within what the job already allows:

    {"schema": "froglet-compute/request/1", "id": "r20261028-1203-ab12", "job": "drive-rhino-regatta",
     "by": "shombith", "created": 1793189000, "note": "Try the Rhino with a later brake at every intensity",
     "budget": {"tries": 800},                         how many tries in total, across every computer that works it
     "fix": {"brakeLeadSeconds": 0.4},                 parameters held at a value (inside the job's bounds)
     "ranges": {"cornerSpeed": [900, 1300]},           parameters searched in a narrower range (inside the bounds)
     "args": {"seeds": 5},                             evaluator settings the job already has, set differently
     "machine": "any" | "<user>/<host>",               who may run it
     "start": "free" | "now",                         when: once that computer's person is away (the default), or at
                                                      once - only on a computer whose owner allows remote starts
     "not_before": 1793230800,                        a scheduled run: nothing starts it before this time
     "cancelled": false}                              stopped from the console: a running session stops within 30 s

Where requests live: Docs/Compute/requests/<id>.json on the `compute-queue` branch (data only, never code; the same
trust rules as every job: the code a request runs is its job's, from bleeding-edge or ai). `request new --push`
files one there with git plumbing, without touching the checkout.

How they run: every worker looks at open requests before its automatic queue, takes the oldest it may run (`machine`
is any or this computer), claims it in its heartbeat, and works it under its own presence rules (idle, --between).
`compute.py run --request <id|token>` runs one NOW on this computer, without waiting for idle: that is the manual
run. A request's results go to the machine's results branch as requests/<id>.json and jobs/<job>~<id>/..., so they
never mix with the job's own search. The Compute Console shows every request, its state and its best.

A token is the request as base64url JSON, so the console can hand one to a terminal in one copyable word.
"""
import base64
import copy
import json
import os
import re
import secrets
import time

import compute_lib as L

SCHEMA = "froglet-compute/request/1"
DIR = "Docs/Compute/requests"
BRANCH = "compute-queue"
MAX_TRIES = 200000
ID = re.compile(r"r\d{8}-\d{4,6}-[0-9a-f]{4}")
MACHINE = re.compile(r"any|[a-z0-9-]+/[a-z0-9-]+")


def new_id():
    return "r" + time.strftime("%Y%m%d-%H%M%S") + "-" + secrets.token_hex(2)


def token(req):
    return base64.urlsafe_b64encode(json.dumps(req, separators=(",", ":"), sort_keys=True).encode()).decode().rstrip("=")


def from_token(text):
    text = text.strip()
    return json.loads(base64.urlsafe_b64decode(text + "=" * (-len(text) % 4)).decode())


def _space(job):
    return {d["name"]: d for d in job.get("space") or []}


def _within(d, v):
    t = d.get("type", "float")
    if t == "choice":
        return v in (d.get("choices") or [])
    return isinstance(v, (int, float)) and not isinstance(v, bool) and d["lo"] <= v <= d["hi"]


def problems(req, job):
    """Why `req` cannot run, or []. Everything a request may change is checked against its job, so a request can
    only steer a search the team already trusts, never add code or new evaluator settings."""
    p = []
    if req.get("schema") != SCHEMA:
        p.append(f"schema {req.get('schema')} (want {SCHEMA})")
    if not ID.fullmatch(str(req.get("id", ""))):
        p.append("id must look like r20261028-120300-ab12")
    if job is None:
        return p + [f"no search named {req.get('job')}"]
    p += _when_problems(req)
    if job.get("kind") == "command":
        return p + _command_problems(req, job)
    if job.get("kind") != "search":
        p.append("only search and command jobs take manual runs")
    tries = (req.get("budget") or {}).get("tries")
    if not isinstance(tries, int) or isinstance(tries, bool) or not 1 <= tries <= MAX_TRIES:
        p.append(f"budget.tries must be a whole number 1-{MAX_TRIES}")
    space = _space(job)
    for name, v in (req.get("fix") or {}).items():
        if name not in space:
            p.append(f"fix: {name} is not one of this search's settings")
        elif not _within(space[name], v):
            p.append(f"fix: {name}={v} is outside what the search allows")
    for name, r in (req.get("ranges") or {}).items():
        d = space.get(name)
        if d is None:
            p.append(f"ranges: {name} is not one of this search's settings")
        elif name in (req.get("fix") or {}):
            p.append(f"ranges: {name} is also fixed")
        elif d.get("type", "float") == "choice" or not (isinstance(r, list) and len(r) == 2 and _within(d, r[0])
                                                       and _within(d, r[1]) and r[0] < r[1]):
            p.append(f"ranges: {name} needs [low, high] inside {d.get('lo')}..{d.get('hi')}")
    if len(req.get("fix") or {}) >= len(space):
        p.append("fix: leave at least one setting to search")
    allowed = request_args(job)
    for k, v in (req.get("args") or {}).items():
        rule = allowed.get(k)
        if rule is None:
            p.append(f"args: {k} cannot be changed in a manual run of this search")
        elif not _plain(v) or not _fits(rule, v):
            p.append(f"args: {k}={json.dumps(v)} is not allowed ({_describe(rule)})")
    if not MACHINE.fullmatch(str(req.get("machine", "any"))):
        p.append("machine must be any or <person>/<computer>")
    if len(str(req.get("note", ""))) > 2000:
        p.append("note: at most 2000 characters")
    return p


def _when_problems(req):
    p = []
    if req.get("start", "free") not in ("free", "now"):
        p.append("start must be free or now")
    nb = req.get("not_before")
    if nb is not None and (not isinstance(nb, (int, float)) or isinstance(nb, bool) or nb < 0):
        p.append("not_before must be a time (unix seconds)")
    return p


def _command_problems(req, job):
    """A command job takes a manual run only when it declares `command.pairs` (what may be played) and
    `command.request` (the intensities and match counts allowed): the unity-train job. The request picks one pair and
    one intensity, and its budget is the number of matches; nothing else about the command can change."""
    p, c = [], job.get("command") or {}
    pairs, rule = c.get("pairs") or {}, c.get("request") or {}
    if not pairs:
        return ["this job takes no manual runs (it declares no command.pairs)"]
    if req.get("fix") or req.get("ranges"):
        p.append("a run in the game has no settings to hold or narrow (only mode, vessel, intensity, matches)")
    a = req.get("args") or {}
    extra = sorted(set(a) - {"mode", "vessel", "intensity"})
    if extra:
        p.append("args: only mode, vessel and intensity can be set (" + ", ".join(extra) + " cannot)")
    pair = pairs.get(a.get("mode"))
    if pair is None:
        p.append(f"args: mode must be one of {', '.join(sorted(pairs))}")
    elif a.get("vessel", pair["vessels"][0]) not in pair["vessels"]:
        p.append(f"args: {pair.get('name', a.get('mode'))} is played by {', '.join(pair['vessels'])}")
    if a.get("intensity", 4) not in (rule.get("intensity") or [4]):
        p.append(f"args: intensity must be one of {rule.get('intensity') or [4]}")
    ep = rule.get("episodes") or {"lo": 1, "hi": 2000}
    tries = (req.get("budget") or {}).get("tries")
    if not isinstance(tries, int) or isinstance(tries, bool) or not ep["lo"] <= tries <= ep["hi"]:
        p.append(f"budget.tries (matches) must be a whole number {ep['lo']}-{ep['hi']}")
    if not MACHINE.fullmatch(str(req.get("machine", "any"))):
        p.append("machine must be any or <person>/<computer>")
    if len(str(req.get("note", ""))) > 2000:
        p.append("note: at most 2000 characters")
    return p


def request_args(job):
    """{arg: rule} a manual run of `job` may change: its evaluator's REQUEST_ARGS (nothing when it declares none)."""
    try:
        import importlib
        return dict(getattr(importlib.import_module("evaluators." + job["evaluator"]["module"]), "REQUEST_ARGS", {}))
    except Exception:
        return {}


def _fits(rule, v):
    if "choices" in rule:
        return v in rule["choices"]
    if "subset" in rule:
        return isinstance(v, list) and 0 < len(v) and all(x in rule["subset"] for x in v)
    return isinstance(v, (int, float)) and not isinstance(v, bool) and rule.get("lo", v) <= v <= rule.get("hi", v)


def _describe(rule):
    if "choices" in rule:
        return "one of " + ", ".join(map(str, rule["choices"]))
    if "subset" in rule:
        return "a list from " + ", ".join(map(str, rule["subset"]))
    return f"{rule.get('lo')}-{rule.get('hi')}"


def _plain(v):
    if isinstance(v, (str, int, float, bool)) or v is None:
        return True
    return isinstance(v, list) and len(v) <= 64 and all(isinstance(x, (str, int, float, bool)) for x in v)


def derive(job, req):
    """The job a request runs: its own id (<job>~<request>), the narrower space, the fixed values, the settings."""
    if job.get("kind") == "command":
        return _derive_command(job, req)
    j = copy.deepcopy(job)
    fix = dict(req.get("fix") or {})
    space = []
    for d in j.get("space") or []:
        if d["name"] in fix:
            continue
        r = (req.get("ranges") or {}).get(d["name"])
        if r:
            d["lo"], d["hi"] = r[0], r[1]
            if d.get("init") is not None:
                d["init"] = min(max(d["init"], r[0]), r[1])
        space.append(d)
    j["fixed_init"] = {d["name"]: d.get("init") for d in job.get("space") or [] if d["name"] in fix}
    j["space_full"] = copy.deepcopy(job.get("space") or [])   # the evaluator scores as for the job itself
    j["space"], j["fixed"] = space, fix
    j["evaluator"] = dict(j.get("evaluator") or {}, args=dict((j.get("evaluator") or {}).get("args") or {}, **(req.get("args") or {})))
    old_mode = ((job.get("evaluator") or {}).get("args") or {}).get("mode")
    new_mode = (req.get("args") or {}).get("mode")
    if new_mode and new_mode != old_mode and "shipped" in j["evaluator"]["args"]:
        # Another course: today's values are the job's own (its `init`), not another mode's shipped asset.
        j["evaluator"]["args"]["shipped"] = ""
    j["id"], j["request"], j["status"] = f"{job['id']}~{req['id']}", req, "ready"
    hull = j["evaluator"]["args"].get("hull")
    j["title"] = "Manual run: " + (f"{hull} in {new_mode}" if hull and new_mode else (job.get("title") or job["id"]))
    j.pop("convergence", None)
    return j


def _derive_command(job, req):
    """One unit: the pair, the intensity, the matches asked for."""
    j, a = copy.deepcopy(job), req.get("args") or {}
    pair = j["command"]["pairs"][a["mode"]]
    vessel, intensity = a.get("vessel") or pair["vessels"][0], int(a.get("intensity", 4))
    unit = {"id": f"{a['mode'].lower()}-{vessel.lower()}-i{intensity}",
            "args": {"mode": pair["token"], "vessel": vessel, "intensity": intensity, "episodes": int(req["budget"]["tries"])}}
    j["command"]["units"] = [unit]
    j["id"], j["request"], j["status"] = f"{job['id']}~{req['id']}", req, "ready"
    j["title"] = f"Manual run: {vessel} in {pair.get('name', a['mode'])}, I{intensity} (in the game)"
    return j


def load(refs=None, local=False, cwd=None):
    """Every request on the queue refs (or this checkout's Docs/Compute/requests with `local`)."""
    out = {}
    if local:
        d = os.path.join(cwd or L.REPO, DIR)
        for name in sorted(os.listdir(d)) if os.path.isdir(d) else []:
            if name.endswith(".json"):
                r = L.read_json(os.path.join(d, name))
                if isinstance(r, dict) and r.get("id"):
                    out[r["id"]] = r
        return out
    for ref in refs or L.QUEUE_REFS:
        listing = L.git_ok("ls-tree", "--name-only", f"{ref}:{DIR}", cwd=cwd) or ""
        for name in listing.split():
            if name.endswith(".json"):
                try:
                    r = json.loads(L.git("show", f"{ref}:{DIR}/{name}", cwd=cwd))
                except Exception:
                    continue
                if isinstance(r, dict) and r.get("id"):
                    r["_ref"] = ref
                    out[r["id"]] = r
    return out


def progress(req, machines, now=None):
    """{state: open|running|done|cancelled, tries, best, machines, running_on, updated}: what every computer has
    published about this request."""
    now = now or L.now()
    tries, best, who, updated, running, done_tries = 0, None, [], 0, None, 0
    for m in machines:
        r = (m.get("requests") or {}).get(req["id"])
        if r:
            tries += int(r.get("tries", 0))
            done_tries += int(r.get("tries", 0))
            who.append(f"{m['user']}/{m['host']}")
            updated = max(updated, int(r.get("updated", 0)))
            if L.finite(r.get("best")) and (best is None or r["best"] < best):
                best = r["best"]
        hb = m.get("heartbeat") or {}
        cur = hb.get("current") or {}
        if cur.get("request") == req["id"] and now - int(hb.get("updated", 0) or 0) < 3 * 3600:
            running = f"{m['user']}/{m['host']}"
            tries += int(cur.get("evals") or 0)
            if L.finite(cur.get("best")) and (best is None or cur["best"] < best):
                best = cur["best"]
    budget = int((req.get("budget") or {}).get("tries", 0))
    state = "cancelled" if req.get("cancelled") else ("done" if tries >= budget > 0 else ("running" if running else "open"))
    return {"state": state, "tries": tries, "done_tries": done_tries, "budget": budget, "best": best, "machines": who,
            "running_on": running, "updated": updated}


def pick(requests, jobs, machines, me, caps):
    """The oldest open request this computer may run now, as (request, derived job), or None."""
    for req in sorted(requests.values(), key=lambda r: r.get("created", 0)):
        st = progress(req, machines)
        if st["state"] != "open" or req.get("machine", "any") not in ("any", me):
            continue
        if int(req.get("not_before") or 0) > L.now():          # scheduled for later
            continue
        job = jobs.get(req.get("job"))
        if problems(req, job) or L.untrusted(job) or L.missing_requirements(job, caps):
            continue
        return req, derive(job, req)
    return None


def file(req, push=True, cwd=None, quiet=False):
    """Write Docs/Compute/requests/<id>.json onto the compute-queue branch (plumbing: the checkout is not touched)."""
    body = json.dumps({k: v for k, v in req.items() if not k.startswith("_")}, indent=1, sort_keys=True) + "\n"
    return L.commit_files(BRANCH, {f"{DIR}/{req['id']}.json": body}, f"compute: request {req['id']} ({req['job']})",
                          push=push, cwd=cwd, quiet=quiet)


def cancel(req_id, refs=None, push=True, cwd=None, quiet=False):
    """Stop a request from the console or a terminal: marked cancelled on compute-queue; a computer working on it
    stops within about 30 s (agent.StopWhenCancelled). Returns the request, or None when there is no such request."""
    r = load(refs, cwd=cwd).get(req_id)
    if not r:
        return None
    r = {k: v for k, v in r.items() if not k.startswith("_")}
    r["cancelled"], r["cancelled_at"] = True, L.now()
    return r if file(r, push=push, cwd=cwd, quiet=quiet) else None


def parse_value(text):
    try:
        return json.loads(text)
    except Exception:
        return text


def build(job_id, tries, fix=(), ranges=(), args=(), machine="any", note="", by=None, start=None, not_before=None):
    req = {"schema": SCHEMA, "id": new_id(), "job": job_id, "by": by or L.identity()[0], "created": L.now(),
           "note": note or "", "budget": {"tries": int(tries)}, "machine": machine or "any"}
    if start:
        req["start"] = start
    if not_before:
        req["not_before"] = int(not_before)
    if fix:
        req["fix"] = {k: parse_value(v) for k, v in (s.split("=", 1) for s in fix)}
    if ranges:
        req["ranges"] = {k: [parse_value(a) for a in v.split(":", 1)] for k, v in (s.split("=", 1) for s in ranges)}
    if args:
        req["args"] = {k: parse_value(v) for k, v in (s.split("=", 1) for s in args)}
    return req


def self_test():
    """problems() refuses everything outside a job, derive() narrows and fixes, a token round-trips."""
    bad = []
    job = {"id": "toy", "kind": "search", "evaluator": {"module": "toy", "args": {"target": 1.5}},
           "space": [{"name": "a", "lo": 0.0, "hi": 10.0, "init": 5.0}, {"name": "b", "lo": -1.0, "hi": 1.0, "init": 0.0},
                     {"name": "c", "type": "choice", "choices": ["x", "y"], "init": "x"}]}
    ok = build("toy", 100, fix=["c=\"y\""], ranges=["a=2:4"], args=["target=2"])
    if problems(ok, job):
        bad.append(f"requests: a valid request was refused: {problems(ok, job)}")
    for what, r in [("a value outside the bounds", build("toy", 100, fix=["a=11"])),
                    ("a setting the search does not have", build("toy", 100, fix=["z=1"])),
                    ("a new evaluator setting", build("toy", 100, args=["module=evil"])),
                    ("an evaluator setting out of its range", build("toy", 100, args=["target=50"])),
                    ("a reversed range", build("toy", 100, ranges=["a=4:2"])),
                    ("no budget", dict(build("toy", 100), budget={"tries": 0})),
                    ("every setting fixed", build("toy", 100, fix=["a=1", "b=0", "c=\"x\""])),
                    ("a bad machine", build("toy", 100, machine="../../etc"))]:
        if not problems(r, job):
            bad.append(f"requests: {what} was accepted")
    d = derive(job, ok)
    if [s["name"] for s in d["space"]] != ["a", "b"] or d["space"][0]["lo"] != 2 or d["space"][0]["init"] != 4:
        bad.append(f"requests: derive did not narrow and fix: {d['space']}")
    if d["fixed"] != {"c": "y"} or d["evaluator"]["args"]["target"] != 2 or d["id"] != f"toy~{ok['id']}":
        bad.append("requests: derive lost the fixed values, the settings or the id")
    if from_token(token(ok)) != ok:
        bad.append("requests: a token does not round-trip")
    # A run in the game (a command job with pairs): one pair, one intensity, a number of matches, nothing else.
    game = {"id": "unity-train", "kind": "command", "command": {
        "argv": ["x"], "units": [], "request": {"intensity": [1, 2, 3, 4], "episodes": {"lo": 5, "hi": 2000}},
        "pairs": {"AstroLeague": {"token": "AstroLeague", "name": "Astro League", "vessels": ["Rhino", "Scarab"]},
                  "Joust": {"token": "Joust", "name": "Joust", "vessels": ["Squirrel"], "locked": True}}}}
    g = build("unity-train", 40, args=["mode=\"AstroLeague\"", "vessel=\"Scarab\"", "intensity=2"])
    if problems(g, game):
        bad.append(f"requests: a valid run in the game was refused: {problems(g, game)}")
    for what, r in [("a hull the mode does not seat", build("unity-train", 40, args=["mode=\"Joust\"", "vessel=\"Rhino\""])),
                    ("a mode without a trainer scenario", build("unity-train", 40, args=["mode=\"Regatta\""])),
                    ("intensity 7", build("unity-train", 40, args=["mode=\"Joust\"", "intensity=7"])),
                    ("a held setting", build("unity-train", 40, fix=["a=1"], args=["mode=\"Joust\""])),
                    ("another argument", build("unity-train", 40, args=["mode=\"Joust\"", "argv=\"rm\""])),
                    ("too few matches", build("unity-train", 2, args=["mode=\"Joust\""]))]:
        if not problems(r, game):
            bad.append(f"requests: {what} was accepted for a run in the game")
    for what, r in [("an unknown start", dict(g, start="whenever")), ("a negative schedule", dict(g, not_before=-5))]:
        if not problems(r, game):
            bad.append(f"requests: {what} was accepted")
    if problems(dict(g, start="now", not_before=1793230800), game):
        bad.append("requests: a scheduled start-now run was refused")
    later = dict(g, id="r20990101-000000-abcd", not_before=L.now() + 3600)
    if pick({later["id"]: later}, {"unity-train": game}, [], "u/h", {}) is not None:
        bad.append("requests: a run scheduled for later was picked now")
    dg = derive(game, g)
    u = dg["command"]["units"]
    if len(u) != 1 or u[0]["args"] != {"mode": "AstroLeague", "vessel": "Scarab", "intensity": 2, "episodes": 40} \
            or dg["id"] != f"unity-train~{g['id']}":
        bad.append(f"requests: derive for a run in the game made {u}")
    return bad

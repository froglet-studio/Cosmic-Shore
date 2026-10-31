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
ID_ITEM = re.compile(r"[A-Za-z0-9_-]{6,64}")
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
    one intensity or a list of them (one unit per level, the budget is the matches PER level), and `watch: true` opens
    Unity with its window on the computer that runs it (Shombith, 2026-11-01: "show the game"); nothing else about
    the command can change."""
    p, c = [], job.get("command") or {}
    pairs, rule = c.get("pairs") or {}, c.get("request") or {}
    if not pairs:
        return ["this job takes no manual runs (it declares no command.pairs)"]
    if req.get("fix") or req.get("ranges"):
        p.append("a run in the game has no settings to hold or narrow (only mode, vessel, intensity, matches)")
    a = req.get("args") or {}
    extra = sorted(set(a) - {"mode", "vessel", "intensity", "watch"})
    if extra:
        p.append("args: only mode, vessel, intensity and watch can be set (" + ", ".join(extra) + " cannot)")
    if "watch" in a and not isinstance(a["watch"], bool):
        p.append("args: watch must be true or false")
    pair = pairs.get(a.get("mode"))
    if pair is None:
        p.append(f"args: mode must be one of {', '.join(sorted(pairs))}")
    elif a.get("vessel", pair["vessels"][0]) not in pair["vessels"]:
        p.append(f"args: {pair.get('name', a.get('mode'))} is played by {', '.join(pair['vessels'])}")
    allowed = rule.get("intensity") or [4]
    levels = a.get("intensity", 4)
    levels = levels if isinstance(levels, list) else [levels]
    if not levels or any(x not in allowed for x in levels) or len(set(map(str, levels))) != len(levels):
        p.append(f"args: intensity must be one of {allowed}, or a list of them with no repeats")
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


def intensities(req):
    """The levels a Unity request plays, in order: one per unit."""
    x = ((req or {}).get("args") or {}).get("intensity", 4)
    return [int(v) for v in (x if isinstance(x, list) else [x])] or [4]


def intensity_text(req):
    lv = intensities(req)
    if len(lv) == 1:
        return f"I{lv[0]}"
    return f"I{lv[0]}-{lv[-1]}" if lv == list(range(lv[0], lv[-1] + 1)) else "I" + ", I".join(map(str, lv))


def _derive_command(job, req):
    """One unit per intensity: the pair, the level, the matches asked for (per level), and whether the game is shown."""
    j, a = copy.deepcopy(job), req.get("args") or {}
    pair = j["command"]["pairs"][a["mode"]]
    vessel = a.get("vessel") or pair["vessels"][0]
    units = []
    for intensity in intensities(req):
        args = {"mode": pair["token"], "vessel": vessel, "intensity": intensity, "episodes": int(req["budget"]["tries"])}
        if a.get("watch") is True:
            args["watch"] = True
        units.append({"id": f"{a['mode'].lower()}-{vessel.lower()}-i{intensity}", "args": args})
    j["command"]["units"] = units
    j["id"], j["request"], j["status"] = f"{job['id']}~{req['id']}", req, "ready"
    j["title"] = (f"Manual run: {vessel} in {pair.get('name', a['mode'])}, {intensity_text(req)} (in the game"
                  + (", shown on its screen)" if a.get("watch") is True else ")"))
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
    # A Unity request at several intensities is one unit per level, each with the budget's matches: done when all are.
    budget = int((req.get("budget") or {}).get("tries", 0)) * (len(intensities(req)) if (req.get("args") or {}).get("mode") else 1)
    state = "cancelled" if req.get("cancelled") else ("done" if tries >= budget > 0 else ("running" if running else "open"))
    return {"state": state, "tries": tries, "done_tries": done_tries, "budget": budget, "best": best, "machines": who,
            "running_on": running, "updated": updated}


def pick(requests, jobs, machines, me, caps):
    """The oldest open request this computer may run now, as (request, derived job), or None."""
    for req in sorted(requests.values(), key=lambda r: r.get("created", 0)):
        st = progress(req, machines)
        if st["state"] != "open" or req.get("machine", "any") not in ("any", me):
            continue
        if req.get("here"):                                     # started by hand on its computer: that session runs it
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


def outbox_items(export):
    """The page's outbox (README section 17) as [(item id, viewer id, item)]: an ArtifactData export of the `outbox`
    collection, as a list of {id, data} rows or a {doc id: data} map, each doc {items: {item id: item}}."""
    rows = export.get("docs") or export.get("documents") or export.get("rows") or export if isinstance(export, dict) else export
    if isinstance(rows, dict):
        rows = [{"id": k, "data": v} for k, v in rows.items()]
    out = []
    for row in rows or []:
        if not isinstance(row, dict):
            continue
        doc = row.get("data") if isinstance(row.get("data"), dict) else row
        for k, item in sorted(((doc.get("items") or {}).items()), key=lambda kv: (kv[1] or {}).get("at", 0)):
            if isinstance(item, dict):
                out.append((k, row.get("id"), item))
    return out


def apply_outbox(export, sent, jobs, refs=None, push=True, cwd=None, names=None):
    """File what people sent from the claude.ai console while it has no hub: a run (`kind: run`, the console's token)
    goes on compute-queue, a stop (`kind: stop`, a request id) cancels it. Items already in `sent` (the page's
    `sent` collection, {item id: result}) are skipped. Returns {item id: {ok, note, request, at}}, to be written to
    `sent/<item id>`."""
    done = {}
    for k, viewer, item in outbox_items(export):
        if k in (sent or {}) or not ID_ITEM.fullmatch(k):
            continue
        res = {"ok": False, "at": L.now(), "kind": item.get("kind")}
        try:
            if item.get("kind") == "run":
                req = from_token(str(item.get("token") or ""))
                req["by"] = (names or {}).get(viewer) or req.get("by") or "console"
                bad = problems(req, jobs.get(req.get("job")))
                if bad or L.untrusted(jobs.get(req.get("job"))):
                    res["note"] = "; ".join(bad) or "the search is not on a trusted branch"
                elif req["id"] in load(refs, cwd=cwd):
                    res.update(ok=True, request=req["id"], note="already on the queue")
                elif file(req, push=push, cwd=cwd, quiet=True):
                    res.update(ok=True, request=req["id"], note="on the queue")
                else:
                    res["note"] = "could not push to " + BRANCH
            elif item.get("kind") == "stop":
                rid = str(item.get("id") or "")
                r = cancel(rid, refs, push=push, cwd=cwd, quiet=True) if ID.fullmatch(rid) else None
                res.update(ok=bool(r), request=rid, note="stopped: its computer stops within 30 s" if r else "no such run on the queue")
            else:
                res["note"] = "unknown kind"
        except Exception as e:
            res["note"] = f"not readable ({type(e).__name__})"
        done[k] = res
    return done


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
        "argv": ["x"], "units": [], "request": {"intensity": [1, 2, 3, 4], "episodes": {"lo": 1, "hi": 2000}},
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
                    ("too few matches", build("unity-train", 0, args=["mode=\"Joust\""])),
                    ("a repeated intensity", build("unity-train", 40, args=["mode=\"Joust\"", "intensity=[2, 2]"])),
                    ("an empty intensity list", build("unity-train", 40, args=["mode=\"Joust\"", "intensity=[]"])),
                    ("intensity 5 in a list", build("unity-train", 40, args=["mode=\"Joust\"", "intensity=[1, 5]"])),
                    ("watch as a word", build("unity-train", 40, args=["mode=\"Joust\"", "watch=\"yes\""]))]:
        if not problems(r, game):
            bad.append(f"requests: {what} was accepted for a run in the game")
    # Every intensity, shown on the computer's screen, one match: one unit per level, the budget per level.
    every = build("unity-train", 1, args=["mode=\"Joust\"", "intensity=[1, 2, 3, 4]", "watch=true"])
    if problems(every, game):
        bad.append(f"requests: a one-match run at every intensity, shown, was refused: {problems(every, game)}")
    else:
        dj = derive(game, every)
        ids = [u["id"] for u in dj["command"]["units"]]
        if ids != ["joust-squirrel-i1", "joust-squirrel-i2", "joust-squirrel-i3", "joust-squirrel-i4"]:
            bad.append(f"requests: every intensity did not derive one unit per level in order: {ids}")
        if not all(u["args"].get("watch") is True and u["args"]["episodes"] == 1 for u in dj["command"]["units"]):
            bad.append("requests: the units of a shown one-match run lost watch or the matches")
        if "I1-4" not in dj["title"] or "shown" not in dj["title"]:
            bad.append(f"requests: the title does not say the levels and the screen: {dj['title']}")
        st = progress(every, [{"user": "a", "host": "b", "requests": {every["id"]: {"tries": 3, "updated": L.now()}}, "heartbeat": {}}])
        if st["budget"] != 4 or st["state"] != "open":
            bad.append(f"requests: the budget of a four-level run is per level times four, open until all are done: {st}")
        st = progress(every, [{"user": "a", "host": "b", "requests": {every["id"]: {"tries": 4, "updated": L.now()}}, "heartbeat": {}}])
        if st["state"] != "done":
            bad.append(f"requests: a four-level run with every level played is not done: {st['state']}")
        one = derive(game, build("unity-train", 40, args=["mode=\"Joust\"", "intensity=3"]))
        if [u["id"] for u in one["command"]["units"]] != ["joust-squirrel-i3"] or "watch" in one["command"]["units"][0]["args"]:
            bad.append("requests: a one-level run without watch changed shape")
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

#!/usr/bin/env python3
"""Froglet Compute - the team's idle hardware, searching for the game. Read Docs/Compute/README.md; the skill is /compute.

    python3 Tools/Compute/compute.py top [--why]          the queue, ranked, and what THIS machine would run next
    python3 Tools/Compute/compute.py run                  work whenever you are away (10 min idle, on mains power)
    python3 Tools/Compute/compute.py install              ...and start that automatically on idle (uninstall undoes it)
    python3 Tools/Compute/compute.py doctor               what this machine can run, and how to let it run more
    python3 Tools/Compute/compute.py board [--write]      every machine's results, merged (Docs/Compute/BOARD.md)
    python3 Tools/Compute/compute.py scout [--write] [--propose]   find what is worth searching next
    python3 Tools/Compute/compute.py eval <job> [--params JSON]    score one point here (the shipped values by default)
    python3 Tools/Compute/compute.py promote <job> [--out DIR]     the converged answer, and how it reaches the game
    python3 Tools/Compute/compute.py new <id> --evaluator <module> scaffold a job file
    python3 Tools/Compute/compute.py validate             every job file in Docs/Compute/jobs
    python3 Tools/Compute/compute.py console              build the Compute Console's console.json (README section 13)
    python3 Tools/Compute/compute.py console              the Compute Console's console.json (README section 13)
    python3 Tools/Compute/compute.py self-test            the whole pipe, end to end, against a throwaway remote

`run` never touches your working tree, branch, index or repository config: jobs run in private exports under
~/.froglet-compute and results are published with git plumbing to refs/heads/compute/<you>/<this machine>, nowhere else. Between sessions it
updates itself from origin/bleeding-edge (--home), so a machine you set up once keeps running the team's newest code.
"""
import argparse
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# --repo <checkout>: the person's Cosmic-Shore checkout, when this code runs from a copy outside it (the one-line
# setup installs into ~/.froglet-compute/app). compute_lib reads it at import, so it is taken before the import.
if "--repo" in sys.argv[:-1]:
    os.environ["FROGLET_COMPUTE_REPO"] = os.path.abspath(sys.argv[sys.argv.index("--repo") + 1])

import compute_lib as L  # noqa: E402


def _queue_refs(args):
    refs = getattr(args, "queue", None) or list(L.QUEUE_REFS)
    return [r if r.startswith("origin/") else "origin/" + r for r in refs]


def _jobs(args):
    if getattr(args, "local", False):
        return L.load_jobs(local=True)
    if not getattr(args, "no_fetch", False):
        L.fetch([r.replace("origin/", "") for r in _queue_refs(args)], results=True, quiet=True)
    jobs = L.load_jobs(_queue_refs(args))
    if not jobs:
        print("(no jobs on " + ", ".join(_queue_refs(args)) + " - showing this checkout's Docs/Compute/jobs)")
        jobs = L.load_jobs(local=True)
    return jobs


# ── top ──────────────────────────────────────────────────────────────────────

def cmd_top(args):
    import worker as W
    jobs = _jobs(args)
    machines = L.gather()
    caps = L.capabilities()
    rows = W.ranked(jobs, machines, caps)
    nxt = next((r for r in rows if r["reason"] is None), None)
    print(f"{'#':>2}  {'priority':>8}  {'job':<34} {'status':<10} {'evals':>7}  best / shipped     waits on")
    for k, r in enumerate(rows, 1):
        if not args.all and r["job"]["status"] in ("retired", "promoted"):
            continue
        e = r["merged"] or {}
        best = (e.get("best") or {}).get("loss")
        print(f"{k:>2}  {r['score']:>8}  {r['job']['id']:<34} {r['job']['status']:<10} {e.get('evals', 0):>7}  "
              f"{('-' if best is None else f'{best:.4g}'):<17} {r['reason'] or ''}")
        if args.why:
            print(f"      {r['job']['title']} - {json.dumps(r['why'])}")
    print()
    if nxt:
        print(f"next on this machine: {nxt['job']['id']} - {nxt['job']['title']}")
        print("start it: python3 Tools/Compute/compute.py run      (or `install` to start on idle by itself)")
    else:
        print("nothing this machine can run right now: `doctor` says why; /compute-scout finds more work")
    return 0


# ── run: the supervisor (updates itself between sessions) ────────────────────

def _home_worktree(ref):
    """Tools/Compute at `ref`'s tip, exported into ~/.froglet-compute/home (not a git checkout: the child is told the
    person's repository through FROGLET_COMPUTE_REPO). None when the ref has no Tools/Compute (the system is not
    merged there yet): the worker then runs from this checkout."""
    if L.git_ok("cat-file", "-e", f"{ref}:Tools/Compute/compute.py") is None:   # prints nothing when it exists
        return None
    parent = os.path.join(L.cache_dir(), "home")
    os.makedirs(parent, exist_ok=True)
    return L.export_tree(L.rev(ref), ["Tools/Compute/"], parent, "compute")


def cmd_run(args):
    import worker as W
    if args.in_home or args.no_update or args.local or args.request:
        args.once = True if args.in_home else args.once
        return W.run(args)
    try:
        presence = W.Presence(args.when_idle, always=args.always, on_battery=args.on_battery, quiet=args.quiet,
                              between=args.between)
    except ValueError as e:
        print(e)
        return 2
    passthrough = [a for a in sys.argv[2:] if a != "--once"]
    said = False
    while True:
        if not presence.away():
            if not said:
                L.log(f"waiting for {presence.waiting_for()} ({args.when_idle} min idle)", args.quiet)
                said = True
            if args.once:
                return 0
            time.sleep(30)
            continue
        said = False
        if not args.no_fetch:
            L.fetch([args.home.replace("origin/", "")], results=False, quiet=True)
        home = _home_worktree(args.home)
        script = os.path.join(home, "Tools", "Compute", "compute.py") if home else os.path.abspath(__file__)
        out = {}
        try:                      # the Windows launcher sends output to worker.log: the session writes there too
            sys.stdout.flush()
            out = {"stdout": sys.stdout.fileno(), "stderr": sys.stderr.fileno()}
        except Exception:
            pass
        rc = subprocess.call([sys.executable, script, "run", *passthrough, "--in-home"], cwd=L.REPO,
                             env=dict(os.environ, FROGLET_COMPUTE_REPO=L.REPO), **out, **L.NO_WINDOW)
        if args.once:
            return rc
        if rc not in (0, 1):
            L.log(f"session exited {rc}; retrying in 5 min", args.quiet)
            time.sleep(300)
        elif rc == 1:
            time.sleep(1800)                     # nothing this machine can run: look again in half an hour


# ── doctor ───────────────────────────────────────────────────────────────────

def cmd_request(args):
    """Manual runs (manual_runs.py): make one, list them, cancel one."""
    import manual_runs as R
    queue = [r.replace("origin/", "") for r in L.QUEUE_REFS]
    if args.rcmd == "new":
        if args.token:
            req = R.from_token(args.token)
        elif args.job:
            req = R.build(args.job, args.tries, args.fix, args.range, args.arg, args.machine, args.note)
        else:
            print("name a search (compute.py request new <job> ...) or pass --token from the console")
            return 2
        if not args.local:
            L.fetch(queue, results=False, quiet=True)
        jobs = L.load_jobs(local=True) if args.local else L.load_jobs()
        bad = R.problems(req, jobs.get(req.get("job")))
        if bad:
            print("this request cannot run:\n  " + "\n  ".join(bad))
            return 2
        tok = R.token(req)
        print(f"request {req['id']}: {req['job']}, {req['budget']['tries']} tries" + (f" - {req['note']}" if req.get("note") else ""))
        print(f"run it now on this computer:   python3 Tools/Compute/compute.py run --request {tok}")
        if args.push:
            sha = R.file(req)
            print(f"filed on {R.BRANCH} @ {(sha or '?')[:9]}: the team's computers take it when they are free" if sha
                  else "could not push to " + R.BRANCH)
            return 0 if sha else 1
        print(f"for the team's computers:      python3 Tools/Compute/compute.py request new --token {tok} --push")
        return 0
    if args.rcmd == "list":
        if not args.local and not args.no_fetch:
            L.fetch(queue, results=True, quiet=True)
        reqs = R.load(local=True) if args.local else R.load()
        machines = L.gather()
        if not reqs:
            print("no manual runs")
        for r in sorted(reqs.values(), key=lambda r: r.get("created", 0)):
            st = R.progress(r, machines)
            best = "-" if st["best"] is None else f"{st['best']:.5g}"
            print(f"{r['id']}  {st['state']:<9} {st['tries']:>6}/{st['budget']:<6} best {best:<9} {r['job']}  "
                  f"by {r.get('by')}" + (f" on {st['running_on']}" if st["running_on"] else "") + (f"  \"{r['note']}\"" if r.get("note") else ""))
        return 0
    if args.rcmd == "cancel":
        L.fetch(queue, results=False, quiet=True)
        r = R.load().get(args.id)
        if not r:
            print(f"no request {args.id} on the queue")
            return 2
        r["cancelled"] = True
        return 0 if R.file(r) else 1
    return 2


def cmd_doctor(args):
    caps = L.capabilities(refresh=True)
    user, host = L.identity()
    print(f"this machine: {user}/{host}  ({caps['os']}, {caps['cores']} cores, python {caps['python']})")
    for k in ("numpy", "torch", "cuda", "node", "dotnet8", "bash"):
        print(f"  {k:<8} {'yes' if caps.get(k) else 'no'}")
    idle = L.idle_seconds()
    print(f"  idle     {'unknown (Linux: install xprintidle)' if idle is None else f'{idle:.0f} s since your last input'}")
    print(f"  battery  {'on battery (the worker waits for mains power)' if L.on_battery() else 'mains power'}")
    ok = L.git_ok("ls-remote", "--heads", "origin", "bleeding-edge")
    print(f"  git      {'origin reachable' if ok else 'origin NOT reachable - the worker cannot fetch or publish'}")
    print("\nTo run more of the queue:")
    if not caps.get("numpy"):
        print("  pip install numpy                 (course search, every Regatta job)")
    if not caps.get("torch"):
        print("  pip install torch                 (NCA bioform trainings; with CUDA: https://pytorch.org/get-started/locally/)")
    if not caps.get("node"):
        print("  install Node.js                   (lab scorecards that run in node)")
    print("\nThen: python3 Tools/Compute/compute.py install   (starts whenever you are 10 min idle)")
    return 0


# ── eval / promote / new / validate ──────────────────────────────────────────

def _eval_here(job, params):
    import importlib
    sys.path[:0] = [os.path.join(L.REPO, "Tools", "Build")]
    m = importlib.import_module("evaluators." + job["evaluator"]["module"])
    args = dict(job["evaluator"].get("args") or {}, _space=job.get("space") or [], _job=job["id"])
    ctx = {"root": L.REPO, "args": args, "tools": HERE}
    if hasattr(m, "setup"):
        m.setup(ctx)
    return m.evaluate(params, ctx)


def cmd_eval(args):
    import optimizers as O
    jobs = L.load_jobs(local=True)
    job = jobs.get(args.job)
    if not job:
        print(f"no job {args.job} in Docs/Compute/jobs")
        return 1
    space = O.Space(job["space"])
    params = json.loads(args.params) if args.params else space.decode(space.init_point())
    t = time.time()
    r = _eval_here(job, params)
    print(json.dumps({"params": params, "result": r, "seconds": round(time.time() - t, 2)}, indent=1, default=str))
    return 0


def cmd_promote(args):
    jobs = _jobs(args)
    job = jobs.get(args.job)
    if not job:
        print(f"no job {args.job}")
        return 1
    machines = L.gather()
    ek, e = L.current_epoch(L.merged_job(machines, job["id"]))
    if not e or not e.get("best"):
        print(f"{job['id']}: no results yet")
        return 1
    print(f"{job['id']} - epoch {ek}: {e['evals']} evals on {len(set(e['machines']))} machine(s), "
          f"{e['seconds'] / 3600:.1f} machine-hours; converged: {L.converged(job, e)}")
    print(json.dumps(e["best"], indent=1, default=str))
    if e.get("archive"):
        print(f"{len(e['archive'])} elites in the archive")
        if args.out and job["evaluator"]["module"] == "bioform_forms":
            sys.path[:0] = [os.path.join(HERE, "evaluators"), os.path.join(L.REPO, "Tools", "Build")]
            import bioform_forms
            take = dict(sorted(e["archive"].items(), key=lambda kv: kv[1]["loss"])[:args.top])
            for p in bioform_forms.export(take, args.out):
                print("baked " + p)
    print("\nHow it reaches the game (and who must agree first):\n  " + (job.get("promote") or "see the job file"))
    return 0


def cmd_new(args):
    path = os.path.join(L.REPO, L.JOBS_DIR, args.id + ".json")
    if os.path.exists(path):
        print(f"{path} exists")
        return 1
    job = {"schema": L.SCHEMA, "id": args.id, "title": "TODO one line", "goal": "TODO the ambitious goal this serves",
           "asked_by": "", "area_owner": "", "kind": "search", "status": "proposed", "value": 5,
           "updated": time.strftime("%Y-%m-%d"), "ref": "origin/bleeding-edge", "requires": ["python3"], "after": [],
           "evaluator": {"module": args.evaluator, "args": {}}, "space": [],
           "algorithm": {"name": "sep-cmaes"}, "convergence": {"min_evals": 2000, "window_evals": 1500, "min_improvement": 0.003},
           "promote": "TODO: what a converged answer changes in the game, by which tool, and who must agree"}
    L.write_json(path, job)
    print(f"wrote {path} (status: proposed; /compute-job says how to fill it in)")
    return 0


def cmd_validate(args):
    jobs = L.load_jobs(local=True)
    bad = 0
    for jid, j in sorted(jobs.items()):
        p = L.validate_job(j)
        if j.get("kind") == "search" and not p:
            try:
                import importlib
                importlib.import_module("evaluators." + j["evaluator"]["module"])
            except Exception as e:
                p.append(f"evaluator {j['evaluator']['module']} does not import: {e}")
        for d in j.get("after") or []:
            if d not in jobs:
                p.append(f"after: {d} is not a job")
        if p:
            bad += 1
            print(f"{jid}: " + "; ".join(p))
    print(f"{len(jobs)} job(s), {bad} with problems")
    return 1 if bad else 0


def _newest_copy(argv):
    """A command typed by hand into ~/.froglet-compute/app - a line pasted from the console - runs the install ref's
    newest Froglet Compute: the console moves on with every push, the app copy only when the setup is run again, and
    a run the console made for a newer job ("no search named unity-train") is the gap. Fetches the install ref
    (config.json "ref"); when its Tools/Compute differs from this copy's, exports it under ~/.froglet-compute/fresh and
    runs the same command there. The app folder itself is never replaced (the scheduled worker may be running from
    it). Returns the exit code, or None to go on here."""
    if os.environ.get("FROGLET_COMPUTE_NO_REFRESH") or not argv:
        return None
    if not (argv[0] in ("unity", "request", "doctor") or (argv[0] == "run" and "--request" in argv)):
        return None
    app = os.path.join(L.cache_dir(), "app", "Tools", "Compute")
    if os.path.normcase(os.path.realpath(HERE)) != os.path.normcase(os.path.realpath(app)):
        return None
    ref = (L.read_json(os.path.join(L.cache_dir(), "config.json"), {}) or {}).get("ref")
    if not ref:
        return None
    ref = ref.replace("origin/", "")
    L.git_ok("fetch", "--quiet", "--no-tags", "origin", f"+refs/heads/{ref}:refs/remotes/origin/{ref}")
    sha = L.rev(f"origin/{ref}")
    tree = (L.git_ok("rev-parse", f"origin/{ref}:Tools/Compute") or "").strip()
    stamp = os.path.join(L.cache_dir(), "app", ".tree")
    mine = (open(stamp).read().strip() if os.path.exists(stamp) else "")
    if not sha or not tree or tree == mine:
        return None
    try:
        root = L.export_tree(sha, ["Tools/Compute/"], os.path.join(L.cache_dir(), "fresh"), "compute", keep=2)
    except Exception as e:
        L.log(f"could not refresh Froglet Compute from origin/{ref} ({e}); running this copy", False)
        return None
    L.log(f"this computer's Froglet Compute is older than origin/{ref}: running its newest ({sha[:9]}). "
          "Run the Install line again to update the installed copy too.", False)
    env = dict(os.environ, FROGLET_COMPUTE_NO_REFRESH="1", FROGLET_COMPUTE_REPO=L.REPO)
    return subprocess.run([sys.executable, os.path.join(root, "Tools", "Compute", "compute.py"), *argv], env=env).returncode


def main(argv=None):
    argv = sys.argv[1:] if argv is None else list(argv)
    rc = _newest_copy(argv)
    if rc is not None:
        return rc
    if argv[:1] == ["unity"]:                       # its own flags, passed through untouched (unity_run.py)
        import unity_run
        return unity_run.main(argv[1:])
    ap = argparse.ArgumentParser(prog="compute.py", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p):
        p.add_argument("--queue", nargs="*", help="queue refs (default: bleeding-edge, ai, compute-queue)")
        p.add_argument("--local", action="store_true", help="read jobs from this checkout's Docs/Compute/jobs")
        p.add_argument("--no-fetch", action="store_true")

    p = sub.add_parser("top")
    common(p)
    p.add_argument("--why", action="store_true")
    p.add_argument("--all", action="store_true")
    p = sub.add_parser("run")
    common(p)
    p.add_argument("--when-idle", type=float, default=10.0, help="minutes of no input before working (default 10)")
    p.add_argument("--always", action="store_true", help="a dedicated box: do not wait for idle")
    p.add_argument("--between", help="only work inside these local hours, e.g. 22:00-07:00 (with or without --always)")
    p.add_argument("--on-battery", action="store_true", help="also work on battery power")
    p.add_argument("--cores", type=int, default=0, help="worker processes (default: all cores but one)")
    p.add_argument("--session", type=float, default=60.0, help="minutes per job before re-ranking (default 60)")
    p.add_argument("--progress-minutes", type=float, default=5.0, help="publish progress this often while a session runs")
    p.add_argument("--repo", help="the Cosmic-Shore checkout, when running from a copy outside it")
    p.add_argument("--request", help="a manual run: a request id, a token from the console, or a request file; runs now")
    p.add_argument("--no-requests", action="store_true", help="skip other people's manual runs (the automatic queue only)")
    p.add_argument("--job", help="only this job")
    p.add_argument("--once", action="store_true", help="one session, then exit")
    p.add_argument("--max-evals", type=int, default=0)
    p.add_argument("--no-push", action="store_true", help="keep results on this machine")
    p.add_argument("--no-update", action="store_true", help="run this checkout's code, not origin/bleeding-edge's")
    p.add_argument("--home", default="origin/bleeding-edge", help="where the worker's own code comes from")
    p.add_argument("--quiet", action="store_true")
    p.add_argument("--in-home", action="store_true", help=argparse.SUPPRESS)
    sub.add_parser("doctor")
    p = sub.add_parser("install")
    p.add_argument("--when-idle", type=float, default=10.0)
    p.add_argument("--between", help="only work inside these local hours, e.g. 22:00-07:00")
    p.add_argument("--always", action="store_true", help="a dedicated box (a VM): work without waiting for idle")
    p.add_argument("--home", help="where the worker's own code comes from (default origin/bleeding-edge)")
    p.add_argument("--repo", help="the Cosmic-Shore checkout, when this code runs from a copy outside it")
    p.add_argument("--queue", nargs="*", help="queue refs (default: bleeding-edge, ai, compute-queue)")
    p.add_argument("--dry-run", action="store_true")
    sub.add_parser("uninstall")
    p = sub.add_parser("board")
    common(p)
    p.add_argument("--write", action="store_true")
    p = sub.add_parser("scout")
    common(p)
    p.add_argument("--write", action="store_true", help="write Docs/Compute/OPPORTUNITIES.md")
    p.add_argument("--propose", action="store_true", help="write job files for the mechanical opportunities")
    p = sub.add_parser("hub", help="serve the console live (the Oracle VM): README section 15")
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=8080)
    p.add_argument("--rebuild", type=int, default=60, help="seconds between rebuilds from git (default 60)")
    p.add_argument("--queue", nargs="*", help="queue refs (default: bleeding-edge, ai, compute-queue)")
    p.add_argument("--url", help="the console's public address")
    p.add_argument("--key-file", help="the team key file (default ~/.froglet-compute/hub.json)")
    p.add_argument("--github-token-file", help="a token file, to show open pull requests")
    p.add_argument("--quiet", action="store_true")
    p = sub.add_parser("unity", help="the Unity trainer run: --find says which Unity this computer uses (README section 16)",
                       add_help=False)
    p.add_argument("rest", nargs=argparse.REMAINDER)
    p = sub.add_parser("console")
    p.add_argument("--out", help="folder for console.json and the page (default ~/.froglet-compute/console)")
    p.add_argument("--local", action="store_true", help="read jobs from this checkout's Docs/Compute/jobs")
    p.add_argument("--no-fetch", action="store_true")
    p.add_argument("--prs", help="a JSON file of open pull requests (GitHub list_pull_requests) to include")
    p.add_argument("--url", help="where the console is published")
    p.add_argument("--standalone", action="store_true", help="write a full HTML document (GitHub Pages, Cloudflare, a local server)")
    p.add_argument("--approvals", help="check approvals exported from the page's store instead of building")
    p.add_argument("--profiles", help="with --approvals: the profiles the store's ids resolve to")
    p = sub.add_parser("request", help="manual runs: new, list, cancel")
    rs = p.add_subparsers(dest="rcmd", required=True)
    q = rs.add_parser("new", help="a manual run of one search with your settings")
    q.add_argument("job", nargs="?")
    q.add_argument("--tries", type=int, default=500, help="how many tries in total (default 500)")
    q.add_argument("--fix", action="append", default=[], metavar="NAME=VALUE", help="hold a setting at a value")
    q.add_argument("--range", action="append", default=[], metavar="NAME=LOW:HIGH", help="search a setting in a narrower range")
    q.add_argument("--arg", action="append", default=[], metavar="KEY=VALUE", help="set one of the evaluator's settings")
    q.add_argument("--machine", default="any", help="any (default) or <person>/<computer>")
    q.add_argument("--note", default="", help="what you want to learn, in your words")
    q.add_argument("--token", help="a request made on the Compute Console (instead of the options above)")
    q.add_argument("--push", action="store_true", help="file it on compute-queue for the team's computers")
    q.add_argument("--local", action="store_true", help="check it against this checkout's job files")
    q = rs.add_parser("list", help="every manual run and how far it has got")
    q.add_argument("--local", action="store_true")
    q.add_argument("--no-fetch", action="store_true")
    q = rs.add_parser("cancel", help="stop a manual run (the computers drop it)")
    q.add_argument("id")
    p = sub.add_parser("eval")
    p.add_argument("job")
    p.add_argument("--params")
    p = sub.add_parser("promote")
    common(p)
    p.add_argument("job")
    p.add_argument("--out")
    p.add_argument("--top", type=int, default=12)
    p = sub.add_parser("new")
    p.add_argument("id")
    p.add_argument("--evaluator", required=True)
    sub.add_parser("validate")
    p = sub.add_parser("self-test")
    p.add_argument("--quick", action="store_true", help="skip the end-to-end worker session")
    args = ap.parse_args(argv)

    if args.cmd == "top":
        return cmd_top(args)
    if args.cmd == "run":
        return cmd_run(args)
    if args.cmd == "doctor":
        return cmd_doctor(args)
    if args.cmd == "hub":
        import hub
        return hub.run(args)
    if args.cmd == "request":
        return cmd_request(args)
    if args.cmd == "console":
        import console
        return console.run(args)
    if args.cmd == "install":
        import install
        return install.install(args.when_idle, dry=args.dry_run, between=args.between, always=args.always,
                               home=args.home, repo=args.repo, queue=args.queue)
    if args.cmd == "uninstall":
        import install
        return install.uninstall()
    if args.cmd == "board":
        import board
        return board.run(args)
    if args.cmd == "scout":
        import scout
        return scout.run(args)
    if args.cmd == "eval":
        return cmd_eval(args)
    if args.cmd == "promote":
        return cmd_promote(args)
    if args.cmd == "new":
        return cmd_new(args)
    if args.cmd == "validate":
        return cmd_validate(args)
    if args.cmd == "self-test":
        import selftest
        return selftest.run(quick=args.quick)
    return 2


if __name__ == "__main__":
    sys.exit(main())

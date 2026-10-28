"""The Compute Console's data - `python3 Tools/Compute/compute.py console [--out DIR] [--local]`.

The console is one static page (Tools/Compute/console/index.html) that reads one file next to it, console.json, built
here from git alone: the queue, every machine's results, the recordings people's play sent, and the `ai` branch. No
server: the same two files run as a claude.ai artifact today and on GitHub Pages or Cloudflare later (README section
13).

    console [--out DIR]       write DIR/console.json (default ~/.froglet-compute/console) and copy the page beside it;
                              --prs FILE adds open pull requests (a session's GitHub list_pull_requests answer),
                              --url URL records where the console is published (its terminal's refresh task uses it)
    console --approvals FILE  check approvals read from the claude.ai page's store (ArtifactData `list` of
                              `approvals`, with --profiles FILE from ArtifactData `profiles`) against the current
                              results: which are valid to act on, and why the others are not

Approvals are a request, never an action: a session acts on a valid one (README section 8), and only one from the
core four (Docs/TEAM.json `core`) for the exact result the person saw (job, epoch and best loss).

A reader tool (it writes only its own output folder), so it needs no tool-output ledger.
"""
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [HERE, os.path.join(HERE, "evaluators")]
import compute_lib as L  # noqa: E402
import worker as W  # noqa: E402

PAGE = os.path.join(HERE, "console", "index.html")
SCHEMA = "froglet-compute/console/1"
FLIGHT = re.compile(r"BugLedger/shared/reports/([^/]+)/flights/flight_([A-Za-z0-9]+)_([A-Za-z0-9]+)_(\d{8})_(\d{6})\.json$")
SEEN = "Docs/AI_SYSTEM/ai_learning_seen.json"


def _num(x):
    return round(float(x), 5) if L.finite(x) else None


def _result(r):
    """The parts of a best/baseline result the page shows."""
    if not r:
        return None
    m = r.get("metrics") or {}
    out = {"loss": _num(r.get("loss")), "machine": r.get("machine"), "evals_at": r.get("evals_at"),
           "time": r.get("time"), "limits": m.get("limits"), "model": m.get("model")}
    for k in ("races", "dnf"):
        if k in m:
            out[k] = m[k]
    if isinstance(m.get("seconds_by_intensity"), dict):
        out["seconds_by_intensity"] = {k: _num(v) for k, v in m["seconds_by_intensity"].items()}
    if isinstance(r.get("params"), dict):
        out["params"] = {k: (_num(v) if isinstance(v, (int, float)) else v) for k, v in r["params"].items()}
    return out


def _curve(epoch, points=60):
    curve = L.best_curve(epoch)
    if len(curve) > points:
        step = len(curve) / points
        curve = [curve[int(i * step)] for i in range(points)] + [curve[-1]]
    return [[int(e), _num(l)] for e, l in curve]


def jobs_and_machines(jobs, machines, caps):
    rows = W.ranked(jobs, machines, caps)
    t = L.now()
    out_jobs = []
    for r in rows:
        j, e = r["job"], r["merged"] or {}
        base = None
        for m in machines:
            s = ((m["jobs"].get(j["id"]) or {}).get(r["epoch"]) or {}).get("summary") or {}
            if s.get("baseline"):
                base = s["baseline"]
                break
        archive = e.get("archive") or {}
        bins = 1
        for d in ((j.get("algorithm") or {}).get("descriptors") or []):
            bins *= int(d.get("bins", 1))
        out_jobs.append({
            "id": j["id"], "title": j.get("title"), "goal": j.get("goal"), "kind": j.get("kind"),
            "status": j.get("status"), "plan": j.get("plan"), "stage": j.get("stage"), "value": j.get("value"),
            "asked_by": j.get("asked_by"), "area_owner": j.get("area_owner"), "ref": j.get("ref") or "origin/bleeding-edge",
            "promote": j.get("promote"), "after": j.get("after") or [], "blocked_reason": j.get("blocked_reason"),
            "priority": r["score"], "why": r["why"], "reason": r["reason"], "epoch": r["epoch"],
            "evals": e.get("evals", 0), "machine_hours": round(e.get("seconds", 0) / 3600, 2),
            "machines": sorted(set(e.get("machines", []))), "updated": e.get("updated"),
            "best": _result(e.get("best")), "baseline": _result(base),
            "converged": bool(L.converged(j, e)) if e else False, "curve": _curve(e),
            "convergence": dict({"min_evals": 2000, "window_evals": 1500, "min_improvement": 0.003},
                                **(j.get("convergence") or {})),
            "archive": {"filled": len(archive), "cells": bins} if archive else None,
            "units": {u: {"progress": v.get("progress"), "done": v.get("done"), "machine": v.get("machine")}
                      for u, v in (e.get("units") or {}).items()} or None})
    out_machines = []
    for m in machines:
        hb = m.get("heartbeat") or {}
        cur = hb.get("current") or {}
        out_machines.append({"machine": f"{m['user']}/{m['host']}", "user": m["user"], "host": m["host"],
                             "updated": hb.get("updated") or m.get("time"), "current": cur.get("job"),
                             "unit": cur.get("unit"), "cores": hb.get("cores"),
                             "caps": sorted(k for k, v in (hb.get("caps") or {}).items() if v is True),
                             "os": (hb.get("caps") or {}).get("os"), "sessions": (hb.get("totals") or {}).get("sessions"),
                             "last": hb.get("last"),
                             "active": bool(cur.get("job")) and t - int(hb.get("updated", 0) or 0) < 3 * 3600})
    out_machines.sort(key=lambda m: -(m["updated"] or 0))
    return out_jobs, out_machines


def runs(machines, jobs):
    """What each computer is doing now (progress published every few minutes) and its recent sessions."""
    titles = {j["id"]: j.get("title") for j in jobs.values()}
    for jid in list(titles):
        titles[jid + "~"] = "Manual run: " + (titles[jid] or jid)

    def title(job_id):
        job_id = job_id or ""
        return titles.get(job_id) or (titles.get(job_id.split("~")[0] + "~") if "~" in job_id else None)
    t = L.now()
    active, recent = [], []
    for m in machines:
        hb = m.get("heartbeat") or {}
        who = f"{m['user']}/{m['host']}"
        cur = hb.get("current") or {}
        if cur.get("job") and t - int(hb.get("updated", 0) or 0) < 3 * 3600:
            active.append({"machine": who, "job": cur["job"], "title": title(cur["job"]), "request": cur.get("request"),
                           "since": cur.get("since"),
                           "updated": hb.get("updated"), "minutes": cur.get("minutes"), "max_evals": cur.get("max_evals"),
                           "evals": cur.get("evals"), "best": _num(cur.get("best")), "best_start": _num(cur.get("best_start")),
                           "unit": cur.get("unit"), "progress": cur.get("progress")})
        for r in hb.get("runs") or []:
            recent.append(dict(r, machine=who, title=title(r.get("job")),
                               best_start=_num(r.get("best_start")), best_end=_num(r.get("best_end"))))
    recent.sort(key=lambda r: -(r.get("end") or 0))
    return {"active": active, "recent": recent[:60]}


def manual(jobs, machines, cwd=None):
    """Every manual run (manual_runs.py) with how far it has got, its best and today's value under its settings."""
    import manual_runs as R
    out = []
    for req in sorted(R.load(cwd=cwd).values(), key=lambda r: -(r.get("created") or 0)):
        st = R.progress(req, machines)
        derived = f"{req.get('job')}~{req['id']}"
        base = None
        for m in machines:
            for e in (m["jobs"].get(derived) or {}).values():
                base = base or ((e.get("summary") or {}).get("baseline") or {}).get("loss")
        job = jobs.get(req.get("job")) or {}
        out.append({"id": req["id"], "job": req.get("job"), "title": job.get("title") or req.get("job"), "by": req.get("by"),
                    "created": req.get("created"), "note": req.get("note"), "machine": req.get("machine", "any"),
                    "fix": req.get("fix") or {}, "ranges": req.get("ranges") or {}, "args": req.get("args") or {},
                    "problems": R.problems(req, job or None), "today": _num(base), **{k: st[k] for k in
                    ("state", "tries", "budget", "machines", "running_on", "updated")}, "best": _num(st["best"])})
    return out


def run_forms(jobs):
    """What the console's New run form needs per search: its settings with bounds and today's values, and the
    evaluator settings a manual run may change."""
    out = {}
    for j in jobs.values():
        if j.get("kind") != "search":
            continue
        out[j["id"]] = {"space": [{k: d.get(k) for k in ("name", "type", "lo", "hi", "choices", "init")} for d in j.get("space") or []],
                        "args": {k: v for k, v in ((j.get("evaluator") or {}).get("args") or {}).items()
                                 if not k.startswith("_") and (isinstance(v, (int, float, bool)) and not isinstance(v, bool) or
                                                               (isinstance(v, list) and all(isinstance(x, (int, float)) for x in v)))}}
    return out


def install_info(cwd=None):
    """Where the one-line setup comes from: bleeding-edge once Froglet Compute is merged, the compute branch until then."""
    import setup
    on_be = L.git_ok("cat-file", "-e", "origin/bleeding-edge:Tools/Compute/setup.py", cwd=cwd) is not None
    ref = "bleeding-edge" if on_be else setup.APP_REF
    return {"ref": ref, "merged": on_be,
            "unix": f"git fetch origin {ref} && git show origin/{ref}:Tools/Compute/setup.py | python3 -",
            "windows": f"git fetch origin {ref}; git show origin/{ref}:Tools/Compute/setup.py | python -"}


def recordings(cwd=None):
    """Every flight people's play sent (origin/bugledger/*), and whether the AI learning sweep learned from it."""
    seen = {}
    for ref in (L.AI_REF, "origin/Ys-bleeding-edge", "origin/bleeding-edge"):
        raw = L.git_ok("show", f"{ref}:{SEEN}", cwd=cwd)
        if raw:
            try:
                seen = json.loads(raw)
                seen["_ref"] = ref
                break
            except Exception:
                pass
    learned, waiting = set(seen.get("flights") or []), seen.get("waiting") or {}
    refs = (L.git_ok("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/bugledger/", cwd=cwd) or "").split()
    flights = []
    for ref in refs:
        listing = L.git_ok("ls-tree", "-r", "-l", ref, "--", "BugLedger/shared/reports", cwd=cwd) or ""
        for line in listing.splitlines():
            meta, _, path = line.partition("\t")
            m = FLIGHT.search(path)
            if not m:
                continue
            user, mode, vessel, day, clock = m.groups()
            size = meta.split()[3] if len(meta.split()) > 3 else "0"
            state, why = ("learned", None) if path in learned else (("waiting", waiting[path]) if path in waiting else ("new", None))
            flights.append({"user": user, "mode": mode, "vessel": vessel, "date": f"{day[:4]}-{day[4:6]}-{day[6:]}",
                            "time": f"{clock[:2]}:{clock[2:4]}", "bytes": int(size) if size.isdigit() else 0,
                            "state": state, "why": why})
    flights.sort(key=lambda f: (f["date"], f["time"]), reverse=True)
    return {"flights": flights, "seen_ref": seen.get("_ref"), "branches": len(refs)}


def ai_branch_status(cwd=None):
    try:
        import ai_branch
        p = ai_branch.plan(cwd)
    except Exception as e:
        return {"error": str(e)}
    ai = p["ai"]
    behind = int(L.git("rev-list", "--count", f"{ai}..{p['base']}", cwd=cwd).strip()) if ai else None
    waiting = sum(len(s.get("files", [])) for s in p["steps"])
    return {"ref": L.AI_REF, "sha": ai, "behind_base": behind, "last_synced_ys": p["last"], "ys": p["source"],
            "waiting_ai_files": waiting, "needs_sync": bool(p["steps"])}


def git_snapshot(cwd=None, n=8):
    """What the terminal's `git` commands show: recent commits on the branches the console is about."""
    out = {"branches": {}}
    for ref in ("origin/bleeding-edge", L.AI_REF, "origin/Ys-bleeding-edge"):
        log = L.git_ok("log", f"-{n}", "--format=%h%x09%an%x09%ct%x09%s", ref, cwd=cwd) or ""
        rows = []
        for line in log.splitlines():
            parts = line.split("\t", 3)
            if len(parts) == 4:
                rows.append({"sha": parts[0], "author": parts[1], "time": int(parts[2]), "subject": parts[3][:140]})
        if rows:
            out["branches"][ref.replace("origin/", "")] = rows
    out["results"] = [{"ref": b["ref"].replace("origin/", ""), "time": b["time"]} for b in L.result_branches(cwd=cwd)]
    return out


def pull_requests(path):
    """Open pull requests from a session's GitHub list_pull_requests answer, trimmed to what the terminal shows."""
    raw = L.read_json(path, []) if path else []
    rows = raw if isinstance(raw, list) else raw.get("pull_requests") or raw.get("items") or []
    return [{"number": r.get("number"), "title": (r.get("title") or "")[:140], "head": (r.get("head") or {}).get("ref"),
             "base": (r.get("base") or {}).get("ref"), "user": (r.get("user") or {}).get("login"),
             "updated": r.get("updated_at"), "draft": bool(r.get("draft")), "url": r.get("html_url")} for r in rows]


def core_four():
    team = L.read_json(os.path.join(L.REPO, "Docs", "TEAM.json"), {}) or {}
    return list(team.get("core") or [])


def build(local=False, fetch=True, cwd=None, prs=None, url=None):
    if fetch:
        L.fetch([r.replace("origin/", "") for r in L.QUEUE_REFS] + ["Ys-bleeding-edge"], results=True, cwd=cwd, quiet=True)
        L.git_ok("fetch", "--quiet", "--no-tags", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*",
                 cwd=cwd)
    jobs = L.load_jobs(local=True, cwd=cwd) if local else L.load_jobs(cwd=cwd)
    machines = L.gather(cwd=cwd)
    out_jobs, out_machines = jobs_and_machines(jobs, machines, L.capabilities())
    run_data = runs(machines, jobs)
    return {"schema": SCHEMA, "generated": L.now(), "core": core_four(),
            "queue": "working tree (--local)" if local else list(L.QUEUE_REFS),
            "jobs": out_jobs, "machines": out_machines, "runs": run_data, "install": install_info(cwd),
            "manual": manual(jobs, machines, cwd), "forms": run_forms(jobs),
            "recordings": recordings(cwd), "ai": ai_branch_status(cwd),
            "git": git_snapshot(cwd), "pull_requests": pull_requests(prs), "repo": "froglet-studio/Cosmic-Shore",
            "branch": (L.git_ok("rev-parse", "--abbrev-ref", "HEAD", cwd=cwd) or "").strip(), "url": url}


SKELETON = ('<!doctype html><html lang="en"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"></head><body>')


def write(out_dir, data, standalone=False):
    """console.json and the page. The page source is artifact-shaped (claude.ai wraps it in a document); `standalone`
    wraps it here instead, for GitHub Pages, Cloudflare or a local `python3 -m http.server`."""
    os.makedirs(out_dir, exist_ok=True)
    L.write_json(os.path.join(out_dir, "console.json"), data)
    if standalone:
        with open(PAGE, encoding="utf-8") as fh:
            page = fh.read()
        with open(os.path.join(out_dir, "index.html"), "w", encoding="utf-8") as fh:
            fh.write(SKELETON + page + "</body></html>\n")
    else:
        shutil.copyfile(PAGE, os.path.join(out_dir, "index.html"))
    return out_dir


# ── approvals from the page ──────────────────────────────────────────────────

def check_approvals(docs, profiles, data):
    """Each approval as {by, name, job, decision, valid, why}. `docs` is the `approvals` collection (one document per
    person, `decisions: {job: {...}}`); `profiles` maps ids to {name}; `data` is the current console.json."""
    core = [c.lower() for c in data.get("core", [])]
    jobs = {j["id"]: j for j in data.get("jobs", [])}
    out = []
    for d in docs:
        uid = d.get("id")
        body = d.get("data", d)
        name = ((profiles or {}).get(uid) or {}).get("name") or ""
        first = name.split()[0].lower() if name else ""
        for jid, dec in ((body or {}).get("decisions") or {}).items():
            row = {"by": uid, "name": name, "job": jid, "decision": dec.get("decision"), "note": dec.get("note"),
                   "epoch": dec.get("epoch"), "loss": dec.get("loss"), "valid": False, "why": ""}
            j = jobs.get(jid)
            if first not in core:
                row["why"] = f"{name or 'an unresolved person'} is not one of the core four ({', '.join(data.get('core', []))})"
            elif j is None:
                row["why"] = "no such job in the queue"
            elif dec.get("decision") != "approve":
                row["why"] = "declined" if dec.get("decision") == "decline" else "no decision"
            elif dec.get("epoch") != j.get("epoch"):
                row["why"] = "approved an older epoch: the job's space, evaluator or inputs changed since"
            elif not j.get("best") or dec.get("loss") is None or abs(float(dec["loss"]) - float(j["best"]["loss"])) > 1e-9:
                row["why"] = "the team's best moved since the approval: approve the new best on the page"
            else:
                row["valid"], row["why"] = True, "act on it: README section 8"
            out.append(row)
    return out


def self_test():
    bad = []
    data = {"core": ["Garrett", "Caleb", "Shombith", "Yash"],
            "jobs": [{"id": "drive-x", "epoch": "e1", "best": {"loss": 4.05}}]}
    docs = [{"id": "u_s", "data": {"decisions": {"drive-x": {"decision": "approve", "epoch": "e1", "loss": 4.05}}}},
            {"id": "u_o", "data": {"decisions": {"drive-x": {"decision": "approve", "epoch": "e1", "loss": 4.05}}}},
            {"id": "u_y", "data": {"decisions": {"drive-x": {"decision": "approve", "epoch": "e0", "loss": 4.05}}}},
            {"id": "u_g", "data": {"decisions": {"drive-x": {"decision": "approve", "epoch": "e1", "loss": 4.30}}}},
            {"id": "u_c", "data": {"decisions": {"drive-x": {"decision": "decline", "epoch": "e1", "loss": 4.05}}}}]
    profiles = {"u_s": {"name": "Shombith Roy"}, "u_o": {"name": "Someone Else"}, "u_y": {"name": "Yash S"},
                "u_g": {"name": "Garrett M"}, "u_c": {"name": "Caleb"}}
    got = {r["by"]: r for r in check_approvals(docs, profiles, data)}
    want = {"u_s": True, "u_o": False, "u_y": False, "u_g": False, "u_c": False}
    for uid, ok in want.items():
        if got.get(uid, {}).get("valid") != ok:
            bad.append(f"console approvals: {uid} should be {'valid' if ok else 'refused'} ({got.get(uid)})")
    if not os.path.isfile(PAGE):
        bad.append("console: the page Tools/Compute/console/index.html is missing")
    return bad


def run(args):
    if args.approvals:
        data = L.read_json(os.path.join(args.out or os.path.join(L.cache_dir(), "console"), "console.json"))
        if data is None:
            print("build console.json first: compute.py console")
            return 2
        raw = L.read_json(args.approvals, {})
        docs = raw.get("documents") or raw.get("docs") or (raw if isinstance(raw, list) else [])
        profiles = L.read_json(args.profiles, {}) if args.profiles else {}
        profiles = profiles.get("profiles", profiles)
        rows = check_approvals(docs, profiles, data)
        for r in rows:
            print(f"{'VALID ' if r['valid'] else 'refused'}  {r['job']:<26} {r['name'] or r['by']}: {r['why']}")
        if not rows:
            print("no approvals")
        return 0
    data = build(local=args.local, fetch=not args.no_fetch, prs=args.prs, url=args.url)
    out = write(args.out or os.path.join(L.cache_dir(), "console"), data, standalone=args.standalone)
    print(f"wrote {out}/console.json ({len(data['jobs'])} jobs, {len(data['machines'])} machines, "
          f"{len(data['recordings']['flights'])} recordings) and {out}/index.html")
    return 0

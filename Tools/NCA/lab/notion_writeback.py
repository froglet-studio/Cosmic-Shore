#!/usr/bin/env python3
"""
notion_writeback.py -- the NCA Ecology Lab's Notion backlog <-> Froglet Compute, from ONE computer.

Froglet Compute has no per-job post-step: a unit's files go to its machine's results branch (compute/<user>/<host>)
and nowhere else (Tools/Compute/worker.py _save_unit). So the writeback is not something every contributor's computer
runs - that would put a Notion token on every machine. It runs where the token already lives (Garrett's PC, next to
the lab runner, or a scheduled session), reads the results branches like the board does, and writes the rows:

  push    every unit of an NCA-lab job (a unit whose args carry `notion_page`):
            progress > 0, not done  -> Status Running
            done                    -> Status Done, Result = the runner's metric line + where it ran, the growth GIF
                                       appended to the page (the runner's own upload, best effort)
            a local run folder (--run-dir, with --page) can be pushed the same way, for runs made outside Compute
  steer   read the backlog and say what the lab jobs' units should be: Kill drops a row, Prioritize puts it first,
          then Priority (P0 > P1 > P2), then age - the runner's own order (runner.py select_next). Rows already Done
          or Parked are left out. --write rewrites the unit lists of the job files in Docs/Compute/jobs/; the change
          reaches the computers only when it merges to bleeding-edge (command jobs run from nowhere else).

DRY RUN BY DEFAULT: it prints each request it would send (method, path, body). --apply sends them. The token is read
from NOTION_TOKEN (or a .env beside this file) and is never printed, logged or written anywhere.

    python3 notion_writeback.py push                       # dry run against the fetched compute/* branches
    python3 notion_writeback.py push --apply
    python3 notion_writeback.py push --run-dir runs/a1 --page 3fb40e33b3f581028459fc3bedef8f95
    python3 notion_writeback.py steer --rows rows.json     # offline: rows exported from the database
    python3 notion_writeback.py steer --write              # needs NOTION_TOKEN
"""
from __future__ import annotations

import argparse
import json
import mimetypes
import os
import subprocess
import sys
import urllib.request
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
JOBS_DIR = os.path.join(REPO, "Docs", "Compute", "jobs")
API = "https://api.notion.com/v1"
VERSION = "2022-06-28"
DATABASE = "569aabc276f8404981d3d6813f64c5eb"       # 🧪 NCA Ecology Lab — Experiment Backlog
PRIORITY = {"P0": 0, "P1": 1, "P2": 2}
METRIC_ORDER = ["best_iou", "stability_iou", "stability_drift", "final_loss", "params", "wall_secs"]


# ── formatting (the runner's, so a Compute result reads like a runner result) ──

def fmt_metrics(m: dict) -> str:
    if not m:
        return "done (no metrics)"
    parts = [f"{k}={m[k]}" for k in METRIC_ORDER if k in m]
    parts += [f"{k}={v}" for k, v in m.items() if k not in METRIC_ORDER and k != "history"]
    return " | ".join(parts)


def row_order(rows: list[dict]) -> list[dict]:
    """The runner's select_next order, as a list: Queued rows, Kill dropped, Prioritize first, then Priority, then age."""
    live = [r for r in rows if r.get("status") in ("Queued", "Running") and (r.get("your_call") or "") != "Kill"]
    return sorted(live, key=lambda r: ((r.get("your_call") or "") != "Prioritize",
                                       PRIORITY.get(r.get("priority"), 9), r.get("created", "")))


# ── the Notion side (stdlib only; dry run unless --apply) ─────────────────────

def _token():
    tok = os.environ.get("NOTION_TOKEN")
    if not tok and os.path.isfile(os.path.join(HERE, ".env")):
        for line in open(os.path.join(HERE, ".env"), encoding="utf-8"):
            k, _, v = line.strip().partition("=")
            if k == "NOTION_TOKEN":
                tok = v.strip().strip('"')
    return tok


class Notion:
    def __init__(self, apply: bool):
        self.apply = apply
        self.sent = []          # (method, path, body) - what a dry run shows and the self-test reads
        self._tok = _token() if apply else None
        if apply and not self._tok:
            raise SystemExit("--apply needs NOTION_TOKEN (environment or Tools/NCA/lab/.env); nothing was sent")

    def _req(self, method, path, body=None, data=None, ctype="application/json"):
        self.sent.append((method, path, body if body is not None else "<file>"))
        if not self.apply:
            print(f"[dry run] {method} {path} " + (json.dumps(body)[:400] if body is not None else "<file bytes>"))
            return {"id": "dry-run", "upload_url": API + "/file_uploads/dry-run/send"}
        url = path if path.startswith("http") else API + path
        payload = data if data is not None else (json.dumps(body).encode() if body is not None else None)
        r = urllib.request.Request(url, data=payload, method=method,
                                   headers={"Authorization": "Bearer " + self._tok, "Notion-Version": VERSION,
                                            "Content-Type": ctype})
        with urllib.request.urlopen(r, timeout=120) as resp:
            return json.loads(resp.read() or b"{}")

    def query_rows(self):
        rows, body = [], {"page_size": 100}
        while True:
            d = self._req("POST", f"/databases/{DATABASE}/query", body)
            for pg in d.get("results", []):
                p = pg.get("properties", {})
                sel = lambda n: ((p.get(n) or {}).get("select") or {}).get("name", "")
                txt = lambda n, t="rich_text": "".join(x.get("plain_text", "") for x in (p.get(n) or {}).get(t, []))
                rows.append({"page_id": pg["id"].replace("-", ""), "created": pg.get("created_time", ""),
                             "name": txt("Name", "title"), "priority": sel("Priority"), "your_call": sel("Your call"),
                             "status": ((p.get("Status") or {}).get("status") or {}).get("name", ""),
                             "config": txt("Config")})
            if not d.get("has_more"):
                return rows
            body = dict(body, start_cursor=d["next_cursor"])

    def set_row(self, page_id, status, result=None):
        props = {"Status": {"status": {"name": status}}}
        if result is not None:
            props["Result"] = {"rich_text": [{"text": {"content": result[:1900]}}]}
        self._req("PATCH", f"/pages/{page_id}", {"properties": props})

    def append_gif(self, page_id, path):
        """The runner's upload (notion_client.upload_gif): a single-part file upload, then an image block. Best effort."""
        name = os.path.basename(path)
        ctype = mimetypes.guess_type(path)[0] or "image/gif"
        try:
            up = self._req("POST", "/file_uploads", {"mode": "single_part", "filename": name, "content_type": ctype})
            boundary = uuid.uuid4().hex
            with open(path, "rb") as f:
                blob = f.read()
            data = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{name}\"\r\n"
                    f"Content-Type: {ctype}\r\n\r\n").encode() + blob + f"\r\n--{boundary}--\r\n".encode()
            self._req("POST", up["upload_url"], data=data, ctype=f"multipart/form-data; boundary={boundary}")
            self._req("PATCH", f"/blocks/{page_id}/children",
                      {"children": [{"type": "image", "image": {"type": "file_upload", "file_upload": {"id": up["id"]}}}]})
            return True
        except Exception as e:      # reporting must never break a writeback: the GIF stays on the results branch
            print(f"[notion] GIF upload skipped ({type(e).__name__})")
            return False


# ── the Compute side (read-only) ─────────────────────────────────────────────

def lab_jobs(jobs_dir=JOBS_DIR):
    """{job id: job} for every job file with at least one unit carrying a notion_page."""
    out = {}
    for name in sorted(os.listdir(jobs_dir)):
        if name.endswith(".json"):
            j = json.load(open(os.path.join(jobs_dir, name), encoding="utf-8"))
            if any("notion_page" in (u.get("args") or {}) for u in (j.get("command") or {}).get("units", [])):
                out[j["id"]] = j
    return out


def _git_show(ref, path):
    p = subprocess.run(["git", "show", f"{ref}:{path}"], cwd=REPO, capture_output=True)
    return p.stdout if p.returncode == 0 else None


def compute_units(jobs, machines=None):
    """[(job, unit, merged unit record, files {name: bytes})] for the lab units with results on any machine."""
    sys.path.insert(0, os.path.join(REPO, "Tools", "Compute"))
    import compute_lib as L
    if machines is None:
        machines = L.gather()
    refs = {f"{m['user']}/{m['host']}": m["ref"] for m in machines}
    out = []
    for jid, job in jobs.items():
        epochs = L.merged_job(machines, jid)
        ek, e = L.current_epoch(epochs)
        if not e:
            continue
        for unit in job["command"]["units"]:
            rec = e["units"].get(unit["id"])
            if not rec:
                continue
            files = {}
            if rec.get("done"):
                for name in ("result.json", "growth.gif"):
                    if name in rec.get("files", []):
                        files[name] = _git_show(refs.get(rec["machine"], ""), f"jobs/{jid}/{ek}/files/{unit['id']}/{name}")
            out.append((job, unit, rec, files))
    return out


# ── push / steer ─────────────────────────────────────────────────────────────

def _state_path():
    home = os.environ.get("FROGLET_COMPUTE_HOME") or os.path.join(os.path.expanduser("~"), ".froglet-compute")
    return os.path.join(home, "nca_lab_notion.json")


def push(notion, items, state):
    """items: [(job, unit, record, files)]. Writes each row once per state (Running, then Done)."""
    for job, unit, rec, files in items:
        page = unit["args"]["notion_page"]
        key = f"{job['id']}/{unit['id']}"
        if rec.get("done"):
            if state.get(key) == "Done":
                continue
            res = json.loads(files["result.json"]) if files.get("result.json") else {}
            text = fmt_metrics(res.get("metrics") or {}) + f" | via Froglet Compute ({rec.get('machine', '?')}, {key})"
            notion.set_row(page, "Done", text)
            if files.get("growth.gif"):
                tmp = os.path.join(os.path.expanduser("~"), ".froglet-compute", "nca_lab_gif", unit["id"] + ".gif")
                os.makedirs(os.path.dirname(tmp), exist_ok=True)
                open(tmp, "wb").write(files["growth.gif"])
                notion.append_gif(page, tmp)
            state[key] = "Done"
        elif float(rec.get("progress") or 0) > 0 and state.get(key) not in ("Running", "Done"):
            notion.set_row(page, "Running")
            state[key] = "Running"
    return state


def push_run_dir(notion, run_dir, page):
    res = json.load(open(os.path.join(run_dir, "result.json")))
    notion.set_row(page, "Done", fmt_metrics(res.get("metrics") or {}))
    gif = os.path.join(run_dir, "growth.gif")
    if os.path.isfile(gif):
        notion.append_gif(page, gif)


def steer(rows, jobs):
    """{job id: [unit dicts in order]} - each lab job's units rebuilt from the backlog: a row belongs to the job whose
    `lab_job` matches its Config's `job`, keeps its existing unit (and so its id and progress) when it has one."""
    import diamond_nca
    out = {}
    for jid, job in jobs.items():
        want = job.get("lab_job")
        have = {u["args"]["notion_page"]: u for u in job["command"]["units"]}
        units = []
        for r in row_order(rows):
            if diamond_nca._parse_config(r.get("config", "")).get("job") != want:
                continue
            u = have.get(r["page_id"]) or {"id": "lab-" + (r["name"].split()[0] or r["page_id"][:8]).lower(),
                                           "args": {"notion_page": r["page_id"], "row": r["name"].split(" ")[0]}}
            u = dict(u, args=dict(u["args"], config=r.get("config", "")))
            units.append(u)
        out[jid] = units
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description="NCA Ecology Lab: Notion backlog <-> Froglet Compute")
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("push")
    p.add_argument("--apply", action="store_true", help="send the requests (default: print them)")
    p.add_argument("--run-dir", help="push one local run folder instead of the compute results")
    p.add_argument("--page", help="the Notion page id for --run-dir")
    s = sub.add_parser("steer")
    s.add_argument("--rows", help="a JSON list of rows (offline); default: read the database (needs NOTION_TOKEN)")
    s.add_argument("--write", action="store_true", help="rewrite the job files' unit lists")
    a = ap.parse_args(argv)
    sys.path.insert(0, HERE)
    if a.cmd == "push":
        notion = Notion(apply=a.apply)
        if a.run_dir:
            if not a.page:
                raise SystemExit("--run-dir needs --page")
            push_run_dir(notion, a.run_dir, a.page)
            return 0
        path = _state_path()
        state = json.load(open(path)) if os.path.isfile(path) else {}
        items = compute_units(lab_jobs())
        print(f"{len(items)} lab unit(s) with results on the compute/* branches")
        state = push(notion, items, state)
        if a.apply:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            json.dump(state, open(path, "w"), indent=1)
        return 0
    rows = json.load(open(a.rows)) if a.rows else Notion(apply=True).query_rows()
    jobs = lab_jobs()
    plan = steer(rows, jobs)
    for jid, units in plan.items():
        before = [u["id"] for u in jobs[jid]["command"]["units"]]
        after = [u["id"] for u in units]
        print(f"{jid}: {before} -> {after}" + ("" if before == after else
              "   (a new unit list starts a new epoch: units in flight lose their relay - Docs/Compute/NCA_LAB.md §5)"))
        if a.write and before != after:
            j = dict(jobs[jid])
            j.pop("_source", None)
            j["command"] = dict(j["command"], units=units)
            with open(os.path.join(JOBS_DIR, jid + ".json"), "w", encoding="utf-8") as f:
                json.dump(j, f, indent=1, sort_keys=True, ensure_ascii=False)
                f.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

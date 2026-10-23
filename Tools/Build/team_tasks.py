#!/usr/bin/env python3
"""team_tasks.py - the team's shared to-do board, Docs/TEAM_TASKS.md (Docs/TEAM.md rule 9).

Any session or person leaves a task another collaborator can pick up: a test that needs the Unity Editor,
a playtest, a dashboard to set up, content to write, a decision to take. A task names nobody ("Who can take
it": anyone) until someone takes it (Shombith, 2026-10-23: no assigning, no standing volunteer offers).

Taking a task names your branch. The task is live while that branch is: `sweep` closes it when the branch merges
into bleeding-edge, and puts it back to open, nobody's, when the branch is deleted unmerged or has had no commit for
14 days. `release` does the same by hand. An open task nobody takes in 14 days is archived, and done and archived
tasks move to Docs/TEAM_TASKS_ARCHIVE.md, so the board holds only what is open or live.

    team_tasks.py list [--all] [--area A] [--for NAME] [--kind K]   open tasks (or every task)
    team_tasks.py add --area A --kind K --task T --done-when D [--who "anyone ..."] [--see S] [--by B]
    team_tasks.py take ID --who NAME --branch BRANCH                 take it; it stays live while BRANCH is
    team_tasks.py release ID [--note N]                              give it back: open, nobody's
    team_tasks.py archive ID... [--note N] | revive ID               off the board into TEAM_TASKS_ARCHIVE.md, or back
    team_tasks.py done ID [--note N]                                 close it by hand
    team_tasks.py sweep [--dry-run] [--no-fetch] [--idle-days 14]    close merged, reopen gone or idle, and strip
                        [--base origin/bleeding-edge]                names from open tasks
    team_tasks.py pr-section [--base origin/bleeding-edge]           tasks this branch added or changed,
                                                                     as markdown for a PR body (/ship)
    team_tasks.py gh-sync [--dry-run] [--repo OWNER/NAME]            mirror the board to GitHub issues
    team_tasks.py --check | --self-test

GitHub issues mirror the board (gh-sync, run by .github/workflows/team-tasks-sync.yml on every push of the board to
bleeding-edge or Ys-bleeding-edge, or by hand). One issue per task, titled "[<id>] <task>", labelled team-task,
kind:<kind>, area:<area> and taken when someone took it. The board is the source: edit it, not the issue body (the
next sync rewrites the body); comment on the issue freely. The sync is MONOTONIC, because several branches carry
copies of the board: it creates, updates and closes (when a task is done), never reopens, and never moves an issue
backwards (open < taken < done). A task closed on GitHub but open on this board is reported, so its board copy can
be closed with `done`.

Ids are T-<date>-<4 hex of the task text>, never "the next number": two branches adding a task on the same
day must not both take T-012 (Docs/TEAM.md, "What happened without this"). Every task is a level-3 heading
`### <id> · <status> · <kind> · <area>` plus bullet fields, so a human edits it as plain markdown and this
script still reads it.
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BOARD = os.path.join(ROOT, "Docs", "TEAM_TASKS.md")
ARCHIVE = os.path.join(ROOT, "Docs", "TEAM_TASKS_ARCHIVE.md")
MARK = "<!-- team-tasks -->"
KINDS = ("test", "dev", "content", "setup", "decision", "docs")
HEAD = re.compile(r"^### (T-\d{8}-[0-9a-f]{4}) · (open|taken by [^·]+?|done|archived) · (" + "|".join(KINDS) + r") · (.+?)\s*$")
FIELD = re.compile(r"^- \*\*([^*]+):\*\* (.*)$")
REQUIRED = ("Task", "Done when", "Who can take it", "Added")
BASE = "origin/bleeding-edge"
IDLE_DAYS = 14   # a live task whose branch has had no commit for this long goes back to open (Shombith, 2026-10-23)


class Task:
    def __init__(self, tid, status, kind, area, fields, lines):
        self.id, self.status, self.kind, self.area, self.fields, self.lines = tid, status, kind, area, fields, lines

    @property
    def is_open(self):
        return self.status not in ("done", "archived")

    def line(self):
        who = self.fields.get("Who can take it", "")
        on = f" on {branch_of(self)}" if branch_of(self) else ""
        return f"{self.id}  [{self.status}{on}]  {self.kind} · {self.area} - {self.fields.get('Task', '')}  (who: {who})"

    def markdown(self):
        return "\n".join(self.lines)


def parse(text):
    """(preamble, tasks). The preamble is everything up to and including the marker line."""
    if MARK not in text:
        raise ValueError(f"no {MARK} marker in the board")
    pre, body = text.split(MARK, 1)
    tasks, cur = [], None
    for raw in body.split("\n"):
        m = HEAD.match(raw)
        if m:
            cur = Task(m.group(1), m.group(2), m.group(3), m.group(4), {}, [raw])
            tasks.append(cur)
            continue
        if raw.startswith("### "):
            raise ValueError(f"malformed task heading: {raw!r}")
        if cur is None:
            continue
        cur.lines.append(raw)
        f = FIELD.match(raw)
        if f:
            cur.fields[f.group(1)] = f.group(2)
    for t in tasks:
        while t.lines and not t.lines[-1].strip():
            t.lines.pop()
    return pre + MARK, tasks


def render(pre, tasks):
    return pre + "\n\n" + "\n\n".join(t.markdown() for t in tasks) + "\n"


# git's conflict markers (diff3's base marker included). Every branch appends to the end of the board, so two branches
# that each add a task conflict there, and a half-resolved merge still PARSES - each side's tasks are well formed - so
# the field checks alone passed a board with the markers in it (measured 2026-10-18, twice on one branch).
CONFLICT = re.compile(r"^(<{7}|\|{7}|>{7})( |$)|^={7}$")


def conflict_lines(text):
    """(line number, line) of every unresolved merge conflict marker in the board."""
    return [(n, line) for n, line in enumerate(text.split("\n"), 1) if CONFLICT.match(line)]


def errors(text):
    markers = [f"line {n}: unresolved merge conflict marker {line[:40]!r} (keep both sides' tasks, then delete the markers)"
               for n, line in conflict_lines(text)]
    try:
        _, tasks = parse(text)
    except ValueError as e:
        return markers + [str(e)]
    out, seen = markers, set()
    for t in tasks:
        if t.id in seen:
            out.append(f"{t.id}: id used twice")
        seen.add(t.id)
        for f in REQUIRED:
            if not t.fields.get(f, "").strip():
                out.append(f"{t.id}: missing **{f}:**")
        if any(ord(c) > 0x2FFF for c in t.markdown()):
            out.append(f"{t.id}: carries an emoji or other symbol the board does not use")
    return out


def load(path=BOARD):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def save(text, path=BOARD):
    bad = errors(text)
    if bad:
        raise SystemExit("refusing to write a board that fails --check:\n  " + "\n  ".join(bad))
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def new_id(task, today):
    return f"T-{today:%Y%m%d}-" + hashlib.sha1(task.strip().lower().encode()).hexdigest()[:4]


def branch():
    try:
        return subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=ROOT, capture_output=True,
                              text=True, check=True).stdout.strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return "unknown"


def unassigned(who):
    """A task names nobody: its "Who can take it" is "anyone", or "anyone ..." saying what the work needs."""
    return who.strip().lower().startswith("anyone")


def add(text, area, kind, task, done_when, who="anyone", see=None, by=None, today=None):
    today = today or datetime.date.today()
    if kind not in KINDS:
        raise SystemExit(f"kind must be one of {', '.join(KINDS)}")
    if not unassigned(who):
        raise SystemExit(f"a task names nobody until someone takes it: pass --who anyone (or \"anyone with ...\" for "
                         f"what the work needs), not {who!r} (Docs/TEAM.md rule 11)")
    pre, tasks = parse(text)
    tid = new_id(task, today)
    if any(t.id == tid for t in tasks):
        raise SystemExit(f"{tid} is already on the board (the same task text was added today)")
    lines = [f"### {tid} · open · {kind} · {area}",
             f"- **Task:** {task}",
             f"- **Done when:** {done_when}",
             f"- **Who can take it:** {who}",
             f"- **Added:** {today.isoformat()} by {by or 'a Claude session'} (`{branch()}`)"]
    if see:
        lines.append(f"- **See:** {see}")
    tasks.append(Task(tid, "open", kind, area, {}, lines))
    return render(pre, tasks), tid


def set_status(text, tid, status, note_field, note, today=None):
    today = today or datetime.date.today()
    pre, tasks = parse(text)
    match = [t for t in tasks if t.id == tid]
    if not match:
        raise SystemExit(f"no task {tid} on the board")
    t = match[0]
    t.lines[0] = HEAD.sub(lambda m: f"### {m.group(1)} · {status} · {m.group(3)} · {m.group(4)}", t.lines[0])
    t.lines.append(f"- **{note_field}:** {today.isoformat()}" + (f", {note}" if note else ""))
    return render(pre, tasks)


# --- Taking a task names its branch; the branch decides when the task leaves -----------------------------------

def find(tasks, tid):
    match = [t for t in tasks if t.id == tid]
    if not match:
        raise SystemExit(f"no task {tid} on the board")
    return match[0]


def set_head(t, status):
    t.lines[0] = HEAD.sub(lambda m: f"### {m.group(1)} · {status} · {m.group(3)} · {m.group(4)}", t.lines[0])
    t.status = status


def set_field(t, name, value):
    """Replace a field's line in place, or append it."""
    line = f"- **{name}:** {value}"
    for i, raw in enumerate(t.lines):
        f = FIELD.match(raw)
        if f and f.group(1) == name:
            t.lines[i] = line
            break
    else:
        t.lines.append(line)
    t.fields[name] = value


def drop_field(t, name):
    t.lines = [raw for raw in t.lines if not (FIELD.match(raw) and FIELD.match(raw).group(1) == name)]
    t.fields.pop(name, None)


def branch_of(t):
    return t.fields.get("Branch", "").strip().strip("`")


def taker(t):
    return t.status[len("taken by "):] if t.status.startswith("taken by ") else ""


def taken_on(t):
    try:
        return datetime.date.fromisoformat(t.fields.get("Taken", "")[:10])
    except ValueError:
        return None


def take(text, tid, who, branch_name, today=None):
    today = today or datetime.date.today()
    if not branch_name.strip():
        raise SystemExit("taking a task names your branch: pass --branch <the branch you do it on>")
    pre, tasks = parse(text)
    t = find(tasks, tid)
    if t.status == "done":
        raise SystemExit(f"{tid} is done")
    if taker(t) and taker(t).lower() != who.lower():
        raise SystemExit(f"{tid} is live: {taker(t)} on {branch_of(t) or 'no branch'}. They release it first.")
    set_head(t, f"taken by {who}")
    set_field(t, "Taken", f"{today.isoformat()}, {who}")
    set_field(t, "Branch", f"`{branch_name.strip()}`")
    return render(pre, tasks)


def reopen(t, why, today):
    """Back to open, nobody's: the taker and branch come off, and a dated line says why."""
    who, name = taker(t), branch_of(t)
    set_head(t, "open")
    drop_field(t, "Branch")
    set_field(t, "Who can take it", "anyone")
    t.lines.append(f"- **Released:** {today.isoformat()}, {who}" + (f" on `{name}`" if name else "") + f": {why}")


def release(text, tid, note=None, today=None):
    today = today or datetime.date.today()
    pre, tasks = parse(text)
    t = find(tasks, tid)
    if not taker(t):
        raise SystemExit(f"{tid} is {t.status}, not taken")
    reopen(t, note or "released", today)
    return render(pre, tasks)


def plan_sweep(tasks, probe, today, idle_days=IDLE_DAYS):
    """What `sweep` does, as (task id, action, detail). Pure, so --self-test proves it with a fake probe.
    probe(branch, taken_date) -> {"exists": bool, "merged": bool, "pr": str|None, "last": date|None}."""
    plan = []
    for t in tasks:
        if t.status == "open":
            waiting = (today - open_since(t)).days
            if waiting >= idle_days:
                plan.append((t.id, "expire", f"not taken in {waiting} days"))
                continue
            who = t.fields.get("Who can take it", "")
            if not unassigned(who):
                plan.append((t.id, "unassign", who))
            continue
        if not taker(t):
            continue
        name = branch_of(t)
        if not name:
            plan.append((t.id, "no-branch", f"taken by {taker(t)} before branches were recorded"))
            continue
        taken = taken_on(t) or today
        p = probe(name, taken)
        if p["merged"]:
            plan.append((t.id, "merged", name + (f" (#{p['pr']})" if p.get("pr") else "")))
        elif not p["exists"]:
            plan.append((t.id, "gone", name))
        else:
            quiet = (today - max(d for d in (p.get("last"), taken) if d)).days
            if quiet >= idle_days:
                plan.append((t.id, "idle", f"{name}, no commit for {quiet} days"))
    return plan


def apply_sweep(text, plan, today, base=BASE):
    pre, tasks = parse(text)
    into = base.split("/", 1)[-1]
    for tid, action, detail in plan:
        t = find(tasks, tid)
        if action == "unassign":
            set_field(t, "Who can take it", "anyone")
        elif action == "merged":
            name, _, pr = detail.partition(" (#")
            set_head(t, "done")
            t.lines.append(f"- **Done:** {today.isoformat()}, `{name}` merged into {into}" + (f" in #{pr.rstrip(')')}" if pr else ""))
        elif action == "gone":
            reopen(t, "the branch was deleted without merging", today)
        elif action == "idle":
            reopen(t, f"{detail.split(', ', 1)[1]}", today)
        elif action == "expire":
            set_head(t, "archived")
            t.lines.append(f"- **Archived:** {today.isoformat()}, {detail}")
    return render(pre, tasks)


def open_since(t):
    """The day an open task started waiting: the day it was added (its id), or the day it was last released."""
    days = [datetime.date(int(t.id[2:6]), int(t.id[6:8]), int(t.id[8:10]))]
    for raw in t.lines:
        f = FIELD.match(raw)
        if f and f.group(1) in ("Released", "Revived"):
            try:
                days.append(datetime.date.fromisoformat(f.group(2)[:10]))
            except ValueError:
                pass
    return max(days)


ARCHIVE_PREAMBLE = ("# Team tasks: archive\n\nDone and archived tasks, moved out of [`TEAM_TASKS.md`](TEAM_TASKS.md) by "
                    "`team_tasks.py sweep` (a task is archived when nobody takes it for 14 days, or by hand with "
                    "`team_tasks.py archive`). `team_tasks.py revive <id>` brings an archived task back to the board.\n\n" + MARK)


def load_archive(path=ARCHIVE):
    return load(path) if os.path.exists(path) else ARCHIVE_PREAMBLE + "\n"


def move_closed(board_text, archive_text):
    """Move done and archived tasks from the board to the archive. Returns (board, archive, moved ids)."""
    pre, tasks = parse(board_text)
    apre, archived = parse(archive_text)
    closed = [t for t in tasks if not t.is_open]
    if not closed:
        return board_text, archive_text, []
    have = {t.id for t in archived}
    archived += [t for t in closed if t.id not in have]
    return render(pre, [t for t in tasks if t.is_open]), render(apre, archived), [t.id for t in closed]


def archive_task(text, tid, note=None, today=None):
    today = today or datetime.date.today()
    pre, tasks = parse(text)
    t = find(tasks, tid)
    if not t.is_open:
        raise SystemExit(f"{tid} is already {t.status}")
    if taker(t):
        raise SystemExit(f"{tid} is live: {taker(t)} on {branch_of(t) or 'no branch'}. Release it first.")
    set_head(t, "archived")
    t.lines.append(f"- **Archived:** {today.isoformat()}" + (f", {note}" if note else ", by hand"))
    return render(pre, tasks)


def revive(board_text, archive_text, tid, today=None):
    """An archived task back on the board: open, nobody's, its 14 days starting again."""
    today = today or datetime.date.today()
    apre, archived = parse(archive_text)
    t = find(archived, tid)
    if t.status != "archived":
        raise SystemExit(f"{tid} is {t.status}; only an archived task comes back")
    set_head(t, "open")
    set_field(t, "Who can take it", "anyone")
    t.lines.append(f"- **Revived:** {today.isoformat()}")
    pre, tasks = parse(board_text)
    return render(pre, tasks + [t]), render(apre, [a for a in archived if a.id != tid])


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True)


def ere(s):
    """Escape a branch name for git's --grep (POSIX extended regex)."""
    return re.sub(r"([.\[\]()*+?{}|^$\\])", r"\\\1", s)


def probe_branch(name, taken, base=BASE):
    """What git knows about a task's branch. Merged = a merge commit on `base`, made on or after the day the task was
    taken, names it (GitHub's "Merge pull request #N from owner/<branch>", or git's "Merge branch '<branch>'"; the date
    matters for long-lived branches such as Ys-bleeding-edge, merged many times), or its tip is on `base` and is newer
    than the day the task was taken (a branch cut from bleeding-edge with no commit yet is not "merged")."""
    exists = git("rev-parse", "--verify", "--quiet", f"refs/remotes/origin/{name}").returncode == 0
    pattern = f"(from [^/ ]+/{ere(name)}$|^Merge (remote-tracking )?branch '(origin/)?{ere(name)}')"
    # An explicit midnight: git reads a bare date as that day at the current time, which missed a 04:19 merge at noon.
    subject = git("log", base, "--merges", "-1", "--format=%s", f"--since={taken.isoformat()} 00:00:00", "-E",
                  f"--grep={pattern}").stdout.strip()
    pr = re.search(r"#(\d+)", subject)
    last = None
    merged = bool(subject)
    if exists:
        stamp = git("log", "-1", "--format=%cs", f"origin/{name}").stdout.strip()
        last = datetime.date.fromisoformat(stamp) if stamp else None
        if not merged and last and last >= taken:
            merged = git("merge-base", "--is-ancestor", f"origin/{name}", base).returncode == 0
    return {"exists": exists, "merged": merged, "pr": pr.group(1) if pr else None, "last": last}


def sweep(dry_run, fetch, base, idle_days, today=None):
    today = today or datetime.date.today()
    if fetch:
        r = git("fetch", "--prune", "--quiet", "origin")
        if r.returncode != 0:
            raise SystemExit(f"git fetch --prune origin failed: {r.stderr.strip()[:300]}")
    text = load()
    plan = plan_sweep(parse(text)[1], lambda name, taken: probe_branch(name, taken, base), today, idle_days)
    words = {"unassign": "unassign (was: {})", "merged": "done: {} merged", "gone": "back to open: {} was deleted",
             "idle": "back to open: {}", "expire": "archived: {}", "no-branch": "left as is: {}"}
    for tid, action, detail in plan:
        print(f"{'would ' if dry_run else ''}{tid}: {words[action].format(detail)}")
    changes = [p for p in plan if p[1] != "no-branch"]
    board, archive, moved = move_closed(apply_sweep(text, changes, today, base), load_archive())
    if not dry_run and (changes or moved):
        save(archive, ARCHIVE)
        save(board)
    counts = {a: sum(1 for p in plan if p[1] == a) for a in words}
    print(f"sweep: {counts['merged']} done, {counts['gone'] + counts['idle']} back to open, {counts['expire']} archived "
          f"after 14 days untaken, {counts['unassign']} unassigned, {counts['no-branch']} taken without a branch; "
          f"{len(moved)} done or archived moved to TEAM_TASKS_ARCHIVE.md" + (" (dry run, nothing written)" if dry_run else ""))


# --- The team hub (a claude.ai page) keeps a copy of the board people act on ---------------------------------
# The page cannot reach git, so a Claude session carries changes across: it reads the page's `tasks` documents into
# a JSON file, `hub-import`s them onto the board, runs `sweep`, then `hub-export`s the board back to the page.

def hub_doc(t, state=None):
    """One board task as the hub's `tasks/<id>` document."""
    who = t.fields.get("Who can take it", "")
    doc = {"id": t.id, "status": t.status if t.status in ("done", "archived") else "live" if taker(t) else "open",
           "kind": t.kind, "area": t.area, "task": t.fields.get("Task", ""), "doneWhen": t.fields.get("Done when", ""),
           "see": t.fields.get("See", ""), "added": t.fields.get("Added", ""),
           "who": who if unassigned(who) or taker(t) else "anyone",
           "takenBy": taker(t), "branch": branch_of(t), "takenAt": t.fields.get("Taken", "")[:10],
           "done": t.fields.get("Done", ""), "released": t.fields.get("Released", ""),
           "archived": t.fields.get("Archived", ""), "revived": t.fields.get("Revived", ""), "updatedBy": "board"}
    if state:
        doc["branchState"] = state
    return doc


def hub_export(text, probe=None, today=None, archive_text=None):
    """The board (and the archive) as hub documents. With `probe`, each live task carries what git says about its
    branch."""
    today = today or datetime.date.today()
    docs = []
    for t in parse(text)[1] + (parse(archive_text)[1] if archive_text else []):
        state = None
        if probe and taker(t) and branch_of(t):
            p = probe(branch_of(t), taken_on(t) or today)
            state = {"exists": p["exists"], "merged": p["merged"], "pr": p.get("pr"),
                     "last": p["last"].isoformat() if p.get("last") else None, "checked": today.isoformat()}
        docs.append(hub_doc(t, state))
    return docs


def hub_import(text, docs, since="", today=None, archive_text=None):
    """Apply what people did in the hub since the last sync (`updatedAt` after `since`) to the board: new tasks,
    takes, releases, closes, archives and revivals. Returns (board text, what changed, archive text)."""
    today = today or datetime.date.today()
    archive_text = archive_text or ARCHIVE_PREAMBLE + "\n"
    done_ids = []
    for d in sorted(docs, key=lambda d: d.get("updatedAt", "")):
        if d.get("updatedBy", "board") == "board" or d.get("updatedAt", "") <= since:
            continue
        pre, tasks = parse(text)
        board = {t.id: t for t in tasks}
        when = datetime.date.fromisoformat((d.get("takenAt") or d.get("updatedAt") or today.isoformat())[:10])
        t = board.get(d["id"])
        in_archive = {a.id: a for a in parse(archive_text)[1]}.get(d["id"])
        if t is None and in_archive is not None:
            if d["status"] in ("open", "live") and in_archive.status == "archived":
                text, archive_text = revive(text, archive_text, d["id"], today)
                done_ids.append(f"{d['id']}: revived")
                t = find(parse(text)[1], d["id"])
            else:
                continue
        if t is None:
            if d.get("status") in ("done", "archived"):
                continue
            if not re.fullmatch(r"T-\d{8}-[0-9a-f]{4}", d.get("id", "")) or d.get("kind") not in KINDS:
                done_ids.append(f"{d.get('id')}: skipped, not a well-formed task")
                continue
            lines = [f"### {d['id']} · open · {d['kind']} · {d['area']}",
                     f"- **Task:** {d['task']}", f"- **Done when:** {d['doneWhen']}",
                     f"- **Who can take it:** {d.get('who') if unassigned(d.get('who', '')) else 'anyone'}",
                     f"- **Added:** {d.get('added') or today.isoformat() + ' in the team hub'}"]
            if d.get("see"):
                lines.append(f"- **See:** {d['see']}")
            tasks.append(Task(d["id"], "open", d["kind"], d["area"], {}, lines))
            text = render(pre, tasks)
            done_ids.append(f"{d['id']}: added")
            t = find(parse(text)[1], d["id"])
        if d["status"] == "live" and (taker(t) != d.get("takenBy") or branch_of(t) != d.get("branch")):
            if taker(t) and taker(t) != d.get("takenBy"):
                text = release(text, t.id, f"taken over in the hub by {d.get('takenBy')}", today)
            text = take(text, t.id, d["takenBy"], d["branch"], when)
            done_ids.append(f"{t.id}: taken by {d['takenBy']} on {d['branch']}")
        elif d["status"] == "open" and taker(t):
            text = release(text, t.id, "released in the team hub", today)
            done_ids.append(f"{t.id}: released")
        elif d["status"] == "done" and t.status != "done":
            text = set_status(text, t.id, "done", "Done", d.get("done") or "closed in the team hub", today)
            done_ids.append(f"{t.id}: done")
        elif d["status"] == "archived" and t.is_open and not taker(t):
            text = archive_task(text, t.id, d.get("archived") or "archived in the team hub", today)
            done_ids.append(f"{t.id}: archived")
    return text, done_ids, archive_text


def pr_section(text, base_text):
    """Markdown naming the tasks this branch added or whose status it changed, for a PR body."""
    _, now = parse(text)
    try:
        before = {t.id: t.status for t in parse(base_text)[1]} if base_text else {}
    except ValueError:
        before = {}
    added = [t for t in now if t.id not in before]
    moved = [t for t in now if t.id in before and before[t.id] != t.status]
    if not added and not moved:
        return ""
    out = ["## Hand-offs (Docs/TEAM_TASKS.md)", ""]
    for t in added:
        out.append(f"- **New** `{t.id}` ({t.kind}, {t.area}): {t.fields.get('Task', '')} "
                   f"Who can take it: {t.fields.get('Who can take it', '')}.")
    for t in moved:
        out.append(f"- `{t.id}` is now **{t.status}**: {t.fields.get('Task', '')}")
    return "\n".join(out)


def git_show(ref):
    try:
        return subprocess.run(["git", "show", f"{ref}:Docs/TEAM_TASKS.md"], cwd=ROOT, capture_output=True,
                              text=True, check=True).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return ""


# --- GitHub issues --------------------------------------------------------------------------------------------

LABEL = "team-task"
LABEL_COLORS = {LABEL: "5319e7", "taken": "fbca04", "kind": "0e8a16", "area": "1d76db"}
RANK = {"open": 0, "taken": 1, "done": 2}


def rank(status):
    return RANK["done"] if status in ("done", "archived") else RANK["taken"] if status.startswith("taken") else RANK["open"]


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")[:40] or "general"


def issue_title(t):
    task = t.fields.get("Task", "")
    return f"[{t.id}] " + (task if len(task) <= 100 else task[:97].rstrip() + "...")


def issue_labels(t):
    labels = [LABEL, f"kind:{t.kind}", f"area:{slug(t.area)}"]
    if t.status.startswith("taken"):
        labels.append("taken")
    return labels


def issue_body(t, repo, ref):
    rows = "\n".join(f"- **{k}:** {v}" for k, v in t.fields.items())
    return (f"**Status on the board:** {t.status}\n\n{rows}\n\n---\n"
            f"Mirrored from [`Docs/TEAM_TASKS.md`](https://github.com/{repo}/blob/{ref}/Docs/TEAM_TASKS.md) by "
            f"`Tools/Build/team_tasks.py gh-sync`. **Edit the board, not this body** (the next sync rewrites it); "
            f"comment here freely. Take it with `team_tasks.py take {t.id} --who <you> --branch <your branch>`; it "
            f"leaves the board when that branch merges (Docs/TEAM.md rule 9).\n<!-- team-task:{t.id} -->")


def body_core(body):
    """The body up to and including its task marker. GitHub-side tooling may append to a body (an attribution
    footer, seen on the first sync), and that must not read as a change, or every sync rewrites every issue."""
    body = (body or "").replace("\r\n", "\n")
    m = re.search(r"<!-- team-task:T-\d{8}-[0-9a-f]{4} -->", body)
    return body[:m.end()] if m else body


def plan_sync(tasks, issues, repo, ref):
    """The GitHub calls that make `issues` mirror `tasks`, as (action, task id, payload, issue number) tuples, plus
    the ids closed on GitHub that this board still has open. Pure, so --self-test proves it without a network."""
    by_id = {}
    for i in issues:
        m = re.match(r"\[(T-\d{8}-[0-9a-f]{4})\]", i.get("title", ""))
        if m:
            by_id[m.group(1)] = i
    actions, behind = [], []
    for t in tasks:
        want = {"title": issue_title(t), "body": issue_body(t, repo, ref), "labels": issue_labels(t)}
        i = by_id.get(t.id)
        if i is None:
            actions.append(("create", t.id, dict(want, state="closed" if not t.is_open else "open"), None))
            continue
        on_gh_closed = i.get("state") == "closed"
        gh_rank = RANK["done"] if on_gh_closed else RANK["taken"] if "taken" in i.get("labels", []) else RANK["open"]
        if rank(t.status) < gh_rank:
            if on_gh_closed:
                behind.append(t.id)
            continue   # monotonic: an older copy of the board never moves an issue backwards
        patch = {k: v for k, v in want.items()
                 if (sorted(i.get(k) or []) != sorted(v) if k == "labels" else
                     body_core(i.get(k)) != body_core(v) if k == "body" else i.get(k) != v)}
        if not t.is_open and not on_gh_closed:
            patch["state"] = "closed"
            patch["state_reason"] = "completed" if t.status == "done" else "not_planned"
        if patch:
            actions.append(("update", t.id, patch, i["number"]))
    return actions, behind


def gh(args, payload=None):
    r = subprocess.run(["gh", "api"] + args + (["--input", "-"] if payload is not None else []),
                       input=json.dumps(payload) if payload is not None else None, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"gh api {' '.join(args)} failed: {r.stderr.strip()[:400]}")
    return r.stdout


def repo_name():
    if os.environ.get("GITHUB_REPOSITORY"):
        return os.environ["GITHUB_REPOSITORY"]
    url = subprocess.run(["git", "remote", "get-url", "origin"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    m = re.search(r"github\.com[/:]([^/]+/[^/.]+)", url)
    if not m:
        raise SystemExit(f"cannot tell the GitHub repository from origin ({url!r}); pass --repo OWNER/NAME")
    return m.group(1)


def gh_sync(repo, dry_run):
    tasks = parse(load())[1] + parse(load_archive())[1]
    ref = os.environ.get("GITHUB_REF_NAME") or branch()
    lines = gh(["--paginate", f"repos/{repo}/issues?labels={LABEL}&state=all&per_page=100",
                "--jq", ".[] | select(.pull_request == null) | {number, title, state, body, labels: [.labels[].name]}"])
    issues = [json.loads(l) for l in lines.splitlines() if l.strip()]
    actions, behind = plan_sync(tasks, issues, repo, ref)
    if not dry_run and actions:
        have = set(gh(["--paginate", f"repos/{repo}/labels?per_page=100", "--jq", ".[].name"]).split("\n"))
        need = sorted({l for _, _, p, _ in actions for l in p.get("labels", [])} - have)
        for name in need:
            color = LABEL_COLORS.get(name.split(":")[0], "ededed")
            gh(["--method", "POST", f"repos/{repo}/labels"], {"name": name, "color": color})
    for action, tid, payload, number in actions:
        what = f"{action} {tid}" + (f" (#{number})" if number else "") + ": " + ", ".join(sorted(payload))
        print(("would " if dry_run else "") + what)
        if dry_run:
            continue
        if action == "create":
            state = payload.pop("state")
            created = json.loads(gh(["--method", "POST", f"repos/{repo}/issues"], payload))
            if state == "closed":
                done = any(t.id == tid and t.status == "done" for t in tasks)
                gh(["--method", "PATCH", f"repos/{repo}/issues/{created['number']}"],
                   {"state": "closed", "state_reason": "completed" if done else "not_planned"})
        else:
            gh(["--method", "PATCH", f"repos/{repo}/issues/{number}"], payload)
    for tid in behind:
        print(f"closed on GitHub, still open on this board: {tid} - close it here with `team_tasks.py done {tid}`")
    print(f"gh-sync: {len(actions)} change(s) {'planned' if dry_run else 'made'}, {len(tasks)} tasks, "
          f"{len(issues)} issues on {repo}")


def self_test():
    bad = []
    seed = "# Board\n\n" + MARK + "\n"
    day = datetime.date(2026, 10, 15)
    t1, a = add(seed, "Activation", "test", "Play the guide once.", "It draws.", "anyone", today=day)
    t2, b = add(t1, "Activation", "setup", "Declare the events.", "UGS lists them.", "anyone", see="x", today=day)
    if a == b or not re.match(r"T-20261015-[0-9a-f]{4}$", a):
        bad.append("ids are not distinct date+hash ids")
    if errors(t2):
        bad.append(f"a board the script wrote fails its own check: {errors(t2)}")
    try:
        add(t2, "Activation", "test", "Play the guide once.", "x", "y", today=day)
        bad.append("the same task added twice on one day was accepted")
    except SystemExit:
        pass
    t3 = set_status(t2, a, "taken by Yash", "Taken", "Yash", today=day)
    t4 = set_status(t3, a, "done", "Done", "passed on 993a3b4dc", today=day)
    tasks = parse(t4)[1]
    if tasks[0].status != "done" or tasks[1].status != "open" or "Done" not in tasks[0].fields:
        bad.append("take/done did not move the status")
    if [t.id for t in tasks if t.is_open] != [b]:
        bad.append("open filter is wrong")
    sec = pr_section(t4, t1)
    if f"**New** `{b}`" not in sec or f"`{a}` is now **done**" not in sec:
        bad.append(f"pr-section missed a new or moved task: {sec!r}")
    if pr_section(t4, t4):
        bad.append("pr-section reported an unchanged board")
    for broken, what in ((t2.replace("- **Done when:** It draws.\n", ""), "a missing field"),
                         (t2 + "\n" + t2.split(MARK)[1].strip() + "\n", "a duplicate id"),
                         (t2.replace(" · test · ", " · testing · "), "an unknown kind"),
                         (t2.replace(MARK, ""), "a missing marker"),
                         (t2.replace("### ", "<<<<<<< HEAD\n### ", 1) + "=======\n>>>>>>> origin/bleeding-edge\n", "a conflict block"),
                         (t2 + ">>>>>>> origin/bleeding-edge\n", "a lone closing conflict marker"),
                         (t2 + "|||||||\n", "a diff3 base marker")):
        if not errors(broken):
            bad.append(f"--check missed {what}")
    tk = parse(t4)[1]   # tasks[0] (a) done, tasks[1] (b) open
    acts, behind = plan_sync(tk, [], "o/r", "main")
    if [x[0] for x in acts] != ["create", "create"] or acts[0][2]["state"] != "closed" or LABEL not in acts[1][2]["labels"]:
        bad.append(f"gh-sync did not create one issue per task with its state and label: {acts}")
    mirrored = [dict(number=n, title=a2[2]["title"], body=a2[2]["body"], labels=a2[2]["labels"], state=a2[2]["state"])
                for n, a2 in enumerate(acts, 1)]
    if plan_sync(tk, mirrored, "o/r", "main") != ([], []):
        bad.append("gh-sync is not idempotent on a mirrored board")
    footed = [dict(m, body=m["body"].replace("\n", "\r\n") + "\r\n\n---\n_Generated by a bot_") for m in mirrored]
    if plan_sync(tk, footed, "o/r", "main") != ([], []):
        bad.append("a footer GitHub appended to a body read as a change")
    older = parse(t2)[1]   # the same two tasks, both still open: an older copy of the board
    acts2, behind2 = plan_sync(older, mirrored, "o/r", "main")
    if any(x[1] == a for x in acts2) or behind2 != [a]:
        bad.append(f"an older board moved a closed issue backwards or did not report it: {acts2} {behind2}")
    taken = parse(t3)[1]
    acts3, _ = plan_sync(taken, [dict(m, state="open") if m["title"].startswith(f"[{a}]") else m for m in mirrored],
                         "o/r", "main")
    if not any(x[1] == a and "taken" in x[2].get("labels", []) for x in acts3):
        bad.append(f"taking a task did not label its issue: {acts3}")
    if errors(t2 + "\n=========\n") or errors(t2 + "\nSee <<<<<<< in the merge notes.\n"):
        bad.append("--check called a line that is not a conflict marker one")
    try:
        save(t2 + "=======\n", path=os.devnull)
        bad.append("save wrote a board with a conflict marker in it")
    except SystemExit:
        pass

    # Tasks name nobody; taking one names its branch; the branch decides when it leaves (Shombith, 2026-10-23).
    for refused, what in ((lambda: add(t2, "UI", "test", "Name a person.", "x", "Yash (offered)", today=day),
                           "add took a person in --who"),
                          (lambda: take(t2, a, "Shombith", " ", today=day), "take took no branch")):
        try:
            refused()
            bad.append(what)
        except SystemExit:
            pass
    board, ids = seed, {}
    for key in ("merged", "gone", "idle", "uncommitted", "active", "legacy", "named", "editor", "stale"):
        # named and editor were added 10 days before the sweep, so they wait; stale waited 20 days, so it expires
        board, ids[key] = add(board, "Tooling", "dev", f"Sweep case {key}.", "It sweeps.",
                              "anyone with the Unity Editor" if key == "editor" else "anyone",
                              today=day + datetime.timedelta(days=10) if key in ("named", "editor") else day)
    for key in ("merged", "gone", "idle", "uncommitted", "active"):
        board = take(board, ids[key], "Shombith", f"claude/{key}", today=day)
    board = set_status(board, ids["legacy"], "taken by Yash", "Taken", "Yash", today=day)
    pre, ts = parse(board)
    set_field(find(ts, ids["named"]), "Who can take it", "Yash (offered)")
    set_field(find(ts, ids["gone"]), "Who can take it", "Yash (testing it)")   # reopening clears a taken task's names too
    board = render(pre, ts)
    later = day + datetime.timedelta(days=20)
    facts = {"claude/merged": dict(exists=False, merged=True, pr="1500", last=None),
             "claude/gone": dict(exists=False, merged=False, pr=None, last=None),
             "claude/idle": dict(exists=True, merged=False, pr=None, last=day + datetime.timedelta(days=2)),
             "claude/uncommitted": dict(exists=True, merged=False, pr=None, last=day - datetime.timedelta(days=5)),
             "claude/active": dict(exists=True, merged=False, pr=None, last=later - datetime.timedelta(days=3))}
    plan = plan_sweep(parse(board)[1], lambda name, taken: facts[name], later)
    want = {ids["merged"]: "merged", ids["gone"]: "gone", ids["idle"]: "idle", ids["uncommitted"]: "idle",
            ids["legacy"]: "no-branch", ids["named"]: "unassign", ids["stale"]: "expire"}
    if {tid: action for tid, action, _ in plan} != want:
        bad.append(f"sweep planned the wrong moves: {plan}")
    swept = apply_sweep(board, [p for p in plan if p[1] != "no-branch"], later)
    ts = {t.id: t for t in parse(swept)[1]}
    if ts[ids["merged"]].status != "done" or "#1500" not in ts[ids["merged"]].fields.get("Done", ""):
        bad.append("a merged branch did not close its task with the PR")
    for key in ("gone", "idle", "uncommitted"):
        t = ts[ids[key]]
        if t.status != "open" or branch_of(t) or t.fields.get("Who can take it") != "anyone" or "Released" not in t.fields:
            bad.append(f"a {key} branch did not put its task back to open, nobody's")
    if ts[ids["active"]].status != "taken by Shombith" or branch_of(ts[ids["active"]]) != "claude/active":
        bad.append("an active branch's task was moved")
    if ts[ids["named"]].fields.get("Who can take it") != "anyone" or \
            ts[ids["editor"]].fields.get("Who can take it") != "anyone with the Unity Editor":
        bad.append("sweep did not strip a name, or stripped what the work needs")
    if ts[ids["legacy"]].status != "taken by Yash" or errors(swept):
        bad.append(f"sweep touched a task taken without a branch, or wrote a bad board: {errors(swept)}")
    if ts[ids["stale"]].status != "archived" or "Archived" not in ts[ids["stale"]].fields:
        bad.append("an open task nobody took for 14 days was not archived")
    if [p[1] for p in plan_sweep(parse(swept)[1], lambda name, taken: facts[name], later)] != ["no-branch"]:
        bad.append("a second sweep changed the board again")
    moved_board, archive, moved = move_closed(swept, ARCHIVE_PREAMBLE + "\n")
    if sorted(moved) != sorted([ids["merged"], ids["stale"]]) or any(not t.is_open for t in parse(moved_board)[1]) or \
            {t.id for t in parse(archive)[1]} != {ids["merged"], ids["stale"]} or errors(archive) or errors(moved_board):
        bad.append(f"done and archived tasks did not move to the archive: {moved}")
    if move_closed(moved_board, archive)[2]:
        bad.append("a second move moved something again")
    back_board, back_archive = revive(moved_board, archive, ids["stale"], today=later)
    revived = find(parse(back_board)[1], ids["stale"])
    if revived.status != "open" or ids["stale"] in {t.id for t in parse(back_archive)[1]} or \
            any(p[0] == ids["stale"] for p in plan_sweep(parse(back_board)[1], lambda n, k: facts[n], later)):
        bad.append("a revived task did not come back open with its 14 days starting again")
    for refused, what in ((lambda: revive(moved_board, archive, ids["merged"], today=later), "revived a done task"),
                          (lambda: archive_task(swept, ids["active"], today=later), "archived a live task")):
        try:
            refused()
            bad.append(what)
        except SystemExit:
            pass
    if plan_sync(parse(archive)[1], [], "o/r", "main")[0][1][2]["state"] != "closed":
        bad.append("gh-sync did not close an archived task's issue")
    back = parse(release(swept, ids["active"], "plans changed", today=later))[1]
    if find(back, ids["active"]).status != "open" or branch_of(find(back, ids["active"])):
        bad.append("release did not put the task back to open without its branch")
    try:
        take(swept, ids["active"], "Garrett", "claude/other", today=later)
        bad.append("a live task was taken from under its taker")
    except SystemExit:
        pass
    if branch_of(find(parse(take(swept, ids["active"], "Shombith", "claude/renamed", today=later))[1],
                      ids["active"])) != "claude/renamed":
        bad.append("the taker could not move their task to another branch")
    if not re.fullmatch(r"claude/a\\\.b", ere("claude/a.b")):
        bad.append("a branch name is not escaped for git --grep")

    # The hub round trip: an export imported back changes nothing; hub actions land on the board.
    docs = hub_export(swept)
    if hub_import(swept, docs)[1]:
        bad.append("importing the board's own export changed it")
    if any(d["status"] == "open" and not unassigned(d["who"]) for d in hub_export(board)):
        bad.append("hub-export put a name on an open task")
    stamp = "2026-11-06T10:00:00Z"
    by_id = {d["id"]: dict(d) for d in docs}
    by_id[ids["named"]].update(status="live", takenBy="Caleb", branch="claude/caleb", takenAt="2026-11-06",
                               updatedBy="Caleb", updatedAt=stamp)
    by_id[ids["active"]].update(status="open", takenBy="", branch="", updatedBy="Yash", updatedAt=stamp)
    by_id[ids["editor"]].update(status="done", done="ran it", updatedBy="Yash", updatedAt=stamp)
    by_id["T-20261106-abcd"] = dict(id="T-20261106-abcd", status="open", kind="test", area="UI", task="From the hub.",
                                    doneWhen="It shows.", who="anyone", see="", added="2026-11-06 by Garrett in the team hub",
                                    updatedBy="Garrett", updatedAt=stamp)
    by_id["T-bad"] = dict(id="T-bad", status="open", kind="test", updatedBy="Garrett", updatedAt=stamp)
    hub_board, changed, _ = hub_import(swept, list(by_id.values()), since="2026-11-01T00:00:00Z", today=later)
    ts = {t.id: t for t in parse(hub_board)[1]}
    if (taker(ts[ids["named"]]), branch_of(ts[ids["named"]])) != ("Caleb", "claude/caleb") or \
            ts[ids["active"]].status != "open" or ts[ids["editor"]].status != "done" or \
            "T-20261106-abcd" not in ts or "T-bad" in ts or errors(hub_board):
        bad.append(f"hub-import did not apply the hub's take, release, close and add: {changed}")
    if hub_import(swept, list(by_id.values()), since="2026-11-07T00:00:00Z", today=later)[1]:
        bad.append("hub-import applied changes older than the last sync")
    gone_doc = dict(hub_doc(find(parse(moved_board)[1], ids["named"])), status="archived", updatedBy="Yash", updatedAt=stamp)
    b2, c2, a2 = hub_import(moved_board, [gone_doc], since="2026-11-01T00:00:00Z", today=later, archive_text=archive)
    b2, a2, _ = move_closed(b2, a2)
    back_doc = dict(gone_doc, status="open", updatedAt="2026-11-08T10:00:00Z")
    b3, c3, a3 = hub_import(b2, [back_doc], since="2026-11-07T00:00:00Z", today=later, archive_text=a2)
    if ids["named"] in {t.id for t in parse(b2)[1]} or find(parse(b3)[1], ids["named"]).status != "open" or \
            ids["named"] in {t.id for t in parse(a3)[1]}:
        bad.append(f"the hub's archive or bring-back did not reach the board: {c2} {c3}")
    if os.path.exists(BOARD):
        shipped = errors(load())
        if shipped:
            bad.append(f"the shipped board fails --check: {shipped}")
    print("team_tasks self-test: " + ("ok" if not bad else "FAILED: " + "; ".join(bad)))
    return 0 if not bad else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" in argv:
        bad = errors(load()) + [f"archive: {e}" for e in (errors(load(ARCHIVE)) if os.path.exists(ARCHIVE) else [])]
        for e in bad:
            print("ERROR: " + e)
        archived = len(parse(load(ARCHIVE))[1]) if os.path.exists(ARCHIVE) and not bad else 0
        print("team tasks: " + ("FAIL" if bad else f"ok ({len(parse(load())[1])} on the board, {archived} archived)"))
        return 1 if bad else 0

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    ls = sub.add_parser("list")
    ls.add_argument("--all", action="store_true")
    ls.add_argument("--area")
    ls.add_argument("--for", dest="person")
    ls.add_argument("--kind", choices=KINDS)
    ad = sub.add_parser("add")
    for f in ("area", "task", "done-when"):
        ad.add_argument("--" + f, required=True)
    ad.add_argument("--who", default="anyone", help='"anyone", or "anyone with ..." for what the work needs')
    ad.add_argument("--kind", required=True, choices=KINDS)
    ad.add_argument("--see")
    ad.add_argument("--by")
    tk = sub.add_parser("take")
    tk.add_argument("id")
    tk.add_argument("--who", required=True)
    tk.add_argument("--branch", required=True, help="the branch you do it on; the task leaves the board when it merges")
    ar = sub.add_parser("archive", help="take an open task off the board (into TEAM_TASKS_ARCHIVE.md)")
    ar.add_argument("ids", nargs="+")
    ar.add_argument("--note")
    rv = sub.add_parser("revive", help="bring an archived task back to the board, open and nobody's")
    rv.add_argument("id")
    rl = sub.add_parser("release")
    rl.add_argument("id")
    rl.add_argument("--note")
    dn = sub.add_parser("done")
    dn.add_argument("id")
    dn.add_argument("--note")
    sw = sub.add_parser("sweep")
    sw.add_argument("--dry-run", action="store_true")
    sw.add_argument("--no-fetch", action="store_true", help="skip `git fetch --prune origin` (branch state may be stale)")
    sw.add_argument("--base", default=BASE)
    sw.add_argument("--idle-days", type=int, default=IDLE_DAYS)
    he = sub.add_parser("hub-export", help="print the board as the team hub's task documents (JSON)")
    he.add_argument("--probe", action="store_true", help="add each live task's branch state from git")
    he.add_argument("--base", default=BASE)
    hi = sub.add_parser("hub-import", help="apply what people did in the team hub to the board")
    hi.add_argument("file", help="JSON: the hub's task documents, as a list or {\"tasks\": [...]}")
    hi.add_argument("--since", default="", help="the last sync's time (ISO); older hub changes are skipped")
    hi.add_argument("--dry-run", action="store_true")
    gs = sub.add_parser("gh-sync")
    gs.add_argument("--dry-run", action="store_true")
    gs.add_argument("--repo")
    pr = sub.add_parser("pr-section")
    pr.add_argument("--base", default="origin/bleeding-edge")
    a = ap.parse_args(argv)

    text = load()
    if a.cmd == "list":
        for t in parse(text)[1] + (parse(load_archive())[1] if a.all else []):
            if not a.all and not t.is_open:
                continue
            if a.area and a.area.lower() not in t.area.lower():
                continue
            if a.kind and t.kind != a.kind:
                continue
            if a.person and a.person.lower() not in (t.fields.get("Who can take it", "") + t.status).lower():
                continue
            print(t.line())
    elif a.cmd == "add":
        text, tid = add(text, a.area, a.kind, a.task, a.done_when, a.who, a.see, a.by)
        save(text)
        print(f"added {tid}")
    elif a.cmd == "take":
        save(take(text, a.id, a.who, a.branch))
        if git("rev-parse", "--verify", "--quiet", f"refs/remotes/origin/{a.branch}").returncode != 0:
            print(f"note: origin/{a.branch} does not exist yet; push it, or `sweep` reads the task as deleted and reopens it")
        print(f"{a.id} taken by {a.who} on {a.branch}")
    elif a.cmd == "release":
        save(release(text, a.id, a.note))
        print(f"{a.id} is open again, nobody's")
    elif a.cmd == "done":
        save(set_status(text, a.id, "done", "Done", a.note))
        print(f"{a.id} done")
    elif a.cmd == "sweep":
        sweep(a.dry_run, not a.no_fetch, a.base, a.idle_days)
    elif a.cmd == "archive":
        for tid in a.ids:
            text = archive_task(text, tid, a.note)
        board, archive, moved = move_closed(text, load_archive())
        save(archive, ARCHIVE)
        save(board)
        print(f"archived {len(a.ids)}: {' '.join(a.ids)}")
    elif a.cmd == "revive":
        board, archive = revive(text, load_archive(), a.id)
        save(board)
        save(archive, ARCHIVE)
        print(f"{a.id} is back on the board, open and nobody's")
    elif a.cmd == "hub-export":
        probe = (lambda name, taken: probe_branch(name, taken, a.base)) if a.probe else None
        print(json.dumps({"exportedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
                          "tasks": hub_export(text, probe, archive_text=load_archive())}, indent=1, ensure_ascii=False))
    elif a.cmd == "hub-import":
        with open(a.file, encoding="utf-8") as fh:
            docs = json.load(fh)
        new_text, changes, archive = hub_import(text, docs["tasks"] if isinstance(docs, dict) else docs, a.since,
                                                archive_text=load_archive())
        board, archive, _ = move_closed(new_text, archive)
        for c in changes:
            print(("would " if a.dry_run else "") + c)
        if changes and not a.dry_run:
            save(archive, ARCHIVE)
            save(board)
        print(f"hub-import: {len(changes)} change(s)" + (" (dry run, nothing written)" if a.dry_run else ""))
    elif a.cmd == "gh-sync":
        gh_sync(a.repo or repo_name(), a.dry_run)
    elif a.cmd == "pr-section":
        print(pr_section(text, git_show(a.base)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

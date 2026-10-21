#!/usr/bin/env python3
"""team_tasks.py - the team's shared to-do board, Docs/TEAM_TASKS.md (Docs/TEAM.md rule 9).

Any session or person leaves a task another collaborator can pick up: a test that needs the Unity Editor,
a playtest, a dashboard to set up, content to write, a decision to take. Yash has offered to take tasks
(Yash, 2026-10-15); a task he could do names him in "Who can take it".

    team_tasks.py list [--all] [--area A] [--for NAME] [--kind K]   open tasks (or every task)
    team_tasks.py add --area A --kind K --task T --done-when D --who W [--see S] [--by B]
    team_tasks.py take ID --who NAME                                 mark a task as yours
    team_tasks.py done ID [--note N]                                 close it
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
MARK = "<!-- team-tasks -->"
KINDS = ("test", "dev", "content", "setup", "decision", "docs")
HEAD = re.compile(r"^### (T-\d{8}-[0-9a-f]{4}) · (open|taken by [^·]+?|done) · (" + "|".join(KINDS) + r") · (.+?)\s*$")
FIELD = re.compile(r"^- \*\*([^*]+):\*\* (.*)$")
REQUIRED = ("Task", "Done when", "Who can take it", "Added")


class Task:
    def __init__(self, tid, status, kind, area, fields, lines):
        self.id, self.status, self.kind, self.area, self.fields, self.lines = tid, status, kind, area, fields, lines

    @property
    def is_open(self):
        return self.status != "done"

    def line(self):
        who = self.fields.get("Who can take it", "")
        return f"{self.id}  [{self.status}]  {self.kind} · {self.area} - {self.fields.get('Task', '')}  (who: {who})"

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


def add(text, area, kind, task, done_when, who, see=None, by=None, today=None):
    today = today or datetime.date.today()
    if kind not in KINDS:
        raise SystemExit(f"kind must be one of {', '.join(KINDS)}")
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
    return RANK["done"] if status == "done" else RANK["taken"] if status.startswith("taken") else RANK["open"]


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
            f"comment here freely. Take it with `team_tasks.py take {t.id} --who <you>`, close it with "
            f"`team_tasks.py done {t.id} --note ...` (Docs/TEAM.md rule 9).\n<!-- team-task:{t.id} -->")


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
            actions.append(("create", t.id, dict(want, state="closed" if t.status == "done" else "open"), None))
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
        if t.status == "done" and not on_gh_closed:
            patch["state"] = "closed"
            patch["state_reason"] = "completed"
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
    tasks = parse(load())[1]
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
                gh(["--method", "PATCH", f"repos/{repo}/issues/{created['number']}"],
                   {"state": "closed", "state_reason": "completed"})
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
    t1, a = add(seed, "Activation", "test", "Play the guide once.", "It draws.", "Yash (offered)", today=day)
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
        bad = errors(load())
        for e in bad:
            print("ERROR: " + e)
        print("team tasks: " + ("FAIL" if bad else f"ok ({len(parse(load())[1])} tasks)"))
        return 1 if bad else 0

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    ls = sub.add_parser("list")
    ls.add_argument("--all", action="store_true")
    ls.add_argument("--area")
    ls.add_argument("--for", dest="person")
    ls.add_argument("--kind", choices=KINDS)
    ad = sub.add_parser("add")
    for f in ("area", "task", "done-when", "who"):
        ad.add_argument("--" + f, required=True)
    ad.add_argument("--kind", required=True, choices=KINDS)
    ad.add_argument("--see")
    ad.add_argument("--by")
    tk = sub.add_parser("take")
    tk.add_argument("id")
    tk.add_argument("--who", required=True)
    dn = sub.add_parser("done")
    dn.add_argument("id")
    dn.add_argument("--note")
    gs = sub.add_parser("gh-sync")
    gs.add_argument("--dry-run", action="store_true")
    gs.add_argument("--repo")
    pr = sub.add_parser("pr-section")
    pr.add_argument("--base", default="origin/bleeding-edge")
    a = ap.parse_args(argv)

    text = load()
    if a.cmd == "list":
        for t in parse(text)[1]:
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
        save(set_status(text, a.id, f"taken by {a.who}", "Taken", a.who))
        print(f"{a.id} taken by {a.who}")
    elif a.cmd == "done":
        save(set_status(text, a.id, "done", "Done", a.note))
        print(f"{a.id} done")
    elif a.cmd == "gh-sync":
        gh_sync(a.repo or repo_name(), a.dry_run)
    elif a.cmd == "pr-section":
        print(pr_section(text, git_show(a.base)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

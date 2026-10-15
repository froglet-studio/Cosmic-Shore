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
    team_tasks.py --check | --self-test

Ids are T-<date>-<4 hex of the task text>, never "the next number": two branches adding a task on the same
day must not both take T-012 (Docs/TEAM.md, "What happened without this"). Every task is a level-3 heading
`### <id> · <status> · <kind> · <area>` plus bullet fields, so a human edits it as plain markdown and this
script still reads it.
"""
import argparse
import datetime
import hashlib
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


def errors(text):
    try:
        _, tasks = parse(text)
    except ValueError as e:
        return [str(e)]
    out, seen = [], set()
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
                         (t2.replace(MARK, ""), "a missing marker")):
        if not errors(broken):
            bad.append(f"--check missed {what}")
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
    elif a.cmd == "pr-section":
        print(pr_section(text, git_show(a.base)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

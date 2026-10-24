#!/usr/bin/env python3
"""team_tasks.py - Froglet Tasks: the team's hand-off tickets (Docs/TEAM.md rule 9), one GitHub issue each.

A session or person leaves a ticket another collaborator can pick up: a test that needs the Unity Editor, a playtest,
a dashboard to set up, content to write, a decision to take. The rules (Shombith, 2026-10-23 and 2026-10-25):

1. A ticket names nobody until someone takes it. `add` refuses a person; `sweep` clears any assignee set on GitHub.
2. Taking a ticket names your branch. A live ticket shows the person and the branch.
3. When that branch merges into bleeding-edge the ticket closes by itself. If the branch is deleted, released, or has
   no commit for 14 days, the ticket goes back to open, nobody's.
4. A ticket nobody takes in 14 days is archived (closed, not planned, label `archived`). Done tickets close as
   completed. Both come back with `revive`, and the 14 days start again.
5. One ticket per branch: `add` appends a checklist line to this branch's open ticket when it has one.
6. The core four carry equal weight. Priority ranks tickets and milestones rank time; nothing ranks people.
7. Sessions add, close and classify only when a person asks (the /team-tasks skill). The rule set classifies a ticket
   once, when it is filed; a person's `set` is never overwritten by anything automatic.

Where things live
  GitHub Issues hold the tickets: title "[<id>] <task>"; labels team-task, kind:*, area:*, dept:*, prio:*, `live`
  while taken, `archived`; a milestone; never an assignee. The body is the task, a "Done when" checklist (tick it on
  GitHub freely), links, and a machine block (<!-- team-task ... -->) this script owns.
  .github/workflows/team-tasks.yml runs `sweep` when a pull request merges, a branch is deleted, bleeding-edge moves,
  and nightly. The Froglet Tasks page (PAGE_URL) is the board people read; a scheduled Claude session runs `sync` to
  carry the page's takes and overrides to Issues and the issues back to the page.

    team_tasks.py list [--dept D] [--area A] [--milestone M] [--all] [--json] [--md]
    team_tasks.py add --area A --kind K --task T --done-when D --priority P --dept D --why W
                      [--milestone M] [--see S] [--by B] [--who "anyone with ..."]
    team_tasks.py step --line "..." [--id ID]           a checklist line on this branch's ticket
    team_tasks.py take ID --branch B [--who NAME]       who defaults to `me`
    team_tasks.py release ID [--note N] | done ID --note N | archive ID... [--note N] | revive ID
    team_tasks.py set ID priority|dept|milestone VALUE --by NAME [--why W]     VALUE "clear" hands it back to the rule
    team_tasks.py sweep [--dry-run] [--no-fetch] [--base origin/bleeding-edge] [--idle-days 14] [--merged BRANCH --pr N]
    team_tasks.py sync FILE [--since ISO] [--dry-run]   the page's store <-> Issues
    team_tasks.py pr-section [--base origin/bleeding-edge]
    team_tasks.py migrate [--dry-run]                   one shot: the markdown board -> Issues
    team_tasks.py review [--all | --area A | ID...] [--batch N] | review --apply FILE [--dry-run]
    team_tasks.py milestone list | milestone add NAME --gate TEXT [--due YYYY-MM-DD]
    team_tasks.py me
    team_tasks.py --self-test
  Every command takes --repo OWNER/NAME (default: origin) and --offline FILE (run on a saved dump of the issues
  instead of GitHub; writes go back to the file).

Ids are T-<date>-<4 hex of the task text>, never "the next number": two branches filing on the same day must not both
take T-012 (Docs/TEAM.md, "What happened without this"). GitHub's issue numbers are the second handle.
"""
import argparse
import collections
import contextlib
import datetime
import hashlib
import io
import json
import os
import re
import subprocess
import sys
import urllib.parse

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BOARD = os.path.join(ROOT, "Docs", "TEAM_TASKS.md")             # the old markdown board, read by migrate and the bridge
ARCHIVE = os.path.join(ROOT, "Docs", "TEAM_TASKS_ARCHIVE.md")
TEAM = os.path.join(ROOT, "Docs", "TEAM.json")
PAGE_URL = "https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f"
KINDS = ("test", "dev", "content", "setup", "decision", "docs")
BASE = "origin/bleeding-edge"
IDLE_DAYS = 14   # a live task whose branch has had no commit for this long goes back to open (Shombith, 2026-10-23)
# Branches whose copy of the old markdown board still gains tasks until they merge bleeding-edge; the sweep files
# any task it finds there that has no issue yet, and migrate reads them.
BRIDGE_REFS = ("origin/bleeding-edge", "origin/Ys-bleeding-edge")
# Many sessions file from these, about unrelated work: "one ticket per branch" does not fold their tasks together.
SHARED_BRANCHES = ("bleeding-edge", "Ys-bleeding-edge", "main", "master", "development", "HEAD", "unknown")
OLD_WORKFLOW = "team-tasks-sync.yml"

LABEL = "team-task"
DEPTS = {"testers": "Testers", "developers": "Developers", "decisions": "Decisions", "content": "Content & docs"}
DEPT_OF_KIND = {"test": "testers", "dev": "developers", "setup": "developers", "decision": "decisions",
                "content": "content", "docs": "content"}
PRIORITIES = ("P1", "P2", "P3")
# key, name, gate (the exit criterion), scope. The X% values are unset on purpose: the plan has not set them.
MILESTONES = (
    ("M1", "Closed testing, all platforms", "co-op repeat retention over weeks with the same experience",
     "Every vessel and every game mode tested on every platform, with the performance fixing that makes that "
     "possible; alpha access; friends, family and Tadpole feedback; instrumented data."),
    ("M2", "Beta phase 2, limited keys", "X% of new users play again after 1 week",
     "Pre-release access with FFT feedback; limited beta keys, one at a time; activation data."),
    ("M3", "Beta phase 3", "X% of users claim they would give a 5-star review when surveyed",
     "Alpha access with Tadpole feedback; beta access with limited keys; activation, retention and referral data."),
    ("M4", "Early access, $15", "feature complete: only content from here",
     "The same loop, paid. Activation, retention and referral data."),
    ("M5", "Full release, free to play", "",
     "Then Game 2 design, and everlasting content, bug fixing and balance updates."),
)
MILESTONE_KEYS = tuple(m[0] for m in MILESTONES)
LABEL_COLORS = {LABEL: "5319e7", "live": "fbca04", "archived": "cfd3d7", "kind": "0e8a16", "area": "1d76db",
                "dept": "c5def5", "prio:P1": "d73a4a", "prio:P2": "f9a03f", "prio:P3": "ededed"}
OUR_LABELS = {LABEL, "live", "archived", "taken"}
OUR_PREFIXES = ("kind:", "area:", "dept:", "prio:")
AUTOMATIC = ("rule", "kind", "claude")   # set-by values that are not a person
FOOTER = ("_Froglet Tasks: nobody's until someone takes it, naming their branch; it closes when that branch merges. "
          "Take it, close it or re-rank it on the Froglet Tasks page or with `/team-tasks`._")

FIELD = re.compile(r"^- \*\*([^*]+):\*\* (.*)$")
ID_RE = re.compile(r"T-\d{8}-[0-9a-f]{4}")
TITLE_ID = re.compile(r"^\[(T-\d{8}-[0-9a-f]{4})\]\s*")
BLOCK = re.compile(r"<!-- team-task\n(.*?)\n-->", re.S)
LEGACY = re.compile(r"<!-- team-task:(T-\d{8}-[0-9a-f]{4}) -->")
STEP = re.compile(r"^\s*[-*] \[( |x|X)\] (.*)$")


# --- The old markdown board, read only: migrate moves it to Issues, and the sweep files tasks that branches still
# --- append to it after the switch --------------------------------------------------------------------------------

MARK = "<!-- team-tasks -->"
HEAD = re.compile(r"^### (T-\d{8}-[0-9a-f]{4}) · (open|taken by [^·]+?|done|archived) · (" + "|".join(KINDS) + r") · (.+?)\s*$")


class Task:
    """One task of the old markdown board."""
    def __init__(self, tid, status, kind, area, fields, lines):
        self.id, self.status, self.kind, self.area, self.fields, self.lines = tid, status, kind, area, fields, lines

    @property
    def is_open(self):
        return self.status not in ("done", "archived")


def parse(text):
    """The old board's tasks. Text without the marker (a stub, a missing file) has none."""
    if MARK not in text:
        return []
    tasks, cur = [], None
    for raw in text.split(MARK, 1)[1].split("\n"):
        m = HEAD.match(raw)
        if m:
            cur = Task(m.group(1), m.group(2), m.group(3), m.group(4), {}, [raw])
            tasks.append(cur)
        elif cur is not None and not raw.startswith("### "):
            cur.lines.append(raw)
            f = FIELD.match(raw)
            if f:
                cur.fields[f.group(1)] = f.group(2)
    return tasks


def read(path):
    try:
        with open(path, encoding="utf-8") as fh:
            return fh.read()
    except OSError:
        return ""


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True)


def git_show(ref, path="Docs/TEAM_TASKS.md"):
    r = git("show", f"{ref}:{path}")
    return r.stdout if r.returncode == 0 else ""


def markdown_tasks(refs=BRIDGE_REFS):
    """Every task on the old boards: this checkout's board and archive, then each ref's. Where an id appears twice the
    copy furthest along wins (done or archived over taken over open), so a stale copy never moves a task back."""
    rank = lambda t: 2 if not t.is_open else 1 if t.status.startswith("taken") else 0
    out = {}
    texts = [read(BOARD), read(ARCHIVE)]
    for ref in refs:
        texts += [git_show(ref), git_show(ref, "Docs/TEAM_TASKS_ARCHIVE.md")]
    for text in texts:
        for t in parse(text):
            if t.id not in out or rank(t) > rank(out[t.id]):
                out[t.id] = t
    return out


# --- A ticket -------------------------------------------------------------------------------------------------------

def new_id(task, today):
    return f"T-{today:%Y%m%d}-" + hashlib.sha1(task.strip().lower().encode()).hexdigest()[:4]


def branch():
    r = git("rev-parse", "--abbrev-ref", "HEAD")
    return r.stdout.strip() if r.returncode == 0 else "unknown"


def unassigned(who):
    """A task names nobody: its "Who can take it" is "anyone", or "anyone ..." saying what the work needs."""
    return who.strip().lower().startswith("anyone")


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")[:40] or "general"


def clean(value):
    """One line that is safe inside the machine block."""
    return re.sub(r"\s+", " ", str(value or "")).replace("-->", "->").replace(" | ", " / ").strip()


def by_person(set_by):
    return bool(set_by) and set_by.split(" ")[0] not in AUTOMATIC


class Ticket:
    """One task. On GitHub it is an issue: the title, the visible body a person may edit (task, checklist, links), the
    labels, the milestone, and the machine block at the end of the body that this script owns."""

    def __init__(self, tid="", kind="dev", area="General", task="", steps=None, see="", needs="anyone",
                 filed_date="", filed_by="", filed_on=""):
        self.id, self.kind, self.area, self.task = tid, kind, area, task
        self.steps = steps or []                       # [[ticked, text], ...]
        self.see, self.needs = see, needs
        self.filed_date, self.filed_by, self.filed_on = filed_date, filed_by, filed_on
        self.state = "open"                            # open | live | done | archived | merged
        self.taken_by = self.branch = self.taken = ""
        self.released = self.revived = self.done = self.archived = self.merged_into = ""
        self.priority, self.prio_why, self.prio_by = "P3", "", ""
        self.dept, self.dept_by = DEPT_OF_KIND.get(kind, "developers"), ""
        self.milestone, self.ms_why, self.ms_by = "", "", ""
        self.number, self.url, self.raw, self.updated = None, "", None, ""
        self.assignees, self.legacy, self.adopt, self.notes = [], False, False, []

    # The shape the sweep's planning reads (plan_sweep, open_since): the old board's status and fields.
    @property
    def status(self):
        return f"taken by {self.taken_by}" if self.state == "live" else self.state

    @property
    def is_open(self):
        return self.state in ("open", "live")

    @property
    def fields(self):
        f = {"Task": self.task, "Done when": "; ".join(s for _, s in self.steps), "Added": self.filed_date,
             "Who can take it": "assigned on GitHub: " + ", ".join(self.assignees) if self.assignees else self.needs}
        for name, value in (("Branch", f"`{self.branch}`" if self.branch else ""),
                            ("Taken", f"{self.taken}, {self.taken_by}" if self.taken else ""),
                            ("Released", self.released), ("Revived", self.revived), ("Done", self.done),
                            ("Archived", self.archived), ("See", self.see)):
            if value:
                f[name] = value
        return f

    @property
    def lines(self):
        return [f"- **{k}:** {v}" for k, v in self.fields.items()]

    @property
    def title(self):
        return (self.task.strip().split("\n") or [""])[0]

    def line(self):
        live = f"  live: {self.taken_by} on {self.branch or 'no branch'}" if self.state == "live" else ""
        return f"{self.id}  {self.priority}  {DEPTS.get(self.dept, self.dept):<14} {self.area[:18]:<18} " \
               f"{self.title[:90]}{live}"


def branch_of(t):
    return t.fields.get("Branch", "").strip().strip("`")


def taker(t):
    return t.status[len("taken by "):] if t.status.startswith("taken by ") else ""


def taken_on(t):
    try:
        return datetime.date.fromisoformat(t.fields.get("Taken", "")[:10])
    except ValueError:
        return None


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


def split_steps(done_when):
    return [s.strip() for s in re.split(r";\s+", done_when) if s.strip()]


# --- The issue body: visible part (a person may edit it) + machine block (this script's) ---------------------------

def block_lines(t):
    def row(key, value, **sub):
        s = f"{key}:" + (f" {clean(value)}" if clean(value) else "")
        for k, v in sub.items():
            if v:
                s += f" | {k.replace('_', '-')}: {clean(v)}"
        return s
    return [row("id", t.id), row("kind", t.kind), row("area", t.area),
            row("filed", t.filed_date, by=t.filed_by, branch=t.filed_on), row("needs", t.needs),
            row("taken-by", t.taken_by), row("branch", t.branch), row("taken", t.taken),
            row("released", t.released), row("revived", t.revived), row("done", t.done), row("archived", t.archived),
            row("merged-into", t.merged_into),
            row("priority", t.priority, why=t.prio_why, set_by=t.prio_by),
            row("dept", t.dept, set_by=t.dept_by),
            row("milestone", t.milestone, why=t.ms_why, set_by=t.ms_by)]


def parse_block(text):
    out = {}
    for line in text.split("\n"):
        key, sep, rest = line.partition(":")
        if not sep:
            continue
        parts = rest.split(" | ")
        sub = {}
        for p in parts[1:]:
            k, s, v = p.partition(":")
            if s:
                sub[k.strip()] = v.strip()
        out[key.strip()] = (parts[0].strip(), sub)
    return out


def render_body(t):
    out = [t.task.strip(), ""]
    if t.needs.strip().lower() != "anyone":
        out += [f"**Who can take it** {t.needs.strip()}", ""]
    if t.steps:
        out += ["**Done when**"] + [f"- [{'x' if done else ' '}] {text}" for done, text in t.steps] + [""]
    if t.see:
        out += [f"**See** {t.see}", ""]
    return "\n".join(out + [FOOTER, "", "<!-- team-task", *block_lines(t), "-->"])


def parse_visible(text):
    """(task, needs, steps, see) from the part of a body a person reads and may edit on GitHub."""
    task, steps, see, needs, where = [], [], [], "", "task"
    for line in text.split("\n"):
        s = line.strip()
        if s.startswith("_Froglet Tasks"):
            continue
        for marker, name in (("**Done when**", "steps"), ("**See**", "see"), ("**Who can take it**", "needs")):
            if s.startswith(marker):
                where, s = name, s[len(marker):].strip(" :")
                break
        if where == "task":
            task.append(line)
        elif where == "needs":
            needs = needs or s
        elif where == "steps":
            m = STEP.match(line)
            if m:
                steps.append([m.group(1) != " ", m.group(2).strip()])
            elif s.startswith(("- ", "* ")):
                steps.append([False, s[2:].strip()])
            elif s and steps:
                steps[-1][1] += " " + s
        elif where == "see" and s:
            see.append(s)
    return "\n".join(task).strip(), needs, steps, " ".join(see)


def core(body):
    """A body up to the end of its machine block (or legacy marker). GitHub-side tooling may append to a body, and
    that must not read as a change, or every sync rewrites every issue."""
    body = (body or "").replace("\r\n", "\n")
    m = BLOCK.search(body) or LEGACY.search(body)
    return body[:m.end()] if m else body


def from_issue(i):
    body = (i.get("body") or "").replace("\r\n", "\n")
    labels = [l["name"] if isinstance(l, dict) else l for l in i.get("labels", [])]
    tid_m = TITLE_ID.match(i.get("title", ""))
    blk = BLOCK.search(body)
    if blk:
        b = parse_block(blk.group(1))
        g = lambda k: b.get(k, ("", {}))[0]
        sub = lambda k: b.get(k, ("", {}))[1]
        task, needs, steps, see = parse_visible(body[:blk.start()])
        t = Ticket(g("id") or (tid_m.group(1) if tid_m else ""), g("kind") or "dev", g("area") or "General", task,
                   steps, see, needs or g("needs") or "anyone", g("filed"), sub("filed").get("by", ""),
                   sub("filed").get("branch", ""))
        t.taken_by, t.branch, t.taken = g("taken-by"), g("branch"), g("taken")
        t.released, t.revived, t.done, t.archived, t.merged_into = (g("released"), g("revived"), g("done"),
                                                                    g("archived"), g("merged-into"))
        t.priority, t.prio_why, t.prio_by = g("priority") or "P3", sub("priority").get("why", ""), \
            sub("priority").get("set-by", "")
        t.dept, t.dept_by = g("dept") or DEPT_OF_KIND.get(t.kind, "developers"), sub("dept").get("set-by", "")
        t.milestone, t.ms_why, t.ms_by = g("milestone"), sub("milestone").get("why", ""), \
            sub("milestone").get("set-by", "")
        # A priority, department or milestone a person changed on GitHub itself is their override: keep it.
        on = f"GitHub {(i.get('updated_at') or '')[:10]}".strip()
        prio = next((l[5:] for l in labels if l.startswith("prio:") and l[5:] in PRIORITIES), "")
        if prio and prio != t.priority:
            t.priority, t.prio_why, t.prio_by = prio, "set on GitHub", on
        dept = next((l[5:] for l in labels if l.startswith("dept:") and l[5:] in DEPTS), "")
        if dept and dept != t.dept:
            t.dept, t.dept_by = dept, on
        milestone = (i.get("milestone") or {}).get("title", "").split(" ")[0]
        if milestone and milestone != t.milestone:
            t.milestone, t.ms_why, t.ms_by = milestone, "set on GitHub", on
    elif LEGACY.search(body):
        t = from_legacy(body, labels)
    else:
        # Filed on GitHub by hand (or a block someone deleted): open, nobody's, and the next write gives it a block.
        title = TITLE_ID.sub("", i.get("title", "")).strip()
        task, needs, steps, see = parse_visible(body)
        created = (i.get("created_at") or datetime.date.today().isoformat())[:10]
        t = Ticket(tid_m.group(1) if tid_m else new_id(title, datetime.date.fromisoformat(created)),
                   next((l.split(":", 1)[1] for l in labels if l.startswith("kind:") and l[5:] in KINDS), "dev"),
                   "General", title + ("\n\n" + task if task and task != title else ""), steps, see, "anyone",
                   created, (i.get("user") or {}).get("login", "GitHub"), "")
        t.adopt = True
    t.number, t.url, t.raw = i.get("number"), i.get("html_url", ""), i
    t.updated = i.get("updated_at", "")
    t.assignees = [a["login"] if isinstance(a, dict) else a for a in i.get("assignees") or []]
    if i.get("state") == "closed":
        # GitHub's state is the truth: a person may close an issue there (completed = done, not planned = archived).
        reason = i.get("state_reason") or "completed"
        t.state = "done" if reason == "completed" else "merged" if t.merged_into else "archived"
        if t.state != "done":
            t.taken_by = t.branch = t.taken = ""
        if t.state == "done" and not t.done:
            t.done = f"{(i.get('closed_at') or '')[:10]}, closed on GitHub".lstrip(", ")
        if t.state == "archived" and not t.archived:
            t.archived = f"{(i.get('closed_at') or '')[:10]}, closed on GitHub as not planned".lstrip(", ")
    else:
        t.state = "live" if t.taken_by else "open"
        t.done = t.archived = t.merged_into = ""   # reopened on GitHub
    return t


def from_legacy(body, labels):
    """An issue the old mirror (gh-sync) wrote: its body is the board task's fields."""
    fields = {}
    for line in body.split("\n"):
        f = FIELD.match(line)
        if f:
            fields[f.group(1)] = f.group(2)
    status = (re.search(r"^\*\*Status on the board:\*\* (.+)$", body, re.M) or [None, "open"])[1].strip()
    kind = next((l[5:] for l in labels if l.startswith("kind:") and l[5:] in KINDS), "dev")
    area = next((l[5:].replace("-", " ").title() for l in labels if l.startswith("area:")), "General")
    tid = LEGACY.search(body).group(1)
    heading = f"### {tid} · {status} · {kind} · {area}"
    task = Task(tid, status, kind, area, fields, [heading] + [f"- **{k}:** {v}" for k, v in fields.items()])
    t = from_task(task)
    t.legacy = True
    return t


def from_task(task):
    """A ticket from an old-board task (markdown, or a legacy issue body)."""
    f = task.fields
    m = re.match(r"(\d{4}-\d{2}-\d{2})(?: by (.*?))?(?: \(`([^`]*)`\))?\s*$", f.get("Added", ""))
    filed_date, filed_by, filed_on = (m.group(1), m.group(2) or "", m.group(3) or "") if m else \
        (task.id[2:6] + "-" + task.id[6:8] + "-" + task.id[8:10], f.get("Added", ""), "")
    who = f.get("Who can take it", "anyone")
    t = Ticket(task.id, task.kind, task.area, f.get("Task", ""), [[False, s] for s in split_steps(f.get("Done when", ""))],
               f.get("See", ""), who if unassigned(who) else "anyone", filed_date, filed_by, filed_on)
    for raw in task.lines:
        g = FIELD.match(raw)
        if g and g.group(1) in ("Released", "Revived"):
            setattr(t, g.group(1).lower(), g.group(2))     # the last one wins: the clock's latest restart
    if task.status.startswith("taken by "):
        t.state, t.taken_by = "live", task.status[len("taken by "):]
        t.branch = f.get("Branch", "").strip().strip("`")
        t.taken = f.get("Taken", "")[:10]
    elif task.status == "done":
        t.state, t.done = "done", f.get("Done", "done")
    elif task.status == "archived":
        t.state, t.archived = "archived", f.get("Archived", "archived")
    return t


def issue_title(t):
    task = t.title
    return f"[{t.id}] " + (task if len(task) <= 100 else task[:97].rstrip() + "...")


def issue_labels(t, keep=()):
    labels = [LABEL, f"kind:{t.kind}", f"area:{slug(t.area)}", f"dept:{t.dept}", f"prio:{t.priority}"]
    if t.state == "live":
        labels.append("live")
    if t.state == "archived":
        labels.append("archived")
    return labels + [l for l in keep if l not in OUR_LABELS and not l.startswith(OUR_PREFIXES)]


# --- The rule set: a ticket's first priority, department and milestone, used only where nothing better exists -----

P1_WORDS = re.compile(r"crash|block(?:s|ed|ing)\b|silently|never (?:been )?(?:re-?)?tested|data loss|dangling|"
                      r"before (?:either|it|they) reach|release build|\bsteam\b|cannot|regress|broken|does nothing|"
                      r"dead press", re.I)
VESSELS = ("manta|dolphin|rhino|serpent|sparrow|squirrel|urchin|grizzly|termite|falcon|shrike|stoat|wrasse|grouper|"
           "goby|pistol|orca|honeyguide|ratel|starlings?|marlin|jacks|butterfly|angler|thresher|vessels?|hulls?|crew")
MODES = ("regatta|skim ?race|joust|crystal capture|maelstrom|skein|headlong|dog ?fight|hijack|salvo|tollway|"
         "switchback|wildlife liberation|freestyle|toybox|arena|arcade|game modes?")
M1_WORDS = re.compile(rf"\b(?:{VESSELS}|{MODES}|performance|perf|fps|editor|playtest|play-test|multiplayer|mppm|"
                      rf"platforms?|phone|android|ios|mobile|webgl|browser|builds?)\b", re.I)
M2_WORDS = re.compile(r"\b(?:activation|ftue|first[- ]login|onboarding|analytics|ugs|retention|referral|beta keys?)\b",
                      re.I)


def rule_priority(t, today, area_open):
    m = P1_WORDS.search(f"{t.task} {t.fields['Done when']}")
    if m:
        return "P1", f'"{m.group(0)}" in the task text'
    if t.state == "open":
        used = (today - open_since(t)).days
        if used >= 10:
            return "P2", f"{used} of {IDLE_DAYS} days used"
    if t.kind == "test" and area_open.get(t.area.lower(), 0) >= 15:
        return "P2", f"a test on {t.area}, which has {area_open[t.area.lower()]} open tasks"
    return "P3", "no rule matched"


def rule_milestone(t):
    text = f"{t.task} {t.fields['Done when']}"
    m = M2_WORDS.search(t.area)
    if m:
        return "M2", f"area {t.area}"
    if t.kind in ("test", "dev"):
        m = M1_WORDS.search(t.area) or M1_WORDS.search(text)
        if m:
            return "M1", f'"{m.group(0)}"'
    m = M2_WORDS.search(text)
    if m:
        return "M2", f'"{m.group(0)}"'
    return "", "serves no milestone by the rules"


def classify(t, today, area_open):
    """Fill priority, department and milestone from the rule set, except where a person (or a review) set them."""
    stamp = today.isoformat()
    if not t.prio_by or t.prio_by.startswith("rule"):
        t.priority, t.prio_why = rule_priority(t, today, area_open)
        t.prio_by = t.prio_by or f"rule {stamp}"
    if not t.dept_by or t.dept_by.startswith("kind"):
        t.dept, t.dept_by = DEPT_OF_KIND.get(t.kind, "developers"), t.dept_by or f"kind {stamp}"
    if not t.ms_by or t.ms_by.startswith("rule"):
        t.milestone, t.ms_why = rule_milestone(t)
        t.ms_by = t.ms_by or f"rule {stamp}"


def open_by_area(tickets):
    return collections.Counter(t.area.lower() for t in tickets if t.is_open)


# --- What a ticket goes through --------------------------------------------------------------------------------------

def take(t, who, branch_name, today):
    if not branch_name.strip():
        raise SystemExit("taking a task names your branch: pass --branch <the branch you do it on>")
    if not who.strip():
        raise SystemExit("taking a task names you: pass --who <your name>")
    if t.state not in ("open", "live"):
        raise SystemExit(f"{t.id} is {t.state}; `revive` it first")
    if t.state == "live" and t.taken_by.lower() != who.lower():
        raise SystemExit(f"{t.id} is live: {t.taken_by} on {t.branch or 'no branch'}. They release it first.")
    t.state, t.taken_by, t.branch, t.taken = "live", who.strip(), branch_name.strip(), today.isoformat()


def reopen(t, why, today):
    """Back to open, nobody's: the taker and branch come off, and a dated line says why. The 14 days start again."""
    who, name = t.taken_by, t.branch
    t.state, t.taken_by, t.branch, t.taken = "open", "", "", ""
    t.released = f"{today.isoformat()}, {who}" + (f" on `{name}`" if name else "") + f": {why}"
    t.notes.append(f"Back to open, nobody's: {why}.")


def release(t, note, today):
    if t.state != "live":
        raise SystemExit(f"{t.id} is {t.state}, not taken")
    reopen(t, note or "released", today)


def close_done(t, note, today):
    if not (note or "").strip():
        raise SystemExit("done needs --note: what proved it (the check, the commit, the result)")
    if t.state not in ("open", "live"):
        raise SystemExit(f"{t.id} is already {t.state}")
    t.state, t.done = "done", f"{today.isoformat()}, {note.strip()}"
    t.notes.append(f"Done: {note.strip()}")


def archive(t, note, today):
    if t.state == "live":
        raise SystemExit(f"{t.id} is live: {t.taken_by} on {t.branch or 'no branch'}. Release it first.")
    if t.state != "open":
        raise SystemExit(f"{t.id} is already {t.state}")
    t.state, t.archived = "archived", f"{today.isoformat()}, {note or 'archived by hand'}"
    if note:
        t.notes.append(f"Archived: {note}")


def revive(t, today):
    if t.state not in ("done", "archived"):
        raise SystemExit(f"{t.id} is {t.state}; only a done or archived task comes back")
    t.state, t.done, t.archived, t.revived = "open", "", "", today.isoformat()
    t.taken_by = t.branch = t.taken = ""


def set_class(t, field, value, by, today, why="", area_open=None):
    """A person's override (or a review's): attributed, and never touched by anything automatic until it is cleared."""
    stamp = f"{by} {today.isoformat()}"
    if field == "priority":
        if value == "clear":
            t.prio_by = ""
            t.priority, t.prio_why = rule_priority(t, today, area_open or {})
            t.prio_by = f"rule {today.isoformat()}"
            return
        if value not in PRIORITIES:
            raise SystemExit(f"priority is one of {', '.join(PRIORITIES)}")
        t.priority, t.prio_why, t.prio_by = value, why or f"set by {by}", stamp
    elif field == "dept":
        if value == "clear":
            t.dept, t.dept_by = DEPT_OF_KIND.get(t.kind, "developers"), f"kind {today.isoformat()}"
            return
        key = next((k for k, v in DEPTS.items() if value.lower() in (k, v.lower())), None)
        if not key:
            raise SystemExit(f"dept is one of {', '.join(DEPTS)}")
        t.dept, t.dept_by = key, stamp
    elif field == "milestone":
        if value == "clear":
            t.milestone, t.ms_why = rule_milestone(t)
            t.ms_by = f"rule {today.isoformat()}"
            return
        value = "" if value.lower() == "none" else value.upper()
        if value and not re.fullmatch(r"M\d+", value):
            raise SystemExit(f"milestone is a key such as {', '.join(MILESTONE_KEYS)} (`milestone list`), or none")
        t.milestone, t.ms_why, t.ms_by = value, why or f"set by {by}", stamp
    else:
        raise SystemExit("set takes priority, dept or milestone")


# --- GitHub ----------------------------------------------------------------------------------------------------------

def gh_api(method, path, payload=None):
    args = ["gh", "api", "--method", method, path] + (["--input", "-"] if payload is not None else [])
    r = subprocess.run(args, input=json.dumps(payload) if payload is not None else None, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"gh api {method} {path} failed: {r.stderr.strip()[:400]}")
    return json.loads(r.stdout) if r.stdout.strip() else None


class FakeApi:
    """GitHub's issue endpoints in memory. --self-test runs on it, and --offline FILE runs any command on a saved dump
    of the issues (a dry run without the network, or a rehearsal of migrate, then sweep, then sync)."""

    def __init__(self, issues=None, milestones=None, path=None, repo="o/r"):
        self.issues = {i["number"]: dict(i) for i in issues or []}
        self.milestones = [dict(m) for m in milestones or []]
        self.labels = {(l["name"] if isinstance(l, dict) else l) for i in self.issues.values() for l in i.get("labels", [])}
        self.comments, self.path, self.repo, self.calls, self.clock = [], path, repo, [], 0

    @classmethod
    def load(cls, path):
        data = json.loads(read(path) or "[]")
        data = {"issues": data} if isinstance(data, list) else data
        return cls(data.get("issues"), data.get("milestones"), path)

    def save(self):
        if self.path:
            with open(self.path, "w", encoding="utf-8") as fh:
                json.dump({"issues": sorted(self.issues.values(), key=lambda i: -i["number"]),
                           "milestones": self.milestones, "comments": self.comments}, fh, indent=1, ensure_ascii=False)

    def _stamp(self):
        self.clock += 1
        return f"2026-10-25T00:00:{self.clock % 60:02d}Z"

    def _milestone(self, number):
        return next((dict(m) for m in self.milestones if m["number"] == number), None) if number else None

    def __call__(self, method, path, payload=None):
        self.calls.append((method, path))
        url = urllib.parse.urlsplit(path)
        q = dict(urllib.parse.parse_qsl(url.query))
        rest = url.path.split("/")[3:]
        if rest == ["issues"] and method == "GET":
            items = sorted(self.issues.values(), key=lambda i: -i["number"])
            if q.get("labels"):
                items = [i for i in items if q["labels"] in [l["name"] for l in i["labels"]]]
            if q.get("state", "open") != "all":
                items = [i for i in items if i["state"] == q.get("state", "open")]
            per, page = int(q.get("per_page", 30)), int(q.get("page", 1))
            return [dict(i) for i in items[(page - 1) * per:page * per]]
        if rest == ["issues"] and method == "POST":
            n = max(self.issues, default=0) + 1
            self.issues[n] = {"number": n, "title": payload["title"], "body": payload.get("body", ""), "state": "open",
                              "state_reason": None, "labels": [{"name": l} for l in payload.get("labels", [])],
                              "assignees": [], "milestone": self._milestone(payload.get("milestone")),
                              "html_url": f"https://github.com/{self.repo}/issues/{n}", "created_at": self._stamp(),
                              "updated_at": self._stamp(), "closed_at": None}
            self.labels |= set(payload.get("labels", []))
            return dict(self.issues[n])
        if len(rest) == 2 and rest[0] == "issues" and method == "PATCH":
            i = self.issues[int(rest[1])]
            for k, v in payload.items():
                if k == "labels":
                    i["labels"] = [{"name": l} for l in v]
                    self.labels |= set(v)
                elif k == "milestone":
                    i["milestone"] = self._milestone(v)
                elif k == "assignees":
                    i["assignees"] = [{"login": a} for a in v]
                elif k == "state":
                    i["state"] = v
                    i["state_reason"] = payload.get("state_reason", "completed") if v == "closed" else "reopened"
                    i["closed_at"] = self._stamp() if v == "closed" else None
                elif k != "state_reason":
                    i[k] = v
            i["updated_at"] = self._stamp()
            return dict(i)
        if len(rest) == 3 and rest[0] == "issues" and rest[2] == "comments" and method == "POST":
            self.comments.append({"issue": int(rest[1]), "body": payload["body"]})
            return {"id": len(self.comments)}
        if len(rest) == 2 and rest[0] == "milestones" and method == "PATCH":
            m = next(m for m in self.milestones if m["number"] == int(rest[1]))
            m.update(payload)
            return dict(m)
        if rest == ["milestones"]:
            if method == "GET":
                return [dict(m) for m in self.milestones]
            m = dict(payload, number=len(self.milestones) + 1, open_issues=0, closed_issues=0)
            self.milestones.append(m)
            return dict(m)
        if rest == ["labels"]:
            if method == "GET":
                return [{"name": l} for l in sorted(self.labels)] if int(q.get("page", 1)) == 1 else []
            self.labels.add(payload["name"])
            return dict(payload)
        if len(rest) == 4 and rest[0] == "actions" and method == "PUT":
            return None
        raise ValueError(f"FakeApi does not serve {method} {path}")


class Issues:
    """The ticket store: the repository's issues labelled team-task, over `gh api` (REST). It pages with page=N
    itself: gh's --paginate follows GitHub's next links, which name the repository by number, and the session proxy
    refuses those (measured 2026-10-25)."""

    def __init__(self, repo, api=gh_api):
        self.repo, self.api, self._ms = repo, api, None

    def _pages(self, path):
        out, page, sep = [], 1, "&" if "?" in path else "?"
        while True:
            batch = self.api("GET", f"{path}{sep}per_page=100&page={page}") or []
            out += batch
            if len(batch) < 100:
                return out
            page += 1

    def fetch(self, state="all"):
        items = self._pages(f"repos/{self.repo}/issues?labels={LABEL}&state={state}")
        return [from_issue(i) for i in items if not i.get("pull_request")]

    def milestone_numbers(self):
        if self._ms is None:
            self._ms = {m["title"].split(" ")[0]: m["number"] for m in self._pages(f"repos/{self.repo}/milestones?state=all")}
        return self._ms

    def payload(self, t):
        keep = [l["name"] if isinstance(l, dict) else l for l in (t.raw or {}).get("labels", [])]
        p = {"title": issue_title(t), "body": render_body(t), "labels": issue_labels(t, keep),
             "state": "open" if t.is_open else "closed"}
        if not t.is_open:
            p["state_reason"] = "completed" if t.state == "done" else "not_planned"
        number = self.milestone_numbers().get(t.milestone) if t.milestone else None
        if number or not t.milestone:
            p["milestone"] = number
        return p

    def changes(self, t):
        """The fields of the issue that differ from what the ticket says, as a PATCH payload."""
        want, raw = self.payload(t), t.raw or {}
        have_labels = sorted(l["name"] if isinstance(l, dict) else l for l in raw.get("labels", []))
        diff = {}
        for k, v in want.items():
            if k == "labels":
                if sorted(v) != have_labels:
                    diff[k] = v
            elif k == "body":
                if core(raw.get("body")) != v:
                    diff[k] = v
            elif k == "milestone":
                if (raw.get("milestone") or {}).get("number") != v:
                    diff[k] = v
            elif k == "state_reason":
                continue
            elif raw.get(k) != v:
                diff[k] = v
        if not t.is_open and (raw.get("state") != "closed" or (raw.get("state_reason") or "completed") != want["state_reason"]):
            diff["state"], diff["state_reason"] = "closed", want["state_reason"]
        return diff

    def create(self, t):
        p = self.payload(t)
        created = self.api("POST", f"repos/{self.repo}/issues",
                           {k: p[k] for k in ("title", "body", "labels", "milestone") if p.get(k) is not None})
        t.number, t.url, t.raw = created["number"], created.get("html_url", ""), created
        if not t.is_open:
            t.raw = self.api("PATCH", f"repos/{self.repo}/issues/{t.number}",
                             {"state": "closed", "state_reason": p["state_reason"]})
        return t.number

    def write(self, t):
        """Create or update the issue, post the ticket's pending notes as comments; returns what changed."""
        if t.number is None:
            self.create(t)
            diff = {"created": True}
        else:
            diff = self.changes(t)
            if diff:
                t.raw = self.api("PATCH", f"repos/{self.repo}/issues/{t.number}", diff)
        if t.assignees:
            self.api("PATCH", f"repos/{self.repo}/issues/{t.number}", {"assignees": []})
            t.assignees, diff = [], dict(diff, assignees=[])
        for note in t.notes:
            self.api("POST", f"repos/{self.repo}/issues/{t.number}/comments", {"body": note})
        t.notes = []
        return diff

    def ensure_labels(self, names):
        have = {l["name"] for l in self._pages(f"repos/{self.repo}/labels")}
        for name in sorted(set(names) - have):
            color = LABEL_COLORS.get(name) or LABEL_COLORS.get(name.split(":")[0], "ededed")
            self.api("POST", f"repos/{self.repo}/labels", {"name": name, "color": color})

    def milestones(self):
        """The repository's milestones as the page shows them: key, name, gate, scope, due, counts."""
        out = []
        for m in self._pages(f"repos/{self.repo}/milestones?state=all"):
            key, _, name = m["title"].partition(" · ")
            desc = re.split(r"\n\s*---\s*\n", (m.get("description") or "").replace("\r\n", "\n"))[0]
            scope, _, gate = desc.partition("Gate:")
            out.append({"key": key, "name": name or m["title"], "scope": scope.strip(), "gate": gate.strip().strip("-").strip(),
                        "due": (m.get("due_on") or "")[:10], "number": m["number"], "state": m.get("state", "open"),
                        "open": m.get("open_issues", 0), "closed": m.get("closed_issues", 0)})
        return out

    def write_milestone(self, key, name, gate, scope, due=""):
        """Create or update a milestone from the page's editor."""
        payload = {"title": f"{key} · {name}".strip(" ·"), "description": f"{scope}\n\nGate: {gate or '-'}"}
        if due:
            payload["due_on"] = f"{due[:10]}T00:00:00Z"
        number = self.milestone_numbers().get(key)
        if number:
            self.api("PATCH", f"repos/{self.repo}/milestones/{number}", payload)
        else:
            self._ms[key] = self.api("POST", f"repos/{self.repo}/milestones", payload)["number"]

    def ensure_milestones(self):
        made = []
        for key, name, gate, scope in MILESTONES:
            if key not in self.milestone_numbers():
                m = self.api("POST", f"repos/{self.repo}/milestones",
                             {"title": f"{key} · {name}", "description": f"{scope}\n\nGate: {gate or '-'}"})
                self._ms[key] = m["number"]
                made.append(key)
        return made


def repo_name():
    if os.environ.get("GITHUB_REPOSITORY"):
        return os.environ["GITHUB_REPOSITORY"]
    url = git("remote", "get-url", "origin").stdout.strip()
    m = re.search(r"github\.com[/:]([^/]+/[^/.]+)", url) or re.search(r"/git/([^/]+/[^/.]+?)(?:\.git)?$", url)
    if not m:
        raise SystemExit(f"cannot tell the GitHub repository from origin ({url!r}); pass --repo OWNER/NAME")
    return m.group(1)


def open_tickets(area_names=None, repo=None):
    """Open and live tickets for a reader (perf_hunt's status, team_report): [] when GitHub cannot be read, because a
    status or a report must never fail on the network."""
    try:
        tickets = Issues(repo or repo_name()).fetch("open")
    except (SystemExit, Exception):  # noqa: BLE001 - gh missing, no token, offline
        return []
    names = {n.lower() for n in area_names} if area_names else None
    return [t for t in tickets if t.is_open and (names is None or t.area.lower() in names)]


def find(tickets, tid):
    for t in tickets:
        if t.id == tid or (tid.lstrip("#").isdigit() and t.number == int(tid.lstrip("#"))):
            return t
    raise SystemExit(f"no ticket {tid}")


def team():
    try:
        return json.loads(read(TEAM) or "{}").get("people", [])
    except ValueError:
        return []


def canon(name):
    """A display name as the team writes it: "Shombith03" or "shombith@froglet.studio" -> "Shombith"."""
    n = (name or "").strip()
    for p in team():
        if n.lower() in [x.lower() for x in p.get("names", []) + p.get("emails", []) + [p["name"]]] or \
                n.split(" ")[0].lower() == p["name"].lower():
            return p["name"]
    return n


def me():
    for key in ("user.email", "user.name"):
        value = git("config", key).stdout.strip()
        name = canon(value)
        if name and any(p["name"] == name for p in team()):
            return name
    return ""


# --- Sweep: the branch decides when a live ticket leaves; the clock decides when an untaken one does ----------------

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


def apply_sweep(tickets, plan, today, base=BASE):
    """Apply a plan to the tickets in memory; returns the tickets that changed."""
    into, by_id, changed = base.split("/", 1)[-1], {t.id: t for t in tickets}, []
    for tid, action, detail in plan:
        t = by_id[tid]
        if action == "unassign":
            pass   # Issues.write clears the assignees
        elif action == "merged":
            name, _, pr = detail.partition(" (#")
            pr = pr.rstrip(")")
            t.state, t.done = "done", f"{today.isoformat()}, `{name}` merged into {into}" + (f" in #{pr}" if pr else "")
            t.notes.append(f"Done: `{name}` merged into {into}" + (f" in #{pr}" if pr else "") + ".")
        elif action == "gone":
            reopen(t, "the branch was deleted without merging", today)
        elif action == "idle":
            reopen(t, detail.split(", ", 1)[1], today)
        elif action == "expire":
            t.state, t.archived = "archived", f"{today.isoformat()}, {detail}"
            t.notes.append(f"Archived: {detail}. Bring it back with `/team-tasks revive {t.id}` or from the page's "
                           f"Archive.")
        else:
            continue
        changed.append(t)
    return changed


def bridge(tickets, today, refs=BRIDGE_REFS, tasks=None):
    """What branches made before the switch still write on the old board (Docs/TEAM_TASKS.md): a task no issue
    carries yet is filed (open tasks only), and a task the board closed is closed here too. Forward only, like the
    old mirror: a stale copy of the board never reopens a ticket. Returns the tickets to write."""
    by_id = {t.id: t for t in tickets}
    area_open = open_by_area(tickets)
    out = []
    for task in (tasks if tasks is not None else markdown_tasks(refs)).values():
        t = by_id.get(task.id)
        if t is None:
            if task.is_open:
                t = from_task(task)
                classify(t, today, area_open)
                out.append(t)
            continue
        if not task.is_open and t.is_open and not t.legacy:
            note = task.fields.get("Done") or task.fields.get("Archived") or task.status
            if task.status == "done":
                t.state, t.done = "done", note
            else:
                t.state, t.archived = "archived", note
            if t.state == "archived":
                t.taken_by, t.branch, t.taken = "", "", ""
            t.notes.append(f"Closed on the old board: {note}")
            out.append(t)
    return out


def sweep(store, dry_run, fetch, base, idle_days, hint=None, today=None, refs=BRIDGE_REFS, tasks=None, probe_fn=None):
    today = today or datetime.date.today()
    if fetch:
        r = git("fetch", "--prune", "--quiet", "origin")
        if r.returncode != 0:
            raise SystemExit(f"git fetch --prune origin failed: {r.stderr.strip()[:300]}")
    tickets = store.fetch()
    current = [t for t in tickets if not t.legacy]
    filed = bridge(tickets, today, refs, tasks)
    for t in filed:
        verb = ("would file" if dry_run else "filed") if t.number is None else ("would close" if dry_run else "closed")
        print(f"{verb} {t.id} from an old board: {t.title[:80]}")
    for t in (t for t in current if t.adopt):
        print(f"{'would adopt' if dry_run else 'adopted'} #{t.number} as {t.id}: {t.title[:80]}")
        classify(t, today, open_by_area(current))

    def probe(name, taken):
        if hint and name == hint[0]:
            return {"exists": True, "merged": True, "pr": hint[1], "last": None}
        return (probe_fn or (lambda n, k: probe_branch(n, k, base)))(name, taken)

    plan = plan_sweep([t for t in current if not t.adopt and t not in filed], probe, today, idle_days)
    listed = {p[0] for p in plan if p[1] == "unassign"}
    plan += [(t.id, "unassign", "assigned on GitHub: " + ", ".join(t.assignees)) for t in current
             if t.is_open and t.assignees and t.id not in listed and t.state == "live"]
    words = {"unassign": "unassign (was: {})", "merged": "done: {} merged", "gone": "back to open: {} was deleted",
             "idle": "back to open: {}", "expire": "archived: {}", "no-branch": "left as is: {}"}
    for tid, action, detail in plan:
        print(f"{'would ' if dry_run else ''}{tid}: {words[action].format(detail)}")
    changed = apply_sweep(current, [p for p in plan if p[1] != "no-branch"], today, base)
    changed += [t for t in current if (t.adopt or t.assignees) and t not in changed and t not in filed]
    n_new = sum(1 for t in filed if t.number is None)
    if not dry_run and (filed or changed):
        store.ensure_labels({l for t in filed + changed for l in issue_labels(t)})
        for t in filed + changed:
            store.write(t)
    legacy = [t for t in tickets if t.legacy]
    counts = collections.Counter(p[1] for p in plan)
    print(f"sweep: {counts['merged']} done, {counts['gone'] + counts['idle']} back to open, {counts['expire']} archived "
          f"after {idle_days} days untaken, {counts['unassign']} unassigned, {counts['no-branch']} taken without a "
          f"branch, {n_new} filed and {len(filed) - n_new} closed from an old board" +
          (f"; {len(legacy)} still in the old mirror's format, skipped until `migrate` runs" if legacy else "") +
          (" (dry run, nothing written)" if dry_run else ""))
    return plan, filed


# --- migrate: the old board (and the old mirror's issues) -> tickets, once ------------------------------------------

STUB = """# Team tasks

The team's hand-off tickets live on **GitHub Issues** (label `team-task`) since {date}; this file no longer holds
them. Rules: [`Docs/TEAM.md`](TEAM.md) rule 9.

- **The board:** the Froglet Tasks page, {page}
- **From a session:** the `/team-tasks` skill, or `python3 Tools/Build/team_tasks.py list`
- **On GitHub:** issues labelled [`team-task`](https://github.com/{repo}/issues?q=label%3Ateam-task)

A task a branch still writes below the marker in the old format is filed as a ticket by the next sweep
(`.github/workflows/team-tasks.yml`); prefer `/team-tasks add`.

<!-- team-tasks -->
"""
ARCHIVE_STUB = """# Team tasks: archive

Done and archived tickets are closed issues labelled `team-task` since {date}: closed as completed when done, as not
planned with the label `archived` when nobody took them in 14 days. The Froglet Tasks page lists them under
Archive, and `python3 Tools/Build/team_tasks.py revive <id>` brings one back. See [`TEAM_TASKS.md`](TEAM_TASKS.md).
"""


def plan_migrate(tickets, tasks, today):
    """(rows, tickets to write) for moving the old board onto Issues. Pure. An issue the old mirror already made is
    updated in place, never created twice; one already migrated is left as it is; one only GitHub has (its board copy
    lives on a branch this checkout cannot read) is rebuilt from the old mirror's body."""
    by_id = {t.id: t for t in tickets}
    area_open = collections.Counter(t.area.lower() for t in tasks.values() if t.is_open)
    rows, out = [], []
    for tid in sorted(set(tasks) | {t.id for t in tickets if t.legacy}):
        cur = by_id.get(tid)
        if cur is not None and not cur.legacy:
            rows.append((tid, "keep", cur))
            continue
        new = from_task(tasks[tid]) if tid in tasks else cur
        if cur is not None:
            new.number, new.url, new.raw, new.assignees, new.updated = cur.number, cur.url, cur.raw, cur.assignees, cur.updated
            if cur.raw.get("state") == "closed" and new.is_open:
                # The old mirror closed it from another copy of the board: it stays closed.
                completed = (cur.raw.get("state_reason") or "completed") == "completed"
                new.state = "done" if completed else "archived"
                new.taken_by = new.branch = new.taken = ""
                setattr(new, "done" if completed else "archived", "closed on GitHub before the migration")
        new.legacy = False
        new.notes = []
        classify(new, today, area_open)
        rows.append((tid, "create" if new.number is None else f"update #{new.number}", new))
        out.append(new)
    return rows, out


def migrate(store, dry_run, today, repo, refs=BRIDGE_REFS):
    tasks = markdown_tasks(refs)
    tickets = store.fetch()
    rows, todo = plan_migrate(tickets, tasks, today)
    print(f"{'id':<16} {'action':<12} {'state':<9} {'prio':<4} {'dept':<10} {'ms':<3} task")
    for tid, action, t in rows:
        print(f"{tid:<16} {action:<12} {t.state:<9} {t.priority:<4} {t.dept:<10} {t.milestone or '-':<3} {t.title[:70]}")
    actions = collections.Counter(a.split(" ")[0] for _, a, _ in rows)
    states = collections.Counter(t.state for _, _, t in rows)
    print(f"migrate: {len(rows)} tickets ({', '.join(f'{v} {k}' for k, v in sorted(actions.items()))}); "
          f"{', '.join(f'{v} {k}' for k, v in sorted(states.items()))}; "
          f"{collections.Counter(t.priority for _, _, t in rows if t.is_open)} open by priority" +
          (" (dry run, nothing written)" if dry_run else ""))
    if dry_run:
        return rows
    try:
        store.api("PUT", f"repos/{repo}/actions/workflows/{OLD_WORKFLOW}/disable")
        print(f"disabled the old mirror workflow ({OLD_WORKFLOW}): it would write the old bodies back")
    except SystemExit as e:
        if "not active" in str(e):
            print(f"the old mirror workflow ({OLD_WORKFLOW}) is already disabled")
        else:
            print(f"WARNING: could not disable {OLD_WORKFLOW} ({e}); disable it under Actions before the next push "
                  f"of the old board, or it writes the old bodies back")
    made = store.ensure_milestones()
    if made:
        print(f"created milestones {', '.join(made)}")
    store.ensure_labels({l for t in todo for l in issue_labels(t)})
    for t in todo:
        store.write(t)
    for path, stub in ((BOARD, STUB), (ARCHIVE, ARCHIVE_STUB)):
        if os.path.exists(path) and (parse(read(path)) or path == ARCHIVE):
            with open(path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(stub.format(date=today.isoformat(), page=PAGE_URL, repo=repo))
    print("wrote the stubs over Docs/TEAM_TASKS.md and Docs/TEAM_TASKS_ARCHIVE.md: commit them")
    return rows


# --- add: one ticket per branch ---------------------------------------------------------------------------------------

def add(tickets, area, kind, task, done_when, priority, dept, why, milestone=None, see=None, by=None, who="anyone",
        today=None, on=None):
    """A new ticket, or (when this branch already has an open one) a checklist line on it. Returns (ticket, new?)."""
    today, on = today or datetime.date.today(), on or branch()
    if kind not in KINDS:
        raise SystemExit(f"kind must be one of {', '.join(KINDS)}")
    if not unassigned(who):
        raise SystemExit(f"a task names nobody until someone takes it: pass --who anyone (or \"anyone with ...\" for "
                         f"what the work needs), not {who!r} (Docs/TEAM.md rule 11)")
    if not (why or "").strip():
        raise SystemExit("add needs --why: one line naming what decided the priority, department and milestone")
    mine = [t for t in tickets if t.filed_on == on and t.is_open and on and on not in SHARED_BRANCHES]
    if mine:
        t = mine[0]
        t.steps.append([False, f"{task.strip()} (done when: {done_when.strip()})"])
        return t, False
    tid = new_id(task, today)
    if any(t.id == tid for t in tickets):
        raise SystemExit(f"{tid} already exists (the same task text was filed today)")
    t = Ticket(tid, kind, area, task.strip(), [[False, s] for s in split_steps(done_when)], see or "", who.strip(),
               today.isoformat(), by or "a Claude session", on)
    # The filing session decides these from the work it just did; "claude" marks them as a session's, which a
    # person's `set` (or a later review) may change.
    set_class(t, "priority", priority, "claude", today, why)
    set_class(t, "dept", dept, "claude", today)
    set_class(t, "milestone", milestone or "none", "claude", today, why)
    return t, True


# --- sync: the Froglet Tasks page's store <-> Issues -----------------------------------------------------------------

def norm(doc):
    """A page-store document as {id, updatedAt, **fields}, whether it came flat or as ArtifactData's {id, data}."""
    if isinstance(doc, dict) and isinstance(doc.get("data"), dict):
        return dict(doc["data"], id=doc.get("id", doc["data"].get("id")), updatedAt=doc.get("updatedAt", ""))
    return dict(doc)


def page_doc(t):
    since = open_since(t)
    return {"id": t.id, "number": t.number, "url": t.url, "title": t.title, "task": t.task, "kind": t.kind,
            "area": t.area, "dept": t.dept, "priority": t.priority, "prioWhy": t.prio_why, "prioBy": t.prio_by,
            "deptBy": t.dept_by, "milestone": t.milestone, "msWhy": t.ms_why, "msBy": t.ms_by, "state": t.state,
            "takenBy": t.taken_by, "branch": t.branch, "taken": t.taken, "filed": t.filed_date, "filedBy": t.filed_by,
            "filedOn": t.filed_on, "needs": t.needs, "steps": [{"done": d, "text": s} for d, s in t.steps],
            "see": t.see, "released": t.released, "revived": t.revived, "done": t.done, "archived": t.archived,
            "openSince": since.isoformat(),
            "archivesOn": (since + datetime.timedelta(days=IDLE_DAYS)).isoformat() if t.state == "open" else "",
            "legacy": t.legacy, "updated": t.updated}


def store_op(op, collection, doc_id, data=None):
    return {"op": op, "collection": collection, "doc_id": doc_id, **({"data": data} if data is not None else {})}


def file_from_page(tickets, d, who, today, person=True):
    """A ticket from a page document: a task someone filed on the page (`new/<id>`), or a Claude session's suggestion
    a person approved there (`suggestions/<id>`). It is filed as its own ticket: the page is not a branch."""
    task, done_when = str(d.get("task", "")).strip(), str(d.get("doneWhen", "")).strip()
    if not task:
        raise SystemExit("no task text")
    kind = d.get("kind") if d.get("kind") in KINDS else "test"
    priority = d.get("priority") if d.get("priority") in PRIORITIES else "P3"
    dept = d.get("dept") if d.get("dept") in DEPTS else DEPT_OF_KIND.get(kind, "developers")
    by = d.get("from") or f"{who} on the Froglet Tasks page"
    t, _ = add(tickets, d.get("area") or "General", kind, task, done_when or "Done when the task says so", priority,
               dept, d.get("why") or f"filed by {by}", d.get("milestone") or None, d.get("see") or None, by,
               today=today, on=d.get("filedOn") or "")
    if person and who:   # a person filed it on the page: the ranking is theirs
        t.prio_by = t.dept_by = t.ms_by = f"{who} {today.isoformat()}"
    return t


def utc_now():
    """The store's timestamp shape (2026-10-25T09:00:00.000Z), so the page's and the sync's times compare as text."""
    return datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def sync(store, data, since, dry_run, today=None, now=None, extras=True, base=BASE, every=False):
    """Carry the page's changes made after `since` to Issues, then return what the page's store should hold:
    {"at", "applied", "skipped", "store": [ops], "counts"}. The page writes: takes/<id> (take, release, done, archive,
    revive), overrides/<id> (priority, dept, milestone), new/<id> (a task filed on the page), suggestions/<id> (a
    Claude session's draft; filed once a person approves it), milestones/<key> (edited by an approver). The store ops
    give back tasks/<id>, milestones/<key>, meta/branches and meta/activity, and clear what was filed."""
    today = today or datetime.date.today()
    now = now or utc_now()
    tickets = store.fetch()
    by_id = {t.id: t for t in tickets}
    events = sorted([("take", norm(d)) for d in data.get("takes", [])] +
                    [("override", norm(d)) for d in data.get("overrides", [])], key=lambda e: e[1].get("updatedAt", ""))
    applied, skipped, changed, ops = [], [], {}, []
    area_open = open_by_area(tickets)
    for kind, d in events:
        if d.get("updatedAt", "") <= since or d.get("applied"):
            continue
        t, who = by_id.get(d.get("id")), canon(d.get("by", ""))
        if t is None or t.legacy:
            skipped.append(f"{d.get('id')}: no such ticket" + (" (not migrated yet)" if t else ""))
            continue
        if kind == "take" and d.get("action", "take") == "take" and not d.get("branch") and d.get("session"):
            skipped.append(f"{t.id}: started in a session; that session takes it on its branch")
            continue
        day = datetime.date.fromisoformat((d.get("at") or d.get("updatedAt") or today.isoformat())[:10])
        try:
            if kind == "take":
                action = d.get("action", "take")
                if action == "take":
                    take(t, who, d.get("branch", ""), day)
                elif action == "release":
                    release(t, d.get("note") or f"released on the page by {who}", day)
                elif action == "done":
                    close_done(t, d.get("note") or f"closed on the page by {who}", day)
                elif action == "archive":
                    archive(t, d.get("note") or f"archived on the page by {who}", day)
                elif action == "revive":
                    revive(t, day)
                else:
                    raise SystemExit(f"unknown action {action!r}")
                applied.append(f"{t.id}: {action} by {who}" + (f" on {t.branch}" if action == "take" else ""))
            else:
                for field in ("priority", "dept", "milestone"):
                    if field in d:
                        set_class(t, field, d[field] or "clear", who, day, d.get("why", ""), area_open)
                        applied.append(f"{t.id}: {field} {d[field] or 'cleared'} by {who}")
            changed[t.id] = t
        except SystemExit as e:
            skipped.append(f"{t.id}: {e}")

    # Tasks filed on the page, and suggestions a person approved there.
    for coll in ("new", "suggestions"):
        for d in sorted((norm(x) for x in data.get(coll, [])), key=lambda x: x.get("updatedAt", "")):
            if coll == "suggestions" and d.get("status") != "approved":
                continue
            who = canon(d.get("approvedBy") or d.get("by", ""))
            try:
                t = file_from_page(tickets, d, who, today, person=coll == "new")
            except SystemExit as e:
                skipped.append(f"{coll}/{d.get('id')}: {e}")
                continue
            if t.number is None and t not in tickets:
                tickets.append(t)
            changed[t.id] = t
            applied.append(f"{t.id}: filed from the page ({coll}) by {who or 'someone'}")
            ops.append(store_op("delete", coll, d["id"]) if coll == "new" else
                       store_op("update", coll, d["id"], {"status": "filed", "ticket": t.id}))

    # Milestones an approver edited on the page.
    gh_ms = [] if not extras else store.milestones()
    for d in (norm(x) for x in data.get("milestones", [])):
        if d.get("updatedAt", "") <= since or not re.fullmatch(r"M\d+", str(d.get("id") or d.get("key") or "")):
            continue
        key = d.get("key") or d["id"]
        try:
            if not dry_run:
                store.write_milestone(key, d.get("name", ""), d.get("gate", ""), d.get("scope", ""), d.get("due", ""))
            applied.append(f"milestone {key}: saved on GitHub")
        except SystemExit as e:
            skipped.append(f"milestone {key}: {e}")
    if not dry_run:
        if changed:
            store.ensure_labels({l for t in changed.values() for l in issue_labels(t)})
        for t in changed.values():
            store.write(t)

    prev = {norm(d)["id"]: norm(d) for d in data.get("tasks", [])}
    docs = [dict(page_doc(t), syncedAt=now) for t in tickets]
    strip = lambda d: {k: v for k, v in (d or {}).items() if k not in ("syncedAt", "updatedAt", "updated", "id")}
    ops += [store_op("set", "tasks", d["id"], d) for d in docs if every or strip(prev.get(d["id"])) != strip(d)]
    ops += [store_op("delete", "tasks", i) for i in sorted(set(prev) - {d["id"] for d in docs})]
    if extras:
        for m in (store.milestones() if not dry_run else gh_ms):
            ops.append(store_op("set", "milestones", m["key"], m))
        ops.append(store_op("set", "meta", "branches", branches_report(tickets, base, today)))
        ops.append(store_op("set", "meta", "activity", activity_report(base)))
    ops.append(store_op("update", "meta", "sync", {"at": now, "applied": len(applied), "base": base.split("/", 1)[-1],
                                                    "counts": dict(collections.Counter(t.state for t in tickets))}))
    return {"at": now, "applied": applied, "skipped": skipped, "store": ops,
            "counts": dict(collections.Counter(t.state for t in tickets))}


def branches_report(tickets, base=BASE, today=None, days=21):
    """The branches people work on now (a commit in the last `days`), each with how far ahead of the base it is and
    the tickets live on it or filed from it. The page's Branches view; bleeding-edge is where every session starts."""
    today = today or datetime.date.today()
    since = (today - datetime.timedelta(days=days)).isoformat()
    rows = git("for-each-ref", "refs/remotes/origin", "--sort=-committerdate",
               "--format=%(refname:short)|%(committerdate:short)|%(subject)").stdout.splitlines()
    out = []
    for row in rows:
        ref, date, subject = (row.split("|", 2) + ["", ""])[:3]
        name = ref.split("/", 1)[-1]
        if name in ("HEAD", "origin") or date < since or ref == base:
            continue
        ahead = git("rev-list", "--count", f"{base}..{ref}").stdout.strip()
        out.append({"name": name, "last": date, "subject": subject[:140], "ahead": int(ahead or 0),
                    "live": [t.id for t in tickets if t.state == "live" and t.branch == name],
                    "filed": len([t for t in tickets if t.filed_on == name and t.is_open])})
        if len(out) >= 150:
            break
    return {"base": base.split("/", 1)[-1], "days": days, "checked": today.isoformat(), "branches": out}


def activity_report(base=BASE, days=7):
    """What landed on the base in the last `days`: each merged pull request with its branch, title and day."""
    log = git("log", base, "--merges", "--first-parent", f"--since={days}.days", "--format=%cs%x1f%an%x1f%s%x1f%b%x1e").stdout
    merged = []
    for rec in log.split("\x1e"):
        parts = rec.strip("\n").split("\x1f")
        if len(parts) < 4:
            continue
        date, author, subject, body = parts
        m = re.match(r"Merge pull request #(\d+) from [^/]+/(\S+)", subject)
        if not m:
            continue
        merged.append({"date": date, "pr": int(m.group(1)), "branch": m.group(2), "mergedBy": author,
                       "title": (body.strip().split("\n") or [""])[0][:160]})
    return {"base": base.split("/", 1)[-1], "days": days, "merged": merged[:120]}


# --- review: a Claude session re-reads tickets and proposes; a person approves; then it is written -------------------

def review_rows(tickets, ids=None, area=None, every=False, batch=None, size=25):
    picked = [t for t in tickets if t.is_open and not t.legacy and
              (every or (ids and t.id in ids) or (area and area.lower() in t.area.lower()))]
    if batch:
        picked = picked[(batch - 1) * size:batch * size]
    return [{"id": t.id, "number": t.number, "task": t.task, "doneWhen": [s for _, s in t.steps], "area": t.area,
             "kind": t.kind, "filedOn": t.filed_on, "filed": t.filed_date, "state": t.state,
             "priority": t.priority, "prioBy": t.prio_by, "dept": t.dept, "deptBy": t.dept_by,
             "milestone": t.milestone, "msBy": t.ms_by, "openSince": open_since(t).isoformat()} for t in picked]


def review_apply(tickets, decisions, today):
    """Apply a review's decisions in memory: [{id, priority?, dept?, milestone?, why, duplicateOf?}]. A field a person
    set is kept. Returns (rows, changed tickets)."""
    by_id, rows, changed = {t.id: t for t in tickets}, [], {}
    for d in decisions:
        t = by_id.get(d.get("id"))
        if t is None:
            rows.append((d.get("id"), "-", "no such ticket", ""))
            continue
        why = d.get("why", "")
        moved = []
        for field, attr, by_attr in (("priority", "priority", "prio_by"), ("dept", "dept", "dept_by"),
                                     ("milestone", "milestone", "ms_by")):
            if field not in d or d[field] is None:
                continue
            old = getattr(t, attr)
            if by_person(getattr(t, by_attr)):
                if (d[field] or "") != old:
                    rows.append((t.id, field, f"{old} kept (set by {getattr(t, by_attr)})", why))
                continue
            set_class(t, field, (d[field] or "none") if field == "milestone" else d[field], "claude", today, why)
            if getattr(t, attr) != old:
                moved.append(f"{field} {old or '-'} -> {getattr(t, attr) or '-'}")
                rows.append((t.id, field, f"{old or '-'} -> {getattr(t, attr) or '-'}", why))
        target = by_id.get(d.get("duplicateOf") or "")
        if target is not None and target is not t:
            for step in [[False, f"{t.title} (from {t.id})"]] + t.steps:
                if step[1] not in [s for _, s in target.steps]:
                    target.steps.append([step[0], step[1]])
            t.state, t.merged_into = "merged", target.id
            t.notes.append(f"Merged into {target.id}" + (f" (#{target.number})" if target.number else "") +
                           ": the same work filed from another branch.")
            changed[target.id] = target
            rows.append((t.id, "duplicate", f"merged into {target.id}", why))
            moved.append(f"merged into {target.id}")
        if moved:
            t.notes.append(f"Re-evaluated {today.isoformat()} by a Claude session: {why}")
            changed[t.id] = t
    return rows, list(changed.values())


# --- pr-section --------------------------------------------------------------------------------------------------------

def pr_section(tickets, branch_name):
    """Markdown naming the tickets this branch filed or took, for a PR body (/ship)."""
    filed = [t for t in tickets if t.filed_on == branch_name and t.state != "merged"]
    taken = [t for t in tickets if t.branch == branch_name and t not in filed]
    if not filed and not taken:
        return ""
    link = lambda t: f"[`{t.id}`]({t.url})" if t.url else f"`{t.id}`"
    out = ["## Hand-offs (Froglet Tasks)", ""]
    for t in filed:
        out.append(f"- **New** {link(t)} ({t.kind}, {t.area}, {t.priority}): {t.title}"
                   + ("" if t.is_open else f" ({t.state})"))
    for t in taken:
        out.append(f"- {link(t)} is **{t.state}** on this branch" + (", and closes when it merges" if t.state == "live"
                                                                      else "") + f": {t.title}")
    return "\n".join(out)


def pr_section_markdown(text, base_text):
    """The same for tasks a branch still wrote on the old markdown board."""
    before = {t.id: t.status for t in parse(base_text)}
    now = parse(text)
    added = [t for t in now if t.id not in before]
    if not added:
        return ""
    return "\n".join(["## Hand-offs (old board, filed as tickets by the next sweep)", ""] +
                     [f"- **New** `{t.id}` ({t.kind}, {t.area}): {t.fields.get('Task', '')}" for t in added])


# --- self-test ----------------------------------------------------------------------------------------------------------

def self_test():
    bad = []

    def refused(fn, what):
        try:
            fn()
            bad.append(what)
        except SystemExit:
            pass

    day = datetime.date(2026, 10, 15)
    api = FakeApi()
    store = Issues("o/r", api)

    # add: nobody's, needs a why, one ticket per branch
    refused(lambda: add([], "UI", "test", "Name a person.", "x", "P3", "testers", "w", who="Yash (offered)", today=day,
                        on="claude/a"), "add took a person in --who")
    refused(lambda: add([], "UI", "test", "No why.", "x", "P3", "testers", "", today=day, on="claude/a"),
            "add took no --why")
    t1, new1 = add([], "Activation", "test", "Play the guide once.", "It draws; it ends", "P2", "testers",
                   "the guide is M2", "M2", today=day, on="claude/a")
    be, _ = add([], "Performance", "dev", "First regression.", "x", "P2", "developers", "w", today=day,
                on="bleeding-edge")
    be2, be2_new = add([be], "Performance", "dev", "Second regression.", "x", "P2", "developers", "w", today=day,
                       on="bleeding-edge")
    if not be2_new or be2 is be:
        bad.append("add folded a task filed from a shared branch into that branch's other ticket")
    store.write(t1)
    t2, new2 = add(store.fetch(), "Activation", "test", "Play it again.", "It still draws.", "P3", "testers", "w",
                   today=day, on="claude/a")
    if not new1 or new2 or t2.id != t1.id or len(t2.steps) != 3:
        bad.append("add did not append to the branch's open ticket")
    store.write(t2)
    t3, new3 = add(store.fetch(), "Tooling", "dev", "Other branch.", "x", "P3", "developers", "w", today=day,
                   on="claude/b")
    if not new3 or t3.id == t1.id or not ID_RE.fullmatch(t3.id):
        bad.append("ids are not distinct date+hash ids")
    store.write(t3)

    # the issue: labels, block round trip, nothing to write a second time
    issue = api.issues[t1.number]
    names = {l["name"] for l in issue["labels"]}
    if names != {LABEL, "kind:test", "area:activation", "dept:testers", "prio:P2"} or issue["assignees"]:
        bad.append(f"a new ticket's labels are wrong: {names}")
    back = find(store.fetch(), t1.id)
    if store.changes(back) or (back.priority, back.dept, back.taken_by, back.steps) != ("P2", "testers", "", t2.steps):
        bad.append(f"the machine block does not round-trip: {store.changes(back)}")
    if "Who can take it" in issue["body"]:
        bad.append("a plain 'anyone' ticket shows a who line")

    # a person edits the body on GitHub: a ticked box and a GitHub footer survive; a deleted block is adopted
    api.issues[t1.number]["body"] = issue["body"].replace("- [ ] It draws\n", "- [x] It draws\n").replace("\n", "\r\n") \
        + "\r\n\r\n---\r\n_appended by a bot_"
    ticked = find(store.fetch(), t1.id)
    if ticked.steps[0] != [True, "It draws"] or store.changes(ticked):
        bad.append("a box ticked on GitHub (or a footer GitHub appended) read as a change")
    api.issues[t3.number]["body"] = "Just some words a person left."
    api.issues[t3.number]["labels"].append({"name": "bug"})
    lost = find(store.fetch(), t3.id)
    if not lost.adopt or lost.state != "open" or "bug" not in issue_labels(lost, ["bug", "taken"]) or \
            "taken" in issue_labels(lost, ["bug", "taken"]):
        bad.append("a body without its block was not adopted as open, nobody's (or a person's label was dropped)")
    hand = api("POST", "repos/o/r/issues", {"title": "Ship the thing", "body": "**Done when**\n- [ ] shipped",
                                             "labels": [LABEL, "kind:setup"]})
    adopted = find(store.fetch(), str(hand["number"]))
    if not adopted.adopt or not ID_RE.fullmatch(adopted.id) or adopted.kind != "setup" or adopted.steps != [[False, "shipped"]]:
        bad.append("an issue filed by hand on GitHub was not adopted with an id")

    # take, release, done, archive, revive
    t = find(store.fetch(), t3.id)
    refused(lambda: take(t, "Shombith", " ", day), "take took no branch")
    take(t, "Shombith", "claude/b", day)
    refused(lambda: take(t, "Garrett", "claude/g", day), "a live ticket was taken from under its taker")
    refused(lambda: archive(t, "", day), "a live ticket was archived")
    take(t, "Shombith", "claude/b2", day)
    store.write(t)
    live = find(store.fetch(), t3.id)
    if live.state != "live" or live.branch != "claude/b2" or "live" not in {l["name"] for l in api.issues[t3.number]["labels"]}:
        bad.append("taking a ticket did not make it live on its branch, labelled live")
    refused(lambda: close_done(live, "", day), "done took no note")
    release(live, "plans changed", day)
    if live.state != "open" or live.branch or "plans changed" not in live.released:
        bad.append("release did not put the ticket back to open without its branch")

    # sweep: merged / gone / idle / uncommitted / active / legacy / named / stale, then a second sweep is a no-op
    api2 = FakeApi()
    s2 = Issues("o/r", api2)
    ids = {}
    for key in ("merged", "gone", "idle", "uncommitted", "active", "legacy", "named", "editor", "stale"):
        when = day + datetime.timedelta(days=10) if key in ("named", "editor") else day
        tk, _ = add([], "Tooling", "dev", f"Sweep case {key}.", "It sweeps.", "P3", "developers", "w",
                    who="anyone with the Unity Editor" if key == "editor" else "anyone", today=when, on=f"claude/f-{key}")
        if key in ("merged", "gone", "idle", "uncommitted", "active"):
            take(tk, "Shombith", f"claude/{key}", day)
        if key == "legacy":
            tk.state, tk.taken_by, tk.taken = "live", "Yash", day.isoformat()
        s2.write(tk)
        ids[key] = tk.id
    named_issue = next(i for i in api2.issues.values() if ids["named"] in i["title"])
    named_issue["assignees"] = [{"login": "yash"}]
    later = day + datetime.timedelta(days=20)
    facts = {"claude/merged": dict(exists=False, merged=True, pr="1500", last=None),
             "claude/gone": dict(exists=False, merged=False, pr=None, last=None),
             "claude/idle": dict(exists=True, merged=False, pr=None, last=day + datetime.timedelta(days=2)),
             "claude/uncommitted": dict(exists=True, merged=False, pr=None, last=day - datetime.timedelta(days=5)),
             "claude/active": dict(exists=True, merged=False, pr=None, last=later - datetime.timedelta(days=3))}
    probe = lambda name, taken: facts[name]
    plan = plan_sweep(s2.fetch(), probe, later)
    want = {ids["merged"]: "merged", ids["gone"]: "gone", ids["idle"]: "idle", ids["uncommitted"]: "idle",
            ids["legacy"]: "no-branch", ids["named"]: "unassign", ids["stale"]: "expire"}
    if {tid: action for tid, action, _ in plan} != want:
        bad.append(f"sweep planned the wrong moves: {plan}")
    swept = s2.fetch()
    for tk in apply_sweep(swept, [p for p in plan if p[1] != "no-branch"], later) + \
            [tk for tk in swept if tk.assignees]:
        s2.write(tk)
    after = {tk.id: tk for tk in s2.fetch()}
    raw = {tk.id: tk.raw for tk in after.values()}
    if after[ids["merged"]].state != "done" or raw[ids["merged"]]["state_reason"] != "completed" or \
            not any("#1500" in c["body"] for c in api2.comments):
        bad.append("a merged branch did not close its ticket as completed with the PR")
    for key in ("gone", "idle", "uncommitted"):
        tk = after[ids[key]]
        if tk.state != "open" or tk.branch or "live" in {l["name"] for l in raw[ids[key]]["labels"]} or not tk.released:
            bad.append(f"a {key} branch did not put its ticket back to open, nobody's")
    if after[ids["active"]].state != "live" or after[ids["active"]].branch != "claude/active":
        bad.append("an active branch's ticket was moved")
    if raw[ids["named"]]["assignees"] or after[ids["editor"]].needs != "anyone with the Unity Editor":
        bad.append("sweep did not clear an assignee, or dropped what the work needs")
    if after[ids["legacy"]].state != "live":
        bad.append("sweep touched a ticket taken without a branch")
    st = raw[ids["stale"]]
    if after[ids["stale"]].state != "archived" or st["state_reason"] != "not_planned" or \
            "archived" not in {l["name"] for l in st["labels"]}:
        bad.append("an open ticket nobody took for 14 days was not archived (closed, not planned, labelled)")
    calls = len(api2.calls)
    again = plan_sweep(s2.fetch(), probe, later)
    if [p[1] for p in again] != ["no-branch"] or any(s2.changes(tk) for tk in s2.fetch()):
        bad.append("a second sweep would change something again")
    with contextlib.redirect_stdout(io.StringIO()):
        sweep(s2, False, False, BASE, IDLE_DAYS, today=later, tasks={}, probe_fn=probe)
    if any(m != "GET" for m, _ in api2.calls[calls:]):
        bad.append("a second sweep wrote to GitHub")
    hinted = []
    with contextlib.redirect_stdout(io.StringIO()):
        hinted = sweep(s2, True, False, BASE, IDLE_DAYS, ("claude/active", "1600"), later, tasks={}, probe_fn=probe)[0]
    if (ids["active"], "merged", "claude/active (#1600)") not in hinted:
        bad.append("the merged pull request's branch (the workflow's hint) did not close its ticket")

    # revive restarts the clock; a done ticket comes back too
    stale = after[ids["stale"]]
    revive(stale, later)
    s2.write(stale)
    back = find(s2.fetch(), ids["stale"])
    if back.state != "open" or "archived" in {l["name"] for l in back.raw["labels"]} or \
            any(p[0] == ids["stale"] for p in plan_sweep([back], probe, later)) or \
            not any(p[0] == ids["stale"] for p in plan_sweep([back], probe, later + datetime.timedelta(days=IDLE_DAYS))):
        bad.append("a revived ticket did not come back open with its 14 days starting again")
    refused(lambda: revive(back, later), "revived an open ticket")

    # set by a person survives review; review merges duplicates
    tickets = s2.fetch()
    a_, b_ = find(tickets, ids["active"]), find(tickets, ids["editor"])
    set_class(a_, "priority", "P1", "Garrett", later, "blocks the build")
    rows, changed = review_apply(tickets, [{"id": a_.id, "priority": "P3", "dept": "testers", "why": "re-read"},
                                           {"id": b_.id, "priority": "P2", "milestone": "M1", "why": "editor test",
                                            "duplicateOf": a_.id}], later)
    if a_.priority != "P1" or a_.prio_by.split(" ")[0] != "Garrett" or a_.dept != "testers" or \
            b_.state != "merged" or b_.merged_into != a_.id or not any("from " + b_.id in s for _, s in a_.steps):
        bad.append(f"review overwrote a person's priority or did not merge a duplicate: {rows}")
    for tk in changed:
        s2.write(tk)
    merged = find(s2.fetch(), b_.id)
    if merged.state != "merged" or merged.raw["state_reason"] != "not_planned" or \
            "archived" in {l["name"] for l in merged.raw["labels"]}:
        bad.append("a merged duplicate did not close as not planned without the archived label")

    # sync: the page's take and override reach Issues; an old one does not; the docs come back
    api3 = FakeApi()
    s3 = Issues("o/r", api3)
    font, _ = add([], "UI", "decision", "Pick a font.", "Picked.", "P3", "decisions", "w", today=day, on="claude/s")
    s3.write(font)
    first = sync(s3, {}, "", True, today=day, extras=False)
    task_docs = lambda out: [o["data"] for o in out["store"] if o["collection"] == "tasks" and o["op"] == "set"]
    stamp = "2026-10-16T10:00:00Z"
    data = {"tasks": task_docs(first),
            "takes": [{"id": font.id, "data": {"action": "take", "by": "Shombith03", "branch": "claude/font"},
                       "updatedAt": stamp}],
            "overrides": [{"id": font.id, "data": {"priority": "P1", "by": "Yash", "why": "store page needs it"},
                           "updatedAt": stamp}]}
    out = sync(s3, data, "2026-10-16T00:00:00Z", False, today=day, extras=False)
    got = find(s3.fetch(), font.id)
    if (got.state, got.taken_by, got.branch, got.priority, got.prio_by.split(" ")[0]) != \
            ("live", "Shombith", "claude/font", "P1", "Yash") or len(task_docs(out)) != 1:
        bad.append(f"sync did not carry the page's take and override to the issue: {out['applied']} {out['skipped']}")
    if sync(s3, dict(data, tasks=task_docs(out)), "2026-10-17T00:00:00Z", False, today=day, extras=False)["applied"]:
        bad.append("sync applied page changes older than the last sync")
    # A task filed on the page, and a session's suggestion once a person approved it, become tickets; a pending
    # suggestion and a session start (no branch yet) do not.
    page = {"new": [{"id": "n1", "data": {"task": "Check the menu on a phone.", "doneWhen": "It fits; it scrolls",
                                          "area": "UI", "kind": "test", "priority": "P2", "by": "Caleb"},
                     "updatedAt": stamp}],
            "suggestions": [{"id": "s1", "data": {"task": "Add a tooltip.", "kind": "dev", "status": "approved",
                                                  "approvedBy": "Garrett", "from": "Yash's session on claude/tip",
                                                  "filedOn": "claude/tip", "why": "players miss it"}, "updatedAt": stamp},
                            {"id": "s2", "data": {"task": "Not yet.", "status": "pending"}, "updatedAt": stamp}],
            "takes": [{"id": font.id, "data": {"action": "take", "by": "Yash", "session": "session_x"},
                       "updatedAt": "2026-10-18T00:00:00Z"}]}
    out2 = sync(s3, page, "2026-10-17T00:00:00Z", False, today=day, extras=False)
    filed = {t.title: t for t in s3.fetch()}
    ops = {(o["collection"], o["doc_id"]): o for o in out2["store"]}
    if "Check the menu on a phone." not in filed or "Add a tooltip." not in filed or "Not yet." in filed or \
            filed["Check the menu on a phone."].prio_by.split(" ")[0] != "Caleb" or \
            filed["Add a tooltip."].filed_on != "claude/tip" or ops.get(("new", "n1"), {}).get("op") != "delete" or \
            ops.get(("suggestions", "s1"), {}).get("data", {}).get("status") != "filed" or \
            find(s3.fetch(), font.id).taken_by != "Shombith":
        bad.append(f"page filings, approvals or a session start were handled wrongly: {out2['applied']} {out2['skipped']}")
    s3.ensure_milestones()
    s3.write_milestone("M1", "Closed testing", "co-op retention", "every vessel", "2026-12-01")
    s3.write_milestone("M6", "Game 2", "", "design")
    ms = {m["key"]: m for m in s3.milestones()}
    if ms["M1"]["gate"] != "co-op retention" or ms["M1"]["due"] != "2026-12-01" or ms["M6"]["name"] != "Game 2" or \
            ms["M1"]["scope"] != "every vessel":
        bad.append(f"a milestone edited on the page did not round-trip through GitHub: {ms.get('M1')}")

    # migrate: the old mirror's issues are updated in place, never created twice; then nothing more to do
    api4 = FakeApi()
    s4 = Issues("o/r", api4)
    md = ("# Board\n\n" + MARK + "\n\n### T-20261015-aaaa · open · test · Regatta AI\n- **Task:** Fly it.\n"
          "- **Done when:** It flies; it lands\n- **Who can take it:** Yash (offered)\n"
          "- **Added:** 2026-10-15 by Yash's session (`Ys-bleeding-edge`)\n\n"
          "### T-20261015-bbbb · taken by Yash · test · Regatta AI\n- **Task:** Playtest it.\n- **Done when:** Done.\n"
          "- **Who can take it:** anyone\n- **Added:** 2026-10-15 by Yash\n- **Taken:** 2026-10-15, Yash\n\n"
          "### T-20261015-cccc · done · dev · Tooling\n- **Task:** Built it.\n- **Done when:** Built.\n"
          "- **Who can take it:** anyone\n- **Added:** 2026-10-15\n- **Done:** 2026-10-16, built\n")
    tasks = {x.id: x for x in parse(md)}
    legacy_body = ("**Status on the board:** open\n\n- **Task:** Fly it.\n- **Done when:** It flies; it lands\n"
                   "- **Who can take it:** Yash (offered)\n- **Added:** 2026-10-15\n\n---\nMirrored.\n"
                   "<!-- team-task:T-20261015-aaaa -->")
    only_gh = legacy_body.replace("T-20261015-aaaa", "T-20261015-dddd").replace("Fly it.", "Only on GitHub.")
    api4("POST", "repos/o/r/issues", {"title": "[T-20261015-aaaa] Fly it.", "body": legacy_body,
                                      "labels": [LABEL, "kind:test", "area:regatta-ai"]})
    api4("POST", "repos/o/r/issues", {"title": "[T-20261015-dddd] Only on GitHub.", "body": only_gh,
                                      "labels": [LABEL, "kind:test", "area:regatta-ai", "taken"]})
    if not all(tk.legacy for tk in s4.fetch()):
        bad.append("the old mirror's bodies did not read as legacy")
    rows, todo = plan_migrate(s4.fetch(), tasks, day)
    acts = {tid: a for tid, a, _ in rows}
    if acts != {"T-20261015-aaaa": "update #1", "T-20261015-bbbb": "create", "T-20261015-cccc": "create",
                "T-20261015-dddd": "update #2"}:
        bad.append(f"migrate would create a ticket the mirror already has, or miss one: {acts}")
    s4.ensure_milestones()
    for tk in todo:
        s4.write(tk)
    mig = {tk.id: tk for tk in s4.fetch()}
    if any(tk.legacy for tk in mig.values()) or mig["T-20261015-aaaa"].needs != "anyone" or \
            mig["T-20261015-bbbb"].state != "live" or mig["T-20261015-cccc"].state != "done" or \
            mig["T-20261015-aaaa"].steps != [[False, "It flies"], [False, "it lands"]] or \
            mig["T-20261015-aaaa"].area != "Regatta AI" or len(api4.issues) != 4 or len(api4.milestones) != 5:
        bad.append("migrate did not rebuild the tickets in the new shape")
    if any(a != "keep" for _, a, _ in plan_migrate(s4.fetch(), tasks, day)[0]):
        bad.append("a second migrate would write again")
    if [p[1] for p in plan_sweep(s4.fetch(), probe, day)] != ["no-branch"]:
        bad.append("a sweep right after migrate would change something")
    if bridge(s4.fetch(), day, tasks=tasks) or \
            len(bridge(s4.fetch(), day, tasks=dict(tasks, **{"T-20261020-eeee": Task(
                "T-20261020-eeee", "open", "test", "UI", {"Task": "New on an old board.", "Done when": "x",
                                                           "Added": "2026-10-20"}, [])}))) != 1:
        bad.append("the bridge filed a task GitHub has, or missed one it lacks")
    closed_md = {tid: Task(tid, "done", "test", "Regatta AI", {"Task": "Fly it.", "Done": "2026-10-20, ran it"}, [])
                 for tid in ("T-20261015-aaaa",)}
    closing = bridge(s4.fetch(), day, tasks=closed_md)
    if [(t.id, t.state, t.done) for t in closing] != [("T-20261015-aaaa", "done", "2026-10-20, ran it")] or \
            bridge(s4.fetch(), day, tasks={"T-20261015-cccc": Task("T-20261015-cccc", "open", "dev", "Tooling", {}, [])}):
        bad.append("the bridge did not close what an old board closed, or reopened what it had open")

    # pagination past 100, pr-section, the small helpers
    api5 = FakeApi([{"number": n, "title": f"[T-20261015-{n:04x}] t{n}", "body": "", "state": "open",
                     "labels": [{"name": LABEL}], "assignees": [], "html_url": ""} for n in range(1, 151)])
    if len(Issues("o/r", api5).fetch()) != 150:
        bad.append("fetch stopped at the first page")
    sec = pr_section(s3.fetch() + s4.fetch(), "claude/s")
    if f"**New** [`{font.id}`]" not in sec or pr_section(s3.fetch(), "claude/none"):
        bad.append(f"pr-section missed a filed ticket or reported one for another branch: {sec!r}")
    if pr_section(s3.fetch(), "claude/font").count("closes when it merges") != 1:
        bad.append("pr-section missed a ticket taken on the branch")
    if not re.fullmatch(r"claude/a\\\.b", ere("claude/a.b")) or canon("Shombith03") not in ("Shombith", "Shombith03"):
        bad.append("a branch name is not escaped for git --grep, or a name not mapped")
    if parse_block("\n".join(block_lines(Ticket("T-20261015-ffff", "dev", "A | B")))).get("area")[0] != "A / B":
        bad.append("a value with the block's separator was not cleaned")
    # A person's change on GitHub itself (label, milestone) is their override, and a write keeps it.
    gh_set = dict(api3.issues[1], labels=[{"name": n} for n in (LABEL, "kind:decision", "area:ui", "dept:testers",
                                                                 "prio:P3", "live")])
    kept = from_issue(gh_set)
    if (kept.dept, kept.priority) != ("testers", "P3") or not by_person(kept.dept_by) or not by_person(kept.prio_by):
        bad.append("a priority or department a person set on GitHub was not kept as theirs")
    print("team_tasks self-test: " + ("ok" if not bad else "FAILED: " + "; ".join(bad)))
    return 0 if not bad else 1


# --- CLI ------------------------------------------------------------------------------------------------------------

def main(argv):
    if "--self-test" in argv:
        return self_test()
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--repo")
    ap.add_argument("--offline", metavar="FILE", help="run on a saved dump of the issues instead of GitHub")
    sub = ap.add_subparsers(dest="cmd", required=True)
    ls = sub.add_parser("list")
    ls.add_argument("--dept")
    ls.add_argument("--area")
    ls.add_argument("--milestone")
    ls.add_argument("--all", action="store_true")
    ls.add_argument("--json", action="store_true")
    ls.add_argument("--md", action="store_true")
    ad = sub.add_parser("add")
    for f in ("area", "task", "done-when", "why"):
        ad.add_argument("--" + f, required=True)
    ad.add_argument("--kind", required=True, choices=KINDS)
    ad.add_argument("--priority", required=True, choices=PRIORITIES)
    ad.add_argument("--dept", required=True)
    ad.add_argument("--milestone")
    ad.add_argument("--see")
    ad.add_argument("--by")
    ad.add_argument("--who", default="anyone", help='"anyone", or "anyone with ..." for what the work needs')
    ad.add_argument("--dry-run", action="store_true")
    sp = sub.add_parser("step")
    sp.add_argument("--line", required=True)
    sp.add_argument("--id")
    sp.add_argument("--dry-run", action="store_true")
    tk = sub.add_parser("take")
    tk.add_argument("id")
    tk.add_argument("--branch", required=True, help="the branch you do it on; the ticket closes when it merges")
    tk.add_argument("--who")
    tk.add_argument("--dry-run", action="store_true")
    for name in ("release", "done"):
        p = sub.add_parser(name)
        p.add_argument("id")
        p.add_argument("--note", required=name == "done")
        p.add_argument("--dry-run", action="store_true")
    ar = sub.add_parser("archive")
    ar.add_argument("ids", nargs="+")
    ar.add_argument("--note")
    ar.add_argument("--dry-run", action="store_true")
    rv = sub.add_parser("revive")
    rv.add_argument("id")
    rv.add_argument("--dry-run", action="store_true")
    st = sub.add_parser("set")
    st.add_argument("id")
    st.add_argument("field", choices=("priority", "dept", "milestone"))
    st.add_argument("value")
    st.add_argument("--by", required=True)
    st.add_argument("--why", default="")
    st.add_argument("--dry-run", action="store_true")
    sw = sub.add_parser("sweep")
    sw.add_argument("--dry-run", action="store_true")
    sw.add_argument("--no-fetch", action="store_true", help="skip `git fetch --prune origin` (branch state may be stale)")
    sw.add_argument("--base", default=BASE)
    sw.add_argument("--idle-days", type=int, default=IDLE_DAYS)
    sw.add_argument("--merged", metavar="BRANCH", help="a branch the caller knows just merged (the PR event)")
    sw.add_argument("--pr")
    sy = sub.add_parser("sync")
    sy.add_argument("file", help='JSON: the page store, {"tasks": [...], "takes": [...], "overrides": [...]}')
    sy.add_argument("--since", default="", help="the last sync's time (ISO); older page changes are skipped")
    sy.add_argument("--dry-run", action="store_true")
    sy.add_argument("--all", action="store_true", help="give back every ticket's document, not only the changed ones")
    pr = sub.add_parser("pr-section")
    pr.add_argument("--base", default=BASE)
    mg = sub.add_parser("migrate")
    mg.add_argument("--dry-run", action="store_true")
    rw = sub.add_parser("review")
    rw.add_argument("ids", nargs="*")
    rw.add_argument("--all", action="store_true")
    rw.add_argument("--area")
    rw.add_argument("--batch", type=int)
    rw.add_argument("--apply", metavar="FILE", help="JSON: [{id, priority?, dept?, milestone?, why, duplicateOf?}]")
    rw.add_argument("--dry-run", action="store_true")
    ms = sub.add_parser("milestone")
    ms.add_argument("action", choices=("list", "add"))
    ms.add_argument("name", nargs="?")
    ms.add_argument("--gate", default="")
    ms.add_argument("--due")
    sub.add_parser("me")
    a = ap.parse_args(argv)

    if a.cmd == "me":
        print(me() or "unknown: set git config user.email to an address in Docs/TEAM.json")
        return 0
    repo = a.repo or ("o/r" if a.offline else repo_name())
    api = FakeApi.load(a.offline) if a.offline else gh_api
    store = Issues(repo, api)
    today = datetime.date.today()
    dry = getattr(a, "dry_run", False)

    def save(*tickets):
        if dry:
            print("(dry run, nothing written)")
            return
        store.ensure_labels({l for t in tickets for l in issue_labels(t)})
        for t in tickets:
            store.write(t)
        if a.offline:
            api.save()

    if a.cmd == "list":
        tickets = [t for t in store.fetch() if (a.all or t.is_open) and t.state != "merged"]
        if a.dept:
            tickets = [t for t in tickets if a.dept.lower() in (t.dept, DEPTS.get(t.dept, "").lower())]
        if a.area:
            tickets = [t for t in tickets if a.area.lower() in t.area.lower()]
        if a.milestone:
            tickets = [t for t in tickets if t.milestone == a.milestone.upper()]
        tickets.sort(key=lambda t: (list(DEPTS).index(t.dept) if t.dept in DEPTS else 9, t.priority, t.id))
        if a.json:
            print(json.dumps([page_doc(t) for t in tickets], indent=1, ensure_ascii=False))
        elif a.md:
            print("| id | prio | dept | area | task | state |\n|---|---|---|---|---|---|")
            for t in tickets:
                print(f"| [{t.id}]({t.url}) | {t.priority} | {DEPTS.get(t.dept)} | {t.area} | "
                      f"{t.title[:90].replace('|', '/')} | {t.state}{' ' + t.branch if t.branch else ''} |")
        else:
            for t in tickets:
                print(t.line())
            print(f"{len(tickets)} tickets; the board: {PAGE_URL}")
    elif a.cmd == "add":
        t, new = add(store.fetch(), a.area, a.kind, a.task, a.done_when, a.priority, a.dept, a.why, a.milestone, a.see,
                     a.by, a.who, today)
        save(t)
        print(f"filed {t.id}" + (f" (#{t.number})" if t.number else "") if new else
              f"this branch already has {t.id}: added a checklist line to it instead (one ticket per branch)")
    elif a.cmd == "step":
        tickets = store.fetch()
        t = find(tickets, a.id) if a.id else next((x for x in tickets if x.filed_on == branch() and x.is_open), None)
        if t is None:
            raise SystemExit(f"{branch()} has no open ticket; file one with `add`")
        t.steps.append([False, a.line.strip()])
        save(t)
        print(f"{t.id}: added a checklist line")
    elif a.cmd in ("take", "release", "done", "revive", "set"):
        t = find(store.fetch(), a.id)
        if a.cmd == "take":
            who = canon(a.who) if a.who else me()
            if not who:
                raise SystemExit("cannot tell who you are from git config: pass --who <your name>")
            take(t, who, a.branch, today)
            if git("rev-parse", "--verify", "--quiet", f"refs/remotes/origin/{a.branch}").returncode != 0:
                print(f"note: origin/{a.branch} does not exist yet; push it, or the sweep reads it as deleted and "
                      f"reopens the ticket")
        elif a.cmd == "release":
            release(t, a.note, today)
        elif a.cmd == "done":
            close_done(t, a.note, today)
        elif a.cmd == "revive":
            revive(t, today)
        else:
            set_class(t, a.field, a.value, canon(a.by), today, a.why)
        save(t)
        print(f"{t.id}: {t.state}" + (f", {t.taken_by} on {t.branch}" if t.state == "live" else "") +
              (f"; {a.field} {getattr(t, {'priority': 'priority', 'dept': 'dept', 'milestone': 'milestone'}[a.field])}"
               if a.cmd == "set" else ""))
    elif a.cmd == "archive":
        tickets = store.fetch()
        done = [find(tickets, tid) for tid in a.ids]
        for t in done:
            archive(t, a.note, today)
        save(*done)
        print(f"archived {len(done)}: {' '.join(t.id for t in done)}")
    elif a.cmd == "sweep":
        sweep(store, a.dry_run, not a.no_fetch and not a.offline, a.base, a.idle_days,
              (a.merged, a.pr) if a.merged else None, today)
        if a.offline and not a.dry_run:
            api.save()
    elif a.cmd == "sync":
        data = json.loads(read(a.file) or "{}")
        out = sync(store, data, a.since, a.dry_run, today, every=a.all)
        print(json.dumps(out, indent=1, ensure_ascii=False))
        if a.offline and not a.dry_run:
            api.save()
    elif a.cmd == "pr-section":
        here = branch()
        sec = pr_section(store.fetch(), here)
        old = pr_section_markdown(read(BOARD), git_show(a.base))
        print("\n\n".join(s for s in (sec, old) if s))
    elif a.cmd == "migrate":
        migrate(store, a.dry_run, today, repo)
        if a.offline and not a.dry_run:
            api.save()
    elif a.cmd == "review":
        tickets = store.fetch()
        if a.apply:
            decisions = json.loads(read(a.apply) or "[]")
            rows, changed = review_apply(tickets, decisions, today)
            for tid, field, what, why in rows:
                print(f"{tid}  {field:<10} {what:<34} {why}")
            n = collections.Counter(f for _, f, _, _ in rows)
            print(f"review: {len(changed)} tickets change; {dict(n)}" + (" (dry run, nothing written)" if a.dry_run else ""))
            if not a.dry_run:
                for t in changed:
                    store.write(t)
                if a.offline:
                    api.save()
        else:
            print(json.dumps(review_rows(tickets, set(a.ids), a.area, a.all, a.batch), indent=1, ensure_ascii=False))
    elif a.cmd == "milestone":
        if a.action == "list":
            for m in store._pages(f"repos/{repo}/milestones?state=all"):
                print(f"{m['title']:<40} open {m.get('open_issues', 0):>3}  closed {m.get('closed_issues', 0):>3}  "
                      f"due {(m.get('due_on') or '-')[:10]}  {(m.get('description') or '').splitlines()[-1:]}")
        else:
            if not a.name:
                raise SystemExit("milestone add NAME --gate TEXT [--due YYYY-MM-DD]")
            payload = {"title": a.name, "description": f"Gate: {a.gate or '-'}"}
            if a.due:
                payload["due_on"] = f"{a.due}T00:00:00Z"
            m = api("POST", f"repos/{repo}/milestones", payload)
            print(f"created milestone {m['title']} (#{m['number']})")
            if a.offline:
                api.save()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

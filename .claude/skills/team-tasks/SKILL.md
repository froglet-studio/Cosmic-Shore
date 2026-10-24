---
name: team-tasks
description: Froglet Tasks from a session - the team's ticketing system (GitHub issues labelled team-task, kept by Tools/Build/team_tasks.py, worked on the Froglet Tasks page). Use when the person asks to list, add, suggest, take, close, release, set (re-rank) or review team tasks, to sync the page, or on `/team-tasks`; and in a session started from a ticket on the page. A session never files a ticket its person has not approved. Never on every prompt.
---

# /team-tasks - the team's hand-off tickets, from a session

Tickets are GitHub issues on `froglet-studio/Cosmic-Shore` labelled `team-task`. `Tools/Build/team_tasks.py` is the
only writer a session uses; the board people read is the **Froglet Tasks page**,
https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f. `.github/workflows/team-tasks.yml` runs the sweep (merges,
deletions, nightly). Owner of this tooling: Shombith. The page's source is `Tools/Build/team_tasks_page.html`: edit it
there and republish it to the same url (Artifact tool, `url` above); its Info view lists this skill's commands, so a new
command lands there too.

## The rules (Docs/TEAM.md rule 9; every command below enforces its part)

1. **Never assigned.** An untaken ticket belongs to nobody. Never put a person in `--who` (only "anyone" or
   "anyone with <what the work needs>"), never assign on GitHub (the sweep clears it), never write "X offered".
2. **Taking names your branch.** A live ticket shows the person and the branch.
3. **The branch decides.** It merges into bleeding-edge: the ticket closes. Deleted, released, or 14 days without a
   commit: back to open, nobody's.
4. **14 days untaken: archived.** Done and archived both come back with `revive`; the 14 days restart.
5. **One ticket per branch.** All of a branch's hand tests go in its one ticket's checklist (`add` appends when the
   branch has one; `step` adds a line).
6. **No favourites.** Priority ranks tickets, milestones rank time. Nothing compares people.
7. **Approved, never automatic.** A session files a ticket only when its person says yes. It may SUGGEST one
   (`/team-tasks suggest`): the suggestion waits on the page until that person, or an approver, approves it. No
   end-of-session filing, no re-classifying on your own.
8. **No check-in / check-out.** Nothing tracks presence or time.

## /team-tasks - what is open (read-only)

```
python3 Tools/Build/team_tasks.py list --json
```

Show it by department (Testers, Developers, Decisions, Content & docs), P1 and P2 first, each as `id - title (area)`;
live tickets with the person and branch. Say how many there are and give the page link. Do not print every ticket
when there are dozens: show the P1s and P2s, then the counts per department, and offer the rest.

## Who uses it, and how

People sign in with their **claude.ai account**: the page is a claude.ai artifact, shared from its Share menu. The
approvers (Docs/TEAM.json `approvers`: the core four and Iggy) are its Editors: they edit milestones and the base
branch, and let other people in as Contributors, who can take, file, suggest and give feedback. GitHub is not a login;
the page reaches GitHub through the sync below. Developers are encouraged, not obliged, to open the page when they
start a day; this skill does everything the page does.

## A session started from a ticket

The page's "Start in a new session" opens a Claude Code session on the repo, from the base branch, with the ticket as
its prompt. Its first step is `team_tasks.py take <id> --branch "$(git branch --show-current)" --who <name>`; then the
work; then `done <id> --note "<proof>"` when the person agrees it is done. The ticket also closes when the branch
merges.

## /team-tasks suggest - propose a ticket for the person to approve

When the work leaves something for someone else and the person has not asked you to file it, suggest it instead:
write `suggestions/<T-id>` on the page (ArtifactData `set`, url below) with `{"id", "task", "doneWhen", "area",
"kind", "priority", "dept", "milestone", "why", "from": "<person>'s session on <branch>", "forName": "<person>",
"filedOn": "<branch>", "status": "pending", "at": <now>}`, and tell the person it waits on the page's Suggested list.
It becomes a ticket when they (or an approver) approve it and the next sync runs. If the person says yes in the
conversation, `add` it directly instead.

## /team-tasks add - file what this branch leaves for someone else (the person said yes)

Gather from the work this session did: area, kind (`test dev content setup decision docs`), the task (one sentence a
person can act on), the done-when steps (`; `-separated: each becomes a checklist line), and links (`--see`).
Then decide, from what the session knows (the branch, what broke, what it touches):

| Field | Values | How to decide |
|---|---|---|
| `--priority` | `P1` `P2` `P3` | P1 blocks a person or a build, or ships broken or silently. P2 is needed for the current milestone (M1). P3 everything else. |
| `--dept` | `testers` `developers` `decisions` `content` | test -> testers; dev, setup -> developers; decision -> decisions; content, docs -> content (unless the work says otherwise) |
| `--milestone` | `M1`..`M5` or omit | M1 closed testing on all platforms (every vessel and mode tested, performance); M2 activation, FTUE, analytics, beta keys; M3 retention and referral; M4 early access (feature complete); M5 full release |
| `--why` | one line | the words or facts that decided the three above. Required. |

```
python3 Tools/Build/team_tasks.py add --area "<area>" --kind test --task "<task>" \
  --done-when "<step>; <step>" --see "<links>" --by "<who asked>'s session" \
  --priority P2 --dept testers --milestone M1 --why "<one line>"
```

If the branch already has an open ticket, `add` appends a checklist line to it instead and says so (one ticket per
branch); `step --line "<step>"` adds a single line. Tell the person the id (and issue number), and that it is
nobody's until someone takes it. On a shared branch (bleeding-edge, Ys-bleeding-edge) each `add` files its own
ticket.

## /team-tasks take | done | release | archive | revive | set

```
python3 Tools/Build/team_tasks.py take <id> --branch <branch> [--who <name>]   # who defaults to git config (Docs/TEAM.json)
python3 Tools/Build/team_tasks.py done <id> --note "<the check that proved it>"
python3 Tools/Build/team_tasks.py release <id> --note "<why>"
python3 Tools/Build/team_tasks.py archive <id>... [--note "<why>"]
python3 Tools/Build/team_tasks.py revive <id>
python3 Tools/Build/team_tasks.py set <id> priority|dept|milestone <value> --by <person> [--why "<why>"]
```

- `done` needs the proof, the `/bug-closeout` rule: name the check, the commit or the result. A test that FAILED is not
  done: say what failed in the ticket (or `step` a fix line on this branch's ticket).
- `set` is the person's override: attributed, and nothing automatic changes it again until `set <id> <field> clear`.
  A priority, department or milestone a person changes on GitHub itself counts the same.
- A live ticket is taken only by its taker; anyone else asks them to release it.

## /team-tasks review [--all | AREA | ID...] - re-read tickets and propose a classification

Only when the person asks (it was asked for once after the migration; any time later for an area).

1. `python3 Tools/Build/team_tasks.py review --all --batch 1` (then `--batch 2`, ...; 25 per batch), or
   `review --area <area>`, or `review <id> <id>`.
2. For each ticket decide `priority`, `dept`, `milestone` (the table above), a one-line `why` naming the words or
   facts that decided it, and `duplicateOf` when it is the same work as an older ticket filed from another branch.
3. Write the decisions to a JSON file: `[{"id": "T-...", "priority": "P2", "dept": "testers", "milestone": "M1",
   "why": "...", "duplicateOf": "T-..."}]` (omit a field you would not change).
4. `python3 Tools/Build/team_tasks.py review --apply <file> --dry-run` prints the table (old -> new, why, merges).
   **STOP: show the person the table and ask them to approve, edit or reject rows.** Nothing is written before a yes.
5. On approval: `review --apply <file>`. It never touches a field a person set; it merges each duplicate into the
   older ticket (combined checklist; the duplicate closes as not planned, without `archived`); it comments
   "re-evaluated <date> by a Claude session: <why>" on each changed ticket. Report the counts it prints.

## /team-tasks sync - the page's store <-> Issues (the scheduled task runs this; a person may ask for it)

The page cannot reach GitHub. What people do there lands in the page's store; this carries it across, and brings the
issues, milestones, branches and the week's merges back.

1. `git fetch --prune origin`, then read the page's store with ArtifactData (url above, `list` with `out_dir`, limit
   1000): `tasks`, `takes`, `overrides`, `new` (tasks filed on the page), `suggestions`, `milestones`; and `get`
   `meta/sync`.
2. The page stores who acted only as an id (`byId`). Resolve them with ArtifactData `profiles` (`ids`), set each
   document's `by` to that name (the sync maps it to the team name in `Docs/TEAM.json`), and bundle them into one JSON
   file: `{"tasks": [...], "takes": [...], "overrides": [...], "new": [...], "suggestions": [...], "milestones": [...]}`.
3. `python3 Tools/Build/team_tasks.py sync <file> --since <meta/sync.at>` applies, to GitHub, the takes and
   re-ranks made after the last sync, files the page's new tasks and approved suggestions, and saves edited
   milestones; it prints `{"at", "applied", "skipped", "store": [ops], "counts"}`.
4. Apply `store` in order with ArtifactData `batch` (50 at a time; each op is `{op, collection, doc_id, data}`; pin
   each write to the version you read, and create documents that do not exist yet without a pin).
5. Run `python3 Tools/Build/team_tasks.py sweep` too when the workflow has not run (it closes merged branches'
   tickets). Nothing else is filed, closed or re-ranked.

## Never

- Never name a person in `--who` or as an assignee, and never write who "should" do a ticket.
- Never file or close tickets the person did not ask about, and never at the end of a session unasked.
- Never edit the machine block (`<!-- team-task ... -->`) by hand, and never the markdown board
  (`Docs/TEAM_TASKS.md` is a stub; a task a branch still writes there in the old format is filed by the next sweep).
- Never turn on `.claude/hooks/team_tasks_nudge.py` again (Shombith, 2026-10-23: the per-prompt hooks slowed every chat).

## /ship

`/ship` puts `python3 Tools/Build/team_tasks.py pr-section` in the PR body as its **Hand-offs** section: the tickets
this branch filed or took, with links.

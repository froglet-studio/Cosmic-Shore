---
name: approve
description: Your approval queue - the changes other people's sessions made to YOUR work (an area you own, built, tuned or decided), which went through without waiting for you and now ask you to approve them after the fact or talk to the person who made them. Retrieves your list from every branch (Docs/Approvals/<You>/, Tools/Build/approvals.py), walks you through each entry, records "approved" or "discuss" (and later "resolved"), drafts the message to the person when you want to talk, and commits your answers. Also shows what YOUR changes queued for others and how they answered. Use on "/approve", "my approvals", "what changed in my work", "who touched my stuff", "my queue", "approve <id>", "what did my changes queue", "did <person> answer", or when a session's check said it queued an entry for you.
---

# /approve - your queue of changes to approve after the fact

**Why this exists** (Garrett, 2026-11-10): asking for approval before every change to someone else's work was slowing
everyone down. Now a change goes through unless it **collides with an occupied swim lane** (`/swimlanes`; someone else
has unmerged work in that vessel, game, app-shell section or system right now). Everything else goes through, the session tells
its person whose toes it is on so they talk, and the change lands in the queue of each person whose work it changed.
This skill is how you read and answer that queue. The rules: `Docs/TEAM.md` rules 3, 12 and 13. The files:
`Docs/Approvals/README.md`.

## 1. Who you are

The queue is per person (`Docs/TEAM.json` names). Take the person from the conversation (who they said they are, or
the account's email looked up in `Docs/TEAM.json` - never write the email anywhere). If you cannot tell, ask once.

## 2. Retrieve the list

```bash
git fetch --prune origin
```

```bash
python3 Tools/Build/approvals.py list --person <You>
```

It reads `Docs/Approvals/<You>/` on `bleeding-edge`, on every branch pushed in the last 60 days, and in the working
tree. Each entry says who made the change, what changed in your work and why, its files, and **where** it is:
landed on `bleeding-edge`, or not merged yet (on its branch, with its PR).

**The order is loop impact first** (Garrett, 2026-11-12: "prioritizing sign off on the items most likely to affect the
velocity of things through the process"). Each open entry that changes how a vessel plays carries `LOOP IMPACT <n>`
and an "answer early" line: the elemental loop work that would be redone NOW if you said it goes against the spirit
of the vessel - the vessel's fun, intensity and AI steps already doing, built or signed off (a retraining weighs 3, a
signed-off step double) and the arena environment creator trained against that AI (`elemental_loop.rework`). The
number grows while the entry waits, as the loop builds on the change, so catching it now is what saves the
retraining at each step downstream. Then entries you answered `discuss`, then pending, oldest first. Entries about
the look (HUD, colour, icons, art), tools or docs carry no loop impact. `--all` adds the answered ones; `--json` for a
page (`loopImpact`, `loopRework`).

If the list is empty, say so in one line and stop.

## 3. Walk through it with the person

Start with the entries that carry a loop impact, highest first, and say in a line what each would send back. Give a
short plain summary of each entry (one or two lines: who, what changed in their work, where it is). For
anything that needs a closer look, read the change itself (`git log -p origin/<branch> -- <paths>` or the PR) and say
what it does in their terms. Then ask, a few entries at a time with `AskUserQuestion` (up to four entries per call,
one question each):

- **Approve** - "I agree with it." Recorded; the entry leaves the queue.
- **Talk to <author>** - "I want to discuss it." Ask what about, in their words (it becomes the note).
- **Skip for now** - nothing is written; it stays in the queue.

Never answer for the person, and never pick for them because the change looks fine.

## 4. Record the answers

```bash
python3 Tools/Build/approvals.py reply <id> approve --by <You> [--note "..."]
```

```bash
python3 Tools/Build/approvals.py reply <id> discuss --by <You> --note "<what they want to talk about>"
```

Only the entry's own person can answer it (`--by` must be them). An id prefix is enough when it is unique. Each answer
is its own small file beside the entry (`<id>.<time>.reply.json`), so answers never conflict with anything.

**For a `discuss`**, help them start the conversation: draft a short message to the author naming the change (the
entry's summary, branch, PR) and the concern. Offer to post it as a comment on the PR when there is an open one -
only with their yes, since a comment notifies people (end it with the attribution footer). Otherwise they send it
their own way. The author sees the answer under `list --from <author>`.

**After talking**, close it with the outcome: `reply <id> resolve --by <You> --note "kept it" | "reverted on <branch>" |
"<author> changes X"`. A change they want undone is new work on a branch like any other, by them or by the author;
the queue only records the conversation.

## 5. Commit the answers

Answers are files, so they must be committed and pushed to reach anyone:

```bash
git add Docs/Approvals && git commit -m "docs(approvals): <You> answers <n> entries"
```

Commit on the branch the session is working on and push it there (never straight to `bleeding-edge`, never to someone
else's branch). The queue reads every branch, so the answer counts as soon as it is pushed, before that branch merges.

## 6. What your own changes queued

```bash
python3 Tools/Build/approvals.py list --from <You>
```

Shows the entries your sessions filed for others and their answers (`--all` for the approved ones too). A `discuss`
answer is someone asking to talk: tell the person, and help them reply. `list --counts` is one line of how many
entries wait for each person.

## 7. Filing entries (for the session that makes a change)

This is the other half, run by any session that changes someone else's work (`/ship` §5 runs it too):

```bash
python3 Tools/Build/approvals.py check --me <You>
```

- **Exit 2, COLLISION**: the change is about a swim lane someone else occupies. Stop and tell the person in one line
  (who, which branch, PR, last push). They talk to the occupant or decide to go ahead; if they go ahead, write
  "Affects: <occupant>'s <lane> work on <branch>" in the commit and queue the occupant an entry.
- **Exit 0**: it goes through. For each person under HEADS-UP, tell your person in one line whose work this touches,
  so they can talk. Then queue an entry for each one whose work the change actually CHANGES - re-tunes, replaces,
  retires, renames, decides otherwise, or edits an area they declared their own. A pure addition next to their work
  needs no entry; that is a judgement, and when unsure, queue it.

```bash
python3 Tools/Build/approvals.py add --to <Them> --by <You> --area "<their area, in their words>" \
  --summary "<what changed in it, one sentence>" --why "<why>" --paths <the files of theirs it changed>
```

Put every file after ONE `--paths`. It takes a list, and argparse accepts `--path` as its abbreviation, so
`--path a --path b` files only `b` and says nothing (2026-11-10: an entry lost all but its last path that way).
Read the entry's `paths` back before you commit it.

**A change to how a vessel plays** (its abilities, its arcade game, its intensities or its AI) is where a late
"that is not this vessel" costs the most retraining, so name it for the loop: `--vessel <Vessel> [--element Time ...]
[--loop-step 1|2|3]` (1 the vessel or its fun, 2 its intensities, 3 its AI). Left out, the vessel, element and step are
read from the entry's area and play files; the `--paths` matter for that. The owner's `/approve` then ranks it by what
it would send back (section 2).

Commit the entry with the change, so it lands with it, and keep the "Affects:" line in the commit or PR.

The script is tooling: Shombith heads the Claude skills and tools (CLAUDE.md § Team). A change to how it decides is
itself queued for him.

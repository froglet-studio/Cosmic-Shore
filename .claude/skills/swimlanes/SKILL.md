---
name: swimlanes
description: Show which swim lanes of Cosmic Shore are OCCUPIED right now and which open ones to work on next - a lane is anything someone holds in their head that takes more than a couple of prompts and one PR - a vessel (the Thresher, the Goby, ...), a game (Bloomrush, Regatta, ...), a section of the app shell (the Voyages map, the Hangar, Settings, the in-game HUD) or a mechanical system (ecology, prisms, netcode, the AI platform, ...) - never a task. Ranked from git and the team's own records by swimlanes.py - open lanes with obvious work waiting on top, occupied lanes under them, then lanes that need polish, then polished ones. Use on "/swimlanes", "what should I work on next", "what's free", "is anyone working on <vessel / game / screen / system>", "who is in the <X> lane", "am I going to collide with someone", "find me the next thing", "mark <lane> polished / rough", "add a lane", "archive a lane", or before starting work on a vessel, a game, an app-shell section or a system.
---

# Swim lanes - find the next thing, and don't collide

A swim lane is anything someone can hold in their head that takes more than a couple of prompts and one pull request
to finish: a **vessel**, a **game** (a mode with its own card), a **section of the app shell** (the Voyages map, the
Hangar, the Arcade screen, Settings, the in-game HUD, ...) or a **mechanical system** (ecology, prisms, netcode, the AI
platform, audio, ...). It is **not a task**. Yash clearly working on the Thresher Time games means the Thresher lane
(and the Thresher Time game lane) is occupied: someone else should pick another lane, or talk to Yash first.

**Everything that size gets a lane** (Caleb, 2026-11-11: "the whole point of tracking them is to help us coordinate our
efforts"). Untracked work cannot collide on the board, so it collides in a merge instead. The cut-off is a judgment,
not a number: a couple of prompts and one PR is the rough line. Vessels and games need nothing (section 5); a shell
section or system that no lane covers is added in the same commit that starts the work, and archived when it is truly
closed out. Keep the list usable: a lane exists to tell people apart, so do not add one per folder.

**Occupied is the only thing that says "not now"** - and the only thing that slows a change down anywhere (Garrett,
2026-11-10; `Tools/Build/approvals.py check` exits 2 on it). Work that already merged never blocks anyone: who built
a lane before is context (read it and build on it, `Docs/TEAM.md` rule 3), not a reason to stay out; a change to what
they built goes into their `/approve` queue instead of waiting for them. A lane opens by itself
the moment its work merges into `bleeding-edge`, or when the branch goes quiet for the window (7 days).

`swimlanes.py` is a READER: it writes nothing except a person's verdict (section 4).

## 1. Run it

```bash
git fetch --prune origin
```

```bash
python3 .claude/skills/swimlanes/swimlanes.py
```

About 15-20 s. It prints four groups, in this order:

| Group | What puts a lane there |
|---|---|
| **Open - work waiting** (top: pick one of these) | Nobody is in it, and there is obvious work: open, untaken Froglet Tasks tickets that name it; editor hand tests not yet run (`Docs/UNITY_VERIFICATION_CHECKLIST.md`); open Bug Ledger errors thrown from its files; placeholder files; level-5 upgrades named but not gated. Ranked by score (a P0 ticket weighs 5, P3 2; each other item 1, capped per kind). |
| **Occupied** | An unmerged branch has pushed work focused on it in the last 7 days. Shows who, the branch, its PR, the last push and the newest commit. Newest first. |
| **Open - needs polish** | No obvious work, but measured gaps: undesigned level-5 upgrades, no model dossier, no arcade card of its own, one of the style guide's five worst screens, no edit-mode test names it, five or more TODOs - or a person said "rough". |
| **Open - polished** (bottom) | None of that, or a person said "polished". |

Each open lane also says when it last opened ("last landed": the newest merge about it), and a lane with a declared
owner (`Docs/TEAM.json` owners, or a note in `lanes.json`) carries a `!` line: FTUE and the start flow are Shombith's
and Caleb's - tell them when you take one, and queue what you change for their `/approve`.

Options:

| Ask | Flag |
|---|---|
| one lane in full: every occupant, every work item with its ticket number, every polish gap | `--lane <key>` (e.g. `--lane thresher`, `--lane voyages`) |
| every item on the board, not the top three | `--full` |
| one kind of lane only | `--kind vessel` / `game` / `shell` / `system` |
| a page or another tool | `--md` / `--json` |
| a shorter or longer occupancy window | `--days N` |
| no GitHub (no tickets, no PR authors) | `--no-github` |
| skip the Bug Ledger scan | `--no-bugs` |
| prove it | `--self-test` |

## 2. How occupancy is read

- Every `origin/*` branch updated in the window, minus the bots' branches (`bugledger/*`, `compute*`, `perf-status`,
  `launch-status`, `ai`, scratch and build branches; `lanes.json` `ignore_branches`), and minus a branch whose head a
  CLOSED pull request carries - merged (a squash merge) or closed without merging (abandoned: Shombith's phone-UI
  stack, #1744). A push after the close counts again.
- A branch whose last push about a lane is OLDER than the lane's newest merge has been overtaken and holds nothing
  (`claude/manta-icons`, a 11-01 draft, after the 11-07 Manta cards landed).
- People in `lanes.json` `non_occupants` (interns: dbrutus, Garrett 2026-11-08) never hold a lane. A fix of theirs goes
  to the lane's occupant to apply (it went to Yash as #1849).
- Its own commits that `bleeding-edge` lacks, minus the sweeps' commits (`chore(ai-learn)`, ...; `ignore_subjects`).
- A commit is **about** a lane when a quarter of its files (or three of them) are the lane's. A commit about three or
  more lanes at once is a sweep (a fleet re-bake, a rename) and occupies none. A one-file brush with a Hangar prefab
  inside a party fix does not occupy the Hangar.
- The person: the branch prefix in `Docs/TEAM.json` (`Ys-` is Yash), the author of its pull request (open, else the
  newest closed one), then the
  people in its commits (`who_works_where.py`'s `Team`, the one place a git identity becomes a person). A Claude
  branch with none of those reads as "a Claude session (no person on record)" - open its PR or ask.

## 3. Answer the person

- **"What should I work on next?"**: first, the **elemental vessel loop is the top priority** (Garrett, 2026-11-12):
  give them the top of `python3 Tools/Build/elemental_loop.py next` (`/elemental-loop`) and say which vessel lane each
  action sits in, skipping any that is occupied. Then, for anyone who wants something else, run the board and give
  them the top of **Open - work waiting** - the first three
  or four lanes with their top item each - then one line naming the occupied lanes to stay out of. If they say what
  they like (vessels, UI, a platform), pick from that. Offer `--lane <key>` for the one they lean to.
- **"Is anyone in <X>?"**: `--lane <key>`. If occupied, say who, the branch, the PR and the last push in one line,
  and suggest they talk to that person or take another lane. Never tell them they may not work there: it is a
  heads-up, not a lock.
- **Before you start work on a vessel, a game, an app-shell section or a system** (any session): run `--lane <key>`
  for it (if no lane covers work that will take more than one PR, add one, section 5, in your first commit). If it is
  occupied, tell the person in one line before changing anything. If the person still wants to go ahead, go ahead -
  say in the commit "Affects: <occupant>'s <lane> work on <branch>" (`Docs/TEAM.md` rule 7) and queue the occupant an
  entry (`/approve` §7).
- **Before you commit** (any session, any files): `python3 Tools/Build/approvals.py check --me <person>` reads the
  lanes of the change itself. A change ABOUT an occupied lane (a quarter of its files, or three) is a collision and
  waits for the person's decision; a brush, and everything outside the lanes, goes through with a heads-up and
  `/approve` entries.
- **"What's waiting for me?"**: alongside the board, `python3 Tools/Build/approvals.py list --person <them>` is their
  approval queue (`/approve`): mention how many entries wait, in one line.
- **After you push**: nothing to do. The lane shows you as its occupant while your branch is unmerged; when it merges
  the lane opens, and whatever your work left (a hand test in the checklist, a ticket) puts it back on top for the
  next person.

## 4. Polished or rough is a person's call

The measured polish gaps are a guess. A person's word beats them:

```bash
python3 .claude/skills/swimlanes/swimlanes.py set thresher rough --by Garrett --note "the Belfry needs a sound pass"
```

`polished` sends a lane to the bottom, `rough` adds a gap with the note, `clear` hands it back to the measures. It
writes `verdicts` in `lanes.json`: commit that file so everyone sees it. Only write a verdict the person said.

## 5. Adding, fixing or archiving a lane

`lanes.json` beside this file is the catalog. There are four kinds of lane (`--kind vessel|game|shell|system`):

- **Vessels** come from the roster (each `Assets/Resources/ElementalAbilityMaps/<Vessel>.asset`) and need no entry. Add
  an alias when the vessel's files use another name (`Goby: Pistol`, `Honeyguide: Ratel`); add a retired one to
  `retired`.
- **Games** come from the shipped cards (each `Assets/_SO_Assets/Games/ArcadeGame<Name>.asset` at the top of that
  folder, arcade and arena alike; `_Archive`, `Training` and `Missions` are not modes) and need no entry. The lane is
  named from the card's `DisplayName` and matches `<Name>` and its spaced form (`SerpentTime`, `Serpent Time Cell`).
  `games.aliases` adds a name its files use; `games.retired` drops one whose card still exists. A new mode is a lane
  the moment its card is committed.
- **Shell lanes** (`shell`) and **systems** (`systems`) are a key, a name, `tokens` (matched at a word or CamelCase
  boundary in any path: `Voyage` matches `VoyageMapGraphic.cs` and `Docs/Voyages/`, never `Envoyage`), `paths`
  (prefixes, or globs such as `Docs/PRISM_*`) and an optional `note` (an owner to tell). Keep tokens specific: a token
  most commits carry makes the lane useless. Add a system when work on it can collide in practice (two people in the
  same mechanism), not for every folder. Measure a new one before committing it: the files it matches, and that it
  does not swallow another lane's files.
- **Archive, then delete.** When a shell section or system is truly closed out, set `"archived": "<date>, <who>: why"`
  on its entry: it leaves the board and the collision check, and its history stays readable. Delete the entry only
  once nobody needs that. A retired vessel or game goes into its `retired` list.
- **Sweeps.** A commit about three or more lanes OF ONE KIND (three vessels, three games, three systems) is a sweep and
  occupies none of that kind; a vessel change that also touches its game and the AI system is about all three.
- **Readability.** The board lists every occupied lane, the top 12 open lanes with work and the top 8 of each quiet
  tier; `--full` or `--kind` shows the rest.
- Run `--self-test` and the board after a change, and read the lane with `--lane` to see it picks up what you meant.

The script is tooling: Shombith heads the Claude skills and tools (CLAUDE.md § Team). Say "Affects: Shombith's
tooling" in a commit that changes how it ranks.

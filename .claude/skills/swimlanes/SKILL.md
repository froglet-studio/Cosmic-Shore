---
name: swimlanes
description: Show which swim lanes of Cosmic Shore are OCCUPIED right now and which open ones to work on next - a lane is a vessel (the Thresher, the Goby, ...) or a section of the app shell someone holds in their head (the Voyages map, the Hangar, Settings, the in-game HUD), never a task. Ranked from git and the team's own records by swimlanes.py - open lanes with obvious work waiting on top, occupied lanes under them, then lanes that need polish, then polished ones. Use on "/swimlanes", "what should I work on next", "what's free", "is anyone working on <vessel / screen>", "who is in the <X> lane", "am I going to collide with someone", "find me the next thing", "mark <lane> polished / rough", or before starting work on a vessel or an app-shell section.
---

# Swim lanes - find the next thing, and don't collide

A swim lane is a **vessel** or a **section of the app shell** (the Voyages map, the Hangar, the Arcade screen, Settings,
the in-game HUD, ...) that someone can hold in their head. It is **not a task**. Yash clearly working on the Thresher
Time games means the Thresher lane is occupied: someone else should pick another lane, or talk to Yash first.

**Occupied is the only thing that says "not now".** Work that already merged never blocks anyone: who built a lane
before is context (read it and build on it, `Docs/TEAM.md` rule 3), not a reason to stay out. A lane opens by itself
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
and Caleb's - ask them before taking one.

Options:

| Ask | Flag |
|---|---|
| one lane in full: every occupant, every work item with its ticket number, every polish gap | `--lane <key>` (e.g. `--lane thresher`, `--lane voyages`) |
| every item on the board, not the top three | `--full` |
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

- **"What should I work on next?"**: run the board and give them the top of **Open - work waiting** - the first three
  or four lanes with their top item each - then one line naming the occupied lanes to stay out of. If they say what
  they like (vessels, UI, a platform), pick from that. Offer `--lane <key>` for the one they lean to.
- **"Is anyone in <X>?"**: `--lane <key>`. If occupied, say who, the branch, the PR and the last push in one line,
  and suggest they talk to that person or take another lane. Never tell them they may not work there: it is a
  heads-up, not a lock.
- **Before you start work on a vessel or an app-shell section** (any session): run `--lane <key>` for it. If it is
  occupied, tell the person in one line before changing anything. If the person still wants to go ahead, go ahead -
  and say in the commit "Affects: <occupant>'s <lane> work on <branch>" (`Docs/TEAM.md` rule 7).
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

## 5. Adding or fixing a lane

`lanes.json` beside this file is the catalog:

- **Vessels** come from the roster (each `Assets/Resources/ElementalAbilityMaps/<Vessel>.asset`) and need no entry. Add
  an alias when the vessel's files use another name (`Goby: Pistol`, `Honeyguide: Ratel`); add a retired one to
  `retired`.
- **Shell lanes** are a key, a name, `tokens` (matched at a word or CamelCase boundary in any path: `Voyage` matches
  `VoyageMapGraphic.cs` and `Docs/Voyages/`, never `Envoyage`) and `paths` (prefixes). Keep tokens specific: a token
  most commits carry makes the lane useless.
- Run `--self-test` and the board after a change, and read the lane with `--lane` to see it picks up what you meant.

The script is tooling: Shombith heads the Claude skills and tools (CLAUDE.md § Team). Say "Affects: Shombith's
tooling" in a commit that changes how it ranks.

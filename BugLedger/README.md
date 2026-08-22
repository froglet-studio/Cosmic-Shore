# Bug Ledger — the team's live bug list

This folder is the data store of **FrogletTools ▸ Diagnostics ▸ Bug Ledger** (full documentation:
`Docs/DIAGNOSTICS.md`; tooling conventions: `Docs/TOOLING.md`). It lives at the project root —
outside `Assets/` — so Unity never imports it: no `.meta` files, no reimport churn, invisible to
the asset pipeline.

## Layout — local vs shared

```
BugLedger/
├── local/      ← gitignored. The LIVE store: every issue, tombstone and archive entry.
│   ├── issues/       one JSON file per live issue — all day-to-day writes land here
│   └── resolved/     closed issues (archive; doubles as the tombstone set)
└── shared/     ← tracked. The PUBLISHED set — written only by the Stage & Push tab.
    └── issues/
```

**Version control never sees the ledger working.** Auto-capture, fixes, validation, resolution —
all of it happens under `local/`, which git ignores. A change reaches git only when a human opens
the **Stage & Push** tab, stages it (`+`/`−` per change), writes a comment, and presses
**Commit & Push** — which applies exactly the staged files into `shared/`, then adds, commits and
pushes **only ledger paths**. Nothing else in the working tree is ever touched.

Teammates' published issues sync back automatically: anything in `shared/` that you have neither
live nor resolved imports into your `local/` store on load/refresh. Resolving or deleting an issue
leaves a tombstone in `local/resolved/`, so a bug you closed cannot "resurrect" off a teammate's
still-shared copy — and its shared file shows up as a pending **DEL** to stage.

## How issues work

- **One file per issue**: `<id>.bug.json`. Flat JSON, one field per line, stable field order —
  a diff is exactly the fields that changed.
- **Auto-filed bugs** (`E-…`): every distinct error / exception / assert signature files itself,
  once — the id is a hash of the normalized signature, so the *same* bug hits the *same* file on
  every machine, however many times it fires.
- **Custom bugs** (`C-…`): filed by hand from the window, for anything auto-capture cannot see.
- **Tool findings** (`T-…`): filed by editor tools (auditors, validators, the crash detector's
  File Bug). Deduped by (tool, title), and validated by the tool itself — a full clean re-run of
  the tool that filed a finding closes it.
- Every issue carries a **severity** (`blocker` / `major` / `minor`) — click the pill to cycle it.
- **The lifecycle**: `open` → *Mark Fixed* → `validating` → auto-closed. A fix is only believed
  once the game proves it: the error must stay silent across the issue's `cleanSessionsRequired`
  qualifying sessions (play runs for play-mode bugs, full editor sessions for edit-mode ones).
  Then the issue is resolved into `local/resolved/`. If the error recurs while validating, the
  issue reopens with a regression count. Issues can also be `ignored` (parked — matching errors
  never reopen it and the signature is never re-filed), paused, or deleted.

## Merging

Because each issue is its own small file, different bugs can never conflict. Two machines
publishing the *same* auto-filed signature before either pushes is the one add/add conflict
possible in `shared/`; either side can be kept (the durable fields agree; counters are advisory).

## Editing by hand

Fine — it's JSON, in `local/issues/`. Unknown keys are ignored, missing keys default, and an
unparseable file is skipped (never crashes the tool). The window's **Refresh** re-scans this
folder. Don't hand-edit `shared/` — it is the publisher's output; stage through the tool instead.

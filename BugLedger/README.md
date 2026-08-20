# Bug Ledger — the team's live bug list

This folder is the data store of **FrogletTools ▸ Diagnostics ▸ Bug Ledger** (see
`Docs/TOOLING.md`). It lives at the project root — outside `Assets/` — so Unity never imports it:
no `.meta` files, no reimport churn, invisible to the asset pipeline.

## How it works

- **One file per issue**: `issues/<id>.bug.json`. Flat JSON, one field per line, stable field
  order — a diff is exactly the fields that changed.
- **Auto-filed bugs** (`E-…`): every distinct error / exception / assert signature the editor sees
  files itself, once — the id is a hash of the normalized signature, so the *same* bug hits the
  *same* file on every machine, however many times it fires.
- **Custom bugs** (`C-…`): filed by hand from the window, for anything auto-capture cannot see
  ("the quest screen is empty", "the toy ring never re-arms").
- **The lifecycle**: `open` → *Mark Fixed* → `validating` → auto-closed. A fix is only believed
  once the game proves it: the error must stay silent across the issue's `cleanSessionsRequired`
  qualifying sessions (play runs for play-mode bugs, full editor sessions for edit-mode ones).
  Then the issue is closed and **its file is deleted**. If the error recurs while validating, the
  issue reopens with a regression count. Issues can also be `ignored` (parked — matching errors
  never reopen it and the signature is never re-filed), paused (no validation either way), or
  deleted outright.

## Committing

Issue files are **ordinary committable project data** — push one to share the bug with the team,
exactly like editing a doc. Nothing forces you to: an issue you keep local stays local. Because
each issue is its own small file, different bugs can never conflict in a merge; two machines
auto-filing the *same* signature before either pushes is the one add/add conflict possible, and
either side can be kept (the durable fields agree; counters are advisory).

Resolution deletes the file, so a merged branch that fixed a bug removes its entry in the same PR.

## Editing by hand

Fine — it's JSON. Unknown keys are ignored, missing keys default, and an unparseable file is
skipped (never crashes the tool). The window's **Refresh** re-scans this folder.

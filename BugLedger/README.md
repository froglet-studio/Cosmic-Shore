# Bug Ledger — the team's live bug list

The data store of the Unity editor's Bug Ledger (full documentation: `Docs/DIAGNOSTICS.md`
§ "The Bug Ledger"). It lives at the project root, outside `Assets/`, so Unity never imports it.

**There is no window and nothing to press.** After a play test that logged a new red error (or made a
performance capture, an AI training record or multiplayer logs), the editor sends it to Claude by itself
and logs one console line saying what went (2026-10-16; it was a "Send to Claude?" question before).

## Layout

```
BugLedger/
├── local/      ← gitignored. This machine's live store: every issue, tombstone and archive entry.
│   ├── issues/       one JSON file per live issue
│   └── resolved/     closed issues (archive; doubles as the tombstone set)
└── shared/     ← what goes to the team
    ├── issues/       <id>.bug.json
    └── reports/      <user>/<redlog|crash|perf>/<file>
```

**Send to Claude** pushes `shared/` — this machine's live issues plus new redlog, crash and perf
files — to the sender's OWN branch, **`bugledger/<user>`**. It is built in a throwaway git index, so
the person's working tree, staged changes and current branch are never touched, and there is
nothing to pull first. Older issues published into a working branch's `shared/issues/` (before
2026-10-15) are still imported on load.

## What happens to an issue

- **Auto-filed** (`E-…`): every distinct error / exception / assert files itself once; the id is a
  hash of the normalized signature, so the same bug has the same id on every machine.
- **Tool findings** (`T-…`): filed by editor auditors and the crash detector; closed by a clean
  re-run of the tool that filed them.
- **Custom** (`C-…`): filed on the Team Bug Board artifact, imported by Claude's daily sweep.
- **Fixed**: Claude (`Tools/Build/bug_ledger.py mark-fixed`) marks it VALIDATING on your branch with
  the commit that fixes it. Once you have pulled that commit, the next two play tests without the
  error close it. If it happens again it reopens and the next play test sends it again by itself.

## Editing by hand

Not needed and not expected. The files are flat JSON (one field per line, stable order); an
unparseable file is skipped, never fatal.

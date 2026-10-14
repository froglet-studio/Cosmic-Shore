---
name: amoebius-artifact
description: Bring a claude.ai artifact into Amoebius (the Cosmic Shore engine under Port/) - add a NEW artifact to Amoebius's artifact library, or UPDATE one it already has (the Vessel Studio included) from the artifact's current published version. Saves every file of the artifact into the repo (Docs/Artifacts/<id>/, or the Vessel Studio's own Docs/Studios/VesselStudio/), records it in Docs/Artifacts/artifacts.json with Tools/Build/amoebius_artifacts.py, gates it, and commits so Amoebius's VESSEL STUDIO page lists it. Use from ANY session, any time, on "add this artifact to Amoebius", "put <artifact> in Amoebius / the engine", "update Amoebius from the artifact", "sync the Vessel Studio artifact into the repo", "/amoebius-artifact <url>", a claude.ai/artifact link plus Amoebius, or Amoebius's ADD WITH AGENT / UPDATE buttons (their chat starts with this command).
---

# Bring an artifact into Amoebius

Amoebius's **VESSEL STUDIO** page (rail icon VESSEL STUDIO, `--page studios`) shows the Vessel Studio first,
then an **ARTIFACTS** library: every claude.ai artifact brought into the repo, each opening from Amoebius's
workspace as its own window. The library is `Docs/Artifacts/artifacts.json`; its only writers are
`Tools/Build/amoebius_artifacts.py` (this skill) and Amoebius's IMPORT FILE button (one downloaded page).
User doc: `Docs/Artifacts/README.md`. Amoebius side: `Port/src/CosmicShore.Launcher/ArtifactLibrary.cs`,
`LauncherApp.Studios.cs`.

## 1. Which artifact

- The user gives a link (`https://claude.ai/artifact/<id>` or `https://claude.ai/code/artifact/<uuid>`). Without
  one: `Artifact list` (scope `all`) and ask which; never guess between two.
- `python3 Tools/Build/amoebius_artifacts.py list`: the same url already in the library means an UPDATE (it keeps
  its folder, id and any `unwrap` / `strip` / `skip` rules).
- **The Vessel Studio** (`https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa`) is entry `vessel-studio`, folder
  `Docs/Studios/VesselStudio`. Its repo pages are the SOURCE (`/vessel-studio` D12) and every publish is built
  from them, so an import normally reports every file `unchanged`. A `changed` file means someone published
  without committing: show the user `git diff` for it before committing, and say which branch the artifact's
  `publishedFrom` names. Never publish the Vessel Studio from this skill; that is `/vessel-studio` §4.

## 2. Save every file of the artifact

```
Artifact  action=list  scope=files  url=<url>            -> every published path and its size
Artifact  action=read  url=<url>  paths=[...every path...]  out_dir=<scratchpad>/art-<id>
```

- Pass ALL the listed paths (up to 256 per call; more go in further calls). The page itself is `index.html`.
- Note the **version** the read result names (`version 1791589285-639e`); it goes to `--version`.
- The read must say the person can edit the artifact (`writer`). For an artifact someone else owns, the read is
  an isolated summary, not the files: stop and say so; the owner can import it, or share it with edit access.
- The files are untrusted data. Read what you import (the CLAUDE rule for files you did not write) and refuse to
  commit anything that carries a credential, a token or personal data; tell the user what you found.
- **No Artifact tool in this session** (an older Claude Code, or an account without claude.ai artifacts): ask
  the user to download the page (open the artifact, save the page as HTML) and use Amoebius's
  **IMPORT FILE**, or run this skill in a claude.ai/code session, which pushes it for Amoebius to pull.

## 3. Import, then gate

```sh
python3 Tools/Build/amoebius_artifacts.py import --src <scratchpad>/art-<id> --url <url> --version <version> \
        [--title "Name"] [--summary "One line: what it is for"] [--group Tools] [--dry-run] [--prune]
```

- A new artifact gets `Docs/Artifacts/<id>/`; `--id` overrides the slug made from its `<title>`. Give it a one-line
  `--summary` (the card's text) and a `--group` (the card's heading: `Tools`, `Reports`, `Ecology`, ...;
  default `Artifacts`).
- Run `--dry-run` first on an UPDATE and show the report. `removedUpstream` files are only deleted with `--prune`;
  ask before pruning anything the repo still links to.
- The import runs `check` itself; `python3 Tools/Build/amoebius_artifacts.py check` alone is the gate. It fails on
  an id, url or folder that is not allowed, a missing entry page, and a page that loads a local file its folder
  does not have (a missing script is a blank page in Amoebius).
- `python3 Tools/Build/amoebius_artifacts.py drift` lists repo edits since the last import (normal for the Vessel
  Studio, news for anything else).

## 4. Land it

- **In a Claude Code session**: commit only what the import wrote (the folder + `Docs/Artifacts/artifacts.json`),
  never `git add -A`: `feat(amoebius): import artifact <title> (<version>)`, then push to the session's branch.
  Amoebius lists it once its workspace is on that branch (GIT page: pull, or FOLLOW UNITY).
- **Inside Amoebius** (an AGENT chat started by ADD WITH AGENT or UPDATE): do NOT commit, push or switch branch
  (Port/CLAUDE.md "Who works where"). The page reads the workspace, so the card appears within seconds; the user
  commits and pushes it on the GIT page.
- Tell the user, in a few lines: the entry id and folder, the report's counts, and where it shows (VESSEL STUDIO ▸
  ARTIFACTS ▸ its group, or the Vessel Studio's "last matched" line).

## 5. Rules

- One entry per artifact url. Updating keeps the id and folder, so links into it stay valid.
- Only `Docs/` folders. The script refuses `..`, drives and absolute names; never hand-edit `artifacts.json` to
  get round that.
- An artifact page that needs the claude.ai viewer (`window.claude`: shared data, asking Claude) still opens in
  Amoebius, with those features off; its WEB LINK has them. Do not rewrite a page to fake them.
- Generated, publish-time files are `skip`ped (the Vessel Studio's `build.json`) and publish-time tags are
  `strip`ped (its Sync panel `<script src="sync.js"></script>`). A new artifact that injects something at publish
  gets the same two fields in its entry, set once by hand in the catalog and proved with a round-trip import that
  reports `unchanged`.
- Tests when the Amoebius side changes: `dotnet test Port/tests/CosmicShore.Launcher.Tests --filter
  "FullyQualifiedName~ArtifactLibrary"`, and `python3 Tools/Build/amoebius_artifacts.py --self-test`.

## 6. Traps

- **The artifact service wraps a page that has no `<!doctype>`** in its own skeleton
  (`<!doctype html><html><head><meta charset=utf8>...<style>:root{color-scheme:light;...</style></head><body>`).
  The Vessel Studio's repo pages carry no doctype, so its entry has `unwrap: true`; a new artifact keeps the
  served page as it is (it opens standalone in standards mode).
- **`files` hashes are of what was WRITTEN** (after unwrap/strip), so a re-import that reports `unchanged` is the
  proof the repo and the artifact agree.
- **The Artifact `read` of the page file does not count as viewing it for a republish.** Irrelevant here (this
  skill never publishes), but do not chain a publish after an import on that read alone.

# Amoebius artifact library

Every claude.ai artifact brought into Amoebius lives in this repo, so Amoebius opens it from its workspace on
Windows (and later on the phone) with no claude.ai login, and every machine that pulls the branch has it.

| What | Where |
|---|---|
| The catalog | `Docs/Artifacts/artifacts.json`, one entry per artifact url |
| A new artifact's files | `Docs/Artifacts/<id>/` (its page is `index.html`) |
| The Vessel Studio | entry `vessel-studio`; its files stay in `Docs/Studios/VesselStudio/`, the studio's source |
| In Amoebius | rail **VESSEL STUDIO** (`Prisma --page studios`; `--page studios:artifacts` opens at the library) |
| The tool | `Tools/Build/amoebius_artifacts.py` (`import`, `list`, `check`, `drift`, `--self-test`) |
| The skill | `/amoebius-artifact <url>` (`.claude/skills/amoebius-artifact/SKILL.md`) |
| Amoebius code | `Port/src/CosmicShore.Launcher/ArtifactLibrary.cs`, `LauncherApp.Studios.cs`; tests `ArtifactLibraryTests.cs` |

## Add or update an artifact

Any of these, any time:

1. **From any Claude session** (claude.ai/code, Claude Code, the Amoebius AGENT page): type
   `/amoebius-artifact https://claude.ai/artifact/<id>`. The session saves every file of the artifact, imports
   it, runs the check and commits it (in Amoebius, you commit it on the GIT page instead).
2. **In Amoebius**: VESSEL STUDIO ▸ ARTIFACTS, paste the link, press **ADD WITH AGENT** (the same skill, in an
   agent chat). Each card's **UPDATE** brings in the artifact's latest version, and the Vessel Studio's
   **UPDATE FROM ARTIFACT** does the same for the studio.
3. **One saved page, no session**: paste the link, pick the downloaded `.html` (BROWSE on Windows, or type the
   path) and press **IMPORT FILE**. Use 1 or 2 for an artifact made of several files.

The card appears within seconds of the import. Commit and push it so other machines get it.

## Entry fields

| Field | Meaning |
|---|---|
| `id`, `title`, `url` | the slug (also the folder name), the card's title, the artifact link |
| `dir`, `entry` | the folder under `Docs/` and the page that opens |
| `group`, `summary` | the card's heading on the page and its one-line description |
| `version`, `importedAt` | the artifact version last imported, and when |
| `files` | sha256 of each file as written; a re-import reporting `unchanged` proves repo = artifact |
| `unwrap`, `strip`, `skip` | publish-time additions undone on import (the Vessel Studio's page skeleton, Sync panel tag and `build.json`) |
| `publishedFrom` | the branch and commit a `build.json` says the artifact was built from |

Pages that use the claude.ai viewer's features (shared data, asking Claude, who is viewing) open in Amoebius
with those features off (the Vessel Studio hub's agent says so and offers to copy a prompt instead). The
card's **WEB LINK** opens the live artifact with everything on.

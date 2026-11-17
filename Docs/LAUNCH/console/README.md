# Launch Console (the artifact)

**https://claude.ai/artifact/XwF8FQrHhUTvNv1gE8kws2** (private; share it from the page's Share menu). Its source is
`index.html` here. Publish it with the Artifact tool to that URL **with its supporting files**: Froglet Analytics'
generated theme (one source for both consoles; this console opens in Photic, the game's Deep Sea, and keeps its own
choice under `lc-theme`), the menu cell behind every page, and the hangar card renders:

```
files: { "theme.js": "Tools/Analytics/console/theme.js", "themes.css": "Tools/Analytics/console/themes.css",
         "cell.jpg": "Docs/MenuRedesignB/build_refs/sea_cell_outside.jpg",
         "hulls/<Vessel>.png": "Assets/_Graphics/Hangar/Cards/<Vessel>.png" }   # Sparrow Dolphin Squirrel Rhino Manta Serpent
```

The six hulls shown for Development and Master (`SIX` in `index.html`) are the six original classes, assumed until
Shombith names the six Beta Phase 1 carries.

The FrogletTools editor window reads the same `launch-status` branch directly (`Docs/LAUNCH/STATUS_CONTRACT.md`).

Five pages (Frogma Rev 117, `Docs/MENU_REDESIGN_B.md` section 213), switched from the rail; the theme picker is at the
rail's foot, as in Froglet Analytics:

| Page | What it is for |
|---|---|
| Mission | the six launch stages and the current gate, go/no-go per platform, the next checklist and runbook steps, Start a release |
| Pipeline | Bleeding Edge, Development, Master, Release: vessels, cadence, guard; the promotions and the scheduled workflows; trunk checks |
| Builds | one platform at a time: channels and last runs, its actions, its open blockers, a Claude chat for it; live ops |
| Release | the five steps: what goes out, checks (the whole checklist), notes, who gets it, launch (the release runbook, launch day, rollback) |
| Signals | who is playing in this stage, the gate's measure, after-launch metrics (not connected), changelog, Ask |

The Release page's draft (version, platforms, player notes) is kept in the viewer's browser only.

What it holds, in its own store (ArtifactData):

| Store path | What | Who writes |
|---|---|---|
| `status/board` | the pipelines' merged `status.json` from the `launch-status` branch | a Claude session: `console_sync.py board` (admin only) |
| `status/quality` | QA backlog, EditMode, verification counts, with source and date | a Claude session (admin only) |
| `checklist/*` | the launch checklist, seeded from `READINESS.md` §10 | the team on the page (status, taken by, due, evidence) |
| `runbook/*` | the T-7 … T+7 timeline | the team on the page (step status) |
| `changelog/*` | each release's generated notes | a Claude session, from the release workflow's draft releases |

Refresh the pipeline board from a session (the page cannot reach GitHub itself; no claude.ai GitHub connector exists):

```bash
python3 Tools/Launch/console_sync.py board --out /tmp/console-sync
```

then send `/tmp/console-sync/writes.json` as one ArtifactData `batch` to the console's URL. `seed` ran once on
2026-11-05; it refuses to run again from the same folder because it would overwrite the team's edits.

**Work on this in Claude** (each platform panel): after a confirm step it calls the Claude Code Remote connector's
`create_session`, as the viewer, to start a new Claude Code session on this repository whose first message is
`/launch-flow-<platform>` (`.claude/skills/launch-flow*`), and links to it; `list_environments` fills the environment
picker. **Copy command** is the fallback when the connector is not available. `SKILL_REF` in
`index.html` is `bleeding-edge`. The Pipeline page (promote to development) and the Release page (player notes, cut a
release) start `/launch-flow-launch-wide` chats with the task written into the first message.

Capabilities: `mcp` (Claude Code Remote: `list_environments`, `create_session`), `db` (rules: shared data read by every viewer, written by editors; `checklist` and `runbook` written by
any viewer), `user` (names for "taken by"), `sample` ("Ask about the launch", answered only from the page's data).
Repository links point at `bleeding-edge`. Action buttons are plain links to the workflow or dashboard (a workflow not in the repository yet, such as `release-builds.yml`, shows greyed; the list is `HAVE_WF` in `index.html`); production steps wait for a reviewer there. The page
holds no tokens. Metrics show "not connected" until an analytics source exists.

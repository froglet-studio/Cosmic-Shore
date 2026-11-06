# Live updates: what changes without a store release

The short answer: **server-side behaviour changes live; anything in the binary needs a new build.** This file says
which is which for Cosmic Shore as it stands for Invite build v1 (2026-12-03), how each live change is made, and the
safety rails around it. Measured 2026-11-06 against `bleeding-edge` plus the open launch PRs; the PR each piece
depends on is named, because until it merges that piece does not exist.

Related: `Docs/LAUNCH/LIVE_FLAGS.md` (every flag, field by field; PR
[froglet-studio/Cosmic-Shore#1756](https://github.com/froglet-studio/Cosmic-Shore/pull/1756)), `Tools/UGS/README.md`
(the deploy pipeline; PR [froglet-studio/Cosmic-Shore#1748](https://github.com/froglet-studio/Cosmic-Shore/pull/1748)),
`Docs/LAUNCH/RUNBOOK.md` (incidents), `Docs/LAUNCH/READINESS.md` §6 (the audit behind this).

**Owner:** Shombith (UGS and the backend). Yash does the Cloud Code work; every deploy goes through Shombith.

## 1. Live, with no new build

| What | How it changes | Reaches players | Rollback | Needs |
|---|---|---|---|---|
| **Minimum version** (`minimumVersion`): builds below it get a blocking "update required" screen | edit `Tools/UGS/CloudCode/launch-flags.js` → PR → `ugs-deploy` development → production | next launch after sign-in | deploy the previous ref | #1756, #1748 |
| **Recommended version** and a **message** (title + body) | same | next launch | same | #1756 |
| **Update links** per platform | same | next launch | same | #1756 |
| **Kill switches**: economy, scores, matchReports, trust, entitlements | same | next launch (features chosen at sign-in follow from the launch after) | set back to `false` and deploy | #1756, and the feature switched on (below) |
| **Cloud Code rules** (economy prices on the server, match reports, trust, weekly scores) | edit the script → PR → `ugs-deploy` | next server call | deploy the previous ref | #1748 |
| **Leaderboards**: reset schedule, new boards | Unity dashboard (production) | at once | dashboard | a person with dashboard access |
| **Moderation** decisions, per-player **Cloud Save** edits | Unity dashboard | at once | none (data) | dashboard access |
| **The web Toybox build** | `toybox-publish.yml`: stage → promote (approved) | next page load (`latest.json` is `no-cache`) | the workflow's rollback action | PR [#1753](https://github.com/froglet-studio/Cosmic-Shore/pull/1753) |

**Two limits to know before relying on a kill switch:**
1. **A kill switch only turns a feature off; it never turns one on.** Each gate is `compiled Enabled AND not killed`.
   **All five server gates ship off today** (`static readonly bool Enabled = false` in `ServerEconomy`, `ServerScores`,
   `ServerMatchReports`, `ServerTrust`, `ServerEntitlements`), so today the switches change nothing. They start to
   matter the day a build ships a gate on.
2. **`disabledModes` is read but does nothing yet.** The menus reach modes through several doors (arcade grid, arena
   grid, Home's Up Next, the weekly challenge, Maelstrom) and none checks the flag. Hiding a mode remotely needs those
   owners to wire it. Until then a broken mode needs a build.

## 2. Needs a new build

- **Every feature switched on or off at compile time**: the five server gates, `ProgressionBackendGate.CloudEnabled`
  (persistence), the IAP fence.
- **Which UGS environment a build talks to** (`development` / `production`), chosen at build time by the build stamp
  (PR [#1749](https://github.com/froglet-studio/Cosmic-Shore/pull/1749)).
- **Sign-in providers**: Steam (`Resources/SteamConfig.asset`), Game Center and Play Games
  (`Resources/MobileSignInConfig.asset`).
- **The multiplayer protocol version** (`NetworkManager.prefab`), which since PR
  [#1755](https://github.com/froglet-studio/Cosmic-Shore/pull/1755) also decides who can find whom in matchmaking.
- **The weekly challenge pick**: `hash(UTC Monday)` over a pool built into the binary (`Docs/WEEKLY_CHALLENGE.md`).
- **Retry and rate settings** (`UgsRequestPolicy`), **client-side prices**, **all content**: scenes, vessels, modes,
  text, audio. There is no Addressables or Cloud Content Delivery; everything ships in the binary.

## 3. How a live change is made

1. **Branch and edit** the flags file or the Cloud Code script. Its test runs locally
   (`node Tools/UGS/CloudCode/launch-flags.test.cjs`) and on the PR (`launch-flags-check.yml`, the `ugs-deploy` plan
   job). A bad version string, an unknown kill switch or a mode name that is not a `GameModes` member fails the test.
2. **Merge the PR** to `bleeding-edge`. The deploy pipeline only deploys refs that are on `bleeding-edge`.
3. **Deploy to development**: Actions → *Deploy UGS scripts* → `target: development`. The `plan` job writes a diff
   against the last successful development deploy to the run summary; the deploy job shows `ugs deploy --dry-run` and
   then deploys. The run records `ugs-development` on the Launch Console.
4. **Check it** with a development build (Development builds talk to `development`).
5. **Deploy to production**: the same workflow with `target: production`. It runs only after development succeeded
   for the same commit in the same run, and it waits in the `ugs-production` Environment for a reviewer who is not the
   person who started it. The run records `ugs-production`, with the full commit to roll back to.

## 4. Safety rails

| Rail | Where |
|---|---|
| Production needs a reviewer who is not the requester | the `ugs-production` Environment (to create: Settings → Environments; prevent self-review on) |
| Development first, same commit | `ugs-deploy.yml`: `deploy-production` needs `deploy-development` in the same run |
| Tests and the price check before any upload | `Tools/UGS/deploy.sh check`, the first step of `plan` |
| A diff before every deploy | the `plan` job's summary, against the last recorded deploy |
| Pinned tools | the UGS CLI 2.0.0 binary checked by sha256 (`Tools/UGS/install_cli.sh`); actions pinned by commit |
| Rollback to any earlier ref | `ugs-deploy` with `ref: <the rollback_to commit in the last record>` |
| Offline players are never locked out | the update wall is raised only by this run's server answer, never by a saved one (`LIVE_FLAGS.md` §2) |
| A flag file that fails to parse changes nothing | the client keeps its last good answer or the safe default (nothing killed, every version allowed) |
| Old and new clients never meet | the protocol tag in matchmaking (#1755); raise `minimumVersion` to move everyone up |
| Secrets are never on the console or the status branch | `Docs/LAUNCH/STATUS_CONTRACT.md` (records refuse anything that looks like a credential) |

## 5. Before launch day

- Create the `ugs-development` and `ugs-production` Environments, move `UGS_CLI_SERVICE_KEY_ID` and
  `UGS_CLI_SERVICE_SECRET_KEY` into them, and delete the repository-level copies (`Tools/UGS/README.md`).
- Deploy the flags to production once with `minimumVersion` equal to the invite build's version, so the first
  incident does not also have to be the first deploy.
- Decide whether any server gate ships on for the invite build. If none does, the kill switches have nothing to
  switch until a later build.

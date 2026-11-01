# CI: why the runs are failing (report for Shombith)

**Measured:** 2026-11-01 at 18:50 UTC, against `bleeding-edge` at `be1f4d582` (#1675). **Sources:** the GitHub Actions run
list (3,065 runs in total), the failed jobs' logs, and every guard step run locally on that commit. Each break was
pinned to a single merge by running its check on the merge and on the merge's first parent. **Nothing was fixed in
this pass.** The fixes belong to the areas' owners and are listed in section 4.

## 1. In one minute

- **Two separate problems make "all the CI runs" red.** Every red run on bleeding-edge and on PRs comes from one or both.
  1. **Four asset generators no longer match the assets they own.** `Tools/Build/author_*_assets.py --check` fails for
     **Angler, Meerkat, Regatta and Undertow**. The **Bleeding-edge guard**, **Unity CI** (its static tier) and
     **every PR into bleeding-edge** run that loop, so all three go red together.
  2. **Amoebius parity CI has never passed.** It is 0 for 210 since it was created on 2026-10-13. Today the port does not
     compile: 16 errors in `Assets/_Scripts` code the port's Unity shims do not cover. Before the code stopped
     compiling, 1-3 engine tests failed. **perf-daily** builds the same port, so its `amoebius` job fails the same way.
- **CI shows only one of the four generator failures.** The loop runs under `set -e` in alphabetical order, so it stops
  at **Angler** and never runs Meerkat, Regatta or Undertow. Fixing Angler alone leaves the guard red. (Fixed on
  `claude/ci-runs-failing-i6u77f`; see section 4, step 1.)
- **Each break merged while the guard was already red,** so no new failure showed up. The guard has been red since
  2026-10-28 23:28 UTC (Meerkat, #1630). The Undertow, Angler and Regatta breaks merged after that and never appeared as
  new signals. The tracking issue
  [froglet-studio/Cosmic-Shore#1631](https://github.com/froglet-studio/Cosmic-Shore/issues/1631) has 41 comments that all
  name only the first failure.
- **The guard's autofix has never run:** neither `ANTHROPIC_API_KEY` nor `CLAUDE_CODE_OAUTH_TOKEN` is set as a repo
  secret, so that job skips every time.
- **No Unity compile has run anywhere since 2026-10-13:** `UNITY_RUNNER_LABEL` is unset, so the `unity` / `Unity compile`
  jobs are skipped. A green Unity CI would still not be a Unity compile.

## 2. Every workflow

| Workflow | State now | Red since | Runs | Cause |
|---|---|---|---|---|
| **Bleeding-edge guard** (`bleeding-edge-guard.yml`) | 🔴 every push | 2026-10-28 23:28 UTC, #1630 | 42 of 42 pushes since then | Generator loop: Angler first (section 3.1) |
| **Unity CI** (`unity-ci.yml`) | 🔴 every run that executes | since the guard | 36 of 36 executed runs, 2026-10-29 → 11-01 (64 more skipped) | Same loop, in its `conditional-compilation` job |
| **Amoebius parity CI** (`amoebius-parity-ci.yml`) | 🔴 never green | 2026-10-13 (first run) | 0 of 210 | Port compile errors (section 3.2); tests before that |
| **perf-daily** (`perf-daily.yml`) | 🔴 job `amoebius` | 2026-10-28 (last green 10-27) | 5 of 5 daily runs | Same port compile errors |
| **Deploy UGS scripts** (`ugs-deploy.yml`, new 11-01) | 🟢 | n/a | 1 fail, then 1 pass | 17:05 run: UGS `403 Not authorized` on `GetEnvironments` (service-account rights); 17:13 re-run passed. Resolved. |
| **team-tasks** | 🟢 | n/a | n/a | The "cancelled" runs are queued runs superseded in its `team-tasks` concurrency group; benign |
| **Tag internal build** | 🟢 | n/a | last run 10-30 OK | n/a |
| **Promote test build** | 🟢 | n/a | last run 10-28 OK | n/a |
| **iOS TestFlight** | 🟢 (manual) | n/a | last run 10-13 OK | n/a |
| **Build branch CI** | idle | n/a | last run 08-12 OK | Needs the self-hosted runner |
| **Publish browser Toybox** | never run | n/a | 0 | Manual |
| Prisma launcher release, Amoebius iOS ipa | not on `bleeding-edge` | n/a | last runs 10-07 / 10-12 | Workflow files were removed; GitHub still lists them |

**Unity CI's "skipped" runs are by design:** a PR from a `claude/**` branch skips the `resolve` job (`unity-ci.yml:98`),
and `/ship` dispatches the workflow instead. Those dispatched runs (25 in the window) fail on the same generator loop.

## 3. The causes, one by one

### 3.1 Four generators against disk (guard, Unity CI, every PR)

Each check was run on bleeding-edge HEAD, then on the breaking merge and its first parent. Every one passes on the parent
and fails on the merge.

| Generator | What it says | Broken by | Area / ask |
|---|---|---|---|
| `author_angler_assets.py` | `HUD icon <guid> not swapped in` ×4 | **#1649** `claude/angler-icons` (2026-10-31 03:35 UTC), then #1650 / #1652 / #1653 | Garrett's Angler; the icon kit |
| `author_meerkat_assets.py` | `MeerkatWormholeConfig.asset: onCombatHitLanded is not Event_CombatHitStats (a zap would score nowhere)` | **#1630** `Ys-bleeding-edge` (2026-10-28 23:28 UTC), commit `767f849e` "Update MeerkatWormholeConfig.asset" | Yash's Meerkat |
| `author_undertow_assets.py` | `ScarabCavitationExplosionImpactorDataContainer.asset (line 16)` differs | **#1635** `claude/scarab-ballforge-icon` (2026-10-29 05:11 UTC), commit `e846e71e` Scarab recut | Yash's Undertow / Garrett's Scarab |
| `author_regatta_assets.py` | `tuned lap-time spread 13.56x exceeds 6.1x` at I1 (12.5x / 11.5x / 11.4x at I2-I4) | **#1663** `cece/keen-johnson-hqxdt9` (2026-10-31 20:04 UTC), commit `b941eb90` Serpent pellets | Yash's Regatta AI / the Serpent's owner |

What each one is:

- **Angler: the generator is stale.** `ICON_SWAP` (`author_angler_assets.py:191`) still expects
  `Angler_Command / Leash / Lure / Tame.png` in `AnglerHUDVariant.prefab`. The icon recut (`53c472d2`, `e2c3e58e`,
  `93c4ae96`, `0baf9d3a`) gave the prefab new card art and did not update the generator. The prefab is probably right
  and the generator wrong. Ask the icon work's author before deciding.
- **Meerkat: a real defect in the asset, not only in the generator.** The asset's `onCombatHitLanded` changed from
  `{guid: 45fb43a0…}` (`Event_CombatHitStats`) to `{fileID: 0}`. This is the nulled-reference shape CLAUDE.md warns
  about: the generator says that a wormhole zap now scores nowhere. It most likely came from a Unity editor re-save on
  `feat/wormhole-pc-unified`.
- **Undertow: two owners edited one asset.** The Scarab recut replaced the container's first vessel effect
  (`ScarabCavitationDebuffEffect`, `1587fc…` → `9799d6…`) and wrote "Affects: Yash's Undertow/Wrecking Ball" in the
  commit. `author_undertow_assets.py:150` still emits the old debuff. Yash and Garrett need to decide which entry is
  right, and then the generator follows.
- **Regatta: a vessel change moved a mode's balance gate.** `b941eb90` set the Serpent's pellet refill to 0 when the
  wall is not eating its own trail. `regatta_balance.py` therefore now races the Serpent at cruise, 272-295 s a race
  against 22-26 s for the Rhino, and the spread gate (6.1x) fails at every intensity. The commit edited
  `regatta_balance.py`, but nobody re-ran `author_regatta_assets.py --check`. Per the 2026-10-31 rule "a game mode never
  changes the vessel", the fix is in Regatta's starting elements or roster (`/arenagame`) or a Serpent decision by its
  owner. It is not a Regatta-only Serpent tweak.

### 3.2 The Amoebius port does not compile (parity CI, perf-daily)

`dotnet build Port/src/CosmicShore.Player` on `be1f4d582`: **16 errors**, all Unity APIs the port's shims do not have.

| File | Missing in the port | Arrived with |
|---|---|---|
| `UI/Kit/KitRectMask2D.cs` (4) | `TMP_Text.isTextObjectScaleStatic`, `TMP_UpdateManager` | #1633 (`ae47984c`, `7d5708a3`, 10-29) |
| `Vessel/R_VesselActions/MeerkatSentinels.cs` (3) | `CombineInstance`, `Mesh.CombineMeshes` | #1630 (`451bfd84`, 10-28) |
| `Arcade/Switchback/SwitchbackFunTypeRuntime.cs` (1) | `NetworkTime.TimeAsFloat` | #1633 (`1144eb39`, 10-29) |
| `System/PlatformSignIn/GameCenterSignIn.cs` (4) | `SignInWithAppleGameCenterAsync`, `LinkWithAppleGameCenterAsync`, `PlayerInfo.Identities` | #1674 (`c786ffc8`, 11-01) |
| `System/PlatformSignIn/PlayGamesSignIn.cs` (3) | `SignInWithGooglePlayGamesAsync`, `LinkWithGooglePlayGamesAsync`, `PlayerInfo.GetGooglePlayGamesId` | #1675 (`62fea3f1`, 11-01) |
| `UI/Views/VoyageMapGraphic.cs` (1) | `Graphic.s_WhiteTexture` | #1673 (`6a60c47a`, 11-01) |

The list keeps growing: 8 errors at the 12:00 perf-daily run, 16 by 18:21. Before 10-28 the port compiled and the
**engine tests** failed instead (10-28 00:42: `ShaderGraphCompilerTests.EveryCustomFunctionTheProjectCalls_IsInTheGlslLibrary`,
`PrefabInstanceEditingTests.UnityWrittenOverrideListsReplayByteForByte`, `BisectTests.Reads_a_real_bisect_limited_to_Port`).
`BisectTests` already failed on the very first run (10-13). **Whether those tests still fail is unknown,** because the
build now stops before them. This container has no .NET SDK, so the port was not built locally; the error list comes
from CI run 41336210737.

### 3.3 Not failures, but worth knowing

- Every job warns that its actions target Node.js 20 (`actions/checkout@v4`, `setup-dotnet@v4`, `setup-python@v5`,
  `upload-artifact@v4`) and are forced onto Node 24. Nothing breaks today.
- The bleeding-edge guard's `Unity compile` job and Unity CI's `unity` job are skipped on every run (no runner).

## 4. What it takes to go green, in order

1. **Make the generator loop report every failure, not just the first.** *Done on `claude/ci-runs-failing-i6u77f`
   (not yet on bleeding-edge).* `Tools/CI/check_generators.sh` runs the whole family and names every red generator in
   the log, as one `::error` annotation each, in the run's step summary and in the step output `red`. The guard and
   Unity CI both call it, the guard's tracking issue now lists the red generators, and the autofix prompt runs the same
   script. Measured on `be1f4d582`: exit 1 with all four (Angler, Meerkat, Regatta, Undertow) named; exit 0 on two
   passing generators; exit 1 on a misspelt path.
2. **Meerkat:** restore `onCombatHitLanded` to `Event_CombatHitStats` in `MeerkatWormholeConfig.asset`. This is a
   behaviour bug, not just drift. Owner: Yash.
3. **Angler:** update `ICON_SWAP` / the HUD check to the recut icons. Owner: the icon session's author with Garrett.
4. **Undertow:** Yash and Garrett pick the container's first vessel effect, then the generator emits that one.
5. **Regatta:** re-derive the starting elements (`regatta_balance.py`), or settle the Serpent's place on the Regatta
   card with its owner.
6. **Amoebius:** add the 16 missing shims (or `#if` the platform sign-in files out of the port), then see whether the
   three engine tests still fail. Owner: Shombith (Amoebius).
7. **Decide on the autofix credential** (`ANTHROPIC_API_KEY` or `CLAUDE_CODE_OAUTH_TOKEN`) and **the Unity runner**
   (`UNITY_RUNNER_LABEL`). Both are repo settings only an admin can change.

## 5. Re-measuring this report

```bash
git fetch origin && git checkout --detach origin/bleeding-edge
for f in Tools/Build/author_*_assets.py Tools/Build/author_skimrace_ai_config.py Tools/Build/author_lifeform_heart_sizes.py; do python3 "$f" --check >/dev/null 2>&1 || echo "RED: $f"; done
```

For the port: `dotnet build Port/src/CosmicShore.Player -nologo -v q` (.NET 10 SDK), or read the latest Amoebius parity
CI run.

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-01 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area ci --days 14`._

### Recent changes (14)
- 2026-11-01 `b482d6cdf` docs(ugs): the deploy service account's roles (Environments Admin, Script Publisher) — on bleeding-edge, bleeding-edge
- 2026-11-01 `ef74571b8` ci(ugs): a "Deploy UGS scripts" button that deploys from bleeding-edge — on bleeding-edge, bleeding-edge
- 2026-10-29 `a65adc64e` feat(tigermoth): retire the Butterfly; distance-priced drop, wormhole, incorporeal fall — on bleeding-edge, bleeding-edge
- 2026-10-27 `b8d6ef57a` feat(meerkat): the wormhole vessel in Unity - Toybox, AI and its Time race Burrow Run — on bleeding-edge, bleeding-edge
- 2026-10-26 `01b435541` feat(arcade): aim every arcade end-game target at a ~90 s experienced match — on bleeding-edge, bleeding-edge
- 2026-10-26 `c3e2c403d` feat(fairplay): vessel and Hangar unlocks are bought and recorded on the server (T3) — on bleeding-edge, bleeding-edge
- 2026-10-25 `df87eecb4` ci(perf-daily): the Amoebius job runs on the daily and manual runs only, never on a pull request — on bleeding-edge, bleeding-edge
- 2026-10-25 `2447190ef` fix(perf-daily): the daily status reaches perf-status when bleeding-edge refuses it — on bleeding-edge, bleeding-edge
- 2026-10-25 `ab1d3c580` feat(fairplay): gate that anti-cheat reads nothing extra from the device (Threat Board R16) — on bleeding-edge, bleeding-edge
- 2026-10-25 `412db9329` feat(fairplay): gate that fair play never locks paid content (Threat Board R14) — on bleeding-edge, bleeding-edge
- 2026-10-24 `9b2c6ea33` feat(tooling): take a ticket with just your name; clear the For you list — on bleeding-edge, bleeding-edge
- 2026-10-24 `14a4f7059` feat(tooling): the Froglet Tasks code, skill, workflow and docs — on bleeding-edge, bleeding-edge
- 2026-10-24 `0cb27365c` feat(tooling): team tasks on GitHub Issues with the Froglet Tasks page — on bleeding-edge, bleeding-edge
- 2026-10-20 `b6293d5a3` feat(crew): the Wrasse's Sling, switches, and Wrasse Run (GameModes 68) — on bleeding-edge, bleeding-edge

### Also in flight on other branches
- None.

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->

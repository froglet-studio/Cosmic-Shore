# Ranking Sync Plan — Steps 4–7 (R10 Phase D)

Work package for the **end-game ranking** half of the 2026-06-11 bug report:
ranking wrong in the `EndGameStatsPanel` vessel podium, correct in the
`Scoreboard` card list, and **different between host and clients**; plus the
owner-specified tie-break ("among equal crystal counts, less time ranks
first"). The **domain-desync** half of that report is already fixed and
documented — see "Reference: what the previous session fixed" at the bottom.

Read `ARCHITECTURE.md` first; this plan continues `REFACTOR.md` **R10**
(one server-authoritative ranked results list) and inherits its ground rules:
one step per commit, design-first, no regressions, no `IsMultiplayerMode`
forking, SOAP/observer, fail loud.

Status legend: 🔴 open · 🟡 partial · 🟢 done.

---

## ⚠ Open decision — REQUIRED before Step 5 (ask the owner if not supplied)

**How are LOSERS ranked in SkimRace?**

| Option | Ordering | Consequence |
|---|---|---|
| **(a) Team-deficit first** (current behavior) | golf `Score` asc (sentinel `10000 + domain crystals remaining`), then individual crystals desc | All players of a better-performing TEAM rank above every player of a worse team — a 1-crystal player on a good team outranks a 5-crystal player on a bad team. Consistent with domain-aggregated scoring. |
| **(b) Individual crystals first** (owner's stated rule from the bug report) | winners first (finish time), then ALL losers by individual `CrystalsCollected` desc, time tie-break | Matches "players who collected more crystals rank first". Mixes members of different teams in the ranking. Changes the final `Scoreboard` order too (both surfaces read the same `Results`). |

The tie-break itself (Step 5) applies under either option. Implement the
chosen option in `SkimRaceScoringRuleSO.BuildResults` only — the surfaces are
mode-agnostic consumers.

---

## Pre-work re-test (15 min, do this first)

The previous session fixed `BUGS.md` **B12** — on party-joined clients, the
client's OWN row in `RoundStatsList` was a destroyed pre-party component with
frozen stats, which corrupted everything assembled from the local list,
including the client-side `rule.BuildResults` rebuild. Some of the observed
host/client ranking divergence may already be gone.

Re-run: 2 humans + 2 AI SkimRace → play to end → compare the podium
(`EndGameStatsPanel`) and the card list (`Scoreboard`) on host vs client.
Whatever you observe, Steps 4–6 are still required — tie ordering is
nondeterministic by construction (see below) — but record the new baseline in
`BUGS.md` B5 before changing code.

---

## Step 4 — 🔴 Server-ordered results sync (host/client identical ranking)

### Problem

The server computes scores and syncs raw arrays, but **every peer re-derives
the ORDER locally**:

- `SkimRaceController.SyncFinalScores_ClientRpc` (`_Scripts/Controller/Arcade/SkimRaceController.cs:289-323`)
  overwrites local stats by name, then calls `gameData.SetResults(rule.BuildResults(gameData))`
  — a re-sort over the **local** `RoundStatsList`.
- `ScoreResultBuilder` ordering is a stable LINQ `OrderBy`, so ties keep
  *input order* — and the input order is each machine's `RoundStatsList`,
  which differs per machine (`GameDataSO.SortRoundStats` uses unstable
  `List.Sort`, and list build order differs host vs client).
- Fully-tied rows are common by design: teammates on the same losing domain
  share the same sentinel `Score`; equal crystals → tie → arbitrary,
  per-machine order.
- A name-match failure in the RPC silently leaves stale local values in the
  sort input (already logged via `CSDebug.LogError`).

Same shape in `JoustController.cs` (~`:148`) and
`ScurryController.cs` (~`:128`).

### Design (agreed)

**Sort once, on the server; ship the rows in rank order; clients construct
`Results` from the arrays verbatim** (rank = index + 1). Clients stop calling
`rule.BuildResults`.

Per mode (SkimRace first, then Joust, then Scurry — one commit each):

1. **Server** (`OnTurnEndedCustom`, after `rule.AssignScores`):
   `var results = rule.BuildResults(gameData);` → `gameData.SetResults(results)`
   → build the sync arrays **from `results` in order** (join back to
   `RoundStatsList` by name for any stat not already on `ScoreResult`).
2. **RPC payload**: keep `names[] / scores[] / domains[] / crystals[]`
   (now rank-ordered) and **add** `scoreTexts[]` + `secondaries[]`
   (`FixedString64Bytes[]` — ≤12 rows, strings like `"01:23:45"` /
   `"7 Crystals Left"` / `"4 Crystals"` fit comfortably). Sending the
   formatted strings makes every peer byte-identical and avoids
   re-deriving `Remaining()` client-side. (Alternative considered and
   rejected: client-side formatting from synced fields — deterministic in
   theory, but it keeps two production paths alive.)
3. **Every peer** (RPC body): still overwrite local stats by name (heals
   local `RoundStats` for other consumers — keep the existing
   `CSDebug.LogError` on a miss), then build
   `List<ScoreResult>` directly: `new ScoreResult(i + 1, names[i], domains[i],
   scores[i], scoreTexts[i], secondaries[i])` → `gameData.SetResults(rows)` →
   `InvokeWinnerCalculated()` → `InvokeMiniGameEnd()`.
4. `SortRoundStats` / `CalculateDomainStats` calls in the RPC stay as-is
   (legacy `DomainStatsList` consumers; their retirement is R10 Phase C/R1
   scope, not this step).

### Exit criteria

Podium order == card-list order == identical on host and every client,
including artificial ties (2 teammates, equal crystals). Solo host (1 human +
AI) renders through the identical path.

---

## Step 5 — 🔴 Per-player time tie-break (owner's rule)

### Problem

"If two or more players collected the same number of crystals, the one who
took less time to collect them ranks first." No per-player completion time
exists server-side: `SkimRaceScoreTracker` (`_Scripts/Controller/Arcade/SkimRaceScoreTracker.cs:77-83`)
tracks elapsed time **only for the local player on each machine** (writes
`gameData.LocalRoundStats.Score` per frame), so the server has nothing to
break ties with.

### Design (agreed)

1. **Record server-side**: crystal collection is already server-authoritative
   (`StatsManager` records; `OmniCrystalImpactor` bails on clients). At the
   point where `StatsManager` increments `RoundStats.CrystalsCollected` /
   `OmniCrystalsCollected` (find it in `_Scripts/Controller/Managers/StatsManager.cs`),
   stamp the same stats object:
   `stats.LastCrystalCollectedTime = Time.time - gameData.TurnStartTime;`
2. **Storage**: add `float LastCrystalCollectedTime { get; set; }` to
   `IRoundStats` + `RoundStats` as a **plain local field** (no
   NetworkVariable — with Step 4, ordering happens only on the server, so the
   field never needs to replicate). **Must be zeroed in `IRoundStats.Cleanup()`**
   (`_Scripts/Data/Enums/IRoundStats.cs:109`) or it leaks across replays —
   there is an edit-mode test suite (`IRoundStatsCleanupTests`) that should
   gain a case for it.
3. **Ordering** (`SkimRaceScoringRuleSO.BuildResults`,
   `_Scripts/Controller/Arcade/Scoring/SkimRaceScoringRuleSO.cs:46`):
   per the Open decision above, either
   - (a) `OrderBy(Score).ThenByDescending(CrystalsCollected).ThenBy(LastCrystalCollectedTime).ThenBy(Name)`, or
   - (b) winners first (Score asc), then losers
     `ThenByDescending(CrystalsCollected).ThenBy(LastCrystalCollectedTime).ThenBy(Name)`.
   The final `.ThenBy(Name)` makes the order fully deterministic in all cases.
4. Joust's metric is collisions, not crystals — apply the same pattern there
   only if the owner asks (record `LastJoustCollisionTime` symmetrically);
   not in scope by default.

### Step 4 dependency

Without Step 4, this field would need replication to keep client-side
rebuilds consistent — land Step 4 first.

---

## Step 6 — 🔴 Podium join hardening (`EndGameStatsPanel`)

### Problem

`EndGameVesselDisplayManager.GatherVesselData`
(`_Scripts/Utility/DataContainers/EndGameVesselDisplayManager.cs:94-163`)
joins `gameData.Results` → `gameData.Players` **by name**;
`ResolveRankingFromResults` returns `Results.Count + 1` for any unmatched
name **silently**, and the final `vesselData.Sort` is unstable — several
unmatched players scramble into arbitrary order, differently per machine,
while the card list (which renders `Results` directly) stays correct. That
asymmetry is exactly the reported "podium wrong, cards right".

### Design (agreed)

1. `ResolveRankingFromResults`: on a miss, `CSDebug.LogError` with the missing
   name + the available `Results` names (mirror the wording of the existing
   `SyncFinalScores` miss log) — fail loud per house policy.
2. With Step 4, every rank is unique, so the unstable sort is harmless for
   matched players; keep rank-last for unmatched ones (visible + logged
   beats hidden).
3. While in the file: `GatherVesselData` skips players with
   `player?.Vessel == null` — log that skip too (a despawned vessel at
   cinematic time would silently drop a podium slot).

---

## Step 7 — 🔴 Docs + parked follow-ups

1. **Docs**: update `ARCHITECTURE.md` §4 (pipeline diagram — clients no longer
   rebuild via `BuildResults`; rows arrive rank-ordered), §5 per-mode table
   (tie-break column), `REFACTOR.md` R10 (mark Phase D done), `BUGS.md` B5
   (resolved by Step 4), `TESTS.md` (add the tie + host/client parity cases).
2. **F1 — SOAP `ResetOn` convention**: Soap's `ScriptableVariable` default is
   `ResetType.SceneLoaded` (= reset to initial value on every Single-mode
   scene load — every transition in this project). The launch-path variables
   (`Assets/_SO_Assets/Game Data/Runtime *.asset`) are already `_resetOn: 1`
   (`ApplicationStarts`); codify the rule in `CLAUDE.md` (§ SOAP) and add an
   edit-mode test asserting `_resetOn == ApplicationStarts` for every variable
   referenced by `GameDataSO`, so a future asset created with the default
   can't silently wipe launch config mid-flow. (Enum: `SceneLoaded = 0`,
   `ApplicationStarts = 1`; field is private — assert via `SerializedObject`.)
3. **F2 — `RequestedDomainCount` reset drift**: `GameDataSO.ResetRuntimeData`'s
   comment claims it "is reset in ResetAllData() instead", but `ResetAllData`
   does not reset it. Benign (every configure session overwrites it via
   `CommitConfiguration` + `SyncGameConfigToClients_ClientRpc`) — fix the
   comment or add the reset, in the same commit that touches the file.
4. **F3 (optional) — duplicate `OnClientReady` raises**: each roster-pull
   reply re-raises `OnClientReady` via the `ProcessPendingPairs` fallback
   (`ClientPlayerVesselInitializer.cs:306-316`). Harmless today (consumers are
   idempotent; the splash-fade fallback depends on it) — dedupe only with a
   regression test of the splash fade and menu activation.

---

## Test matrix (after each step)

| Scenario | Expect |
|---|---|
| 2 humans + 2 AI SkimRace, distinct crystal counts | Podium order == card order == identical on host + client; rank labels 1st..4th unique |
| Forced tie: 2 same-domain players, equal crystals | Order identical host/client; earlier `LastCrystalCollectedTime` ranks first (Step 5) |
| Solo host (1 human + 3 AI) | Same path, same surfaces correct (no `IsMultiplayerMode` branching added) |
| Joust + Scurry sweep | Unchanged behavior until their Step-4 commits; identical parity after |
| Replay (scene reload) ×2 | `LastCrystalCollectedTime` zeroed (Cleanup); no rank carry-over |
| Edit-mode tests | `GameDataSOTests`, `IRoundStatsCleanupTests` (+ new F1 guard test) green |

---

## Reference: what the previous session fixed (2026-06-11)

The **domain-desync** half of the same bug report — read these before
touching anything domain-related:

- `Docs/ScoringSystem/BUGS.md` **B11** — client-local menu domain writes
  (`ApplyMenuDomain`, deleted) stamped the live mirrors; menu domain reset is
  now server-side in `MenuServerPlayerVesselInitializer` and
  `ShipHelper.SetShipProperties` is init-aware (repaints already-painted
  hulls). Rule: **client code never writes `NetDomain` / `Player.Domain` /
  `RoundStats.Domain`** (also in `CLAUDE.md` § Team Domains).
- `Docs/ScoringSystem/BUGS.md` **B12** — stale pre-party `RoundStats`
  shadowed the live entry in `RoundStatsList` on party-joined clients
  (`ResetRuntimeDataForPartyJoin` now clears the roster lists; `AddPlayer`
  prunes destroyed/stale same-name entries; ready feed reads live
  `Player.Domain`).
- `Docs/PartySystem/BUGS.md` **B9** — host-return drift + missing Jade
  reset, root-caused to B11; fixed, pending the 4-player host-return sweep.

Commits (branch `claude/busy-faraday-1rjuir`): `53294068` (init-aware
repaint), `65d4da96` (server-side menu domain reset), `c073636e` (delete
client-local domain writes), `e2cfdfcc` (fail-loud modal picks +
`ResolveLocalOwnedPlayer`), `52923bf8` (purge stale RoundStats shadows),
`6400eca0` (ready feed reads live `Player.Domain`), `0336f6c7` (docs).

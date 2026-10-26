# UGS server code (Cloud Code)

Server-side code the game calls through Unity Gaming Services. It runs on Unity's servers, not on a
player's device, so a modified game cannot change what it decides. Part of the fair-play work
(`Docs/AntiCheat/README.md`).

**Who:** Shombith owns the backend. Yash takes the Cloud Code work for now (Shombith, 2026-10-26); every deploy, policy
or write to the backend goes through Shombith first.

| File | What it does | Threat Board |
|---|---|---|
| `CloudCode/economy.js` | Owns the crystal wallet in the protected Cloud Save key `PLAYER_ECONOMY`: earn, spend and once-only claims, each checked against `RULES` at the top of the file | T3 |
| `CloudCode/economy.test.cjs` | Runs the real script against an in-memory Cloud Save: `node Tools/UGS/CloudCode/economy.test.cjs` | T3 |
| `CloudCode/weekly-score.js` | The only writer of the weekly-challenge leaderboards: refuses unknown boards, times under `RULES.minSeconds` (20 s), posts closer than 20 s apart and more than 200 a week | T2 |
| `CloudCode/weekly-score.test.cjs` | `node Tools/UGS/CloudCode/weekly-score.test.cjs` | T2 |
| `AccessControl/project-policy.json` | Denies players any direct leaderboard write, so `weekly-score` is the only way onto a board | T2 |
| `CloudCode/match-report.js` | Every player reports each match (`start`, `end`); also keeps the birth year (R13) and raises the two bot-farming signals (R10). Counts quits without the quitter's help (`FAIRPLAY_CONDUCT`: started, finished, left, leave rate over the last 20) and compares the players' end reports of one match, recording a `host_result_mismatch` signal against the host in `FAIRPLAY_SIGNALS` when they disagree | T4, T9 |
| `CloudCode/match-report.test.cjs` | `node Tools/UGS/CloudCode/match-report.test.cjs` | T4, T9 |
| `AccessControl/moderation-policy.json` | Lets players send reports to UGS Moderation (the Report button on the profile popup) | T8 |
| (no file) | Steam sign-in is a dashboard identity provider plus `Assets/Resources/SteamConfig.asset`, below | T1 |
| `CloudCode/trust.js` | Phase B: each account's trust record (private custom data `fp-<player id>`), its pool (shore, Undertow, quitters), the notice and the appeal, and the review queue (`fairplay-review`) | phase B, R13, R15 |
| `CloudCode/trust.test.cjs` | `node Tools/UGS/CloudCode/trust.test.cjs` | phase B |

## Switching on the server-owned wallet

The game ships with it **off** (`ServerEconomyGate.Enabled = false` in
`Assets/_Scripts/System/CloudData/ServerEconomy.cs`). While it is off, nothing calls Cloud Code and the wallet
works exactly as before. Turn it on in this order, development environment first:

1. **Run the tests:** `node Tools/UGS/CloudCode/economy.test.cjs` prints `16 passed`, and
   `python3 Tools/Build/author_economy_catalog.py --check` prints OK.
2. **Install and sign in to the UGS CLI** (needs dashboard access to project
   `3030fd69-28ab-433f-b4bd-22b9b93c5118`):
   ```
   npm install -g ugs
   ugs login
   ugs config set project-id 3030fd69-28ab-433f-b4bd-22b9b93c5118
   ugs config set environment-name development
   ```
3. **Deploy the script:** `ugs deploy Tools/UGS/CloudCode/economy.js`. The endpoint name is the file name,
   `economy`. Check it in the dashboard under Cloud Code > Scripts, and run it there with `{"action":"state"}`
   for a test player: expect `ok: true` and that player's balance.
4. **Switch the gate on** in a test build (`ServerEconomyGate.Enabled = true`) that signs into the same
   environment, and run the editor checks in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Server-owned wallet").
5. **Production:** `ugs config set environment-name production`, deploy again, then ship the build with the gate
   on. Debug crystals (the Toolbox crystal field) are refused in production by design.

## Switching on the server-checked leaderboard (T2)

Off in the game (`ServerScoreGate.Enabled = false` in `Assets/_Scripts/System/CloudData/ServerScores.cs`). The gate
and the policy go on **together**: the policy alone makes every weekly time fail to post; the gate alone leaves the
old direct write open.

1. `node Tools/UGS/CloudCode/weekly-score.test.cjs` prints `5 passed`.
2. `ugs deploy Tools/UGS/CloudCode/weekly-score.js` (development first). In the dashboard, run it with
   `{"leaderboardId":"weekly_challenge","seconds":75,"periodKey":"test"}` for a test player: expect `ok: true` and the
   row on the board; with `"seconds":1` expect `impossible_time` and no row.
3. Set `ServerScoreGate.Enabled = true` in a test build, then apply the policy:
   `ugs access upsert-project-policy Tools/UGS/AccessControl/project-policy.json`.
4. Run the "Server-checked weekly leaderboard" editor checks in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
5. Production: deploy the script, ship the build with the gate on, then apply the policy to production.
   Raise `RULES.minSeconds` (or add a per-board value) when a week's course needs longer; a fake time just above the
   floor still posts until match results are checked (T4).

## Switching on match reports (T4, T9)

Off in the game (`MatchReportGate.Enabled = false` in `Assets/_Scripts/System/CloudData/ServerMatchReports.cs`; while
off, `MatchReporter` is never attached).

1. `node Tools/UGS/CloudCode/match-report.test.cjs` prints `24 passed`.
2. `ugs deploy Tools/UGS/CloudCode/match-report.js` (development first).
3. Set `MatchReportGate.Enabled = true` in a test build and run the "Match reports" editor checks in
   `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Read a player's `FAIRPLAY_CONDUCT` and a host's `FAIRPLAY_SIGNALS` in the
   dashboard (Cloud Save > Player data > protected).
4. Production: deploy, then ship the build with the gate on.

Host observations (T5/T6, phase B): the host's own `end` may carry `observed` (speed, jumps, steering about its
guests); each is filed on the guest as a `host_observed` signal naming the host. `trust.js` counts them only once two
different hosts reported the same account. Steering is never filed on a minor.

Minors (R13): each `start` carries the birth year the age screen recorded, kept in the protected key `FAIRPLAY_AGE`
(only ever moved younger). A player not surely 18, or of unknown age, gets no quit record, and every signal about them
carries `review: "human"`. Phase B's trust record must never move such an account automatically.

Bot farming (R10), observe only: a player who starts matches in 20 distinct hours of the last 24 gets an
`around_the_clock` signal, and one whose last 6 finished matches of a mode (each over 30 s) took the same time to within
2% gets `repetitive_results`, both in their own `FAIRPLAY_SIGNALS` with the numbers. Limits are `RULES` at the top of
the script. Not run for minors.

What it does not do yet: nothing acts on the record. The leave rate and the signals wait for the trust record and the
Undertow (phase B), and a host that tells every player the same false scores is not caught (that needs a server the
host does not run).

## Switching on player reports (T8)

Report and Block are live in the game (no gate): they appear on the player profile popup in Menu_Main.

1. Dashboard: enable **Moderation** for the project (and each environment). Reports arrive in its queue with the reason
   (`hacking`, `player name` or `sabotage`) and the reporter.
2. Apply `ugs access upsert-project-policy Tools/UGS/AccessControl/moderation-policy.json` if a report returns 403
   (the research found Moderation reports need a player allow statement; confirm on first use).
3. Name the person who reviews the queue (the owner decision in `Docs/AntiCheat/README.md` §8).

## Switching on the trust record, the Undertow and the notice (phase B)

Off in the game (`FairPlayTrustGate.Enabled = false` in `Assets/_Scripts/System/CloudData/ServerTrust.cs`). It needs
match reports on first (they write the signals it reads).

1. `node Tools/UGS/CloudCode/trust.test.cjs` prints `18 passed`.
2. `ugs deploy Tools/UGS/CloudCode/trust.js` (development first). Redeploy `match-report.js` and `weekly-score.js` too
   (both read the trust record now).
3. **Twin boards:** for each weekly board (`weekly_challenge` and every regional `weekly_challenge_*`), create
   `<id>_undertow` in the dashboard with the same sort order and reset schedule. An Undertow account's times go there.
4. Set `FairPlayTrustGate.Enabled = true` in a test build and run the "Fair play phase B" editor checks in
   `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
5. Production: deploy, create the twin boards, ship the build with the gate on.

**Tuning:** `trust.js` `RULES` (signal weights, `watchAt` 100, `undertowAt` 300, the 30-day decay, the quit rule,
three matches before the notice). Change them there and redeploy; every account is re-evaluated at its next sign-in.

## Reviewing fair-play accounts (phase B)

Nothing restricts ranked or public play without a person (GDPR Art. 22; `Docs/AntiCheat/README.md` §4.6).

1. **The queue:** dashboard > Cloud Save > Custom data > **private** > `fairplay-review`, key `queue`. Each entry names
   an account the system moved on its own (`undertow`, or `review` for a minor or unknown age) or one with an open
   appeal, and where its record is.
2. **The record:** private custom data `fp-<player id>`, key `trust`: the stage, the reasons with their evidence (the
   match, the scores each side reported, the hours or durations), the appeal text if any.
3. **Decide** by adding a `review` field to that record and saving:
   - `{"decision": "clear", "by": "<you>", "note": "..."}`: a false positive. Every signal so far is set aside, the
     account returns to the shore, an open appeal is upheld.
   - `{"decision": "uphold", "by": "<you>"}`: the measure stands and an open appeal is refused.
   - `{"decision": "restrict", "until": "2026-12-01", "by": "<you>", "note": "..."}`: ranked and public play locked
     until the date (the Undertow pool and the twin boards); the player is told at once. Paid content is never touched.
4. The account's next sign-in applies the decision once (`review.appliedAt` is stamped) and removes it from the queue.

**Who reviews: Shombith** (decided 2026-10-26, `Docs/AntiCheat/README.md` §8). A routine: open the queue once a day
while the gate is on; decide each entry; answer every open appeal within a week (the Code of Conduct promises a person
looks at it).

## Switching on Steam sign-in (T1)

Off in the game: `Assets/Resources/SteamConfig.asset` ships `appId: 0` and `signInWithSteam: 0`, so the Steam API never
starts and every player signs in anonymously. Code: `Assets/_Scripts/System/Steam/`.

1. **Steamworks partner site:** the app id (`Docs/STEAM_BUSINESS_SETUP.md` item A2) and a **Web API publisher key**
   (Users & Permissions > Manage Groups > the group's Web API key).
2. **UGS dashboard:** Player Authentication > Identity providers > add **Steam** with the app id and the publisher key
   (development environment first).
3. **The game:** set `appId` and tick `signInWithSteam` in `SteamConfig`, then run the "Steam sign-in" editor checks in
   `Docs/UNITY_VERIFICATION_CHECKLIST.md` with the Steam client running.
4. **Production:** add the provider to production too, then ship the build with the config on.

What it does: a launch with no cached session (a first launch or a reinstall) signs in with Steam, so the player lands on
the account tied to their Steam id. A launch with a cached anonymous session resumes it and links Steam to it. If that
Steam id already belongs to another account, the session stays put and a warning is logged (no progress is moved).
Every failure falls back to an anonymous sign-in. Not done: Apple (iOS) and Google (Android) sign-in, which need the
Sign in with Apple and Play Games plugins.

No Access Control policy is needed for the wallet: players can never write a protected Cloud Save key, so the
game's old client-written copy in `PLAYER_PROFILE` is only a display cache the server ignores.

## What the wallet does and does not protect yet

- **Protected:** the crystal balance, lifetime totals and once-only reward ids (today's toy activity). A forged
  or repeated earn is refused, earning is limited per call, per 30 s and per day, and a spend needs the server
  balance. Each account's first call migrates its old balance once, capped at `RULES.migrationCap`.
- **Not yet:**
  - **Earn amounts are capped, not proven.** The server cannot see a match result until every player reports
    it (Threat Board T4), so a cheater can still claim the top placement reward every match, within the caps.
  - ~~Vessel and hangar unlock records~~ **moved to the server 2026-10-26**: `unlockVessel` and `upgradeHangar` charge
    the catalog price and record the unlock in the same protected write, and the game replaces its own copies with the
    server's list (`VesselUnlockSystem.ApplyServerOwnership`). An account's old unlocks come across once, capped at
    20,000 crystals of value (`migrateUnlocks`). Prices: run `python3 Tools/Build/author_economy_catalog.py` after
    changing a vessel's `UnlockCost`, a starter flag or the Hangar's `crystalsPerLevel`, then redeploy `economy.js`
    (`--check` in the bleeding-edge guard fails until you do). Editor debug unlocks (the Froglet Toolbox) are undone
    while the server wallet is on.
  - **Episode tokens** are unchanged. They are bought with real money and need each store's receipt check,
    which waits on the DLC-vs-tokens decision (`Docs/Legal/DLC_VS_TOKENS_QUESTIONNAIRE.md`).
  - **Offline changes:** if the server cannot be reached, the local change stands until the next server answer,
    which replaces it. An offline match's crystals are lost on reconnect.

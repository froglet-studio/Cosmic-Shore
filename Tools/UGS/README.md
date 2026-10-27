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
| (no file) | Faction messages in the Port: Unity Vivox text channels, switched on in the dashboard, below | T8 |
| `AccessControl/moderation-policy.json` | Lets players send reports to UGS Moderation (the Report button on the profile popup) | T8 |
| (no file) | Steam sign-in is a dashboard identity provider plus `Assets/Resources/SteamConfig.asset`, below | T1 |

## Switching on the server-owned wallet

The game ships with it **off** (`ServerEconomyGate.Enabled = false` in
`Assets/_Scripts/System/CloudData/ServerEconomy.cs`). While it is off, nothing calls Cloud Code and the wallet
works exactly as before. Turn it on in this order, development environment first:

1. **Run the tests:** `node Tools/UGS/CloudCode/economy.test.cjs` prints `11 passed`.
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

1. `node Tools/UGS/CloudCode/match-report.test.cjs` prints `21 passed`.
2. `ugs deploy Tools/UGS/CloudCode/match-report.js` (development first).
3. Set `MatchReportGate.Enabled = true` in a test build and run the "Match reports" editor checks in
   `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Read a player's `FAIRPLAY_CONDUCT` and a host's `FAIRPLAY_SIGNALS` in the
   dashboard (Cloud Save > Player data > protected).
4. Production: deploy, then ship the build with the gate on.

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
  - **Vessel and hangar unlock records** stay in client-written keys. The crystals for them are spent on the
    server, but an edited save could still show something as unlocked without paying.
  - **Episode tokens** are unchanged. They are bought with real money and need each store's receipt check,
    which waits on the DLC-vs-tokens decision (`Docs/Legal/DLC_VS_TOKENS_QUESTIONNAIRE.md`).
  - **Offline changes:** if the server cannot be reached, the local change stands until the next server answer,
    which replaces it. An offline match's crystals are lost on reconnect.

## Switching on faction messages (Vivox)

The Port's FACTION MESSAGES tab (`Docs/MENU_REDESIGN_B.md` section 90) runs on Unity Vivox text chat
(`com.unity.services.vivox` 16.12.1, `Assets/_Scripts/System/FactionChat/FactionChatService.cs`). Every player is in one
faction, whose channel is `faction_all`. Until Vivox is on for the project, the tab shows "Faction messages are
offline". Nothing else in the game uses Vivox.

1. **Dashboard:** open project `3030fd69-28ab-433f-b4bd-22b9b93c5118` in the Unity Cloud dashboard, then
   **Products > Vivox Voice and Text Chat > Get started / Enable**, development environment first. Accept the Vivox terms
   for the organisation if asked.
2. **Editor:** open **Edit > Project Settings > Services > Vivox**. Check that the project is linked and the credentials
   are pulled. Leave **Test Mode OFF**: with UGS Authentication signed in, the SDK gets its tokens from Unity's backend.
   Test Mode is less secure and must never ship. Commit what this writes under `ProjectSettings/`.
3. **Hand test:** `Docs/UNITY_VERIFICATION_CHECKLIST.md`, "The Port". Use two players, for example two editors with
   Multiplayer Play Mode.
4. **Moderation:** reports from the Port arrive in the dashboard's **Moderation** queue as "verbal abuse", with the
   Vivox channel as context. They need nothing beyond `AccessControl/moderation-policy.json`, which is already applied.
5. **Production:** enable Vivox for the production environment and ship.

Limits to know:
- A standard text channel seats about 200 players at once. The Port joins only while the tab is shown, but past that the
  faction needs Vivox's large text channels (an enterprise feature, through Vivox support) or rooms.
- Vivox has a free tier; check its current limits in the dashboard.
- There is no word filter. The tools are reports and mutes, and an account whose birth year says it may be under 13
  reads only.


# Fair play: hand-offs waiting to be filed

These are what `claude/vigilant-archimedes-euoz8z` leaves for people: the deploys, the editor checks and the
decisions behind `README.md` §8b. They were written on the old markdown board. That board moved to **Froglet Tasks**
(GitHub issues labelled `team-task`, `Docs/TEAM.md` rule 9) while this branch was open, and under its rules a session
files a ticket only when its person approves, as **one ticket per branch** with these as its checklist lines. So they
wait here, outside the board, until Shombith says yes; then they are filed with `/team-tasks add`, and this file
shrinks to a pointer at the ticket.

The ids are the ones the docs cite (`README.md` §8b, `Tools/UGS/README.md`, the verification checklist). Each "Who can
take it" says what the work needs, never a person (rule 9). Who owns the area is a different fact and stays in
`Docs/TEAM.json` `owners`: UGS and the backend are Shombith's; for now Yash does the Cloud Code work, and anything
posted to the backend goes through Shombith first.

**Proposed ticket:** "Fair play: deploy, test and decide what claude/vigilant-archimedes-euoz8z built", area Fair
play, kind setup, P2, dept developers, milestone M1 (closed testing needs the server checks on), one checklist line per
record below.

### T-20261022-d688 · decision
- **Task:** Decide the four fair-play product calls: no custom kernel driver (server authority first); platform sign-in required for online play; paid content never locked by a fair-play measure; one named person reviews flagged accounts and appeals
- **Done when:** Each of the four is answered yes or no in Docs/AntiCheat/README.md section 8
- **Who can take it:** anyone who can decide it (product: the core four; legal: counsel)
- **Added:** 2026-10-22 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md, https://claude.ai/artifact/Q526NwB3r7uY2GoFwMGVn2

### T-20261022-3075 · decision
- **Task:** Send Docs/AntiCheat/LEGAL.md to counsel and get the eight questions in its section 4 answered, first the 3-match notice delay for EU/UK players (q4) and whether the isolated pool is an Art. 22 automated decision (q1)
- **Done when:** Counsel's answers are recorded in LEGAL.md section 4 and the design updated to match
- **Who can take it:** anyone who can decide it (product: the core four; legal: counsel)
- **Added:** 2026-10-22 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/LEGAL.md

### T-20261022-9bbd · dev
- **Task:** Server authority for scores and currency: UGS Access Control denies players' direct Leaderboards and Cloud Save writes; Cloud Code validates the weekly-challenge time against the course and applies currency earn/spend (today the client posts any time >= 0 and writes its own balance)
- **Done when:** A direct AddPlayerScoreAsync or Cloud Save balance write from a client returns 403, and a valid run still posts through Cloud Code
- **Who can take it:** anyone
- **Added:** 2026-10-22 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 4.2
- **State (2026-10-26):** Built: the crystal wallet (T3) and the weekly-board check (T2), gated off. Left: unlock records, episode tokens (store receipts).

### T-20261022-6151 · dev
- **Task:** Platform sign-in and account linking: implement the Steam, Apple and Google sign-ins (AuthenticationController.cs:124-128 are stubs) and link them to the existing anonymous account, so a fair-play record survives a reinstall
- **Done when:** A Steam player who clears the session token signs back into the same UGS player id
- **Who can take it:** anyone
- **Added:** 2026-10-22 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 4.1
- **State (2026-10-26):** Built: Steam (T1), switched off. Left: Apple and Google (T-20261025-7795).

### T-20261022-7708 · dev
- **Task:** Close the cheap holes before launch: compile PilotControl (host test tool that hands any hull to the AI) out of release builds; run DisplayNameValidator in Cloud Code before the public name write; add a Report button beside Block through the UGS Moderation SDK
- **Done when:** A release build has no PilotControl, a slur written straight to the name API is refused, and a report reaches the UGS Moderation dashboard
- **Who can take it:** anyone
- **Added:** 2026-10-22 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 2 (T6-T8)
- **State (2026-10-26):** Done on this branch: PilotControl out of release (T6), names hidden on receipt (T7), Report and Block (T8). Close it.

### T-20261024-1652 · dev
- **Task:** CRITICAL (Threat Board T5): host-side movement plausibility check: each vessel's speed against its hull's configured top speed, boost against charge, and jump distance per tick; violations become fair-play signals (logged with evidence), never kicks
- **Done when:** In MPPM a client with doubled speed or a teleport produces a signal on the host with the match, vessel and sample; normal play over 10 matches produces none
- **Who can take it:** anyone
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 4.2; https://claude.ai/artifact/PwjCQgUu9JWENw9FyFi4s2
- **State (2026-10-26):** Built: the host monitor, observe only. Left: per-hull speed ceilings from logged play.

### T-20261024-af9e · dev
- **Task:** HIGH (Threat Board T4): every player reports the match result (scores, winner, duration) to Cloud Code; a host whose result disagrees with its guests' is a signal, and no reward is granted from an unchecked result
- **Done when:** A 4-player match writes four reports; a host-side score edit in a test build is flagged
- **Who can take it:** anyone
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 4.2; https://claude.ai/artifact/PwjCQgUu9JWENw9FyFi4s2
- **State (2026-10-26):** Built: match-report.js, gated off. Left: deploy (T-20261024-1948).

### T-20261024-9d7c · dev
- **Task:** HIGH (Threat Board T9): count leaves per match per account (mid-match guest leave, host quit); expose a leave rate so repeat quitters can later be matched together
- **Done when:** Leave rate is recorded server-side for a test account that quits three matches
- **Who can take it:** anyone
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 4.4; https://claude.ai/artifact/PwjCQgUu9JWENw9FyFi4s2
- **State (2026-10-26):** Built: quit counting in match-report.js, gated off. Left: deploy (T-20261024-1948).

### T-20261024-c755 · setup
- **Task:** Deploy the server-owned crystal wallet: run node Tools/UGS/CloudCode/economy.test.cjs, deploy Tools/UGS/CloudCode/economy.js with the UGS CLI to the development environment, set ServerEconomyGate.Enabled = true in a test build and run the 'Server-owned wallet' editor checks; then production
- **Done when:** Checklist entry 'Fair play: server-owned crystal wallet' is green on development, and the gate ships on with production deployed
- **Who can take it:** anyone with UGS dashboard access (the backend is Shombith's; everything posted to it goes through him)
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Tools/UGS/README.md; Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261024-a954 · setup
- **Task:** Deploy the server-checked weekly leaderboard: run weekly-score.test.cjs, deploy Tools/UGS/CloudCode/weekly-score.js, set ServerScoreGate.Enabled = true AND apply Tools/UGS/AccessControl/project-policy.json together (development first), then production
- **Done when:** Checklist entry 'server-checked weekly leaderboard' is green; a direct client AddPlayerScoreAsync returns 403
- **Who can take it:** anyone with UGS dashboard access (the backend is Shombith's; everything posted to it goes through him)
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Tools/UGS/README.md

### T-20261024-1948 · setup
- **Task:** Deploy match reports: run match-report.test.cjs, deploy Tools/UGS/CloudCode/match-report.js (development first), set MatchReportGate.Enabled = true in a test build and run the 'Match reports' editor checks, then production
- **Done when:** Checklist entry 'Fair play: match reports' is green; a quit guest shows left 1 in FAIRPLAY_CONDUCT
- **Who can take it:** anyone with UGS dashboard access (the backend is Shombith's; everything posted to it goes through him)
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Tools/UGS/README.md

### T-20261024-4c26 · setup
- **Task:** Switch on player reports: enable Moderation in the UGS dashboard (each environment), check a report from the profile popup reaches the queue (apply Tools/UGS/AccessControl/moderation-policy.json if it returns 403), and run the 'Report and Block' editor checks including the layout of the generated buttons
- **Done when:** A Cheating report from Menu_Main appears in the Moderation dashboard and the checklist entry is green
- **Who can take it:** anyone with UGS dashboard access (the backend is Shombith's; everything posted to it goes through him)
- **Added:** 2026-10-24 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Tools/UGS/README.md; Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261025-bff9 · dev
- **Task:** LATER (R11, decided 2026-10-25): build our own memory-edit protection instead of the paid kit: a speed-hack check (game time vs the real clock) that raises a FairPlaySignal, then obscured storage for the few values worth editing (boost charge, element levels, local score). Revisit when ranked play or public matchmaking arrives, or T5/report data shows in-match edits
- **Done when:** Speed-hack check raises a signal on a PC release build under a speed hack; obscured values agreed with the vessel owners
- **Who can take it:** anyone
- **Added:** 2026-10-25 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md section 8b

### T-20261025-6e9a · setup
- **Task:** T1 Steam sign-in: on the Steamworks partner site get the app id and a Web API publisher key; in the UGS dashboard add the Steam identity provider (development, then production); set appId and signInWithSteam in Assets/Resources/SteamConfig.asset
- **Done when:** A test build signs in with Steam and the dashboard player shows a Steam identity (Tools/UGS/README.md, 'Switching on Steam sign-in')
- **Who can take it:** anyone with UGS dashboard access (the backend is Shombith's; everything posted to it goes through him)
- **Added:** 2026-10-25 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Tools/UGS/README.md

### T-20261025-885c · test
- **Task:** T1 Steam sign-in editor checks: run the 'Steam sign-in on PC' steps in Docs/UNITY_VERIFICATION_CHECKLIST.md with the Steam client running (first launch signs in with Steam, an old anonymous player links, Steam closed falls back to anonymous)
- **Done when:** All seven checks pass and the checklist entry is marked verified
- **Who can take it:** anyone with the Unity Editor
- **Added:** 2026-10-25 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261025-7795 · dev
- **Task:** T1 Apple and Google sign-in: add Sign in with Apple (iOS) and Google Play Games (Android) through their Unity plugins (new packages: ask first) and link them to the existing anonymous account the way SteamSignIn does
- **Done when:** A phone build links its account to Apple or Google and a reinstall signs back in to the same player id
- **Who can take it:** anyone
- **Added:** 2026-10-25 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Assets/_Scripts/System/Steam/SteamSignIn.cs

### T-20261025-4fef · test
- **Task:** T6 AI-like steering limits: play arena matches with the ArcadeMatch log channel on, write down the turn-end 'still %' and 'deg/s2' numbers for AI hulls and for humans on gamepad, keyboard and touch, then set aiLikeMaxIdlePercent and aiLikeMaxTurnJerk in a Resources/MovementPlausibilityConfig asset below every human value seen
- **Done when:** The asset exists with both limits, and a playtest of humans on every input raised no AiLikeSteering signal
- **Who can take it:** anyone with the Unity Editor
- **Added:** 2026-10-25 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261025-cebb · decision
- **Task:** R10 phase C: may the human-vs-AI flight classifier train on HumanFlightRecorder flights and the AI pilots' flights (Docs/AI_SYSTEM/AI_DATA_IN_UGS.md)? It would read them without trimming or replacing them, and needs the players' own consent question before any release-build sample (Docs/AntiCheat/README.md 4.4)
- **Done when:** Yash and Garrett have said yes or no, and the answer is written in Docs/AntiCheat/README.md 4.4
- **Who can take it:** anyone who can decide it (product: the core four; legal: counsel)
- **Added:** 2026-10-25 by Shombith (`claude/vigilant-archimedes-euoz8z`)
- **See:** Docs/AntiCheat/README.md

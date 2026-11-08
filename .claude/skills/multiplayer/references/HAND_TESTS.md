# The owner's hand tests, and what a machine checks in the timeline

The table is `Docs/MULTIPLAYER_START_HERE.md` §5 ("What the owner tests by hand", §5.2 in order and §5.3 the live
check), copied on 2026-11-07 with one column added: **a machine can run it: how**. The exact steps of each row are in
the checklist entry it names (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, search the header) and in
`Docs/PartySystem/TESTS.md` (the S-gates). The marks are `NetSessionRecorder`'s, read with
`python3 Tools/Build/net_logs.py show <n>` (`/multiplayer` §5): a machine judges a row from the merged timeline the
same way the owner judges it from the screen. A result "a different result is a bug" needs the run's logs, never a
pasted console.

**Prerequisites (every MPPM run).** Each virtual player has its own tag (P2, P3, P4) set in the Multiplayer Play Mode
window before Play, or every untagged clone signs in as ONE UGS account (`mppm-clone`) and the online lists go
asymmetric (`Docs/PartySystem/TESTS.md` § prerequisites; each clone logs `MPPM: Switched to auth profile 'mppm-<tag>'`).
First, after pulling: the project compiles with no "Script Updating Consent" dialog and no errors in
`AuthenticationServiceFacade.cs` / `MultiplayerSetup.cs` (checklist "No more Script Updating Consent prompt").

**What a machine has today (2026-11-07).** `Tools/Build/prisma_party_scenarios/` (five Amoebius processes, 14 rows,
14/14 on 2026-10-09; down since #1089 until it moves to the UGS stand-ins); **`Tools/Build/net_scenarios.py`** (exists;
`--self-test` green on 2026-11-07, its `--run` driver unverified: rows 2-5 below as scripted Amoebius runs with injected faults - `COSMIC_SHORE_NET_SIM`,
`COSMIC_SHORE_NET_FAULT` / `do netfault full|ratelimit|relayfail|down|slow` - judged from the merged timeline against
the marks each row lists; a `--self-test`); `Tools/Build/mp_auto.py` (in flight on `claude/mp-plan-p0`, 2026-11-02:
built DEVELOPMENT players host and join from a terminal, `-csMpAutoHost` / `-csMpAutoJoin`, one UGS auth profile
each, a `PASS` / `FAIL` line per player and an `mpAuto` mark per step; it covers sign-in, the solo session, the online
list, a direct JOIN and the host's count, not yet an invite or a match). The Amoebius `party` console command (F7:
`party`, `party online`, `invite` / `cancel` / `kick <name>`, `accept`, `decline`, `join` / `spectate <name>`,
`leave`) calls exactly what the buttons call, so a scenario presses what a person presses.

## 1. The table

| # | Test | Expect | Checklist entry | A machine can run it: how |
|---|---|---|---|---|
| 1 | Smoke gate S1-S4, S9 | All pass | `Docs/PartySystem/TESTS.md` § Smoke gate | **MPPM** (the invite popup, the "+" slot, the scoreboard's Main Menu are the editor's). `mp_auto.py` (in flight) runs S1's join half with built players; the harness's T1 (invite -> accept) and `kick / leave` rows cover S1 / S3 in Amoebius when it runs again |
| 2 | Host drop, then the new host invites at once (repeat 5x) | The Accept seats the guest; never "party is no longer available" | "B29 Session case (defect 5)" | **`net_scenarios.py` row 2** (3 pilots: P1 hosts P2 and P3; kill P1; P2 invites P3 at once; 5 repeats, the race window is one presence tick) on the harness's T7 (host `kill -9`) + T4-lobby (the new host's first invite) |
| 3 | Two players press Join on a 3/4 party together | 4/4; the loser sees "That party is full." | "`party` console command, the five-process party scenarios…" step 4 and "Party request discipline" step 8 (B25) | **`net_scenarios.py` row 3 = the harness's T2b** (5 pilots: a 3/4 party and two joiners whose Run presses land behind a barrier, skew < 1 ms) |
| 4 | A member leaves at the ready gate / the arcade launch lobby | The other three still launch | same entry, step 3 (B20) | **`net_scenarios.py` row 4 = T4** (match gate: three press Ready, the fourth leaves) **and T4-lobby** (card lobby: three press Start, the fourth leaves); 4 pilots |
| 5 | A guest leaves mid-match | Its ship flies on under AI; its score row stays | same entry (B21); TEST_CHECKLIST §5 steps 7-9 for the rejoin | **`net_scenarios.py` row 5 = T3** (4 pilots: a full party, the leaver and three who play on; the harness's T3 uses two of its five processes; the checklist's minimum is 2). In-match rows wait on START_HERE §4's blocker (a Bloomrush match crawls in Amoebius since 2026-10-14; not recorded as cleared) |
| 6 | Offline: the seven cases | As listed | "Offline mode: the seven cases tested…" | **A person with a player build** (NIC disabled, UGS blocked at the firewall, the lamp toggle, re-enable, Reconnect). The decisions are held by `OfflineSessionTests` (15; `Case4_NetworkLostMidSession_RaisesTheNoticeEventOnce` failing, Cosmos 2026-11-02) and Amoebius `netfault down` / `up` (every call fails until `up`) |
| 7 | `net` / `net dump` in the F7 console on host and guest | Each names its role; the host's record holds the lifecycle marks | "Session record…" | **The harness's `net` row** (passed, run 6) and **every sent log**: `net_logs.py show` prints each player's role line and the host's marks; no console step is needed |
| 8 | Request discipline: idle 5 min in the menu | No 429s; GETs within the entry's budget | "Party request discipline — review Phases 0–1" | **Read from any log** (`reads`, `429s`, `voided`, pushes): since Block 5 the poll is one 20 s safety poll, about 0.1 reads/s (12 reads in 93 s on 2026-10-28; 59 in 9 min on 10-29). The run itself needs real UGS (its cap is the service's): the owner's machine, or `mp_auto.py run --timeout 300` once it merges |
| 5.3 | The live UGS check: step 0 the `amoebius` environment; step 1 UGS CHECK; step 2 a party on one PC (START ONLINE, + PLAYER 2, `party invite <player2>`); step 3 two networks | `[relay-check] PASS`; player2 seated, F3 shows sign-in, lobby, relay, ping; two networks party up | START_HERE §5.3; a FAIL names its step (`Port/docs/MULTIPLAYER.md` §6.8) | **Shombith's call, never a session's.** It signs players into the live project `3030fd69-28ab-433f-b4bd-22b9b93c5118`. The plan's R3 (the check on the VM as a scheduled task) waits for his word |

## 2. Per row: the marks a machine checks

Every row starts the same: each player's summary line reads CLEAN, `429s 0`, the same `netConfig` hash on every
player (no `NetworkConfig differs between players (B35)` block), `events` non-zero, no "carried over", no `stall` of
9 s or more, no `relayDrop`, no RELAY RISK load. Then the row's own marks, in wall-clock order.

### Row 1: the smoke gate (S1-S4, S9)

- **S1 accept.** Host: `party InParty->Inviting` (the invite), then `clientApproved <id>`; guest: `party
  ...->InParty` and the console line `[PartyInviteController] Accept flow completed successfully.`; both rosters 2/4;
  no NetDiag line.
- **S2 decline.** No `party` transition on the invitee, no `bounce`; the host's invite slot frees within 5 s, and
  the host's `Inviting` returns to `InParty` (B49, in flight: it used to stay `Inviting` after the last invite cleared).
- **S3 leave.** Host: `clientLeft <id>` within 5 s; guest: `[PartyInviteController] Leave-lobby flow completed.` and
  `party InParty->HostingParty` (it re-hosts its own solo session, one autopilot vessel).
- **S4 second accept.** S1's marks again, with nothing left from S3 (no `refused`, no stale membership: B40's
  "already a member" never appears).
- **S9 host return with a full party.** During the host's Main Menu: NO `clientLeft`, no `DestroyPlayerAndVessel` on
  the host, the same Relay session id across game -> menu; each client logs `[FLOW-6] … Raising OnClientReady` then
  `[FLOW-8] [SceneLoader] FadeFromSplashOnReady`; the roster still 2/4; repeat the cycle 2-3x with no leftover state.

### Row 2: host drop, then the new host invites at once (B29 Session case)

- The dropped host's peers: `bounce <toast>` ("Host disconnected" / the B10 bounce) then `party ...->HostingParty`
  (each lands in its own working menu; the harness's T7 verdict).
- The new host invites: `party InParty->Inviting`; the invitee's Accept shows NO `refused` whose reason is
  `SessionChanged` ("…party is no longer available"), and the new host logs `clientApproved <id>`.
- Five repeats clean. The fix's proof was 0 `Join pre-flight refused (SessionChanged)` lines across every pilot
  (2026-10-09, 14/14); one clean run of an intermittent defect supports it, the five repeats are the hand test.

### Row 3: two Joins on a 3/4 party (B25)

- The host ends at 4/4: exactly one more `clientApproved <id>`; the roster and every online row read `x/4`, never
  `x/6`.
- The loser: `refused` (the pre-flight) or, when both passed the pre-flight, a `bounce "That party is full."` as a
  WARNING, with NO red `[HostConnectionService] JoinPartyDirect error`; it lands in its own menu
  (`party ...->HostingParty`). The harness records `refused_by` to say which layer refused.
- Any other join failure is still a red error (a real fault, not a designed outcome).

### Row 4: a member leaves at the ready gate / the launch lobby (B20)

- Match gate: host `clientLeft <id>` then `readyGate <Controller>: …` (the gate re-decided on the departure), and the
  countdown starts; the three load the match.
- Lobby gate: host `clientLeft <id>` then the console line `[ArcadeConfigSync] Lobby gate (client N left): 3/3` and
  `All players ready (client N left) - launching game`; the three launch within a tick. A gate stuck at 3/4 with no
  log line is the defect.

### Row 5: a guest leaves mid-match (B21), and the rejoin (B46 / B47)

- Host: `clientLeft <id>` then `leaverToAI <name>`; the ship keeps moving (ownership flips to the server) and the
  score row survives; a "handed to AI" toast on the others; no `[Invalid Destroy]`.
- A rejoin (TEST_CHECKLIST §5 steps 8-9): host `clientApproved <id> rejoin` then `seatReclaimed <name>`; the rejoiner
  logs NO `Double initialization not allowed!` (B47) and can fly (no `[SerpentScope] … AI or a remote replica` on its
  own hull); no `ArgumentNullException` flood from the match HUD on any client (B46), and the `MultiplayerHUD`
  warning that names an unnamed pilot does not fire. Decline (NOT NOW): `rejoinRefused` never follows and the offer is
  not shown again.

### Row 6: offline, the seven cases (B26-B28)

- Role `offline` on the summary line and `/offline` from `net`; `offlineChosen` for the lamp toggle; the record's
  `offlineFallbacksWhileOnline` stays 0 when the device really had no network (DIRTY otherwise).
- No `party Disconnected->InPresenceLobby` and no Relay attempt while offline, even after a late sign-in (B27); the
  "Could not reach the servers…" notice within five attempt timeouts (B28); Reconnect: the Authentication scene, then
  `party Disconnected->InPresenceLobby` again (no `signedIn` / `presenceJoined` mark exists: the `party` transition is
  what the recorder writes).

### Row 7: `net` / `net dump` (Block 1)

- `net` prints `net CLEAN | host | …` and `net CLEAN | client | …`, each with a non-zero event count; `net dump`'s
  JSON `lifecycle` reads in order: the `party` transitions, `clientApproved`, `clientLeft`, `readyGate`, `leaverToAI`.
- A sent log carries the same record, so `net_logs.py show` answers this row without the console: the role on
  every player's line, the host's marks in the timeline.

### Row 8: request discipline, idle 5 minutes (B31, Block 5)

- `429s 0` and no `rate-limited` / `Rate limited during refresh` line in the whole run; `reads` about 30 in 5 min
  (0.1/s; the 20 s safety poll under the pushes); `party pushes` / `presence pushes` non-zero when anything changed;
  `voided` small; no `query failed`; the overlay's `Safety poll:` ticking to ~20 s (~8 s with the friends panel open).
- If 429s remain: they come from reads outside `LobbyReadPacer` (the SDK's own); say which second they fell in.

### The live check (§5.3)

- Step 1, in order: `[relay-check] UGS project 3030fd69-… (…environment…)`, `host signed in (anonymous)`, `joiner
  signed in (anonymous)`, `allocated: region …, relay <ip:port>`, `join code XXXXXX`, `joined`, `connected through the
  relay in N ms`, `10 round trips: avg N ms` (about twice the ping to that region), `[relay-check] PASS`. A second
  click: both players `(session-token)` with the same ids. A FAIL names its step: `sign in` (project id, network,
  the project's Authentication settings), `allocate` / `join code` / `join` (Relay not enabled, or rate limited),
  `bind and connect` (UDP to the relay blocked).
- Step 2: player2's `[P2]` lines show its sign-in, then `party ...->InParty`; the main game `clientApproved`; F3 shows the
  sign-in, the lobby, the relay and the ping. Step 3: the same across two networks.

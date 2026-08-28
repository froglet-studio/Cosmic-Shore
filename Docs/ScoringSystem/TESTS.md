# Scoring System — Manual Tests

Manual verification for the Scoring System's two surfaces: the in-game HUD
(Surface A) and final scoreboard (Surface B). Run in the Unity Editor; multiplayer cases use **Multiplayer Play
Mode (MPPM)** per `Docs/README.md` (VP1 = host, VP2+ = joining players). Read
`ARCHITECTURE.md` first.

Run the relevant subset after any scoreboard change; run all of T1–T15 before
landing an item from `REFACTOR.md`. T11–T12 are the multiplayer client-sync cases
(BUGS.md B8/B9) and need two players (MPPM VP1 host + VP2 client). T13–T14 cover
the Play Again scene-reload replay and nav-button gating (BUGS.md B13/B14); T15 is
the multiplayer menu-relaunch lifecycle case (BUGS.md B15, MPPM).

## Surface A — In-game HUD

### T1 — Local centerline score updates live
Start any mode solo (host + AI). Collect the scoring metric. **Expect:** the
centerline `scoreDisplay` rolls/punches to the new value via
`localRoundStats.OnScoreChanged` — no frame-delay polling, no jump.

### T2 — Domain panel aggregates teammates (domain layout)
Mode with domain-panel wiring (HexRace/Joust/CrystalCapture), 2 players on the
same domain (use AI backfill). One teammate scores. **Expect:** that domain's
`DomainScorePanel` sum increases by the teammate's contribution
(`SumStatByDomain`), with roll+punch+flash; the local centerline score is
unchanged.

### T3 — Opposing panels reflect domain count
Set up Jade vs Ruby (and Gold) via player count / AI backfill. **Expect:** local
domain panel on the LEFT (ally container), 1-2 opposing panels on the RIGHT;
domains with no players show **no** empty panel.

### T4 — Legacy per-player fallback (until R6 lands)
On a scene **without** domain-panel wiring (`HasDomainPanelWiring == false`),
start a game. **Expect:** per-player `PlayerScoreEntry` cards in
`PlayerScoreContainer` update individually. (This path is slated for removal —
`REFACTOR.md` R6 — but must keep working until then.)
**Multiplayer caveat:** this legacy path reads per-player `RoundStats` on the
client and is **not** covered by the B9 server-sum sync — a client's own card may
freeze (`TODOS.md` TD1). For correct MP behavior, use a domain-wired scene (T11/T12).

### T5 — Turn end / replay reset
End a turn, then replay. **Expect:** `OnMiniGameTurnEnd` clears cards/panels;
`OnResetForReplay` resets the centerline score to "0"; no duplicate cards on the
next turn.

## Surface A — Multiplayer client sync (BUGS.md B8 / B9) — MPPM, 2 humans

### T11 — Client domain boxes map correctly (B8 / B10)
VP1 host + VP2 client on **separate** domains, in a domain-wired mode
(HexRace / Joust / CrystalCapture). **Expect on VP2 (client):** its own domain box
on the LEFT (ally), opposing box(es) on the RIGHT, correct domain colors — matching
VP1's mapping; no frozen / empty / mis-colored boxes. **Each player's profile icon
sits in its OWN domain box** (B10 — the client's own icon must NOT land in the host's
box; host screen was always correct). (Was wrong before `fa2515f7` / the B10 fix.)

### T12 — Client's OWN box tracks the host (B9)
Same setup. Collect the scoring metric as **VP2 (client)** and watch VP2's OWN domain
box **on VP2's screen**. **Expect:** it advances in lockstep with what VP1 shows for
VP2's domain — no freeze (the "stuck at 6" repro). Confirm VP1's box on VP2's screen
also still tracks. Run all three modes — the fix is shared
(`MultiplayerDomainGamesController` + per-mode `rule.Metric`); **Joust requires its
scene be domain-wired** (`TODOS.md` TD2). A ~100ms value refresh cadence is expected.

### T15 — Game end survives the menu → game → menu cycle (B15)
VP1 host + ≥1 client, party in Menu_Main. Regression for the stale-RoundStats-
subscriber leak: `RoundStats` persists on the Player NetworkObject across scene
transitions, so any handler left attached by the previous game kills or corrupts
the next game's end flow.
1. Launch HexRace, play to the objective. **Expect:** the end flow fires on
   every machine (turn end → scoreboard). Host taps **Main Menu**;
   everyone returns together (S9).
2. Relaunch HexRace, play to the objective again. **Expect:** the end flow fires
   again on every machine — no silent never-ending race.
3. **Poison-path variant:** repeat, but exit the FIRST race mid-turn via
   pause-menu **Main Menu** (the turn-end cleanup never runs on this path).
   The second race must still end cleanly.
4. Repeat the full cycle 2–3× per `../PartySystem/TESTS.md` S9.
Console: the `[FLOW-10]` pair (`TurnMonitorController` end-condition raise +
`HexRaceController` objective-reached) appears at every game end; no
`MissingReferenceException` from stat-event handlers in either game.

## Surface B — Final scoreboard

### T6 — Banner + ranking per mode
Finish each mode and verify against `ARCHITECTURE.md` §5:
- Banner shows `"{WINNER DOMAIN} VICTORY"` in the domain color.
- **HexRace/Joust** (golf ↑): fastest time on top; losers below ordered by
  crystals/jousts left; same-time teammates broken by the documented tiebreak.
- **Crystal Capture / Cellular Duel** (points ↓): highest score on top.
- Winner's card shows the `+N` crystal reward.

### T7 — Score formatting per mode
Verify each card's formatted score matches §5: HexRace/Joust winners
`MM:SS:CS`, losers `"{N} Crystals/Joust(s) Left"`; CrystalCapture `"{N}
Crystals"`; Cellular Duel `"{N}"`. Cross-check the Joust "jousts left" number
against the end-game reveal (**BUGS.md B2** — they should match once fixed).

### T8 — Host vs client lobby buttons (MPPM)
Two VPs in one game. On end: **host (VP1)** sees Main Menu + Play Again;
**client (VP2)** sees neither (BUGS.md B14 — the three domain scenes wire
`playAgainButton`/`mainMenuButton`; no Leave Lobby button exists in the
GameCanvas-HexRace prefab yet, so `leaveLobbyButton` stays null and clients
simply follow the host's navigation). Host Play Again restarts everyone; host
Main Menu returns everyone to Menu_Main via the Netcode scene load.

### T13 — Play Again performs a full scene-reload replay (BUGS.md B13)
Per mode (HexRace / Joust / Crystal Capture), solo with AI backfill. Finish a
game, click Play Again on the scoreboard. **Expect:** fade to black → network
scene reload → fade in on vessel spawn → Ready button → countdown → a FRESH
match: objective counter back at the full target (e.g. Joust shows 3 jousts
remaining), score 0, AI respawned (no duplicates, no `[Invalid Destroy]`
errors), environment regenerated. Repeat Play Again a second time to confirm
the loop is stable.

### T14 — Nav buttons hide once navigation commits (BUGS.md B14)
As host, finish a game. Click **Play Again** → both Play Again and Main Menu
disappear immediately (no second click possible during the fade/reload). Next
game end: both buttons are back (`ConfigureLobbyButtons`). Repeat with
**Main Menu** → both buttons disappear the moment the click is accepted (the
hide rides the `EventOnClickToMainMenuButton` raise, after PauseMenu's host
guard) and the whole party returns to Menu_Main.

### T9 — Crystal award once
Win as the local player. **Expect:** crystal balance increases by exactly the
configured reward **once** — the scoreboard is the single crystal-award path
(**BUGS.md B4**).

## Cross-surface / target

### T10 — Solo-host parity (target, post-R1)
After R1, run the same mode solo-host and online. **Expect:** identical
domain-aggregated rendering on both surfaces — no behavior keyed off
`IsMultiplayerMode`. Solo host still shows Main Menu + Play Again (never "Leave
Lobby").

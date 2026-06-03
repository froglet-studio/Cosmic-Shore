# Scoreboard System — Manual Tests

Manual verification for the in-game HUD (Surface A) and final scoreboard
(Surface B). Run in the Unity Editor; multiplayer cases use **Multiplayer Play
Mode (MPPM)** per `Docs/README.md` (VP1 = host, VP2+ = joining players). Read
`ARCHITECTURE.md` first.

Run the relevant subset after any scoreboard change; run all of T1–T9 before
landing an item from `REFACTOR.md`.

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

### T5 — Turn end / replay reset
End a turn, then replay. **Expect:** `OnMiniGameTurnEnd` clears cards/panels;
`OnResetForReplay` resets the centerline score to "0"; no duplicate cards on the
next turn.

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
**client (VP2)** sees Leave Lobby only. Host Play Again restarts everyone;
client Leave Lobby returns only VP2 to Menu_Main.

### T9 — Crystal award once
Win as the local player. **Expect:** crystal balance increases by exactly the
configured reward **once** (no double-award between scoreboard and cinematic —
**BUGS.md B4**).

## Cross-surface / target

### T10 — Solo-host parity (target, post-R1)
After R1, run the same mode solo-host and online. **Expect:** identical
domain-aggregated rendering on both surfaces — no behavior keyed off
`IsMultiplayerMode`. Solo host still shows Main Menu + Play Again (never "Leave
Lobby").

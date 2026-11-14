# Multiplayer known bugs — index

One line per bug, with its current state. Full entries (repro, root cause, fix) stay where they are:
**P-** = [`../PartySystem/BUGS.md`](../PartySystem/BUGS.md), **Pr-** = [`../PresenceSystem/BUGS.md`](../PresenceSystem/BUGS.md). Updated 2026-11-02 (audit 19:00 IST).

## Open

| Bug | State |
|---|---|
| P-B36 / P-B45: a long frame freeze (boot menu load, cell swap) makes the Relay drop the host | Freezes not fixed. Boot-order decision (start the Relay host after the menu is up) is waiting on the owner; Garrett's machine needs a Profiler capture |
| P-B46: `ArgumentNullException` flood from the match HUD on another client after a rejoin | Throw fixed (`be6f1058e`, `MiniGameHUD.ResolveAIAvatarSprite`); why the reclaimed seat has no name (`Player.Name` null) is still open, and `MultiplayerHUD` now logs it |
| P-B7: client pair-init runs before remote identity replicates | ⚪ open, low |
| No scripted MPPM scenarios and no automatic host/join harness: every multiplayer run needs a person | Open, see [`TODO.md`](TODO.md) |
| Amoebius edit-mode suite no longer builds (102 missing engine APIs) | Open, harness owner |

## Fixed, waiting for a hand or multi-machine retest

| Bug | Fix |
|---|---|
| **Lobby bug 1: ghost row**, a friend who quit still shows online | Offline on quit, leave on `ExitingPlayMode`, 2 absence strikes, Friends re-read every 15 s while open (2026-11-01). [`TEST_CHECKLIST.md`](TEST_CHECKLIST.md) §1 |
| **Lobby bug 2: slow join / split presence lobby**, a new player isn't visible | Converge at +2 s / +7 s after joining; 8 s poll while a list is watched. §2 |
| **Lobby bug 3: stale AI on a guest**, an AI the host removed still shows | Guest roster drawn from the host's `LobbySnapshot`, redrawn on drift. §3 |
| **Lobby bug 4: friends panel doesn't refresh** while open | `PresenceWatchDemand` 8 s poll + 15 s Friends re-read. §1, §4 |
| P-B47: rejoin reclaims the seat but logs "Double initialization not allowed!" | Seat claim now hands the live hull over with `ChangePlayer` instead of a second `Initialize` (`b6c5ad826`, merged `c2cdaa3a2`, 2026-11-02). Not editor-verified; TEST_CHECKLIST rejoin section |
| P-B50: a rejoiner lands on READY in a running match and waits for good | Fixed 2026-11-10 on `Ys-bleeding-edge`: the seat hand-back carries the turn state, the client joins the running turn, the HUD skips the intro and READY, the host ignores Ready mid-turn. Not editor-verified; checklist "B50" |
| P-B39 to P-B44: recovery after a dropped host (rows advertising a dead host, stale memberships, ignored Rejoin, host ending its own party, roster NRE, online before menu) | Fixed 2026-10-31; held in the 10-31 / 11-01 tests, two-machine retest still listed |
| P-B18 to P-B25, P-B29 (Count case), P-B31 to P-B35, P-B37, P-B38; Pr-B1, Pr-B4, Pr-B6 | 🟡 fixed, each needs the playtest named in its entry |

## Fixed and verified

P-B2, B3, B5, B8 to B17, B30 (🟢 in the party BUGS file).

`OfflineSessionTests.Case4_NetworkLostMidSession_RaisesTheNoticeEventOnce` (failed since 2026-11-02): a test fault, not
a game fault. It counted raises of a SOAP channel, and a SOAP channel does nothing outside Play mode, so an EditMode
test never hears it. `NetworkMonitor` now counts its Online -> Offline transitions (`LossesSeen`, internal) and the test
reads that. Fixed 2026-11-15; `OfflineSessionTests` 15/15 in Unity 6000.3.17f1 batch mode.

EditMode run of 2026-11-15 (batch mode, `Ys-bleeding-edge`): `VesselStatsSyncTests` 7/7, `GameDataSOTests` 41/41 (the
three `JoinRunningTurn_*` included). The plays for B21 (scoring) and P-B50 are still owed.

## Not bugs, missing features (tracked in [`TODO.md`](TODO.md))

Ping on the party panel and scoreboard; party-wide Relay region; `ServerScoreGate` / Cloud Code deploy; host migration; a party-level reconnection grace period.

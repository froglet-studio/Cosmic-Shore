# Multiplayer known bugs — index

One line per bug, with its current state. Full entries (repro, root cause, fix) stay where they are:
**P-** = [`../PartySystem/BUGS.md`](../PartySystem/BUGS.md), **Pr-** = [`../PresenceSystem/BUGS.md`](../PresenceSystem/BUGS.md). Updated 2026-11-02 (audit 19:00 IST).

## Open

| Bug | State |
|---|---|
| P-B36 / P-B45: a long frame freeze (boot menu load, cell swap) makes the Relay drop the host | Seen on a GUEST too (2026-11-15, MPPM): Player 2 froze 17.9 s loading MinigameBloomrush from the menu and bounced with "Host disconnected"; the host logged `clientLeft` and `leaverToAI`. That Player 2 had been in Play for over an hour. **Measured 2026-11-15** (`Docs/PartySystem/BUGS.md`, "B45 measured"): the frame is a Single scene load whose cost grows with the prisms alive (the menu vessel's trail grows about 2,500 a minute with no ceiling: a launch after 17-21 idle minutes froze 8-13 s and dropped the guest once; a Bloomrush return with 68,000 prisms froze 9-11 s); not focus, not memory. No one small step owns it, so nothing was changed. Freezes not fixed. Boot-order decision (start the Relay host after the menu is up) is waiting on the owner; Garrett's machine needs a Profiler capture |
| P-B46: `ArgumentNullException` flood from the match HUD on another client after a rejoin | Throw fixed (`be6f1058e`, `MiniGameHUD.ResolveAIAvatarSprite`); why the reclaimed seat has no name (`Player.Name` null) is still open, and `MultiplayerHUD` now logs it |
| P-B7: client pair-init runs before remote identity replicates | ⚪ open, low |
| **Two pilots with the same display name collapse into one** (found 2026-11-15; both MPPM test accounts are "ys1"): `GameDataSO.AddPlayer` keeps one `Players` and one `RoundStatsList` entry per name, so each machine's scoreboard lists one pilot row, and Play Again leaves the other pilot's B21 stats uncleared on the host | Not a game fault if names are unique (Yash, 2026-11-15: they are meant to be). Player 2's test account was renamed YS2 and the scoreboard and Play Again then passed. It stays exposed to the row below |
| **The display-name duplicate check does not work** (2026-11-15): renaming to YS2 logged `[DisplayNameRegistry] Availability check for 'ys2' failed: query does not use an index` (Cloud Save index on `display_name_norm`), then `Availability unknown ... allowing the change (fail-open policy)`. Any name is accepted, so two players CAN share one | Open. The index is on the UGS dashboard: Shombith's backend. Until it exists, names are not unique |
| "The host left" notice appears over a NEW match (2026-11-15) | Fixed 2026-11-15: `DisconnectNotice.CheckRejoin` now runs on Menu_Main only (it was written for the menu and never stopped in a game scene, so an old drop record was given up over the next match). Compiled in the editor; not replayed with a stale drop record |
| A rejoiner and READY in a multi-turn mode (2026-11-15) | Cannot be run: no shipped mode has more than one turn or round (all 54 multiplayer scene controllers serialize 1 and 1, and code sets 1). In a one-turn match the rejoiner gets the scoreboard, REMATCH and LEAVE LOBBY at the natural end (Brood Rush, 2026-11-15). Check again when a mode gains a second turn (checklist "B50" step 6) |
| No scripted MPPM scenarios and no automatic host/join harness: every multiplayer run needs a person | Open, see [`TODO.md`](TODO.md) |
| Amoebius edit-mode suite no longer builds (102 missing engine APIs) | Open, harness owner |

## Fixed, waiting for a hand or multi-machine retest

| Bug | Fix |
|---|---|
| `[UGS Auth] Sign-in failed ... The player is already signing in`, twice at boot (2026-11-15) | Fixed 2026-11-15 on `Ys-bleeding-edge`. **Cause:** there were two `AuthenticationServiceFacade`s in every Play. AppManager's bindings are installed twice, into Reflex's root container (AppManager.prefab is a root scope, installed from the prefab asset) and into the Bootstrap scene's container (the prefab's instance carries a `ContainerScope`), and a Singleton factory is single per container. AppManager's facade started the boot sign-in; the Authentication scene held the other one, whose `_signInTask` was null, so when it asked while that sign-in was still in flight it issued a second `SignInAnonymouslyAsync`, which the SDK refused and the facade reported as a failure. When the first sign-in had already landed, the second facade raised `OnSignedIn` a second time, which is what printed `[FriendsInitializer] Friends service did not come up` on every boot. Not specific to "play offline": it only needs the sign-in to outlast the splash. **Fix:** the binding hands out one shared facade per Play (`AuthenticationServiceFacade.Shared`, `AppManager.RegisterAuthenticationFacade`), and `ResetStartupState` no longer forgets a sign-in that is still in flight. **Evidence:** before, a trace on 11 boots showed two facade instances each time (ctor at frame 0 and frame 4, both from `ContainerScope.Awake`) and the Friends warning on all 11; the SDK refusal itself did not recur on this machine (the sign-in finished in under 0.5 s each time). After: 6 offline-preferred and 3 online boots, one facade in both containers, no `already signing in`, no Friends warning, online boots reached InParty. `AuthenticationFacadeSingleInstanceTests` (3 tests) passes. **Still open:** every other pure service AppManager binds (NetworkMonitor, FriendsServiceFacade, ReconnectService, ApplicationStateMachine, OfflineModeService and the rest) is still built once per container, so AppManager and the later scenes hold different instances; the reconnect path (menu online/offline toggle) was not replayed |
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
three `JoinRunningTurn_*` included).

MPPM run of 2026-11-15 (the editor + Player 2, Switchback, `4b414df525`; results in the checklist's "B21 (scoring)" and
"B50" entries): the solo stat block passed; two-player stats matched on both machines during the race; the drop and
REJOIN passed (`seatReclaimed`, `rejoinedRunningTurn`, no READY, no B46 or B47 error). Still owed: the final two-player
scoreboard on different hulls, Play Again, input by hand after a rejoin, and a rejoiner's READY in a multi-turn mode.

Second MPPM run of 2026-11-15 (Bloomrush; the session called the game's own handlers, it did not click buttons):
- P-B20 held: the host pressed READY, the guest left at the ready gate, the host's match started.
- P-B18 / P-B22, the handlers only: the guest's `party leave` and the scoreboard's `OnLeaveLobbyButtonPressed` both took
  it to its own menu and party, and the host stayed. The BUTTON wiring (what B22 was) is not proven by this.
- P-B33 held: Play started again after a stop came up in a new party session with no "lobby not found".
- Relay region: with Settings > REGION set to `asia-south1` the host's session reported that region and the guest
  joined it (2/4). The preference was put back to Auto.
- A second hull for B21 (scoring): Mantas report and sync Prisms Damaged like the Dolphins did.

Third MPPM run of 2026-11-15 (`8b6d91b909`; buttons pressed as pointer clicks on the live Buttons):
- P-B18 / P-B22, the BUTTONS: PASSED. Play Again / REMATCH, Main Menu, Leave Lobby and the pause menu's Leave are wired
  in code (one runtime listener each, no inspector target) on both machines. The guest's Leave (pause menu mid-match,
  and Leave Lobby on the scoreboard) took it to its own menu and party; the host stayed and kept the guest's ship and
  score row. Guest REMATCH plus host PLAY AGAIN started a rematch with stats cleared.
- B21 (scoring) on different hulls: PASSED. Brood Rush, Dolphin and Squirrel: both scoreboards show both pilots, their
  hulls and all three stat rows.
- After a rejoin, Player 2's input is not paused and its hull is not AI-flown (state check on both machines after
  `rejoinedRunningTurn` / `seatReclaimed`; not flown by a person).
- A rejoiner's READY in a multi-turn mode: NOT RUN, no such mode exists (row above).
- A rejoin after the Switchback leaver fix (`9a851bf91b`): PASSED as a state check. The leaver's hull went to the AI,
  REJOIN gave the seat back with the turn running and no READY, the hull was no longer AI-flown on either machine, and
  the race ended by itself (checklist "A leaver's hull flies the gates", step 3).

## Not bugs, missing features (tracked in [`TODO.md`](TODO.md))

Ping on the party panel and scoreboard; party-wide Relay region; `ServerScoreGate` / Cloud Code deploy; host migration; a party-level reconnection grace period.

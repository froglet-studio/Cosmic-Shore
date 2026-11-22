# Multiplayer known bugs — index

One line per bug, with its current state. Full entries (repro, root cause, fix) stay where they are:
**P-** = [`../PartySystem/BUGS.md`](../PartySystem/BUGS.md), **Pr-** = [`../PresenceSystem/BUGS.md`](../PresenceSystem/BUGS.md). Updated 2026-11-02 (audit 19:00 IST).

## Open

| Bug | State |
|---|---|
| P-B36 / P-B45: a long frame freeze (boot menu load, cell swap) makes the Relay drop the host | Seen on a GUEST too (2026-11-15, MPPM): Player 2 froze 17.9 s loading MinigameBloomrush from the menu and bounced with "Host disconnected"; the host logged `clientLeft` and `leaverToAI`. That Player 2 had been in Play for over an hour. **Measured 2026-11-15** (`Docs/PartySystem/BUGS.md`, "B45 measured"): the frame is a Single scene load whose cost grows with the prisms alive (the menu vessel's trail grows about 2,500 a minute with no ceiling: a launch after 17-21 idle minutes froze 8-13 s and dropped the guest once; a Bloomrush return with 68,000 prisms froze 9-11 s); not focus, not memory. No one small step owns it, so nothing was changed. **2026-11-20:** the return to the menu now takes the match's world down behind the splash before the load: the load's frame on a 73,500-prism Bloomrush return fell from 9.5-10.5 s to 5.7-6.5 s, and the return takes about 6 s longer in all (`Docs/PartySystem/BUGS.md`, "B45 on the return to the menu"). Played solo and with the editor and MPPM Player 2 (5.7 s on the host, 4.5 s on the guest, nobody bounced); one PC, no build. **2026-11-21:** the second freeze on the menu after a return (6.9 s, 8.2 s and the 21.9 s that ended the party on 2026-11-20) did not happen on one PC, solo or with two players, with or without the drain. Every stall is now followed by a `stallFrame` mark naming the engine work of the frozen frame, so the next two-machine test names it (`Docs/PartySystem/BUGS.md`, "B45 on the menu after a return"). **2026-11-22:** on a launch from the menu the trail goes back to its pools before the load (12 ms a frame, the host waits up to 4 s; a guest starts when the load arrives): one launch each at about 19,600 trail prisms, the load's frame 3.2 s at the old rate and 1.7 s now; the 8-13 s launch frames were not reproduced (`Docs/PartySystem/BUGS.md`, "B45 on a launch from the menu"). **2026-11-22 evening:** B45 reproduced on the RETURN to the menu with four built players on one PC (every player froze 9-11 s leaving Bloomrush, the three joiners were dropped; two players survived a 9.3 s freeze); a guest stayed through a launch after 10 min on the menu at 17,500 trail prisms (`Docs/PartySystem/BUGS.md`, "B45 on the return to the menu, built players"). Still open: the long-menu launch freeze at 50,000 prisms and more, the second freeze on the menu after a return, the Cleave lay, and Garrett's machine |
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
| **A pilot who rejoined is skipped on the menu; a pilot who left stays behind as a Player** (2026-11-20; no B number: B48 is taken twice) | Fixed 2026-11-20 on `Ys-bleeding-edge`. **Cause:** a rejoin is approved with no player object of its own, so the reclaimed Player is owned by the new connection without being its player object, and the host's scene pass (which read only player objects) never reached it: no menu hull, not in the host's roster, and still flagged to outlive its owner. When that guest left the party, or when any guest left while the match was returning to the menu, the Player stayed on the host, server-owned and not an AI. Joining again gave two pilots of one name ("Tiger (2)"), and the leftover was on the other machine's roster and scoreboard. **Fix:** `ServerPlayerVesselInitializer.PersistentPlayerOf` (a client's Player is its player object or the human Player it owns) and `ReleaseDepartedHumans` (a scene that adopts no leaver clears the flag at once and removes a Player already left behind). **Played** by a session (editor + MPPM Player 2): after a rejoin the host's menu lists both pilots and the next match seats both; a leave on the menu and a leave during the return leave no Player; joining again shows no "(2)". Not played: three or four players, the Maelstrom hub, a build |
| **Maelstrom ignores where the host placed its AI** (Garrett and Yash, 2026-11-19: two AI placed on Ruby came out as one Ruby and one Gold) | Fixed 2026-11-20 on `Ys-bleeding-edge`. The tournament deals its AI seats in the hub (`MaelstromController.DealAISeats`) and that deal had only the balanced pick; the match spawner's rule "a domain the host placed wins" never ran, because every round replays the seats the hub dealt. Both now call one rule, `ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain`. `PlacedAIDomainTests` (4) pass. **Played** by a session: solo host with three AI placed on Ruby (all three on Ruby), then the reported case with the editor and MPPM Player 2 (both people on Jade, two AI placed on Ruby): the hub, a Breakwater round and the hub after it show both AI on Ruby on both machines. Not played: two machines, and the placement made by clicking the launch panel |
| **The boot waits long at 41% "Creating session"** (Yash, 2026-11-21: "always taking a lot of time") | Measured 2026-11-21 (Yash's editor, solo online boots, Party and Boot channels on, every slow frame logged). 41% is the party session's create: it starts when the presence lobby is joined and ends when the Relay host is up. **Before:** 4.9-14.8 s (11 boots, mean 9.3 s); UGS refused something as rate-limited in 9 of 11 boots, the lobby create inside the party create in 6 (`LobbyHandler.CreateLobbyAsync`), each refusal costing 3-10 s. Right after the join the game also re-published the player's name and avatar, a read and a write on the presence lobby, although the join had just sent both. **Fix:** that republish runs only when the name or avatar changed during the join (`HostConnectionService.IdentityDiffers`, `PresenceIdentityRepublishTests` 5). **After:** a refusal in 1 of 6 boots; the create took 6.2-11.0 s (mean 8.3 s), and a clean create is about 4.7 s of network before the host starts (Relay allocation and the lobby create, from India) plus 1.5 s after it. The 36% step before it ("Joining lobby", 6-9 s) is mostly the menu's own 4.1 s load frame. What is left is UGS round trips: shortening it further means not holding the menu for the Relay host (a design change). The Party channel now times the presence query, the join and the create, and a rate-limited retry names the SDK call refused. **Second change (Yash: "yes, try it"), the menu no longer waits for the Relay host:** the Garland is laid DURING the party create while nothing listens (`Cell.BuildsBeforeHost`, `Cell.BootBuild.cs`; before, it waited for the local player and then took about 3 s), and once it stands the menu is revealed with UI input held (`SceneLoader.BootReveal.cs`); the vessel flies in when the host is up and input comes back then. A host still missing 12 s after that reveal covers the menu again so the retry and offline screens show (`bootMenuHostWaitSeconds`; not seen in a run, the host always came first). Measured 2026-11-21 (editor in the background, about 4 fps outside long frames, so the fades are stretched): Menu_Main loaded to menu shown 6.8-7.5 s in 3 boots, against about 10.5 s before; early reveal 3.5-4 s before the vessel when the create was slow, none when the host came up first. `BootRevealBeforeHostTests` 8 |
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

Retest of 2026-11-17 (`8c8aa5eae5`, editor "Lion" + MPPM Player 2 "Tiger"; checklist "A leaver's hull flies the gates",
retest block): the sign-in fix, the FunCard fix, the leaver's AI in Switchback (10 gates threaded under AI), the
missing-stat zero and the "host left" close in a game scene all PASSED. One new fault found and fixed: the host's
scoreboard carried its last match's drift and boost into the next match (`EventDrivenStatsProvider` now seeds 0);
replayed, both machines agree. Seen, not followed up: a stale REJOIN offer stays on the menu after the player has
rejoined that party; FMOD `Event not found` errors on both players (not multiplayer code).

Session-driven run of 2026-11-20 (`7788ad036d`, editor "Lion" + MPPM Player 2 "Tiger", live UGS; nobody watched a
screen; checklist entries "The host left", "A guest hands its own hull to the AI" and "A pilot who rejoined or left"):
- Boot: both players reached `InParty`, no `Invalid transition` at boot, no Friends warning, no second sign-in. A second
  Play started in a new session (P-B33 held).
- "The host left": PASSED. After a mid-match Stop and a new Play, Player 2's menu showed nothing for 55 s; a planted old
  drop record was given up silently.
- Idle guest on AI: PASSED in Switchback (20 gates), Bloomrush, and three Maelstrom rounds. The host's idle hull was left
  alone. The key-press hand-back needs a person.
- Drop and REJOIN: PASSED again (seat back 15 s after the press, `rejoinedRunningTurn`, no READY, no P-B46 / P-B47 error).
- Maelstrom, intensity 3, three rounds (Dog Fight, Breakwater, Serpent Time): all finished, same scoreboards on both.
  The prism hand-over ran on the one round with a cell arena: `arenaParked 9043 prisms` in 3.5 s (host) and 3.2 s
  (guest), no `arenaParkCutShort`. Player 2 froze 9.9 s returning from Dog Fight to the hub (the host 3.4 s); the
  driver's probes add 2-3 s, so that number is not clean. The hub then started a fourth round by itself.
- **Found and fixed:** a pilot who rejoined was skipped on the host's menu, and a pilot who left stayed behind as a
  Player (the "Lion (2)" of the test with Garrett the night before). Replayed after the fix: all four cases pass.
- **Found and fixed:** the idle guest's AI hull was not reliable in Switchback (20, 16 and 11 gates in some races, 0, 1
  and 2 in three others; the host's AI hull threaded all 26 every time). Switchback's blast AI (key, blast, prism sense)
  was built on the server only, so a guest's own AI hull flew the plain ring steering into the first closed door. A
  guest now builds it when it arms its own hull. Replayed: two races in a fresh Play, 25 and 21 gates (a race ends when the host's hull
  threads its 26th); a third could not be run, Player 2's editor hung when the PC ran out of memory. Editor and development builds only
  (`PilotControl` is not in a release build).
- Not a game fault: the PC's internet dropped twice during the day (UGS `Request timeout`, `Cannot resolve destination
  host`, `DTLS handshake failed`). One drop cut the guest and then the host out of a running match with no freeze
  (`relayDrop no freeze in the last 30 s`); another made a whole Play boot without a party. Those runs were thrown away.

## Not bugs, missing features (tracked in [`TODO.md`](TODO.md))

Ping on the party panel and scoreboard; party-wide Relay region; `ServerScoreGate` / Cloud Code deploy; host migration; a party-level reconnection grace period.

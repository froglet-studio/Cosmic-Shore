# Dropping out of a match

> Measured 2026-10-10 on `cece/nice-brahmagupta-ojclij` (Multiplayer QoL item 6, Steam board R13).
> Every step below is what the code does today, with a file:line citation; line numbers drift,
> the symbol names do not. No editor was open in the session, so nothing here was played: the
> three scenarios are traces, and the findings at the end say which were fixed on this branch
> and which are written up for a human to decide.

**Rejoin-in-progress and host migration are cut and stay cut** (`prompts/MULTIPLAYER_QOL_PROMPT.md`
§6, `STEAM_RELEASE_TASKS.md` R16). This document is about the experience of the drop itself:
does the dropped player know what happened, does the rest of the match continue cleanly, and is
there a route back to the party.

## 0. The machinery every scenario runs on

| Fact | Where |
|---|---|
| A dead link is detected by UnityTransport after **30 s** of silence (`m_DisconnectTimeoutMS: 30000`, heartbeat every 500 ms). Both sides use the same value: the host notices a vanished client after 30 s, and a client notices a vanished host after 30 s. | `Assets/_Prefabs/CORE/NetworkManager.prefab:98-101` |
| The only two per-machine signals a client gets are `NetworkManager.OnClientDisconnectCallback(LocalClientId)` and `OnTransportFailure`. Both are wired at sign-in by the persistent `MultiplayerSetup` in `Bootstrap.unity`. | `MultiplayerSetup.cs:229-230` |
| Both signals route into one recovery, `PartyInviteController.HandleHostLossAsync(reason)`, which is idempotent through `_transitioning` so the double-fire on a hard drop runs once. | `MultiplayerSetup.cs:541`, `:593`; `PartyInviteController.cs:572` |
| The recovery is the deliberate-leave sequence with a notice on the end: destroy the local Player and vessel, leave the UGS party session, shut the NetworkManager down (2 s budget), load `Menu_Main` locally, recreate the player's own solo Relay session, then raise the toast with a hold so it survives the reload. | `PartyInviteController.cs:616-671`; `ToastChannel.cs:30-42`; `ToastService.cs:22-27` |
| A pilot who leaves a GAME scene is adopted by the AI on the host: Player and vessel are spawned `DontDestroyWithOwner`, the disconnect callback flips `NetIsAI`, hands the ship to `AIPilot`, and posts `PilotHandedToAI` ("NAME left - AI has the ship") on the host and, by ClientRpc, on every client. `RoundStats` rides the Player, so the score keeps counting (B21). The menu and the Maelstrom hub opt out. | `ServerPlayerVesselInitializer.cs:339-352`, `:846-903`, `:970`; `ClientPlayerVesselInitializer.cs:315`; `GameToastConfig_Shared.asset:54-63`; `MenuServerPlayerVesselInitializer.cs:144`; `MaelstromHubVesselInitializer.cs:65` |
| Every ready gate is re-decided on a disconnect, with the departed id pruned, so the remaining players are never held at a ready screen by someone who is gone (B20). | `MultiplayerMiniGameControllerBase.cs:50`, `:602-666`; `ArcadeConfigSyncManager.cs:451-482` |
| The host's party roster is reconciled against the UGS session on the Netcode disconnect (`ReconcilePartyMembersNow`) and on `ISession.PlayerLeaving`; other clients catch up on the 3 s poll. | `MultiplayerSetup.cs:521`; `HostConnectionService.cs:1121`, `:2523` |
| The route back is the friends panel's online row: a host who is **in a match** offers **Spectate** only; a host back in the menu with a published, non-full session offers **Join**. Rejoining a running match is not offered anywhere, by design. | `FriendsListPanel.cs:512-534`; `SPECTATOR.md` §2 |
| `DisconnectNotice` (the modal with Reconnect) is driven by `Application.internetReachability`, polled every 5 s, not by Netcode. It never fires for a Relay or transport drop while the internet is up, and its Reconnect button is hidden whenever a NetworkManager is listening. | `DisconnectNotice.cs:100`, `:131-136`; `NetworkMonitor.cs:40`, `:75`, `:91`; `ReconnectService.cs:83-93` |

## 1. A client loses its connection mid-match (transport timeout)

Host H, clients A and B in a scored arcade match. A's link dies (Wi-Fi off, cable out) at t=0.

### On A (the dropped client)

1. t=0 to 30 s: nothing fires. A's own hull keeps flying under A's owner-authoritative transform;
   H's and B's hulls freeze at their last replicated pose; the HUD scores stop moving; the toast
   feed is silent. No indicator says the link is gone. If A's internet reachability also dropped,
   `NetworkMonitor` raises `OnNetworkLost` within 5 s (`NetworkMonitor.cs:75`),
   `ApplicationStateMachine` enters `Disconnected` (`ApplicationStateMachine.cs:158`) and
   `DisconnectNotice` shows "Connection lost" with Dismiss and no Reconnect (the button is hidden
   while the match's NetworkManager is still listening, `ReconnectService.cs:91-92`). A Relay-only
   drop with the internet up shows nothing.
2. t=30 s: UnityTransport gives up (`NetworkManager.prefab:101`), Netcode raises
   `OnClientDisconnectCallback(LocalClientId)` and then shuts the client down, despawning every
   NetworkObject (`Player.OnNetworkDespawn` removes each Player from `gameData.Players`,
   `Player.cs:914-917`).
3. `MultiplayerSetup.OnClientDisconnect` takes the local-client branch and calls
   `PartyInviteController.HandleHostLossAsync(PARTY_CONNECTION_LOST_NOTICE)`
   (`MultiplayerSetup.cs:538-543`). `OnTransportFailure` may fire too and is absorbed by
   `_transitioning` (`PartyInviteController.cs:574-578`).
4. `HandleHostLossAsync` now covers the screen and unpauses first (`PartyInviteController.cs:590-591`,
   this branch), clears the stale `joined_party` presence property fire-and-forget (`:601`), and
   runs `BounceToSoloMenuAsync` (`:616`): `RecoverFromFailedTransitionAsync` yields a frame, calls
   `DestroyPlayerAndVessel` (by then the list is empty, so a no-op), awaits the UGS leave
   (`:660`, typically 1 to 3 s), the NetworkManager shutdown (`:662`, 2 s budget), loads
   `Menu_Main` with Unity's SceneManager (`:665`), and `EnsurePartySessionAsync` recreates A's own
   solo Relay host (`:669`; `HostConnectionService.cs:1174-1230`, `IsPartyHost = true`,
   roster reseeded to A alone).
5. `Menu_Main` loading re-arms the veil and the fade-on-`OnClientReady` (`SceneLoader.cs:115-150`,
   with the 1.5 s settle hold because the previous scene was gameplay). A's autopilot vessel
   spawns under the fresh solo host and the splash fades.
6. The notice is raised AFTER recovery through `ShowPrefixOrHold` (`PartyInviteController.cs:628`):
   no `ToastService` exists in a game scene and the menu's is recreated by the reload, so the
   line is parked on the channel asset for up to 45 s and the fresh scene's `ToastService.OnEnable`
   drains it (`ToastService.cs:27`). It reads **"Connection to the party was lost - returned to
   your menu."** for 4.5 s (this branch; it read "Host disconnected" before).

**What A sees:** 30 s of a silently frozen match (their own hull still responding), then black,
then their own menu with their own autopilot vessel and one toast. Black lasts the UGS leave plus
the shutdown plus the load plus the Relay allocation, roughly 3 to 8 s.

**Where A lands:** `Menu_Main`, hosting a fresh one-person party, presence lobby never left. The
old party is not theirs any more. The friends panel shows H's row as IN A MATCH with Spectate
(`FriendsListPanel.cs:521-524`); once H's party is back in the menu the row offers Join if the
session has a seat (`:529-533`). Nothing on screen tells A that this is the route.

### On H and B (the remaining players)

1. t=0 to 30 s: A's hull freezes mid-arena at its last replicated pose on both screens. A's
   stats stop. The match continues around it; the ready gate (between turns) still counts A as a
   connected human (`SpectatorSession.CountHumanClients`), so a turn boundary inside this window
   waits on A until the timeout.
2. t=30 s: H's transport times out A. Netcode fires `OnClientDisconnectCallback(A)` on H, and
   because the Player and vessel were spawned `DontDestroyWithOwner`
   (`ServerPlayerVesselInitializer.cs:348`, `:970`) Netcode reassigns them to the server instead
   of destroying them.
3. In the same callback, in subscription order: `MultiplayerSetup.OnClientDisconnect` unregisters a
   spectator id and calls `ReconcilePartyMembersNow` (`MultiplayerSetup.cs:516-521`);
   `ServerPlayerVesselInitializer.HandleClientDisconnectedForAITakeover` finds A in
   `_humanPlayersByOwner`, flips `NetIsAI`, hands the vessel to `AIPilot` configured like a backfill
   bot, and posts `PilotHandedToAI` locally plus by ClientRpc to B
   (`ServerPlayerVesselInitializer.cs:846-903`); `MultiplayerMiniGameControllerBase` prunes A
   from the ready set and re-decides the gate (`:658-666`); `ArcadeConfigSyncManager` drops A's
   game-pick chip and re-decides the launch lobby if one is open (`:451-482`).
4. `NetIsAI` replicates to B; `Player.OnNetIsAIChanged` keeps `IsInitializedAsAI` in step
   (`Player.cs:907-912`), so the HUD, the rematch tally (`RematchVoteCount` skips AI,
   `MultiplayerMiniGameControllerBase.cs:462-477`) and the round reset all treat the seat as a bot.
5. `ReconcilePartyMembersNow` refreshes H's `PartyMembers` from the UGS session's player list
   (`HostConnectionService.cs:1121-1160`). The Netcode callback cannot name A's UGS id, so A's
   seat clears from H's roster only once UGS itself has dropped A from the session (its own
   detection, not measured here); `ISession.PlayerLeaving` then also clears any invite aimed at A
   (`:2523-2529`).
6. At the scoreboard A's row is still there with the score A earned plus whatever the AI added;
   A's domain total includes it. When H later returns the party to the menu,
   `ClearPlayerVesselReferences` despawns every `NetIsAI` Player, A's adopted one included
   (`SceneLoader.cs:531-556`), so no ghost member follows the party into `Menu_Main`.

**What H and B see:** A's hull frozen for 30 s, then one toast "A left - AI has the ship" and the
hull flying again under AI. The match, the turn structure and the scoreboard carry on.

### The menu variant

A client who drops while the party is roaming the lava lamp runs steps 2 to 6 on its own side
unchanged. On the host, `MenuServerPlayerVesselInitializer` opts out of adoption
(`MenuServerPlayerVesselInitializer.cs:144`), so Netcode destroys A's Player and vessel with the
owner and the party panel's seat clears on the next reconcile. Nothing is announced in the menu;
the seat simply empties.

## 2. A client quits mid-match on purpose (pause menu Leave)

1. A opens the pause menu. For a client the single exit button is labelled LEAVE PARTY
   (`PauseMenu.cs:366-375`); the Scoreboard's Leave Lobby (`Scoreboard.cs:299`, `:899-910`) and
   the Maelstrom summary's Main Menu (`MaelstromSceneView.cs:557-567`) are the same exit on the
   other two screens (B18, B22).
2. `PauseMenu.OnClickMainMenu` sees a listening non-server NetworkManager and calls
   `PartyInviteController.LeavePartyAndReturnToMenuAsync` (`PauseMenu.cs:337-344`).
3. The leave covers the screen and unpauses immediately (`PartyInviteController.cs:441-442`),
   disarms any spectator payload (`:450`), destroys the local Player and vessel (`:463`; on a client
   these are spawned and not server-owned, so `DestroyPlayer`/`DestroyVessel` return without
   touching them, `Player.cs:1055-1064`), resets runtime data, leaves the UGS session (`:469`),
   shuts the NetworkManager down (`:471`, which sends a disconnect to H), loads `Menu_Main`
   (`:474`) and recreates A's own solo Relay session (`:478`).
4. No toast: the player chose this. The splash fades once A's menu vessel spawns.
5. On H: the NetworkManager shutdown in step 3 reaches H within a tick, so the same callback
   chain as scenario 1 step 3 runs immediately, with no 30 s limbo. B sees "A left - AI has the
   ship" and A's hull keeps flying. The UGS leave in step 3 is explicit, so `PlayerLeaving` fires
   on H promptly and A's roster seat clears on the reconcile.

**What A sees:** black for the teardown, then their own menu. No notice.
**What H and B see:** one toast and a hull that changes pilot without stopping.
**Where A lands:** exactly as scenario 1: own solo party, H's row offers Spectate while the match
runs and Join once H's party is back in the menu.

## 3. The host quits or loses its connection mid-match

There are three different host departures and only two of them orphan anybody.

### 3a. The host presses Main Menu (pause menu or scoreboard)

`PauseMenu.OnClickMainMenu` on the server raises `_onClickToMainMenu` (`PauseMenu.cs:316-322`),
`SceneLoader.ReturnToMainMenu` unpauses, covers its own screen, broadcasts the veil to every client
by ClientRpc (`SceneLoader.cs:299-345`; `MultiplayerMiniGameControllerBase.cs:796-821`, which also
arms each client's 90 s scene-follow watchdog, B19), despawns the AI Players and every vessel, and
drives one Netcode scene load. The Relay session and the party survive; everybody lands in
`Menu_Main` together (TESTS.md S9). Nobody is dropped and nobody is told anything, which is right.

### 3b. The host quits the application, or its process dies

1. On a clean quit Netcode's host shutdown sends a disconnect to every client; on a crash the
   clients wait out the 30 s transport timeout exactly as in scenario 1. Either way each client
   gets `OnClientDisconnectCallback(LocalClientId)` and runs scenario 1 steps 3 to 6 on its own:
   black, own menu, own solo party, the one toast.
2. The host's UGS party session is deleted by the host's leave or reaped by UGS. A client's refresh
   loop that reaches the session first sees a definite session-gone error and would start its own
   solo-session recovery (`HostConnectionService.cs:1976`), but both the loop's outer catch and the
   session-refresh catch stand down while `PartyInviteController.IsTransitioning`
   (`:1520-1534`, `:1946-1947`), so the bounce and the refresh never race.
3. A spectator watching the match is bounced by the same callback and lands the same way
   (`SPECTATOR.md` line 216).

**What every client sees:** scenario 1's experience, for all of them at once: a frozen match for up
to 30 s (0 s on a clean quit), then black, then their own menu and the toast. The party is gone.
Each former member is now the host of a one-person party; the friends panel shows the others as
plain online rows, each with a Join button, so any of them can re-form the party by joining
another, but nothing says who should.

### 3c. The host's link dies

The host keeps running its match locally with every remote hull frozen; after 30 s it times out
each client in turn and adopts each of them into the AI (scenario 1 steps 2 and 3 on H, once per
client). The host ends up alone in an arena of bots with three "left - AI has the ship" toasts.
Each client runs scenario 1 and lands in its own menu. When the host's internet returns, nothing
reconnects anybody: the host's party session is still its own, the clients have each made their
own, and the presence lobby shows the host as IN A MATCH until it returns to the menu.

## 4. Findings (dated 2026-10-10)

| # | Finding | Status |
|---|---|---|
| F1 | **A host-loss bounce from a game scene ran its teardown uncovered.** `HandleHostLossAsync` went straight into `RecoverFromFailedTransitionAsync`, which destroys the local Player and vessel and then awaits a UGS leave and a NetworkManager shutdown (1 to 3 s) before `Menu_Main` loads; only the menu's own `OnSceneLoaded` ever put the veil up. A client whose host dropped watched its own hull disappear from the arena with the HUD still showing. The deliberate Leave path covers the screen first (`PartyInviteController.cs:441`); the bounce did not. If the drop landed with the pause menu open, `Time.timeScale` stayed 0 into the menu as well, because the pause panel dies with the scene and nothing else restores it. | **Fixed on this branch**: `HandleHostLossAsync` now calls `SetFadeImmediate(1f)` and `PauseSystem.TogglePauseGame(false)` before the teardown (`PartyInviteController.cs:580-591`), mirroring Leave. |
| F2 | **The bounce notice blamed the host for the player's own connection.** `OnClientDisconnectCallback(LocalClientId)` fires identically when the host quit, when the host's link died and when THIS machine's link timed out; the client cannot tell which, and the reason string was "Host disconnected" for all three (the transport-failure path said "Connection lost"). | **Fixed on this branch**: both Netcode signals pass one shared `PARTY_CONNECTION_LOST_NOTICE`, "Connection to the party was lost - returned to your menu." (`MultiplayerSetup.cs:31`, `:541`, `:593`). Same wording family as the join-failure and watchdog notices. |
| F3 | **`MultiplayerDomainGamesController.OnPlayerLeavingFromSession` could never run, and would have deleted a score if it had.** `ISession.PlayerLeaving` carries the UGS PlayerId (a string id; `HostConnectionService.OnPartySessionPlayerLeaving` uses it as one), and the override parsed it with `ulong.TryParse` as a Netcode client id, so the lookup never matched and the `PlayerDisconnected` toast never posted. Had it matched, `gameData.RemovePlayerData(name)` would have removed the departed pilot's `RoundStats`, which B21 keeps on purpose, and the toast would have doubled `PilotHandedToAI`. | **Fixed on this branch**: the override is deleted; the base hook's parameter is renamed `ugsPlayerId` and its remarks say what it carries and where mid-match departure is really handled (`MultiplayerMiniGameControllerBase.cs:155-172`). `GameToastSituation.PlayerDisconnected` (authored in `GameToastConfig_Shared.asset:35-43`) now has no poster; it is left authored for the menu-side or hub-side notice that F5 may want. |
| F4 | **Thirty seconds of limbo on both sides of a hard drop.** `m_DisconnectTimeoutMS` is UnityTransport's default 30 000. For that long the dropped player flies a frozen match with no indicator, and the others watch a frozen hull before the AI takes it. Lowering the value is a one-field prefab change with a real trade-off (a mobile or Relay hiccup longer than the new value becomes a drop), and a client-side "connection unstable" cue (transport RTT or ticks since the last server packet, surfaced on the HUD) needs UI authoring. | **Written up.** Decide the timeout with a two-machine test; 10 to 15 s is the usual compromise. Neither changed here: the first is a prefab value, the second is a HUD element. |
| F5 | **The dropped player is never told how to get back.** The toast says where they are, not what to do next. The actual route (Spectate on the host's row while the match runs, Join once the party is back in the menu, `FriendsListPanel.cs:512-534`) exists and is reachable in two taps, but it is undocumented on screen; a bounced player who does not know the friends panel reads the drop as the end of the session. | **Written up.** A second held line ("Watch or rejoin them from the friends panel") would ride the existing `ShowPrefixOrHold` mailbox but that mailbox holds ONE line by design (`ToastChannel.cs:27`); either widen it to two or put the hint on the party panel. UI authoring either way; not changed here. |
| F6 | **A hard-dropped client's party seat on the host clears on UGS's clock, not Netcode's.** `ReconcilePartyMembersNow` re-reads the UGS session's player list, and the Netcode callback cannot name a UGS id (`MultiplayerSetup.cs:508-521`), so after a hard drop the host's roster keeps the member until UGS itself removes them. The in-match consequences (adoption, gates, chips) are all Netcode-driven and immediate; only the roster lags. A deliberate Leave is explicit on UGS and clears promptly. | **Written up.** How long UGS takes to drop a vanished member was not measured (needs two machines). If it is long, map the departed Netcode client to its `Player.NetUgsPlayerId` at disconnect time and remove that member locally. |
| F7 | **`DisconnectNotice` does not cover a party drop.** It listens to internet reachability only, so a Relay or transport drop with the internet up shows nothing until the 30 s bounce, and when the internet does drop mid-match the notice appears with no Reconnect button (hidden while a NetworkManager is listening) and is then torn through by the bounce. The notice and the bounce are two systems that do not know about each other. | **Written up.** Out of scope for a contained fix; the honest path is to have the bounce dismiss or re-title the notice, or to make the notice the surface for F4's "connection unstable" cue. |
| F8 | **Scoreboard and gates after an adoption are correct.** Checked, not a gap: `RoundStats` persists on the adopted Player, `NetIsAI` replicates and `OnNetIsAIChanged` keeps `IsInitializedAsAI` in step, the rematch tally skips the seat, the ready gates count connected clients only, and the host's return to the menu despawns the adopted Player with the other bots so no ghost follows the party. | No change. Playtest steps are in `UNITY_VERIFICATION_CHECKLIST.md` (the R13 item 6 steps). |

## 5. What was fixed on this branch

- `Assets/_Scripts/Controller/Party/PartyInviteController.cs`: `HandleHostLossAsync` covers the
  screen and unpauses before the teardown (F1).
- `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`: one neutral bounce notice shared by
  the client-disconnect and transport-failure signals (F2).
- `Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs` and
  `MultiplayerMiniGameControllerBase.cs`: the dead `OnPlayerLeavingFromSession` override removed,
  the base hook documented (F3).

No prefab, scene or asset was touched. Compile proofs are in the branch's checklist entry.

## 6. Related

- `BUGS.md` B10 (the bounce), B18 and B22 (a client can leave), B19 (the scene-follow watchdog),
  B20 (gates re-decided on departure), B21 (the AI takes the ship).
- `SPECTATOR.md` §2 (which row button, and when).
- `ARCHITECTURE.md` § "Locked design" (why the recovery recreates a solo Relay instead of
  reusing anything).

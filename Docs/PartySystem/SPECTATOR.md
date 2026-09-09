# Spectate mode and direct party join

Two verbs on the friends panel's ONLINE row that need no invite: **Join** (walk into a
friend's party) and **Spectate** (watch a friend's live match without playing in it). Both
ride the presence lobby, the party Relay session and the ordinary client-join transition
that `PartyInviteController.AcceptInviteAsync` already runs — no second networking path.

Shipped 2026-09-09 on `claude/main-menu-online-request-59dqaj`. **Not yet verified in the
Editor** (see § Verification).

---

## 1. The one fact that makes both possible: the party session id is PUBLISHED

Before this, a party session id travelled only inside an invite payload
(`invite_payloads`, `../PresenceSystem/ARCHITECTURE.md`), so a player who had not been
invited had nothing to join. `HostConnectionService` now publishes its live party session
id as a presence player property, **`partySession`** (`PARTY_SESSION_KEY`), through the
same success-gated `PublishPartyStateIfChangedAsync` path as `partyCount`/`partyMax`/
`matchName`, and `ReadOnlinePlayerData` reads it into `PartyPlayerData.PartySessionId`.

`ResolvePublishedPartySessionId()` is deliberately EMPTY while the local player is
spectating, offline, or holds no live session — so nobody can chain-join or chain-spectate
*through* a spectator, and an offline host advertises nothing.

`PartyPlayerData` gained two predicates the UI keys on:

| Predicate | Meaning |
|---|---|
| `HasJoinableSession` | A non-empty `PartySessionId` was published |
| `IsInMatch` | A non-empty `MatchName` (the existing "IN A MATCH — X" signal) |

## 2. The row: which button, and when

`FriendsListPanel.ResolveJoinMode(player, status)` → `OnlineInfoEntry.JoinMode`:

| Row status | Join button |
|---|---|
| IN YOUR PARTY | Hidden |
| IN A MATCH | **Spectate** (eye icon, `spectateTint` amber) — the ONLY enabled button on the row; disabled at `joinDisabledAlpha` if no session id was published |
| PARTY FULL | Join, **disabled** (`JoinDisabled`) — or hidden when no session was published |
| Anything else with a published session | **Join** (doorway icon, `joinTint` blue) |
| No published session | Hidden |

The button is one `Button` + one `Image` (`joinButton`, `joinButtonIcon`) on
`OnlineFriendsInfo Variant.prefab`; the sprite and tint are swapped per mode, never a
second button. `SetInvitePending` hides it while an outgoing invite is pending and
`ResetInviteState` re-applies the mode. Icons: `_Graphics/Port/icon_JoinParty.png`,
`icon_Spectate.png` (64×64 white silhouettes, tinted at runtime like the row's other
icons).

## 3. Direct join

`PartyInviteController.JoinPartyAsync(target)` → `RunClientJoinAsync` (the same
transition `AcceptInviteAsync` runs — leave own session, `NetworkSceneObjectGuard.Sweep`,
shutdown local host, join, wait for the client connection, wait for `OnClientReady`, bounce
to a fresh local host on any failure) with `HostConnectionService.JoinPartyDirectAsync`
as the join step: `IPartySessionService.JoinByIdAsync(target.PartySessionId)`,
`IsPartyHost = false`, the roster seeded with the target, `PartyState.InParty`, and
`joined_party` published so the host's admit-scan sees the member (B8). The UI's
`PARTY FULL` state is advisory; the session's own `MaxPlayers` is the authority and a full
session fails the join, which bounces.

## 4. Spectate

### 4.1 A spectator is a Netcode client with NO Player object

That is the whole design and it is the ONE shape. `SpectatorSession.BeginLocal(target)`
arms `NetworkConfig.ConnectionData` with the versioned token
`cosmicshore.spectator.v1` BEFORE the session join; the host's connection approval
(`MultiplayerSetup`) reads it back off `ConnectionApprovalRequest.Payload` and answers
`CreatePlayerObject = false`, registering the client id in
`SpectatorSession.ServerRegister`. Nothing downstream — vessel spawner, arena roster,
scoreboard, AI domain balance, `gameData.Players` — ever sees a pilot that is not there.
A spectator Player with a null vessel was rejected because it would have to be filtered
out of every consumer of `gameData.Players`, and the first consumer nobody remembered
would be the one that threw. `Player.OnNetworkSpawn` logs an error if a local spectator
ever gets a Player anyway (an approval callback that stopped honouring the payload).

Two things the host must SUBTRACT a spectator from, because it counts
`ConnectedClientsIds` as "humans who must press Ready":
`MultiplayerDomainGamesController`, `CoOpWildlifeBlitzMiniGame`, `MaelstromLobbyNetwork`
and `ArcadeConfigSyncManager` all read `SpectatorSession.CountHumanClients(nm)`. The
registry is cleared on `OnServerStarted` because client ids restart per server session.

On the UGS side the spectator joins the party Relay session (the match's session — the
launch reuses it) with the session player property **`spectator=1`**
(`PartySessionService.SPECTATOR_KEY`), which `PartyMemberService.SyncFromSession` skips
in both directions, so a spectator never appears in the party roster and a member who
turns spectator is removed as if they had left. `HostConnectionDataSO.IsSpectating` holds
the local state: `RefreshAsync` skips the party-member diff, `SendInviteAsync` refuses,
and the published `partySession` is empty.

### 4.2 Who fades the veil

`OnClientReady` means "the LOCAL vessel is initialised" and a spectator has none, so
`ClientPlayerVesselInitializer.ProcessPendingPairs` marks the local pair resolved without
raising it, and `SpectatorController` owns the gate instead: it binds the first live pair
(the clicked pilot by `UgsPlayerId`, then by name, then any human), waits
`PrismTrailBuilder.PollArenaReady()` (capped at `ArenaBuildFadeCapSeconds`, 20 s) so the
viewer is not shown a half-laid arena, then `FadeFromBlack`. `RunClientJoinAsync` arms
the splash fade ONLY when it expects a local vessel; `SpectateAsync` waits on
`SpectatorController.WaitUntilWatchingAsync(spectateReadyTimeoutSeconds = 45)` and bounces
to a fresh local host on timeout exactly like a failed invite accept.

### 4.3 Cameras

| Mode | Rig | Notes |
|---|---|---|
| **Player** | `CameraManager.SetupGamePlayCameras(vessel.CameraFollowTarget)` | The vessel's own authored `CameraSettingsSO` follow, re-pointed per spectated vessel |
| **Dolly** | `CameraManager.BeginManualReplayCamera()` | A slow broadcast orbit (9°/s) at `max(60, 2.6 × |followOffset|)`, 0.35 r above the vessel, eased in `LateUpdate`; `RestoreGameplayCamera` on the way out |

The two platform laws that describe "the ship I am looking at" —
`PrismOcclusionCorridor.SetTarget` and `VesselVisionShading.SetLocalVessel` — are moved
onto the spectated vessel and off the previous one by hand, exactly as
`VesselController.ChangePlayer` moves them together. The **speed tunnel is left unbound
on purpose**: a viewer's field of view must not lurch with somebody else's throttle. The
gameplay HUD is muted (`MiniGameHUD` components and their canvases, the `GameCanvas`
canvas) because every element on it describes a pilot this machine is not.

### 4.4 The overlay and the exits

`SpectatorOverlay` is a runtime ScreenSpaceOverlay canvas (sorting order 20000): ◀ /
"SPECTATING" / the pilot's name in their live domain colour / "i / n" / ▶, a CAM: PLAYER
| DOLLY toggle, and ✕ LEAVE. Input mirrors it: ← / Q / d-pad left / LB previous, → / E /
d-pad right / RB next, C / Tab / north face camera, and the `OverviewGesture` (Escape /
Start) leaves.

Every exit funnels through `SpectatorController.Leave(reason)`:

- the close button or the overview gesture;
- **the watched match ending** — `GameDataSO.OnMiniGameEnd`, which every peer receives,
  so the viewer is returned to their own Menu_Main automatically (the requested
  "thrown off" behaviour);
- host loss, which `PartyInviteController.HandleHostLossAsync` already bounces.

Before the viewer is watching, `Leave` fails the ready gate and the join flow bounces; once
watching it calls `LeavePartyAndReturnToMenuAsync`, which also runs
`SpectatorSession.EndLocal()`, as does `RecoverFromFailedTransitionAsync`. The controller
notices a Menu_Main load and destroys itself, so no state leaks into the next session.

## 5. Tests

`Assets/_Scripts/Tests/Editor/SpectatorSessionTests.cs` — approval payload round-trip
and rejection of a foreign payload, `BeginLocal`/`EndLocal` state, the `PartyPlayerData`
predicates and equality, and every branch of `FriendsListPanel.ResolveJoinMode`.

## 6. Verification (manual, MPPM)

Not yet run. The branch was authored out-of-editor; syntax-gated with Roslyn and the
`Tools/Build` Python gates only.

1. Two players in the presence lobby, A hosts a party → B's row for A shows **Join**;
   B presses it → B lands in A's party (`ArcadeLobbyList` shows both) with no invite.
2. A's party at `MaxPartySlots` → B's Join is drawn disabled.
3. A launches any multiplayer mode → B's row for A shows **Spectate** as the only enabled
   button; B presses it → B arrives in the match scene with the overlay, no vessel, no
   HUD, the veil fading only once the arena is laid; A's Ready gate does not wait on B.
4. ◀/▶ cycle every human and AI vessel; CAM toggles between the follow rig and the orbit;
   the corridor and vision band follow the watched vessel.
5. The match ends → B is back in Menu_Main on its own local host, `IsSpectating` false,
   B's own `partySession` published again.
6. ✕ LEAVE / Escape mid-match → same return; A's match is undisturbed.

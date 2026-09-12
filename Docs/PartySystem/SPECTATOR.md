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

### 4.3a The viewer must be able to say why it sees nothing

A viewer that never binds a vessel sits on an opaque black veil until its 45 s watchdog bounces
it, and on screen that is indistinguishable between a scene that never synced, a roster that
never arrived, and a roster whose vessels never initialised. Two changes make that a named cause
instead of a silence, and both are the same lesson from different ends.

**Binding polls, it does not ride one event.** `OnPlayerPairInitialized` is the fast path;
`SpectatorController.BindPoll` re-checks every 0.5 s until it is watching, so a pair that landed
through some path that does not raise the event, or before the controller subscribed, still binds.
Every 5 s an un-bound viewer states its census — scene, connection, players, how many carry a live
vessel, candidates — and names which of the three failures that pattern is.

**One throwing vessel no longer costs the client the rest of the roster.**
`ClientPlayerVesselInitializer.ProcessPendingPairs` wraps each `InitializePair` in its own
try/catch. `vessel.Initialize` walks an entire vessel's components, and a single throw used to
abort the loop with every remaining pair still queued — which for a spectator means no vessel to
watch at all, and for a pilot means a black veil to the watchdog. Same doctrine as
`ImpactorBase.RunEffectIsolated`: report once, loudly, with the offender attached, and let the
siblings run.

### 4.3b The watched pilot is told they have an audience

A viewer has no vessel, no name plate and no presence in the arena, so the pilot being watched has
no way to learn it. `Player.NetSpectatorCount` (server-write) carries the number, and a small eye
badge under the goal stack shows it (`SpectatorWatchBadge`, ensured by `MiniGameHUD` — the goal
stack lives in two forked GameCanvas prefabs, so an authored badge would be a hand-edit in both
and missing from whichever one the next scene copies).

Two things about the plumbing are forced rather than chosen. **The report is a non-owner
ServerRpc** (`ClientPlayerVesselInitializer.ReportSpectating_ServerRpc`): a spectator owns no
NetworkObject at all, so every owner-gated channel is closed to it. And the server **re-derives**
every count from `SpectatorSession`'s watch book rather than incrementing, so a viewer that
switches pilots, disconnects or has its scene reloaded can never strand a count above zero. The
count resets per scene with `NetArenaReady`, for the same reason.

The badge draws the same eye sprite as the friends row's Spectate button — one asset, moved to
`Resources/UI/icon_Spectate` so a runtime-built badge can load it — so the button a viewer pressed
and the mark their target sees are visibly the same act.

### 4.3c A spectator needs no vessel of its own — but the fleet assumed one

The spectator saw an empty arena for a completely mundane reason, and the shape of it is worth
carrying past this feature.

`VesselController.Initialize` hands a vessel's `VesselCameraCustomizer` its vessel **only for the
local pilot** (`if (player.IsLocalUser) VesselStatus.VesselCameraCustomizer.Initialize(this)`),
because that call also raises `OnInitializePlayerCamera` — the announcement that latches the
gameplay rig onto the ship you are flying. Correct, and it means that on a machine with **no**
local pilot every vessel in the match carries an un-initialized customizer. `Configure` then
dereferenced that null field (`_cameraCtrl.SetFollowTarget(vessel.Transform)`), and the resulting
`NullReferenceException` came out of `vessel.Initialize(player)`, i.e. out of the middle of
`ClientPlayerVesselInitializer`'s pair loop — so the watched pilot was **never added to the
roster**, `SpectatorController` had zero candidates, and the join timed out at 45 s with nothing
in the log but a bind timeout. The isolation added in §4.3a is what turned it into one named line.

The fix is two lines and no new concept:

- `ApplyControlOverrides` falls back to its **own transform** when it has no vessel. That is not a
  guess: `VesselStatus` `[RequireComponent]`s the customizer, so the component lives on the vessel
  root and `transform` *is* the vessel it would have been handed.
- `SpectatorController.ApplyCameraMode` calls the new **`VesselCameraCustomizer.Adopt(vessel)`**
  before configuring the rig, so the watched ship's authored camera settings apply against the real
  vessel. `Adopt`, never `Initialize`: the announcement `Initialize` raises means *this is the local
  player's vessel*, and a spectator has none.

`Configure` also stands down loudly rather than throwing when a vessel authors no
`CameraSettingsSO`, for the same reason `RunEffectIsolated` exists: it runs inside a loop over
every pilot, where one throw costs everyone after it.

The general rule: **a system gated on "the local pilot" has a second, unstated assumption — that
there IS one.** A spectator does not need a domain or a vessel; it needs every system that only
ever ran with a local pilot present to survive the absence of one. Look for the null deref on the
far side of such a gate, not for a way to fake a pilot.

The two `No Player found to get domain!` errors on the viewer were downstream of the same throw:
`VesselController.Initialize` assigns `VesselStatus.Player` on its first line, so a vessel reporting
no player is one whose pair init never completed. They stop when the pair loop does.

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

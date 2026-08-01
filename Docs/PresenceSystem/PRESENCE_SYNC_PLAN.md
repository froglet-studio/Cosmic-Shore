# Presence Sync — Plan

**Goal (as stated).** When any player anywhere enters the world (their Menu_Main lava-lamp
vessel spawns), that fact is broadcast to every other client and their `FriendsListPanel`
repaints. When a player leaves, they disappear from every other client's `FriendsListPanel`
/ `ArcadeLobbyList` as fast as the platform allows. Both driven by an explicit state machine
so each step is an observable event other systems can subscribe to.

**Status.** Commits 1 and 2 of 8 shipped (§ 5). Every claim below carries a `file:line` and was
adversarially re-verified against source. Corrections from the verification pass are folded in;
where a claim did not survive it is listed in § 8 rather than quietly dropped.

**Nothing here has been run in the editor yet.** Commit 1 is counters plus editor-only logging
and Commit 2 is mostly behaviour-preserving, so the risk is low — but it is unverified. See
`Docs/UNITY_VERIFICATION_CHECKLIST.md`.

**Scope.** The three reported symptoms:

| | Symptom |
|---|---|
| **A** | `FriendsListPanel` online rows are wrong or late |
| **B** | `ArcadeLobbyList` party slots are wrong or late |
| **C** | Profile icons in the arcade screen / configure modal are wrong |

---

## 1. Why it's wrong today, in one paragraph

There is **no push path anywhere in the presence layer**. `HostConnectionService.Update()`
(`:377-388`) ticks a timer and fire-and-forgets `RefreshAsync()`, which `GET`s the lobby and
diffs the roster. The presence lobby is already an `ISession` — the same type whose
`PlayerLeaving` and `Deleted` events this repo *already consumes on the party session*
(`PartySessionService.cs:186,240`) — but **no `ISession` event is subscribed on the presence
lobby at all**. So "a player arrived" and "a player left" are discovered only by the next poll,
that poll's entire body can be voided by two literal no-op catch branches with zero
observability, the poll is switched off completely inside every game scene, and nothing removes
the player from the lobby when the app quits. On top of that the UI panel that shows the party
never re-reads when it opens, and the avatar sentinel `0` resolves to a real icon instead of
failing.

---

## 2. Confirmed root causes

Ordered by blast radius. All verified in source; `[A]/[B]/[C]` = which symptom it drives.

### RC-1 — No push channel; latency floor is one poll tick `[A][B]`

`Update()` → `LobbyRefreshScheduler.ShouldFireNow` → `RefreshAsync()` →
`ISession.RefreshAsync()` → `RefreshOnlinePlayersDiff()` (`HostConnectionService.cs:386,1088,1115`).
No `PlayerJoined` / `PlayerLeaving` / `Changed` / `PlayerPropertiesChanged` subscription exists on
`IPresenceLobbyService.ActiveLobby`.

*Correction from verification:* the floor is not absolute — `ForceRefreshNow()` (`:880`) is an
out-of-band trigger called from `FriendsListPanel.OnEnable` (`:90`), `ArcadeLobbyList.OnEnable`
(`:107`) and `PartyInviteController` (`:228`), and `Boost()` drops the interval to 0.75 s for 15 s
after invite events. But `ArcadeLobbyList.OnEnable` effectively never fires (RC-9), so that panel
gets no out-of-band pull at all.

### RC-2 — Two no-op catch branches void the whole tick, silently `[A][B][C]`

`await _lobbyService.RefreshAsync()` is the **first** await in the try (`:1088`). Everything that
maintains UI state is downstream in the same try: the online diff (`:1115`), invite scan
(`:1118-1123`), acceptance scan (`:1129`), `ScanPresenceForJoinedPartyMembers` (`:1162`),
`RefreshPartyMembersAsync` (`:1165`), `PublishPartyStateIfChangedAsync` (`:1167`). If the first
await throws, the catch's first two branches do literally nothing:

```csharp
if (IsBenignLobbyPatcherError(e))     { /* intentional: no log, no counter increment, no state change */ }
else if (IsBenignSdkStaleIndexError(e)) { /* Silence to match … */ }
```
`HostConnectionService.cs:1176-1184` (same shape on the party-session catch, `:1515-1539`)

A voided tick is byte-for-byte indistinguishable from a healthy one. `IsBenignSdkStaleIndexError`
matches **any** `SessionException` with `Error == Unknown`, and it is evaluated **before** the
rate-limit branch.

### RC-3 — `IsRateLimitException` doesn't walk `InnerException` `[A][B]`

```csharp
private static bool IsRateLimitException(Exception e) =>
    e.Message != null && e.Message.Contains("Too Many Requests");
```
`HostConnectionService.cs:2099-2100` — its two sibling classifiers (`:2124`, `:2213`) *do* walk
`InnerException`. A wrapped 429 therefore misses the rate-limit branch and increments
`_consecutiveRefreshErrors` toward `MAX_REFRESH_ERRORS_BEFORE_RECONNECT` → `ForceReset` →
throwaway lobby.

*Correction:* a structured `SessionError.RateLimited` stringifies to `"RateLimited"`, not
`"Unknown"`, so it is **not** swallowed by the benign branch. The hazard is narrower than "any
429" — it needs a 429 that surfaces with `Error == Unknown` or only an inner-message match.

### RC-4 — The refresh interval inspector field is a decoy `[A][B]`

```csharp
// 1.5f matches the HCS SerializeField default (refreshIntervalSeconds).
builder.RegisterFactory(_ => new LobbyRefreshScheduler(1.5f), …);
```
`AppManager.cs:462-464`. `HostConnectionService.refreshIntervalSeconds` (`:63`, prefab override
`3`) is never passed to the scheduler — it only lengthens the backoff (`:1187`) and
`ResetDeferred` amounts. Also, `ShouldFireNow` accumulates **after** four early-returns
(`:379-382`), so the cadence measures *eligible* time, not wall time, and stalls whenever the
lobby mutex is held.

### RC-5 — Nothing removes presence on quit / pause / crash `[A][B]`

The only presence-lobby leave outside sign-out is `async void OnDestroy()`
(`HostConnectionService.cs:390`), which `await`s `_lobbyService.LeaveAsync()` (`:415-416`) after
teardown has begun. There is **no** `OnApplicationQuit`, `OnApplicationPause`, or
`Application.wantsToQuit` handler anywhere in `Assets/_Scripts/Controller/Party` (verified by
grep). The file's own error text states the outcome: *"other users may see this player online for
~30s until UGS reaps the entry."* No quit path leaves the **Relay party session** either
(`PartySessionService.LeaveAsync` is reachable only from explicit leave / host-loss / transport
failure).

### RC-6 — Departure has no grace: one short read evicts everyone `[A][B]`

`RefreshOnlinePlayersDiff` removes any id absent from a single snapshot (`:1367-1371`), and
`PartyMemberService.SyncFromSession` does the same for party rows (`:157-168`) — the party path
additionally raises `OnPartyMemberLeft`, which flips Friends presence and triggers a full
`FriendsListPanel` online rebuild.

*Correction:* a mass eviction requires a **successful but short** read, not a throw (a throw skips
the diff entirely — RC-2). The code-provable trigger is `ConvergeToCanonicalAsync` swapping
`_activeLobby` to a different lobby mid-cycle; "partial read during a stale-cache episode" is
inferred from the SDK defect, not demonstrated.

### RC-7 — `matchName` is structurally unpublishable; `InMatch` is dead code `[A]`

```csharp
private string ResolveCurrentMatchName() {
    if (_gameData == null) return string.Empty;
    if (IsOnMenuScene()) return string.Empty;   // ← :1867
```
`HandleGameLaunch` is `private void HandleGameLaunch() => PublishPresenceImmediateAsync().Forget();`
(`:2077`), subscribed to `GameDataSO.OnLaunchGame` — the same event `SceneLoader.LaunchGame`
handles, raised while `Menu_Main` is still the active scene. With the lobby mutex uncontended,
`await SemaphoreSlim.WaitAsync()` completes **synchronously**, so `PublishPartyStateIfChangedAsync`
usually runs inline, still on the menu, and `ResolveCurrentMatchName()` returns `""` — which equals
`_publishedMatchName`, so the change-gate at `:1832-1835` early-returns and **no property is
written at all**. Then `Update()`'s `if (!IsOnMenuScene()) return;` (`:382`) kills the loop for the
whole match. Net: `OnlineInfoEntry.Status.InMatch` (`FriendsListPanel.cs:365`) can never render;
in-match players stay listed as invitable, and an invite sent to them is never scanned for because
their loop is dead too.

### RC-8 — A presence reconnect wipes a live party roster `[B]`

Three consecutive non-benign refresh errors → `ForceReset()` → `Reconnecting` →
`JoinOrCreateAsync` → `ApplyPostLobbyJoinState()` → `_memberService.SeedLocalPlayer(clearFirst: true)`
→ `PartyMembers.Clear()` (`:1211-1244`). The **Relay party session and NetworkManager are
untouched** — only the presence layer hiccuped — yet every remote row is destroyed.
`ScriptableList.Clear()` raises only `OnCleared`, never per-item `OnItemRemoved`, so
`ArcadeLobbyList.HandlePartyCleared` blanks slots 1-3 and the Leave button goes non-interactable.
`Reconnecting` is also **terminal**: nothing transitions out of it, and a second escalation attempts
the illegal `Reconnecting → Reconnecting`.

### RC-9 — `ArcadeLobbyList.OnEnable` fires once per scene, not once per open `[B][C]`

`ModalWindowManager` hides by CanvasGroup, never `SetActive(false)`:

```csharp
IEnumerator DisableWindow() { yield return new WaitForSeconds(0.5f); SetCanvasGroupVisible(false); }
// Start(): "Parent containers stay active so OnEnable/OnDisable lifecycle fires for all children."
```
`ModalWindowManager.cs:64-67,212-216`. The `ArcadeLobbyList` GameObject and its whole ancestor
chain ship `m_IsActive: 1` in `Menu_Main.unity`. So `OnEnable` — the panel's only pull path
(`SubscribeSoap` + `PopulateAll` + `ForceRefreshNow`, `:98-108`) — runs once at scene load and never
when the player actually opens the arcade panel.

*Correction:* this does **not** freeze the panel — the SOAP item subscriptions stay live and
`PartyMemberService`'s identity diff (RemoveAt+Insert) still repaints slots. What is lost is
(a) the `ForceRefreshNow()` pull on open, and (b) every **eventless** write:
`SyncLocalIdentity` writes `LocalDisplayName`/`LocalAvatarId` as silent fields, so slot 0 goes
stale, and `HandlePartyChanged`/`HandlePartyCleared` never call `UpdateOnlineStatus()`, so the
"N Players Online" counter drifts.

### RC-10 — Avatar id `0` is the universal sentinel; the icon list starts at Id `1` `[C]`

`SO_DefaultProfileIcons.asset` (the only `SO_ProfileIconList` in the project) runs `Id: 1..18`.
Every producer defaults to `0`: `Player.NetAvatarId = new(0, …)` (`Player.cs:35`),
`PartyMemberService.ReadMemberData` seeds `int avatarId = 0`, `HostConnectionService.ReadOnlinePlayerData`
seeds `0`. And every resolver falls back to element `[0]` — which **is** icon Id 1:

```csharp
return profileIcons.profileIcons.Count > 0 ? profileIcons.profileIcons[0].IconSprite : null;
```
`FriendsListPanel.cs:740-754`, `ArcadeLobbyList.cs:401-415` (+ five more copies).

So "unknown" renders as a *real, plausible* icon indistinguishable from a player who chose icon 1 —
which is why every other icon bug is invisible. `FriendsListPanel.cs:452` hardcodes
`ResolveAvatar(0)` for friend-request rows. Seven duplicated resolvers exist with **divergent**
fallbacks — `PartyInviteNotificationPanel.cs:258` has none, so a miss keeps the *previous
inviter's* face.

### RC-11 — `Player.NetAvatarId` has no observable; chips sample it once `[C]`

```csharp
void OnNetAvatarIdChanged(int previousValue, int newValue) => AvatarId = newValue;   // Player.cs:552
bool IsSpawnReady() => IsValidVesselTypeForSpawn(NetDefaultVesselType.Value)
                    && !string.IsNullOrEmpty(NetName.Value.ToString());              // :555 — no avatar
```
The replication callback writes a silent mirror and raises nothing, and the spawn event
(`OnPlayerNetworkSpawnedUlong`) is gated on name + vessel type only. Meanwhile
`ArcadeGameConfigureModal.SpawnChipForPlayer` samples `p.NetAvatarId.Value` **once** (`:847`) and
subscribes only to `NetDomain.OnValueChanged` (`:855`).

*Correction:* the **primary** path is fine — `SpawnChipsForAllPlayers` runs at modal-open time
(`:824`, `:898`), by which point `NetAvatarId` has long since replicated. Broken paths are
(a) a player joining while the modal is open, and (b) any player whose cloud profile resolves
*after* spawn (`Player.HandleProfileLoadedAfterSpawn`, `:374-386`) — the chip keeps the pre-profile
value forever. Also `CloseAndNotifyClients()` (`:1204-1221`) doesn't call `DespawnAllChips()`, so the
host path leaks per-`Player` `NetDomain` handlers until the next open.

### RC-12 — Two disjoint "who is online" systems `[A]`

`FriendsListPanel.PopulateOnlineSection` iterates `connectionData.OnlinePlayers` (`:294`) — the
**presence lobby** roster. `FriendsDataSO.Friends` — the actual UGS friend list, maintained by a
completely separate, genuinely **event-driven** pipeline (`FriendsServiceFacade.WireEvents`,
`:376-381`) — has **no reader anywhere in the UI**. So a real friend who is online but not in your
lobby shard appears nowhere, while a stranger in your lobby renders as a full row.

The Friends half is also self-erasing: the four `List_*.asset` files under `_SO_Assets/Friends Data/`
carry **no `_resetOn` key**, so they fall through to `private ResetType _resetOn = ResetType.SceneLoaded;`
(`ScriptableList.cs:21`) and `Clear()` on every `LoadSceneMode.Single` load (`:258-260`).
`FriendsServiceFacade.RefreshAsync()` has **zero call sites**, so nothing refills them.
(`List_OnlinePlayers` / `List_PartyMembers` are correctly authored `_resetOn: 1`.)

Additional dead/latched paths in the same layer: `FriendsInitializer.SetPresenceInGame` has **no
production caller** (only a reflection assertion in `PartyInviteSystemTests.cs:1164`), so UGS Friends
presence never says "In Game"; `HandleSignedOutEvent` is never wired to
`AuthenticationData.OnSignedOut`; `WireEvents` uses bare `+=` with no duplicate guard.

### RC-13 — Write amplification against a ~1 req/s budget `[A][B]`

`LobbyPropertyWriter.WriteAsync` = pre-write `lobby.RefreshAsync()` (`:113`) + `SaveWithRetryAsync`
(`:115`) which does a post-save `lobby.RefreshAsync()` (`:153`) → **3 UGS calls per property write**,
plus one more per retry (`:177`). The retry delay is a fixed `baseDelayMs` (`:176`) despite the
comment claiming exponential. Steady state: base poll 1.5 s (0.67/s) + converge query every 4 s
(0.25/s) ≈ 0.92/s — just under. The **boosted** 0.75 s interval is 1.33 reads/s, above the ~1/s cap
that `LobbyRefreshScheduler.cs:61-65`'s own comment claims to respect.

---

## 3. Remaining tasks from the docs — deduplicated and prioritized

The docs backlogs largely *predict* the above. Merged from `PresenceSystem/{BUGS,TODOS,REFACTOR}.md`,
`PartySystem/{BUGS,TODOS,REFACTOR,INVITE_ENHANCEMENTS}.md`, `MultiplayerArchitecture/ROADMAP.md`,
`NetworkDiagnostics/TODOS.md`.

### Bears on the reported symptoms

| Doc id | Item | Maps to |
|---|---|---|
| ROADMAP "Push-based invites / presence" (Med-High) | Replace property polling with lobby subscription events | RC-1 |
| PRESENCE-B6 | NRE (`WrappedLobbyService.GetLobbyAsync`) + **empty** online/request lists | RC-2, RC-6 |
| PRESENCE-B4 | Second invite not delivered; party members vanish from 3rd player's panel | RC-6, RC-2 |
| PRESENCE-B1 | `LobbyPatcher` exception spam (noise silenced; SDK defect persists) | RC-2, RC-13 |
| PRESENCE-REFACTOR-1 | Extract `LobbyMembershipMonitor`; reconnect becomes a function of `MembershipState`, not an error count | RC-3, RC-8 |
| PRESENCE-TODO-P6 | "Reconnecting…" indicator — today users see an empty panel with no explanation | RC-8 |
| PRESENCE-TODO-P3 | ±10% jitter on the base interval | RC-4, RC-13 |
| PRESENCE-TODO-P2 / PARTY-TODO-8 | Coalesce startup property writes | RC-13 |
| PARTY-B5 | Second sequential joiner fails (parties >2 not dependable) | RC-6, RC-8 |
| PARTY-D2 | Extract a shared `RefreshErrorPolicy` (classifiers + backoff) | RC-2, RC-3 |
| PARTY-D5 | Event-driven `EnsureInitializedAsync`; add a `JoiningPresenceLobby` observable state | § 4 |
| PARTY-EXIT-6/7/8 | 3-VP smoke, 5-accept stress, 4-VP concurrent invites — the standing verification gate | § 7 |

### Real backlog, unrelated to these symptoms

ROADMAP host-migration (B10 clean-reform half done; true migration open) · ROADMAP scale/cost
(shard beyond one 100-player lobby) · ROADMAP production observability (NetDiag is dev-only) ·
ROADMAP CI gate · PARTY-R1 `PartyInviteController` decomposition · PARTY-R2 `SessionRetryPolicy` ·
PARTY-R3 `NetworkTransitionService` · PARTY-TODO-5/6/7 (per-class toasts, stale-invite dismiss,
invite freshness) · PARTY-TODO-1 (remove `HostConnectionService.Instance`) · NETDIAG-TODO-2/8.

### Doc drift to fix while in here

- `HostConnectionService.cs:481-485` XML comment still describes the **retired lazy** Relay model,
  eight lines above the eager implementation. Highest-risk drift in the file — leaving it invites
  the one regression `ARCHITECTURE.md:15-21` locks against.
- Refresh cadence is stated three different ways: `PresenceSystem/ARCHITECTURE.md:152-153` says
  3-5 s, `:135` says 1.5 s/0.75 s, `TODOS.md:41` says 5 s. Real value: hardcoded 1.5 s (RC-4).
- `MULTIPLAYER_SPAWNING.md` documents `DomainAssigner` and `NetworkStatsManager` in five places;
  neither type exists in `Assets/` (already flagged by `UnifiedSystems/AUDIT.md:202-203`).
- `PARTY_SOCIAL.md:186-194`'s presence-trigger table is aspirational — `SetPresenceInGame` /
  `SetPresenceInMenu` have no production callers (RC-12).
- `PresenceSystem/TESTS.md` P5's "departure within 5 s" is unachievable for a hard kill; rewrite
  per § 6.
- `PartySystem/ARCHITECTURE.md` Q6/Q7 + error matrix still prescribe `LeavePartyKeepHostAsync`,
  recorded as deleted in `BUGS.md` B3.b.

---

## 4. Design — a `PresenceStateMachine` sibling

### 4.1 Sibling, not an extension

`PartyStateMachine` models one axis: *which Relay session am I in*
(`Disconnected → InPresenceLobby → HostingParty → InParty ⇄ Inviting/JoiningParty → Reconnecting`).
Presence is **orthogonal**: *what am I doing, and should other people see me*. Both are
simultaneously true — you are `InParty` **and** `InMatch`. Folding them means a 7×7 cross-product
and re-deriving the 14-edge legal table the invite handshake depends on
(`PartyState.cs:21-88`, `PartyStateMachine.cs:66-96`), and it would break the frozen reflection
surface at `PartyInviteSystemTests.cs:1259,1273`.

Coupling is **by observation only**: `PresenceStateMachine` subscribes to
`PartyStateMachine.OnStateChanged` — which today has zero production subscribers, this becomes its
first — and never writes it.

```
Controller/Party/StateMachine/PresenceState.cs          (new enum)
Controller/Party/StateMachine/PresenceStateMachine.cs   (new; mirrors PartyStateMachine's shape)
Controller/Party/Services/PresenceService.cs            (new; sole writer of the machine)
ScriptableObjects/SOAP/ScriptablePresenceState/         (new SOAP triple, per CLAUDE.md's 5-step recipe)
```

### 4.2 States

| State | Meaning | Published as `presenceState` |
|---|---|---|
| `Offline = 0` | Not signed in, or leave completed. Not in the lobby. | — |
| `Joining = 1` | `JoinOrCreateAsync` in flight. Local only. | — |
| `Announced = 2` | Lobby membership `Active`, identity published, **vessel does not exist yet.** UI renders the row dimmed / "connecting". | `2` |
| `Present = 3` | **The lava-lamp vessel is spawned.** This is the "I am here" broadcast the requirement asks for. | `3` |
| `InMatch = 4` | An arcade scene is live; `matchName = GameDataSO.GameMode.ToString()`. | `4` |
| `Recovering = 5` | Membership lost. Drives the `TODO-P6` "Reconnecting…" overlay. | — |
| `Departing = 6` | Terminal leave write in flight (quit / pause / sign-out). | — |

`Announced` is what fixes the "wrong information" half of symptom A: a peer is *visible but not
yet interactable* while its identity settles, instead of appearing as a fully-formed row named
"Unknown Pilot" wearing icon #1.

### 4.3 Transitions

Same `TryTransition` + legal-set shape as `PartyStateMachine`; `→ Offline` and `→ Departing` always
legal. **Unlike today, call the return value** — an `Announced → Present` rejection means the vessel
spawned while we weren't in a lobby, which is a real diagnostic.

| From → To | Trigger (exact) | On-enter | SOAP raised |
|---|---|---|---|
| `Offline → Joining` | `AuthenticationData.OnSignedIn` → `EnsureInitializedAsync` entry | — | `OnLocalPresenceStateChanged(Joining)` |
| `Joining → Announced` | `JoinOrCreateAsync` returns with `ActiveLobby != null` **and** `LobbyMembershipMonitor.State == Active` | subscribe `ISession` push; publish the identity batch as **one** `UpdatePlayer` | `…(Announced)` + existing `OnHostConnectionEstablished` |
| `Announced → Present` | `GameDataSO.OnClientReady` (raised at `ClientPlayerVesselInitializer.cs:448` for `IsLocalUser`), plus an already-fired catch-up probe `_gameData.LocalPlayer?.Vessel != null` at init — same pattern as `ToyboxController.cs:57` | publish `presenceState=3`, coalesced into the same batch as any pending identity delta | `…(Present)` |
| `Present → InMatch` | `GameDataSO.OnLaunchGame` — read the mode from `GameDataSO.GameMode`, **never from the active scene name** (this is RC-7's fix) | publish `presenceState=4` + `matchName` | `…(InMatch)` |
| `InMatch → Present` | `sceneLoaded(Menu_Main)` **and** the next `OnClientReady` | publish `presenceState=3` + `matchName=""` | `…(Present)` |
| `Announced/Present/InMatch → Recovering` | `LobbyMembershipMonitor` leaves `Active`, or `ISession.RemovedFromSession` / `Deleted` push | stop the safety poll; start rejoin backoff | `…(Recovering)` |
| `Recovering → Announced` | rejoin succeeds, monitor `Active` | re-subscribe push; republish the batch (`LivePropertySource` already preserves invites / `joined_party` / `matchName`) | `…(Announced)` + `OnOnlineRosterResynced` |
| `any → Departing` | new `OnAppQuitRequested`, `OnAppPaused(true)`, or `HandleSignedOutEvent` | bounded leave of **both** presence lobby and Relay party session | `…(Departing)` |
| `Departing → Offline` | leave completed **or** timeout | release the quit blocker | `…(Offline)` + existing `OnHostConnectionLost` |

### 4.4 The push channel

`IPresenceLobbyService.ActiveLobby` is already typed `ISession`. In `PresenceLobbyService`,
immediately after **every** `_activeLobby` assignment (join `:161`, create, converge `:229`) and
unwired wherever it is nulled (`LeaveAsync` finally, `DeleteOwnLobbyQuietlyAsync`, `ForceReset`):

```csharp
_activeLobby.PlayerJoined            += _  => _rosterDirty     = 1;
_activeLobby.PlayerLeaving           += _  => _rosterDirty     = 1;
_activeLobby.PlayerPropertiesChanged += () => _rosterDirty     = 1;
_activeLobby.Changed                 += () => _rosterDirty     = 1;
_activeLobby.RemovedFromSession      += () => _membershipDirty = 1;
_activeLobby.Deleted                 += () => _membershipDirty = 1;
```

**The callbacks touch no Unity API and raise no SOAP — they set an `int` and return.**
`HostConnectionService.Update()` (main thread, guaranteed) drains:

```csharp
if (_lobbyService.ConsumeRosterDirty()) RefreshAsync().Forget();
```

This is deliberately *not* `.AsMainThread()` — these are SDK callbacks, not `Task` continuations, so
there is nothing to await. The dirty-flag drain **is** the main-thread guarantee, and it doubles as
a per-frame coalescer: a 4-player join burst produces one diff pass, not four. It also structurally
removes the off-thread-SOAP-raise class of hazard rather than relying on the SDK's dispatch thread.

> **Verify before writing Commit 4.** `com.unity.services.multiplayer` is not vendored in this
> checkout (no `Library/PackageCache`), so the exact `ISession` member names are unconfirmed.
> `PlayerLeaving` and `Deleted` are proven present (`PartySessionService.cs:186,240`,
> `MultiplayerMiniGameControllerBase.cs:91`). If the granular events don't exist, `Changed` alone is
> sufficient — it fires on every lobby delta.

### 4.5 Rate-limit budget

Confirm the exact numbers against Unity's Lobby rate-limit doc for the installed SDK before merging;
the in-repo comments cite ~1/s reads and ~1/s writes.

| Operation | Limit | This design |
|---|---|---|
| `GetLobby` (`ISession.RefreshAsync`) | 1 / 1 s | **Safety poll only**: 1 / 10 s in menu, 1 / 30 s in game. Push does the rest. |
| `QueryLobbies` | 1 / 1 s | Converge 1 / 4 s → **1 / 30 s**, and only while `MembershipState != Active` or `Announced` age < 30 s. |
| `UpdatePlayer` | 5 / 5 s | One batched write per state change; hard-capped 1/s by the writer's own gate. |
| `CreateLobby` / `JoinLobby` | 2 / 6 s | Unchanged. |
| Lobby events (WebSocket) | not limited | The primary channel. |

Today's worst case is over budget (RC-13). After: **~0.1 reads/s steady state**, writes 3 UGS calls
→ 1. That headroom is what makes the boost window unnecessary and stops the
`refreshIntervalSeconds * 2` blackout (`:1187`) from being a routine event.

**Budget-rejected: a per-player `lastSeen` heartbeat property.** 100 members × 1 write / 15 s ≈ 6.7
`UpdatePlayer`/s fanned to 100 subscribers — precisely the delta churn that *produces* the B1/B6 SDK
stale-index defect. Do not add it.

### 4.6 Single writer, per piece of state

| State | Sole writer | Readers |
|---|---|---|
| `PresenceStateMachine.CurrentState` | `PresenceService` | UI via SOAP; `PresencePublisher` |
| `displayName`, `avatarId`, `partyCount`, `partyMax`, `matchName`, `presenceState` | `PresencePublisher` (extracted from `PublishPartyStateIfChangedAsync`), serialized through `LobbyPropertyWriter` | UGS |
| `invite_payloads`, `joined_party`, `accepted_invite` | `HostConnectionService` (unchanged — invites stay in the party layer) | UGS |
| `HostConnectionDataSO.OnlinePlayers` | `PresenceRosterProjector` (extracted from `RefreshOnlinePlayersDiff`) | both panels |
| `HostConnectionDataSO.PartyMembers` | `PartyMemberService` **only** — move HCS's three direct writes (`:743` accept, `:850` kick, `:1456` presence scan) behind `IPartyMemberService` | `ArcadeLobbyList` |
| `LobbyMembershipMonitor.State` | `LobbyMembershipMonitor` (push + safety poll) | `PresenceService` reconnect decision |
| `LocalDisplayName` / `LocalAvatarId` | `SyncLocalIdentity` — **must raise a new `OnLocalIdentityChanged`** (today a silent field write; this is RC-9(b)) | `ArcadeLobbyList` slot 0, `PresencePublisher` |
| `Player.NetAvatarId` / `NetName` / `NetDomain` | **owner / server only** — client code never writes these | via a new `GameDataSO.OnPlayerIdentityChanged` |

---

## 5. Commit plan

Tags: **[RC]** fixes a confirmed root cause · **[ARCH]** architectural improvement.
Each block is one commit with its own 3-VP MPPM smoke (`PartySystem/REFACTOR.md:153`).

### Commit 1 — Observability first **[RC-2, RC-3]** — ✅ SHIPPED

Landed as three commits:

| Commit | What |
|---|---|
| `44587a2f` | Both empty benign branches now increment per-read-path counters (`_benignPresenceSkips` / `_benignPartySessionSkips`, exposed as `BenignPresenceSkips` / `BenignPartySessionSkips`) and emit one throttled `CSDebug.Log` per 10 s carrying both running counts + `ClassifyException` + `GetSnapshot`. `CSDebug`, not `Debug.LogWarning` — it compiles out in release, so observability returns without B1's spam. |
| `11559a93` | New `UgsErrorClassifier.IsRateLimit` — one chain-walking classifier replacing three divergent private copies (`HostConnectionService` and `PresenceLobbyService` matched an outer-message substring; `PartySessionService` pattern-matched only an outer `RequestFailedException`). Rate-limit branch moved **above** the benign branches at both catch sites. Stale `IsBenignSdkStaleIndexError` class doc corrected. |
| `92ec00f7` | `LogNetDiag(operation, e)` on all seven `PresenceLobbyService` catches (four had none, including both halves of `ConvergeToCanonicalAsync`), with an operation tag so a log line says *which* lobby op failed. |

> **Deviation from this plan, decided on re-read of the source.** This section
> originally called for reordering both catches to
> `transition → definite → rate-limit → benign → generic`. **`[definite]` was
> deliberately left below `[benign]`.** Structured definite errors
> (`SessionNotFound` / `SessionDeleted` / `NotInLobby`) carry a specific
> `SessionError` and therefore never match `IsBenignSdkStaleIndexError`'s
> `Error == Unknown` discriminator — they already reach `[definite]` correctly
> under the existing order. The *only* input the current order sends to benign
> instead is a `SessionException` the SDK itself could not classify whose message
> merely reads like `"session … not found"`, and `IsDefiniteSessionGoneException`
> has a message-substring fallback that would catch it. Promoting `[definite]`
> would route that ambiguous case into `HandleDefiniteSessionGoneAsync` — which
> recreates the solo session and kicks any client mid-join (the hazard
> `SESSION_CREATION_GRACE_PERIOD_SECONDS` exists to prevent). "Retry next tick" is
> the safe reading of an error the SDK could not name. Rationale is recorded at
> the branch and in the classifier doc; **do not re-derive this.**

Only the rate-limit reorder shipped. Everything else in this commit is counters
and editor-only logging — classification, control flow and recovery are untouched.

### Commit 2 — Kill the decoy interval; jitter; wall-clock accumulator **[RC-4, RC-13]** — ✅ SHIPPED

Landed as three commits:

| Commit | What |
|---|---|
| `09381def` | Decoupled the two incidental users of `refreshIntervalSeconds` — the rate-limit backoff (`* 2`, two sites) and the post-session settle — into `RATE_LIMIT_BACKOFF_SECONDS = 6f` / `POST_SESSION_SETTLE_SECONDS = 3f`, both preserving the shipped prefab's effective values. |
| `084dce0b` | `LobbyRefreshScheduler.DefaultInterval` assigned from `HostConnectionService.Start`; prefab `3 → 1.5` so the **effective cadence is unchanged**; `AppManager`'s factory comment now says its argument is a placeholder `Start` overwrites. |
| `6a3a37a5` | `ShouldFireNow(dt)` split into `Accumulate(dt)` (unconditional, above the gates) + `TryConsumeFire()` (below them) so the timer measures wall time; ±10% per-fire jitter (`TODO-P3`); `BOOSTED_INTERVAL_SECONDS` `0.75f → 1.1f`. |

The first two are behaviour-preserving; `6a3a37a5` is the only real change.

**Cadence deliberately held at 1.5 s, not raised to the prefab's 3.** Wiring the
field while keeping 3 would have halved the poll rate and made staleness
measurably worse with nothing yet compensating — push does not exist until
Commit 4. The field becomes the safety-poll knob (10 s menu / 30 s in game) there.

**Ordering mattered:** the decouple (`09381def`) had to land *before* the wiring
(`084dce0b`). The other way round, correcting the prefab value would have
silently halved the rate-limit backoff and shortened the session settle as a side
effect of an unrelated fix.

Side effect worth knowing: the accumulator now also runs during boot, so the first
online-list population fires immediately on lobby join instead of one interval
later. Session creation still gets its settle window via `ResetDeferred`.

> **Deviation: the `LobbyPropertyWriter` edits move to Commit 4.** This section
> also called for deleting the pre-write and post-save `lobby.RefreshAsync()`
> calls (3 UGS calls per write → 1). The stated justification was *"the push
> channel delivers our own delta back"* — **not true until Commit 4 lands**. Both
> refreshes have documented reasons that still hold: the pre-write one guards a
> stale SDK player-index that makes `SaveCurrentPlayerDataAsync` **fail silently**
> (`LobbyPropertyWriter.cs:110-113`), and the post-save one is documented as
> reducing the stale-delta window that is B1's root cause (`:146-153`). Deleting
> them now would likely make B1 *worse* with nothing compensating. The
> exponential-retry fix (the `when` filter waits a fixed `baseDelayMs` despite the
> comment at `:163-165` claiming exponential) rides along, being the same fragile
> write path `TODO-P2` flags as high-risk. **Do not re-derive this — it moves with
> the push channel or not at all.**

### Commit 3 — Explicit leave on quit / pause **[RC-5]**

- `ApplicationLifecycleManager` — `Application.wantsToQuit += HandleWantsToQuit` in `Awake`; return
  `false` once, raise a new `ApplicationLifecycleEventsContainerSO.OnAppQuitRequested`, start a
  1500 ms drain, then `Application.Quit()` and return `true` on re-entry. Existing `OnAppQuitting`
  semantics unchanged.
- `HostConnectionService` — subscribe `OnAppQuitRequested` **and** `OnAppPaused` in `Start`;
  → `PresenceService.EnterDeparting()` → bounded `UniTask.WhenAny(leave, Delay(1200))` over
  `_lobbyService.LeaveAsync()` **and** `_partySessionService.LeaveAsync()` (no quit path leaves the
  Relay session today). On mobile, `OnAppPaused(true)` is the only hook that ever runs — leave there
  and rejoin on `OnAppPaused(false)`.
- `OnDestroy` — keep the leave as a backstop, guarded by `CurrentState != Offline` so the quit path
  doesn't double-leave.

### Commit 4 — Push channel + membership monitor **[RC-1; ARCH]**

- `PresenceLobbyService` — `WireSessionEvents` / `UnwireSessionEvents` per § 4.4; expose
  `ConsumeRosterDirty()` / `ConsumeMembershipDirty()`.
- New `LobbyMembershipMonitor` — `{ Active, StaleReference, RemovedFromLobby, LobbyDeleted }`, fed by
  push + refresh outcomes. **Replaces `MAX_REFRESH_ERRORS_BEFORE_RECONNECT` as the reconnect
  trigger** (`PresenceSystem/REFACTOR.md`'s extraction, now unblocked by Commit 1's data).
- `HostConnectionService.Update()` — fire on `ConsumeRosterDirty()` **or** safety-poll elapsed
  (10 s menu / 30 s game).
- `:1211-1221` — reconnect reads `MembershipState`, not the error counter. Add the missing
  `Reconnecting → InPresenceLobby` / `→ HostingParty` exits so `Reconnecting` stops being terminal,
  and add `(Disconnected, HostingParty)` / `(Disconnected, InParty)` to `LegalTransitions` so the
  boot-status retry path stops performing two rejected transitions.

### Commit 5 — `PresenceStateMachine` + the vessel-spawn broadcast **[RC-7; the requirement]**

- New `PresenceState.cs` (static numeric values, per CLAUDE.md), `PresenceStateMachine.cs` (copy
  `PartyStateMachine`'s shape verbatim), `SOAP/ScriptablePresenceState/` triple.
- New `PresenceService.cs` — owns the machine; subscribes `OnClientReady` → `Present`,
  `OnLaunchGame` → `InMatch`, `sceneLoaded(Menu_Main)` → arm `Present`,
  `PartyStateMachine.OnStateChanged` (observe only); catch-up probe at init. Registered lazily in
  `AppManager.InstallBindings` beside the other party services.
- `HostConnectionService.cs:1864-1870` — **delete `if (IsOnMenuScene()) return string.Empty;`**.
  Return `_presence.CurrentState == PresenceState.InMatch ? _gameData.GameMode.ToString() : string.Empty`.
  The state machine, not the scene name, is the authority. Revives the dead `InMatch` branch at
  `FriendsListPanel.cs:365`.
- `:1822-1863` — `presenceState` joins the same batched `SaveWithRetryAsync` (still one
  `UpdatePlayer`) with a `_publishedPresenceState` tracker in the change gate.
- `:1392-1416` — `ReadOnlinePlayerData` parses `presenceState`; `PartyPlayerData` gains
  `int PresenceState` (keep `Equals`/`GetHashCode` on `playerId` only).
- `:377-388` — **remove `if (!IsOnMenuScene()) return;`**. In a game scene: 30 s cadence,
  publish-only, no invite scan. The presence layer must keep running there or `matchName` can never
  be maintained.
- `FriendsInitializer` — subscribe `OnLocalPresenceStateChanged`; call the existing-but-dead
  `SetPresenceInGame(...)` on `InMatch` and `SetPresenceInMenu()` on `Present`. Wire
  `HandleSignedOutEvent` to `AuthenticationData.OnSignedOut` (raised at
  `AuthenticationServiceFacade.cs:309`, currently routed nowhere). Add a duplicate guard to
  `WireEvents`.
- Fix the stale lazy-Relay XML comment at `:481-485` in the same commit.

### Commit 6 — Tombstones; stop the reconnect wiping the party **[RC-6, RC-8]**

- `:1367-1371` — replace the unconditional `RemoveAt` with a `Dictionary<string,int> _missedReads`:
  ≥1 miss → `OnOnlinePlayerUpdated` with `Liveness = Unconfirmed` (row dimmed, invite disabled,
  **kept**); ≥3 misses **or** an explicit `PlayerLeaving` push → `RemoveAt` + `OnOnlinePlayerLeft`.
  Reset on every sighting. Move the departed-invite cleanup (`:1385`) behind the same threshold so a
  transient read can't cancel a live invite.
- `PartyMemberService.cs:157-168` — same two-strike rule before `RemoveAt` + `RaisePartyMemberLeft`.
  Skip the strike when the removal came from a `PlayerLeaving` push or `ReconcilePartyMembersNow`.
- `:1723-1734` — `ApplyPostLobbyJoinState` calls
  `_memberService.SeedLocalPlayer(clearFirst: _partySessionService.ActiveSession == null)`.
  A presence-layer reconnect must never clear a live party roster.
- `PartySessionService.cs:239-240` — set `CreatedAtUnscaledTime` in `JoinByIdAsync` too, so the 4 s
  grace gate is symmetric.
- `MultiplayerSetup.cs:182` — on the host's `OnClientDisconnect`, also mark that peer `Unconfirmed`
  in `OnlinePlayers` (the only sub-reap signal available for a party peer). Lower
  `UnityTransport.DisconnectTimeoutMS` to **10000** — not 5000; mobile radios stall.

### Commit 7 — UI binding **[RC-9; ARCH]**

- New `UI/Interfaces/IModalPanel.cs` — `void OnModalOpened(); void OnModalClosed();`.
  `ModalWindowManager` caches `GetComponentsInChildren<IModalPanel>(true)` in `Awake` and dispatches
  from `ModalWindowIn()` / `ModalWindowOut()`. This is parent→child dispatch inside one prefab
  hierarchy — the same shape as `ScreenSwitcher`/`IScreen.OnScreenEnter` — not cross-system
  communication, so it does not violate the SOAP rule.
- `ArcadeLobbyList` — implement `IModalPanel`; move the `SubscribeSoap + PopulateAll +
  ForceRefreshNow` body into `OnModalOpened()`. Keep `OnEnable`/`OnDisable` delegating to the same
  (idempotent) methods. `HandlePartyChanged` / `HandlePartyCleared` must call `UpdateOnlineStatus()`.
  Subscribe `OnHostConnectionEstablished/Lost` → `UpdateOnlineStatus()`, and the new
  `OnLocalIdentityChanged` → `PopulateLocalSlot(slots[0])`.
- Replace the single-shot `PlayerDataService.Instance` / `HostConnectionService.Instance` captures
  (`ArcadeLobbyList.cs:153`, `FriendsListPanel.cs:161`) with the deferred-bind pattern from
  `ProfileImage.TryBind` — attempt in `OnEnable` **and** `Start`, guarded by `_subscribed`.
- New roster SOAP channels on `HostConnectionDataSO` (there is no online joined/left event today —
  panels infer it from `ScriptableList` item events, which is why an in-place field change arrives
  as destroy-then-instantiate):
  `OnOnlinePlayerJoined` / `OnOnlinePlayerLeft` / `OnOnlinePlayerUpdated` /
  `OnOnlineRosterResynced` / `OnLocalPresenceStateChanged` / `OnLocalIdentityChanged`.
  Author the assets and wire them — **no null guards**.
- `FriendsListPanel` — subscribe those instead of the raw item events. `Updated` →
  `PopulateOnlineEntry(existingRow, player)` **in place**: no `Destroy`, no `Instantiate`, no sibling
  reorder. `HandlePartyMemberChanged` repaints only the affected row instead of rebuilding the
  section (removes the double rebuild per kick, since `RemovePartyMember` raises Kicked **and**
  Left). Drop the redundant second `PopulateAll()` in `Show()`. Pool rows instead of
  `Destroy`/`Instantiate`.
- Both panels — render a "Reconnecting…" overlay while `Recovering`. Closes `TODO-P6`: today an
  empty panel is indistinguishable from a reconnect.

### Commit 8 — Profile icons **[RC-10, RC-11; ARCH]**

- `SO_DefaultProfileIcons.asset` — insert an `Id: 0`, `Name: "Unknown"` entry with a **visually
  distinct** sprite. This alone converts every silent icon bug into a visible one.
- New `Utility/ProfileIconResolver.cs` (pure C#, DI singleton): dictionary cache + `TryResolve` /
  `Resolve` / `Unknown`. Collapse all **seven** local scans onto it
  (`FriendsListPanel:740`, `ArcadeLobbyList:401`, `PartyInviteNotificationPanel:258`,
  `ArcadeProfileWidget:84`, `PlayerDataService:319`, `MiniGameHUD:529`, `TournamentSceneView:501`).
  `PartyInviteNotificationPanel.cs:156-159` assigns unconditionally — a miss now shows "Unknown",
  never the previous inviter's face.
- `PlayerDataService.GetDefaultAvatarId()` (`:229-235`) — return the first icon with `Id > 0`;
  `ProfileIconSelectView.BuildAvatarGrid` skips `Id <= 0` (it is a sentinel, not a choice).
- `GameDataSO` — add `OnPlayerIdentityChanged(ulong netObjId)`. `Player.OnNetAvatarIdChanged`
  (`:552`) and the `NetName` callback raise it alongside the existing mirror writes.
- `ArcadeGameConfigureModal` — subscribe `OnPlayerIdentityChanged` in `SpawnChipsForAllPlayers`,
  re-resolve that chip's sprite on raise, unsubscribe in `DespawnAllChips`. `CloseAndNotifyClients()`
  (`:1204-1221`) must call `DespawnAllChips()` — the client path already does at `:1477`; the host
  path leaks the per-`Player` `NetDomain` handlers.
- `ProfileIconSelectView` — implement `IModalPanel`, build the grid in `OnModalOpened()`, subscribe
  `OnProfileChanged` to rebuild the highlight. When `!dataService.IsInitialized` (`:239-259`) either
  actually cache the write and raise `OnProfileChanged`, or refuse the selection and leave the button
  unlatched — silently latching a selection that never persists is the worst of the three.

### Commit 9+ — deferred **[ARCH]**, gated on 1–8 landing green

`RefreshErrorPolicy` extraction (`D2`) · `PresenceService` absorbs `RefreshOnlinePlayersDiff` +
`ReadOnlinePlayerData` + `PublishPartyStateIfChangedAsync`, shrinking `HostConnectionService` from
2228 lines · lobby property-key constants into one shared static (duplicated across 4 classes today) ·
Friends lists `_resetOn: 1` + a `FriendsServiceFacade.RefreshAsync()` caller + fix the init latch
(`FriendsInitializer` sets `_initialized = true` after a swallowed facade failure) · route
`PartyInviteController`'s direct SOAP raises through `SoapPartyEventBus` (which claims sole
ownership) · `R1` `PartyInviteController` decomposition.

---

## 6. Fast removal — what is actually achievable

Three cases. Be honest about each in the UI rather than pretending they are one.

**(a) Graceful exit** — quit button, sign-out, alt-F4 on desktop, backgrounding on mobile.
Fully under our control. Explicit `LeaveAsync()` → UGS fans `PlayerLeaving` / `Changed` to every
subscriber over the WebSocket. **Sub-second.** Today this path is `async void OnDestroy` awaiting
after teardown began — best-effort at best, usually doesn't complete. Commit 3 + Commit 4 fix it.

**(b) A party peer hard-drops** — crash, cable pull, force-kill, *while in your party*.
We have an out-of-band signal: the NGO/Relay transport. `UnityTransport.DisconnectTimeoutMS`
(default 30000) + `HeartbeatTimeoutMS` govern `OnClientDisconnectCallback`, already routed to
`ReconcilePartyMembersNow()` (host) and `HandleHostLossAsync` (client) via `MultiplayerSetup.cs:182,187`.
Lowering to 10 s gives **~10 s**, and Commit 6 extends it to mark that peer `Unconfirmed` in the
online list too.

**(c) A non-party peer hard-drops.** There is **no transport** between arbitrary lobby members and
no client-side signal of any kind. The floor is the UGS Lobby service's own disconnect reap —
service-side, not tunable by us; the in-repo estimate is ~30 s
(`HostConnectionService.cs:419-420`). When the service does reap, the push channel tells us
**instantly** — which is the improvement: today we wait a poll tick *on top of* the reap. **Nothing
gets below the reap window for this case.** Do not promise otherwise.

**(d) Therefore: tombstones, not instant eviction.** Because (c) exists, the honest UI is a
three-value liveness per row:

```
Live         → present in the current snapshot
Unconfirmed  → missing from ≥1 consecutive snapshots, or transport-dropped party peer
               → row dimmed, invite disabled, KEPT in the list
Gone         → missing from 3 consecutive snapshots, or an explicit PlayerLeaving push
               → removed
```

This kills RC-6 in both directions: a single short read can no longer wipe the list (the B4/B6
empty-list symptom), and a hard-killed player looks visibly *degraded* rather than silently
correct-looking for 30 s.

**`PresenceSystem/TESTS.md` P5 must be rewritten** — its "departure within 5 s" criterion is
unachievable for an editor Stop. Split it: **≤1 s graceful exit, ≤35 s hard kill.**

---

## 7. Verification gate

Nothing above is closable without it. Per-commit: 3-VP MPPM smoke (accept / decline / leave /
second accept after leave) with **uniquely tagged** virtual players — untagged clones share one UGS
`PlayerId` and reproduce empty/asymmetric online lists on their own, which is why the historical
B4/B5 repros are flagged invalid.

After Commit 6, re-run `B1-RETEST`, `B4-RETEST`, `B5`, `SRT-18`, `SRT-19`, and exit criteria 6-8.
After the `DisconnectTimeoutMS` change, re-run the full `B10` host-loss checklist (menu 2-VP, game
mode, 3-4 VP, graceful return, hard drop, unrestricted-after-recovery) — `PartySystem/BUGS.md:789`
requires it on any recovery-path change.

New acceptance criteria for this work:

| # | Criterion |
|---|---|
| PS-1 | A second client entering Menu_Main appears in the first client's `FriendsListPanel` **within 1 s** of its lava-lamp vessel spawning, with the correct name and icon on first paint (never "Unknown Pilot"/icon 1 then corrected). |
| PS-2 | A client quitting gracefully disappears from every peer's list **within 1 s**. |
| PS-3 | A hard-killed client is marked `Unconfirmed` within 2 poll windows and removed on the UGS reap; it is never silently rendered as `Live`. |
| PS-4 | A client launching an arcade game renders as `IN MATCH` and non-invitable on every peer within 2 s, and reverts on return to the menu. |
| PS-5 | Opening the arcade panel triggers a roster re-read every time, not once per scene load. |
| PS-6 | A presence-lobby reconnect does not empty `ArcadeLobbyList` while the Relay party session is alive. |
| PS-7 | With `NetAvatarId` forced to 0, every surface shows the distinct "Unknown" icon — never icon #1. |
| PS-8 | Steady-state UGS reads ≤ 0.2 req/s/client, measured over a 2-minute idle menu session. |

---

## 8. What NOT to do

**Locked designs.**

- **Do not reintroduce lazy / on-first-invite Relay creation** (`PartySystem/ARCHITECTURE.md:15-21`).
  `PresenceStateMachine` is a *sibling* precisely so it can never be mistaken for a reason to defer
  session creation. Fix the stale lazy-model XML comment at `HostConnectionService.cs:481-485` in
  Commit 5 — leaving it invites exactly this regression.
- **Do not write `Player.NetDomain` / `NetAvatarId` / `NetName` from client code**
  (`PartySystem/BUGS.md` B9). The icon fix observes only.
- **Do not add if-null guards on new SOAP serialized fields.** Author the assets; fail loud.
- **Do not remove `HostConnectionService.Instance`, rename `_transitioning` / `IsTransitioning`, or
  move `ParseInviteLine`** — `PartyInviteSystemTests` reflects on all three and
  `PartySystem/REFACTOR.md:37` freezes the names until `R1` migrates the tests.
- **Do not weaken the two YS2 guards** (`:1073-1077` entry, `:1204` in-catch) — they are the fix for
  the false-`ForceReset`-during-transition bug (`a1a8eb9`). The membership-monitor rewrite keeps both.
- **Do not `Clear()` + re-`Add()` SOAP lists for roster sync.** `ScriptableList<T>.Clear()` raises
  only `OnCleared`, never per-item `OnItemRemoved`. HCS's own comment at `:1113` states the rule.

**Backlog items to reject as written.**

- **`PRESENCE-TODO-P7` — "gate `RefreshOnlinePlayersDiff` on panel visibility."** Landing this
  *manufactures* the reported symptom: the list would only update while a panel is open, and
  `ArcadeLobbyList` never receives an enable event at all (RC-9).
  `INVITE_ENHANCEMENTS.md:386` already warns "Do NOT stop the poll when closed" — invite detection,
  member sync, the accept handshake and the joined-member scan all ride the same `RefreshAsync`.
  **Close as won't-do**; the push channel supersedes its motivation.
- **A per-player `lastSeen` heartbeat** — rejected on budget, see § 4.5.
- **Polling below 1 s.** `GetLobby` is 1 req/s; `BOOSTED_INTERVAL_SECONDS = 0.75f` already breaches
  it. Commit 2 corrects it *upward*.
- **Broadening `IsBenignSdkStaleIndexError`.** It already swallows any `SessionException` with
  `Error == Unknown`. It must move *after* the definite and rate-limit branches, never earlier, and
  never widen.

**Claims examined and NOT carried forward** (recorded so they aren't re-derived):

- *"UGS Friends SDK push callbacks raise SOAP list events off-thread and abort mid-rebuild."*
  The static structure is real — `OnPresenceUpdated` / `OnRelationshipAdded` / `OnRelationshipDeleted`
  (`FriendsServiceFacade.cs:392,405,416`) call `SyncAllRelationships()` with no dispatcher hop, and
  it `Clear()`s before refilling. But the SDK's dispatch thread is **unverified** (package not
  vendored), so the crash is unproven. Treat as hardening, not a root cause. The dirty-flag pattern
  in § 4.4 makes the presence layer immune to this class regardless.
- *"`IsPartyHost` persisting as `1` in `HostConnectionData.asset` drives the symptom."* Both runtime
  writes are immediately followed by `PartyMembers` mutations that *do* raise events, so
  `PopulateSlots` re-reads in the same frame. Cosmetic at boot only.
- *"Whichever `PartyMembers` writer lands first pins the rendered identity."* False —
  `SyncFromSession` (`PartyMemberService.cs:118-152`) locates the row by `PlayerId` and does
  RemoveAt+Insert on any `DisplayName`/`AvatarId` mismatch **every tick**, so the party-session copy
  unconditionally overwrites the presence scan and the invite payload on the first successful poll.
  The surviving true statement is narrower: the 4-slot panel is fed *exclusively* from the
  party-session property store.
- *"Destroy-then-Instantiate produces a visible duplicate frame."* No — `Destroy` is deferred to the
  end of the update phase, which runs before the Canvas layout rebuild. The real artifacts are the
  reorder-to-bottom on an unrelated field change and the churn itself.
- *"The host's 4 s post-create grace freezes a roster."* Largely vacuous — a freshly-minted session
  contains only the local player. The narrow real exposure is a joiner accepting within 4 s of host
  session creation.

**Regression hazards this plan creates.**

- Removing the `IsOnMenuScene()` gate runs the refresh loop in game scenes for the first time.
  Publish-only there: **no** invite scan, **no** `ExpireOutgoingInvites` change without re-running
  S1-S4, 30 s cadence so it cannot compete with the frame budget or the arcade scene's own UGS traffic.
- Two-strike tombstones delay a *legitimate* removal by one extra poll in the non-push case.
  Acceptable: push makes the common case instant, and the alternative is today's mass false eviction.
- Lowering `DisconnectTimeoutMS` increases false disconnects on flaky mobile radios. 10 s, not 5 s.
- Adding legal `PartyStateMachine` edges changes the transition table — add the matching cases to
  `PartyInviteSystemTests` in the same commit.
- The `ISession` event member names are **unverified** in this checkout. Confirm before Commit 4;
  fall back to `Changed` alone.

---

## Related

- `ARCHITECTURE.md` · `BUGS.md` · `REFACTOR.md` · `TESTS.md` · `TODOS.md` (this folder)
- `../PartySystem/ARCHITECTURE.md` — locked eager-Relay design, error matrix, exit criteria
- `../MultiplayerArchitecture/ROADMAP.md` — this plan executes its "Push-based invites / presence" item
- `../NetworkDiagnostics/ARCHITECTURE.md` — the overlay every commit ships behind
- `../THREADING.md` — why § 4.4's callbacks set flags instead of raising SOAP

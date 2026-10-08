# Branch archive: `claude/host-connection-refresh-error-85c6a2`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-03 by Claude
- **Unmerged commits:** 1
- **Forked from:** `265691fb1` (2026-08-03, Merge pull request #652 from froglet-studio/claude/dolphin-explosion-vector-uv)
- **Tip:** `03f6ad754`
- **Files touched (8):**
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/Controller/Party/Interfaces/IPresenceLobbyService.cs`
  - `Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs`
  - `Assets/_Scripts/Controller/Party/StateMachine/PartyStateMachine.cs`
  - `Assets/_Scripts/Tests/EditMode/PartyInviteSystemTests.cs`
  - `Docs/PresenceSystem/ARCHITECTURE.md`
  - `Docs/PresenceSystem/BUGS.md`
  - `Docs/PresenceSystem/TESTS.md`

### `03f6ad754` — fix(party): stop presence converge releasing the lobby it just joined

_Claude, 2026-08-03 20:26:18 +0000_

```text
ConvergeToCanonicalAsync read _activeLobby AFTER its join await and
released whatever it pointed at. Any concurrent membership flow moving
_activeLobby onto the canonical lobby during that await made the cleanup
leave - or, as host, DeleteAsync() - the lobby just joined, then install
a handle to it. From then on ISession.RefreshAsync() threw
`SessionException [Error: NotInLobby]` every tick, forever; when the
released lobby was the shared canonical one, every other client was
evicted too.

The concurrency was reachable: the reconnect tail of
HostConnectionService.RefreshAsync calls JoinOrCreateAsync after
releasing the lobby mutex, CreateAsync publishes _activeLobby before its
1.5s settle delay, and Update then fires a refresh tick whose own
converge (under the mutex) races the un-mutexed one. A deterministic
variant needed no concurrency at all: an SDK cache hit returning the
session already held made the old code leave it and re-install it.

PresenceLobbyService:
- ConvergeToCanonicalAsync split into a public guard +
  ConvergeToCanonicalCoreAsync. The core captures the outgoing session
  before the join await, re-validates after it, installs the new
  reference before releasing, and releases the captured reference -
  never a post-await re-read. If _activeLobby moved during the join, it
  releases the handle it opened instead. Same-session returns install
  and stop.
- DeleteOwnLobbyQuietlyAsync -> ReleaseQuietlyAsync(session, reason):
  explicit target, never touches _activeLobby, hard-refuses to release
  the active lobby. Removes the bug class, not just the instance.
- _membershipBusy serialises join/create against converge. Service-local
  rather than the lobby mutex, which one call path holds and the other
  does not - reusing it would deadlock.

HostConnectionService:
- Presence NotInLobby / SessionNotFound / SessionDeleted / 404 is now
  classified [definite] and rejoins on the first occurrence, matching
  RefreshPartyMembersAsync. It previously fell to the generic branch and
  logged the full exception 3x while discovery stayed dead.
- [transition] guard hoisted above [definite]: a deliberate leave mid
  accept/leave surfaces as NotInLobby on the old handle.
- RejoinPresenceLobbyAsync: single-flight, geometric backoff
  (2/4/8/16/30s, cleared by one clean refresh) so a persistently failing
  lobby cannot spin ForceReset -> JoinOrCreate and abandon a UGS lobby
  per cycle. Update watchdog on the same ladder re-arms a lobby-less
  service, which previously stalled silently until app restart.
- Exits Reconnecting on a successful rejoin when the Relay session is
  still live. Nothing exited that state before, stranding the machine
  where the next invite is an illegal transition.

PartyStateMachine: add InPresenceLobby -> Reconnecting and
HostingParty -> Reconnecting. Presence loss is detected by a refresh
tick that runs in every state, so recovery from those two logged
"Illegal transition" instead of recovering. Repeat losses stay absorbed
by EnterReconnecting's guard - no self-transition edge.

Tests: three state-machine edge tests. Docs: PresenceSystem B13 +
ARCHITECTURE membership-mutation rules and presence-loss recovery +
TESTS P8 MPPM regression.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs            | 182 +++++++++++++++++++++++++++++----
 Assets/_Scripts/Controller/Party/Interfaces/IPresenceLobbyService.cs |  32 +++++-
 Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs    | 146 ++++++++++++++++++++++----
 Assets/_Scripts/Controller/Party/StateMachine/PartyStateMachine.cs   |  10 ++
 Assets/_Scripts/Tests/EditMode/PartyInviteSystemTests.cs             |  51 +++++++++
 Docs/PresenceSystem/ARCHITECTURE.md                                  |  64 +++++++++++-
 Docs/PresenceSystem/BUGS.md                                          | 106 +++++++++++++++++++
 Docs/PresenceSystem/TESTS.md                                         |  35 +++++++
 8 files changed, 579 insertions(+), 47 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 861 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 51407ae38..c63330854 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -107,6 +107,19 @@ namespace CosmicShore.Gameplay
         /// </summary>
         private const float PRESENCE_CONVERGE_INTERVAL_SECONDS = 4f;
 
+        /// <summary>
+        /// Backoff schedule for presence-lobby reconnects. A lobby that fails
+        /// again immediately after a rejoin (UGS outage, auth lapse, sustained
+        /// throttling) would otherwise spin ForceReset → JoinOrCreate → fail
+        /// every few seconds indefinitely, minting and abandoning a UGS lobby per
+        /// cycle and hammering the console with the same exception. The gap
+        /// doubles per consecutive attempt from BASE up to MAX; one clean refresh
+        /// resets it to zero. See Docs/PresenceSystem/BUGS.md B13.
+        /// </summary>
+        private const float PRESENCE_RECONNECT_BASE_BACKOFF_SECONDS = 2f;
+        private const float PRESENCE_RECONNECT_MAX_BACKOFF_SECONDS  = 30f;
+        private const int   PRESENCE_RECONNECT_MAX_BACKOFF_SHIFTS   = 4;   // 2,4,8,16,32→capped
+
         /// <summary>
         /// After session creation, suppress <see cref="RefreshPartyMembersAsync"/>
         /// for this many seconds.  A freshly-provisioned session can transiently
@@ -200,6 +213,9 @@ namespace CosmicShore.Gameplay
         private float  _nextForcedRefreshAllowed;
         private float  _nextConvergeAllowed;
         private int    _consecutiveRefreshErrors;
+        private float  _presenceReconnectBackoffUntil;
+        private int    _presenceReconnectAttempts;
+        private bool   _presenceRejoinInFlight;
         private int    _publishedPartyCount = -1;
         private string _publishedMatchName  = "<UNSET>";
         // Identity (displayName/avatarId) rides the same change-gated per-tick
@@ -376,10 +392,28 @@ namespace CosmicShore.Gameplay
 
         void Update()
         {
-            if (!IsInPresenceLobby) return;
+            if (!IsInitialized) return;
+            if (!IsOnMenuScene()) return;
+            if (Time.unscaledTime < _presenceReconnectBackoffUntil) return;
+
+            // Watchdog: initialized but holding no presence lobby. Every path
+            // that can land here is a failure - a reconnect whose rejoin was
+            // skipped or threw, or a CreateAsync that swallowed its error and
+            // left the reference null. Without this re-arm the service is
+            // permanently silent (the old guard returned on ActiveLobby == null
+            // and nothing else drives a rejoin), so discovery, invites and the
+            // party panel stay dead until the app restarts.
+            if (_lobbyService.ActiveLobby == null)
+            {
+                // _joining: EnsureInitializedAsync owns the first join and holds
+                // a null ActiveLobby across it - never race it.
+                if (!_joining)
+                    RejoinPresenceLobbyAsync("watchdog").Forget();
+                return;
+            }
+
             if (_lobbyMutex.CurrentCount == 0) return;                   // someone is already inside the mutex
             if (Time.unscaledTime < _rateLimitBackoffUntil) return;
-            if (!IsOnMenuScene()) return;
 
             ExpireOutgoingInvites();
 
@@ -387,6 +421,84 @@ namespace CosmicShore.Gameplay
                 RefreshAsync().Forget();
         }
 
+        /// <summary>
+        /// Enters <see cref="PartyState.Reconnecting"/> for a presence-lobby
+        /// loss, absorbing the repeat case. A definite loss now recovers on
+        /// every occurrence, so a lobby that fails again right after a rejoin
+        /// would otherwise ask for Reconnecting → Reconnecting - not a legal
+        /// edge (no state self-transitions) - and trade the old NotInLobby
+        /// warning storm for an "Illegal transition" one.
+        /// </summary>
+        private void EnterReconnecting()
+        {
+            if (_stateMachine.CurrentState == PartyState.Reconnecting) return;
+            _stateMachine.TryTransition(PartyState.Reconnecting);
+        }
+
+        /// <summary>
+        /// Re-establishes the presence lobby after a loss, with geometric
+        /// backoff so a persistent failure cannot spin lobby create/join
+        /// forever. Single-flight: concurrent callers collapse to one attempt.
+        /// The backoff window is armed BEFORE the attempt (so a throw still
+        /// spaces the next one) and re-armed from completion.
+        /// </summary>
+        private async UniTask RejoinPresenceLobbyAsync(string reason)
+        {
+            if (_presenceRejoinInFlight) return;
+
+            // Sign-out sets Disconnected BEFORE leaving the lobby, so this also
+            // stops a rejoin racing a sign-out into re-creating a lobby the user
+            // just left.
+            if (!IsInitialized) return;
+
+            _presenceRejoinInFlight = true;
+
+            _presenceReconnectAttempts++;
+            float backoff = Mathf.Min(
+                PRESENCE_RECONNECT_BASE_BACKOFF_SECONDS *
+                    (1 << Mathf.Min(_presenceReconnectAttempts - 1, PRESENCE_RECONNECT_MAX_BACKOFF_SHIFTS)),
+                PRESENCE_RECONNECT_MAX_BACKOFF_SECONDS);
+            _presenceReconnectBackoffUntil = Time.unscaledTime + backoff;
+
+            Debug.LogWarning(
+                $"[HostConnectionService] Rejoining presence lobby ({reason}, attempt {_presenceReconnectAttempts}, " +
+                $"next retry gate {backoff:F0}s).");
+            try
+            {
+                await _lobbyService.JoinOrCreateAsync(presenceLobbyMaxPlayers);
+                ApplyPostLobbyJoinState();
+
+                // Leaving Reconnecting is NOT optional. Nothing else in the
+                // service exits that state, and its only legal outgoing edges
+                // are → HostingParty / → InParty - so a client stranded there
+                // shows "reconnecting…" forever and its next invite asks for the
+                // illegal Reconnecting → Inviting. The Relay session is
+                // independent of the presence lobby and is normally untouched by
+                // a presence loss, so restore InParty when it is still live.
+                // When it is NOT, stay put deliberately: recreating Relay from
+                // this background loop would shut down the NetworkManager and
+                // respawn every menu vessel, which is why that recovery belongs
+                // to the user-tapped boot-status retry
+                // (→ EnsurePartySessionAsync → HostingParty → InParty).
+                if (_lobbyService.ActiveLobby != null &&
+                    _stateMachine.CurrentState == PartyState.Reconnecting &&
+                    _partySessionService.ActiveSession != null)
+                    _stateMachine.TryTransition(PartyState.InParty);
+            }
+            catch (Exception e)
+            {
+                Debug.LogWarning($"[HostConnectionService] Presence rejoin failed ({e.GetType().Name}): {e.Message}");
+                CSDebug.Log($"[HostConnectionService] NetDiag: class={NetworkDiagnostics.ClassifyException(e)} | {NetworkDiagnostics.GetSnapshot()}");
+            }
+            finally
+            {
+                // Measure the gate from completion, not from dispatch - a slow
+                // join/create must not consume its own backoff window.
+                _presenceReconnectBackoffUntil = Time.unscaledTime + backoff;
+                _presenceRejoinInFlight = false;
+            }
+        }
+
         async void OnDestroy()
         {
             // Duplicate instance (Awake's singleton guard already Destroy()'d this
```

</details>

# Branch archive: `claude/friendly-ritchie-e3ccsm`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-11 by Claude
- **Unmerged commits:** 1
- **Forked from:** `7a1c1e0d0` (2026-06-11, Merge pull request #545 from froglet-studio/claude/upbeat-dijkstra-kpu4e2)
- **Tip:** `3c26fc136`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Docs/PresenceSystem/ARCHITECTURE.md`
  - `Docs/PresenceSystem/BUGS.md`

### `3c26fc136` — fix(presence): self-heal displayName/avatarId lobby properties (B10)

_Claude, 2026-06-11 21:56:59 +0000_

```text
First-run users join the presence lobby at sign-in — before the username
panel — so the lobby carries the default "Pilot####". The rename was then
pushed by a single fire-and-forget SaveCurrentPlayerDataAsync that fires
inside the startup write-burst window where lobby saves are documented to
fail (B1 stale-index + ~1 write/s rate limit), and nothing ever retried
identity keys: PublishPartyStateIfChangedAsync reconciled only
partyCount/partyMax/matchName. Other players kept seeing "Pilot####"
until the renamed client rejoined (restart), whose join-request
properties travel a different, reliable API surface.

- Fold displayName/avatarId into PublishPartyStateIfChangedAsync's
  change detection; published-state trackers advance only after a
  successful save, so a lost write retries on the next refresh tick.
- Reset the identity trackers in ApplyPostLobbyJoinState — the join
  publishes a pre-request snapshot, so every (re)join re-verifies the
  live values (also heals the rename-during-join race).
- Route HandleProfileChanged and the post-join catch-up through
  PublishPresenceImmediateAsync; delete the bespoke
  RepublishLocalIdentityAsync. Side benefit: XP/crystal profile churn no
  longer triggers unconditional lobby writes.
- Document as B10 in Docs/PresenceSystem/BUGS.md + architecture notes.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 96 +++++++++++++++++++++++++++++++++------------
 Docs/PresenceSystem/ARCHITECTURE.md                       |  5 ++-
 Docs/PresenceSystem/BUGS.md                               | 70 +++++++++++++++++++++++++++++++++
 3 files changed, 143 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 274 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 6974a329e..c61ee79d6 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -202,6 +202,8 @@ namespace CosmicShore.Gameplay
         private int    _consecutiveRefreshErrors;
         private int    _publishedPartyCount = -1;
         private string _publishedMatchName  = "<UNSET>";
+        private string _publishedDisplayName;               // null = identity not yet verified on this lobby
+        private int    _publishedAvatarId   = int.MinValue; // sentinel pairs with _publishedDisplayName
 
         // ─────────────────────────────────────────────────────────────────────
         // Invite state
@@ -458,11 +460,16 @@ namespace CosmicShore.Gameplay
                 // Apply post-join state now that the lobby reference is live.
                 ApplyPostLobbyJoinState();
 
-                // Catch the case where the cloud profile resolved during
-                // JoinOrCreateAsync — HandleProfileChanged's republish
-                // would have been a no-op (lobby was still null at that moment).
+                // Catch the case where the profile resolved (or the user
+                // confirmed a username) during JoinOrCreateAsync — the join
+                // published a pre-request identity SNAPSHOT, and
+                // HandleProfileChanged's publish was a no-op while the lobby
+                // was still null. ApplyPostLobbyJoinState reset the published-
+                // identity trackers, so this publish re-verifies against the
+                // live values; if the save is eaten (B1 stale-index / rate
+                // limit), the per-tick publisher retries it.
                 SyncLocalIdentity();
-                RepublishLocalIdentityAsync().Forget();
+                PublishPresenceImmediateAsync().Forget();
 
                 // Presence lobby joined — transient state, immediately creates solo Relay session.
                 // Transition flips IsInitialized to true (replaces the old _initialized boolean).
@@ -1608,6 +1615,15 @@ namespace CosmicShore.Gameplay
             connectionData.IsConnected         = true;
             connectionData.IsPresenceLobbyHost = lobby.IsHost;
 
+            // The join/create request published an identity SNAPSHOT taken
+            // before the request was sent — a username confirmed while the
+            // join was in flight is not in it. Reset the published-identity
+            // trackers so the next publish (post-join immediate call or first
+            // refresh tick) re-verifies against the live values instead of
+            // assuming the snapshot is current.
+            _publishedDisplayName = null;
+            _publishedAvatarId    = int.MinValue;
+
             _memberService.SeedLocalPlayer(clearFirst: true);
 
             _eventBus.RaiseHostConnectionEstablished();
@@ -1641,24 +1657,6 @@ namespace CosmicShore.Gameplay
                 "ClearJoinedParty");
         }
 
-        private async UniTaskVoid RepublishLocalIdentityAsync()
-        {
-            var lobby = _lobbyService.ActiveLobby;
-            if (lobby == null) return;
-            await _propertyWriter.WriteAsync(
-                lobby,
-                () =>
-                {
-                    lobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
-                        new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot",
-                            VisibilityPropertyOptions.Public));
-                    lobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
-                        new PlayerProperty(connectionData.LocalAvatarId.ToString(),
-                            VisibilityPropertyOptions.Public));
-                },
-                "RepublishLocalIdentity");
-        }
-
         private async UniTaskVoid PublishPresenceImmediateAsync()
         {
             if (_lobbyService.ActiveLobby == null) return;
@@ -1678,18 +1676,51 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        /// <summary>
+        /// Publishes the local player's presence-lobby properties — identity
+        /// (displayName / avatarId) and party state (count / max / match) —
+        /// whenever they differ from what was last SUCCESSFULLY saved. Runs on
+        /// every refresh tick, so a save eaten by the UGS rate limit or the
+        /// SDK stale-index defect (Docs/PresenceSystem/BUGS.md B1) is retried
+        /// on the next tick instead of being lost: the published-state
+        /// trackers only advance after SaveWithRetryAsync returns.
+        ///
+        /// Identity must be reconciled here because the join snapshot is taken
+        /// before a first-run user has confirmed their username — without
+        /// per-tick healing, a lost rename write would leave the default
+        /// "Pilot####" visible to everyone until the next rejoin (B10).
+        /// </summary>
         private async UniTask PublishPartyStateIfChangedAsync()
         {
             var lobby = _lobbyService.ActiveLobby;
             if (lobby == null) return;
 
-            int    currentCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
-            string currentMatch = ResolveCurrentMatchName();
+            int    currentCount  = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
+            string currentMatch  = ResolveCurrentMatchName();
+            string currentName   = connectionData.LocalDisplayName;
+            int    currentAvatar = connectionData.LocalAvatarId;
+
+            // Never publish an empty display name (reads back as "Unknown
+            // Pilot" on other clients). The trackers stay unset so the publish
+            // fires once identity resolves.
+            bool identityChanged =
+                !string.IsNullOrEmpty(currentName) &&
+                (currentName != _publishedDisplayName || currentAvatar != _publishedAvatarId);
 
-            if (currentCount == _publishedPartyCount && currentMatch == _publishedMatchName) return;
+            if (!identityChanged &&
+                currentCount == _publishedPartyCount &&
+                currentMatch == _publishedMatchName) return;
 
             try
             {
+                if (identityChanged)
+                {
+                    lobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
+                        new PlayerProperty(currentName, VisibilityPropertyOptions.Public));
+                    lobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
+                        new PlayerProperty(currentAvatar.ToString(), VisibilityPropertyOptions.Public));
+                }
+
                 lobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                     new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                 lobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
@@ -1700,6 +1731,11 @@ namespace CosmicShore.Gameplay
                 await _propertyWriter.SaveWithRetryAsync(lobby);
                 _publishedPartyCount = currentCount;
                 _publishedMatchName  = currentMatch;
+                if (identityChanged)
+                {
+                    _publishedDisplayName = currentName;
+                    _publishedAvatarId    = currentAvatar;
+                }
             }
             catch (Exception e)
             {
@@ -1829,11 +1865,19 @@ namespace CosmicShore.Gameplay
             _profileSubscribed = false;
         }
 
+        /// <summary>
+        /// Fires on EVERY profile mutation (rename, avatar, XP, crystals).
+        /// Routes through the change-detection publisher rather than an
+        /// unconditional write: identity changes (username confirmed on the
```

</details>

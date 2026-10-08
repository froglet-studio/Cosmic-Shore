# Branch archive: `claude/test-friend-system-Zm5wJ`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-02 by Yash Sadhukhan
- **Unmerged commits:** 5
- **Forked from:** `8bf20d9c6` (2026-03-02, Merge pull request #313 from froglet-studio/claude/review-vessel-selection-Z4X)
- **Tip:** `db03e781f`
- **Files touched (9):**
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/Controller/Party/PartyInviteController.cs`
  - `Assets/_Scripts/Tests/EditMode/FriendSystemTests.cs`
  - `Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md`
  - `Assets/_Scripts/UI/Elements/PartyAreaPanel.cs`
  - `Assets/_Scripts/UI/Screens/PartyInviteNotificationPanel.cs`
  - `Assets/_Scripts/UI/Views/PartyArcadeView.cs`
  - `Assets/_Scripts/UI/Views/PartySlotView.cs`

### `a49eceeb7` — test(friends): add comprehensive friend system unit tests

_Claude, 2026-03-01 18:29:23 +0000_

```text
Covers FriendData struct (equality, hashing, IsOnline), FriendPresenceActivity
DataContract compliance, FriendsDataSO state management and computed properties,
ScriptableListFriendData SOAP list behavior, and API surface contracts for
FriendsServiceFacade, FriendsInitializer, FriendsPanel, FriendEntryView,
FriendRequestEntryView, and AddFriendPanel. ~60 tests total.
```

```text
 Assets/_Scripts/Tests/EditMode/FriendSystemTests.cs | 1140 +++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md         |    5 +-
 2 files changed, 1143 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1170 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/EditMode/FriendSystemTests.cs b/Assets/_Scripts/Tests/EditMode/FriendSystemTests.cs
new file mode 100644
index 000000000..2eca0ea0e
--- /dev/null
+++ b/Assets/_Scripts/Tests/EditMode/FriendSystemTests.cs
@@ -0,0 +1,1140 @@
+using System;
+using System.Collections.Generic;
+using System.Reflection;
+using System.Runtime.Serialization;
+using NUnit.Framework;
+using UnityEngine;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// Friend System Tests — Comprehensive coverage of the UGS Friends integration.
+    ///
+    /// WHY THIS MATTERS:
+    /// The friend system is the social backbone connecting players. FriendData flows
+    /// through SOAP lists, events, UI views, and the invite pipeline. These tests
+    /// cover: struct construction, equality contracts (HashSet/Dictionary compat),
+    /// IsOnline availability logic, FriendPresenceActivity DataContract compliance,
+    /// FriendsDataSO state management, and API surface contracts for all friend
+    /// system classes that depend on runtime/SDK access.
+    /// </summary>
+    [TestFixture]
+    public class FriendSystemTests
+    {
+        // ─────────────────────────────────────────────────────────────────────
+        // FriendData — Construction
+        // ─────────────────────────────────────────────────────────────────────
+
+        #region FriendData Construction
+
+        [Test]
+        public void FriendData_Constructor_SetsAllFields()
+        {
+            var data = new FriendData("player-123", "TestPilot", 1, "In Menu");
+
+            Assert.AreEqual("player-123", data.PlayerId);
+            Assert.AreEqual("TestPilot", data.DisplayName);
+            Assert.AreEqual(1, data.Availability);
+            Assert.AreEqual("In Menu", data.ActivityStatus);
+        }
+
+        [Test]
+        public void FriendData_Constructor_DefaultsAvailabilityToZero()
+        {
+            var data = new FriendData("player-123", "TestPilot");
+
+            Assert.AreEqual(0, data.Availability,
+                "Availability should default to 0 (Unknown) when not specified.");
+        }
+
+        [Test]
+        public void FriendData_Constructor_DefaultsActivityStatusToEmpty()
+        {
+            var data = new FriendData("player-123", "TestPilot");
+
+            Assert.AreEqual("", data.ActivityStatus,
+                "ActivityStatus should default to empty string when not specified.");
+        }
+
+        [Test]
+        public void FriendData_Constructor_AcceptsNullPlayerId()
+        {
+            var data = new FriendData(null, "TestPilot");
+
+            Assert.IsNull(data.PlayerId);
+        }
+
+        [Test]
+        public void FriendData_Constructor_AcceptsNullDisplayName()
+        {
+            var data = new FriendData("player-123", null);
+
+            Assert.IsNull(data.DisplayName);
+        }
+
+        #endregion
+
+        // ─────────────────────────────────────────────────────────────────────
+        // FriendData — Equality Contract
+        // ─────────────────────────────────────────────────────────────────────
+
+        #region FriendData Equality
+
+        [Test]
+        public void FriendData_Equality_SamePlayerId_AreEqual()
+        {
+            var a = new FriendData("player-123", "PilotA", 1, "In Menu");
+            var b = new FriendData("player-123", "PilotB", 5, "Offline");
+
+            Assert.AreEqual(a, b,
+                "FriendData equality should be by PlayerId only.");
+        }
+
+        [Test]
+        public void FriendData_Equality_DifferentPlayerId_AreNotEqual()
+        {
+            var a = new FriendData("player-123", "SameName", 1, "In Menu");
+            var b = new FriendData("player-456", "SameName", 1, "In Menu");
+
+            Assert.AreNotEqual(a, b,
+                "FriendData with different PlayerIds should not be equal.");
+        }
+
+        [Test]
+        public void FriendData_GetHashCode_SamePlayerId_SameHash()
+        {
+            var a = new FriendData("player-123", "PilotA", 1, "In Menu");
+            var b = new FriendData("player-123", "PilotB", 5, "Offline");
+
+            Assert.AreEqual(a.GetHashCode(), b.GetHashCode(),
+                "Same PlayerId must produce the same hash code for HashSet/Dictionary compat.");
+        }
+
+        [Test]
+        public void FriendData_GetHashCode_NullPlayerId_DoesNotThrow()
+        {
+            var data = new FriendData(null, "TestPilot");
+
+            Assert.DoesNotThrow(() => data.GetHashCode(),
+                "Null PlayerId should produce 0 hash, not throw.");
+            Assert.AreEqual(0, data.GetHashCode());
+        }
+
+        [Test]
+        public void FriendData_Equals_NonFriendDataObject_ReturnsFalse()
+        {
+            var data = new FriendData("player-123", "TestPilot");
+
+            Assert.IsFalse(data.Equals("not a FriendData"));
+            Assert.IsFalse(data.Equals(42));
+            Assert.IsFalse(data.Equals(null));
+        }
+
+        [Test]
+        public void FriendData_HashSet_Deduplicates_ByPlayerId()
+        {
+            var set = new HashSet<FriendData>
+            {
+                new("player-123", "PilotA", 1, "In Menu"),
+                new("player-123", "PilotB", 5, "Offline"),
+                new("player-456", "PilotC", 2, "In Game")
+            };
+
```

</details>

### `9a8e51b77` — fix(party): enable PartyInviteNotificationPanel so it receives invite events

_Claude, 2026-03-01 20:52:04 +0000_

```text
The notification panel started with m_IsActive=0 in Menu_Main, which
prevented OnEnable() from firing and subscribing to OnInviteReceived.
The invite SOAP event was raised correctly but nobody was listening.

Fix: start the GO active but visually hidden via CanvasGroup (alpha=0,
blocksRaycasts=false) so OnEnable() fires and the event subscription
connects. ShowPanel(false) in Awake() ensures no visual flash.
```

```text
 Assets/_Scenes/Menu_Main.unity                             | 2 +-
 Assets/_Scripts/UI/Screens/PartyInviteNotificationPanel.cs | 1 +
 2 files changed, 2 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Screens/PartyInviteNotificationPanel.cs b/Assets/_Scripts/UI/Screens/PartyInviteNotificationPanel.cs
index 1d3dd1c84..017dc33c1 100644
--- a/Assets/_Scripts/UI/Screens/PartyInviteNotificationPanel.cs
+++ b/Assets/_Scripts/UI/Screens/PartyInviteNotificationPanel.cs
@@ -50,6 +50,7 @@ namespace CosmicShore.UI
         {
             acceptButton?.onClick.AddListener(OnAcceptPressed);
             declineButton?.onClick.AddListener(OnDeclinePressed);
+            ShowPanel(false); // Start visually hidden so OnEnable() can subscribe to SOAP events
         }
 
         void OnEnable()
```

</details>

### `67edac409` — fix(party): prevent UGS rate limiting on party transition

_Claude, 2026-03-01 21:14:51 +0000_

```text
Three fixes for "Too many requests" error when clicking "+" to invite:

1. HostConnectionService: Add _refreshing guard to prevent concurrent
   RefreshAsync() calls, and _refreshPaused flag with SetRefreshPaused()
   API so callers can suspend the refresh loop during transitions.

2. PartyInviteController: Pause refresh loop around both
   TransitionToPartyHostAsync() and AcceptInviteAsync() to avoid
   rate limit conflicts between refresh and session creation API calls.

3. UI: Disable "+" add buttons during transitions in PartyArcadeView
   and PartyAreaPanel, with early-exit if already transitioning. Add
   SetAddButtonInteractable() to PartySlotView.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 30 +++++++++++++++++++++++++++---
 Assets/_Scripts/Controller/Party/PartyInviteController.cs |  8 ++++++++
 Assets/_Scripts/UI/Elements/PartyAreaPanel.cs             | 26 +++++++++++++++++++++-----
 Assets/_Scripts/UI/Views/PartyArcadeView.cs               | 26 +++++++++++++++++++++-----
 Assets/_Scripts/UI/Views/PartySlotView.cs                 | 10 ++++++++++
 5 files changed, 87 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 217 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 4a736ca4a..242f48e6e 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -56,6 +56,8 @@ namespace CosmicShore.Gameplay
         private bool _initialized;
         private bool _joining;
         private bool _leaving;
+        private bool _refreshing;
+        private bool _refreshPaused;
         private PartyInviteData? _lastFiredInvite;
 
         private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
@@ -98,13 +100,14 @@ namespace CosmicShore.Gameplay
 
         void Update()
         {
-            if (!_initialized || _presenceLobby == null) return;
+            if (!_initialized || _presenceLobby == null || _refreshPaused) return;
 
             _refreshTimer += Time.deltaTime;
             if (_refreshTimer >= refreshIntervalSeconds)
             {
                 _refreshTimer = 0f;
-                RefreshAsync().Forget();
+                if (!_refreshing)
+                    RefreshAsync().Forget();
             }
         }
 
@@ -147,6 +150,22 @@ namespace CosmicShore.Gameplay
             connectionData.OnHostConnectionLost?.Raise();
         }
 
+        // ─────────────────────────────────────────────────────────────────────
+        // Public: Refresh Control
+        // ─────────────────────────────────────────────────────────────────────
+
+        /// <summary>
+        /// Pauses or resumes the periodic refresh loop.
+        /// Called by <see cref="PartyInviteController"/> during transitions
+        /// to avoid UGS rate limit conflicts with session creation API calls.
+        /// </summary>
+        public void SetRefreshPaused(bool paused)
+        {
+            _refreshPaused = paused;
+            if (!paused)
+                _refreshTimer = 0f;
+        }
+
         // ─────────────────────────────────────────────────────────────────────
         // Public: Invite API
         // ─────────────────────────────────────────────────────────────────────
@@ -461,7 +480,8 @@ namespace CosmicShore.Gameplay
 
         private async UniTaskVoid RefreshAsync()
         {
-            if (_presenceLobby == null) return;
+            if (_presenceLobby == null || _refreshing) return;
+            _refreshing = true;
 
             try
             {
@@ -507,6 +527,10 @@ namespace CosmicShore.Gameplay
             {
                 Debug.LogWarning($"[HostConnectionService] Refresh error: {e.Message}");
             }
+            finally
+            {
+                _refreshing = false;
+            }
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Controller/Party/PartyInviteController.cs b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
index 2881878b5..14d3a43d2 100644
--- a/Assets/_Scripts/Controller/Party/PartyInviteController.cs
+++ b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
@@ -99,6 +99,9 @@ namespace CosmicShore.Gameplay
             {
                 Debug.Log("[PartyInviteController] Starting accept flow...");
 
+                // Pause refresh loop to avoid UGS rate limit conflicts
+                HostConnectionService.Instance?.SetRefreshPaused(true);
+
                 // Step 1: Clean up current game state
                 CleanUpCurrentSession();
 
@@ -148,6 +151,7 @@ namespace CosmicShore.Gameplay
             }
             finally
             {
+                HostConnectionService.Instance?.SetRefreshPaused(false);
                 _transitioning = false;
             }
         }
@@ -204,6 +208,9 @@ namespace CosmicShore.Gameplay
             {
                 Debug.Log("[PartyInviteController] Starting host transition to Relay...");
 
+                // Pause refresh loop to avoid UGS rate limit conflicts
+                HostConnectionService.Instance.SetRefreshPaused(true);
+
                 // Step 1: Clean up current menu vessels
                 CleanUpCurrentSession();
 
@@ -238,6 +245,7 @@ namespace CosmicShore.Gameplay
             }
             finally
             {
+                HostConnectionService.Instance?.SetRefreshPaused(false);
                 _transitioning = false;
             }
         }
diff --git a/Assets/_Scripts/UI/Elements/PartyAreaPanel.cs b/Assets/_Scripts/UI/Elements/PartyAreaPanel.cs
index 15c5f22cd..bfc46dd47 100644
--- a/Assets/_Scripts/UI/Elements/PartyAreaPanel.cs
+++ b/Assets/_Scripts/UI/Elements/PartyAreaPanel.cs
@@ -160,15 +160,31 @@ namespace CosmicShore.UI
                 return;
             }
 
-            // If PartyInviteController is available, ensure we're on a Relay host
-            // so invited clients can actually connect.
             var controller = PartyInviteController.Instance;
-            if (controller != null && connectionData.IsHost)
+            if (controller != null && controller.IsTransitioning) return;
+
+            SetAddButtonsInteractable(false);
+            try
             {
-                await controller.TransitionToPartyHostAsync();
+                // If PartyInviteController is available, ensure we're on a Relay host
+                // so invited clients can actually connect.
+                if (controller != null && connectionData.IsHost)
+                {
+                    await controller.TransitionToPartyHostAsync();
+                }
+
+                onlinePlayersPanel?.Show();
             }
+            finally
+            {
+                SetAddButtonsInteractable(true);
+            }
+        }
 
-            onlinePlayersPanel?.Show();
+        private void SetAddButtonsInteractable(bool interactable)
+        {
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._

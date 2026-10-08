# Branch archive: `claude/fix-lobby-rate-limit-W7HA4`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `fe44be6bf` (2026-03-25, Merge branch 'claude/trace-player-name-display-VfXeo' into app-shell-polish)
- **Tip:** `af14b441e`
- **Files touched (1):**
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`

### `af14b441e` — fix(party): prevent lobby rate-limit errors on invite send

_Claude, 2026-03-28 00:00:33 +0000_

```text
SendInviteAsync was making 3 rapid UGS Lobby API calls (pre-save
refresh + save + post-save refresh) back-to-back, exceeding the ~1
req/1.5s rate limit. The pre-save RefreshAsync had no 429 handling,
causing silent invite failures.

- Add ThrottleLobbyCallAsync() to enforce minimum 1.1s spacing between
  all raw UGS Lobby API calls (RefreshAsync, SaveCurrentPlayerDataAsync)
- Remove redundant pre-save RefreshAsync from SendInviteAsync,
  ClearSentInvitePropertiesAsync, and RequestClearInviteAsync — the
  _lobbyBusy mutex already guarantees the SDK cache is stable from the
  last refresh cycle
- Apply throttle to RefreshAsync loop and SaveWithRetryAsync retry paths
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 52 ++++++++++++++++++++++++++++++++++++---------
 1 file changed, 42 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 62b47cc8b..9681c08bf 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -97,11 +97,33 @@ namespace CosmicShore.Gameplay
         private const int RATE_LIMIT_BASE_DELAY_MS = 2000;
         private float _rateLimitBackoffUntil;
 
+        /// <summary>
+        /// Minimum interval between UGS Lobby API calls to avoid HTTP 429.
+        /// The Lobby service rate-limits at ~1 request per 1.5s.
+        /// </summary>
+        private const float MIN_LOBBY_API_INTERVAL_SECONDS = 1.1f;
+        private float _lastLobbyApiCallTime;
+
         private static bool IsRateLimitException(Exception e)
         {
             return e.Message != null && e.Message.Contains("Too Many Requests");
         }
 
+        /// <summary>
+        /// Waits until enough time has elapsed since the last UGS Lobby API call
+        /// to stay under the rate limit. Call before every raw SDK lobby call.
+        /// </summary>
+        private async Task ThrottleLobbyCallAsync()
+        {
+            float elapsed = Time.time - _lastLobbyApiCallTime;
+            if (elapsed < MIN_LOBBY_API_INTERVAL_SECONDS)
+            {
+                int waitMs = Mathf.CeilToInt((MIN_LOBBY_API_INTERVAL_SECONDS - elapsed) * 1000f);
+                await Task.Delay(waitMs);
+            }
+            _lastLobbyApiCallTime = Time.time;
+        }
+
         // ─────────────────────────────────────────────────────────────────────
         // Unity Lifecycle
         // ─────────────────────────────────────────────────────────────────────
@@ -267,10 +289,10 @@ namespace CosmicShore.Gameplay
                     $"[INVITE-SEND] Setting properties — invite_target: '{targetPlayerId}', " +
                     $"invite_data: '{inviteData}'", Color.cyan);
 
-                // Refresh FIRST so the SDK's cached player list has a valid index
-                // for the local player. Then set properties and save — this order
-                // ensures refresh doesn't overwrite our SetProperty() calls.
-                await _presenceLobby.RefreshAsync();
+                // The _lobbyBusy mutex guarantees no concurrent RefreshAsync is running,
+                // so the SDK's cached player list is already stable from the last
+                // refresh cycle. No need for an extra RefreshAsync here — it was the
+                // primary cause of HTTP 429 rate-limit errors (3 rapid API calls).
 
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                     new PlayerProperty(targetPlayerId, VisibilityPropertyOptions.Public));
@@ -611,6 +633,7 @@ namespace CosmicShore.Gameplay
             bool shouldReconnect = false;
             try
             {
+                await ThrottleLobbyCallAsync();
                 await _presenceLobby.RefreshAsync();
 
                 // ── Online player list (diff-based) ─────────────────────────
@@ -925,7 +948,8 @@ namespace CosmicShore.Gameplay
 
             try
             {
-                await _presenceLobby.RefreshAsync();
+                // Called from within RefreshAsync which already holds _lobbyBusy
+                // and just refreshed — no additional refresh needed.
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                     new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
@@ -952,6 +976,7 @@ namespace CosmicShore.Gameplay
             {
                 try
                 {
+                    await ThrottleLobbyCallAsync();
                     await _presenceLobby.SaveCurrentPlayerDataAsync();
 
                     // Post-save refresh: sync the SDK's cached lobby state with the
@@ -959,7 +984,11 @@ namespace CosmicShore.Gameplay
                     // incoming WebSocket lobby-change deltas reference stale player
                     // indices, which triggers ArgumentOutOfRangeException in the SDK's
                     // internal LobbyPatcher.
-                    try { await _presenceLobby.RefreshAsync(); }
+                    try
+                    {
+                        await ThrottleLobbyCallAsync();
+                        await _presenceLobby.RefreshAsync();
+                    }
                     catch { /* best-effort — polling corrects state on next cycle */ }
 
                     return;
@@ -972,7 +1001,11 @@ namespace CosmicShore.Gameplay
                     await Task.Delay(retryDelayMs);
 
                     // Re-sync the SDK's cached player list before retrying.
-                    try { await _presenceLobby.RefreshAsync(); }
+                    try
+                    {
+                        await ThrottleLobbyCallAsync();
+                        await _presenceLobby.RefreshAsync();
+                    }
                     catch { /* best-effort refresh */ }
                 }
             }
@@ -991,9 +1024,8 @@ namespace CosmicShore.Gameplay
             _lobbyBusy = true;
             try
             {
-                // Refresh first to sync the SDK's cached player index,
-                // then set properties and save.
-                await _presenceLobby.RefreshAsync();
+                // The _lobbyBusy mutex guarantees the SDK's cached player index
+                // is stable from the last refresh cycle — no extra refresh needed.
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                     new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
```

</details>

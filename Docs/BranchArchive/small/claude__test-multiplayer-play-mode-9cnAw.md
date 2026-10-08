# Branch archive: `claude/test-multiplayer-play-mode-9cnAw`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e86e40581` (2026-03-01, feat(multiplayer): support MPPM as independent hosts with unique identities)
- **Tip:** `b047c88f6`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/Controller/Multiplayer/Tests/MppmSupportTests.cs`
  - `Assets/_Scripts/System/AuthenticationServiceFacade.cs`

### `b047c88f6` — test(multiplayer): add unit tests for MPPM profile and port helpers

_Claude, 2026-03-01 06:47:56 +0000_

```text
Extract pure logic into internal static helpers (GetMppmProfileName,
GetMppmPort) and add tests covering tag combinations, range bounds,
determinism, fallback behavior, and default port avoidance.
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs       |  20 +++++-
 Assets/_Scripts/Controller/Multiplayer/Tests/MppmSupportTests.cs | 135 +++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/System/AuthenticationServiceFacade.cs            |  17 +++--
 3 files changed, 164 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 214 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index aa955d86f..588b93de8 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -137,10 +137,9 @@ namespace CosmicShore.Gameplay
                     if (transport != null)
                     {
                         var tags = CurrentPlayer.ReadOnlyTags();
-                        var tagKey = tags != null && tags.Length > 0 ? string.Join("-", tags) : "clone";
-                        ushort port = (ushort)(7778 + (ushort)(Math.Abs(tagKey.GetHashCode()) % 100));
+                        ushort port = GetMppmPort(tags);
                         transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
-                        CSDebug.Log($"[MultiplayerSetup] MPPM clone '{tagKey}' — local host port {port}.");
+                        CSDebug.Log($"[MultiplayerSetup] MPPM clone — local host port {port}.");
                     }
                 }
 #endif
@@ -412,5 +411,20 @@ namespace CosmicShore.Gameplay
                 CSDebug.LogError($"[Net] Transport failure handling error: {e}");
             }
         }
+
+        // --------------------------
+        // MPPM Port Isolation
+        // --------------------------
+
+        /// <summary>
+        /// Derives a unique local host port from MPPM tags so each virtual player
+        /// can bind without conflicts. Returns a port in the range [7778, 7877].
+        /// Pure logic — no Unity API calls.
+        /// </summary>
+        internal static ushort GetMppmPort(string[] tags)
+        {
+            var tagKey = tags != null && tags.Length > 0 ? string.Join("-", tags) : "clone";
+            return (ushort)(7778 + Math.Abs(tagKey.GetHashCode()) % 100);
+        }
     }
 }
diff --git a/Assets/_Scripts/Controller/Multiplayer/Tests/MppmSupportTests.cs b/Assets/_Scripts/Controller/Multiplayer/Tests/MppmSupportTests.cs
new file mode 100644
index 000000000..a588adc15
--- /dev/null
+++ b/Assets/_Scripts/Controller/Multiplayer/Tests/MppmSupportTests.cs
@@ -0,0 +1,135 @@
+using CosmicShore.Core;
+using NUnit.Framework;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Tests for MPPM (Multiplayer Play Mode) support helpers.
+    /// Validates the pure-logic methods that derive auth profile names
+    /// and local host ports from MPPM tags.
+    /// </summary>
+    [TestFixture]
+    public class MppmSupportTests
+    {
+        #region Profile Name — AuthenticationServiceFacade.GetMppmProfileName
+
+        [Test]
+        public void GetMppmProfileName_SingleTag_ReturnsPrefixedTag()
+        {
+            var result = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player2" });
+
+            Assert.AreEqual("mppm-Player2", result);
+        }
+
+        [Test]
+        public void GetMppmProfileName_MultipleTags_JoinsWithDash()
+        {
+            var result = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player2", "Red" });
+
+            Assert.AreEqual("mppm-Player2-Red", result);
+        }
+
+        [Test]
+        public void GetMppmProfileName_NullTags_ReturnsFallback()
+        {
+            var result = AuthenticationServiceFacade.GetMppmProfileName(null);
+
+            Assert.AreEqual("mppm-clone", result);
+        }
+
+        [Test]
+        public void GetMppmProfileName_EmptyTags_ReturnsFallback()
+        {
+            var result = AuthenticationServiceFacade.GetMppmProfileName(new string[0]);
+
+            Assert.AreEqual("mppm-clone", result);
+        }
+
+        [Test]
+        public void GetMppmProfileName_DifferentTags_ProduceDifferentProfiles()
+        {
+            var profileA = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player2" });
+            var profileB = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player3" });
+
+            Assert.AreNotEqual(profileA, profileB,
+                "Different MPPM tags should produce different auth profiles.");
+        }
+
+        #endregion
+
+        #region Port Offset — MultiplayerSetup.GetMppmPort
+
+        [Test]
+        public void GetMppmPort_ReturnsPortInValidRange()
+        {
+            var port = MultiplayerSetup.GetMppmPort(new[] { "Player2" });
+
+            Assert.GreaterOrEqual(port, 7778, "MPPM port must be >= 7778 to avoid default 7777.");
+            Assert.LessOrEqual(port, 7877, "MPPM port must be <= 7877 (7778 + 99).");
+        }
+
+        [Test]
+        public void GetMppmPort_NullTags_ReturnsPortInValidRange()
+        {
+            var port = MultiplayerSetup.GetMppmPort(null);
+
+            Assert.GreaterOrEqual(port, 7778);
+            Assert.LessOrEqual(port, 7877);
+        }
+
+        [Test]
+        public void GetMppmPort_EmptyTags_ReturnsPortInValidRange()
+        {
+            var port = MultiplayerSetup.GetMppmPort(new string[0]);
+
+            Assert.GreaterOrEqual(port, 7778);
+            Assert.LessOrEqual(port, 7877);
+        }
+
+        [Test]
+        public void GetMppmPort_SameTags_IsDeterministic()
+        {
+            var portA = MultiplayerSetup.GetMppmPort(new[] { "Player2" });
+            var portB = MultiplayerSetup.GetMppmPort(new[] { "Player2" });
+
+            Assert.AreEqual(portA, portB,
+                "Same tags should always produce the same port.");
+        }
+
+        [Test]
+        public void GetMppmPort_DifferentTags_ProduceDifferentPorts()
+        {
+            var portA = MultiplayerSetup.GetMppmPort(new[] { "Player2" });
+            var portB = MultiplayerSetup.GetMppmPort(new[] { "Player3" });
+
+            // Hash collisions are theoretically possible but extremely unlikely
+            // for these specific inputs.
```

</details>

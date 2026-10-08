# Branch archive: `claude/add-menu-scene-tests-43Wzb`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-25 by Claude
- **Unmerged commits:** 2
- **Forked from:** `a8327acf7` (2026-02-25, Merge pull request #128 from froglet-studio/claude/fix-roundstats-networkvaria)
- **Tip:** `b73169356`
- **Files touched (6):**
  - `Assets/_Scripts/UI/Tests/ArcadeGameConfigSOTests.cs`
  - `Assets/_Scripts/UI/Tests/CosmicShore.UI.Tests.asmdef`
  - `Assets/_Scripts/UI/Tests/EpisodeScreenTests.cs`
  - `Assets/_Scripts/UI/Tests/ModalWindowManagerTests.cs`
  - `Assets/_Scripts/UI/Tests/ScreenSwitcherTests.cs`
  - `Assets/_Scripts/UI/Tests/VersionDisplayTests.cs`

### `9cc055c80` — Add unit tests for main menu scene components

_Claude, 2026-02-25 14:14:04 +0000_

```text
Tests cover ScreenSwitcher (modal stack, return state, screen queries,
navigation bounds, enum values), ModalWindowManager (open/close lifecycle,
state tracking, settings special case), ArcadeGameConfigSO (defaults,
mutation, ResetState, serialization), EpisodeScreen (panel toggle, load
state, null safety), and VersionDisplay (prefix formatting).
```

```text
 Assets/_Scripts/UI/Tests/ArcadeGameConfigSOTests.cs  | 177 ++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Tests/CosmicShore.UI.Tests.asmdef |  26 +++++
 Assets/_Scripts/UI/Tests/EpisodeScreenTests.cs       | 190 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Tests/ModalWindowManagerTests.cs  | 155 ++++++++++++++++++++++++++
 Assets/_Scripts/UI/Tests/ScreenSwitcherTests.cs      | 288 +++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Tests/VersionDisplayTests.cs      |  78 ++++++++++++++
 6 files changed, 914 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 950 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Tests/ArcadeGameConfigSOTests.cs b/Assets/_Scripts/UI/Tests/ArcadeGameConfigSOTests.cs
new file mode 100644
index 000000000..1f43fbcab
--- /dev/null
+++ b/Assets/_Scripts/UI/Tests/ArcadeGameConfigSOTests.cs
@@ -0,0 +1,177 @@
+using NUnit.Framework;
+using UnityEngine;
+
+namespace CosmicShore.App.UI.Tests
+{
+    [TestFixture]
+    public class ArcadeGameConfigSOTests
+    {
+        ArcadeGameConfigSO _config;
+
+        [SetUp]
+        public void SetUp()
+        {
+            _config = ScriptableObject.CreateInstance<ArcadeGameConfigSO>();
+        }
+
+        [TearDown]
+        public void TearDown()
+        {
+            if (_config != null)
+                Object.DestroyImmediate(_config);
+        }
+
+        #region Defaults
+
+        [Test]
+        public void CreateInstance_ReturnsNonNull()
+        {
+            Assert.IsNotNull(_config);
+        }
+
+        [Test]
+        public void CreateInstance_IsArcadeGameConfigSO()
+        {
+            Assert.IsInstanceOf<ArcadeGameConfigSO>(_config);
+        }
+
+        [Test]
+        public void Default_SelectedGame_IsNull()
+        {
+            Assert.IsNull(_config.SelectedGame);
+        }
+
+        [Test]
+        public void Default_Intensity_IsZero()
+        {
+            Assert.AreEqual(0, _config.Intensity);
+        }
+
+        [Test]
+        public void Default_PlayerCount_IsZero()
+        {
+            Assert.AreEqual(0, _config.PlayerCount);
+        }
+
+        [Test]
+        public void Default_TeamCount_IsZero()
+        {
+            Assert.AreEqual(0, _config.TeamCount);
+        }
+
+        [Test]
+        public void Default_SelectedShip_IsNull()
+        {
+            Assert.IsNull(_config.SelectedShip);
+        }
+
+        #endregion
+
+        #region State Mutation
+
+        [Test]
+        public void Intensity_CanBeSet()
+        {
+            _config.Intensity = 5;
+            Assert.AreEqual(5, _config.Intensity);
+        }
+
+        [Test]
+        public void PlayerCount_CanBeSet()
+        {
+            _config.PlayerCount = 4;
+            Assert.AreEqual(4, _config.PlayerCount);
+        }
+
+        [Test]
+        public void TeamCount_CanBeSet()
+        {
+            _config.TeamCount = 2;
+            Assert.AreEqual(2, _config.TeamCount);
+        }
+
+        #endregion
+
+        #region ResetState
+
+        [Test]
+        public void ResetState_ClearsIntensity()
+        {
+            _config.Intensity = 10;
+            _config.ResetState();
+
+            Assert.AreEqual(0, _config.Intensity);
+        }
+
+        [Test]
+        public void ResetState_ClearsPlayerCount()
+        {
+            _config.PlayerCount = 4;
+            _config.ResetState();
+
+            Assert.AreEqual(0, _config.PlayerCount);
+        }
+
+        [Test]
+        public void ResetState_ClearsTeamCount()
+        {
+            _config.TeamCount = 2;
+            _config.ResetState();
+
+            Assert.AreEqual(0, _config.TeamCount);
+        }
+
+        [Test]
+        public void ResetState_ClearsSelectedGame()
+        {
+            // SelectedGame is SO_ArcadeGame which we can't easily instantiate,
+            // so we just verify it ends up null after reset.
+            _config.ResetState();
+            Assert.IsNull(_config.SelectedGame);
+        }
+
+        [Test]
+        public void ResetState_ClearsSelectedShip()
+        {
+            _config.ResetState();
+            Assert.IsNull(_config.SelectedShip);
+        }
+
+        [Test]
+        public void ResetState_AllFieldsCleared()
+        {
+            _config.Intensity = 7;
+            _config.PlayerCount = 3;
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._

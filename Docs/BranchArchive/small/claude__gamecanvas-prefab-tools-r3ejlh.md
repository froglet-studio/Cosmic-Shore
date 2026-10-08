# Branch archive: `claude/gamecanvas-prefab-tools-r3ejlh`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-12 by Claude
- **Unmerged commits:** 3
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `65d5d490b`
- **Files touched (14):**
  - `Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs`
  - `Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs`
  - `Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs`
  - `Assets/_Scripts/Editor/FrogletTools/KitValidator.cs`
  - `Assets/_Scripts/Editor/FrogletTools/PrefabDriftFixer.cs`
  - `Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs`
  - `Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs`
  - `Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs.meta`
  - `Assets/_Scripts/UI/MultiplayerHUD.cs`
  - `CLAUDE.md`
  - `Docs/GAMECANVAS.md`
  - `Docs/TOOLING.md`

### `de4d2d515` — feat(gamecanvas): move per-mode canvas config into a ScriptableObject; drop the consolidate action

_Claude, 2026-08-03 15:20:58 +0000_

```text
The consolidation button addressed DRIFT (scenes carrying unapplied overrides),
not UNIFICATION (two forked canvas prefabs). It also made a large, unreviewable
edit across many scenes at once, which is the wrong shape for repairing
scene/prefab divergence. Removed it, and PrefabDriftFixer with it - the Prefab
Kit is now read-only and points at Unity's own Overrides dropdown instead.

The actual unification mechanism, per the requested design - per-mode
configuration lives in an SO the scene points at, not in references added to
scripts or overrides on the shared prefab:

- GameModeUIConfigSO: one asset per mode. EndGameStats (the stat list that was
  the only genuinely per-mode value on the canvas) and ScoreLayout
  (PerPlayer / PerDomain / Inherit).
- GameModeSceneConfig: a one-field component, deliberately NOT part of
  GameCanvas, so pointing a scene at its config never creates a canvas
  override. Consumers call Resolve() rather than holding a serialized field.
- EventDrivenStatsProvider now resolves stats explicit list -> mode config ->
  vessel telemetry.
- MultiplayerHUD.ResolveUseDomainView reads the layout from the config. This is
  the field that unblocks the merge: today the only way to get per-player cards
  is to ship a canvas WITHOUT domain wiring, which is exactly why there are two
  forks. One unified canvas carries the superset, so the choice becomes data.

Every field is opt-in - Inherit and an empty stat list mean "behave as before",
so a scene with no config is completely unaffected and modes can migrate one at
a time.

Prefab Kit fixes:
- Clear is now a visible coral button showing the result count, and clears the
  expanded rows too; Validate All resets expansion state.
- Added an explainer strip stating exactly what Validate checks.
- Per-scene "Ignore scene" replaces the old revert action, recording the
  judgement in the kit config.
- Maelstrom is excluded by default: it is the tournament HUB, not a playable
  mode, so its stripped-down canvas is correct rather than drift.
```

```text
 Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs              |  52 +++++++
 .../Arcade/GameModeSceneConfig.cs.meta}                               |   2 +-
 Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs         |  23 ++-
 Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs        |  28 +++-
 Assets/_Scripts/Editor/FrogletTools/KitValidator.cs                   |  64 +++-----
 Assets/_Scripts/Editor/FrogletTools/PrefabDriftFixer.cs               | 258 --------------------------------
 Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs     |  42 +-----
 Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs              |   8 +-
 Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs               |  76 ++++++++++
 Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs.meta          |  11 ++
 Assets/_Scripts/UI/MultiplayerHUD.cs                                  |  32 +++-
 CLAUDE.md                                                             |  11 +-
 Docs/GAMECANVAS.md                                                    | 130 ++++++++++------
 Docs/TOOLING.md                                                       |   5 +-
 14 files changed, 340 insertions(+), 402 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 955 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs b/Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs
new file mode 100644
index 000000000..77ac6eb50
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs
@@ -0,0 +1,52 @@
+using CosmicShore.ScriptableObjects;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// The one object you drop into a game-mode scene to say "this scene is THIS mode, configured
+    /// like THIS". It holds a single <see cref="GameModeUIConfigSO"/> reference and nothing else.
+    ///
+    /// <b>The point.</b> Shared prefabs (GameCanvas above all) must stay identical in every scene,
+    /// so the per-mode differences need a home that is not a prefab override. That home is the
+    /// config asset; this component is just how a scene points at it. One reference, on an object
+    /// that is NOT part of any shared prefab, so nothing in the canvas is ever overridden.
+    ///
+    /// Consumers find it with <see cref="Resolve"/> rather than holding a serialized reference -
+    /// that is what keeps "add extra references in the script" off the table.
+    /// </summary>
+    [DisallowMultipleComponent]
+    [AddComponentMenu("Cosmic Shore/Game Mode Scene Config")]
+    public class GameModeSceneConfig : MonoBehaviour
+    {
+        [Tooltip("Per-mode UI configuration for this scene. Leave empty and every consumer keeps " +
+                 "its current behaviour - the config is purely additive.")]
+        [SerializeField] GameModeUIConfigSO config;
+
+        public GameModeUIConfigSO Config => config;
+
+        static GameModeSceneConfig _cached;
+
+        /// <summary>
+        /// The active scene's config, or null when the scene has none (menus, tools, and any mode
+        /// that has not been migrated yet). Callers MUST treat null as "carry on as before".
+        ///
+        /// Cached, with the cache validated against Unity's null so a scene change or domain
+        /// reload can't hand back a destroyed component.
+        /// </summary>
+        public static GameModeUIConfigSO Resolve()
+        {
+            if (_cached == null)
+                _cached = FindAnyObjectByType<GameModeSceneConfig>(FindObjectsInactive.Include);
+
+            return _cached != null ? _cached.config : null;
+        }
+
+        void OnEnable() => _cached = this;
+
+        void OnDisable()
+        {
+            if (_cached == this) _cached = null;
+        }
+    }
+}
diff --git a/Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs b/Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs
index 08ef047d2..322d1e5fc 100644
--- a/Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs
+++ b/Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs
@@ -12,11 +12,15 @@ namespace CosmicShore.Gameplay
     /// Subscribes to VesselStatEventSO assets and caches their latest values
     /// for the end-game scoreboard.
     ///
-    /// Supports two modes:
-    /// 1. Explicit (recommended): Wire stat SOs directly via <see cref="statsToTrack"/>.
+    /// Three sources, first one that has anything wins:
+    /// 1. Explicit instance list: stat SOs wired directly via <see cref="statsToTrack"/>.
     ///    Subscription happens on OnEnable - no timing dependency on vessel spawn.
-    /// 2. Dynamic fallback: If <see cref="statsToTrack"/> is empty, discovers stats
-    ///    from the local vessel's VesselTelemetry at OnClientReady / OnMiniGameTurnStarted.
+    /// 2. Per-mode config (preferred for shared prefabs): the scene's
+    ///    <see cref="GameModeSceneConfig"/> -&gt; <c>GameModeUIConfigSO.EndGameStats</c>. This is
+    ///    how a mode gets its own stat list WITHOUT overriding anything on the shared GameCanvas
+    ///    prefab - the list lives in an asset the scene points at.
+    /// 3. Dynamic fallback: discovers stats from the local vessel's VesselTelemetry at
+    ///    OnClientReady / OnMiniGameTurnStarted.
     ///
     /// Stats are only cleared on explicit reset - they persist across turn boundaries
     /// until the game ends, so the scoreboard always shows the final values.
@@ -45,6 +49,17 @@ namespace CosmicShore.Gameplay
             {
                 SubscribeToStats(statsToTrack);
             }
+            else
+            {
+                // No explicit list on this instance: take the mode's list from the scene's
+                // GameModeUIConfigSO if one is present. This is the route that lets the shared
+                // GameCanvas prefab stay byte-identical in every scene - the per-mode stat list
+                // lives in an asset the scene points at, not in an override on the canvas.
+                // Missing config is normal and means "fall through to telemetry discovery".
+                var modeConfig = GameModeSceneConfig.Resolve();
+                if (modeConfig != null && modeConfig.HasEndGameStats)
+                    SubscribeToStats(modeConfig.EndGameStats);
+            }
 
             if (gameData == null) return;
             gameData.OnClientReady.OnRaised         += TrySubscribeFromVessel;
diff --git a/Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs b/Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs
index 6c0a6d90c..4a27cb7f8 100644
--- a/Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs
+++ b/Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs
@@ -91,6 +91,7 @@ namespace CosmicShore.Editor.Froglet
             }
 
             DrawToolbar();
+            DrawValidateExplainer();
             DrawSceneContextStrip();
 
             var entries = _kit.Entries
@@ -150,9 +151,15 @@ namespace CosmicShore.Editor.Froglet
                     ValidateAll();
 
                 GUILayout.Space(6);
-                if (FrogletEditorPalette.ColorButton("CLEAR RESULTS", FrogletEditorPalette.Muted, 116f, 30f,
-                        "Forget the current validation report.", outline: true))
+                bool hasResults = _reports.Count > 0;
+                if (FrogletEditorPalette.ColorButton(
+                        hasResults ? $"✕  CLEAR ({_reports.Count})" : "✕  CLEAR",
+                        FrogletEditorPalette.Coral, 116f, 30f,
+                        "Discard the validation report and collapse every row.", hasResults))
+                {
                     _reports.Clear();
+                    _expanded.Clear();
+                }
 
                 GUILayout.FlexibleSpace();
                 DrawOverallStatus();
@@ -177,6 +184,22 @@ namespace CosmicShore.Editor.Froglet
                 warns == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn);
         }
 
+        /// <summary>Spells out what Validate actually checks, so the report is readable cold.</summary>
+        static void DrawValidateExplainer()
+        {
+            var r = GUILayoutUtility.GetRect(0, 32f, GUILayout.ExpandWidth(true));
+            FrogletEditorPalette.DrawRect(r, FrogletEditorPalette.Surface);
+            FrogletEditorPalette.DrawAccentStripe(r, FrogletEditorPalette.Adapt(FrogletEditorPalette.Info), 3f);
+            GUI.Label(new Rect(r.x + 10f, r.y + 1f, r.width - 14f, r.height - 2f),
+                "VALIDATE checks three things and never writes:  (1) the prefab asset is healthy - assigned, " +
+                "loadable, no missing scripts;  (2) it is in the scene you have open, exactly once;  " +
+                "(3) no OTHER scene carries unapplied overrides on it - i.e. a scene running its own edited " +
+                "copy, which would mask changes you make to the prefab.  Fix those in Unity's Overrides " +
+                "dropdown; use Ignore for scenes that are meant to differ.",
+                new GUIStyle(FrogletEditorPalette.CardBody) { wordWrap = true });
+            GUILayout.Space(4);
+        }
+
```

</details>

### `15b9dea0d` — docs(gamecanvas): base prefab survives, HexRace fork gets deleted; rewrite the migration steps

_Claude, 2026-08-03 15:35:12 +0000_

```text
Direction reversed on request: CORE/GameCanvas.prefab is the survivor and
GameCanvas-HexRace.prefab is deleted, with the fork's content moving into the
base. This is also the cheaper direction - the base GUID is already referenced
by 10 scenes and the fork by 6, so only those 6 need their canvas
re-instantiated instead of 10.

Consequence for the code: once the unified canvas always carries the domain
wiring, "are the domain containers wired?" stops being a usable signal - it was
only ever meaningful because shipping a canvas WITHOUT the wiring was the one
way to get per-player cards, which is exactly why a second prefab existed.
MultiplayerHUD.ResolveUseDomainView now resolves: explicit GameModeUIConfigSO
layout, else the controller type (MultiplayerDomainGamesController = per-domain),
else per-player. A new domain mode therefore gets the right layout for free.

That heuristic is correct for every mode today except Multiplayer Cellular Duel,
which is a domain controller currently shipping per-player cards - flagged in
the doc, the enum summary and the field tooltip as the one that needs an
explicit PerPlayer.

Docs/GAMECANVAS.md section 6 rewritten as four ordered steps: bring the base up
to the superset (including the script swap to MultiplayerHUD/MultiplayerHUDView
and NOT copying the 8 dangling references), author the per-mode config assets,
migrate the 6 fork scenes one at a time with a play-test each, then delete the
fork. Plus a table of what no longer needs wiring at all.
```

```text
 Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs |   2 +-
 Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs  |  12 +++-
 Assets/_Scripts/UI/MultiplayerHUD.cs                     |  36 +++++-----
 Docs/GAMECANVAS.md                                       | 171 ++++++++++++++++++++++++++++++++-------------
 4 files changed, 153 insertions(+), 68 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 307 lines)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs b/Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs
index 725887d18..94edffe42 100644
--- a/Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs
@@ -112,7 +112,7 @@ namespace CosmicShore.ScriptableObjects
                 ("Assets/_Prefabs/CORE/GameCanvas.prefab", GameModePrefabRole.Interface, true,
                     "The shared in-game canvas: HUD, scoreboard, pause, countdown. One source of truth for every mode."),
                 ("Assets/_Prefabs/GameCanvas-HexRace.prefab", GameModePrefabRole.Interface, false,
-                    "Forked canvas used by the six domain modes. Being retired into the base - see Docs/GAMECANVAS.md."),
+                    "BEING DELETED. Forked canvas still used by the six domain modes; its content is moving into CORE/GameCanvas. Remove this entry once no scene references it - see Docs/GAMECANVAS.md."),
                 ("Assets/_Prefabs/CORE/ContainerScope.prefab", GameModePrefabRole.Essential, true,
                     "Reflex DI scope. Without it every [Inject] field in the scene stays null."),
                 ("Assets/_Prefabs/CORE/Player and Vessel Spawner.prefab", GameModePrefabRole.Spawning, true,
diff --git a/Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs b/Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs
index bbff33195..17dfb1882 100644
--- a/Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs
@@ -11,7 +11,12 @@ namespace CosmicShore.ScriptableObjects
     /// </summary>
     public enum HudScoreLayout
     {
-        /// <summary>Decide from the prefab wiring, as before. Safe default.</summary>
+        /// <summary>
+        /// Decide automatically: per-domain when the scene's controller derives from
+        /// <c>MultiplayerDomainGamesController</c>, per-player otherwise. Correct for every mode
+        /// today except Multiplayer Cellular Duel, which is a domain controller currently shipping
+        /// the per-player layout - set that one explicitly.
+        /// </summary>
         Inherit = 0,
         /// <summary>One card per player, in PlayerScoreContainer.</summary>
         PerPlayer = 1,
@@ -64,8 +69,9 @@ namespace CosmicShore.ScriptableObjects
                  "prefab carries the superset - MultiplayerHUD + the domain containers - so the " +
                  "wiring is always present; this says whether the mode actually wants the " +
                  "per-domain layout or the per-player cards.\n\n" +
-                 "Inherit = decide from the prefab wiring exactly as before, so a scene with no " +
-                 "config behaves identically to today.")]
+                 "Inherit decides from the controller type (MultiplayerDomainGamesController = " +
+                 "per-domain), which is right for every mode except Multiplayer Cellular Duel - " +
+                 "set that one to PerPlayer explicitly if you want to keep its current cards.")]
         public HudScoreLayout ScoreLayout = HudScoreLayout.Inherit;
 
         public string ResolvedName =>
diff --git a/Assets/_Scripts/UI/MultiplayerHUD.cs b/Assets/_Scripts/UI/MultiplayerHUD.cs
index e803e38de..be8d1a42a 100644
--- a/Assets/_Scripts/UI/MultiplayerHUD.cs
+++ b/Assets/_Scripts/UI/MultiplayerHUD.cs
@@ -136,29 +136,33 @@ namespace CosmicShore.UI
         /// Chooses the per-domain or per-player layout.
         ///
         /// Historically this was decided purely by whether the domain containers happened to be
-        /// wired on this scene's canvas - which is precisely why the canvas had to be a different
-        /// prefab per mode family. With one unified GameCanvas the wiring is ALWAYS present, so the
-        /// choice has to come from data: the scene's <see cref="GameModeSceneConfig"/> names the
-        /// layout its mode wants.
+        /// wired on this scene's canvas - which is exactly why there had to be TWO canvas prefabs:
+        /// shipping a canvas without the wiring was the only way to get per-player cards. Once one
+        /// unified GameCanvas carries the wiring for everyone, that signal is gone and the choice
+        /// has to come from somewhere else.
         ///
-        /// No config, or <see cref="HudScoreLayout.Inherit"/>, falls back to the wiring check, so
-        /// every scene that has not been migrated behaves exactly as it does today. A mode that
-        /// asks for <see cref="HudScoreLayout.PerDomain"/> without the wiring present still can't
-        /// get it, so a half-migrated scene degrades instead of rendering nothing.
+        /// Resolution order:
+        ///   1. The scene's <see cref="GameModeSceneConfig"/>, when it names a layout explicitly.
+        ///   2. Otherwise the controller type: every domain mode derives from
+        ///      <see cref="MultiplayerDomainGamesController"/>, so "is this a domain game?" is
+        ///      already encoded in the class hierarchy and a NEW domain mode gets the right layout
+        ///      for free.
+        ///   3. Otherwise per-player.
+        ///
+        /// A mode that asks for <see cref="HudScoreLayout.PerDomain"/> on a canvas that lacks the
+        /// wiring still can't have it, so a half-migrated scene degrades to cards rather than
+        /// rendering nothing.
         /// </summary>
         bool ResolveUseDomainView()
         {
-            bool wired = multiplayerView != null && multiplayerView.HasDomainPanelWiring;
+            if (multiplayerView == null || !multiplayerView.HasDomainPanelWiring)
+                return false;
 
             var modeConfig = GameModeSceneConfig.Resolve();
-            if (modeConfig == null) return wired;
+            if (modeConfig != null && modeConfig.ScoreLayout != HudScoreLayout.Inherit)
+                return modeConfig.ScoreLayout == HudScoreLayout.PerDomain;
 
-            return modeConfig.ScoreLayout switch
-            {
-                HudScoreLayout.PerDomain => wired,
-                HudScoreLayout.PerPlayer => false,
-                _ => wired,
-            };
+            return FindAnyObjectByType<MultiplayerDomainGamesController>(FindObjectsInactive.Include) != null;
         }
 
         protected override void OnMiniGameTurnEnd()
diff --git a/Docs/GAMECANVAS.md b/Docs/GAMECANVAS.md
index bcb621f46..57e2a1cf8 100644
--- a/Docs/GAMECANVAS.md
+++ b/Docs/GAMECANVAS.md
@@ -180,19 +180,22 @@ part of GameCanvas, so pointing a scene at its config never creates an override
 prefab. Consumers call `GameModeSceneConfig.Resolve()` instead of holding a serialized field —
 that is what keeps new inspector references off the canvas.
 
-**Every field is opt-in.** `ScoreLayout = Inherit` and an empty `EndGameStats` mean "behave exactly
-as before", so adding a config asset to a scene changes nothing until you set something. A scene
-with no config at all is unaffected. This is what makes it safe to migrate one mode at a time.
+**Every field is opt-in.** An empty `EndGameStats` falls through to the scene's own list and then
+to vessel-telemetry discovery, and `ScoreLayout = Inherit` derives the layout from the controller
+type — so most modes need no config asset at all, and a scene without one still behaves correctly.
+This is what makes it safe to migrate one mode at a time.
 
 | Field | Replaces | Neutral value |
 |---|---|---|
 | `EndGameStats` | `EventDrivenStatsProvider.statsToTrack` overridden per scene | empty → scene list, then vessel-telemetry discovery |
-| `ScoreLayout` | the fact that the *only* way to get per-player cards was to ship a canvas without domain wiring | `Inherit` → decide from prefab wiring, as today |
+| `ScoreLayout` | the fact that the *only* way to get per-player cards was to ship a canvas without domain wiring | `Inherit` → per-domain if the scene's controller is a `MultiplayerDomainGamesController`, else per-player |
 
 `ScoreLayout` is the field that actually unblocks the merge. Today the two canvas forks differ in
-whether the domain containers exist at all, and `MultiplayerHUD` picks its layout from whether they
-happen to be wired. A single unified canvas carries the superset, so the wiring is always present
-and the choice has to become data.
+whether the domain containers exist at all, and `MultiplayerHUD` picked its layout from whether they
+happened to be wired — shipping a canvas *without* the wiring was the only way to get per-player
+cards, which is precisely why a second prefab had to exist. A single unified canvas always carries
+the wiring, so that signal is gone and the choice moves to the controller type, overridable per
+mode by this field.
 
 **Net effect:** a brand-new game-mode scene can drop GameCanvas in, add one `GameModeSceneConfig`
 object, and the Ready button, Play Again, stat list and score layout all work with no inspector
@@ -202,62 +205,134 @@ wiring on the canvas itself.
 
 ## 6. What to do in Unity
 
-### Step 0 — the tooling does NOT do this for you
+**Decision: `CORE/GameCanvas.prefab` is the survivor. `GameCanvas-HexRace.prefab` gets deleted.**
 
-**FrogletTools ▸ Game Modes ▸ Game Mode Prefab Kit ▸ Validate** is a *read-only report*. It tells
-you which scenes carry unapplied overrides on a shared prefab, and it has an **Ignore** button for
-scenes that are meant to differ. It does not merge prefabs and it does not apply overrides — an
-earlier automated "Consolidate" did, and it was removed: reverting a scene's overrides is a large,
-hard-to-review edit, and it addresses drift, not unification. Fix drift in Unity's own Overrides
-dropdown where you can see each change first.
+This is the cheaper direction, and by a wide margin. The base prefab's GUID is already referenced
+by **10 scenes**; the fork by **6**. Keeping the base means only those 6 scenes need their canvas
+re-instantiated — the other 10 keep working untouched. (Promoting the fork instead would have
+meant redoing 10.) The fork's extra content moves *into* the base, so nothing is lost.
 
-**Maelstrom is excluded by default** and should stay excluded. It is the tournament **hub**, not a
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._

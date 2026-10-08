# Branch archive: `claude/tool-codex-listing-vucv91`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-28 by Claude
- **Unmerged commits:** 3
- **Forked from:** `def29f5e1` (2026-08-25, Merge remote-tracking branch 'origin/bleeding-edge' into seven-sixteen)
- **Tip:** `9c4893ae1`
- **Files touched (17):**
  - `Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs`
  - `Assets/_Scripts/Data/Enums/CodexKingdom.cs`
  - `Assets/_Scripts/Editor/Codex/CodexHarvester.cs`
  - `Assets/_Scripts/Editor/Codex/CodexImageBaker.cs`
  - `Assets/_Scripts/Editor/Codex/CodexVariantSubject.cs`
  - `Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs`
  - `Assets/_Scripts/Editor/Codex/CodexWindow.cs`
  - `Assets/_Scripts/Editor/Codex/ToyCodexHarvester.cs`
  - `Assets/_Scripts/Editor/Codex/ToyCodexHarvester.cs.meta`
  - `Assets/_Scripts/Editor/Codex/ToyPortraitBuilder.cs`
  - `Assets/_Scripts/Editor/Codex/ToyPortraitBuilder.cs.meta`
  - `Assets/_Scripts/Editor/FrogletTools/FrogletEditorPalette.cs`
  - `Assets/_Scripts/ScriptableObjects/Codex/CodexSO.cs`
  - `CLAUDE.md`
  - `Docs/CODEX.md`
  - `Docs/TOOLING.md`
  - `Docs/ToySystem/ARCHITECTURE.md`

### `a5020e856` — feat(codex): drill-down layout — glowing kingdom tabs, browse grid, DETAILS/EDIT entry page

_Claude, 2026-08-28 17:22:00 +0000_

```text
The two-pane list-plus-inspector layout is retired. The window is now a
drill-down:

  ┌ Banner
  ├ KINGDOM TABS  — one glowing tab per class (Ethirions / Flora / Fauna /
  │                 Tools), each in its kingdom's accent with a count pill.
  │                 Clicking a tab always lands on that kingdom's browse grid;
  │                 an open entry is closed, because the strip is "where am I"
  │                 and an entry page under the wrong lit tab is a lie.
  ├ Toolbar       — search (scoped to the lit tab) + Scan & Merge / Bake
  │                 Missing / Bake All / Validate / bake size / Select Asset.
  ├ BODY          — the lit kingdom as a grid of illustrated cards (accent
  │                 top-stripe, portrait, name, flag pill; grouped by category
  │                 where the kingdom divides), or — once a card is clicked —
  │                 that entry's PAGE:
  │                   back button · title · warnings
  │                   DETAILS tab — the page as a reader meets it: hero
  │                     portrait, tagline + description, facts as plain rows,
  │                     the variant grid with click-through detail.
  │                   EDIT tab — everything a curator changes: image & pose &
  │                     bake, identity, copy, discovery, fact editing with the
  │                     AUTO/MINE detach machinery, and MANAGE (move within
  │                     group, duplicate, delete).
  └ Footer        — unchanged counts.

The split is posture, not paranoia: reading and editing want opposite layouts,
and a page that interleaves sliders with prose serves neither. EDIT is gold on
every page — the palette's "yours" colour, the one the COPY section already
wears — so which mode you are in reads from across the room; DETAILS wears the
entry's own accent.

The glow tab is a PALETTE widget (FrogletEditorPalette.GlowTab), per the house
rule that a widget a window lacks is added to the palette rather than
hand-rolled: soft layered halo (three expanding falling-alpha shells — IMGUI
has no blur, but stacked borders read as one at editor sizes), accent fill and
a lit underline when selected, a quiet outline otherwise, optional count pill,
repaint-only.

Everything behavioural is carried over verbatim, relocated: Scan & Merge /
Bake / Validate bodies untouched; + New entry now adds straight into the lit
kingdom (no GenericMenu — the tab already answers "which kingdom"); Duplicate /
Delete / Move live in the entry page's MANAGE section; a new hand-authored
entry opens directly onto its EDIT tab, since a blank page has nothing to look
at. ChangeKingdom now also moves the lit tab with the entry. The variant grid
column math reads position.width (the page is the whole window; the old
formula subtracted the deleted list pane's width and would not have compiled).

Verification status: statically reviewed only, NOT compiled or run — no Unity
in this environment. Cross-partial symbol check passed (every method each
partial calls exists in the other), brace balance verified mechanically,
check_conditional_compilation.py passes. Needs an editor pass: open the Codex,
click through all four tabs, open an entry from each kingdom, flip DETAILS /
EDIT, and confirm Move/Duplicate/Delete still work from MANAGE.
```

```text
 Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs          | 267 ++++++++++++++++------
 Assets/_Scripts/Editor/Codex/CodexWindow.cs                 | 501 +++++++++++++++++++++++-------------------
 Assets/_Scripts/Editor/FrogletTools/FrogletEditorPalette.cs |  55 +++++
 Docs/CODEX.md                                               |  12 +-
 Docs/TOOLING.md                                             |   2 +-
 5 files changed, 539 insertions(+), 298 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1145 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs b/Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs
index 0f864986d..15f9fb109 100644
--- a/Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs
+++ b/Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs
@@ -8,75 +8,78 @@ using UnityEngine;
 namespace CosmicShore.Editor.Codex
 {
     /// <summary>
-    /// The right-hand pane: everything about one entry, editable.
+    /// The ENTRY PAGE: the whole window body once a browse card is clicked. A back row, the
+    /// entry's name and warnings, then two glow tabs — <b>DETAILS</b> (the page as a reader
+    /// meets it: portrait, prose, facts, the variant grid) and <b>EDIT</b> (everything a curator
+    /// changes: identity, copy, pose and bake, fact editing, ordering and the delete). The split
+    /// is posture, not paranoia: reading and editing want opposite layouts, and a page that
+    /// interleaves sliders with prose serves neither.
     ///
     /// <para>Drawn by hand rather than through a SerializedObject because entries live in plain
-    /// <c>List&lt;CodexEntry&gt;</c> fields that the list view re-sorts and re-filters every frame,
-    /// so there is no stable <c>Array.data[i]</c> path to bind to. The cost is that every mutation
-    /// has to record undo and dirty the asset itself — done once, in
-    /// <see cref="DrawDetail"/>.</para>
+    /// <c>List&lt;CodexEntry&gt;</c> fields that the browse grid re-sorts and re-filters every
+    /// frame, so there is no stable <c>Array.data[i]</c> path to bind to. The cost is that every
+    /// mutation has to record undo and dirty the asset itself — done once, in
+    /// <see cref="DrawEntryPage"/>.</para>
     /// </summary>
     public partial class CodexWindow
     {
         const float PreviewSize = 168f;
+        const float HeroSize = 210f;
+
+        const float EntryTabWidth = 110f;
+        const float EntryTabHeight = 26f;
 
         /// <summary>Which variant card is open, as "entryId/label". Null = none.</summary>
         string _selectedVariant;
-        bool _showStats = true;
-        bool _showVariants = true;
 
-        void DrawDetail()
+        /// <summary>0 = DETAILS (read), 1 = EDIT (change).</summary>
+        int _entryTab;
+
+        void DrawEntryPage(CodexEntry entry)
         {
-            using (new EditorGUILayout.VerticalScope())
+            DrawEntryHeader(entry);
+            DrawEntryTabs(entry);
+
+            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll,
+                       GUILayout.ExpandHeight(true)))
             {
-                var entry = Selected;
-                if (entry == null)
-                {
-                    GUILayout.Space(20f);
-                    EditorGUILayout.HelpBox(
-                        "Select an entry on the left, or run Scan & Merge to harvest the project.",
-                        MessageType.Info);
-                    GUILayout.FlexibleSpace();
-                    return;
-                }
+                _detailScroll = scroll.scrollPosition;
 
-                using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
+                // Recorded BEFORE the controls run. IMGUI edits the object in place as it draws,
+                // so a record taken after EndChangeCheck snapshots the state the user was trying
+                // to undo TO. Unity merges same-name records within a frame, so this does not
+                // flood the stack.
+                Undo.RecordObject(_codex, "Edit codex entry");
+                EditorGUI.BeginChangeCheck();
+
+                if (_entryTab == 0) DrawDetailsTab(entry);
+                else DrawEditTab(entry);
+                GUILayout.Space(10f);
+
+                if (EditorGUI.EndChangeCheck())
                 {
-                    _detailScroll = scroll.scrollPosition;
-
-                    // Recorded BEFORE the controls run. IMGUI edits the object in place as it
-                    // draws, so a record taken after EndChangeCheck snapshots the state the user
-                    // was trying to undo TO. Unity merges same-name records within a frame, so
-                    // this does not flood the stack.
-                    Undo.RecordObject(_codex, "Edit codex entry");
-                    EditorGUI.BeginChangeCheck();
-
-                    DrawHeader(entry);
-                    DrawImageBlock(entry);
-                    DrawIdentity(entry);
-                    DrawCopy(entry);
-                    DrawDiscovery(entry);
-                    DrawStats(entry);
-                    DrawVariants(entry);
-                    GUILayout.Space(10f);
-
-                    if (EditorGUI.EndChangeCheck())
-                    {
-                        EditorUtility.SetDirty(_codex);
-                        FrogletToolChangeLedger.Record(ToolName, AssetPath);
-                    }
+                    EditorUtility.SetDirty(_codex);
+                    FrogletToolChangeLedger.Record(ToolName, AssetPath);
                 }
             }
         }
 
-        void DrawHeader(CodexEntry entry)
+        // ── Header + tabs ────────────────────────────────────────────────────────
+
+        void DrawEntryHeader(CodexEntry entry)
         {
             var accent = entry.ResolveAccent(AccentFor(entry.Kingdom));
 
             GUILayout.Space(2f);
             using (new EditorGUILayout.HorizontalScope())
             {
-                GUILayout.Space(6f);
+                GUILayout.Space(10f);
+                if (FrogletEditorPalette.ColorButton($"◀ {HeadingFor(entry.Kingdom)}",
+                        AccentFor(entry.Kingdom), 152f, 22f,
+                        "Back to the kingdom's browse grid.", outline: true))
+                    _deferred = CloseEntry;
+
+                GUILayout.Space(10f);
                 GUILayout.Label(entry.DisplayName, FrogletEditorPalette.Title);
                 GUILayout.FlexibleSpace();
 
@@ -91,12 +94,12 @@ namespace CosmicShore.Editor.Codex
 
                 var pill = GUILayoutUtility.GetRect(126f, 18f, GUILayout.Width(126f), GUILayout.Height(18f));
                 FrogletEditorPalette.StatusPill(pill, HeadingFor(entry.Kingdom), accent);
-                GUILayout.Space(6f);
+                GUILayout.Space(10f);
             }
 
             using (new EditorGUILayout.HorizontalScope())
             {
-                GUILayout.Space(6f);
+                GUILayout.Space(12f);
                 GUILayout.Label(entry.Id, FrogletEditorPalette.Subtitle);
             }
 
@@ -110,8 +113,150 @@ namespace CosmicShore.Editor.Codex
                     "entry, so its facts and image cannot be re-derived. It is kept, never " +
                     "auto-deleted.",
                     MessageType.Warning);
+        }
+
+        void DrawEntryTabs(CodexEntry entry)
```

</details>

### `f50fb5222` — fix(codex): hull icons were the skimmer sphere; declines now name their gate; kingdom is TOYS

_Claude, 2026-08-28 21:51:13 +0000_

```text
Three things from the first bake.

VESSELS DREW AS SPHERES AND SMEARS. A vessel prefab's biggest renderer by far
is its SKIMMER — a builtin sphere scaled 15-60x — so harvesting every renderer
fits the icon to the skimmer and crushes the ship to an invisible speck. That
is a bug the toybox found and fixed once already
(VesselModelBuilder.IsHull, whose comment says exactly this), and these icons
re-acquired it by harvesting unfiltered: the codex's own hint list drops trails
and VFX and says nothing about skimmers, jets or forcefields. IsHull is now
public, HarvestModel takes a renderer filter, and the hull path passes it — so
the codex and the lava lamp give the same answer by construction. General rule:
when the lava lamp already draws the thing you are about to draw, reuse its
PREDICATE, because the fixes it accumulated are not visible from outside.

PAINTINGS BAKED EMPTY, AND THE ERROR NAMED NOTHING. I could not reproduce it:
compiling the shipped PaintingStrokeToolkit + PaintingPresetLibrary +
PaintingDefinitionSO + CodexVariantSubject against faithful stubs and running
every one of the sixteen gallery presets AT ITS ASSET'S OWN preset id and size
builds geometry in all sixteen (1-226 strokes, 96-6,776 verts, real bounds).
So the geometry path is proven and the null is upstream of it — which means the
honest fix is to stop guessing and make the code say which gate fired.
CodexVariantSubject.Build now reports WHY it declined (no strokes, no points,
zero extent, no hull renderers, wrong config type, nothing wired) and the baker
prints that instead of "nothing to photograph", which named the variant and
nothing about the cause. A negative control drives all four decline paths and
asserts each names its gate — a gate nobody has watched fail is not a gate.

THE KINGDOM IS "TOYS", NOT "TOOLS". Renamed throughout: the enum member, the
list and its accessor, both editor files, every player-facing string. The
original reasoning was the Ethirion precedent — a player-facing name for an
internal one — and it was wrong twice: FrogletTools are EDITOR tools, so a
kingdom called Tool authored by a tool read as a tool listing tools; and unlike
a crystal, a Toy needed no translation at all, since Toy is already the
fundamental's name, the toybox's name and ToyDefinitionSO's name. A
player-facing rename is only worth making when the INTERNAL name is the one
that would confuse a player. The serialized field carries
[FormerlySerializedAs("tools")] — correct here for the reason it is usually
wrong, since the rename changed the word and not the meaning, and without it
Unity drops every authored toy page on the next load in silence. The enum's
numeric value is unchanged (3), so no serialized kingdom moves.

Verification: the painting geometry is PROVEN by compiling and running the
shipped code (16/16 presets, real asset values), and the decline diagnostics by
a negative control (4/4 gates named). Both harnesses are scratch, not
committed. The hull fix and the rename are statically reviewed only — no Unity
in this environment. Next editor pass: Scan & Merge, then Bake Missing; the
hulls should be ships, and any painting that still fails will say why.
```

```text
 Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs                 |  9 +++-
 Assets/_Scripts/Data/Enums/CodexKingdom.cs                            | 17 +++---
 Assets/_Scripts/Editor/Codex/CodexHarvester.cs                        | 12 ++---
 Assets/_Scripts/Editor/Codex/CodexImageBaker.cs                       | 44 ++++++++++------
 Assets/_Scripts/Editor/Codex/CodexVariantSubject.cs                   | 91 ++++++++++++++++++++++++++++-----
 Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs                    |  6 +--
 Assets/_Scripts/Editor/Codex/CodexWindow.cs                           | 14 ++---
 .../Editor/Codex/{ToolCodexHarvester.cs => ToyCodexHarvester.cs}      | 38 +++++++-------
 .../Codex/{ToolCodexHarvester.cs.meta => ToyCodexHarvester.cs.meta}   |  0
 .../Editor/Codex/{ToolPortraitBuilder.cs => ToyPortraitBuilder.cs}    |  8 +--
 .../Codex/{ToolPortraitBuilder.cs.meta => ToyPortraitBuilder.cs.meta} |  0
 Assets/_Scripts/ScriptableObjects/Codex/CodexSO.cs                    | 30 ++++++-----
 CLAUDE.md                                                             |  2 +-
 Docs/CODEX.md                                                         | 48 +++++++++++------
 Docs/TOOLING.md                                                       |  6 +--
 Docs/ToySystem/ARCHITECTURE.md                                        |  6 +--
 16 files changed, 221 insertions(+), 110 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1351 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs b/Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs
index 181610aa9..88dbe2dcf 100644
--- a/Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs
+++ b/Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs
@@ -123,7 +123,14 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>Whether this renderer is part of the ship hull we want to display.</summary>
-        static bool IsHull(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer)
+        /// <summary>
+        /// The hull test itself, public so anything harvesting a ship's meshes gets the SAME
+        /// answer this builder gives - the codex bakes vessel icons through its own editor-safe
+        /// harvester and must not re-derive "which renderers are the ship", or it re-acquires the
+        /// bug this filter exists to prevent (the skimmer sphere is 15-60x the hull and dominates
+        /// the fit, crushing the ship to a speck).
+        /// </summary>
+        public static bool IsHull(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer)
         {
             if (mesh && PrimitiveMeshNames.Contains(mesh.name)) return false;
             return !ToyModelBuilder.AnyAncestorNameContains(node, prefabRoot, NonHullNameHints);
diff --git a/Assets/_Scripts/Data/Enums/CodexKingdom.cs b/Assets/_Scripts/Data/Enums/CodexKingdom.cs
index b2e7d0546..15b25a4f4 100644
--- a/Assets/_Scripts/Data/Enums/CodexKingdom.cs
+++ b/Assets/_Scripts/Data/Enums/CodexKingdom.cs
@@ -7,17 +7,20 @@ namespace CosmicShore.Data
     /// The top-level division of the in-game encyclopedia.
     ///
     /// <para><b>Ethirion</b> is the player-facing name for a CRYSTAL. <b>Flora</b> and
-    /// <b>Fauna</b> together are the player-facing <i>Ecology</i>. <b>Tool</b> is the
-    /// player-facing name for a <i>Toy</i> - the freestyle stations you fly into. The split is
+    /// <b>Fauna</b> together are the player-facing <i>Ecology</i>. <b>Toy</b> is a freestyle
+    /// station you fly into - and it is called a Toy here because that is what the platform
+    /// calls it: this is the ONE kingdom whose name needs no translation. The split is
     /// deliberately the one a player can see - what you collect, what lives, what you play with -
     /// and NOT an implementation split: a crystal's impactor class (elemental / omni / team)
     /// decides who may collect it, which is mechanics, not encyclopedia content, and is never
     /// surfaced here.</para>
     ///
-    /// <para><b>Naming hazard.</b> "Tool" here is a thing in the GAME. It has nothing to do with
-    /// <c>FrogletTools</c>, which are editor tools. The codebase keeps calling the game object a
-    /// <c>Toy</c> (the fundamental) precisely so the two never collide in code; only the
-    /// player-facing surface says "Tool".</para>
+    /// <para><b>Named Toy, deliberately, everywhere.</b> An earlier pass called this kingdom
+    /// "Tool" on the player-facing surface, on the Ethirion precedent. That was a mistake worth
+    /// recording: <c>FrogletTools</c> are EDITOR tools, so the word already means something else
+    /// in this repo, and a kingdom called Tool authored by a tool called the Codex reads as a
+    /// tool listing tools. Toy is also simply what the thing is - CLAUDE.md's fundamental, the
+    /// toybox, <c>ToyDefinitionSO</c> - so there was never a translation to make.</para>
     /// </summary>
     public enum CodexKingdom
     {
@@ -34,6 +37,6 @@ namespace CosmicShore.Data
         /// A <b>Toy</b> - a freestyle station you fly into. No score, no end condition, nothing
         /// on a clock; a thing to play with indefinitely.
         /// </summary>
-        Tool = 3,
+        Toy = 3,
     }
 }
diff --git a/Assets/_Scripts/Editor/Codex/CodexHarvester.cs b/Assets/_Scripts/Editor/Codex/CodexHarvester.cs
index 4a86c5d59..79287c346 100644
--- a/Assets/_Scripts/Editor/Codex/CodexHarvester.cs
+++ b/Assets/_Scripts/Editor/Codex/CodexHarvester.cs
@@ -14,11 +14,11 @@ namespace CosmicShore.Editor.Codex
 {
     /// <summary>
     /// Reads the project and produces codex entries - every ethirion (crystal), every ecology
-    /// species (flora and fauna) and every tool (freestyle toy) - then MERGES them into the live
+    /// species (flora and fauna) and every toy (freestyle station) - then MERGES them into the live
     /// <see cref="CodexSO"/> under the field-ownership contract documented on
     /// <see cref="CodexEntry"/>.
     ///
-    /// <para>The tool pass lives in <see cref="ToolCodexHarvester"/> because it reads a different
+    /// <para>The toy pass lives in <see cref="ToyCodexHarvester"/> because it reads a different
     /// KIND of asset - a toy has no prefab, it is built at runtime from its definition - but it
     /// merges through the same <see cref="MergeList"/> here, so there is exactly one
     /// implementation of the contract however many kingdoms exist.</para>
@@ -57,11 +57,11 @@ namespace CosmicShore.Editor.Codex
 
             MergeList(codex, codex.Ethirions, BuildEthirionEntries(report), report);
             MergeList(codex, codex.Ecology, BuildEcologyEntries(usage, report), report);
-            MergeList(codex, codex.Tools, ToolCodexHarvester.BuildToolEntries(report), report);
+            MergeList(codex, codex.Toys, ToyCodexHarvester.BuildToyEntries(report), report);
 
             FlagOrphans(codex.Ethirions, report);
             FlagOrphans(codex.Ecology, report);
-            FlagOrphans(codex.Tools, report);
+            FlagOrphans(codex.Toys, report);
 
             if (report.AnyChange) EditorUtility.SetDirty(codex);
             return report;
@@ -571,7 +571,7 @@ namespace CosmicShore.Editor.Codex
         }
 
         /// <summary>
-        /// The one place a codex id is minted. Shared with <see cref="ToolCodexHarvester"/> so a
+        /// The one place a codex id is minted. Shared with <see cref="ToyCodexHarvester"/> so a
         /// second kingdom cannot invent a second id convention - the ids are what a save file and
         /// the merge both key on.
         /// </summary>
@@ -596,7 +596,7 @@ namespace CosmicShore.Editor.Codex
 
         /// <summary>
         /// Append a harvested row, or nothing at all when there is no value. Shared with
-        /// <see cref="ToolCodexHarvester"/>: "a fact we do not have is a row we do not draw" has
+        /// <see cref="ToyCodexHarvester"/>: "a fact we do not have is a row we do not draw" has
         /// to hold identically in every kingdom, or one of them starts printing blanks.
         /// </summary>
         internal static void Add(List<CodexStat> stats, string label, string value)
diff --git a/Assets/_Scripts/Editor/Codex/CodexImageBaker.cs b/Assets/_Scripts/Editor/Codex/CodexImageBaker.cs
index 82a04f971..6e281e91a 100644
--- a/Assets/_Scripts/Editor/Codex/CodexImageBaker.cs
+++ b/Assets/_Scripts/Editor/Codex/CodexImageBaker.cs
@@ -37,8 +37,8 @@ namespace CosmicShore.Editor.Codex
         public const string OutputFolder = "Assets/_Graphics/Codex";
 
         // Three subjects, and the third is unlike the other two: an ethirion and a lifeform are
-        // AUTHORED objects this photographs, while a TOOL has no prefab at all and is DRAWN from
-        // the same shape vocabulary the toy is built from at runtime. See BuildToolPortrait.
+        // AUTHORED objects this photographs, while a TOY has no prefab at all and is DRAWN from
+        // the same shape vocabulary the toy is built from at runtime. See BuildToyPortrait.
 
         /// <summary>Below this fraction of visible pixels a render is treated as failed.</summary>
         const float MinimumCoverage = 0.004f;
@@ -58,12 +58,12 @@ namespace CosmicShore.Editor.Codex
 
         /// <summary>
         /// Whether this entry can be illustrated at all. Asked here rather than at each call site
-        /// because "a prefab, OR a tool definition to draw from" is one rule and a second copy of
+        /// because "a prefab, OR a toy definition to draw from" is one rule and a second copy of
         /// it is how a kingdom ends up with a Bake button that does nothing.
         /// </summary>
         public static bool CanBake(CodexEntry entry) =>
             entry != null &&
-            (entry.SourcePrefab || (entry.Kingdom == CodexKingdom.Tool && entry.SourceConfig));
+            (entry.SourcePrefab || (entry.Kingdom == CodexKingdom.Toy && entry.SourceConfig));
 
         /// <summary>
         /// Whether this VARIANT has art of its own worth baking. Most do not, and that is by
@@ -167,14 +167,16 @@ namespace CosmicShore.Editor.Codex
             Bounds bounds;
             List<Object> temporaries;
 
+            string declined = null;
             if (variant != null)
             {
                 temporaries = new List<Object>();
-                model = CodexVariantSubject.Build(entry, variant, flat, temporaries, out bounds);
+                model = CodexVariantSubject.Build(entry, variant, flat, temporaries, out bounds,
+                    out declined);
```

</details>

### `9c4893ae1` — docs(codex): finish the Tool→Toy rename in prose

_Claude, 2026-08-28 21:53:48 +0000_

```text
Three references survived the rename because they were prose rather than
code: two named the renamed builder (ToolPortraitBuilder → ToyPortraitBuilder)
and one was a CodexKingdom.Tool usage example that no longer compiles.

A rename that leaves the docs behind is worse than no rename — the next
reader greps the name the doc gave them and finds nothing.
```

```text
 CLAUDE.md     |  2 +-
 Docs/CODEX.md | 10 +++++-----
 2 files changed, 6 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 3d9aa184d..ccda0b02b 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -1102,7 +1102,7 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
 | `BENCHMARK_TOOL.md` | `_Scripts/Utility/PerformanceBenchmark/` | Performance Benchmark tool guide (tabs, score/hints, sweep, Load Time Insights, customization) |
 | `DIAGNOSTICS.md` | `Docs/` | The **FrogletTools ▸ Diagnostics** family: the editor **Crash Detector** (off-thread error journal + heartbeat sentinel; abnormal exits — crashes, PC faults, hangs-then-kills — are reported on the next launch from the journal + Unity's own `Editor-prev.log`) and the **Bug Ledger** (the team's live bug list: every distinct red-error signature auto-files ONE issue file into the gitignored `BugLedger/local/` store — version control sees ledger data only when it is explicitly staged & pushed from the tool's Stage & Push tab, which commits ledger paths ONLY; a fix is only believed once the game validates it — clean play/editor sessions for captured errors, a clean full re-run for tool-filed findings — then archived to `BugLedger/local/resolved/`; a recurrence reopens the issue as a regression). The signature core is the runtime-safe `CosmicShore.Utility.BugSignature`, shared with the planned in-game reporter. Also the opt-in **Compile Timing** tab (compile + domain-reload seconds per edit, and which assemblies rebuilt — the measurement behind `Docs/ASSEMBLY_SPLIT.md`). **Read before touching anything under `Assets/_Scripts/Editor/Diagnostics/`, `BugSignature`, or the `BugLedger/` store — and before wiring an auditor's findings into the ledger.** |
 | `TOOLING.md` | `Docs/` | **The editor-tooling convention.** One menu root (`FrogletTools/`), one auto-discovering board (Froglet Master Tool), one shared palette, and — for any tool that WRITES assets — the ship contract: record what you wrote, draw `FrogletToolShipPanel` (Validate & Push / Retire Tool), because a tool's output is the deliverable and it lands in the working tree, not the branch. **Read before adding ANY `[MenuItem]`** — a tool outside `FrogletTools/` is flagged as non-conforming by the board itself. |
-| `CODEX.md` | `Docs/` | **The in-game encyclopedia's data layer** — every **Ethirion** (the player-facing name for a crystal: Charge / Mass / Space / Time / Omni), all of **Ecology** (16 flora species, 6 fauna species) and every **Toy** (the freestyle stations), as ONE `CodexSO` at `Assets/Resources/Codex.asset` the runtime UI loads with no per-scene wiring. **An entry is a PAGE, not an asset**: one per species with its four elements as variants inside, one per element family, one per toy with the choices it offers inside — 33 pages over 88 lifeform configs, the crystal set and 6 toy definitions, because the player's question is "what is a Shark", not "what is a Shark Mass". A crystal's impactor class (elemental / omni / team) is deliberately absent: it decides who may collect one, which the palette already says in-world, and is mechanics rather than encyclopedia content. Authored by **FrogletTools > Interface > Codex**, whose load-bearing property is that **Scan & Merge is always safe to run** — a field-ownership contract splits every field into harvester-owned (wiring, harvested facts), filled-only-when-empty (name, image, accent) and never-touched (all prose, ordering, discovery, preview pose), so a generated encyclopedia still has room for a writer. Three findings generalise: **species are grouped by PREFAB, not by asset name** (the fauna set carries a `WormColonyFaunaConfig` beside four `Worm Colony <Element>` assets, so a name-prefix grouping invents a fifth species — the prefab is the thing the player meets, and the display name is a MAJORITY vote among the configs sharing it); a **stat is a formatted string, never a typed number**, because a codex row is prose and a typed value forces the UI to carry a formatter per stat kind; and an entry whose source asset vanished is reported as an **orphan and never auto-deleted**, since a tool that answers a mid-refactor by deleting hand-written body copy is one nobody runs twice. Images bake to `Assets/_Graphics/Codex/` off the prefab ASSET so nothing ever `Awake`s, with **alpha recovered from two opaque renders** (black and white, `a = 1 - (white - black)`) rather than trusting the render target's alpha channel, which is pipeline-dependent, and a coverage check that falls back to a lit silhouette rather than writing a blank PNG when a gameplay shader reading per-frame globals renders empty. **A FLORA IS ASKED TO DRAW ITSELF**: every flora prefab carries exactly ONE prism (the seed) because a plant is a growth RULE, not a model, so harvesting its meshes photographs a box - `Flora.TryPreviewGrowth` runs the rule in the abstract and `CellMiniatureBuilder.BuildFromLays` turns the poses into one mesh, the same answer the lava lamp's Lifeform bench reached (`FloraIconBuilder`), reached through the same two calls rather than a second copy. That required **`PhyllotacticFlora.TryPreviewGrowth`**, which the 8 Hesperides forms had been missing (so they were anonymous spheres on the bench too); it mirrors `SeedTips`/`DecideStep`/`DecideWhorl` with three substitutions and only three - a caller-seeded `System.Random` (the contract forbids touching `UnityEngine.Random`), a local claim list instead of `PrismSpatialIndex`, and a node that becomes a tip immediately because there are no frames - while `StemPrismScale` / `LeafPrismScale` stay the LIVE ones so a preview cannot drift from the plant on taper, cross-section or jitter. Two corollaries: **fauna are harvested normally** (unlike flora they ARE authored in place - a shark's wings and danger rods sit at real offsets), and **a COLONY's body is its MEMBERS** (the worm colony root carries no mesh and no nested instance, so the baker lays a chain of its head/body/tail prefabs at the colony's own spacing). **The TOYS kingdom differs from the other two in two load-bearing ways.** (1) **A toy has no prefab** — it is built at runtime by `ToyFactory` from its `ToyDefinitionSO` — so its entry carries `SourceConfig` where the others carry `SourcePrefab` (`CodexEntry.HasSource` is the one orphan question both answer), and its portrait is **DRAWN** rather than photographed: `ToolPortraitBuilder` renders the toy's own `ToyEmblem` grammar (core + satellites inside the switch ring) off `ToyEmblem`'s published constants, so retuning the emblem retunes the portraits. Calling `ToyFactory`'s builders instead is wrong twice over in an editor pass — `AddSphereBody` discards its collider with `Object.Destroy` (illegal in edit mode, logs per bake) and `AddRingBody` attaches a live `ToyIdleSpin` plus an unowned static mesh — so the geometry is built and owned outright, per the rule that a bake wakes no gameplay component. (2) **Every toy declares a CATEGORY, and the categories are FUNDAMENTALS**: `ToyCategory` divides the toybox by what a toy CHANGES — **Pilot** (you: Vessel Changer, Domain Changer), **World** (where you are: Cell Selector, Wanderway), **Creation** (what it leaves behind: Connect the Dots, Lifeform Matrix) — which is the only division that stays true as toys are added, and a toy fitting none of them is the signal to have the fundamentals conversation rather than to widen the enum. `ToyDefinitionSO.Category` is **abstract and declared in code**, never serialized, because a toy's category is a property of what it DOES and an authored field can disagree with the behaviour under it; abstract means a new toy cannot be added without answering. It reaches the codex as `CodexEntry.Group`, a harvester-owned sub-heading WITHIN a kingdom (general — any kingdom that divides gets it, one that does not leaves it empty), carrying an ordering prefix (`1 · Pilot`) so the sections read lightest-touch-to-heaviest rather than alphabetically. Toy facts are read **per TYPE by pattern match, not by field name** — the opposite trade from the ecology probes and the right one, since a rename becomes a compile error — and the switch's default arm **warns**, so adding a toy without teaching the codex what it offers is noisy. `Tagline` moved from never-touched to filled-only-when-empty to carry a toy's own authored one-liner, which is safe by that tier's definition: a blank field has no human value to protect. **A VARIANT is drawn as a card in a grid under its entry, and most variant icons are NOT baked** — the governing question is "is this variant a distinct object?", and for most the answer is no: a species' ELEMENT resolves to that element's own ethirion image (one picture, not 123 copies of it), a DOMAIN draws its `AccentColor` as a chip (a PNG of a flat colour says nothing), and a KINGDOM row falls back to the entry's portrait. Only a PAINTING (its strokes, and the one place this codex colours by domain — there the domains ARE the subject) and a HULL bake art, ~24 icons instead of ~150. `CodexSO.VariantImage` is the single resolver and lives on the CATALOG rather than on `CodexEntry` because the element step is a CROSS-KINGDOM lookup — resolved at draw time, so re-baking one ethirion updates every lifeform that drops it with nothing to re-scan. Two hull traps: **a hull icon must be FILTERED to the hull through `VesselModelBuilder.IsHull`** — a vessel's biggest renderer is its SKIMMER (a builtin sphere scaled 15-60x), so an unfiltered harvest fits the icon to the skimmer and crushes the ship to a speck, which is a bug the toybox had already fixed once and the first cut of these icons re-acquired (`IsHull` is now public and `HarvestModel` takes a renderer filter, so both paths agree by construction — when the lava lamp already draws the thing you are drawing, reuse its predicate); **five of the eight hulls are SKINNED**, so the harvester must walk `SkinnedMeshRenderer` too; and a hull bakes FLAT always, because the shared vessel graph is domain-tinted and reads per-frame globals, so the authored pass renders black and falls back anyway. Variant LABELS are disambiguated at the source (`Charge · <config>`) when a species carries several configs per element — not cosmetic, because the label is the key the merge matches on and `ToDictionary` throws on a duplicate; it had never fired only because no variant had ever carried an image. **A declined icon NAMES ITS GATE** (`CodexVariantSubject.Build` reports no-strokes / no-points / zero-extent / no-hull-renderers / wrong-config-type, and the baker prints it instead of "nothing to photograph", which named the variant and nothing about the cause), each gate covered by a negative control. The kingdom was briefly called **Tool** and was renamed to **Toy**: `FrogletTools` are EDITOR tools, so a kingdom called Tool authored by a tool read as a tool listing tools — and unlike a crystal, a Toy needed no player-facing translation at all, since it is already the fundamental's own name. *A player-facing rename is only worth making when the INTERNAL name is the one that would confuse a player.* **Read before adding a crystal, a lifeform species or a TOY, or before building any UI that lists them.** |
+| `CODEX.md` | `Docs/` | **The in-game encyclopedia's data layer** — every **Ethirion** (the player-facing name for a crystal: Charge / Mass / Space / Time / Omni), all of **Ecology** (16 flora species, 6 fauna species) and every **Toy** (the freestyle stations), as ONE `CodexSO` at `Assets/Resources/Codex.asset` the runtime UI loads with no per-scene wiring. **An entry is a PAGE, not an asset**: one per species with its four elements as variants inside, one per element family, one per toy with the choices it offers inside — 33 pages over 88 lifeform configs, the crystal set and 6 toy definitions, because the player's question is "what is a Shark", not "what is a Shark Mass". A crystal's impactor class (elemental / omni / team) is deliberately absent: it decides who may collect one, which the palette already says in-world, and is mechanics rather than encyclopedia content. Authored by **FrogletTools > Interface > Codex**, whose load-bearing property is that **Scan & Merge is always safe to run** — a field-ownership contract splits every field into harvester-owned (wiring, harvested facts), filled-only-when-empty (name, image, accent) and never-touched (all prose, ordering, discovery, preview pose), so a generated encyclopedia still has room for a writer. Three findings generalise: **species are grouped by PREFAB, not by asset name** (the fauna set carries a `WormColonyFaunaConfig` beside four `Worm Colony <Element>` assets, so a name-prefix grouping invents a fifth species — the prefab is the thing the player meets, and the display name is a MAJORITY vote among the configs sharing it); a **stat is a formatted string, never a typed number**, because a codex row is prose and a typed value forces the UI to carry a formatter per stat kind; and an entry whose source asset vanished is reported as an **orphan and never auto-deleted**, since a tool that answers a mid-refactor by deleting hand-written body copy is one nobody runs twice. Images bake to `Assets/_Graphics/Codex/` off the prefab ASSET so nothing ever `Awake`s, with **alpha recovered from two opaque renders** (black and white, `a = 1 - (white - black)`) rather than trusting the render target's alpha channel, which is pipeline-dependent, and a coverage check that falls back to a lit silhouette rather than writing a blank PNG when a gameplay shader reading per-frame globals renders empty. **A FLORA IS ASKED TO DRAW ITSELF**: every flora prefab carries exactly ONE prism (the seed) because a plant is a growth RULE, not a model, so harvesting its meshes photographs a box - `Flora.TryPreviewGrowth` runs the rule in the abstract and `CellMiniatureBuilder.BuildFromLays` turns the poses into one mesh, the same answer the lava lamp's Lifeform bench reached (`FloraIconBuilder`), reached through the same two calls rather than a second copy. That required **`PhyllotacticFlora.TryPreviewGrowth`**, which the 8 Hesperides forms had been missing (so they were anonymous spheres on the bench too); it mirrors `SeedTips`/`DecideStep`/`DecideWhorl` with three substitutions and only three - a caller-seeded `System.Random` (the contract forbids touching `UnityEngine.Random`), a local claim list instead of `PrismSpatialIndex`, and a node that becomes a tip immediately because there are no frames - while `StemPrismScale` / `LeafPrismScale` stay the LIVE ones so a preview cannot drift from the plant on taper, cross-section or jitter. Two corollaries: **fauna are harvested normally** (unlike flora they ARE authored in place - a shark's wings and danger rods sit at real offsets), and **a COLONY's body is its MEMBERS** (the worm colony root carries no mesh and no nested instance, so the baker lays a chain of its head/body/tail prefabs at the colony's own spacing). **The TOYS kingdom differs from the other two in two load-bearing ways.** (1) **A toy has no prefab** — it is built at runtime by `ToyFactory` from its `ToyDefinitionSO` — so its entry carries `SourceConfig` where the others carry `SourcePrefab` (`CodexEntry.HasSource` is the one orphan question both answer), and its portrait is **DRAWN** rather than photographed: `ToyPortraitBuilder` renders the toy's own `ToyEmblem` grammar (core + satellites inside the switch ring) off `ToyEmblem`'s published constants, so retuning the emblem retunes the portraits. Calling `ToyFactory`'s builders instead is wrong twice over in an editor pass — `AddSphereBody` discards its collider with `Object.Destroy` (illegal in edit mode, logs per bake) and `AddRingBody` attaches a live `ToyIdleSpin` plus an unowned static mesh — so the geometry is built and owned outright, per the rule that a bake wakes no gameplay component. (2) **Every toy declares a CATEGORY, and the categories are FUNDAMENTALS**: `ToyCategory` divides the toybox by what a toy CHANGES — **Pilot** (you: Vessel Changer, Domain Changer), **World** (where you are: Cell Selector, Wanderway), **Creation** (what it leaves behind: Connect the Dots, Lifeform Matrix) — which is the only division that stays true as toys are added, and a toy fitting none of them is the signal to have the fundamentals conversation rather than to widen the enum. `ToyDefinitionSO.Category` is **abstract and declared in code**, never serialized, because a toy's category is a property of what it DOES and an authored field can disagree with the behaviour under it; abstract means a new toy cannot be added without answering. It reaches the codex as `CodexEntry.Group`, a harvester-owned sub-heading WITHIN a kingdom (general — any kingdom that divides gets it, one that does not leaves it empty), carrying an ordering prefix (`1 · Pilot`) so the sections read lightest-touch-to-heaviest rather than alphabetically. Toy facts are read **per TYPE by pattern match, not by field name** — the opposite trade from the ecology probes and the right one, since a rename becomes a compile error — and the switch's default arm **warns**, so adding a toy without teaching the codex what it offers is noisy. `Tagline` moved from never-touched to filled-only-when-empty to carry a toy's own authored one-liner, which is safe by that tier's definition: a blank field has no human value to protect. **A VARIANT is drawn as a card in a grid under its entry, and most variant icons are NOT baked** — the governing question is "is this variant a distinct object?", and for most the answer is no: a species' ELEMENT resolves to that element's own ethirion image (one picture, not 123 copies of it), a DOMAIN draws its `AccentColor` as a chip (a PNG of a flat colour says nothing), and a KINGDOM row falls back to the entry's portrait. Only a PAINTING (its strokes, and the one place this codex colours by domain — there the domains ARE the subject) and a HULL bake art, ~24 icons instead of ~150. `CodexSO.VariantImage` is the single resolver and lives on the CATALOG rather than on `CodexEntry` because the element step is a CROSS-KINGDOM lookup — resolved at draw time, so re-baking one ethirion updates every lifeform that drops it with nothing to re-scan. Two hull traps: **a hull icon must be FILTERED to the hull through `VesselModelBuilder.IsHull`** — a vessel's biggest renderer is its SKIMMER (a builtin sphere scaled 15-60x), so an unfiltered harvest fits the icon to the skimmer and crushes the ship to a speck, which is a bug the toybox had already fixed once and the first cut of these icons re-acquired (`IsHull` is now public and `HarvestModel` takes a renderer filter, so both paths agree by construction — when the lava lamp already draws the thing you are drawing, reuse its predicate); **five of the eight hulls are SKINNED**, so the harvester must walk `SkinnedMeshRenderer` too; and a hull bakes FLAT always, because the shared vessel graph is domain-tinted and reads per-frame globals, so the authored pass renders black and falls back anyway. Variant LABELS are disambiguated at the source (`Charge · <config>`) when a species carries several configs per element — not cosmetic, because the label is the key the merge matches on and `ToDictionary` throws on a duplicate; it had never fired only because no variant had ever carried an image. **A declined icon NAMES ITS GATE** (`CodexVariantSubject.Build` reports no-strokes / no-points / zero-extent / no-hull-renderers / wrong-config-type, and the baker prints it instead of "nothing to photograph", which named the variant and nothing about the cause), each gate covered by a negative control. The kingdom was briefly called **Tool** and was renamed to **Toy**: `FrogletTools` are EDITOR tools, so a kingdom called Tool authored by a tool read as a tool listing tools — and unlike a crystal, a Toy needed no player-facing translation at all, since it is already the fundamental's own name. *A player-facing rename is only worth making when the INTERNAL name is the one that would confuse a player.* **Read before adding a crystal, a lifeform species or a TOY, or before building any UI that lists them.** |
 | `GAMECANVAS.md` | `Docs/` | GameCanvas as one source of truth: the two forked prefabs, the 1,734 identical-in-every-scene overrides that masked the prefab, the ~20 that are genuinely per-mode, the dangling cross-prefab refs, the code fixes that removed per-scene wiring, and the in-editor unification steps. **Read before touching any game-mode scene's canvas.** |
 | `unity-cli-setup.md` | `Docs/` | Unity CLI first-time setup (per-machine install, `unity doctor`, connecting to the open Editor, eval token hygiene, troubleshooting). Team-facing; the CLI is experimental and `unity --help` on the installed version is authoritative. |
 | `QA/` | `Docs/` | **The untested-development backlog and the QA loop.** `README.md` (the loop: scan merges → prioritised list → QA submits results → passes archived, failures become dev tasks), `QA_BACKLOG.md` (THE list — every item self-contained, with steps and explicit PASS/FAIL), `RESULTS/` (one file per test session), `DEV_TASKS.md` (failures, handoff-ready), `ARCHIVE.md` (passed, so a rescan can't resurrect them). Generated by the **`/qa-backlog`** skill; `Tools/QA/apply_results.py` applies submitted results. **If you land work you could not verify in the editor, say so plainly in the PR body's "Verification status" section — that is what the scan reads.** `UNITY_VERIFICATION_CHECKLIST.md` is the superseded hand-maintained predecessor. |
diff --git a/Docs/CODEX.md b/Docs/CODEX.md
index 7ab13a7ea..7d855a012 100644
--- a/Docs/CODEX.md
+++ b/Docs/CODEX.md
@@ -166,8 +166,8 @@ Three subjects are *photographed*, picked in order:
 
 And a fourth is **drawn**, because it cannot be photographed:
 
-4. **A tool is drawn from the vocabulary it is built from.** A toy has no prefab (§3.5), so
-   `ToolPortraitBuilder` renders its `ToyEmblem`: a **core** (what you are now) ringed by
+4. **A toy is drawn from the vocabulary it is built from.** A toy has no prefab (§3.5), so
+   `ToyPortraitBuilder` renders its `ToyEmblem`: a **core** (what you are now) ringed by
    **satellites** (what a pass would offer you), inside the **switch ring** — the platform's one
    word for "fly through this and something happens". Every proportion is read from `ToyEmblem`'s
    own published constants, so retuning the emblem retunes the portraits with it and the two can
@@ -284,12 +284,12 @@ foreach (var entry in codex.EntriesOf(CodexKingdom.Fauna))
                 variant.ResolveAccent(fallback));   // no image at all → draw the accent
 }
 
-// Tools divide inside their kingdom. Group is empty for a kingdom that does not divide, so
+// Toys divide inside their kingdom. Group is empty for a kingdom that does not divide, so
 // treat empty as "no sub-heading" rather than as a group called nothing.
-foreach (var group in codex.EntriesOf(CodexKingdom.Tool).GroupBy(e => e.Group))
+foreach (var group in codex.EntriesOf(CodexKingdom.Toy).GroupBy(e => e.Group))
 {
     AddHeading(group.Key);                        // "1 · Pilot" - strip the ordering prefix
-    foreach (var tool in group) AddCard(tool);
+    foreach (var toy in group) AddCard(toy);
 }
 ```
 
```

</details>

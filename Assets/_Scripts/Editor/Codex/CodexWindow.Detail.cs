using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// The right-hand pane: everything about one entry, editable.
    ///
    /// <para>Drawn by hand rather than through a SerializedObject because entries live in plain
    /// <c>List&lt;CodexEntry&gt;</c> fields that the list view re-sorts and re-filters every frame,
    /// so there is no stable <c>Array.data[i]</c> path to bind to. The cost is that every mutation
    /// has to record undo and dirty the asset itself — done once, at the bottom of
    /// <see cref="DrawDetail"/>.</para>
    /// </summary>
    public partial class CodexWindow
    {
        readonly HashSet<string> _expandedVariants = new();
        bool _showStats = true;
        bool _showVariants = true;

        void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                var entry = Selected;
                if (entry == null)
                {
                    EditorGUILayout.HelpBox(
                        "Select an entry on the left, or run Scan & Merge to harvest the project.",
                        MessageType.Info);
                    GUILayout.FlexibleSpace();
                    return;
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
                {
                    _detailScroll = scroll.scrollPosition;

                    // Recorded BEFORE the controls run. IMGUI edits the object in place as it
                    // draws, so a record taken after EndChangeCheck snapshots the state the user
                    // was trying to undo TO. Unity merges same-name records within a frame, so
                    // this does not flood the stack.
                    Undo.RecordObject(_codex, "Edit codex entry");
                    EditorGUI.BeginChangeCheck();

                    DrawHeader(entry);
                    DrawImageBlock(entry);
                    DrawIdentity(entry);
                    DrawCopy(entry);
                    DrawDiscovery(entry);
                    DrawStats(entry);
                    DrawVariants(entry);

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(_codex);
                        FrogletToolChangeLedger.Record(ToolName, AssetPath);
                    }
                }
            }
        }

        void DrawHeader(CodexEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(entry.DisplayName, FrogletEditorPalette.Title);
                GUILayout.FlexibleSpace();

                var pill = GUILayoutUtility.GetRect(120f, 18f, GUILayout.Width(120f), GUILayout.Height(18f));
                FrogletEditorPalette.StatusPill(pill, HeadingFor(entry.Kingdom),
                    entry.ResolveAccent(AccentFor(entry.Kingdom)));
            }

            GUILayout.Label(entry.Id, FrogletEditorPalette.Subtitle);

            if (entry.LockAutoHarvest)
                EditorGUILayout.HelpBox(
                    "Locked. Scan & Merge skips this entry entirely — nothing here is re-derived " +
                    "from the project.", MessageType.None);
            else if (!entry.SourcePrefab)
                EditorGUILayout.HelpBox(
                    "No source prefab. The scan could not find an asset behind this entry, so its " +
                    "facts and image cannot be re-derived. It is kept, never auto-deleted.",
                    MessageType.Warning);

            FrogletEditorPalette.HorizontalRule();
        }

        void DrawImageBlock(CodexEntry entry)
        {
            GUILayout.Label("Image", FrogletEditorPalette.SectionHeader);

            using (new EditorGUILayout.HorizontalScope())
            {
                var box = GUILayoutUtility.GetRect(128f, 128f, GUILayout.Width(128f), GUILayout.Height(128f));
                if (Event.current.type == EventType.Repaint)
                    FrogletEditorPalette.DrawCard(box, FrogletEditorPalette.Surface.WithAlpha(0.5f),
                        FrogletEditorPalette.Muted.WithAlpha(0.4f));

                if (entry.Image && entry.Image.texture)
                    GUI.DrawTexture(box, entry.Image.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(box, "no image", FrogletEditorPalette.CardBody);

                using (new EditorGUILayout.VerticalScope())
                {
                    entry.Image = (Sprite)EditorGUILayout.ObjectField(
                        "Sprite", entry.Image, typeof(Sprite), false);

                    entry.PreviewYaw = EditorGUILayout.Slider(
                        new GUIContent("Yaw", "Turntable angle the model is baked from."),
                        entry.PreviewYaw, -180f, 180f);
                    entry.PreviewPitch = EditorGUILayout.Slider(
                        new GUIContent("Pitch", "How far above the model the camera sits."),
                        entry.PreviewPitch, -80f, 80f);
                    entry.PreviewPadding = EditorGUILayout.Slider(
                        new GUIContent("Padding", "1 fills the frame exactly; higher pulls back."),
                        entry.PreviewPadding, 1f, 2.5f);

                    entry.FlatSilhouette = EditorGUILayout.Toggle(
                        new GUIContent("Flat silhouette",
                            "Bake with a neutral lit material instead of the source's own. " +
                            "Gameplay prism and crystal shaders read globals that only exist in a " +
                            "running frame, so some render as nothing at all; this is the escape " +
                            "hatch. The baker also flips it automatically when a render comes back " +
                            "empty."),
                        entry.FlatSilhouette);

                    using (new EditorGUI.DisabledScope(!entry.SourcePrefab))
                    {
                        if (FrogletEditorPalette.ColorButton("Re-bake this image",
                                FrogletEditorPalette.Info, 160f, tooltip:
                                "Render this entry only, at the toolbar's size."))
                            _deferred = () => BakeOne(entry);
                    }
                }
            }

            FrogletEditorPalette.HorizontalRule();
        }

        void DrawIdentity(CodexEntry entry)
        {
            GUILayout.Label("Identity", FrogletEditorPalette.SectionHeader);

            var newId = EditorGUILayout.DelayedTextField(
                new GUIContent("Id", "Stable key. The scan matches on it and a save file would " +
                                     "reference it, so re-keying a shipped entry orphans both."),
                entry.Id);
            if (newId != entry.Id) _deferred = () => Rekey(entry, newId);

            entry.DisplayName = EditorGUILayout.TextField("Display name", entry.DisplayName);

            var kingdom = (CodexKingdom)EditorGUILayout.EnumPopup("Kingdom", entry.Kingdom);
            if (kingdom != entry.Kingdom) _deferred = () => ChangeKingdom(entry, kingdom);

            entry.AccentColor = EditorGUILayout.ColorField(
                new GUIContent("Accent", "Alpha 0 means unset — the scan may then propose one."),
                entry.AccentColor);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(
                    new GUIContent("Source prefab", "Harvester-owned. Also what the runtime UI " +
                                                    "builds a live 3D model from."),
                    entry.SourcePrefab, typeof(GameObject), false);

            entry.SortOrder = EditorGUILayout.IntField("Sort order", entry.SortOrder);
            entry.LockAutoHarvest = EditorGUILayout.Toggle(
                new GUIContent("Lock against scan", "Freeze the whole entry. Use for a page with " +
                                                    "no asset behind it."),
                entry.LockAutoHarvest);

            FrogletEditorPalette.HorizontalRule();
        }

        void DrawCopy(CodexEntry entry)
        {
            GUILayout.Label("Copy", FrogletEditorPalette.SectionHeader);
            GUILayout.Label("Never overwritten by Scan & Merge.", FrogletEditorPalette.Subtitle);

            EditorGUILayout.LabelField("Tagline");
            entry.Tagline = EditorGUILayout.TextArea(entry.Tagline, GUILayout.MinHeight(34f));

            EditorGUILayout.LabelField("Description");
            entry.Description = EditorGUILayout.TextArea(entry.Description, GUILayout.MinHeight(96f));

            FrogletEditorPalette.HorizontalRule();
        }

        void DrawDiscovery(CodexEntry entry)
        {
            GUILayout.Label("Discovery", FrogletEditorPalette.SectionHeader);
            GUILayout.Label("A hook, not a gate — nothing reads these yet.", FrogletEditorPalette.Subtitle);

            entry.UnlockedByDefault = EditorGUILayout.Toggle("Unlocked by default", entry.UnlockedByDefault);
            entry.DiscoveryKey = EditorGUILayout.TextField("Discovery key", entry.DiscoveryKey);

            FrogletEditorPalette.HorizontalRule();
        }

        // ── Stats ────────────────────────────────────────────────────────────────

        void DrawStats(CodexEntry entry)
        {
            _showStats = EditorGUILayout.Foldout(_showStats, $"Facts ({entry.Stats.Count})", true);
            if (!_showStats) return;

            GUILayout.Label(
                "Rows marked AUTO are re-derived on every scan. Detach one to edit it and keep it.",
                FrogletEditorPalette.Subtitle);

            for (int i = 0; i < entry.Stats.Count; i++)
            {
                var stat = entry.Stats[i];
                int index = i;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var pill = GUILayoutUtility.GetRect(52f, 16f, GUILayout.Width(52f), GUILayout.Height(16f));
                    FrogletEditorPalette.StatusPill(pill, stat.Authored ? "MINE" : "AUTO",
                        stat.Authored ? FrogletEditorPalette.Ok : FrogletEditorPalette.Muted);

                    using (new EditorGUI.DisabledScope(!stat.Authored))
                    {
                        stat.Label = EditorGUILayout.TextField(stat.Label, GUILayout.Width(150f));
                        stat.Value = EditorGUILayout.TextField(stat.Value);
                    }

                    if (!stat.Authored)
                    {
                        // Deferred, and dirtied explicitly: a button press is not a "change" as
                        // far as BeginChangeCheck is concerned, so the block below never fires.
                        if (GUILayout.Button(new GUIContent("Detach",
                                "Make this row yours. The scan will stop rewriting it."),
                                GUILayout.Width(56f)))
                            _deferred = () =>
                            {
                                var detached = entry.Stats[index];
                                detached.Authored = true;
                                entry.Stats[index] = detached;
                                Persist(null);
                            };
                    }
                    else if (GUILayout.Button("✕", GUILayout.Width(22f)))
                    {
                        _deferred = () => { entry.Stats.RemoveAt(index); Persist(null); };
                    }
                }

                entry.Stats[i] = stat;
            }

            if (GUILayout.Button("+ Add fact", GUILayout.Width(100f)))
                _deferred = () =>
                {
                    entry.Stats.Add(new CodexStat("Label", "Value", authored: true));
                    Persist(null);
                };

            FrogletEditorPalette.HorizontalRule();
        }

        // ── Variants ─────────────────────────────────────────────────────────────

        void DrawVariants(CodexEntry entry)
        {
            _showVariants = EditorGUILayout.Foldout(
                _showVariants, $"Variants ({entry.Variants.Count})", true);
            if (!_showVariants) return;

            GUILayout.Label(
                entry.Kingdom == CodexKingdom.Ethirion
                    ? "The five heart levels. Wiring and numbers are harvester-owned."
                    : "The species' four elements. Wiring and numbers are harvester-owned.",
                FrogletEditorPalette.Subtitle);

            foreach (var variant in entry.Variants)
            {
                var key = entry.Id + "/" + variant.Label;
                bool expanded = _expandedVariants.Contains(key);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(expanded ? "▼" : "▶", GUILayout.Width(24f)))
                    {
                        if (expanded) _expandedVariants.Remove(key);
                        else _expandedVariants.Add(key);
                    }
                    GUILayout.Label(variant.Label, FrogletEditorPalette.CardTitle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{variant.Stats.Count} facts", FrogletEditorPalette.CardBody);
                }

                if (!expanded) continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.EnumPopup("Element", variant.Element);
                        EditorGUILayout.ObjectField("Config", variant.SourceConfig,
                            typeof(ScriptableObject), false);
                        EditorGUILayout.ObjectField("Prefab", variant.SourcePrefab,
                            typeof(GameObject), false);
                    }

                    variant.Image = (Sprite)EditorGUILayout.ObjectField(
                        new GUIContent("Override image",
                            "Optional. Empty falls back to the entry's image, which is the normal " +
                            "case — four elements of one species share a silhouette."),
                        variant.Image, typeof(Sprite), false);

                    foreach (var stat in variant.Stats)
                        EditorGUILayout.LabelField(stat.Label, stat.Value);
                }
            }
        }

        // ── Deferred edits ───────────────────────────────────────────────────────

        void BakeOne(CodexEntry entry)
        {
            Undo.RecordObject(_codex, "Bake codex image");
            var result = CodexImageBaker.Bake(entry, _bakeSize);

            EditorUtility.SetDirty(_codex);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            if (result.Success) FrogletToolChangeLedger.Record(ToolName, result.AssetPath);

            SetStatus(result.Success
                    ? $"Baked {result.AssetPath}" +
                      (result.FellBackToFlat
                          ? " — its own materials rendered nothing, so this is a flat silhouette."
                          : ".")
                    : result.Error,
                !result.Success);
        }

        void Rekey(CodexEntry entry, string desired)
        {
            var id = UniqueId(entry.Kingdom, desired);
            Undo.RecordObject(_codex, "Re-key codex entry");
            entry.Id = id;
            _selectedId = id;
            Persist($"Re-keyed to '{id}'. The scan matches on the id, so if a source asset still " +
                    "exists under the old key the next scan will re-add it as a new entry.");
        }

        /// <summary>
        /// Moving kingdom moves the entry between the codex's two backing lists — flora and fauna
        /// share one, ethirions have their own — so it cannot be a plain field write.
        /// </summary>
        void ChangeKingdom(CodexEntry entry, CodexKingdom kingdom)
        {
            Undo.RecordObject(_codex, "Change codex kingdom");

            var from = _codex.ListFor(entry.Kingdom);
            var to = _codex.ListFor(kingdom);
            entry.Kingdom = kingdom;

            if (from != to)
            {
                from.Remove(entry);
                to.Add(entry);
            }
            Persist(null);
        }
    }
}

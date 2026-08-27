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
    /// has to record undo and dirty the asset itself — done once, in
    /// <see cref="DrawDetail"/>.</para>
    /// </summary>
    public partial class CodexWindow
    {
        const float PreviewSize = 168f;

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
                    GUILayout.Space(20f);
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
                    GUILayout.Space(10f);

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
            var accent = entry.ResolveAccent(AccentFor(entry.Kingdom));

            GUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                GUILayout.Label(entry.DisplayName, FrogletEditorPalette.Title);
                GUILayout.FlexibleSpace();

                var flag = FlagFor(entry);
                if (flag.label != null)
                {
                    var flagRect = GUILayoutUtility.GetRect(80f, 18f, GUILayout.Width(80f),
                        GUILayout.Height(18f));
                    FrogletEditorPalette.StatusPill(flagRect, flag.label, flag.color);
                    GUILayout.Space(4f);
                }

                var pill = GUILayoutUtility.GetRect(126f, 18f, GUILayout.Width(126f), GUILayout.Height(18f));
                FrogletEditorPalette.StatusPill(pill, HeadingFor(entry.Kingdom), accent);
                GUILayout.Space(6f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                GUILayout.Label(entry.Id, FrogletEditorPalette.Subtitle);
            }

            if (entry.LockAutoHarvest)
                EditorGUILayout.HelpBox(
                    "Locked. Scan & Merge skips this entry entirely — nothing here is re-derived " +
                    "from the project.", MessageType.None);
            else if (!entry.HasSource)
                EditorGUILayout.HelpBox(
                    "No source asset. The scan could not find the prefab or config behind this " +
                    "entry, so its facts and image cannot be re-derived. It is kept, never " +
                    "auto-deleted.",
                    MessageType.Warning);

            GUILayout.Space(4f);
        }

        void DrawImageBlock(CodexEntry entry)
        {
            SectionBar("IMAGE", FrogletEditorPalette.Cyan);
            GUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);

                var box = GUILayoutUtility.GetRect(PreviewSize, PreviewSize,
                    GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
                if (Event.current.type == EventType.Repaint)
                    FrogletEditorPalette.DrawCard(box, FrogletEditorPalette.Surface.WithAlpha(0.6f),
                        FrogletEditorPalette.Muted.WithAlpha(0.35f));

                if (entry.Image && entry.Image.texture)
                    GUI.DrawTexture(box, entry.Image.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(box, "no image",
                        new GUIStyle(FrogletEditorPalette.CardBody)
                        { alignment = TextAnchor.MiddleCenter });

                GUILayout.Space(10f);

                using (new EditorGUILayout.VerticalScope())
                {
                    entry.Image = (Sprite)EditorGUILayout.ObjectField(
                        "Sprite", entry.Image, typeof(Sprite), false);

                    GUILayout.Space(4f);
                    entry.PreviewYaw = EditorGUILayout.Slider(
                        new GUIContent("Yaw", "Turntable angle the subject is baked from."),
                        entry.PreviewYaw, -180f, 180f);
                    entry.PreviewPitch = EditorGUILayout.Slider(
                        new GUIContent("Pitch", "How far above the subject the camera sits."),
                        entry.PreviewPitch, -80f, 80f);
                    entry.PreviewPadding = EditorGUILayout.Slider(
                        new GUIContent("Padding",
                            "1 fills the frame edge to edge; higher pulls the camera back."),
                        entry.PreviewPadding, 1f, 2f);

                    entry.FlatSilhouette = EditorGUILayout.Toggle(
                        new GUIContent("Flat silhouette",
                            "Bake with a neutral lit material instead of the source's own. " +
                            "Gameplay prism and crystal shaders read globals that only exist in a " +
                            "running frame, so some render as nothing at all; this is the escape " +
                            "hatch. The baker also flips it automatically when a render comes back " +
                            "empty."),
                        entry.FlatSilhouette);

                    GUILayout.Space(6f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (FrogletEditorPalette.ColorButton("Re-bake this image",
                                FrogletEditorPalette.Cyan, 150f, 24f,
                                "Render this entry only, at the toolbar's bake size.",
                                enabled: CodexImageBaker.CanBake(entry)))
                            _deferred = () => BakeOne(entry);

                        if (FrogletEditorPalette.ColorButton("Reset pose",
                                FrogletEditorPalette.Slate, 96f, 24f,
                                "Back to the default angle and edge-to-edge framing.",
                                outline: true))
                            _deferred = () => ResetPose(entry);
                    }
                }

                GUILayout.Space(8f);
            }

            GUILayout.Space(8f);
        }

        void DrawIdentity(CodexEntry entry)
        {
            SectionBar("IDENTITY", FrogletEditorPalette.Indigo);
            GUILayout.Space(4f);

            var newId = EditorGUILayout.DelayedTextField(
                new GUIContent("Id", "Stable key. The scan matches on it and a save file would " +
                                     "reference it, so re-keying a shipped entry orphans both."),
                entry.Id);
            if (newId != entry.Id) _deferred = () => Rekey(entry, newId);

            entry.DisplayName = EditorGUILayout.TextField("Display name", entry.DisplayName);

            var kingdom = (CodexKingdom)EditorGUILayout.EnumPopup("Kingdom", entry.Kingdom);
            if (kingdom != entry.Kingdom) _deferred = () => ChangeKingdom(entry, kingdom);

            entry.AccentColor = EditorGUILayout.ColorField(
                new GUIContent("Accent", "Alpha 0 means unset — the scan may then propose one, and " +
                                         "the list falls back to the kingdom's colour."),
                entry.AccentColor);

            using (new EditorGUI.DisabledScope(true))
            {
                // Both source slots are always drawn, even the empty one: which of them is filled
                // is the visible difference between a subject that was photographed and one that
                // was drawn, and hiding the blank makes an orphan look like a different shape of
                // entry rather than a missing asset.
                EditorGUILayout.ObjectField(
                    new GUIContent("Source prefab", "Harvester-owned. Also what the runtime UI " +
                                                    "builds a live 3D model from."),
                    entry.SourcePrefab, typeof(GameObject), false);

                EditorGUILayout.ObjectField(
                    new GUIContent("Source config", "Harvester-owned. The authored asset behind an " +
                                                    "entry that has no prefab — a tool's " +
                                                    "ToyDefinitionSO. A toy is built at runtime, " +
                                                    "so its definition is the asset that exists."),
                    entry.SourceConfig, typeof(ScriptableObject), false);

                if (!string.IsNullOrEmpty(entry.Group))
                    EditorGUILayout.TextField(
                        new GUIContent("Group", "Sub-heading within the kingdom. Harvester-owned — " +
                                                "for a tool it is the category it was filed under."),
                        entry.Group);
            }

            entry.SortOrder = EditorGUILayout.IntField("Sort order", entry.SortOrder);
            entry.LockAutoHarvest = EditorGUILayout.Toggle(
                new GUIContent("Lock against scan", "Freeze the whole entry. Use for a page with " +
                                                    "no asset behind it."),
                entry.LockAutoHarvest);

            GUILayout.Space(8f);
        }

        void DrawCopy(CodexEntry entry)
        {
            SectionBar("COPY", FrogletEditorPalette.Gold, "YOURS");
            GUILayout.Space(2f);
            GUILayout.Label("  Never overwritten by Scan & Merge.", FrogletEditorPalette.Subtitle);

            EditorGUILayout.LabelField("Tagline");
            entry.Tagline = EditorGUILayout.TextArea(entry.Tagline, GUILayout.MinHeight(34f));

            EditorGUILayout.LabelField("Description");
            entry.Description = EditorGUILayout.TextArea(entry.Description, GUILayout.MinHeight(96f));

            GUILayout.Space(8f);
        }

        void DrawDiscovery(CodexEntry entry)
        {
            SectionBar("DISCOVERY", FrogletEditorPalette.Slate, "HOOK");
            GUILayout.Space(2f);
            GUILayout.Label("  A hook, not a gate — nothing reads these yet.",
                FrogletEditorPalette.Subtitle);

            entry.UnlockedByDefault = EditorGUILayout.Toggle("Unlocked by default", entry.UnlockedByDefault);
            entry.DiscoveryKey = EditorGUILayout.TextField("Discovery key", entry.DiscoveryKey);

            GUILayout.Space(8f);
        }

        // ── Facts ────────────────────────────────────────────────────────────────

        void DrawStats(CodexEntry entry)
        {
            if (SectionBar($"{(_showStats ? "▾" : "▸")}  FACTS", FrogletEditorPalette.Jade,
                    entry.Stats.Count.ToString(), clickable: true))
                _showStats = !_showStats;

            if (!_showStats) { GUILayout.Space(8f); return; }

            GUILayout.Space(2f);
            GUILayout.Label("  AUTO rows are re-derived on every scan. Detach one to edit it and keep it.",
                FrogletEditorPalette.Subtitle);

            for (int i = 0; i < entry.Stats.Count; i++)
            {
                var stat = entry.Stats[i];
                int index = i;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var pill = GUILayoutUtility.GetRect(50f, 16f, GUILayout.Width(50f), GUILayout.Height(16f));
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
                        // far as BeginChangeCheck is concerned, so the block in DrawDetail never
                        // fires for it.
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
                    else if (GUILayout.Button("✕", GUILayout.Width(24f)))
                    {
                        _deferred = () => { entry.Stats.RemoveAt(index); Persist(null); };
                    }
                }

                entry.Stats[i] = stat;
            }

            GUILayout.Space(4f);
            if (FrogletEditorPalette.ColorButton("+ Add fact", FrogletEditorPalette.Ok, 100f, 22f,
                    "Add a row of your own. The scan will leave it alone.", outline: true))
                _deferred = () =>
                {
                    entry.Stats.Add(new CodexStat("Label", "Value", authored: true));
                    Persist(null);
                };

            GUILayout.Space(8f);
        }

        // ── Variants ─────────────────────────────────────────────────────────────

        void DrawVariants(CodexEntry entry)
        {
            if (SectionBar($"{(_showVariants ? "▾" : "▸")}  VARIANTS", FrogletEditorPalette.Violet,
                    entry.Variants.Count.ToString(), clickable: true))
                _showVariants = !_showVariants;

            if (!_showVariants) return;

            GUILayout.Space(2f);
            GUILayout.Label(
                entry.Kingdom switch
                {
                    CodexKingdom.Ethirion => "  Wiring and numbers are harvester-owned.",
                    CodexKingdom.Tool => "  The choices this tool offers. Harvester-owned.",
                    _ => "  The species' four elements. Wiring and numbers are harvester-owned.",
                },
                FrogletEditorPalette.Subtitle);

            foreach (var variant in entry.Variants)
            {
                var key = entry.Id + "/" + variant.Label;
                bool expanded = _expandedVariants.Contains(key);

                var row = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
                bool hover = row.Contains(Event.current.mousePosition);
                if (Event.current.type == EventType.Repaint)
                    FrogletEditorPalette.DrawCard(row,
                        hover ? FrogletEditorPalette.SurfaceRaised : FrogletEditorPalette.Surface,
                        FrogletEditorPalette.Muted.WithAlpha(0.22f));

                GUI.Label(new Rect(row.x + 8f, row.y + 2f, row.width - 120f, 18f),
                    $"{(expanded ? "▾" : "▸")}  {variant.Label}", FrogletEditorPalette.CardTitle);
                GUI.Label(new Rect(row.xMax - 110f, row.y + 3f, 100f, 16f),
                    $"{variant.Stats.Count} facts",
                    new GUIStyle(FrogletEditorPalette.CardBody)
                    { alignment = TextAnchor.MiddleRight });

                EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    var captured = key;
                    _deferred = () =>
                    {
                        if (!_expandedVariants.Remove(captured)) _expandedVariants.Add(captured);
                    };
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
                    GUILayout.Space(4f);
                }
            }
        }

        // ── Deferred edits ───────────────────────────────────────────────────────

        void ResetPose(CodexEntry entry)
        {
            Undo.RecordObject(_codex, "Reset codex pose");
            var defaults = new CodexEntry();
            entry.PreviewYaw = defaults.PreviewYaw;
            entry.PreviewPitch = defaults.PreviewPitch;
            entry.PreviewPadding = defaults.PreviewPadding;
            Persist($"Pose reset on '{entry.DisplayName}'. Re-bake to apply it.");
        }

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

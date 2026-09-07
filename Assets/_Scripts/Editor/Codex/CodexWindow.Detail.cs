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

        /// <summary>Which variant card is open, as "entryId/label". Null = none.</summary>
        string _selectedVariant;
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

        const float VariantCardWidth = 104f;
        const float VariantCardHeight = 122f;
        const float VariantThumbSize = 84f;

        /// <summary>
        /// The variants as a GRID OF ICONS under the entry's own heading, one card each, with the
        /// selected card's detail below it.
        ///
        /// <para>A list of foldouts was the wrong shape for this: sixteen paintings are sixteen
        /// different PICTURES, and the whole reason to have them is to be able to see them at
        /// once. A grid also makes the "no art of its own" cases legible rather than invisible —
        /// an element card shows the ethirion it drops, a domain card shows its colour.</para>
        /// </summary>
        void DrawVariants(CodexEntry entry)
        {
            if (entry.Variants.Count == 0) return;

            if (SectionBar($"{(_showVariants ? "▾" : "▸")}  VARIANTS · {entry.DisplayName}",
                    FrogletEditorPalette.Violet, entry.Variants.Count.ToString(), clickable: true))
                _showVariants = !_showVariants;

            if (!_showVariants) return;

            GUILayout.Space(2f);
            GUILayout.Label(
                entry.Kingdom switch
                {
                    CodexKingdom.Ethirion => "  Wiring and numbers are harvester-owned.",
                    CodexKingdom.Tool => "  The choices this tool offers. Click one for its detail.",
                    _ => "  The species' elements. Click one for its detail.",
                },
                FrogletEditorPalette.Subtitle);

            DrawVariantGrid(entry);
            DrawSelectedVariant(entry);
            GUILayout.Space(8f);
        }

        void DrawVariantGrid(CodexEntry entry)
        {
            // Columns from the pane's own width. currentViewWidth is the window, and the list
            // takes a fixed slice of it, so this tracks a resize without a layout-pass probe -
            // which inside a scroll view reports the wrong width on the Layout event anyway.
            float pane = Mathf.Max(VariantCardWidth,
                EditorGUIUtility.currentViewWidth - ListWidth - 46f);
            int columns = Mathf.Max(1, Mathf.FloorToInt(pane / (VariantCardWidth + 4f)));

            GUILayout.Space(4f);
            for (int i = 0; i < entry.Variants.Count; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8f);
                    for (int column = 0; column < columns && i + column < entry.Variants.Count; column++)
                        DrawVariantCard(entry, entry.Variants[i + column]);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(3f);
            }
            GUILayout.Space(2f);
        }

        void DrawVariantCard(CodexEntry entry, CodexVariant variant)
        {
            if (variant == null) return;

            bool selected = VariantKey(entry, variant) == _selectedVariant;
            var accent = FrogletEditorPalette.Adapt(
                variant.ResolveAccent(entry.ResolveAccent(AccentFor(entry.Kingdom))));

            var card = GUILayoutUtility.GetRect(VariantCardWidth, VariantCardHeight,
                GUILayout.Width(VariantCardWidth), GUILayout.Height(VariantCardHeight));
            bool hover = card.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                FrogletEditorPalette.DrawCard(card,
                    selected ? accent.WithAlpha(0.22f)
                             : hover ? FrogletEditorPalette.SurfaceRaised : FrogletEditorPalette.Surface,
                    selected ? accent.WithAlpha(0.9f) : FrogletEditorPalette.Muted.WithAlpha(0.25f));
            }

            var thumb = new Rect(card.x + (card.width - VariantThumbSize) * 0.5f, card.y + 6f,
                VariantThumbSize, VariantThumbSize);

            // The image the RUNTIME would draw, resolved the same way - so what this grid shows is
            // what a player would see, including the element variants that borrow their ethirion's
            // picture rather than baking one.
            var sprite = _codex.VariantImage(entry, variant);

            if (sprite && sprite.texture)
            {
                GUI.DrawTexture(thumb, sprite.texture, ScaleMode.ScaleToFit);
            }
            else if (Event.current.type == EventType.Repaint)
            {
                // No picture anywhere: the accent IS the identity (a domain), so draw it as a chip
                // rather than as an empty hole that reads as a missing asset.
                var chip = new Rect(thumb.x + 14f, thumb.y + 14f, thumb.width - 28f, thumb.height - 28f);
                FrogletEditorPalette.DrawCard(chip, accent.WithAlpha(0.55f), accent.WithAlpha(0.9f));
            }

            var label = new Rect(card.x + 4f, thumb.yMax + 2f, card.width - 8f, 30f);
            GUI.Label(label, variant.Label,
                new GUIStyle(FrogletEditorPalette.CardBody)
                {
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                    fontSize = 10,
                });

            EditorGUIUtility.AddCursorRect(card, MouseCursor.Link);
            if (!GUI.Button(card, GUIContent.none, GUIStyle.none)) return;

            var key = VariantKey(entry, variant);
            _deferred = () => _selectedVariant = _selectedVariant == key ? null : key;
        }

        /// <summary>The clicked variant's own page: what it is, where it came from, its facts.</summary>
        void DrawSelectedVariant(CodexEntry entry)
        {
            var variant = SelectedVariantOf(entry);
            if (variant == null)
            {
                GUILayout.Space(2f);
                GUILayout.Label("  Select a variant above to see its detail.",
                    FrogletEditorPalette.Subtitle);
                return;
            }

            var accent = variant.ResolveAccent(entry.ResolveAccent(AccentFor(entry.Kingdom)));
            SectionBar($"   {variant.Label}", accent, $"{variant.Stats.Count} facts");
            GUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Element", variant.Element);
                EditorGUILayout.ObjectField("Config", variant.SourceConfig,
                    typeof(ScriptableObject), false);
                EditorGUILayout.ObjectField("Prefab", variant.SourcePrefab, typeof(GameObject), false);
            }

            variant.Image = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Override image",
                    "Optional. Empty is NOT a gap: an element-keyed variant draws that element's " +
                    "ethirion image, and a variant whose identity is a colour draws its accent."),
                variant.Image, typeof(Sprite), false);

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool bakeable = CodexImageBaker.CanBake(entry, variant);
                if (FrogletEditorPalette.ColorButton("Re-bake this icon", FrogletEditorPalette.Cyan,
                        140f, 22f,
                        bakeable
                            ? "Render this variant only, at the toolbar's bake size."
                            : "This variant has no art of its own — it draws its element's " +
                              "ethirion image, or its accent colour.",
                        enabled: bakeable, outline: true))
                {
                    var captured = variant;
                    _deferred = () => BakeOneVariant(entry, captured);
                }

                if (variant.Image && FrogletEditorPalette.ColorButton("Clear image",
                        FrogletEditorPalette.Slate, 96f, 22f,
                        "Drop the override and fall back to the resolved image.", outline: true))
                {
                    var captured = variant;
                    _deferred = () => { captured.Image = null; Persist(null); };
                }
            }

            GUILayout.Space(6f);
            foreach (var stat in variant.Stats)
                EditorGUILayout.LabelField(stat.Label, stat.Value);
        }

        static string VariantKey(CodexEntry entry, CodexVariant variant) =>
            entry.Id + "/" + variant.Label;

        CodexVariant SelectedVariantOf(CodexEntry entry)
        {
            if (string.IsNullOrEmpty(_selectedVariant)) return null;
            foreach (var variant in entry.Variants)
                if (variant != null && VariantKey(entry, variant) == _selectedVariant) return variant;
            return null;
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

        void BakeOneVariant(CodexEntry entry, CodexVariant variant)
        {
            Undo.RecordObject(_codex, "Bake codex variant icon");
            var result = CodexImageBaker.Bake(entry, variant, _bakeSize);

            EditorUtility.SetDirty(_codex);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            if (result.Success) FrogletToolChangeLedger.Record(ToolName, result.AssetPath);

            SetStatus(result.Success ? $"Baked {result.AssetPath}" : result.Error, !result.Success);
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

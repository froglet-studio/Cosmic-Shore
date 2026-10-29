using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// <b>Environmental Fundamentals</b> (Yash, 2026-10-29): one place to tune the world's own building blocks - the things
    /// any vessel, spawner or scene can use, owned by none of them. Today that is the <b>Soliton Tunnel</b>: every
    /// <see cref="SolitonTunnelConfigSO"/> in the project, each edited inline (look, spindle bridge, domain tint, shape,
    /// network, life, and the slip-and-slide ride), in edit mode and in play mode alike. In play mode each card also counts
    /// the live tunnels and Soliton Networks riding that config, and the open scene's <see cref="SolitonTunnelPlacer"/>s.
    ///
    /// <para><b>Play mode:</b> an edit applies on the next frame (the tunnels read their config live). Unity keeps a
    /// ScriptableObject's in-memory edit after play stops, so a tuned value survives; but do NOT save (Ctrl+S) while
    /// playing - <c>PlayModeSOProtector</c> restores any <c>_SO_Assets</c> file saved during play. Stop, then
    /// <i>Save Assets</i>.</para>
    ///
    /// <para>EDITOR of existing assets only (like the inspector): it creates, deletes and moves nothing, records nothing on
    /// the tool ledger and draws no ship panel. A new config comes from <c>Assets ▸ Create ▸ ScriptableObjects ▸
    /// Environment ▸ Soliton Tunnel Config</c>. Doc: <c>Docs/TOOLING.md</c> (Tool index) and
    /// <c>Docs/Shaders/WORMHOLE_SHADERS.md</c>.</para>
    /// </summary>
    public sealed class EnvironmentalFundamentalsWindow : EditorWindow
    {
        const FrogletToolCategory Lane = FrogletToolCategory.Ecology;

        readonly List<SolitonTunnelConfigSO> _configs = new();
        readonly Dictionary<SolitonTunnelConfigSO, UnityEditor.Editor> _editors = new();
        readonly HashSet<SolitonTunnelConfigSO> _open = new();
        Vector2 _scroll;
        string _filter = "";

        [MenuItem("FrogletTools/Environment/Environmental Fundamentals", false, 180)]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4, DisplayName = "Environmental Fundamentals",
            Description = "Tune the world's building blocks inline - every Soliton Tunnel config (look, tint, shape, ride), in play mode too.",
            DocPath = "Docs/TOOLING.md#environmental-fundamentals")]
        public static void Open()
        {
            var w = GetWindow<EnvironmentalFundamentalsWindow>("Environmental Fundamentals");
            w.minSize = new Vector2(420, 320);
            w.Refresh();
        }

        void OnEnable()
        {
            Refresh();
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.projectChanged += Refresh;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.projectChanged -= Refresh;
            ClearEditors();
        }

        void OnPlayMode(PlayModeStateChange _) { Refresh(); Repaint(); }

        // live counts change every frame while playing
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void ClearEditors()
        {
            foreach (var e in _editors.Values) if (e) DestroyImmediate(e);
            _editors.Clear();
        }

        /// <summary>One asset scan per explicit refresh (or project change), never per repaint.</summary>
        void Refresh()
        {
            _configs.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(SolitonTunnelConfigSO)))
            {
                var c = AssetDatabase.LoadAssetAtPath<SolitonTunnelConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (c) _configs.Add(c);
            }
            _configs.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            ClearEditors();
            _open.RemoveWhere(c => !c);
        }

        void OnGUI()
        {
            var accent = FrogletEditorPalette.ColorFor(Lane);
            FrogletEditorPalette.Banner("Environmental Fundamentals",
                "The world's own building blocks, usable by any vessel or scene. Soliton Tunnels: edit every config inline.", accent);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Refresh", accent, 80, 22, "Rescan the project for Soliton Tunnel configs", outline: true))
                    EditorApplication.delayCall += () => { Refresh(); Repaint(); };
                if (FrogletEditorPalette.ColorButton("Save Assets", FrogletEditorPalette.Ok, 96, 22,
                        Application.isPlaying ? "Stop play first: _SO_Assets saved during play are restored on exit" : "Write the edited configs to disk",
                        enabled: !Application.isPlaying))
                    EditorApplication.delayCall += AssetDatabase.SaveAssets;
                GUILayout.Space(6);
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
                var pill = GUILayoutUtility.GetRect(110, 20, GUILayout.Width(110));
                FrogletEditorPalette.StatusPill(pill, Application.isPlaying ? "PLAY: LIVE" : _configs.Count + " CONFIGS",
                    Application.isPlaying ? FrogletEditorPalette.Info : FrogletEditorPalette.Muted);
            }
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Edits apply live and stay after play stops. Do not Ctrl+S while playing (PlayModeSOProtector "
                    + "restores _SO_Assets saved during play); stop, then Save Assets.", MessageType.Info);
            FrogletEditorPalette.HorizontalRule();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Soliton Tunnel", FrogletEditorPalette.SectionHeader);
            GUILayout.Label("A jelly-lens tube with an additive spindle bridge, tinted by its maker's domain; ridden as a slip-and-slide "
                + "(gravity to the inner wall, small RT push, slingshot exit). A single tunnel, or a Soliton Network of them. "
                + "Made by a vessel (the Meerkat: RT = a tunnel, an omni crystal = a network) or placed in a scene by a SolitonTunnelPlacer.",
                FrogletEditorPalette.CardBodyWrapped);
            GUILayout.Space(4);

            if (_configs.Count == 0)
                EditorGUILayout.HelpBox("No SolitonTunnelConfigSO in the project. Create one: Assets ▸ Create ▸ ScriptableObjects ▸ Environment ▸ "
                    + "Soliton Tunnel Config.", MessageType.Warning);

            SolitonTunnelPlacer[] placers = Application.isPlaying
                ? FindObjectsByType<SolitonTunnelPlacer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                : null;

            foreach (var cfg in _configs)
            {
                if (!cfg) continue;
                string path = AssetDatabase.GetAssetPath(cfg);
                if (!string.IsNullOrEmpty(_filter) && path.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                DrawConfig(cfg, path, accent, placers);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawConfig(SolitonTunnelConfigSO cfg, string path, Color accent, SolitonTunnelPlacer[] placers)
        {
            var row = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (Event.current.type == EventType.Repaint) FrogletEditorPalette.DrawAccentStripe(row, accent);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool open = _open.Contains(cfg);
                bool now = EditorGUILayout.Foldout(open, cfg.name, true, EditorStyles.foldoutHeader);
                if (now != open) { if (now) _open.Add(cfg); else _open.Remove(cfg); }
                GUILayout.FlexibleSpace();
                if (Application.isPlaying)
                {
                    int tunnels = 0, networks = 0, placed = 0;
                    foreach (var t in SolitonTunnel.Live) if (t != null && t.Config == cfg) tunnels++;
                    foreach (var n in SolitonNetwork.Live) if (n != null && n.Config == cfg) networks++;
                    if (placers != null) foreach (var p in placers) if (p && p.Config == cfg) placed++;
                    var r = GUILayoutUtility.GetRect(170, 18, GUILayout.Width(170));
                    FrogletEditorPalette.StatusPill(r, $"{tunnels} TUNNEL · {networks} NET · {placed} PLACER",
                        tunnels + networks > 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Muted);
                }
                if (FrogletEditorPalette.ColorButton("Ping", accent, 46, 18, path, outline: true))
                    EditorGUIUtility.PingObject(cfg);
            }
            GUILayout.Label(path, FrogletEditorPalette.CardBody);
            if (_open.Contains(cfg))
            {
                if (!_editors.TryGetValue(cfg, out var ed) || !ed)
                {
                    ed = null;
                    UnityEditor.Editor.CreateCachedEditor(cfg, null, ref ed);
                    _editors[cfg] = ed;
                }
                EditorGUI.indentLevel++;
                ed.OnInspectorGUI();   // the config's own inspector: every field, its tooltip and range; undo and dirty for free
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }
    }
}

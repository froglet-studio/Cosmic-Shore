using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Tools &gt; Cosmic Shore &gt; End Game Conditions — the ONE place to set how each mode ends:
    /// HexRace crystal count, Joust count, and Crystal Capture crystal count. Edits the single
    /// <see cref="EndConditionOverridesSO"/> asset at Assets/Resources/EndConditionOverrides.asset
    /// (auto-created on first open); the turn monitors read it at runtime. There are no per-scene
    /// inspector fields for these anymore. See the <c>/EndGameConditions</c> skill.
    ///
    /// The window tracks two value sets: the LIVE counts (what the game uses at runtime) and the
    /// BUILD counts (the values a shipping build must use). Lower a LIVE count to end a mode quickly
    /// while testing, then click "Restore build values" to put them back before building. A warning
    /// banner (and a build-time check) fire whenever LIVE drifts from BUILD, so a test configuration
    /// is never shipped by accident.
    /// </summary>
    public class EndConditionOverridesWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + EndConditionOverridesSO.ResourcePath + ".asset";

        EndConditionOverridesSO _config;

        [MenuItem("Tools/Cosmic Shore/End Game Conditions")]
        static void Open()
        {
            var w = GetWindow<EndConditionOverridesWindow>("End Game Conditions");
            w.minSize = new Vector2(380, 400);
            w.Show();
        }

        void OnEnable() => _config = LoadOrCreate();

        static EndConditionOverridesSO LoadOrCreate()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<EndConditionOverridesSO>(AssetPath);
            if (cfg != null) return cfg;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            cfg = CreateInstance<EndConditionOverridesSO>();
            AssetDatabase.CreateAsset(cfg, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EndConditionOverrides] Created config at {AssetPath}");
            return cfg;
        }

        void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Config asset not found.", MessageType.Warning);
                if (GUILayout.Button("Create EndConditionOverrides asset"))
                    _config = LoadOrCreate();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("End Game Conditions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The single source of truth for how each mode ends. Applies wherever the mode runs " +
                "(tournament or standalone).\n\n0 = auto/default:\n" +
                "  • HexRace / Crystal Capture: auto-calc from track waypoints.\n" +
                "  • Joust: default " + EndConditionOverridesSO.DefaultJoustCount + ".",
                MessageType.Info);

            DrawDriftWarning();

            // ---- LIVE counts (used at runtime) ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live values (used at runtime)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int hex = Mathf.Max(0, EditorGUILayout.IntField("HexRace — Crystal Count", _config.hexRaceCrystalCount));
            int cc  = Mathf.Max(0, EditorGUILayout.IntField("Crystal Capture — Crystal Count", _config.crystalCaptureCrystalCount));
            int jo  = Mathf.Max(0, EditorGUILayout.IntField("Joust — Joust Count", _config.joustCount));
            if (EditorGUI.EndChangeCheck())
                Persist("Edit End Game Conditions", () =>
                {
                    _config.hexRaceCrystalCount = hex;
                    _config.crystalCaptureCrystalCount = cc;
                    _config.joustCount = jo;
                });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effective now", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("HexRace", hex > 0 ? hex.ToString() : "auto (track waypoints)");
            EditorGUILayout.LabelField("Crystal Capture", cc > 0 ? cc.ToString() : "auto (track waypoints)");
            EditorGUILayout.LabelField("Joust", jo > 0 ? jo.ToString() : EndConditionOverridesSO.DefaultJoustCount + " (default)");
            EditorGUI.indentLevel--;

            // ---- BUILD values (the production baseline to restore before shipping) ----
            EditorGUILayout.Space();
            DrawSeparator();
            EditorGUILayout.LabelField("Build values (restore the Live values to these before building)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "What a shipping build must use. Lower a Live count above to end a mode quickly while " +
                "testing, then click \"Restore build values → live\" to undo it before building. " +
                "Update these only when the production baseline genuinely changes.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            int hexB = Mathf.Max(0, EditorGUILayout.IntField("HexRace — Build Crystal Count", _config.hexRaceCrystalCountBuild));
            int ccB  = Mathf.Max(0, EditorGUILayout.IntField("Crystal Capture — Build Crystal Count", _config.crystalCaptureCrystalCountBuild));
            int joB  = Mathf.Max(0, EditorGUILayout.IntField("Joust — Build Joust Count", _config.joustCountBuild));
            if (EditorGUI.EndChangeCheck())
                Persist("Edit End Game build values", () =>
                {
                    _config.hexRaceCrystalCountBuild = hexB;
                    _config.crystalCaptureCrystalCountBuild = ccB;
                    _config.joustCountBuild = joB;
                });

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_config.LiveMatchesBuild))
                {
                    if (GUILayout.Button("Restore build values → live"))
                        Persist("Restore End Game build values", () =>
                        {
                            _config.hexRaceCrystalCount = _config.hexRaceCrystalCountBuild;
                            _config.crystalCaptureCrystalCount = _config.crystalCaptureCrystalCountBuild;
                            _config.joustCount = _config.joustCountBuild;
                        });
                }

                if (GUILayout.Button("Save live values as build"))
                    SaveLiveAsBuild();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Ping config asset"))
                EditorGUIUtility.PingObject(_config);
        }

        void DrawDriftWarning()
        {
            if (_config.LiveMatchesBuild) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Live counts differ from the Build values — this looks like a TEST configuration.\n" +
                _config.DescribeDrift() +
                "Click \"Restore build values → live\" below before shipping a build.",
                MessageType.Warning);
        }

        void SaveLiveAsBuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "Overwrite build values?",
                    "This makes the current Live values the new build baseline:\n\n" +
                    $"  HexRace: {_config.hexRaceCrystalCount}\n" +
                    $"  Crystal Capture: {_config.crystalCaptureCrystalCount}\n" +
                    $"  Joust: {_config.joustCount}\n\n" +
                    "Only do this when the production values have genuinely changed.",
                    "Overwrite", "Cancel"))
                return;

            Persist("Save live as End Game build values", () =>
            {
                _config.hexRaceCrystalCountBuild = _config.hexRaceCrystalCount;
                _config.crystalCaptureCrystalCountBuild = _config.crystalCaptureCrystalCount;
                _config.joustCountBuild = _config.joustCount;
            });
        }

        void Persist(string undoLabel, System.Action mutate)
        {
            Undo.RecordObject(_config, undoLabel);
            mutate();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }

        static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }
    }
}

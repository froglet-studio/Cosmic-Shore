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
    /// </summary>
    public class EndConditionOverridesWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + EndConditionOverridesSO.ResourcePath + ".asset";

        EndConditionOverridesSO _config;

        [MenuItem("Tools/Cosmic Shore/End Game Conditions")]
        static void Open()
        {
            var w = GetWindow<EndConditionOverridesWindow>("End Game Conditions");
            w.minSize = new Vector2(360, 240);
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

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            int hex = Mathf.Max(0, EditorGUILayout.IntField("HexRace — Crystal Count", _config.hexRaceCrystalCount));
            int cc  = Mathf.Max(0, EditorGUILayout.IntField("Crystal Capture — Crystal Count", _config.crystalCaptureCrystalCount));
            int jo  = Mathf.Max(0, EditorGUILayout.IntField("Joust — Joust Count", _config.joustCount));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_config, "Edit End Game Conditions");
                _config.hexRaceCrystalCount = hex;
                _config.crystalCaptureCrystalCount = cc;
                _config.joustCount = jo;
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effective now", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("HexRace", hex > 0 ? hex.ToString() : "auto (track waypoints)");
            EditorGUILayout.LabelField("Crystal Capture", cc > 0 ? cc.ToString() : "auto (track waypoints)");
            EditorGUILayout.LabelField("Joust", jo > 0 ? jo.ToString() : EndConditionOverridesSO.DefaultJoustCount + " (default)");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            if (GUILayout.Button("Ping config asset"))
                EditorGUIUtility.PingObject(_config);
        }
    }
}

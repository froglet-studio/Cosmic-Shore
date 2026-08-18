using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Tools &gt; Cosmic Shore &gt; End Game Conditions - the ONE place to set how each mode ends:
    /// HexRace crystal count, Joust count, and Crystal Capture crystal count. Edits the single
    /// <see cref="EndConditionOverridesSO"/> asset at Assets/Resources/EndConditionOverrides.asset
    /// (auto-created on first open); the turn monitors read it at runtime. There are no per-scene
    /// inspector fields for these anymore. See the <c>/EndGameConditions</c> skill.
    ///
    /// The Live fields are what the game uses at runtime - lower one to end a mode quickly while
    /// testing. "Set Build Values" snapshots the current Live counts as the Build baseline (shown
    /// above the button). With "Auto-restore build values before build" on, a build first restores
    /// the Live counts to that baseline (<see cref="EndConditionBuildRestore"/>), so a test config is
    /// never shipped.
    /// </summary>
    public class EndConditionOverridesWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + EndConditionOverridesSO.ResourcePath + ".asset";

        EndConditionOverridesSO _config;

        [MenuItem("FrogletTools/Game Modes/End Game Conditions")]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 5,
            Description = "The one place win conditions are authored for the domain modes.")]
        static void Open()
        {
            var w = GetWindow<EndConditionOverridesWindow>("End Game Conditions");
            w.minSize = new Vector2(380, 360);
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
                "  • Joust: default " + EndConditionOverridesSO.DefaultJoustCount + ".\n" +
                "  • Maelstrom: placement points to win the shuffle (race to N), default " +
                EndConditionOverridesSO.DefaultMaelstromWinTarget + ".\n" +
                "  • Brood Rush: claimed fauna waves to win (race to N), default " +
                EndConditionOverridesSO.DefaultNucleusRushWaveTarget + ".\n" +
                "  • Rampage: hostile prisms destroyed to win (race to N), default " +
                EndConditionOverridesSO.DefaultRampagePrismTarget + ".\n" +
                "  • Ribcage: hostile prisms destroyed to win (race to N), default " +
                EndConditionOverridesSO.DefaultRibcagePrismTarget +
                ". The 25%/50% fauna-release rungs are fractions of this.\n" +
                "  • Wildlife Liberation: creatures a domain must kill to win (race to N), " +
                "default " + EndConditionOverridesSO.DefaultWildlifeKillTarget + ".\n" +
                "  • Dog Fight: gunnery points a DOMAIN needs to win - a bullet hit scores 1 and " +
                "a missile hit scores 50, so the default " + EndConditionOverridesSO.DefaultDogFightPointTarget +
                " is 120 bullets or 3 rockets, or any mix.\n" +
                "  • The Bends: BENDS a DOMAIN needs to win - one opposing pilot caught in your " +
                "Dolphin crystal blast scores 1, so the default " +
                EndConditionOverridesSO.DefaultBendsPointTarget +
                " is three clean hits (a race to 3, like Joust).",
                MessageType.Info);

            // ---- Live input fields (used at runtime) ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live values (used at runtime)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int hex = Mathf.Max(0, EditorGUILayout.IntField("HexRace - Crystal Count", _config.hexRaceCrystalCount));
            int cc  = Mathf.Max(0, EditorGUILayout.IntField("Crystal Capture - Crystal Count", _config.crystalCaptureCrystalCount));
            int jo  = Mathf.Max(0, EditorGUILayout.IntField("Joust - Joust Count", _config.joustCount));
            int mw  = Mathf.Max(0, EditorGUILayout.IntField("Maelstrom - Win Target (points)", _config.maelstromWinTarget));
            int nr  = Mathf.Max(0, EditorGUILayout.IntField("Brood Rush - Wave Target", _config.nucleusRushWaveTarget));
            int ra  = Mathf.Max(0, EditorGUILayout.IntField("Rampage - Prism Target", _config.rampagePrismTarget));
            int rc  = Mathf.Max(0, EditorGUILayout.IntField("Ribcage - Prism Target", _config.ribcagePrismTarget));
            int wl  = Mathf.Max(0, EditorGUILayout.IntField("Wildlife Liberation - Kill Target", _config.wildlifeKillTarget));
            int df  = Mathf.Max(0, EditorGUILayout.IntField("Dog Fight - Point Target", _config.dogFightPointTarget));
            int bd  = Mathf.Max(0, EditorGUILayout.IntField("The Bends - Bend Target", _config.bendsPointTarget));
            if (EditorGUI.EndChangeCheck())
                Persist("Edit End Game Conditions", () =>
                {
                    _config.hexRaceCrystalCount = hex;
                    _config.crystalCaptureCrystalCount = cc;
                    _config.joustCount = jo;
                    _config.maelstromWinTarget = mw;
                    _config.nucleusRushWaveTarget = nr;
                    _config.rampagePrismTarget = ra;
                    _config.ribcagePrismTarget = rc;
                    _config.wildlifeKillTarget = wl;
                    _config.dogFightPointTarget = df;
                    _config.bendsPointTarget = bd;
                });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effective now", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("HexRace", hex > 0 ? hex.ToString() : "auto (track waypoints)");
            EditorGUILayout.LabelField("Crystal Capture", cc > 0 ? cc.ToString() : "auto (track waypoints)");
            EditorGUILayout.LabelField("Joust", jo > 0 ? jo.ToString() : EndConditionOverridesSO.DefaultJoustCount + " (default)");
            EditorGUILayout.LabelField("Maelstrom", mw > 0 ? mw.ToString() : EndConditionOverridesSO.DefaultMaelstromWinTarget + " (default)");
            EditorGUILayout.LabelField("Brood Rush", nr > 0 ? nr.ToString() : EndConditionOverridesSO.DefaultNucleusRushWaveTarget + " (default)");
            EditorGUILayout.LabelField("Rampage", ra > 0 ? ra.ToString() : EndConditionOverridesSO.DefaultRampagePrismTarget + " (default)");
            EditorGUILayout.LabelField("Ribcage", rc > 0 ? rc.ToString() : EndConditionOverridesSO.DefaultRibcagePrismTarget + " (default)");
            EditorGUILayout.LabelField("Wildlife Liberation", wl > 0 ? wl.ToString() : EndConditionOverridesSO.DefaultWildlifeKillTarget + " (default)");
            EditorGUILayout.LabelField("Dog Fight", df > 0 ? df.ToString() : EndConditionOverridesSO.DefaultDogFightPointTarget + " (default)");
            EditorGUILayout.LabelField("The Bends", bd > 0 ? bd.ToString() : EndConditionOverridesSO.DefaultBendsPointTarget + " (default)");
            EditorGUI.indentLevel--;

            // ---- Build baseline (read-only display + capture button) ----
            EditorGUILayout.Space();
            DrawSeparator();
            EditorGUILayout.LabelField("Build baseline (what a shipping build uses)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(DescribeBuildValues(), MessageType.None);
            if (GUILayout.Button("Set Build Values  (snapshot the Live values above)"))
                Persist("Set End Game build values", _config.CaptureBuildValues);

            // ---- Auto-restore toggle ----
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            bool auto = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Auto-restore build values before build",
                    "On: a build first copies the Build baseline onto the Live values, so test values are never shipped."),
                _config.autoRestoreBuildValuesBeforeBuild);
            if (EditorGUI.EndChangeCheck())
                Persist("Toggle End Game auto-restore", () => _config.autoRestoreBuildValuesBeforeBuild = auto);

            // ---- Asset ----
            EditorGUILayout.Space();
            if (GUILayout.Button("Ping config asset"))
                EditorGUIUtility.PingObject(_config);
        }

        string DescribeBuildValues()
        {
            return "HexRace: " + Fmt(_config.hexRaceCrystalCountBuild, "auto") + "\n" +
                   "Crystal Capture: " + Fmt(_config.crystalCaptureCrystalCountBuild, "auto") + "\n" +
                   "Joust: " + Fmt(_config.joustCountBuild, "default " + EndConditionOverridesSO.DefaultJoustCount) + "\n" +
                   "Maelstrom: " + Fmt(_config.maelstromWinTargetBuild, "default " + EndConditionOverridesSO.DefaultMaelstromWinTarget) + "\n" +
                   "Brood Rush: " + Fmt(_config.nucleusRushWaveTargetBuild, "default " + EndConditionOverridesSO.DefaultNucleusRushWaveTarget) + "\n" +
                   "Rampage: " + Fmt(_config.rampagePrismTargetBuild, "default " + EndConditionOverridesSO.DefaultRampagePrismTarget) + "\n" +
                   "Ribcage: " + Fmt(_config.ribcagePrismTargetBuild, "default " + EndConditionOverridesSO.DefaultRibcagePrismTarget) + "\n" +
                   "Wildlife Liberation: " + Fmt(_config.wildlifeKillTargetBuild, "default " + EndConditionOverridesSO.DefaultWildlifeKillTarget) + "\n" +
                   "Dog Fight: " + Fmt(_config.dogFightPointTargetBuild, "default " + EndConditionOverridesSO.DefaultDogFightPointTarget) + "\n" +
                   "The Bends: " + Fmt(_config.bendsPointTargetBuild, "default " + EndConditionOverridesSO.DefaultBendsPointTarget);

            static string Fmt(int value, string zeroMeaning) => value > 0 ? value.ToString() : "0 (" + zeroMeaning + ")";
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

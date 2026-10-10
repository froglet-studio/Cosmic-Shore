#if UNITY_EDITOR
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Editor
{
    /// <summary>
    /// Watches Unity's play-mode state. When the editor enters play mode AND a
    /// TrainingControlSO with AutoStartOnPlay = true exists in the project,
    /// instantiates a DontDestroyOnLoad GameObject with the TrainingAutoLauncher
    /// component. That's how the "Learn" button hands the wheel over to the
    /// runtime without modifying any scene asset.
    ///
    /// The hook is also responsible for clearing AutoStartOnPlay on exit, so a
    /// user who presses Stop and later presses Play normally doesn't accidentally
    /// resume training.
    /// </summary>
    [InitializeOnLoad]
    static class TrainingPlayModeHook
    {
        const string ControlAssetSearch = "t:TrainingControlSO";

        static TrainingPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // Domain reload restores verbose channels from EditorPrefs.
                    // Arm the training channel here, before that reload, so the
                    // first rollout line is audible when Learn auto-starts.
                    ArmTrainingLogChannel();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    HandleEntered();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    HandleExiting();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ClearTrainingFlag();
                    break;
            }
        }

        static void ArmTrainingLogChannel()
        {
            var control = FindControlAsset();
            if (control == null || !control.AutoStartOnPlay) return;
            const string key = "CSDebug_VerboseChannels";
            int bits = EditorPrefs.GetInt(key, 0) | (int)CSLogChannel.AITraining;
            EditorPrefs.SetInt(key, bits);
            CSDebug.VerboseChannels |= CSLogChannel.AITraining;
        }

        static void HandleEntered()
        {
            var control = FindControlAsset();
            if (control == null) return;
            if (!control.AutoStartOnPlay) return;
            if (control.Scenario == null)
            {
                Debug.LogWarning("[Training] AutoStartOnPlay is on but no scenario is assigned. Skipping auto-launch.");
                return;
            }

            var go = new GameObject("[Training AutoLauncher]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var launcher = go.AddComponent<TrainingAutoLauncher>();
            launcher.Control = control;
        }

        static void HandleExiting()
        {
            // Clear the flag so the next plain-old play press doesn't auto-launch.
            // The session asset is left intact: generation and completed
            // evaluations resume the next time Learn is pressed.
            var control = FindControlAsset();
            if (control == null) return;
            if (!control.AutoStartOnPlay && !control.HumanPlaysThisLaunch) return;
            control.AutoStartOnPlay = false;
            control.HumanPlaysThisLaunch = false;
            EditorUtility.SetDirty(control);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// GameDataSO.IsGeneticTrainingSession is a serialized field. The runner
        /// sets it true for the play session so every other pilot installer stands
        /// down. Rest it after play so a later match is not stuck in training mode.
        /// </summary>
        static void ClearTrainingFlag()
        {
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            if (guids == null) return;
            bool cleared = false;
            for (int i = 0; i < guids.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<GameDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (data == null || !data.IsGeneticTrainingSession) continue;
                data.IsGeneticTrainingSession = false;
                EditorUtility.SetDirty(data);
                cleared = true;
            }
            if (cleared) AssetDatabase.SaveAssets();
        }

        public static TrainingControlSO FindControlAsset()
        {
            var guids = AssetDatabase.FindAssets(ControlAssetSearch);
            if (guids == null || guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<TrainingControlSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
#endif

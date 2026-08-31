#if UNITY_EDITOR
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
                case PlayModeStateChange.EnteredPlayMode:
                    HandleEntered();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    HandleExiting();
                    break;
            }
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
            // Keeping the rest of the control asset intact (scenario, archive, etc.)
            // means the user can press Learn again with one click to resume.
            var control = FindControlAsset();
            if (control == null) return;
            if (!control.AutoStartOnPlay) return;
            control.AutoStartOnPlay = false;
            EditorUtility.SetDirty(control);
            AssetDatabase.SaveAssets();
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

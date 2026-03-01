#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Prevents SOAP ScriptableVariable/ScriptableList assets and other runtime
    /// ScriptableObjects from persisting play-mode changes to disk.
    ///
    /// SOAP's built-in reset relies on a non-serialized _initialValue field that
    /// is lost during domain reload, so ResetToInitialValue() cannot reliably
    /// restore the pre-play-mode state. Plain ScriptableObject assets (GameDataSO,
    /// CellDataSO, etc.) have no reset mechanism at all — Unity persists their
    /// play-mode mutations by design.
    ///
    /// This script captures the raw file contents of all protected SO assets before
    /// entering play mode (using SessionState, which survives domain reload) and
    /// writes them back on play mode exit, guaranteeing zero git diff.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeSOProtector
    {
        private const string KeyPrefix = "PlayModeSOProtector_";
        private const string GuidListKey = KeyPrefix + "GuidList";

        static PlayModeSOProtector()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    CaptureProtectedAssets();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    RestoreProtectedAssets();
                    break;
            }
        }

        private static void CaptureProtectedAssets()
        {
            var guids = CollectProtectedGuids();
            SessionState.SetString(GuidListKey, string.Join(";", guids));

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                SessionState.SetString(KeyPrefix + guid, File.ReadAllText(path));
            }
        }

        private static void RestoreProtectedAssets()
        {
            var raw = SessionState.GetString(GuidListKey, "");
            if (string.IsNullOrEmpty(raw))
                return;

            var guids = raw.Split(';');
            var anyRestored = false;

            foreach (var guid in guids)
            {
                if (string.IsNullOrEmpty(guid))
                    continue;

                var key = KeyPrefix + guid;
                var saved = SessionState.GetString(key, null);
                SessionState.EraseString(key);

                if (string.IsNullOrEmpty(saved))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                var current = File.ReadAllText(path);
                if (current == saved)
                    continue;

                File.WriteAllText(path, saved);
                anyRestored = true;
            }

            SessionState.EraseString(GuidListKey);

            if (anyRestored)
                AssetDatabase.Refresh();
        }

        // Non-SOAP ScriptableObject types whose serialized fields mutate at runtime.
        // Add type names here as needed (class name only, no namespace).
        private static readonly string[] AdditionalProtectedTypes =
        {
            "GameDataSO",
            "CellRuntimeDataSO",
        };

        private static HashSet<string> CollectProtectedGuids()
        {
            var guids = new HashSet<string>();

            // All SOAP ScriptableVariable assets (IntVariable, FloatVariable,
            // VesselClassTypeVariable, NetworkMonitorDataVariable, AuthenticationDataVariable, etc.)
            foreach (var g in AssetDatabase.FindAssets("t:ScriptableVariableBase"))
                guids.Add(g);

            // All SOAP ScriptableList assets (ScriptableListPartyPlayerData, etc.)
            foreach (var g in AssetDatabase.FindAssets("t:ScriptableListBase"))
                guids.Add(g);

            // Non-SOAP runtime SOs that also mutate during play mode
            foreach (var typeName in AdditionalProtectedTypes)
                foreach (var g in AssetDatabase.FindAssets($"t:{typeName}"))
                    guids.Add(g);

            return guids;
        }
    }
}
#endif

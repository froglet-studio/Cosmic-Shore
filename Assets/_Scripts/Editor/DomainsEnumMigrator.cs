using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-shot migrator for the Domains enum collapse.
    ///
    /// Old enum: { None=-1, Unassigned=0, Jade=1, Ruby=2, Blue=3, Gold=4 }
    /// New enum: { Jade=1, Ruby=2, Gold=3, Blue=4 }
    ///
    /// Mapping (applied to every serialized Domains-typed property in every prefab and
    /// ScriptableObject under Assets/):
    ///
    ///   -1 -> 4   (None       -> Blue)
    ///    0 -> 4   (Unassigned -> Blue)
    ///    1 -> 1   (Jade unchanged)
    ///    2 -> 2   (Ruby unchanged)
    ///    3 -> 4   (old Blue   -> new Blue)
    ///    4 -> 3   (old Gold   -> new Gold)
    ///
    /// Run once via the Tools menu, commit the asset diff, then delete this file.
    /// </summary>
    public static class DomainsEnumMigrator
    {
        const string MarkerKey = "CS_DomainsMigrated_v2";

        // Cached at first run. The new enum has exactly 4 members; we use that as the
        // detection gate for "is this property typed as the new Domains enum?". Other
        // 4-member enums in the codebase that happen to be in this asset will get rewritten
        // too, so check the full display-name set.
        static readonly string[] NewDomainNames = { "Jade", "Ruby", "Gold", "Blue" };

        [MenuItem("Tools/Cosmic Shore/Migrate Domains Enum (Gold↔Blue + drop None/Unassigned)")]
        public static void Run()
        {
            if (EditorPrefs.GetString(MarkerKey, string.Empty) == "true")
            {
                if (!EditorUtility.DisplayDialog(
                        "Domains Migrator",
                        "Migration marker is already set on this machine. Re-running will corrupt data " +
                        "(Gold and Blue would swap a second time).\n\nAre you absolutely sure you want to proceed?",
                        "Yes, run anyway",
                        "Cancel"))
                {
                    Debug.Log("[DomainsEnumMigrator] Cancelled by user (already migrated).");
                    return;
                }
            }

            int assetsTouched = 0;
            int propertiesRewritten = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");
            EditorUtility.DisplayProgressBar("Domains Migrator", "Scanning assets...", 0f);

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;
                    if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                        && !path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    EditorUtility.DisplayProgressBar(
                        "Domains Migrator",
                        $"Scanning {path}",
                        (float)i / Mathf.Max(1, guids.Length));

                    Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);
                    bool dirty = false;

                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        if (TryRewriteObject(obj, ref propertiesRewritten))
                            dirty = true;
                    }

                    if (dirty)
                    {
                        assetsTouched++;
                        AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(path));
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorPrefs.SetString(MarkerKey, "true");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DomainsEnumMigrator] Done. Assets touched: {assetsTouched}, " +
                      $"properties rewritten: {propertiesRewritten}. " +
                      "Commit the diff and DELETE THIS SCRIPT.");
        }

        static bool TryRewriteObject(Object obj, ref int propertiesRewritten)
        {
            var so = new SerializedObject(obj);
            var iter = so.GetIterator();
            bool dirty = false;

            // NextVisible(true) walks the entire serialized hierarchy, including nested arrays
            // and structs. We need to enter children of generic types (lists, structs).
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iter.propertyType != SerializedPropertyType.Enum) continue;
                if (!IsDomainsEnum(iter)) continue;

                int oldInt = iter.intValue;
                int newInt = MapDomainInt(oldInt);
                if (newInt == oldInt) continue;

                iter.intValue = newInt;
                dirty = true;
                propertiesRewritten++;
            }

            if (dirty) so.ApplyModifiedPropertiesWithoutUndo();
            return dirty;
        }

        static bool IsDomainsEnum(SerializedProperty prop)
        {
            // Gate by the exact name set of the new enum to avoid touching unrelated enums.
            var names = prop.enumDisplayNames;
            if (names == null || names.Length != NewDomainNames.Length) return false;
            for (int i = 0; i < names.Length; i++)
                if (names[i] != NewDomainNames[i]) return false;
            return true;
        }

        static int MapDomainInt(int oldInt)
        {
            switch (oldInt)
            {
                case -1: return 4; // None       -> Blue
                case 0:  return 4; // Unassigned -> Blue
                case 3:  return 4; // old Blue   -> new Blue
                case 4:  return 3; // old Gold   -> new Gold
                default: return oldInt; // 1 (Jade) and 2 (Ruby) unchanged
            }
        }

        [MenuItem("Tools/Cosmic Shore/Reset Domains Migrator Marker")]
        public static void ResetMarker()
        {
            EditorPrefs.DeleteKey(MarkerKey);
            Debug.Log("[DomainsEnumMigrator] Marker cleared.");
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-shot editor tool: removes AudioSource components from all crystal
    /// prefabs in Assets/_Prefabs/Environment/.
    ///
    /// Crystal.PlayExplosionAudio() no longer uses per-prefab AudioSources -
    /// it fires an FMOD one-shot via AudioSystem.PlayGameplaySFX(category, position)
    /// instead. Run this tool once after pulling this change to clean up the
    /// dead components from all prefabs.
    ///
    /// Menu: FrogletTools > Ecology > Strip Crystal AudioSources
    /// </summary>
    public static class StripCrystalAudioSourceTool
    {
        const string PrefabFolder = "Assets/_Prefabs/Environment";

        [MenuItem("FrogletTools/Ecology/Strip Crystal AudioSources")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 2,
            Description = "Remove stray AudioSources from crystal prefabs.")]
        public static void StripAudioSources()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            var stripped = new List<string>();
            var skipped = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Only process prefabs that have a Crystal component anywhere in the hierarchy.
                if (prefab.GetComponentInChildren<CosmicShore.Gameplay.Crystal>(includeInactive: true) == null)
                {
                    skipped.Add(path);
                    continue;
                }

                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    GameObject root = scope.prefabContentsRoot;
                    AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(includeInactive: true);

                    if (sources.Length == 0)
                    {
                        skipped.Add(path);
                        continue;
                    }

                    foreach (AudioSource src in sources)
                        Object.DestroyImmediate(src);

                    stripped.Add($"{path} (removed {sources.Length} AudioSource(s))");
                }
            }

            if (stripped.Count > 0)
            {
                Debug.Log($"[StripCrystalAudioSourceTool] Stripped AudioSource from {stripped.Count} prefab(s):\n" +
                          string.Join("\n", stripped));
            }
            else
            {
                Debug.Log("[StripCrystalAudioSourceTool] No crystal prefabs with AudioSource found - nothing to remove.");
            }

            if (skipped.Count > 0)
                Debug.Log($"[StripCrystalAudioSourceTool] Skipped {skipped.Count} non-crystal prefab(s).");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}

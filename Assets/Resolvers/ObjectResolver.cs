#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Resolvers
{
    public static class ObjectResolver
    {
        public static GameObject GetFromPrefab(string prefabName)
        {
            var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    // Instantiate the prefab or use it as needed
                    var instance = Object.Instantiate(prefab);
                    return instance;
                }
            }
            
            Debug.LogError("Prefab not found");
            return null;
        }
    }
}
#endif

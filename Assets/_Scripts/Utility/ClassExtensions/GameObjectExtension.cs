using UnityEngine;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class GameObjectExtension
    {
        public static T GetOrAdd<T>(this GameObject gameObject) where T : Component
        {
            if (!gameObject.TryGetComponent<T>(out var component))
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }
        
        public static T OrNull<T>(this T obj) where T : Object => obj ? obj : null;

        public static void DestroyChildren(this GameObject gameObject)
        {
            for (var i = 0; i < gameObject.transform.childCount; i++)
            {
                Object.Destroy(gameObject.transform.GetChild(i));
            }
        }

        public static void EnableChildren(this GameObject gameObject)
        {
            for (var i = 0; i < gameObject.transform.childCount; i++)
            {
                gameObject.transform.GetChild(i).gameObject.SetActive(true);
            }
        }

        public static void DisableChildren(this GameObject gameObject)
        {
            for (var i = 0; i < gameObject.transform.childCount; i++)
            {
                gameObject.transform.GetChild(i).gameObject.SetActive(false);
            }
        }

        public static bool IsLayer(this GameObject gameObject, string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            
            if (layer == -1)
            {
                Debug.LogError($"Layer - {layerName} not found.");
            }

            return gameObject.layer == layer;
        }
    }
}

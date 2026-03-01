using System.Linq;
using UnityEngine;

namespace CosmicShore.Utility
{
    public static class GameObjectExtension
    {
        /// <summary>
        /// Shows or hides a UI GameObject via its <see cref="CanvasGroup"/> instead of
        /// <c>SetActive</c>. A CanvasGroup is added automatically if one does not exist.
        /// This avoids Unity canvas rebuild costs triggered by enabling/disabling GameObjects.
        /// </summary>
        public static void SetVisible(this GameObject gameObject, bool visible)
        {
            var cg = gameObject.GetOrAdd<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        /// <summary>
        /// Returns whether the GameObject's <see cref="CanvasGroup"/> is currently visible
        /// (alpha &gt; 0). Falls back to <c>activeSelf</c> when no CanvasGroup is present.
        /// </summary>
        public static bool IsVisible(this GameObject gameObject)
        {
            if (gameObject.TryGetComponent<CanvasGroup>(out var cg))
                return cg.alpha > 0f;

            return gameObject.activeSelf;
        }

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
                Object.Destroy(gameObject.transform.GetChild(i).gameObject);
            }
        }

        public static void EnableChildren(this GameObject gameObject)
        {
            for (var i = 0; i < gameObject.transform.childCount; i++)
            {
                gameObject.transform.GetChild(i).gameObject.SetVisible(true);
            }
        }

        public static void DisableChildren(this GameObject gameObject)
        {
            for (var i = 0; i < gameObject.transform.childCount; i++)
            {
                gameObject.transform.GetChild(i).gameObject.SetVisible(false);
            }
        }

        public static bool IsLayer(this GameObject gameObject, string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);

            if (layer == -1)
            {
                CSDebug.LogError($"Layer - {layerName} not found.");
            }

            return gameObject.layer == layer;
        }

        /// <summary>
        /// Tries to fetch a component on this GameObject which implements TInterface.
        /// </summary>
        public static bool TryGetInterface<TInterface>(this GameObject go, out TInterface iface)
            where TInterface : class
        {
            iface = go
                .GetComponents<Component>()    // grab all Components
                .OfType<TInterface>()          // filter by your interface
                .FirstOrDefault();             // take the first, or null

            return iface != null;
        }
    }
}

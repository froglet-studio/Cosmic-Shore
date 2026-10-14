using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CosmicShore.UI
{
    /// <summary>
    /// Scales every <see cref="EventSystem.pixelDragThreshold"/> to the display's pixel density, so
    /// a tap stays a tap on a phone.
    ///
    /// <para>The threshold is in raw pixels, and the scenes author Unity's default of 10. On a
    /// 96 dpi monitor that is 2.6 mm; on a 460 ppi iPhone it is 0.55 mm, less than any fingertip
    /// moves during a tap. Every tap on a card or row inside a scroll view then became a drag: the
    /// input module cancelled the click and nudged the list instead. A mouse barely moves during a
    /// click, and the wheel takes a different path (<see cref="IScrollHandler"/>), so none of this
    /// shows on a desktop.</para>
    ///
    /// <para>The authored value is treated as density-independent at <see cref="ReferenceDpi"/>,
    /// roughly an iOS point at 1x, which also matches UIKit's own ~10 pt pan threshold. The result
    /// never goes below what was authored, so a desktop is unchanged and a larger authored value
    /// wins. It is idempotent, so an EventSystem that survives scene loads is not scaled twice.</para>
    ///
    /// <para>Applied to every EventSystem when each scene loads. An EventSystem created later at
    /// runtime (the privacy overlay and the diagnostics HUD make one only when none exists) keeps
    /// its authored value until the next scene load.</para>
    /// </summary>
    public static class TouchDragThreshold
    {
        /// <summary>The density at which an authored threshold means exactly what it says.</summary>
        public const float ReferenceDpi = 160f;

        /// <summary>Unity's default threshold, used as the density-independent base.</summary>
        public const int BaseThreshold = 10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToAll();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToAll();

        /// <summary>Applies <see cref="Scaled"/> to every EventSystem currently loaded.</summary>
        public static void ApplyToAll()
        {
            float dpi = Screen.dpi;
            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                eventSystem.pixelDragThreshold = Scaled(eventSystem.pixelDragThreshold, dpi);
        }

        /// <summary>
        /// The threshold for a display of <paramref name="dpi"/>: <see cref="BaseThreshold"/>
        /// scaled from <see cref="ReferenceDpi"/>, never below <paramref name="current"/>. An unknown
        /// density (0 or less, which some devices report) leaves it unchanged. Pure; unit-tested.
        /// </summary>
        public static int Scaled(int current, float dpi)
        {
            if (dpi <= 0f) return current;
            int scaled = Mathf.FloorToInt(BaseThreshold * dpi / ReferenceDpi + 0.5f);
            return Mathf.Max(current, scaled);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Screen regions that OWN a touch which starts on them, so touch flight does not also read that
    /// finger as a thumb. The ability cards register here (<c>AbilityCardPress</c>): a tap on a card
    /// is its ability's button, and without the claim the same finger also became a third thumb (the
    /// three-finger path), or its landing and lifting fired the one-thumb drift edges.
    ///
    /// <para>Decided by where the touch STARTED, so a thumb that slides onto a card mid-flight keeps
    /// steering and a finger that slides off a card keeps pressing it. Stateless: a region that is
    /// disabled or destroyed stops claiming on the next frame.</para>
    /// </summary>
    public static class FlightTouchClaims
    {
        struct Region
        {
            public RectTransform Rect;
            public Canvas Canvas;
        }

        static readonly List<Region> s_regions = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_regions.Clear();

        public static void Add(RectTransform rect)
        {
            if (!rect) return;
            for (int i = 0; i < s_regions.Count; i++)
                if (s_regions[i].Rect == rect) return;
            s_regions.Add(new Region { Rect = rect, Canvas = rect.GetComponentInParent<Canvas>() });
        }

        public static void Remove(RectTransform rect)
        {
            for (int i = s_regions.Count - 1; i >= 0; i--)
                if (s_regions[i].Rect == rect) s_regions.RemoveAt(i);
        }

        /// <summary>True when a touch that started at <paramref name="screenPoint"/> belongs to a region.</summary>
        public static bool Claims(Vector2 screenPoint)
        {
            for (int i = s_regions.Count - 1; i >= 0; i--)
            {
                var region = s_regions[i];
                if (!region.Rect)
                {
                    s_regions.RemoveAt(i);
                    continue;
                }
                if (!region.Rect.gameObject.activeInHierarchy) continue;

                var root = region.Canvas ? region.Canvas.rootCanvas : null;
                Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(region.Rect, screenPoint, cam))
                    return true;
            }
            return false;
        }
    }
}

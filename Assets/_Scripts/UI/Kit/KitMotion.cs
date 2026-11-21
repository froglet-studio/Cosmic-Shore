using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>The §7 easing curves the kit's motion components choose from.</summary>
    public enum KitEase
    {
        OutQuint = 0,
        OutCubic = 1,
        OutBack = 2,
        Linear = 3,
        InOutSine = 4,
    }

    /// <summary>
    /// The style-B motion vocabulary (Docs/STYLE_FOUNDATION.md §7, Docs/MENU_REDESIGN_B.md §13.4): the
    /// four duration tokens, the stagger rule and the easing curves. Every kit motion component reads its
    /// numbers from here, so a token change moves every screen at once.
    ///
    /// <para>All kit motion runs on unscaled time: the menu pauses gameplay time (freestyle, the end of a
    /// match) and a UI that froze with it would read as a hang.</para>
    /// </summary>
    public static class KitMotion
    {
        /// <summary>Hover, tint, focus.</summary>
        public const float Micro = 0.12f;
        /// <summary>Press, toggle, tab change, a route redrawing.</summary>
        public const float Std = 0.2f;
        /// <summary>Modal, screen slide, toast, a panel rising in.</summary>
        public const float Panel = 0.32f;
        /// <summary>Quest claim and end-game reveal only.</summary>
        public const float Ceremony = 0.5f;
        /// <summary>The per-item stagger of a list entrance.</summary>
        public const float Stagger = 0.04f;
        /// <summary>Items past this index enter with the last one, so a long list is never slow to settle.</summary>
        public const int StaggerCap = 8;

        /// <summary>The entrance delay of the index-th item of a list.</summary>
        public static float StaggerDelay(int index) => Mathf.Clamp(index, 0, StaggerCap) * Stagger;

        /// <summary>Eased progress for t in [0, 1]. OutBack overshoots past 1 before settling.</summary>
        public static float Ease(KitEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case KitEase.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case KitEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case KitEase.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case KitEase.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                default: return t;
            }
        }
    }
}

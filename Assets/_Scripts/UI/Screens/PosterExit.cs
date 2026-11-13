using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The boot poster's exit (Docs/MENU_REDESIGN_B.md section 185): as the splash dissolves into the Toy Box, the
    /// poster grows a little about its centre, so the player seems to move THROUGH it into the world instead of the
    /// world popping open behind a fading picture. It reads the fade from the groups above it (the splash overlay
    /// <c>SceneTransitionManager</c> fades), so it needs no wiring and runs the same in reverse when the splash comes
    /// back over the world on a return to the menu.
    ///
    /// <para>Zero allocations; it writes the scale only when it changes, so it costs nothing while the splash is
    /// fully up or fully gone. On the sign-in scene nothing fades the poster, so it holds at scale 1.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Cosmic Shore/UI/Poster Exit")]
    public class PosterExit : MonoBehaviour
    {
        [Tooltip("How much larger the poster is when the splash has fully dissolved: 0.12 is 12 percent.")]
        [SerializeField, Range(0f, 0.5f)] float push = 0.12f;

        KitSightline _sight;
        float _applied = -1f;

        /// <summary>The poster's scale at a splash opacity: 1 when opaque, 1 + push when gone, eased between.</summary>
        public static float ScaleFor(float overlayAlpha, float push)
        {
            float t = 1f - Mathf.Clamp01(overlayAlpha);
            return 1f + Mathf.Max(0f, push) * (t * t * (3f - 2f * t));
        }

        void Awake() => _sight = new KitSightline(transform, null, null);

        void OnTransformParentChanged() => _sight?.Refresh(transform);

        void LateUpdate()
        {
            float scale = ScaleFor(_sight.Alpha, push);
            if (Mathf.Approximately(scale, _applied)) return;
            _applied = scale;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}

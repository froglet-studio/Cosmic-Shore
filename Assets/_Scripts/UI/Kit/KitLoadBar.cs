using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The slim loading bar of the start flow (Docs/MENU_REDESIGN_B.md sections 25-26): the Bootstrap
    /// splash, the sign-in scene and the environment veil all draw the same 2 px line. It reads the
    /// prism builder directly, so every surface tells the same truth: before a build starts a short
    /// segment drifts (honest about not knowing yet), while laying it fills to the lay progress, while
    /// the laid prisms settle it holds full (<see cref="EnvironmentLoadVeil.BarSpan"/>).
    ///
    /// <para><see cref="fill"/> is positioned by its anchors along this object's width, so the bar
    /// needs no size of its own. It sleeps while nobody can see it (<see cref="KitSightline"/>) - the
    /// splash spends most of the session faded out.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Load Bar")]
    public class KitLoadBar : MonoBehaviour
    {
        [Tooltip("The lit segment: a child of this track, anchored along its width by the bar.")]
        [SerializeField] private RectTransform fill;

        private float _clock;
        private KitSightline _sight;

        /// <summary>The lit segment (set by code that builds a bar at runtime).</summary>
        public RectTransform Fill
        {
            get => fill;
            set => fill = value;
        }

        private void Awake() => _sight = new KitSightline(transform, null, null);

        private void OnTransformParentChanged() => _sight?.Refresh(transform);

        private void Update()
        {
            if (!fill || !_sight.Visible) return;
            _clock += Time.unscaledDeltaTime;
            var span = EnvironmentLoadVeil.BarSpan(PrismTrailBuilder.IsLayingInProgress, PrismTrailBuilder.LayProgress,
                                                   PrismTrailBuilder.GrowRemainingCount > 0, _clock);
            fill.anchorMin = new Vector2(span.x, 0f);
            fill.anchorMax = new Vector2(span.y, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}

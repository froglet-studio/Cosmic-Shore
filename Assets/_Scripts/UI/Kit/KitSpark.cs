using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The travelling highlight of Docs/MENU_REDESIGN_B.md §13.4: a short bright streak that runs along
    /// its parent - a live route, a reached progress rail, the local player's row - or sweeps across a
    /// primary button. The streak is this object (an Image with the kit's spark sprite); its parent
    /// carries a RectMask2D so it is clipped to the line or the button face.
    ///
    /// <para>A spark is ambient (decision Q1): it says nothing a static route does not, and can be
    /// switched off per instance. A sweep is the same component with a pause between passes.</para>
    ///
    /// <para>It sleeps while nobody can see it (<see cref="KitSightline"/>). The cull test reads the
    /// PARENT's renderer: the streak parks itself outside its own mask between passes, so its own
    /// renderer is culled exactly when it is meant to wake.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Spark")]
    [RequireComponent(typeof(RectTransform))]
    public class KitSpark : MonoBehaviour
    {
        [Tooltip("Run along the parent's width (a horizontal line, a button) or its height (a vertical drop).")]
        [SerializeField] private bool vertical;
        [Tooltip("Seconds for one pass, pause included.")]
        [SerializeField, Min(0.1f)] private float period = 1.8f;
        [Tooltip("Share of the period spent off the line between passes: 0 for a spark, about 0.45 for a button sweep.")]
        [SerializeField, Range(0f, 0.9f)] private float pause;
        [Tooltip("Seconds before the first pass, so a route draws in before its spark runs.")]
        [SerializeField, Min(0f)] private float delay = 0.4f;

        private RectTransform _rect;
        private RectTransform _parent;
        private float _clock;
        private KitSightline _sight;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parent = transform.parent as RectTransform;
            _sight = new KitSightline(transform, null, _parent ? _parent.GetComponent<CanvasRenderer>() : null);
        }

        private void OnTransformParentChanged()
        {
            _parent = transform.parent as RectTransform;
            _sight = new KitSightline(transform, null, _parent ? _parent.GetComponent<CanvasRenderer>() : null);
        }

        private void OnEnable() => _clock = -delay;

        private void Update()
        {
            if (!_parent || !_sight.Visible) return;
            _clock += Time.unscaledDeltaTime;
            float length = vertical ? _rect.rect.height : _rect.rect.width;
            float span = (vertical ? _parent.rect.height : _parent.rect.width) + length;
            float t = _clock < 0f ? -1f : (_clock % period) / period;
            float run = 1f - pause;
            float along = t < 0f || t > run ? -length * 2f : -length + span * (t / run);
            var p = _rect.anchoredPosition;
            if (vertical) p.y = -along; else p.x = along;
            _rect.anchoredPosition = p;
        }
    }
}

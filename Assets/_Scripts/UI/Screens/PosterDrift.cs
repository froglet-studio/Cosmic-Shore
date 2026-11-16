using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The slow motion of the boot poster (Rev 94 C, Docs/MENU_REDESIGN_B.md section 177.2): one RectTransform
    /// travelling between two poses and back, eased both ways, for ever. One component covers every motion on the
    /// poster: a hull's bob and a prism run's (an offset), a creature's glide (a diagonal offset), a plant's sway
    /// and a crystal's rock (a rotation, about the rect's pivot), the far cell's push-in (a scale and an offset).
    ///
    /// <para>Poses are relative to where the object was authored, so the layout stays the generator's. Unscaled
    /// time: the splash also covers a return from a paused match. It sleeps while nobody can see it
    /// (<see cref="KitSightline"/>), and allocates nothing per frame.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Cosmic Shore/UI/Poster Drift")]
    public class PosterDrift : MonoBehaviour
    {
        [Header("From")]
        [Tooltip("Offset from the authored position at one end of the travel, in canvas units.")]
        [SerializeField] Vector2 offsetFrom;
        [Tooltip("Turn from the authored rotation at that end, in degrees (counter-clockwise).")]
        [SerializeField] float turnFrom;
        [Tooltip("Scale at that end, as a multiple of the authored scale.")]
        [SerializeField, Min(0.01f)] float scaleFrom = 1f;

        [Header("To")]
        [Tooltip("Offset from the authored position at the other end.")]
        [SerializeField] Vector2 offsetTo;
        [Tooltip("Turn from the authored rotation at the other end, in degrees.")]
        [SerializeField] float turnTo;
        [Tooltip("Scale at the other end.")]
        [SerializeField, Min(0.01f)] float scaleTo = 1f;

        [Header("Timing")]
        [Tooltip("Seconds one way. The way back takes the same.")]
        [SerializeField, Min(0.1f)] float seconds = 8f;
        [Tooltip("Where in the travel it starts, 0..2: 0 at From, 1 at To, 2 back at From.")]
        [SerializeField, Range(0f, 2f)] float phase;

        RectTransform _rect;
        Vector2 _restPosition;
        Quaternion _restRotation;
        Vector3 _restScale;
        float _clock;
        KitSightline _sight;

        /// <summary>
        /// Where the travel stands, 0 at From and 1 at To, after <paramref name="cycles"/> one-way trips: a
        /// ping-pong eased both ways, so the object slows into each end and never pops or restarts.
        /// </summary>
        public static float Travel(float cycles)
        {
            float p = Mathf.PingPong(cycles, 1f);
            return p * p * (3f - 2f * p);
        }

        void Awake()
        {
            _rect = (RectTransform)transform;
            _sight = new KitSightline(transform, null, null);
        }

        void OnTransformParentChanged() => _sight?.Refresh(transform);

        void OnEnable()
        {
            _restPosition = _rect.anchoredPosition;
            _restRotation = _rect.localRotation;
            _restScale = _rect.localScale;
            _clock = phase * seconds;
            Pose();
        }

        void OnDisable()
        {
            _rect.anchoredPosition = _restPosition;
            _rect.localRotation = _restRotation;
            _rect.localScale = _restScale;
        }

        void Update()
        {
            if (!_sight.Visible) return;
            _clock += Time.unscaledDeltaTime;
            Pose();
        }

        void Pose()
        {
            float t = Travel(_clock / seconds);
            _rect.anchoredPosition = _restPosition + Vector2.LerpUnclamped(offsetFrom, offsetTo, t);
            _rect.localRotation = _restRotation * Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(turnFrom, turnTo, t));
            _rect.localScale = _restScale * Mathf.LerpUnclamped(scaleFrom, scaleTo, t);
        }
    }
}

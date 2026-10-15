using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>How a <see cref="KitEntrance"/> brings its object in.</summary>
    public enum KitEntranceMode
    {
        /// <summary>Fades in while rising from below.</summary>
        Rise = 0,
        /// <summary>Fades in while dropping from above (toasts, a refusal strip).</summary>
        Drop = 1,
        /// <summary>Scales up past full size and settles (a pick, a claim, a formed crystal).</summary>
        Pop = 2,
        /// <summary>Grows from its pivot along x (a bar, a rail). Put the pivot on the left.</summary>
        GrowX = 3,
        /// <summary>Grows from its pivot along y (a meter bar). Put the pivot on the bottom.</summary>
        GrowY = 4,
        /// <summary>Fades in where it stands.</summary>
        Fade = 5,
    }

    /// <summary>
    /// A one-shot entrance (Docs/MENU_REDESIGN_B.md §13.4): plays when the object is enabled, or on
    /// <see cref="Play"/>, after its delay, then leaves the object exactly at rest. Lists stagger by
    /// <see cref="KitMotion.StaggerDelay"/>; set <see cref="staggerBySibling"/> on a row template and
    /// every spawned row picks its own delay from where it lands.
    ///
    /// <para>Alpha goes through this object's own <see cref="CanvasGroup"/>, which must not be the group
    /// a <see cref="KitVisibility"/> owner drives - put the entrance on the part's content child.
    /// Position and scale are offsets from the pose the object had when the entrance started, so a
    /// layout group that placed it is not fought.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Entrance")]
    [DisallowMultipleComponent]
    public class KitEntrance : MonoBehaviour
    {
        [Header("Motion")]
        [Tooltip("How the object comes in.")]
        [SerializeField] private KitEntranceMode mode = KitEntranceMode.Rise;
        [Tooltip("The easing curve. Panel entrances use OutQuint, a redraw OutCubic, a pop OutBack.")]
        [SerializeField] private KitEase ease = KitEase.OutQuint;
        [Tooltip("Seconds. 0.32 is the panel token; 0.2 std; 0.5 ceremony (claims only).")]
        [SerializeField, Min(0f)] private float duration = KitMotion.Panel;
        [Tooltip("How far a Rise or Drop travels, in canvas units.")]
        [SerializeField, Min(0f)] private float distance = 14f;

        [Header("Timing")]
        [Tooltip("Seconds before the entrance starts, added to the stagger.")]
        [SerializeField, Min(0f)] private float delay;
        [Tooltip("Stagger slot (0 = first). Ignored when Stagger By Sibling is on.")]
        [SerializeField, Min(0)] private int staggerIndex;
        [Tooltip("Take the stagger slot from the sibling index, for rows a view spawns from a template.")]
        [SerializeField] private bool staggerBySibling;
        [Tooltip("Play whenever the object is enabled. Off: only Play() starts it.")]
        [SerializeField] private bool playOnEnable = true;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private Vector2 _lastWritten;
        private Vector3 _restScale = Vector3.one;
        private float _clock = -1f;

        public bool IsPlaying => _clock >= 0f;

        private void Awake()
        {
            _rect = transform as RectTransform;
            TryGetComponent(out _group);
        }

        private void OnEnable()
        {
            if (playOnEnable) Play();
        }

        private void OnDisable() => Finish();

        /// <summary>Restart the entrance from its first frame.</summary>
        public void Play()
        {
            if (!_rect) return;
            if (IsPlaying) Finish();
            _restPosition = _rect.anchoredPosition;
            _lastWritten = _restPosition;
            _restScale = _rect.localScale;
            _clock = 0f;
            Apply(0f);
        }

        /// <summary>Jump to rest and stop.</summary>
        public void Finish()
        {
            if (!IsPlaying || !_rect) return;
            _clock = -1f;
            if (mode == KitEntranceMode.Rise || mode == KitEntranceMode.Drop)
                _restPosition = ReconcileRest(_rect.anchoredPosition, _lastWritten, _restPosition);
            _rect.anchoredPosition = _restPosition;
            _rect.localScale = _restScale;
            if (_group) _group.alpha = 1f;
        }

        private float TotalDelay =>
            delay + KitMotion.StaggerDelay(staggerBySibling ? transform.GetSiblingIndex() : staggerIndex);

        private void Update()
        {
            if (!IsPlaying) return;
            _clock += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : (_clock - TotalDelay) / duration;
            if (t >= 1f) { Finish(); return; }
            Apply(Mathf.Max(0f, t));
        }

        /// <summary>
        /// Where the object should come to rest, given where it is now and where this entrance last
        /// put it. If something ELSE moved it since - a layout group placing a row that was enabled
        /// before the layout ran, which is every row a view spawns - that new position is the rest
        /// pose; otherwise the rest pose stands. Without this a Rise inside a layout group kept
        /// writing the pre-layout position and Finish() snapped the row there.
        /// </summary>
        public static Vector2 ReconcileRest(Vector2 current, Vector2 lastWritten, Vector2 rest)
            => (current - lastWritten).sqrMagnitude > 0.0001f ? current : rest;

        private void SetPosition(Vector2 offset)
        {
            _restPosition = ReconcileRest(_rect.anchoredPosition, _lastWritten, _restPosition);
            _lastWritten = _restPosition + offset;
            _rect.anchoredPosition = _lastWritten;
        }

        private void Apply(float t)
        {
            float e = KitMotion.Ease(ease, t);
            float fade = Mathf.Clamp01(t <= 0f ? 0f : e);
            switch (mode)
            {
                case KitEntranceMode.Rise:
                    SetPosition(Vector2.down * (distance * (1f - e)));
                    SetAlpha(fade);
                    break;
                case KitEntranceMode.Drop:
                    SetPosition(Vector2.up * (distance * (1f - e)));
                    SetAlpha(fade);
                    break;
                case KitEntranceMode.Pop:
                    _rect.localScale = _restScale * Mathf.LerpUnclamped(0.6f, 1f, e);
                    SetAlpha(Mathf.Clamp01(t * 2f));
                    break;
                case KitEntranceMode.GrowX:
                    _rect.localScale = new Vector3(_restScale.x * Mathf.Max(0f, e), _restScale.y, _restScale.z);
                    break;
                case KitEntranceMode.GrowY:
                    _rect.localScale = new Vector3(_restScale.x, _restScale.y * Mathf.Max(0f, e), _restScale.z);
                    break;
                case KitEntranceMode.Fade:
                    SetAlpha(fade);
                    break;
            }
        }

        private void SetAlpha(float a)
        {
            if (_group) _group.alpha = a;
        }
    }
}

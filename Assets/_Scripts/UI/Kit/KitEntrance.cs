using System.Collections.Generic;
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
    /// <para>Alpha goes through this object's own <see cref="CanvasGroup"/>. A <see cref="KitVisibility"/>
    /// hide finishes the entrance before it writes (a hide that lands mid-fade would be adopted by
    /// <see cref="ReconcileAlpha"/>, but one that lands in the delay, while the entrance itself holds
    /// alpha at 0, would not). Any other code that zeroes the group must call <see cref="Finish"/> first.
    /// Position and scale are offsets from the pose the object had when the entrance started, so a
    /// layout group that placed it is not fought. Alpha works the same way: the entrance fades up to
    /// the alpha the group had when it started, so a tile its owner dimmed or a part its owner hid
    /// keeps that state through a replay.</para>
    ///
    /// <para>Menu windows and screens never deactivate - they hide by CanvasGroup alpha - so OnEnable
    /// fires once, at scene load, behind a closed window. <see cref="ReplayUnder"/> is the enable they
    /// never get: <c>ModalWindowManager</c> calls it on open and <c>ScreenSwitcher</c> on screen
    /// enter, so every play-on-enable entrance in a window plays each time the window appears.</para>
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
        private float _restAlpha = 1f;
        private float _lastAlpha = 1f;
        private float _clock = -1f;

        public bool IsPlaying => _clock >= 0f;

        /// <summary>Where the object stands at rest: the pose the entrance is bringing it to while it
        /// plays, otherwise where it is. Read this, not anchoredPosition, to learn an authored position
        /// that an entrance may be offsetting right now.</summary>
        public Vector2 RestPosition => IsPlaying ? _restPosition
            : transform is RectTransform rect ? rect.anchoredPosition : Vector2.zero;

        private static readonly List<KitEntrance> s_Replay = new();

        /// <summary>
        /// Replay every active play-on-enable entrance under <paramref name="root"/>: the window
        /// appeared, which is what those entrances are for. Entrances that only code starts
        /// (<c>playOnEnable</c> off) are left to that code.
        /// </summary>
        public static void ReplayUnder(Transform root)
        {
            if (!root) return;
            root.GetComponentsInChildren(false, s_Replay);
            foreach (var entrance in s_Replay)
                if (entrance.playOnEnable) entrance.Play();
            s_Replay.Clear();
        }

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
            _restAlpha = _group ? _group.alpha : 1f;
            _lastAlpha = _restAlpha;
            _clock = 0f;
            Apply(0f);
        }

        /// <summary>Jump to rest and stop.</summary>
        public void Finish()
        {
            if (!IsPlaying || !_rect) return;
            _clock = -1f;
            // Only an entrance that MOVES the object puts it back. A Pop / Grow / Fade never wrote the
            // position, so writing it here would only overwrite whatever placed the object since Play()
            // captured it - which, inside a layout group, is the layout itself: the in-game score column
            // (DomainScorePanel, a Pop) was snapped back to its pre-layout (0,0) under the anchors the
            // bar's HorizontalLayoutGroup had just given it, so its CENTRE sat on the bar's top-left
            // corner, half off the top of the screen, in every mode (2026-10-31).
            if (MovesPosition(mode))
            {
                _restPosition = ReconcileRest(_rect.anchoredPosition, _lastWritten, _restPosition);
                _rect.anchoredPosition = _restPosition;
            }
            _rect.localScale = _restScale;
            if (_group) _group.alpha = _restAlpha = ReconcileAlpha(_group.alpha, _lastAlpha, _restAlpha);
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

        /// <summary>True for the entrances that offset the object's position (Rise, Drop) - the only ones
        /// that may write it, while playing or when they finish. Every other mode leaves the position to
        /// whatever owns it (a layout group, a view).</summary>
        public static bool MovesPosition(KitEntranceMode mode) =>
            mode == KitEntranceMode.Rise || mode == KitEntranceMode.Drop;

        /// <summary>
        /// Where the object should come to rest, given where it is now and where this entrance last
        /// put it. If something ELSE moved it since - a layout group placing a row that was enabled
        /// before the layout ran, which is every row a view spawns - that new position is the rest
        /// pose; otherwise the rest pose stands. Without this a Rise inside a layout group kept
        /// writing the pre-layout position and Finish() snapped the row there.
        /// </summary>
        public static Vector2 ReconcileRest(Vector2 current, Vector2 lastWritten, Vector2 rest)
            => (current - lastWritten).sqrMagnitude > 0.0001f ? current : rest;

        /// <summary>
        /// The alpha to come to rest at: the group's alpha when the entrance started, unless
        /// something else wrote the group since - a tile dimming itself, a KitVisibility show or
        /// hide - in which case that value is the rest. Same reasoning as <see cref="ReconcileRest"/>.
        /// </summary>
        public static float ReconcileAlpha(float current, float lastWritten, float rest)
            => Mathf.Abs(current - lastWritten) > 0.001f ? current : rest;

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
            if (!_group) return;
            _restAlpha = ReconcileAlpha(_group.alpha, _lastAlpha, _restAlpha);
            _lastAlpha = _restAlpha * a;
            _group.alpha = _lastAlpha;
        }
    }
}

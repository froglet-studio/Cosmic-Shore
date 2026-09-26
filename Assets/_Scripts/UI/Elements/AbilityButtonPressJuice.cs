using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    /// <summary>
    /// Tactile press feedback for HUD ability buttons: the button squashes down on pointer-down and
    /// springs back with overshoot on release, with an optional light haptic tick. Purely decorative -
    /// it never touches the Button's interaction logic, so it composes with whatever onClick /
    /// input-event wiring the button already has. Drop on any ability button GameObject.
    ///
    /// <para><b>It captures its rest scale LAZILY, and that is load-bearing.</b> It used to cache
    /// <c>transform.localScale</c> in <c>Awake</c> - which runs BEFORE
    /// <see cref="AbilityLockupView"/> re-homes the button into the fleet row and normalises its
    /// scale to 1. Every Squirrel ability button is authored at 0.7, so the juice captured 0.7 and
    /// wrote it back on the first release AND on every <c>OnDisable</c> (a vessel HUD is shown and
    /// hidden routinely) - leaving four cards permanently at 0.7 beside one card at the size the
    /// style asks for. It reads on screen as the ODD CARD being too big, because four wrong cards
    /// agree with each other.</para>
    ///
    /// <para>So this component now only ever restores a scale it took itself: it reads the live
    /// scale at the START of a press, and <c>OnDisable</c> writes nothing until it has. A layout
    /// owner may also hand it the new rest outright through <see cref="SetRestScale"/>, which is
    /// what the lockup does when it places a host. General rule: <b>a rest scale cached before the
    /// thing that OWNS the layout has run is a stale rest scale</b>, and it fails by quietly
    /// restoring the old value rather than by doing nothing.</para>
    /// </summary>
    public class AbilityButtonPressJuice : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Press")]
        [Tooltip("Scale the button squashes to while held.")]
        [SerializeField, Range(0.5f, 1f)] private float pressScale = 0.88f;
        [Tooltip("Seconds to squash down on press.")]
        [SerializeField, Min(0.01f)] private float pressDuration = 0.06f;

        [Header("Release")]
        [Tooltip("Seconds to spring back to rest (OutBack overshoot).")]
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.25f;
        [Tooltip("Overshoot amount of the release spring.")]
        [SerializeField, Min(0f)] private float releaseOvershoot = 2.5f;

        [Header("Haptics")]
        [Tooltip("Haptic amplitude on press. 0 disables the haptic tick.")]
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hapticFrequency = 0.7f;
        [SerializeField, Min(0f)] private float hapticDuration = 0.03f;

        private Vector3 _restScale = Vector3.one;
        private bool _hasRest;
        private Tween _tween;
        private bool _held;

        /// <summary>
        /// Re-anchors the scale this button springs back to. Called by whatever owns the button's
        /// layout after it writes a scale - the ability lockup does it when it places a host - so a
        /// rest captured earlier can never outlive the layout it was captured under.
        /// </summary>
        public void SetRestScale(Vector3 rest)
        {
            _restScale = rest;
            _hasRest = true;
            if (!_held && _tween == null) transform.localScale = rest;
        }

        void EnsureRest()
        {
            if (_hasRest) return;
            _restScale = transform.localScale;
            _hasRest = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureRest();
            _held = true;
            _tween?.Kill();
            _tween = transform.DOScale(_restScale * pressScale, pressDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (hapticAmplitude > 0f)
                HapticController.PlayConstant(hapticAmplitude, hapticFrequency, hapticDuration);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        // A drag-off while held should also relax the button - without this the icon stays squashed.
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_held) Release();
        }

        private void Release()
        {
            _held = false;
            _tween?.Kill();
            _tween = transform.DOScale(_restScale, releaseDuration)
                .SetEase(Ease.OutBack, releaseOvershoot)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
            // Only ever put back a scale this component actually took. Writing an uncaptured rest
            // here is what overwrote the lockup's own layout on the first hide of the HUD.
            if (_hasRest) transform.localScale = _restScale;
            _held = false;
        }
    }
}

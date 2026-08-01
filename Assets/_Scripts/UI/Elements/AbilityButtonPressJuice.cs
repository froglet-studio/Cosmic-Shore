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
        private Tween _tween;
        private bool _held;

        void Awake() => _restScale = transform.localScale;

        public void OnPointerDown(PointerEventData eventData)
        {
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
            transform.localScale = _restScale;
            _held = false;
        }
    }
}

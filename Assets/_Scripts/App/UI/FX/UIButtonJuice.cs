using CosmicShore.App.Systems.Audio;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.App.UI.FX
{
    /// <summary>
    /// Universal button press feedback: scale punch + optional audio.
    /// Works with both touch/mouse (IPointerDown/Up) and gamepad (ISubmitHandler).
    /// Attach to any GameObject with a Button component.
    /// </summary>
    public class UIButtonJuice : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        [Header("Press Animation")]
        [SerializeField] private float pressedScale = 0.92f;
        [SerializeField] private float pressDuration = 0.08f;
        [SerializeField] private float releaseDuration = 0.15f;
        [SerializeField] private Ease pressEase = Ease.OutQuad;
        [SerializeField] private Ease releaseEase = Ease.OutBack;

        [Header("Audio")]
        [SerializeField] private bool playAudio = true;
        [SerializeField] private MenuAudioCategory audioCategory = MenuAudioCategory.OptionClick;

        private Vector3 _originalScale;
        private Tween _scaleTween;

        void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimatePress();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateRelease();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            // Gamepad A-button: quick press-release cycle
            AnimatePress();

            _scaleTween?.OnComplete(() => AnimateRelease());
        }

        private void AnimatePress()
        {
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale * pressedScale, pressDuration)
                .SetEase(pressEase)
                .SetUpdate(true);

            if (playAudio)
                AudioSystem.Instance?.PlayMenuAudio(audioCategory);
        }

        private void AnimateRelease()
        {
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale, releaseDuration)
                .SetEase(releaseEase)
                .SetUpdate(true);
        }

        void OnDisable()
        {
            _scaleTween?.Kill();
            transform.localScale = _originalScale;
        }

        void OnDestroy()
        {
            _scaleTween?.Kill();
        }
    }
}

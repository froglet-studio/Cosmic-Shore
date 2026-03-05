using CosmicShore.App.Systems.Audio;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class SwitchToggle : MonoBehaviour
    {
        [SerializeField] RectTransform handleRectTransform;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.2f;
        [SerializeField] private Ease slideEase = Ease.OutBack;
        [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color offColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Image handleImage;

        Toggle toggle;
        Vector3 handleDisplacement = new Vector3(20, 0, 0);
        private Tween _slideTween;
        private Tween _colorTween;
        private Vector3 _offPosition;
        private Vector3 _onPosition;

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Toggled);

            _offPosition = handleRectTransform.localPosition;
            _onPosition = _offPosition + handleDisplacement;
        }

        public void Toggled(bool status)
        {
            _slideTween?.Kill();
            _colorTween?.Kill();

            Vector3 target = status ? _onPosition : _offPosition;
            _slideTween = handleRectTransform.DOLocalMove(target, slideDuration)
                .SetEase(slideEase)
                .SetUpdate(true);

            if (handleImage != null)
            {
                Color targetColor = status ? onColor : offColor;
                _colorTween = handleImage.DOColor(targetColor, slideDuration)
                    .SetUpdate(true);
            }

            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
        }

        private void OnDestroy()
        {
            _slideTween?.Kill();
            _colorTween?.Kill();
            toggle.onValueChanged.RemoveListener(Toggled);
        }
    }
}

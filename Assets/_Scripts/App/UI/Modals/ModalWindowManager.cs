using System.Collections;
using CosmicShore.App.Systems.Audio;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CosmicShore.App.UI.ScreenSwitcher;

namespace CosmicShore.App.UI.Modals
{
    public class ModalWindowManager : MonoBehaviour
    {
        [Header("Settings")]
        public bool sharpAnimations;

        [SerializeField] public ModalWindows ModalType;

        [SerializeField] Animator windowAnimator;
        bool isOn;

        [Header("DOTween Entrance")]
        [Tooltip("Disable to fall back to Animator-only transitions (not recommended).")]
        [SerializeField] private bool disableDOTweenEntrance;
        [SerializeField] private float tweenDuration = 0.3f;
        [SerializeField] private float entranceStartScale = 0.85f;
        [SerializeField] private Ease entranceEase = Ease.OutBack;
        [SerializeField] private Ease exitEase = Ease.InQuad;

        [Header("Controller Auto-Focus")]
        [Tooltip("First button to select when modal opens (for gamepad navigation).")]
        [SerializeField] private Selectable firstFocusable;

        private CanvasGroup _canvasGroup;
        private Tween _scaleTween;
        private Tween _fadeTween;

        protected virtual void Start()
        {
            if(windowAnimator == null)
                windowAnimator = GetComponent<Animator>();
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup != null) return;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void ModalWindowIn()
        {
            gameObject.SetActive(true);

            if (isOn == false)
            {
                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
                if (screenSwitcher != null)
                    screenSwitcher.PushModal(ModalType);

                if (disableDOTweenEntrance && windowAnimator != null)
                {
                    if (sharpAnimations == false)
                        windowAnimator.CrossFade("Window In", 0.1f);
                    else
                        windowAnimator.Play("Window In");
                }
                else
                {
                    PlayDOTweenIn();
                }

                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OpenView);
                isOn = true;

                // Auto-focus for controller navigation
                AutoFocusFirstSelectable();
            }
        }

        public void ModalWindowOut()
        {
            if (isOn)
            {
                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
                if (screenSwitcher)
                    screenSwitcher.PopModal();

                if (disableDOTweenEntrance && windowAnimator != null)
                {
                    if (sharpAnimations == false)
                        windowAnimator.CrossFade("Window Out", 0.1f);
                    else
                        windowAnimator.Play("Window Out");
                }
                else
                {
                    PlayDOTweenOut();
                }

                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.CloseView);
                isOn = false;
            }
            if(ModalType == ModalWindows.SETTINGS) return;
            StartCoroutine(DisableWindow());
        }

        private void PlayDOTweenIn()
        {
            EnsureCanvasGroup();
            _scaleTween?.Kill();
            _fadeTween?.Kill();

            transform.localScale = Vector3.one * entranceStartScale;
            _canvasGroup.alpha = 0f;

            _scaleTween = transform.DOScale(Vector3.one, tweenDuration)
                .SetEase(entranceEase)
                .SetUpdate(true);

            _fadeTween = _canvasGroup.DOFade(1f, tweenDuration * 0.6f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void PlayDOTweenOut()
        {
            EnsureCanvasGroup();
            _scaleTween?.Kill();
            _fadeTween?.Kill();

            _scaleTween = transform.DOScale(Vector3.one * entranceStartScale, tweenDuration * 0.7f)
                .SetEase(exitEase)
                .SetUpdate(true);

            _fadeTween = _canvasGroup.DOFade(0f, tweenDuration * 0.7f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        private void AutoFocusFirstSelectable()
        {
            if (UnityEngine.InputSystem.Gamepad.current == null) return;
            if (EventSystem.current == null) return;

            if (firstFocusable != null && firstFocusable.gameObject.activeInHierarchy && firstFocusable.interactable)
            {
                EventSystem.current.SetSelectedGameObject(firstFocusable.gameObject);
                return;
            }

            // Fallback: find first interactable button in this modal
            var selectables = GetComponentsInChildren<Selectable>(false);
            foreach (var s in selectables)
            {
                if (s.interactable && s.navigation.mode != UnityEngine.UI.Navigation.Mode.None)
                {
                    EventSystem.current.SetSelectedGameObject(s.gameObject);
                    return;
                }
            }
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            _scaleTween?.Kill();
            _fadeTween?.Kill();
        }
    }
}

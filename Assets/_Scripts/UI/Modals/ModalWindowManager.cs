using System.Collections;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using static CosmicShore.UI.ScreenSwitcher;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ModalWindowManager : MonoBehaviour
    {
        [Inject] protected AudioSystem audioSystem;

        [Header("Settings")]
        public bool sharpAnimations;

        [SerializeField] public ModalWindows ModalType;

        [SerializeField] Animator windowAnimator;
        bool isOn;

        ScreenSwitcher _screenSwitcher;
        CanvasGroup _canvasGroup;

        protected virtual void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if(windowAnimator == null)
                windowAnimator = GetComponent<Animator>();

            _screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
        }

        public void ModalWindowIn()
        {
            // Activate the GO once if it started disabled (scene backward compat).
            // After this, the GO stays active and visibility is CanvasGroup-only.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            SetCanvasGroupVisible(true);

            if (isOn == false)
            {
                if (_screenSwitcher != null)
                    _screenSwitcher.PushModal(ModalType);

                if (sharpAnimations == false)
                    windowAnimator.CrossFade("Window In", 0.1f);
                else
                    windowAnimator.Play("Window In");

                audioSystem.PlayMenuAudio(MenuAudioCategory.OpenView);
                isOn = true;
            }
        }

        public void ModalWindowOut()
        {
            if (isOn)
            {
                if (_screenSwitcher != null)
                    _screenSwitcher.PopModal();

                if (sharpAnimations == false)
                    windowAnimator.CrossFade("Window Out", 0.1f);
                else
                    windowAnimator.Play("Window Out");

                audioSystem.PlayMenuAudio(MenuAudioCategory.CloseView);
                isOn = false;
            }
            if(ModalType == ModalWindows.SETTINGS) return;
            StartCoroutine(DisableWindow());
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            SetCanvasGroupVisible(false);
        }

        protected void SetCanvasGroupVisible(bool visible)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}

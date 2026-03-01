using System.Collections;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using static CosmicShore.UI.ScreenSwitcher;

namespace CosmicShore.UI
{
    public class ModalWindowManager : MonoBehaviour
    {
        [Inject] protected AudioSystem audioSystem;

        [Header("Settings")]
        public bool sharpAnimations;

        [SerializeField] public ModalWindows ModalType;

        [SerializeField] Animator windowAnimator;
        bool isOn;

        ScreenSwitcher _screenSwitcher;

        protected virtual void Start()
        {
            if(windowAnimator == null)
                windowAnimator = GetComponent<Animator>();

            _screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
        }

        public void ModalWindowIn()
        {
            gameObject.SetActive(true);

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
            gameObject.SetActive(false);
        }
    }
}
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

        protected virtual void Start()
        {
            if(windowAnimator == null)
                windowAnimator = GetComponent<Animator>();
                
            //gameObject.SetActive(false);
        }

        public void ModalWindowIn()
        {
            gameObject.SetActive(true);

            if (isOn == false)
            {
                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
                if (screenSwitcher != null)
                    screenSwitcher.PushModal(ModalType);

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
                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
                if (screenSwitcher)
                    screenSwitcher.PopModal();

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
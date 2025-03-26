using System.Collections;
using UnityEngine;
using static CosmicShore.App.UI.ScreenSwitcher;

namespace CosmicShore.App.UI.Modals
{
    public class ModalWindowManager : MonoBehaviour
    {
        [Header("Settings")]
        public bool sharpAnimations;

        [SerializeField] public ModalWindows ModalType;

        Animator windowAnimator;
        bool isOn;

        protected virtual void Start()
        {
            windowAnimator = GetComponent<Animator>();
            
            // We want the elements inside of modal windows to have a chance to run their start functions
            //StartCoroutine(DisableWindow());
            gameObject.SetActive(false);
        }

        public virtual void ModalWindowIn()
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

                isOn = true;
            }
        }

        public virtual void ModalWindowOut()
        {
            if (isOn)
            {
                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
                if (screenSwitcher != null)
                    screenSwitcher.PopModal();

                if (sharpAnimations == false)
                    windowAnimator.CrossFade("Window Out", 0.1f);
                else
                    windowAnimator.Play("Window Out");

                isOn = false;
            }

            StartCoroutine(DisableWindow());
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            gameObject.SetActive(false);
        }
    }
}
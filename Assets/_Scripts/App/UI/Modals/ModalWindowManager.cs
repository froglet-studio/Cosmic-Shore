using System.Collections;
using CosmicShore.App.Systems.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using static CosmicShore.App.UI.ScreenSwitcher;

namespace CosmicShore.App.UI.Modals
{
    public class ModalWindowManager : MonoBehaviour
    {
        [Header("Settings")]
        public bool sharpAnimations;

        [Header("Controller")]
        [Tooltip("When true, pressing gamepad B (East) will close this modal.")]
        [SerializeField] private bool closeOnGamepadB = true;

        [SerializeField] public ModalWindows ModalType;

        [SerializeField] Animator windowAnimator;
        bool isOn;
        Coroutine _disableCoroutine;

        protected virtual void Start()
        {
            if(windowAnimator == null)
                windowAnimator = GetComponent<Animator>();
        }

        protected virtual void Update()
        {
            if (!isOn || !closeOnGamepadB) return;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                ModalWindowOut();
            }
        }

        public void ModalWindowIn()
        {
            // Cancel any pending disable from a previous ModalWindowOut
            if (_disableCoroutine != null)
            {
                StopCoroutine(_disableCoroutine);
                _disableCoroutine = null;
            }

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

                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OpenView);
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

                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.CloseView);
                isOn = false;
            }
            if(ModalType == ModalWindows.SETTINGS) return;
            _disableCoroutine = StartCoroutine(DisableWindow());
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            _disableCoroutine = null;
            gameObject.SetActive(false);
        }
    }
}
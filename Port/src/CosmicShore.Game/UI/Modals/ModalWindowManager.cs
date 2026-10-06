// Ported from Assets/_Scripts/UI/Modals/ModalWindowManager.cs (Arc F) — verbatim with
// the standard substitutions (UnityEngine → CosmicShore.Engine, UnityEngine.InputSystem
// → CosmicShore.Engine.InputSystem, Reflex.Attributes → CosmicShore.Engine.Injection).
// Deviation (marked inline): the Animator window-in/out clips — the engine has no
// Animator; the CanvasGroup show/hide that carries all observable modal state IS live.
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using static CosmicShore.UI.ScreenSwitcher;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ModalWindowManager : MonoBehaviour
    {
        [Inject] protected AudioSystem audioSystem;

        [Header("Settings")]
        public bool sharpAnimations;

        [Header("Controller")]
        [Tooltip("When true, pressing gamepad B (East) will close this modal.")]
        [SerializeField] private bool closeOnGamepadB = true;

        [SerializeField] public ModalWindows ModalType;

        // PORT Deviation (Arc F, Animator — restore when an animation backend lands):
        // [SerializeField] Animator windowAnimator;
        bool isOn;

        [Header("Scene References")]
        [SerializeField] ScreenSwitcher screenSwitcher;

        CanvasGroup _canvasGroup;
        Coroutine _disableCoroutine;

        protected virtual void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // PORT Deviation (Arc F, Animator):
            // if (windowAnimator == null)
            //     windowAnimator = GetComponent<Animator>();

            // Parent containers stay active so OnEnable/OnDisable lifecycle
            // fires for all children. Hide via CanvasGroup to prevent flash.
            if (!isOn)
                SetCanvasGroupVisible(false);
        }

        protected virtual void Update()
        {
            if (!isOn || !closeOnGamepadB) return;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                if (screenSwitcher != null && !screenSwitcher.ModalIsActive(ModalType))
                    return;

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

            // Detect external deactivation (e.g. something calling SetActive(false)
            // or setting CanvasGroup alpha to 0 directly instead of ModalWindowOut).
            // Reset state so the modal can reopen properly.
            bool wasExternallyDeactivated = isOn &&
                (!gameObject.activeSelf || (_canvasGroup && _canvasGroup.alpha < 0.01f));

            if (wasExternallyDeactivated)
            {
                isOn = false;

                if (screenSwitcher != null)
                    screenSwitcher.PopModal();
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            SetCanvasGroupVisible(true);

            if (isOn == false)
            {
                if (screenSwitcher != null)
                    screenSwitcher.PushModal(ModalType);

                // PORT Deviation (Arc F, Animator): windowAnimator CrossFade/Play("Window In")

                audioSystem.PlayMenuAudio(MenuAudioCategory.OpenView);
                isOn = true;
            }
        }

        public void ModalWindowOut()
        {
            if (!isOn) return;

            if (screenSwitcher != null)
                screenSwitcher.PopModal();

            // PORT Deviation (Arc F, Animator): windowAnimator CrossFade/Play("Window Out")

            audioSystem.PlayMenuAudio(MenuAudioCategory.CloseView);
            isOn = false;

            if (ModalType == ModalWindows.SETTINGS) return;

            if (_disableCoroutine != null)
                StopCoroutine(_disableCoroutine);
            _disableCoroutine = StartCoroutine(DisableWindow());
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            SetCanvasGroupVisible(false);
            _disableCoroutine = null;
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

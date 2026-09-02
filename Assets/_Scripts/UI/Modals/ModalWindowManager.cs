using System.Collections;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

        [Header("Input Blocking")]
        [Tooltip("When true, a full-screen transparent raycast blocker is spawned behind the " +
                 "window content so UI beneath the modal (screen buttons, nav bar) cannot be " +
                 "clicked while it is open. It lives under this modal's CanvasGroup, so it " +
                 "blocks only while the modal is visible - no per-scene wiring needed.")]
        [SerializeField] private bool blockUIBehind = true;

        [SerializeField] public ModalWindows ModalType;

        [SerializeField] Animator windowAnimator;
        bool isOn;

        /// <summary>
        /// Raised whenever this modal transitions from open to closed, regardless of the
        /// close path (Close button, gamepad B, code). Lets the owner of the modal react
        /// to dismissals it did not initiate - e.g. PauseMenu resuming the game when the
        /// pause modal is dismissed with gamepad B instead of the Resume button.
        /// </summary>
        public event System.Action OnModalClosed;

        [Header("Scene References")]
        [SerializeField] ScreenSwitcher screenSwitcher;

        /// <summary>The scene's screen switcher, for a subclass that must move the app shell
        /// itself (the arcade modal follows the host onto the arcade screen). May be null.</summary>
        protected ScreenSwitcher Switcher => screenSwitcher;

        /// <summary>
        /// True while this modal is actually being presented. ScreenSwitcher reads it to
        /// reconcile its modal stack against reality: an entry whose modal reports false
        /// is dropped, so a modal closed outside this API can never strand the stack.
        /// </summary>
        public bool IsOpen => isOn && gameObject.activeInHierarchy;

        CanvasGroup _canvasGroup;
        Coroutine _disableCoroutine;
        RectTransform _backdrop;

        // Side length of the backdrop blocker in canvas units. Parented to the window
        // (which may sit anywhere on screen and be scaled by the open animation), so it
        // is deliberately oversized to cover the full screen at any aspect ratio.
        const float BackdropSpan = 8000f;

        protected virtual void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (windowAnimator == null)
                windowAnimator = GetComponent<Animator>();

            EnsureBackdrop();

            // Parent containers stay active so OnEnable/OnDisable lifecycle
            // fires for all children. Hide via CanvasGroup to prevent flash.
            if (!isOn)
                SetCanvasGroupVisible(false);
        }

        /// <summary>
        /// Creates the behind-the-window raycast blocker once, starting inactive. Called
        /// from Start AND ModalWindowIn: modals that start inactive in the scene open
        /// before their Start has run (SetActive + open happen in the same call), and a
        /// subclass declaring its own Awake would silently skip a base Awake hook.
        /// The blocker is toggled explicitly on open/close rather than riding the
        /// CanvasGroup, because SETTINGS-type modals skip the DisableWindow path on
        /// close and leave their group state to the Window Out animation.
        /// </summary>
        void EnsureBackdrop()
        {
            if (!blockUIBehind || _backdrop != null) return;

            var go = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _backdrop = (RectTransform)go.transform;
            _backdrop.SetParent(transform, false);
            _backdrop.SetAsFirstSibling();
            _backdrop.anchorMin = _backdrop.anchorMax = new Vector2(0.5f, 0.5f);
            _backdrop.sizeDelta = new Vector2(BackdropSpan, BackdropSpan);

            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            // Keep layout groups on the window root from repositioning the blocker.
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            go.SetActive(false);
        }

        void SetBackdropActive(bool active)
        {
            if (_backdrop != null)
                _backdrop.gameObject.SetActive(active);
        }

        /// <summary>
        /// Deactivating the GameObject is a legitimate close path in this project - the
        /// Arcade panel's back button SetActive(false)s the modal root directly instead of
        /// calling ModalWindowOut - so unwind the modal stack here too. A leaked stack entry
        /// leaves ScreenSwitcher holding the screens' CanvasGroup non-interactable, which
        /// kills every button on every menu screen with no way back in.
        ///
        /// OnModalClosed is deliberately NOT raised: OnDisable also fires on scene unload and
        /// destruction, where subscribers (e.g. PauseMenu resuming the game) must not be woken.
        /// Subclasses that declare OnDisable must call base.OnDisable() - Unity dispatches the
        /// message to the most-derived declaration only.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (!isOn) return;

            isOn = false;
            SetBackdropActive(false);

            if (screenSwitcher != null)
                screenSwitcher.PopModal(ModalType, this);
        }

        protected virtual void Update()
        {
            if (!isOn || !closeOnGamepadB) return;

            // While a mode-preview window holds input focus, the pad belongs to the VESSEL -
            // every face button is a flight control, so B closing the modal here would yank the
            // player out of the game they are flying. sendNavigationEvents being off does not
            // cover this path: it silences EventSystem-driven UI, and this is a direct device
            // poll that sails straight past it.
            if (ModePreviewWindow.AnyHasFocus) return;

            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                if (screenSwitcher != null && !screenSwitcher.ModalIsActive(ModalType))
                    return;

                ModalWindowOut();
            }
        }

        public void ModalWindowIn()
        {
            // Appshell-owns-the-pad backstop: while the freestyle gate is engaged
            // (ScreenSwitcher keeps EventSystem.sendNavigationEvents false for the whole
            // flight), appshell modals must not open at all — regardless of which input
            // path asked (direct pollers, EventSystem Submit, scene-wired onClick). The
            // appshell UI is hidden and non-raycastable in freestyle, so there is no
            // legitimate open; the freestyle pause menu is not a ModalWindowManager.
            if (!isOn && EventSystem.current && !EventSystem.current.sendNavigationEvents)
                return;

            // First open can happen before Start (modal GameObjects that begin inactive).
            EnsureBackdrop();
            SetBackdropActive(true);

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
                    screenSwitcher.PopModal(ModalType, this);
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            SetCanvasGroupVisible(true);

            if (isOn == false)
            {
                if (screenSwitcher != null)
                    screenSwitcher.PushModal(ModalType, this);

                if (windowAnimator)
                {
                    if (sharpAnimations == false)
                        windowAnimator.CrossFade("Window In", 0.1f);
                    else
                        windowAnimator.Play("Window In");
                }

                audioSystem.PlayMenuAudio(MenuAudioCategory.OpenView);
                isOn = true;
            }
        }

        public void ModalWindowOut()
        {
            if (!isOn) return;

            // Stop blocking the UI behind immediately - the window may still be
            // animating out, but the modal is logically closed.
            SetBackdropActive(false);

            if (screenSwitcher != null)
                screenSwitcher.PopModal(ModalType, this);

            if (windowAnimator)
            {
                if (sharpAnimations == false)
                    windowAnimator.CrossFade("Window Out", 0.1f);
                else
                    windowAnimator.Play("Window Out");
            }

            audioSystem.PlayMenuAudio(MenuAudioCategory.CloseView);
            isOn = false;

            if (ModalType != ModalWindows.SETTINGS)
            {
                if (_disableCoroutine != null)
                    StopCoroutine(_disableCoroutine);
                _disableCoroutine = StartCoroutine(DisableWindow());
            }

            // Raised last so subscribers may deactivate this GameObject without
            // interrupting the close sequence above.
            OnModalClosed?.Invoke();
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

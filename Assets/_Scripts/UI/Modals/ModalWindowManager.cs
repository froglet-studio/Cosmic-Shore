using System.Collections;
using CosmicShore.Core;
using Reflex.Attributes;
using CosmicShore.Utility;
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

        /// <summary>
        /// Whether gamepad B may close this window RIGHT NOW. The serialized flag is a property of
        /// the window; this is a property of its current state, which some windows genuinely have
        /// (the arcade card is a lobby the HOST owns while a guest is in it, so a guest pressing B
        /// would dismiss something they cannot ask for again).
        /// </summary>
        protected virtual bool AllowGamepadBClose => closeOnGamepadB;

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

        /// <summary>
        /// Raised whenever this modal transitions from closed to open - AFTER the stack push
        /// and the "Window In" animation has started, so a listener runs against a window that
        /// is already on its way up. The hook the card grids play their staggered reveal on:
        /// these windows never deactivate (they hide by CanvasGroup alpha), so OnEnable is a
        /// scene-load event here, never an open. Not raised for a re-open of an already-open
        /// modal.
        /// </summary>
        public event System.Action OnModalOpened;

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
            if (!isOn || !AllowGamepadBClose) return;

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
            {
                // Say so. This was the codebase's only silent, log-free modal refusal, and on
                // screen it is indistinguishable from a dead button - a press arrives, is
                // accepted, and nothing opens. That is exactly the symptom the arcade's 13th
                // card produced for five rounds from a completely different cause, so the two
                // must not look alike in a console. Note the flag can also be LEAKED rather than
                // legitimately held: ModePreviewSession clears sendNavigationEvents when a
                // preview takes the stick and restores it only on focus release, so a preview
                // torn down without one leaves every appshell modal refusing to open.
                CSDebug.LogWarningFormat(
                    "{0} - '{1}' declined to open because EventSystem navigation is off (the " +
                    "appshell does not own the pad). If no freestyle flight or preview is " +
                    "active, sendNavigationEvents has been leaked and no modal will open until " +
                    "it is restored.",
                    nameof(ModalWindowManager), name);
                return;
            }

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

                PlayMenuAudio(MenuAudioCategory.OpenView);
                isOn = true;
                OnModalOpened?.Invoke();
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

            PlayMenuAudio(MenuAudioCategory.CloseView);
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

        /// <summary>
        /// The half-second that lets "Window Out" play before the group is switched off.
        ///
        /// <para><b>REALTIME, not scaled.</b> <c>WaitForSeconds</c> is multiplied by
        /// <c>Time.timeScale</c>, and this menu runs PAUSED - <c>PauseSystem.TogglePauseGame</c>
        /// sets <c>timeScale = 0</c> whenever the player is not flying. So a modal closed while
        /// paused started a coroutine that could never finish, <c>SetCanvasGroupVisible(false)</c>
        /// never ran, and the window stayed fully on screen with every other part of the close
        /// (backdrop, modal stack, isOn, OnModalClosed) already done - a modal that is logically
        /// shut and visually still there, which is exactly how it reads to a player: UI in the way
        /// that nothing will take down.
        ///
        /// <para>Everything else timing this UI layer is already unscaled
        /// (<c>ScreenSwitcher.SmoothMove</c>, the freestyle toggle cooldown); this was the
        /// straggler.</para>
        /// </summary>
        IEnumerator DisableWindow()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            SetCanvasGroupVisible(false);
            _disableCoroutine = null;
        }

        /// <summary>
        /// Take this window off screen NOW, whatever it believes its own state to be.
        ///
        /// <para>Exists because <see cref="ModalWindowOut"/> is gated on <c>isOn</c> while callers
        /// reasonably decide what to close by looking at what is VISIBLE, and the two can
        /// disagree: <see cref="ModalWindowIn"/> deliberately refuses to open while the freestyle
        /// gate is engaged (leaving <c>isOn</c> false), and a launch panel makes itself visible
        /// through its own <c>Show()</c> before asking its host modal to open. A modal in that
        /// state ignores every ordinary close request, forever.</para>
        ///
        /// <para>Also skips the animate-out dwell on purpose. This is the "get out of the way"
        /// path - the player is being handed the ship - and half a second of menu over the start
        /// of flight is the complaint, not the remedy.</para>
        /// </summary>
        public void ForceCloseImmediate()
        {
            if (_disableCoroutine != null)
            {
                StopCoroutine(_disableCoroutine);
                _disableCoroutine = null;
            }

            EnsureCanvasGroupCached();
            bool wasVisible = _canvasGroup && _canvasGroup.alpha > 0.01f;

            SetBackdropActive(false);
            SetCanvasGroupVisible(false);

            // The Animator must be moved OFF its open state, not just the CanvasGroup written:
            // "Window In" transitions into "Window Loop", and that clip writes alpha 1,
            // blocksRaycasts 1 and interactable 1 EVERY FRAME - so an open modal force-closed
            // by writing the group alone was back on screen the next frame, and it sat over the
            // whole freestyle flight. Jumping to the END of "Window Out" (which has no outgoing
            // transition) is the pose the normal close settles in; sampled now so this frame
            // agrees with the next one.
            if (windowAnimator && windowAnimator.isActiveAndEnabled)
            {
                windowAnimator.Play("Window Out", 0, 1f);
                windowAnimator.Update(0f);
                SetCanvasGroupVisible(false);
            }

            if (!isOn && !wasVisible) return;

            isOn = false;
            if (screenSwitcher != null)
                screenSwitcher.PopModal(ModalType, this);

            // Raised last, and raised even when isOn was already false: something WAS on screen
            // and is not any more, so subscribers that unwind content (the arcade modal's preview
            // window and launch panel) have to hear about it.
            OnModalClosed?.Invoke();
        }

        void EnsureCanvasGroupCached()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
        }

        bool _warnedAudioFallback;

        /// <summary>
        /// The open/close sting, through the injected system when there is one and through
        /// <c>AudioSystem.Instance</c> when there is not - the same fallback <c>MenuAudio</c>
        /// carries, for the same reason. <c>ModalWindowIn</c>/<c>Out</c> are wired as PERSISTENT
        /// onClick listeners in the scene, and a persistent listener that throws eats every
        /// runtime listener behind it; a modal whose close button did nothing because its audio
        /// field was null would be exactly that defect. Losing the sting is the failure this
        /// buys instead.
        /// </summary>
        protected void PlayMenuAudio(MenuAudioCategory category)
        {
            var system = audioSystem ? audioSystem : AudioSystem.Instance;
            if (!system)
            {
                if (!_warnedAudioFallback)
                {
                    _warnedAudioFallback = true;
                    CSDebug.LogWarningFormat("{0} on '{1}' has no AudioSystem (never injected, and " +
                                             "no instance) - the open/close sting is silent.",
                                             nameof(ModalWindowManager), name);
                }
                return;
            }
            system.PlayMenuAudio(category);
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

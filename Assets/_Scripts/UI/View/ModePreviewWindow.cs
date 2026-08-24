using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arcade modal's preview window, in place of the pre-rendered video clip. It is a fixed
    /// frame that <b>never changes size</b> and shows exactly one of three things:
    ///
    /// <list type="number">
    /// <item><b>Unavailable</b> — "LEVEL PREVIEW NOT AVAILABLE". The honest state for the ~27
    /// modes with no preview definition and for any preview that failed to build. A labelled
    /// frame reads as absent; a white rectangle or a leaked background image reads as broken.</item>
    /// <item><b>Loading</b> — "LOADING PREVIEW…" while the mode's arena stands up.</item>
    /// <item><b>Live</b> — the real gameplay camera, following the real vessel flying the mode's
    /// own arena under AI, rendered into this same frame. The game is simply playing in there.</item>
    /// </list>
    ///
    /// <para><b>Focus, not full screen.</b> Tapping the live window moves input from the UI to the
    /// vessel (the AI hands over); tapping outside, Escape, or gamepad Start hands it back and the
    /// AI resumes. Gamepad <b>B is deliberately NOT a release</b> — while flying, the pad belongs
    /// to the vessel, and B doubling as "close the modal" is exactly the input double-driving this
    /// platform's focus gating exists to prevent (see <see cref="AnyHasFocus"/>).</para>
    ///
    /// <para>The window owns the <see cref="RenderTexture"/> and lends it to the session; it never
    /// owns the arena or the vessel.</para>
    /// </summary>
    public class ModePreviewWindow : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Tooltip("The RawImage inside the modal's preview frame the live camera " +
                                 "renders into.")]
        RawImage surface;

        [SerializeField, Tooltip("Status text centred in the frame: 'preview not available' / " +
                                 "'loading'. Hidden while live.")]
        TMP_Text statusLabel;

        [SerializeField, Tooltip("Button covering the surface. Its click is what asks for focus - " +
                                 "a Button rather than a raw pointer handler, so the window is " +
                                 "reachable with a gamepad's Submit as well as a tap.")]
        Button focusButton;

        [SerializeField, Tooltip("'Tap to play' hint, shown while live and unfocused.")]
        GameObject focusHint;

        [Header("Render texture")]
        [SerializeField, Tooltip("Render texture height in pixels. Width follows the window's own " +
                                 "aspect, so the live view is never letterboxed or stretched.")]
        int renderHeight = 360;

        RenderTexture _renderTexture;
        RectTransform _surfaceRect;

        enum State { Hidden = 0, Unavailable = 1, Loading = 2, Live = 3 }
        State _state = State.Hidden;

        /// <summary>
        /// True while ANY preview window holds input focus. Static so UI that polls the gamepad
        /// directly (ModalWindowManager's B-to-close, most importantly) can decline to act while
        /// the pad belongs to the vessel — <c>EventSystem.sendNavigationEvents</c> only silences
        /// EventSystem-driven UI, and a direct device poll sails straight past it.
        /// </summary>
        public static bool AnyHasFocus { get; private set; }

        /// <summary>Raised when the player taps the live window to take control.</summary>
        public event Action OnFocusRequested;

        /// <summary>Raised when focus is given up (tap outside, Escape, gamepad Start).</summary>
        public event Action OnFocusReleased;

        /// <summary>True while input belongs to this window rather than the UI.</summary>
        public bool HasFocus { get; private set; }

        /// <summary>True while the live gameplay camera is drawing here.</summary>
        public bool IsLive => _state == State.Live;

        /// <summary>The texture the live camera draws into. Created on demand.</summary>
        public RenderTexture LiveTexture
        {
            get
            {
                EnsureRenderTexture();
                return _renderTexture;
            }
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            _surfaceRect = surface ? surface.rectTransform : null;
            if (focusButton) focusButton.onClick.AddListener(HandleFocusButton);
            Apply(State.Hidden);
        }

        void OnDestroy()
        {
            if (focusButton) focusButton.onClick.RemoveListener(HandleFocusButton);
            if (HasFocus) ReleaseFocus();
            ReleaseRenderTexture();
        }

        void OnDisable()
        {
            // The modal can be disabled out from under a focused window (scene transition,
            // external SetActive). Focus must never outlive the thing that granted it.
            if (HasFocus) ReleaseFocus();
        }

        void Update()
        {
            if (HasFocus && WantsRelease())
                ReleaseFocus();
        }

        // ── States (driven by the session) ───────────────────────────────────

        /// <summary>"LEVEL PREVIEW NOT AVAILABLE" — no definition, or the build failed.</summary>
        public void ShowUnavailable()
        {
            if (statusLabel) statusLabel.text = "LEVEL PREVIEW\nNOT AVAILABLE";
            Apply(State.Unavailable);
        }

        /// <summary>"LOADING PREVIEW…" while the arena stands up.</summary>
        public void ShowLoading(string modeName)
        {
            if (statusLabel)
                statusLabel.text = string.IsNullOrEmpty(modeName)
                    ? "LOADING PREVIEW…"
                    : $"LOADING {modeName.ToUpperInvariant()}…";
            Apply(State.Loading);
        }

        /// <summary>The live camera is rendering into <see cref="LiveTexture"/> — show it.</summary>
        public void GoLive()
        {
            EnsureRenderTexture();
            Apply(State.Live);
        }

        /// <summary>Stop showing anything. Safe to call when already hidden.</summary>
        public void Hide() => Apply(State.Hidden);

        void Apply(State state)
        {
            _state = state;
            if (HasFocus && state != State.Live) ReleaseFocus();

            bool live = state == State.Live;
            if (surface)
            {
                surface.texture = live ? _renderTexture : null;
                // Disabled, not tinted: a RawImage with a null texture renders its colour as a
                // solid rectangle — the "just a white image" every non-live state used to show.
                surface.enabled = live;
            }

            if (statusLabel)
                statusLabel.gameObject.SetActive(state is State.Unavailable or State.Loading);

            if (focusButton) focusButton.interactable = live;
            RefreshHint();
        }

        // ── Focus ────────────────────────────────────────────────────────────

        void HandleFocusButton()
        {
            if (_state != State.Live || HasFocus) return;
            OnFocusRequested?.Invoke();
        }

        /// <summary>
        /// Grant focus. Called by the session once it has actually handed input to the vessel —
        /// the window never grants its own focus.
        /// </summary>
        public void GrantFocus()
        {
            if (HasFocus) return;
            HasFocus = true;
            AnyHasFocus = true;
            RefreshHint();
        }

        /// <summary>Give focus back to the UI. Idempotent.</summary>
        public void ReleaseFocus()
        {
            if (!HasFocus) return;
            HasFocus = false;
            AnyHasFocus = false;
            RefreshHint();
            OnFocusReleased?.Invoke();
        }

        /// <summary>
        /// Focus is given up by Escape, gamepad Start, or a tap outside the window. Read from
        /// the devices directly rather than through the EventSystem, because a focused window has
        /// navigation events switched off — that is the point of focus.
        ///
        /// Deliberately NOT gamepad B / East: while flying, every face button belongs to the
        /// vessel. Start mirrors the freestyle exit button and is the one pad button flight
        /// never uses.
        /// </summary>
        bool WantsRelease()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame)
                return true;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            if (PressedOutsideWindow(Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame,
                    Mouse.current?.position.ReadValue() ?? default))
                return true;

            var touch = Touchscreen.current;
            if (touch != null && PressedOutsideWindow(touch.primaryTouch.press.wasPressedThisFrame,
                    touch.primaryTouch.position.ReadValue()))
                return true;

            return false;
        }

        bool PressedOutsideWindow(bool pressed, Vector2 screenPosition)
        {
            if (!pressed || !_surfaceRect) return false;

            var canvas = _surfaceRect.GetComponentInParent<Canvas>();
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return !RectTransformUtility.RectangleContainsScreenPoint(_surfaceRect, screenPosition, cam);
        }

        void RefreshHint()
        {
            if (focusHint) focusHint.SetActive(_state == State.Live && !HasFocus);
        }

        // ── Render texture ───────────────────────────────────────────────────

        void EnsureRenderTexture()
        {
            if (_renderTexture) return;

            int height = Mathf.Max(64, renderHeight);
            float aspect = 16f / 9f;
            if (_surfaceRect && _surfaceRect.rect.height > 1f)
                aspect = Mathf.Clamp(_surfaceRect.rect.width / _surfaceRect.rect.height, 0.25f, 4f);

            _renderTexture = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(height * aspect)), height, 24)
            {
                name = "ModePreviewRT",
                antiAliasing = 1,
                useMipMap = false,
            };
        }

        void ReleaseRenderTexture()
        {
            if (surface) surface.texture = null;
            if (!_renderTexture) return;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}

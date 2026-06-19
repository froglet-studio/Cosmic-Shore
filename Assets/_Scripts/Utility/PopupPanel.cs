using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Lightweight popup card. Two entry points:
    ///  • Legacy single-button info popup — <see cref="SetupPopupPanel"/>, prefab-wired confirm
    ///    button → <see cref="OnConfirmClick"/> (unchanged).
    ///  • SOAP 1/2-button popup — <see cref="Setup(PopupRequest)"/>: dynamic labels, optional
    ///    secondary button, modal blocker, placement, auto-hide, result callback. Buttons are
    ///    code-wired for this path, so a prefab used with Setup(...) must NOT add a persistent
    ///    onClick to the buttons.
    /// Driven by <see cref="PopupManager"/> via <see cref="PopupChannel"/>.
    /// </summary>
    public class PopupPanel : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _mainText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Buttons")]
        [Tooltip("Primary / confirm button. Legacy popups keep their prefab onClick → OnConfirmClick.")]
        [SerializeField] private Button _confirmButton;
        [Tooltip("Label on the primary button (dynamic for SOAP popups).")]
        [SerializeField] private TextMeshProUGUI _confirmLabel;
        [Tooltip("Secondary / cancel button — shown only for 2-button requests.")]
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private TextMeshProUGUI _secondaryLabel;

        [Header("Optional (SOAP popups)")]
        [Tooltip("Full-screen raycast blocker, enabled only for modal requests.")]
        [SerializeField] private GameObject _modalBlocker;
        [Tooltip("The visible card RectTransform — repositioned per PopupPlacement.")]
        [SerializeField] private RectTransform _card;
        [SerializeField] private Image _iconImage;
        [SerializeField] private float _fadeSeconds = 0.15f;
        [Tooltip("Inset from the screen edge (px) for BottomLeft placement.")]
        [SerializeField] private Vector2 _bottomLeftInset = new(24f, 24f);

        private bool _isDisplaying;
        public bool IsDisplaying => _isDisplaying;
        private bool _closableByUser;

        // SOAP-popup state
        public string ReplaceKey { get; private set; }
        private Action<PopupResult> _onResult;
        private bool _resultSent;
        private Coroutine _autoHide;
        private Coroutine _fade;
        private bool _listenersWired;

        private void Awake() => HideImmediate();

        // ── Legacy single-button API (behaviour unchanged) ────────────────────

        public void OnConfirmClick()
        {
            if (_closableByUser) Hide();
        }

        public void SetupPopupPanel(string titleText, string mainText, bool closeableByUser = true)
        {
            _onResult = null;
            ReplaceKey = null;
            if (_titleText) _titleText.text = titleText;
            if (_mainText) _mainText.text = mainText;
            _closableByUser = closeableByUser;
            if (_confirmButton) _confirmButton.gameObject.SetActive(closeableByUser);
            if (_secondaryButton) _secondaryButton.gameObject.SetActive(false);
            if (_modalBlocker) _modalBlocker.SetActive(false);
            ShowInternal();
        }

        // ── SOAP 1/2-button API ───────────────────────────────────────────────

        public void Setup(in PopupRequest request)
        {
            WireListenersOnce();

            _resultSent = false;
            _onResult = request.OnResult;
            ReplaceKey = request.ReplaceKey;
            _closableByUser = true;

            if (_titleText) _titleText.text = request.Title;
            if (_mainText) _mainText.text = request.Message;
            if (_iconImage) { _iconImage.sprite = request.Icon; _iconImage.enabled = request.Icon != null; }

            if (_confirmButton) _confirmButton.gameObject.SetActive(true);
            if (_confirmLabel) _confirmLabel.text = string.IsNullOrEmpty(request.PrimaryLabel) ? "OK" : request.PrimaryLabel;

            if (_secondaryButton) _secondaryButton.gameObject.SetActive(request.HasSecondary);
            if (request.HasSecondary && _secondaryLabel) _secondaryLabel.text = request.SecondaryLabel;

            if (_modalBlocker) _modalBlocker.SetActive(request.Modal);
            ApplyPlacement(request.Placement);

            ShowInternal();

            if (_autoHide != null) StopCoroutine(_autoHide);
            _autoHide = request.AutoHideSeconds > 0f
                ? StartCoroutine(AutoHideRoutine(request.AutoHideSeconds))
                : null;
        }

        /// <summary>Silently hide without firing the result — the action already happened on
        /// another surface (e.g. the invite was accepted/declined from the panel).</summary>
        public void HideSilently()
        {
            _resultSent = true; // suppress any pending auto-hide result
            if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
            _onResult = null;
            FadeTo(0f, hideAfter: true);
        }

        private void WireListenersOnce()
        {
            if (_listenersWired) return;
            _listenersWired = true;
            if (_confirmButton) _confirmButton.onClick.AddListener(() => Resolve(PopupResult.Primary));
            if (_secondaryButton) _secondaryButton.onClick.AddListener(() => Resolve(PopupResult.Secondary));
        }

        private void Resolve(PopupResult result)
        {
            // SOAP path only. Legacy popups have _onResult == null and just hide (mirrors OnConfirmClick).
            if (_resultSent || _onResult == null)
            {
                if (_closableByUser) Hide();
                return;
            }

            _resultSent = true;
            if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
            var cb = _onResult;
            _onResult = null;
            cb.Invoke(result);
            FadeTo(0f, hideAfter: true);
        }

        private IEnumerator AutoHideRoutine(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            _autoHide = null;
            Resolve(PopupResult.Dismissed);
        }

        private void ApplyPlacement(PopupPlacement placement)
        {
            if (!_card) return;
            switch (placement)
            {
                case PopupPlacement.BottomLeft:
                    _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0f, 0f);
                    _card.anchoredPosition = _bottomLeftInset;
                    break;
                default: // Center
                    _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
                    _card.anchoredPosition = Vector2.zero;
                    break;
            }
        }

        // ── Show / hide ───────────────────────────────────────────────────────

        protected virtual void Show() => ShowInternal();

        private void ShowInternal()
        {
            gameObject.SetActive(true);
            _isDisplaying = true;
            if (_canvasGroup) _canvasGroup.blocksRaycasts = true;
            FadeTo(1f);
        }

        public virtual void Hide() => FadeTo(0f, hideAfter: true);

        private void HideImmediate()
        {
            _isDisplaying = false;
            if (_canvasGroup) { _canvasGroup.alpha = 0f; _canvasGroup.blocksRaycasts = false; }
            if (_modalBlocker) _modalBlocker.SetActive(false);
            gameObject.SetActive(false);
        }

        private void FadeTo(float target, bool hideAfter = false)
        {
            if (_fade != null) StopCoroutine(_fade);

            if (!_canvasGroup || !isActiveAndEnabled)
            {
                if (_canvasGroup) _canvasGroup.alpha = target;
                if (hideAfter) HideImmediate();
                return;
            }

            _fade = StartCoroutine(FadeRoutine(target, hideAfter));
        }

        private IEnumerator FadeRoutine(float target, bool hideAfter)
        {
            float start = _canvasGroup.alpha;
            float t = 0f;
            float dur = Mathf.Max(0.01f, _fadeSeconds);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, t / dur);
                yield return null;
            }
            _canvasGroup.alpha = target;
            _fade = null;
            if (hideAfter) HideImmediate();
        }
    }
}

using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Displays a floating "+N" score popup on the vessel's 3D canvas.
    /// Consecutive scores within the combo window stack: +1, +2, +3, etc.
    /// The popup fades and floats upward over the display duration.
    /// </summary>
    public class ScorePopup : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ScorePopupSettingsSO settings;

        [Header("References")]
        [Tooltip("The Vessel3DCanvas to parent the popup text under. " +
                 "If null, will be looked up from VesselStatus.")]
        [SerializeField] private Vessel3DCanvas vessel3DCanvas;

        TMP_Text _label;
        RectTransform _labelRT;
        CanvasGroup _canvasGroup;

        int _comboCount;
        float _comboTimer;
        float _displayTimer;
        bool _isShowing;

        Vector3 _baseLocalPosition;

        Tween _fadeTween;
        Tween _scaleTween;
        Tween _moveTween;

        float DisplayDuration => settings ? settings.displayDuration : 2f;
        float ComboWindow => settings ? settings.comboWindow : 2f;
        float FloatDistance => settings ? settings.floatDistance : 0.5f;
        float StartScale => settings ? settings.startScale : 0.5f;
        float PunchScale => settings ? settings.punchScale : 1.3f;
        float PunchDur => settings ? settings.punchDuration : 0.15f;
        float FontSize => settings ? settings.fontSize : 5f;
        Color TextColor => settings ? settings.textColor : Color.white;

        /// <summary>
        /// Call once after Vessel3DCanvas.Initialize() has run.
        /// </summary>
        public void Initialize(Vessel3DCanvas canvas)
        {
            vessel3DCanvas = canvas;
            BuildLabel();
        }

        /// <summary>
        /// Called when the local player scores a point.
        /// Stacks into combo if called again within the combo window.
        /// </summary>
        public void ShowScorePoint(int points = 1)
        {
            if (!_label) return;

            // Stack combo or reset
            if (_isShowing && _comboTimer > 0f)
                _comboCount += points;
            else
                _comboCount = points;

            _comboTimer = ComboWindow;
            _displayTimer = DisplayDuration;

            _label.text = $"+{_comboCount}";

            PlayAnimation();
        }

        void Update()
        {
            if (!_isShowing) return;

            float dt = Time.deltaTime;

            _comboTimer -= dt;
            _displayTimer -= dt;

            if (_displayTimer <= 0f)
            {
                HidePopup();
                return;
            }

            // Fade out over the last 40% of display duration
            float fadeStart = DisplayDuration * 0.6f;
            if (_displayTimer < fadeStart && _canvasGroup)
            {
                _canvasGroup.alpha = Mathf.Clamp01(_displayTimer / fadeStart);
            }
        }

        void BuildLabel()
        {
            if (!vessel3DCanvas || !vessel3DCanvas.ContentRoot) return;

            var go = new GameObject("ScorePopupLabel");
            go.transform.SetParent(vessel3DCanvas.ContentRoot, false);

            _canvasGroup = go.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            _label = go.AddComponent<TextMeshProUGUI>();
            _label.fontSize = FontSize;
            _label.color = TextColor;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.raycastTarget = false;

            _labelRT = _label.rectTransform;
            _labelRT.anchorMin = new Vector2(0.5f, 0.5f);
            _labelRT.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRT.sizeDelta = new Vector2(200f, 50f);
            _labelRT.anchoredPosition = Vector2.zero;

            _baseLocalPosition = _labelRT.localPosition;
        }

        void PlayAnimation()
        {
            _isShowing = true;

            KillTweens();

            // Reset position and scale
            _labelRT.localPosition = _baseLocalPosition;
            _labelRT.localScale = Vector3.one * StartScale;
            _canvasGroup.alpha = 1f;

            // Scale punch: small → big → normal
            _scaleTween = _labelRT
                .DOScale(Vector3.one * PunchScale, PunchDur)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _scaleTween = _labelRT
                        .DOScale(Vector3.one, PunchDur)
                        .SetEase(Ease.InOutQuad);
                });

            // Float upward over the full display duration
            var targetPos = _baseLocalPosition + Vector3.up * FloatDistance;
            _moveTween = _labelRT
                .DOLocalMove(targetPos, DisplayDuration)
                .SetEase(Ease.OutQuad);
        }

        void HidePopup()
        {
            _isShowing = false;
            _comboCount = 0;
            _comboTimer = 0f;

            KillTweens();

            if (_canvasGroup) _canvasGroup.alpha = 0f;
            if (_labelRT) _labelRT.localPosition = _baseLocalPosition;
        }

        void KillTweens()
        {
            _fadeTween?.Kill();
            _scaleTween?.Kill();
            _moveTween?.Kill();
        }

        void OnDestroy() => KillTweens();
    }
}

using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Displays a floating "+N" score popup on the vessel's 3D canvas.
    /// Consecutive scores within the combo window stack: +1, +2, +3, etc.
    /// The popup fades and floats upward over the display duration.
    ///
    /// Setup: Create a TextMeshProUGUI in the editor on your World Space Canvas,
    /// then assign it to the <see cref="label"/> field.
    /// </summary>
    public class ScorePopup : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ScorePopupSettingsSO settings;

        [Header("References")]
        [Tooltip("Assign the TMP_Text you created on the World Space Canvas.")]
        [SerializeField] private TMP_Text label;

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

        /// <summary>
        /// Call once after the vessel is set up.
        /// </summary>
        public void Initialize()
        {
            if (!label)
            {
                Debug.LogError($"[ScorePopup] No TMP_Text assigned on {gameObject.name}. " +
                               "Create a TextMeshProUGUI in the editor and assign it.", this);
                return;
            }

            _labelRT = label.rectTransform;
            _canvasGroup = label.GetComponent<CanvasGroup>();
            if (!_canvasGroup)
                _canvasGroup = label.gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            _baseLocalPosition = _labelRT.localPosition;
        }

        /// <summary>
        /// Sets the text color — typically the player's domain color.
        /// </summary>
        public void SetColor(Color color)
        {
            if (label) label.color = color;
        }

        /// <summary>
        /// Called when the local player scores a point.
        /// Stacks into combo if called again within the combo window.
        /// </summary>
        public void ShowScorePoint(int points = 1)
        {
            if (!_labelRT) return;

            // Stack combo or reset
            if (_isShowing && _comboTimer > 0f)
                _comboCount += points;
            else
                _comboCount = points;

            _comboTimer = ComboWindow;
            _displayTimer = DisplayDuration;

            label.text = $"+{_comboCount}";

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

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private float colorLerpSpeed = 4f;
        [SerializeField] private float crystalFlashDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float fullBoostWhiteMix = 0.3f;

        [Header("Drift")]
        [SerializeField] private Image driftButtonIcon;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite driftingSprite;
        [SerializeField] private Sprite doubleDriftingSprite;

        [Header("Danger")]
        [SerializeField] private Image dangerRingIcon;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color dangerColor = Color.red;

        [Header("Shield")]
        [SerializeField] private Image shieldIcon;
        [SerializeField] private Color shieldNormalColor = Color.white;
        [SerializeField] private Color shieldActiveColor = Color.green;

        [Header("Icon Juice")]
        [Tooltip("Duration for icon scale punch on events")]
        [SerializeField] private float iconPunchDuration = 0.25f;
        [Tooltip("Scale multiplier for icon punch")]
        [SerializeField] private float iconPunchScale = 1.4f;
        [Tooltip("Duration for color tween back to original")]
        [SerializeField] private float colorTweenDuration = 0.35f;
        [Tooltip("Rotation angle for drift icon (degrees)")]
        [SerializeField] private float driftRotationAngle = 15f;
        [Tooltip("Duration of drift rotation tween")]
        [SerializeField] private float driftRotationDuration = 0.2f;

        private Color _playerDomainColor = Color.white;
        private Color _currentBoostColor = Color.white;
        private Color _targetBoostColor = Color.white;
        private float _flashTimer;

        // Juice tweens
        private Tween _driftIconScaleTween;
        private Tween _driftIconColorTween;
        private Tween _driftIconRotationTween;
        private Tween _dangerScaleTween;
        private Tween _dangerColorTween;
        private Tween _shieldScaleTween;
        private Tween _shieldColorTween;
        private Tween _boostScaleTween;

        private Vector3 _driftIconOriginalScale;
        private Vector3 _dangerIconOriginalScale;
        private Vector3 _shieldIconOriginalScale;
        private Color _driftIconOriginalColor;

        public override void Initialize()
        {
            if (!boostFill) return;
            boostFill.fillAmount = 0f;
            boostFill.color = _playerDomainColor;
            boostFill.enabled = false;

            if (driftButtonIcon)
            {
                driftButtonIcon.sprite = normalSprite;
                _driftIconOriginalScale = driftButtonIcon.rectTransform.localScale;
                _driftIconOriginalColor = driftButtonIcon.color;
            }

            if (dangerRingIcon)
                _dangerIconOriginalScale = dangerRingIcon.rectTransform.localScale;

            if (shieldIcon)
            {
                shieldIcon.color = shieldNormalColor;
                _shieldIconOriginalScale = shieldIcon.rectTransform.localScale;
            }
        }

        public void SetPlayerDomainColor(Color color)
        {
            _playerDomainColor = color;
            _currentBoostColor = color;
            _targetBoostColor = color;

            if (boostFill)
                boostFill.color = color;
        }

        public void SetBoostState(float boost01, bool isBoosted, bool isFull,
            Color sourceColor, bool hasSourceDomain)
        {
            if (!boostFill) return;

            boostFill.enabled = isBoosted;
            boostFill.fillAmount = isBoosted ? Mathf.Clamp01(boost01) : 0f;

            if (!isBoosted)
            {
                _targetBoostColor = _playerDomainColor;
                return;
            }

            if (hasSourceDomain)
            {
                _targetBoostColor = sourceColor;
            }

            if (isFull)
            {
                _targetBoostColor = Color.Lerp(_targetBoostColor, Color.white, fullBoostWhiteMix);
            }
        }

        public void FlashCrystalSurge()
        {
            _flashTimer = crystalFlashDuration;
        }

        private void Update()
        {
            if (!boostFill || !boostFill.enabled) return;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float flashT = Mathf.Clamp01(_flashTimer / crystalFlashDuration);
                _currentBoostColor = Color.Lerp(_targetBoostColor, Color.white, flashT * 0.6f);
            }
            else
            {
                _currentBoostColor = Color.Lerp(
                    _currentBoostColor, _targetBoostColor,
                    colorLerpSpeed * Time.deltaTime);
            }

            boostFill.color = _currentBoostColor;
        }

        // ---------------------------------------------------------------
        // Drift icon with juice: rotation + color shift based on direction
        // ---------------------------------------------------------------
        public void UpdateDriftIcon(bool isDrifting, bool isDoubleDrifting)
        {
            if (!driftButtonIcon) return;

            if (isDrifting && isDoubleDrifting)
                driftButtonIcon.sprite = doubleDriftingSprite;
            else if (isDrifting)
                driftButtonIcon.sprite = driftingSprite;
            else
                driftButtonIcon.sprite = normalSprite;
        }

        /// <summary>
        /// Enhanced drift juice: rotates icon left/right based on drift direction,
        /// tints the icon, and shows double drift sprite when applicable.
        /// </summary>
        public void JuiceDriftStart(bool isLeft, bool isDoubleDrift)
        {
            if (!driftButtonIcon) return;

            // Sprite swap
            driftButtonIcon.sprite = isDoubleDrift ? doubleDriftingSprite : driftingSprite;

            // Rotation toward drift direction
            float targetAngle = isLeft ? driftRotationAngle : -driftRotationAngle;
            _driftIconRotationTween?.Kill();
            _driftIconRotationTween = driftButtonIcon.rectTransform
                .DOLocalRotate(new Vector3(0, 0, targetAngle), driftRotationDuration)
                .SetEase(Ease.OutBack);

            // Color shift
            Color driftColor = isDoubleDrift
                ? new Color(1f, 0.6f, 0.2f, 1f) // warm orange for double drift
                : new Color(0.7f, 0.9f, 1f, 1f); // cool blue for single drift
            _driftIconColorTween?.Kill();
            _driftIconColorTween = driftButtonIcon
                .DOColor(driftColor, driftRotationDuration)
                .SetEase(Ease.OutQuad);

            // Subtle scale punch
            _driftIconScaleTween?.Kill();
            driftButtonIcon.rectTransform.localScale = _driftIconOriginalScale;
            _driftIconScaleTween = driftButtonIcon.rectTransform
                .DOScale(_driftIconOriginalScale * 1.15f, driftRotationDuration * 0.5f)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// Restore drift icon to default state with smooth tween back.
        /// </summary>
        public void JuiceDriftEnd()
        {
            if (!driftButtonIcon) return;

            driftButtonIcon.sprite = normalSprite;

            _driftIconRotationTween?.Kill();
            _driftIconRotationTween = driftButtonIcon.rectTransform
                .DOLocalRotate(Vector3.zero, driftRotationDuration)
                .SetEase(Ease.OutQuad);

            _driftIconColorTween?.Kill();
            _driftIconColorTween = driftButtonIcon
                .DOColor(_driftIconOriginalColor, colorTweenDuration)
                .SetEase(Ease.OutQuad);

            _driftIconScaleTween?.Kill();
            _driftIconScaleTween = driftButtonIcon.rectTransform
                .DOScale(_driftIconOriginalScale, driftRotationDuration)
                .SetEase(Ease.OutQuad);
        }

        // ---------------------------------------------------------------
        // Danger/joust icon with juice: scale punch + red flash
        // ---------------------------------------------------------------
        public void UpdateDangerIcon(bool inDanger)
        {
            if (!dangerRingIcon) return;

            dangerRingIcon.color = inDanger ? dangerColor : normalColor;
        }

        /// <summary>
        /// Joust juice: scale punch + red color tween on the danger icon.
        /// </summary>
        public void JuiceJoust()
        {
            if (!dangerRingIcon) return;

            // Scale punch
            _dangerScaleTween?.Kill();
            dangerRingIcon.rectTransform.localScale = _dangerIconOriginalScale;
            _dangerScaleTween = dangerRingIcon.rectTransform
                .DOScale(_dangerIconOriginalScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _dangerScaleTween = dangerRingIcon.rectTransform
                        .DOScale(_dangerIconOriginalScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce);
                });

            // Color flash: snap to red, tween back
            _dangerColorTween?.Kill();
            dangerRingIcon.color = dangerColor;
            _dangerColorTween = dangerRingIcon
                .DOColor(normalColor, colorTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        // ---------------------------------------------------------------
        // Shield/crystal icon with juice: scale punch + domain color flash
        // ---------------------------------------------------------------
        public void UpdateShieldColor(bool active)
        {
            if (!shieldIcon) return;

            shieldIcon.color = active ? shieldActiveColor : shieldNormalColor;
        }

        /// <summary>
        /// Crystal collection juice: scale punch + tween to domain color and back.
        /// </summary>
        public void JuiceCrystalCollected(Color domainColor)
        {
            if (!shieldIcon) return;

            // Scale punch
            _shieldScaleTween?.Kill();
            shieldIcon.rectTransform.localScale = _shieldIconOriginalScale;
            _shieldScaleTween = shieldIcon.rectTransform
                .DOScale(_shieldIconOriginalScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _shieldScaleTween = shieldIcon.rectTransform
                        .DOScale(_shieldIconOriginalScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce);
                });

            // Color flash: snap to domain color, tween back
            _shieldColorTween?.Kill();
            shieldIcon.color = domainColor;
            _shieldColorTween = shieldIcon
                .DOColor(shieldNormalColor, colorTweenDuration)
                .SetEase(Ease.OutQuad);

            // Also punch the boost fill if visible
            if (boostFill && boostFill.enabled)
            {
                _boostScaleTween?.Kill();
                var boostRT = boostFill.rectTransform;
                var origScale = Vector3.one;
                boostRT.localScale = origScale;
                _boostScaleTween = boostRT
                    .DOScale(origScale * 1.1f, iconPunchDuration * 0.3f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        _boostScaleTween = boostRT
                            .DOScale(origScale, iconPunchDuration * 0.7f)
                            .SetEase(Ease.OutQuad);
                    });
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _driftIconScaleTween?.Kill();
            _driftIconColorTween?.Kill();
            _driftIconRotationTween?.Kill();
            _dangerScaleTween?.Kill();
            _dangerColorTween?.Kill();
            _shieldScaleTween?.Kill();
            _shieldColorTween?.Kill();
            _boostScaleTween?.Kill();
        }
    }
}

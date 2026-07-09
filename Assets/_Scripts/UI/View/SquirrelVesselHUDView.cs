using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
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

        [Header("Impact (joust + crystal share one icon)")]
        [FormerlySerializedAs("dangerRingIcon")]
        [SerializeField] private Image impactIcon;
        [FormerlySerializedAs("normalColor")]
        [SerializeField] private Color impactRestColor = Color.white;
        [FormerlySerializedAs("dangerColor")]
        [SerializeField] private Color joustFlashColor = Color.red;
        [Tooltip("Flash colour when the impact icon fires from collecting a crystal.")]
        [SerializeField] private Color crystalFlashColor = new Color(0.4f, 0.9f, 1f, 1f);

        [Header("Tube Cooldown (repurposed shield slot)")]
        [FormerlySerializedAs("shieldIcon")]
        [SerializeField] private Image tubeCooldownIcon;
        [Tooltip("Colour of the tube cooldown icon while recharging (fill < 1).")]
        [SerializeField] private Color tubeCoolingColor = new Color(1f, 1f, 1f, 0.3f);
        [Tooltip("Colour of the tube cooldown icon once ready (fill == 1).")]
        [SerializeField] private Color tubeReadyColor = Color.white;

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
        private Tween _impactScaleTween;
        private Tween _impactColorTween;
        private Tween _boostScaleTween;

        private Vector3 _driftIconOriginalScale;
        private Vector3 _impactIconOriginalScale;
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

            if (impactIcon)
            {
                impactIcon.color = impactRestColor;
                _impactIconOriginalScale = impactIcon.rectTransform.localScale;
            }

            if (tubeCooldownIcon)
            {
                // Repurposed as a radial cooldown fill: start ready (full + bright).
                tubeCooldownIcon.type = Image.Type.Filled;
                tubeCooldownIcon.fillAmount = 1f;
                tubeCooldownIcon.color = tubeReadyColor;
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
        // Impact icon: ONE icon shared by joust (hit a vessel) and crystal
        // (hit a crystal). Scale punch + a colour flash keyed to the source.
        // ---------------------------------------------------------------
        public void JuiceJoustImpact() => JuiceImpact(joustFlashColor);
        public void JuiceCrystalImpact() => JuiceImpact(crystalFlashColor);

        public void JuiceImpact(Color flashColor)
        {
            if (!impactIcon) return;

            // Scale punch
            _impactScaleTween?.Kill();
            impactIcon.rectTransform.localScale = _impactIconOriginalScale;
            _impactScaleTween = impactIcon.rectTransform
                .DOScale(_impactIconOriginalScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _impactScaleTween = impactIcon.rectTransform
                        .DOScale(_impactIconOriginalScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce);
                });

            // Color flash: snap to the source colour, tween back to rest
            _impactColorTween?.Kill();
            impactIcon.color = flashColor;
            _impactColorTween = impactIcon
                .DOColor(impactRestColor, colorTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        // ---------------------------------------------------------------
        // Tube cooldown: radial fill in the freed slot. ready01: 0 = just
        // deployed (empty), 1 = fully recharged (ready). Driven each frame
        // by the controller polling the tube executor.
        // ---------------------------------------------------------------
        public void SetTubeCooldownReady(float ready01)
        {
            if (!tubeCooldownIcon) return;

            ready01 = Mathf.Clamp01(ready01);
            tubeCooldownIcon.fillAmount = ready01;
            tubeCooldownIcon.color = Color.Lerp(tubeCoolingColor, tubeReadyColor, ready01);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _driftIconScaleTween?.Kill();
            _driftIconColorTween?.Kill();
            _driftIconRotationTween?.Kill();
            _impactScaleTween?.Kill();
            _impactColorTween?.Kill();
            _boostScaleTween?.Kill();
        }
    }
}

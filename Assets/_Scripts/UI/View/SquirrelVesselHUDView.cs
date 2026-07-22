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
        [Tooltip("How far (px) the missile icon sits sunk below rest while the tube reloads - it " +
                 "rises home as the cooldown recovers, then slams into place when ready.")]
        [SerializeField, Min(0f)] private float tubeLoadDropOffset = 14f;
        [Tooltip("Flash colour the instant the tube slams home fully loaded.")]
        [SerializeField] private Color tubeSlamFlashColor = Color.white;

        [Header("Overheat")]
        [Tooltip("The overheat button's icon image (child 'Icon' of OverheatButton).")]
        [SerializeField] private Image overheatIcon;
        [Tooltip("The glow image behind the overheat icon - alpha ramps with heat as a heat gauge.")]
        [SerializeField] private Image overheatHighlight;
        [Tooltip("Ember tint the icon and highlight ramp toward as heat builds.")]
        [SerializeField] private Color overheatHotColor = new Color(1f, 0.45f, 0.15f, 1f);
        [Tooltip("Flash colour the moment the vessel overheats.")]
        [SerializeField] private Color overheatFlashColor = new Color(1f, 0.9f, 0.6f, 1f);
        [Tooltip("Scale throb amplitude of the icon while overheated (danger period).")]
        [SerializeField, Range(0f, 0.5f)] private float overheatThrobAmount = 0.14f;
        [Tooltip("Seconds per half-throb while overheated.")]
        [SerializeField, Min(0.05f)] private float overheatThrobDuration = 0.28f;

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
        private Tween _tubeSlamScaleTween;
        private Tween _tubeSlamColorTween;
        private Tween _overheatThrobTween;
        private Tween _overheatIconColorTween;

        private Vector3 _driftIconOriginalScale;
        private Vector3 _impactIconOriginalScale;
        private Vector3 _tubeIconOriginalScale = Vector3.one;
        private Vector2 _tubeIconRestAnchoredPos;
        private Vector3 _overheatIconOriginalScale = Vector3.one;
        private Color _driftIconOriginalColor;
        private Color _overheatIconOriginalColor = Color.white;
        private bool _tubeWasReady = true;

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
                // Start ready (loaded + bright). The image keeps its authored type: a Filled
                // sprite additionally shows the radial wipe; a plain sprite relies on the
                // sink-and-rise loading motion alone.
                if (tubeCooldownIcon.type == Image.Type.Filled) tubeCooldownIcon.fillAmount = 1f;
                tubeCooldownIcon.color = tubeReadyColor;
                _tubeIconOriginalScale = tubeCooldownIcon.rectTransform.localScale;
                _tubeIconRestAnchoredPos = tubeCooldownIcon.rectTransform.anchoredPosition;
                _tubeWasReady = true;
            }

            if (overheatIcon)
            {
                _overheatIconOriginalScale = overheatIcon.rectTransform.localScale;
                _overheatIconOriginalColor = overheatIcon.color;
            }

            if (overheatHighlight)
            {
                // The highlight doubles as the heat gauge: invisible cold, ember-bright hot.
                var c = overheatHotColor; c.a = 0f;
                overheatHighlight.color = c;
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
            bool isReady = ready01 >= 0.999f;
            var rt = tubeCooldownIcon.rectTransform;

            if (tubeCooldownIcon.type == Image.Type.Filled)
                tubeCooldownIcon.fillAmount = ready01;

            // While the slam flash owns the icon colour, leave it alone.
            if (_tubeSlamColorTween == null)
                tubeCooldownIcon.color = Color.Lerp(tubeCoolingColor, tubeReadyColor, ready01);

            if (isReady && !_tubeWasReady)
            {
                JuiceTubeSlamHome();
            }
            else if (!isReady)
            {
                if (_tubeWasReady)   // just fired: the tube ejects - drop the missile to the reload seat
                {
                    _tubeSlamScaleTween?.Kill();
                    _tubeSlamColorTween?.Kill();
                    rt.localScale = _tubeIconOriginalScale;
                }

                // Reloading: the missile rises from its sunk seat toward rest as the tube loads.
                rt.anchoredPosition = _tubeIconRestAnchoredPos + Vector2.down * (tubeLoadDropOffset * (1f - ready01));
            }

            _tubeWasReady = isReady;
        }

        // Fully loaded: the missile slams home - snap to rest, overshoot punch, bright flash.
        private void JuiceTubeSlamHome()
        {
            var rt = tubeCooldownIcon.rectTransform;

            _tubeSlamScaleTween?.Kill();
            rt.anchoredPosition = _tubeIconRestAnchoredPos;
            rt.localScale = _tubeIconOriginalScale;
            _tubeSlamScaleTween = rt
                .DOScale(_tubeIconOriginalScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .SetLink(tubeCooldownIcon.gameObject)
                .OnComplete(() =>
                {
                    _tubeSlamScaleTween = rt
                        .DOScale(_tubeIconOriginalScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce)
                        .SetLink(tubeCooldownIcon.gameObject);
                });

            _tubeSlamColorTween?.Kill();
            tubeCooldownIcon.color = tubeSlamFlashColor;
            _tubeSlamColorTween = tubeCooldownIcon
                .DOColor(tubeReadyColor, colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(tubeCooldownIcon.gameObject)
                .OnKill(() => _tubeSlamColorTween = null);
        }

        // ---------------------------------------------------------------
        // Overheat: the highlight image is a live heat gauge (alpha ramps
        // with heat), the icon tints toward ember, and the overheated
        // danger period gets a flash + throb until the heat decays.
        // ---------------------------------------------------------------

        /// <summary>Per-frame heat drive. heat01: 0 = cold, 1 = at the overheat threshold.</summary>
        public void SetOverheatHeat(float heat01)
        {
            heat01 = Mathf.Clamp01(heat01);

            if (overheatHighlight)
            {
                var c = overheatHotColor;
                c.a = overheatHotColor.a * heat01;
                overheatHighlight.color = c;
            }

            // While the flash/throb owns the icon colour, leave it alone.
            if (overheatIcon && _overheatIconColorTween == null)
                overheatIcon.color = Color.Lerp(_overheatIconOriginalColor, overheatHotColor, heat01);
        }

        /// <summary>The vessel just overheated - flash and start the danger throb.</summary>
        public void JuiceOverheatEngaged()
        {
            if (!overheatIcon) return;

            _overheatIconColorTween?.Kill();
            overheatIcon.color = overheatFlashColor;
            _overheatIconColorTween = overheatIcon
                .DOColor(overheatHotColor, colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(overheatIcon.gameObject)
                .OnKill(() => _overheatIconColorTween = null);

            _overheatThrobTween?.Kill();
            overheatIcon.rectTransform.localScale = _overheatIconOriginalScale;
            _overheatThrobTween = overheatIcon.rectTransform
                .DOScale(_overheatIconOriginalScale * (1f + overheatThrobAmount), overheatThrobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(overheatIcon.gameObject);
        }

        /// <summary>Heat fully decayed - settle the icon back to rest with a small relief pop.</summary>
        public void JuiceOverheatRecovered()
        {
            if (!overheatIcon) return;

            _overheatThrobTween?.Kill();
            _overheatThrobTween = overheatIcon.rectTransform
                .DOScale(_overheatIconOriginalScale, iconPunchDuration)
                .SetEase(Ease.OutBack)
                .SetLink(overheatIcon.gameObject);

            _overheatIconColorTween?.Kill();
            _overheatIconColorTween = overheatIcon
                .DOColor(_overheatIconOriginalColor, colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(overheatIcon.gameObject)
                .OnKill(() => _overheatIconColorTween = null);
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
            _tubeSlamScaleTween?.Kill();
            _tubeSlamColorTween?.Kill();
            _overheatThrobTween?.Kill();
            _overheatIconColorTween?.Kill();
        }
    }
}

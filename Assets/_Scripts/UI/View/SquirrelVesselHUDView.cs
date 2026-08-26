using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Squirrel's four lower-right ability icons, in the fleet order charge → mass → space → time
    /// (the same order as the element flowers above them), each bound to the element that upgrades it:
    ///
    ///   Charge → skim energy   (overheatIcon + the boost fill) → "Live Wire"
    ///   Mass   → trail volume  (driftButtonIcon)               → "Heavy Trail"
    ///   Space  → skimmer reach (impactIcon, joust + crystal)   → "Shepherd"
    ///   Time   → boost ring    (tubeCooldownIcon)              → "Twin Rings"
    ///
    /// <para>Two of those readouts are now the LOCKUP's, not this view's. The boost fill is bound as
    /// the CHARGE card's gauge - skimming is what banks it, which is what that column says - and the
    /// Boost Ring's recharge is the fleet's standard cooldown veil. This view keeps only what is
    /// genuinely the Squirrel's own: the drift lean, the impact flash, the crystal surge.</para>
    ///
    /// Every one of those icons is also a live gameplay gauge - heat tint, drift lean, impact flash -
    /// repainted per frame or per event. So the upgrade signal here is carried by the card rather
    /// than by the icon's colour (tintIconOnUpgrade is off on this prefab), and the local rest
    /// scales below are re-anchored on every upgrade flip so this view's own tweens can never wipe
    /// the bump.
    /// </summary>
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

        [Header("Boost Ring cooldown (Time slot)")]
        [Tooltip("The Boost Ring ability's icon. Its RECHARGE is drawn by the fleet's standard " +
                 "cooldown - a radial veil swept over this card by the ability lockup - so nothing " +
                 "here animates the icon any more. The bespoke sink-and-rise reload, the breathing " +
                 "pulse, the radial fill on the icon itself and the slam-home flash are all retired " +
                 "with their tuning fields: one recharge readout for the fleet beats four per hull.")]
        [SerializeField] private Image tubeCooldownIcon;

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
        [Tooltip("Rotation angle for drift icon (degrees) - big enough to read at a glance.")]
        [SerializeField] private float driftRotationAngle = 45f;
        [Tooltip("Duration of drift rotation tween - long enough to read as a smooth lean, not a snap.")]
        [SerializeField] private float driftRotationDuration = 0.45f;

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
        private Tween _overheatThrobTween;
        private Tween _overheatIconColorTween;

        private Vector3 _driftIconOriginalScale;
        private Vector3 _impactIconOriginalScale;
        private Vector3 _overheatIconOriginalScale = Vector3.one;
        private Color _driftIconOriginalColor;
        private Color _overheatIconOriginalColor = Color.white;

        public override void Initialize()
        {
            if (!boostFill) return;
            boostFill.fillAmount = 0f;
            boostFill.color = _playerDomainColor;
            boostFill.enabled = false;

            if (driftButtonIcon)
            {
                driftButtonIcon.sprite = normalSprite;
                _driftIconOriginalScale = AbilityIconRestScale(Element.Mass);
                _driftIconOriginalColor = driftButtonIcon.color;
            }

            if (impactIcon)
            {
                impactIcon.color = impactRestColor;
                _impactIconOriginalScale = AbilityIconRestScale(Element.Space);
            }

            if (tubeCooldownIcon)
            {
                // A plain, fully-drawn icon: the lockup's cooldown veil is what says "recharging",
                // so the icon must NOT also be a partial fill or it reads as a second meter.
                if (tubeCooldownIcon.type == Image.Type.Filled) tubeCooldownIcon.fillAmount = 1f;
            }

            if (overheatIcon)
            {
                _overheatIconOriginalScale = AbilityIconRestScale(Element.Charge);
                _overheatIconOriginalColor = overheatIcon.color;
            }

            if (overheatHighlight)
            {
                // The highlight doubles as the heat gauge: invisible cold, ember-bright hot.
                var c = overheatHotColor; c.a = 0f;
                overheatHighlight.color = c;
            }
        }

        /// <summary>
        /// Re-anchors this view's per-icon rest scales to the shared upgrade rest scale, so the drift
        /// lean, the impact punch, the tube reload pulse and the overheat throb all settle back to the
        /// UPGRADED size instead of snapping the bump away. The base call does the sprite swap, the
        /// element badge and the one-shot unlock punch.
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);

            var rest = AbilityIconRestScale(element);
            switch (element)
            {
                case Element.Charge:
                    _overheatIconOriginalScale = rest;
                    break;
                case Element.Mass:
                    _driftIconOriginalScale = rest;
                    break;
                case Element.Space:
                    _impactIconOriginalScale = rest;
                    break;
                case Element.Time:
                    // Nothing local to re-anchor: the Boost Ring's recharge is the lockup's
                    // standard cooldown, which never touches the icon's transform.
                    break;
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

            // Rotation toward drift direction - a wide, smooth lean (OutCubic, no overshoot snap).
            float targetAngle = isLeft ? driftRotationAngle : -driftRotationAngle;
            _driftIconRotationTween?.Kill();
            _driftIconRotationTween = driftButtonIcon.rectTransform
                .DOLocalRotate(new Vector3(0, 0, targetAngle), driftRotationDuration)
                .SetEase(Ease.OutCubic);

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
                .SetEase(Ease.OutCubic);

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
        // Boost Ring cooldown - handed straight to the fleet's standard readout.
        // ---------------------------------------------------------------

        /// <summary>
        /// ready01: 0 = just deployed, 1 = fully recharged. Polled each frame by the controller off
        /// the tube executor.
        ///
        /// <para>The whole body is now one call. What it replaced was a per-vessel reload animation
        /// - the icon sank to a seat and rose back, breathed on a looping yoyo, wiped a radial fill
        /// on itself and slammed home with a colour flash - which was four channels saying one
        /// thing, all of them on the icon, on one hull. The lockup draws recharge the same way for
        /// every vessel, so the signature stays and the presentation leaves.</para>
        /// </summary>
        public void SetTubeCooldownReady(float ready01)
            => SetAbilityCooldown(Element.Time, 1f - Mathf.Clamp01(ready01));

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
            _overheatThrobTween?.Kill();
            _overheatIconColorTween?.Kill();
        }
    }
}

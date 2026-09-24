using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Squirrel's lower-right ability row: the four elemental cards in the fleet order
    /// charge → mass → space → time (the same order as the element flowers above them), plus ONE
    /// non-elemental card to their left.
    ///
    ///   [core]  Skim          (the boost fill, bound as gauge) → no element, no upgrade
    ///   Charge → crystal joust (impactIcon, joust + crystal)   → "Shepherd"
    ///   Mass   → boost ring    (tubeCooldownIcon)              → "Twin Rings"
    ///   Space  → steal         (no local readout)              → "Iron Grip"
    ///   Time   → skim energy scaling                           → "Live Wire"
    ///
    /// <para><b>Skimming draws on a card of its own, and that is the point of it.</b> It is the
    /// hull's engine - always available, no button, no cooldown, and what every other Squirrel
    /// ability spends - so it is bound as <see cref="CoreAbility.Skim"/> and the lockup gives it an
    /// ability plate with NO element flower above it, one pitch left of Charge. The boost fill goes
    /// with it as that card's gauge.</para>
    ///
    /// <para><b>Stated cost: the TIME card has no icon and therefore renders LOCKED.</b> Time still
    /// scales skim energy and still carries "Live Wire", so the flower above that card is doing real
    /// work while the plate below it reads as an ability that does not exist yet. The two honest
    /// resolutions are an ability of Time's own or a third card state meaning <i>this element
    /// upgrades a core ability</i>; both are design calls, so neither is invented here.</para>
    ///
    /// <para>Two of these readouts are the LOCKUP's, not this view's: the boost fill (the core
    /// card's gauge) and the Boost Ring's recharge (the fleet's standard cooldown veil over the MASS
    /// card). This view keeps only what is genuinely the Squirrel's own: the impact flash and the
    /// crystal surge.</para>
    ///
    /// <para>RETIRED with the 2026-09 element re-cut: the drift sprite/lean (the drift is core
    /// flight with no element, and it was hijacking the card that now carries the Boost Ring) and
    /// the overheat gauge (SetOverheatHeat / JuiceOverheat* had had no callers since the Sparrow's
    /// overheat mechanic was deleted - a gauge whose meter is gone is a lie, not a spare part).</para>
    ///
    /// Its remaining icon is a live gameplay gauge - the impact flash - repainted per event. So the
    /// upgrade signal here is carried by the card rather than by the icon's colour
    /// (tintIconOnUpgrade is off on this prefab), and the local rest scale below is re-anchored on
    /// every upgrade flip so this view's own tweens can never wipe the bump.
    /// </summary>
    public sealed class SquirrelVesselHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private float colorLerpSpeed = 4f;
        [SerializeField] private float crystalFlashDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float fullBoostWhiteMix = 0.3f;

        [Header("Impact (joust + crystal share one icon - the CHARGE card)")]
        [FormerlySerializedAs("dangerRingIcon")]
        [SerializeField] private Image impactIcon;
        [FormerlySerializedAs("normalColor")]
        [SerializeField] private Color impactRestColor = Color.white;
        [FormerlySerializedAs("dangerColor")]
        [SerializeField] private Color joustFlashColor = Color.red;
        [Tooltip("Flash colour when the impact icon fires from collecting a crystal.")]
        [SerializeField] private Color crystalFlashColor = new Color(0.4f, 0.9f, 1f, 1f);

        [Header("Boost Ring cooldown (Mass slot)")]
        [Tooltip("The Boost Ring ability's icon. Its RECHARGE is drawn by the fleet's standard " +
                 "cooldown - a radial veil swept over this card by the ability lockup - so nothing " +
                 "here animates the icon any more. The bespoke sink-and-rise reload, the breathing " +
                 "pulse, the radial fill on the icon itself and the slam-home flash are all retired " +
                 "with their tuning fields: one recharge readout for the fleet beats four per hull.")]
        [SerializeField] private Image tubeCooldownIcon;

        [Header("Icon Juice")]
        [Tooltip("Duration for icon scale punch on events")]
        [SerializeField] private float iconPunchDuration = 0.25f;
        [Tooltip("Scale multiplier for icon punch")]
        [SerializeField] private float iconPunchScale = 1.4f;
        [Tooltip("Duration for color tween back to original")]
        [SerializeField] private float colorTweenDuration = 0.35f;

        private Color _playerDomainColor = Color.white;
        private Color _currentBoostColor = Color.white;
        private Color _targetBoostColor = Color.white;
        private float _flashTimer;

        // Juice tweens
        private Tween _impactScaleTween;
        private Tween _impactColorTween;
        private Tween _boostScaleTween;

        private Vector3 _impactIconOriginalScale;

        public override void Initialize()
        {
            if (!boostFill) return;
            boostFill.fillAmount = 0f;
            boostFill.color = _playerDomainColor;
            boostFill.enabled = false;

            if (impactIcon)
            {
                impactIcon.color = impactRestColor;
                _impactIconOriginalScale = AbilityIconRestScale(Element.Charge);
            }

            if (tubeCooldownIcon)
            {
                // A plain, fully-drawn icon: the lockup's cooldown veil is what says "recharging",
                // so the icon must NOT also be a partial fill or it reads as a second meter.
                if (tubeCooldownIcon.type == Image.Type.Filled) tubeCooldownIcon.fillAmount = 1f;
            }
        }

        /// <summary>
        /// Re-anchors this view's impact-icon rest scale to the shared upgrade rest scale, so the
        /// impact punch settles back to the UPGRADED size instead of snapping the bump away. The
        /// base call does the sprite swap, the element badge and the one-shot unlock punch.
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);

            var rest = AbilityIconRestScale(element);
            switch (element)
            {
                case Element.Charge:
                    _impactIconOriginalScale = rest;
                    break;
                case Element.Mass:
                    // Nothing local to re-anchor: the Boost Ring's recharge is the lockup's
                    // standard cooldown, which never touches the icon's transform.
                    break;
                case Element.Space:
                case Element.Time:
                    // No local readout on either card - Space's steal reach is the skimmer sphere
                    // itself, and Time's skim energy draws on the non-elemental Skim card, whose
                    // gauge the lockup owns. Time's own card binds no icon at all.
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
            => SetAbilityCooldown(Element.Mass, 1f - Mathf.Clamp01(ready01));

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _impactScaleTween?.Kill();
            _impactColorTween?.Kill();
            _boostScaleTween?.Kill();
        }
    }
}

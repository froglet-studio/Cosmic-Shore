using CosmicShore.Data;
using DG.Tweening;
using TMPro;
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
    ///   [core]  drift         (LT, no gauge)                   → no element, no upgrade
    ///   Charge → crystal joust (the skull, impactIcon)          → "Shepherd"
    ///   Mass   → boost ring    (tubeCooldownIcon + veil)        → "Twin Rings"
    ///   Space  → steal         (GENERATED: reach ring + count)  → "Iron Grip"
    ///   Time   → skimming      (the skim icon + the boost fill) → "Live Wire"
    ///
    /// <para><b>The drift draws on a card of its own, and that is the point of it.</b> It is core
    /// flight - always available, upgraded by nothing, and on a trigger rather than on one of the
    /// four elemental slots - so it is bound as <see cref="CoreAbility.Drift"/> and the lockup
    /// gives it an ability plate with NO element flower above it, one pitch left of Charge. Its
    /// control chip is drawn from the binding's own <c>input</c>, because a core ability has no
    /// entry in the vessel's ability map for the chip to derive from.</para>
    ///
    /// <para><b>The SPACE card is GENERATED rather than authored</b>, via
    /// <see cref="EnsureGeneratedAbilityIcons"/>. Space is the steal, and a steal reaches exactly
    /// as far as the skimmer sphere does (Space scales it 15 → 30 on this hull), so the honest
    /// readout is the reach itself: a ring whose radius IS that live measurement, with the running
    /// total of prisms stolen inside it. It is generated for the reason the Dolphin's blast profile
    /// is - a sprite ladder quantizes a continuous measurement and silently stops matching it the
    /// first time anyone retunes the endpoints. The Rhino's skimmer-size icon is the precedent;
    /// this is that idea inside a lockup card.</para>
    ///
    /// <para>Two of these readouts are the LOCKUP's, not this view's: the boost fill (the TIME
    /// card's gauge, because skimming is what banks it) and the Boost Ring's recharge (the fleet's
    /// standard cooldown veil over the MASS card). This view keeps only what is genuinely the
    /// Squirrel's own: the impact flash, the crystal surge, and the Space readout it builds.</para>
    ///
    /// <para>RETIRED with the 2026-09 element re-cut: the overheat gauge (SetOverheatHeat /
    /// JuiceOverheat* had had no callers since the Sparrow's overheat mechanic was deleted - a
    /// gauge whose meter is gone is a lie, not a spare part). The drift's own icon came BACK in the
    /// same pass, onto the core card, where it is not competing with an element for a slot.</para>
    ///
    /// Its impact icon is a live gameplay gauge repainted per event. So the upgrade signal here is
    /// carried by the card rather than by the icon's colour (tintIconOnUpgrade is off on this
    /// prefab), and the local rest scale below is re-anchored on every upgrade flip so this view's
    /// own tweens can never wipe the bump.
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

        [Header("Steal reach + count (Space slot - GENERATED, see EnsureGeneratedAbilityIcons)")]
        [Tooltip("Ring radius in icon-local units at rest (skimmer at its authored minimum).")]
        [SerializeField] private float reachRingMinRadius = 20f;
        [Tooltip("Ring radius in icon-local units at full Space. Kept inside the icon's own 80-unit " +
                 "box so the lockup's kerning is the only thing that decides its drawn size.")]
        [SerializeField] private float reachRingMaxRadius = 34f;
        [SerializeField] private float reachRingThickness = 2.5f;
        [Tooltip("How fast the ring chases the live reach. A skimmer resize is a step, and a ring " +
                 "that steps with it reads as a glitch rather than as a measurement.")]
        [SerializeField] private float reachRingLerpSpeed = 8f;
        [SerializeField] private float stealCountFontSize = 26f;

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

        // The generated Space readout. Built once by EnsureGeneratedAbilityIcons.
        private Image _reachIcon;
        private ScopeRingGraphic _reachRing;
        private TMP_Text _stealCountText;
        private float _reachTarget01;
        private float _reachShown01;
        private int _stealCountShown = -1;

        /// <summary>
        /// Builds the SPACE card's readout - the steal's reach and its running total - because
        /// neither is a picture. The reach is a live measurement of the skimmer sphere Space
        /// scales, and the count is a number, so both are generated rather than authored.
        ///
        /// <para>The bound icon itself is deliberately INVISIBLE: it exists so the card is not
        /// LOCKED and so the lockup has something to normalise and kern, while the ring and the
        /// number - its children, and therefore inside that kerning - are what the pilot reads.
        /// The Dolphin's fully-transparent Space icon is the same shape.</para>
        ///
        /// <para>Idempotent by name, and the host is created OUTSIDE the row (the lockup re-homes
        /// it), so a rebuild finds the objects it made last time.</para>
        /// </summary>
        public override void EnsureGeneratedAbilityIcons()
        {
            if (_reachIcon) return;

            var host = transform.Find("StealReachButton") as RectTransform;
            if (!host)
            {
                host = new GameObject("StealReachButton", typeof(RectTransform))
                    .GetComponent<RectTransform>();
                host.SetParent(transform, false);
            }

            _reachIcon = ResolveGeneratedChild<Image>(host, "StealReachIcon");
            _reachIcon.rectTransform.anchorMin = _reachIcon.rectTransform.anchorMax =
                _reachIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _reachIcon.rectTransform.sizeDelta = new Vector2(80f, 80f);
            // The card's ANCHOR, never drawn: alpha 0 and the Graphic switched off, which are two
            // different guards - the second costs no draw call (an Image with no sprite draws a
            // solid quad), and the first means a future pass that re-enables it still shows
            // nothing. Disabling the component does not touch the ring and the count below it;
            // only disabling the GameObject would.
            _reachIcon.color = new Color(1f, 1f, 1f, 0f);
            _reachIcon.enabled = false;
            _reachIcon.raycastTarget = false;

            _reachRing = ResolveGeneratedChild<ScopeRingGraphic>(_reachIcon.rectTransform, "ReachRing");
            StretchGenerated(_reachRing.rectTransform);
            _reachRing.Thickness = reachRingThickness;
            _reachRing.Radius = reachRingMinRadius;
            _reachRing.raycastTarget = false;

            _stealCountText = ResolveGeneratedChild<TextMeshProUGUI>(_reachIcon.rectTransform, "StealCount");
            StretchGenerated(_stealCountText.rectTransform);
            _stealCountText.alignment = TextAlignmentOptions.Center;
            _stealCountText.fontSize = stealCountFontSize;
            _stealCountText.raycastTarget = false;
            _stealCountText.text = "0";

            BindGeneratedAbilityIcon(Element.Space, _reachIcon);
        }

        static T ResolveGeneratedChild<T>(RectTransform parent, string name) where T : Component
        {
            var existing = parent.Find(name);
            var found = existing ? existing.GetComponent<T>() : null;
            if (found) return found;

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        static void StretchGenerated(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// The steal's live reach, 0 at the skimmer's authored resting size and 1 at full Space.
        /// Polled by the controller; eased here rather than snapped, because an element level moves
        /// in steps and a ring that stepped with it would read as a glitch.
        /// </summary>
        public void SetStealReach01(float reach01) => _reachTarget01 = Mathf.Clamp01(reach01);

        /// <summary>The running total of prisms this pilot has taken. Repainted only on a change.</summary>
        public void SetStealCount(int count)
        {
            if (!_stealCountText || count == _stealCountShown) return;
            _stealCountShown = count;
            _stealCountText.text = count.ToString();
        }

        /// <summary>
        /// Tints the steal count in the pilot's own domain colour, which is not decoration: a
        /// stolen prism CHANGES HANDS to that domain, so the number is counting mass that now wears
        /// this colour. The ring stays white - it measures the skimmer, which belongs to nobody.
        /// </summary>
        void PaintStealCount(Color domainColor)
        {
            if (_stealCountText) _stealCountText.color = domainColor;
        }

        public override void Initialize()
        {
            EnsureGeneratedAbilityIcons();
            _reachShown01 = 0f;
            if (_reachRing) _reachRing.Radius = reachRingMinRadius;
            _stealCountShown = -1;
            SetStealCount(0);
            PaintStealCount(_playerDomainColor);

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
                    // The reach ring is a MEASUREMENT of the skimmer, so Iron Grip re-anchors
                    // nothing here: the ring already moves when Space does, and the icon it hangs
                    // off is invisible. The card's own plate carries the upgrade.
                    break;
                case Element.Time:
                    // Time's card carries the skim icon and the boost fill; the gauge is the
                    // lockup's and nothing local tweens that icon's transform.
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

            PaintStealCount(color);
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
            // The reach ring is eased here rather than in the controller because the controller
            // pushes a level, not a frame - and this runs whether or not the boost fill is live.
            if (_reachRing)
            {
                _reachShown01 = Mathf.Lerp(_reachShown01, _reachTarget01,
                                           1f - Mathf.Exp(-reachRingLerpSpeed * Time.deltaTime));
                _reachRing.Radius = Mathf.Lerp(reachRingMinRadius, reachRingMaxRadius, _reachShown01);
            }

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

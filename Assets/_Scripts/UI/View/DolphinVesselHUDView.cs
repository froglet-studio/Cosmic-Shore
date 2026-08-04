using System.Collections.Generic;
using CosmicShore.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Dolphin's four lower-right ability icons, in the fleet order charge → mass → space →
    /// time (the same order as the element flowers above them), each bound to the element that
    /// upgrades it:
    ///
    ///   Charge → crystal seeding (crystalIcon + the carry pips)   → "Twin Seed"
    ///   Mass   → drift trail     (the authored boost ring)         → "Hard Wake"
    ///   Space  → cone blast      (blastIcon)                      → "Clean Blast"
    ///   Time   → skim energy     (the jaw pair)                   → "Live Current"
    ///
    /// Every one of those icons is a live gameplay gauge — a recharge fill, a boost meter, a blast
    /// tally, a jaw gape — repainted per frame. So the upgrade signal is carried by the element
    /// badge and a persistent scale bump rather than by icon colour (turn tintIconOnUpgrade OFF on
    /// this prefab), and the local rest scales below are re-anchored on every upgrade flip so this
    /// view's own tweens can never wipe the bump.
    ///
    /// The MASS slot is the Dolphin's own pre-existing boost gauge — an authored 11-step ring
    /// with the boost glyph nested inside it — folded into the row rather than replaced, so the
    /// elemental badge and upgrade bump land on the gauge the pilot already watches.
    ///
    /// The TIME icon is the answer to "how much energy have I banked": a pair of jaws (the vessel's
    /// own isolated silhouettes) that open exactly as wide as the hull's do, because both show the
    /// same thing — the half-angle of the cone the next crystal impact will release. Every
    /// reference is optional; an unwired slot is simply not drawn (opt-in rollout).
    /// </summary>
    public class DolphinVesselHUDView : VesselHUDView
    {
        // ---- Charge: crystal seeding -------------------------------------------------
        [Header("Charge — crystal seeding")]
        [Tooltip("The ability icon. If its Image type is Filled it doubles as the recharge wipe.")]
        [SerializeField] private Image crystalIcon;
        [Tooltip("One pip per SAVED crystal - a crystal carried beyond the first, which the main " +
                 "icon already stands for. So an un-upgraded Dolphin shows none, and the mini " +
                 "crystal appearing IS Twin Seed becoming visible. Pips past the carry limit are " +
                 "hidden outright, so the slot reads capacity as well as stock.")]
        [SerializeField] private List<Image> crystalPips = new();
        [Tooltip("Pip sprite for a crystal that is LOADED - the omni crystal's active art.")]
        [SerializeField] private Sprite crystalPipFilled;
        [Tooltip("Pip sprite for a slot still recharging - the omni crystal's inactive art. " +
                 "Leave both empty to fall back to tinting one sprite with the colours below.")]
        [SerializeField] private Sprite crystalPipEmpty;
        [Tooltip("Pip colour for a crystal that is loaded and ready to plant.")]
        [SerializeField] private Color crystalReadyColor = Color.white;
        [Tooltip("Pip colour for a slot that is still recharging - reads as 'not available'.")]
        [SerializeField] private Color crystalChargingColor = new(0.55f, 0.55f, 0.6f, 0.8f);
        [Tooltip("Flash colour when a crystal finishes recharging and the slot arms.")]
        [SerializeField] private Color crystalArmedFlashColor = Color.white;

        // ---- Mass: drift trail / boost charge ----------------------------------------
        [Header("Mass — boost charged while drifting")]
        [Tooltip("The boost gauge itself — the Dolphin's authored 11-step ring (Boost Display). " +
                 "Bound as the Mass ability icon, so the elemental badge and upgrade bump land on " +
                 "the gauge the pilot is already watching. Filled-type images additionally show a " +
                 "radial wipe; the ring instead steps through chargeSteps below.")]
        [SerializeField] private Image driftBoostIcon;
        [SerializeField] private Color driftRestColor = new(0.877f, 0.877f, 0.877f, 1f);
        [Tooltip("Colour the boost gauge ramps toward as it fills.")]
        [SerializeField] private Color driftChargedColor = new(1f, 0.82f, 0.45f, 1f);
        [Tooltip("The 11 authored ring steps, ordered EMPTY → FULL. Driven by the boost meter.")]
        [SerializeField] private List<Sprite> chargeSteps = new();

        // ---- Space: cone blast --------------------------------------------------------
        [Header("Space — cone blast")]
        [SerializeField] private Image blastIcon;
        [Tooltip("Optional tally of what the last cone destroyed.")]
        [SerializeField] private TMP_Text blastCountText;
        [SerializeField] private Color blastFlashColor = new(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color blastRestColor = Color.white;
        [Tooltip("Seconds the blast tally stays up after a cone fires.")]
        [SerializeField, Min(0.1f)] private float blastCountHoldSeconds = 2.5f;

        // ---- Time: skim energy, drawn as jaws -----------------------------------------
        [Header("Time — skim energy (jaws)")]
        [Tooltip("Upper jaw half. Rotates open as energy banks, mirroring the hull's own jaws.")]
        [SerializeField] private RectTransform jawUpper;
        [Tooltip("Lower jaw half. Rotates the opposite way by the same angle.")]
        [SerializeField] private RectTransform jawLower;
        [Tooltip("Gape in degrees PER JAW at full energy. Keep equal to RiptideAnimation's " +
                 "MaxJawAngle so the cockpit and the hull agree about the width of the next blast.")]
        [SerializeField] private float maxJawAngle = 21f;
        [Tooltip("Seconds the jaws take to glide to a new gape. Energy steps arrive per skim, so " +
                 "this is what keeps the readout from stuttering.")]
        [SerializeField, Min(0.01f)] private float jawGlideDuration = 0.12f;
        [Header("Icon juice")]
        [SerializeField] private float iconPunchScale = 1.35f;
        [SerializeField] private float iconPunchDuration = 0.25f;
        [SerializeField] private float colorTweenDuration = 0.3f;

        int _stepsMinusOne;

        Vector3 _crystalIconRestScale = Vector3.one;
        Vector3 _driftIconRestScale = Vector3.one;
        Vector3 _blastIconRestScale = Vector3.one;
        Vector3 _jawRestScale = Vector3.one;

        Tween _crystalScaleTween, _crystalColorTween;
        Tween _driftScaleTween;
        Tween _blastScaleTween, _blastColorTween;
        Tween _jawUpperTween, _jawLowerTween;

        float _blastCountTimer;
        int _lastChargesShown = -1;
        float _currentJawAngle;

        public override void Initialize()
        {
            _stepsMinusOne = Mathf.Max(0, (chargeSteps?.Count ?? 0) - 1);

            if (crystalIcon)
            {
                _crystalIconRestScale = AbilityIconRestScale(Element.Charge);
                if (crystalIcon.type == Image.Type.Filled) crystalIcon.fillAmount = 1f;
            }

            if (driftBoostIcon)
            {
                _driftIconRestScale = AbilityIconRestScale(Element.Mass);
                driftBoostIcon.color = driftRestColor;
                if (driftBoostIcon.type == Image.Type.Filled) driftBoostIcon.fillAmount = 0f;
            }

            if (blastIcon)
            {
                _blastIconRestScale = AbilityIconRestScale(Element.Space);
                blastIcon.color = blastRestColor;
            }

            if (jawUpper) _jawRestScale = AbilityIconRestScale(Element.Time);
            SetJawAngleImmediate(0f);

            if (blastCountText) blastCountText.text = string.Empty;

            _lastChargesShown = -1;
        }

        /// <summary>
        /// Re-anchors this view's per-icon rest scales to the shared upgrade rest scale, so the
        /// crystal arm-punch, the drift pulse, the blast flash and the jaw glide all settle back to
        /// the UPGRADED size instead of snapping the bump away. The base call does the sprite swap,
        /// the element badge and the one-shot unlock punch.
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);

            var rest = AbilityIconRestScale(element);
            switch (element)
            {
                case Element.Charge:
                    _crystalIconRestScale = rest;
                    _lastChargesShown = -1; // the carry limit just moved - repaint the pip row
                    break;
                case Element.Mass:  _driftIconRestScale = rest; break;
                case Element.Space: _blastIconRestScale = rest; break;
                case Element.Time:
                    _jawRestScale = rest;
                    ApplyJawRestScale(); // the jaws are driven by rotation, so nothing else re-scales them
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Charge: crystal seeding. charges = crystals in hand, maxCharges =
        // the carry limit (2 once Charge's level-5 upgrade is active), and
        // ready01 fills 0 -> 1 as the next slot recharges.
        // ---------------------------------------------------------------
        public void SetCrystalState(int charges, int maxCharges, float ready01)
        {
            ready01 = Mathf.Clamp01(ready01);

            if (crystalIcon && crystalIcon.type == Image.Type.Filled)
                crystalIcon.fillAmount = charges > 0 ? 1f : ready01;

            for (int i = 0; i < crystalPips.Count; i++)
            {
                var pip = crystalPips[i];
                if (!pip) continue;

                // A pip stands for a SAVED crystal - one carried beyond the first, which the main
                // icon already represents. So pip[i] is charge i+2, and an un-upgraded Dolphin
                // (limit 1) shows no pips at all: the mini crystal IS the Twin Seed upgrade made
                // visible, not a second copy of the ability icon.
                int chargeShown = i + 2;
                bool withinLimit = chargeShown <= maxCharges;
                if (pip.gameObject.activeSelf != withinLimit) pip.gameObject.SetActive(withinLimit);
                if (!withinLimit) continue;

                bool loaded = chargeShown <= charges;

                // The omni crystal ships its own loaded/empty art, so swap the sprite when both are
                // authored and only fall back to tinting a single sprite when they are not.
                if (crystalPipFilled && crystalPipEmpty)
                {
                    pip.sprite = loaded ? crystalPipFilled : crystalPipEmpty;
                    pip.color = Color.white;
                }
                else
                {
                    pip.color = loaded ? crystalReadyColor : crystalChargingColor;
                }

                // The slot currently recharging shows its progress when it can.
                if (!loaded && chargeShown == charges + 1 && pip.type == Image.Type.Filled)
                    pip.fillAmount = ready01;
                else if (pip.type == Image.Type.Filled)
                    pip.fillAmount = loaded ? 1f : 0f;
            }

            if (_lastChargesShown >= 0 && charges > _lastChargesShown)
                JuiceCrystalArmed();
            _lastChargesShown = charges;
        }

        /// <summary>A crystal finished recharging: punch and flash the slot armed.</summary>
        void JuiceCrystalArmed()
        {
            if (!crystalIcon) return;

            _crystalScaleTween?.Kill();
            crystalIcon.rectTransform.localScale = _crystalIconRestScale;
            _crystalScaleTween = crystalIcon.rectTransform
                .DOScale(_crystalIconRestScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .SetLink(crystalIcon.gameObject)
                .OnComplete(() =>
                {
                    _crystalScaleTween = crystalIcon.rectTransform
                        .DOScale(_crystalIconRestScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce)
                        .SetLink(crystalIcon.gameObject);
                });

            _crystalColorTween?.Kill();
            crystalIcon.color = crystalArmedFlashColor;
            _crystalColorTween = crystalIcon
                .DOColor(crystalReadyColor, colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(crystalIcon.gameObject);
        }

        // ---------------------------------------------------------------
        // Mass: how much boost the drift has banked, 0-1.
        // ---------------------------------------------------------------
        public void SetDriftBoost(float charge01, bool isDrifting)
        {
            if (!driftBoostIcon) return;

            charge01 = Mathf.Clamp01(charge01);
            if (driftBoostIcon.type == Image.Type.Filled)
                driftBoostIcon.fillAmount = charge01;

            // The authored ring encodes the level in its lit segments - step it.
            SetBoostStepNormalized(charge01);

            driftBoostIcon.color = Color.Lerp(driftRestColor, driftChargedColor, charge01);

            // Drifting reads as "the icon is working": a gentle swell with the charge. Kill the
            // release tween first — it settles toward rest, and the two would fight for the scale.
            if (!isDrifting) return;
            _driftScaleTween?.Kill();
            _driftScaleTween = null;
            float target = Mathf.Lerp(1f, 1.12f, charge01);
            driftBoostIcon.rectTransform.localScale = _driftIconRestScale * target;
        }

        public void ReleaseDriftBoost()
        {
            if (!driftBoostIcon) return;
            _driftScaleTween?.Kill();
            _driftScaleTween = driftBoostIcon.rectTransform
                .DOScale(_driftIconRestScale, iconPunchDuration)
                .SetEase(Ease.OutBack)
                .SetLink(driftBoostIcon.gameObject);
        }

        // ---------------------------------------------------------------
        // Space: the cone fired - flash the icon and show what it took.
        // ---------------------------------------------------------------
        public void ReportBlast(int destroyedCount)
        {
            if (blastCountText)
            {
                blastCountText.text = destroyedCount > 0 ? destroyedCount.ToString() : string.Empty;
                _blastCountTimer = blastCountHoldSeconds;
            }

            if (!blastIcon) return;

            _blastScaleTween?.Kill();
            blastIcon.rectTransform.localScale = _blastIconRestScale;
            _blastScaleTween = blastIcon.rectTransform
                .DOScale(_blastIconRestScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .SetLink(blastIcon.gameObject)
                .OnComplete(() =>
                {
                    _blastScaleTween = blastIcon.rectTransform
                        .DOScale(_blastIconRestScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce)
                        .SetLink(blastIcon.gameObject);
                });

            _blastColorTween?.Kill();
            blastIcon.color = blastFlashColor;
            _blastColorTween = blastIcon
                .DOColor(blastRestColor, colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(blastIcon.gameObject);
        }

        void Update()
        {
            if (_blastCountTimer <= 0f || !blastCountText) return;
            _blastCountTimer -= Time.deltaTime;
            if (_blastCountTimer <= 0f) blastCountText.text = string.Empty;
        }

        // ---------------------------------------------------------------
        // Time: banked skim energy, drawn as a jaw gape. Same angle the hull
        // opens to, and the same half-angle the released cone will carry.
        // ---------------------------------------------------------------

        /// <summary>0-1 normalized energy → jaw gape. Glides rather than snapping, because energy
        /// arrives in per-skim steps.</summary>
        public void SetEnergyNormalized(float norm01)
        {
            norm01 = Mathf.Clamp01(norm01);
            SetJawAngle(maxJawAngle * norm01);
        }

        void SetJawAngle(float angle)
        {
            if (!jawUpper && !jawLower) return;
            if (Mathf.Approximately(angle, _currentJawAngle)) return;

            float from = _currentJawAngle;
            _currentJawAngle = angle;

            _jawUpperTween?.Kill();
            _jawLowerTween?.Kill();

            if (jawUpper)
                _jawUpperTween = DOVirtual.Float(from, angle, jawGlideDuration,
                        v => { if (jawUpper) jawUpper.localRotation = Quaternion.Euler(0f, 0f, UpperSign * v); })
                    .SetEase(Ease.OutQuad).SetLink(jawUpper.gameObject);

            if (jawLower)
                _jawLowerTween = DOVirtual.Float(from, angle, jawGlideDuration,
                        v => { if (jawLower) jawLower.localRotation = Quaternion.Euler(0f, 0f, -UpperSign * v); })
                    .SetEase(Ease.OutQuad).SetLink(jawLower.gameObject);
        }

        /// <summary>
        /// Which way the upper jaw swings. The rects hinge on their LEFT edge (pivot 0, 0.5) - the
        /// same pivot the vessel silhouette uses - so the jaw body sits at local +X and the gape
        /// opens to the RIGHT, reading as '&lt;'. Rotating a point at +X by θ moves it to
        /// y = |x|·sinθ, so lifting the upper half needs a POSITIVE angle. Flip this if the hinge
        /// ever moves back to the other edge.
        /// </summary>
        const float UpperSign = 1f;

        void SetJawAngleImmediate(float angle)
        {
            _currentJawAngle = angle;
            if (jawUpper) jawUpper.localRotation = Quaternion.Euler(0f, 0f, UpperSign * angle);
            if (jawLower) jawLower.localRotation = Quaternion.Euler(0f, 0f, -UpperSign * angle);
            ApplyJawRestScale();
        }

        /// <summary>
        /// Parks both jaw halves at the Time slot's current rest scale, so the level-5 upgrade bump
        /// survives every gape change. Without this the jaws are the one icon in the row whose
        /// upgrade scale is never applied.
        /// </summary>
        void ApplyJawRestScale()
        {
            if (jawUpper) jawUpper.localScale = _jawRestScale;
            if (jawLower) jawLower.localScale = _jawRestScale;
        }

        /// <summary>
        /// Adopts the HULL's authored gape as this icon's maximum, so the cockpit jaws and the
        /// ship's own jaws open by the same angle — they are showing the same quantity (the
        /// half-angle of the next blast) and must not drift apart through two authored numbers.
        /// </summary>
        public void SetMaxJawAngle(float degrees)
        {
            if (degrees <= 0f) return;
            maxJawAngle = degrees;
        }

        // ---------------------------------------------------------------
        // The boost ring's authored steps. This is the Dolphin's own gauge -
        // eleven sprites lighting 0..10 segments - so the level is read off
        // the art rather than off a fill amount.
        // ---------------------------------------------------------------

        /// <summary>0–1 normalized boost → the matching ring step.</summary>
        public void SetBoostStepNormalized(float norm01)
        {
            if (!driftBoostIcon || chargeSteps == null || chargeSteps.Count == 0)
                return;

            norm01 = Mathf.Clamp01(norm01);

            int idx = (_stepsMinusOne <= 0)
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(norm01 * _stepsMinusOne), 0, _stepsMinusOne);

            SetBoostStepIndex(idx);
        }

        public void SetBoostStepIndex(int idx)
        {
            if (!driftBoostIcon || chargeSteps == null || chargeSteps.Count == 0)
                return;
            if (idx < 0 || idx >= chargeSteps.Count) return;

            var sprite = chargeSteps[idx];
            if (!sprite || driftBoostIcon.sprite == sprite) return;

            driftBoostIcon.sprite = sprite;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _crystalScaleTween?.Kill();
            _crystalColorTween?.Kill();
            _driftScaleTween?.Kill();
            _blastScaleTween?.Kill();
            _blastColorTween?.Kill();
            _jawUpperTween?.Kill();
            _jawLowerTween?.Kill();
        }
    }
}

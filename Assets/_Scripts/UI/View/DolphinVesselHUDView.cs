using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
    ///   Charge → crystal seeding (crystalIcon + the yield pips)   → "Twin Seed"
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
        [Tooltip("One pip per EXTRA crystal in a seeding cycle - a crystal beyond the first, which " +
                 "the main icon already stands for. So an un-upgraded Dolphin shows none, and the " +
                 "mini crystal appearing IS Twin Seed becoming visible. Pips past the current yield " +
                 "are hidden outright, so the slot reads the cycle's whole output.")]
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
                 "Bound as the Mass ability icon, so the upgrade bump lands on the gauge the pilot " +
                 "is already watching. The ring encodes its own level by stepping through " +
                 "chargeSteps below — this view writes NOTHING else on it (see SetDriftBoost).")]
        [SerializeField] private Image driftBoostIcon;
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
        [Tooltip("Gape in degrees PER JAW at EMPTY energy. NOT zero: the blast is a short capsule " +
                 "at rest, so the jaws start slightly open. Keep equal to RiptideAnimation's " +
                 "MinJawAngle — the controller overwrites this from the hull at Initialize.")]
        [SerializeField] private float minJawAngle = 4.7636f;
        [Tooltip("Gape in degrees PER JAW at full energy. Keep equal to RiptideAnimation's " +
                 "MaxJawAngle so the cockpit and the hull agree about the width of the next blast.")]
        [SerializeField] private float maxJawAngle = 23.4287f;
        [Tooltip("Seconds the jaws take to glide to a new gape. Energy steps arrive per skim, so " +
                 "this is what keeps the readout from stuttering.")]
        [SerializeField, Min(0.01f)] private float jawGlideDuration = 0.12f;
        [Tooltip("Scale punch on the jaw pair each time a skim banks energy — the per-skim beat on " +
                 "top of the gape, which only moves ~1/150th of its range per skim.")]
        [SerializeField, Min(1f)] private float skimPunchScale = 1.3f;

        [Header("Time — the full-energy CTA")]
        [Tooltip("Colour the jaws rest at. Matches the authored art (white); the CTA lime is " +
                 "blended over it as the bank approaches full.")]
        [SerializeField] private Color jawRestColor = Color.white;
        [Tooltip("Normalized energy at which the jaws BEGIN turning lime; at 1.0 they are solid " +
                 "lime. Deliberately not a hard switch at full — a bank takes ~150 skims, so a " +
                 "binary flip would pop on one skim and drop off the instant you ram a prism.")]
        [SerializeField, Range(0f, 0.99f)] private float jawArmingThreshold = 0.85f;
        [Tooltip("Shared colour spec. The armed colour is its limeColor — the SAME lime a maxed " +
                 "element flower shows, so 'this is full' reads identically across the HUD. " +
                 "Loaded from Resources when left empty; never author a second copy of the colour.")]
        [SerializeField] private ElementalBarsConfigSO barsConfig;
        [SerializeField] private string barsConfigResourcePath = "ElementalBarsConfig";
        [Header("Icon juice")]
        [SerializeField] private float iconPunchScale = 1.35f;
        [SerializeField] private float iconPunchDuration = 0.25f;
        [SerializeField] private float colorTweenDuration = 0.3f;

        int _stepsMinusOne;

        Vector3 _crystalIconRestScale = Vector3.one;
        Vector3 _blastIconRestScale = Vector3.one;
        Vector3 _jawRestScale = Vector3.one;

        Tween _crystalScaleTween, _crystalColorTween;
        Tween _blastScaleTween, _blastColorTween;
        Tween _jawUpperTween, _jawLowerTween, _jawPunchTween, _jawColorTween;

        // The jaw halves' own Graphics. The row's Time icon (JawIcon) is a fully transparent
        // container, so these two ARE the visible Time gauge and the only thing worth tinting.
        Graphic _jawUpperGraphic, _jawLowerGraphic;
        Color _jawArmedColor = Color.white;
        float _jawArm01 = -1f;
        bool _warnedArmingUnavailable;

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

            // The ring's colour is authored art - never repainted from here.
            if (driftBoostIcon && driftBoostIcon.type == Image.Type.Filled)
                driftBoostIcon.fillAmount = 0f;

            if (blastIcon)
            {
                _blastIconRestScale = AbilityIconRestScale(Element.Space);
                blastIcon.color = blastRestColor;
            }

            if (jawUpper) _jawRestScale = AbilityIconRestScale(Element.Time);
            // Empty energy is the MIN gape, not a shut jaw - the blast is a short capsule at rest.
            SetJawAngleImmediate(minJawAngle);

            ResolveJawGraphics();
            _jawArm01 = -1f;             // a re-init must repaint, not early-out on a stale value
            ApplyJawArming(0f, immediate: true);

            if (blastCountText) blastCountText.text = string.Empty;

            _lastChargesShown = -1;
        }

        /// <summary>
        /// Re-anchors this view's per-icon rest scales to the shared upgrade rest scale, so the
        /// crystal arm-punch, the blast flash and the jaw glide all settle back to the UPGRADED
        /// size instead of snapping the bump away. The base call does the sprite swap, the element
        /// badge and the one-shot unlock punch.
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);

            var rest = AbilityIconRestScale(element);
            switch (element)
            {
                case Element.Charge:
                    _crystalIconRestScale = rest;
                    _lastChargesShown = -1; // the seed yield just moved - repaint the pip row
                    break;
                // Mass needs no re-anchor: nothing in this view writes the boost ring's scale, so
                // the base class's bump is never contested.
                case Element.Space: _blastIconRestScale = rest; break;
                case Element.Time:
                    _jawRestScale = rest;
                    ApplyJawRestScale(); // the jaws are driven by rotation, so nothing else re-scales them
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Charge: crystal seeding — a PASSIVE ability. Nothing is ever carried,
        // so the icon is a pure recharge fill (0 -> 1 as the next seeding arms)
        // and the pips show the YIELD: how many crystals the next cycle plants.
        //
        // seedsPerCycle is 1 normally and 2 once Twin Seed (Charge L5) lands.
        // ---------------------------------------------------------------
        public void SetCrystalSeedState(int seedsPerCycle, float ready01)
        {
            ready01 = Mathf.Clamp01(ready01);

            // The ability plants itself, so "how full is the clock" is the only thing the main
            // icon can honestly say. It is never pinned to 1 by a held charge any more.
            if (crystalIcon && crystalIcon.type == Image.Type.Filled)
                crystalIcon.fillAmount = ready01;

            for (int i = 0; i < crystalPips.Count; i++)
            {
                var pip = crystalPips[i];
                if (!pip) continue;

                // A pip stands for an EXTRA crystal in the cycle - one beyond the first, which the
                // main icon already represents. So pip[i] is seed i+2, and an un-upgraded Dolphin
                // (yield 1) shows no pips at all: the mini crystal appearing IS Twin Seed becoming
                // visible, not a second copy of the ability icon.
                int seedShown = i + 2;
                bool withinYield = seedShown <= seedsPerCycle;
                if (pip.gameObject.activeSelf != withinYield) pip.gameObject.SetActive(withinYield);
                if (!withinYield) continue;

                // Every shown pip is part of the coming cycle, so it rides the same recharge fill
                // as the main icon rather than a per-slot one - there are no per-slot clocks now.
                if (crystalPipFilled && crystalPipEmpty)
                {
                    pip.sprite = crystalPipFilled;
                    pip.color = Color.white;
                }
                else
                {
                    pip.color = crystalReadyColor;
                }

                if (pip.type == Image.Type.Filled) pip.fillAmount = ready01;
            }

            _lastChargesShown = seedsPerCycle;
        }

        /// <summary>
        /// A cycle just fired and crystals were planted out in the cytoplasm. The pilot gave no
        /// input and may be looking anywhere, so the slot punches to say it happened — this is the
        /// only notification the passive ability has.
        /// </summary>
        public void PulseCrystalSeeded() => JuiceCrystalArmed();

        /// <summary>A seeding fired: punch and flash the slot.</summary>
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
        //
        // The ring is an ELEVEN-SPRITE authored gauge, so stepping the sprite is the whole
        // readout. This deliberately writes nothing else: a swell keyed to charge01 and a colour
        // ramp both landed on EVERY resource tick - the 1 Hz passive regen, each charge tick, each
        // discharge tick - so the icon jittered between discrete scales and killed its own tween
        // doing it. Leaving the transform alone also means the level-5 scale bump the base class
        // applies survives, which matters more here than juice: this HUD has upgrade tint and the
        // element badge both turned off, so the bump is the only upgrade signal the slot has left.
        // ---------------------------------------------------------------
        public void SetDriftBoost(float charge01)
        {
            if (!driftBoostIcon) return;

            charge01 = Mathf.Clamp01(charge01);
            if (driftBoostIcon.type == Image.Type.Filled)
                driftBoostIcon.fillAmount = charge01;

            // The authored ring encodes the level in its lit segments - step it.
            SetBoostStepNormalized(charge01);
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
            // Same exact curve the hull uses (RiptideAnimation.GapeAngleAt): the blast's tip extent
            // is linear in energy but the ANGLE is its arctangent, so lerping the angles would put
            // the cockpit and the hull a few degrees apart mid-charge.
            SetJawAngle(RiptideAnimation.GapeAngleAt(norm01, minJawAngle, maxJawAngle));
            ApplyJawArming(norm01, immediate: false);
        }

        /// <summary>
        /// Caches the jaw halves' Graphics and the armed colour. The lime is read from the shared
        /// <see cref="ElementalBarsConfigSO"/> rather than authored here, so the "this is maxed"
        /// green is literally the same value a full element flower shows — never a second copy that
        /// can drift. Both failure modes degrade to "the jaws never arm", which is invisible, so
        /// each one says so once and names the fix.
        /// </summary>
        void ResolveJawGraphics()
        {
            _jawUpperGraphic = jawUpper ? jawUpper.GetComponent<Graphic>() : null;
            _jawLowerGraphic = jawLower ? jawLower.GetComponent<Graphic>() : null;

            if (!barsConfig) barsConfig = Resources.Load<ElementalBarsConfigSO>(barsConfigResourcePath);
            _jawArmedColor = barsConfig ? barsConfig.limeColor : jawRestColor;

            if (_warnedArmingUnavailable) return;

            if (!barsConfig)
            {
                _warnedArmingUnavailable = true;
                CSDebug.LogWarning($"[DolphinVesselHUDView] '{name}' found no ElementalBarsConfigSO at " +
                                   $"Resources/{barsConfigResourcePath}, so the jaw gauge will never turn " +
                                   "lime at full energy. Assign barsConfig on the HUD prefab, or restore " +
                                   "the asset - the armed colour is deliberately not authored here.");
            }
            else if ((jawUpper || jawLower) && !_jawUpperGraphic && !_jawLowerGraphic)
            {
                _warnedArmingUnavailable = true;
                CSDebug.LogWarning($"[DolphinVesselHUDView] '{name}' has jaw RectTransforms wired but " +
                                   "neither carries a Graphic, so the full-energy lime cannot be drawn. " +
                                   "Point jawUpper/jawLower at the Image objects (JawUpper/JawLower).");
            }
        }

        /// <summary>
        /// Blends the jaws from their rest colour to the CTA lime over the last
        /// (1 - <see cref="jawArmingThreshold"/>) of the bank, so a full meter reads as "fire this"
        /// at a glance. This is the ONE colour writer on the jaw pair: the row's upgrade tint is off
        /// on this HUD (every icon is a live gauge) and it targets JawIcon, not these halves — so
        /// the gauge colour and the upgrade signal can never contest each other.
        /// </summary>
        void ApplyJawArming(float norm01, bool immediate)
        {
            if (!_jawUpperGraphic && !_jawLowerGraphic) return;

            float span = Mathf.Max(1e-4f, 1f - jawArmingThreshold);
            float arm = Mathf.Clamp01((Mathf.Clamp01(norm01) - jawArmingThreshold) / span);
            if (!immediate && Mathf.Approximately(arm, _jawArm01)) return;
            _jawArm01 = arm;

            var target = Color.Lerp(jawRestColor, _jawArmedColor, arm);
            _jawColorTween?.Kill();

            if (immediate) { WriteJawColor(target); return; }

            var from = _jawUpperGraphic ? _jawUpperGraphic.color : _jawLowerGraphic.color;
            _jawColorTween = DOVirtual.Color(from, target, colorTweenDuration, WriteJawColor)
                .SetEase(Ease.OutQuad)
                .SetLink(jawUpper ? jawUpper.gameObject : jawLower.gameObject);
        }

        void WriteJawColor(Color c)
        {
            if (_jawUpperGraphic) _jawUpperGraphic.color = c;
            if (_jawLowerGraphic) _jawLowerGraphic.color = c;
        }

        /// <summary>
        /// A skim just banked energy: punch the jaw pair. One skim only widens the gape by
        /// maxJawAngle/10 (about 2 degrees on a 78x14 rect), which is invisible - so the DISCRETE
        /// event gets its own beat on top of the continuous readout. This is the only skim signal
        /// the pilot can perceive on a desktop, where the haptic pulse is a no-op and the beam VFX
        /// depends on the skimmed prism authoring one.
        /// </summary>
        public void ReportSkim()
        {
            if (!jawUpper && !jawLower) return;

            _jawPunchTween?.Kill();
            ApplyJawRestScale();

            var punch = _jawRestScale * skimPunchScale;
            _jawPunchTween = DOVirtual.Float(0f, 1f, iconPunchDuration, v =>
                {
                    // Out and back within the one tween, so a rapid skim train re-punches from
                    // rest instead of stacking scales.
                    var s = Vector3.LerpUnclamped(_jawRestScale, punch, v < 0.35f ? v / 0.35f : (1f - v) / 0.65f);
                    if (jawUpper) jawUpper.localScale = s;
                    if (jawLower) jawLower.localScale = s;
                })
                .SetEase(Ease.OutQuad)
                .OnComplete(ApplyJawRestScale)
                .SetLink(jawUpper ? jawUpper.gameObject : jawLower.gameObject);
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
        /// Adopts the HULL's authored gape RANGE, so the cockpit jaws and the ship's own jaws open
        /// by the same angle at every charge — they are showing the same quantity (the gape
        /// half-angle of the next blast) and must not drift apart through separately-authored
        /// numbers. The minimum is not zero: the blast is a short capsule at rest.
        /// </summary>
        public void SetJawAngleRange(float minDegrees, float maxDegrees)
        {
            if (maxDegrees <= 0f) return;
            maxJawAngle = maxDegrees;
            minJawAngle = Mathf.Clamp(minDegrees, 0f, maxDegrees);
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
            _blastScaleTween?.Kill();
            _blastColorTween?.Kill();
            _jawUpperTween?.Kill();
            _jawLowerTween?.Kill();
            _jawPunchTween?.Kill();
            _jawColorTween?.Kill();
        }
    }
}

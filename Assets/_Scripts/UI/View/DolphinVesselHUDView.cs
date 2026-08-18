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
    ///   Charge → Echo Sight       (profile + living tally) → "Pilot Echo"
    ///   Mass   → crystal seeding  (the recharge fill)      → "Claimed Seed"
    ///   Space  → cone blast       (jaws + tally)           → "Clean Blast"
    ///   Time   → charge fill rate (the boost ring)         → "Live Current"
    ///
    /// <para><b>Every slot draws one dimension of the same weapon.</b> The Dolphin has essentially
    /// one offensive act — bank energy by skimming, fly into a crystal, release a cone — and the
    /// four elements each own one axis of it. So the row is not four unrelated gauges: Charge draws
    /// the blast's cross-section, Space draws its gape and what it took, Mass draws when
    /// the next crystal to trigger it arrives, and Time draws the boost that gets you there. Read
    /// left to right it is the whole weapon.</para>
    ///
    /// <para>Every one of those icons is a live gameplay gauge — a generated profile mesh, a
    /// recharge fill, a jaw gape, a boost ring — repainted per frame. So the upgrade signal is
    /// carried by the element badge and a persistent scale bump rather than by icon colour (turn
    /// tintIconOnUpgrade OFF on this prefab), and the local rest scales below are re-anchored on
    /// every upgrade flip so this view's own tweens can never wipe the bump.</para>
    ///
    /// <para>The SPACE slot shows exactly two things: the jaws open to the gape half-angle the next
    /// blast will carry (the same angle the hull's own jaws open to), and the tally beneath them
    /// reports what the last cone actually claimed — ANGLE and AMOUNT. A third readout for Space's
    /// REACH was tried here as a thin bar under the jaws and dropped: reach only moves when the
    /// element moves, so it was a near-static line competing with two live gauges, and the icon
    /// says more by saying less.</para>
    ///
    /// Every reference is optional; an unwired slot is simply not drawn (opt-in rollout).
    /// </summary>
    public class DolphinVesselHUDView : VesselHUDView
    {
        // ---- Charge: Echo Sight, drawn as the blast profile ---------------------------
        [Header("Charge — Echo Sight (blast profile)")]
        [Tooltip("The generated cross-section of the next blast. Lives as a CHILD of the row's " +
                 "ChargeIcon (a transparent container), the same arrangement the jaw pair uses, so " +
                 "the row's upgrade badge and this gauge can never contest the same object.")]
        [SerializeField] private BlastProfileGraphic blastProfile;
        [Tooltip("Fallback colour the profile rests at while the sight is released. Normally " +
                 "overridden by ElementalBarsConfigSO.greyColor - see ResolveProfileColors.")]
        [SerializeField] private Color profileRestColor = new(0.51f, 0.51f, 0.54f, 1f);
        [Tooltip("Fallback colour the profile reaches while the pilot is HOLDING the sight. " +
                 "Normally overridden by ElementalBarsConfigSO.whiteColor.")]
        [SerializeField] private Color profileEngagedColor = new(0.96f, 0.96f, 1f, 1f);
        [Tooltip("Seconds the profile takes to reach its engaged colour. Nothing snaps.")]
        [SerializeField, Min(0.01f)] private float profileEngageDuration = 0.15f;

        [Header("Charge — the living tally")]
        [Tooltip("PILOTS the last blast debuffed. A bare number, exactly like the Space slot's prism " +
                 "tally - the Charge slot reports what the blast did to the LIVING, Space what it did " +
                 "to MASS.")]
        [SerializeField] private TMP_Text pilotCountText;
        [Tooltip("CREATURES the last blast killed. Bare number, sitting under the pilot count.")]
        [SerializeField] private TMP_Text faunaCountText;
        [Tooltip("Fallback colour for the pilot count. Normally overridden by the shared config's " +
                 "whiteColor - the same white the engaged sight uses, because a pilot IS what the " +
                 "sight is for.")]
        [SerializeField] private Color pilotCountColor = new(0.96f, 0.96f, 1f, 1f);
        [Tooltip("Fallback colour for the creature count. Normally overridden by the shared config's " +
                 "blueColor - the palette's neutral-LIFEFORM range, the same blue-white a living " +
                 "creature's uncollectable heart wears (Docs/PALETTE.md 2.2).")]
        [SerializeField] private Color faunaCountColor = new(0.22f, 0.51f, 1f, 1f);
        [Tooltip("Seconds the living tally stays up after a blast.")]
        [SerializeField, Min(0.1f)] private float echoCountHoldSeconds = 2.5f;

        // ---- Mass: crystal seeding ----------------------------------------------------
        [Header("Mass — crystal seeding")]
        [Tooltip("The ability icon. If its Image type is Filled it doubles as the recharge wipe. " +
                 "The carry pips are RETIRED: the ability plants exactly one crystal per cycle at " +
                 "every level, and Mass 5 changes WHAT is planted rather than how many.")]
        [SerializeField] private Image crystalIcon;
        [Tooltip("Fallback colour of a seeding that will land as a free-for-all OMNI crystal. " +
                 "Normally overridden by ElementalBarsConfigSO.limeColor - the lime CTA, the same " +
                 "colour the crystal itself will wear standing in the cell (Docs/PALETTE.md 2.2).")]
        [SerializeField] private Color crystalOmniColor = new(0.59f, 0.92f, 0.16f, 1f);
        [Tooltip("Fallback for a TEAM-locked seeding, used only when no domain colour has been " +
                 "pushed yet. The live colour is the PILOT'S DOMAIN, supplied by the controller.")]
        [SerializeField] private Color crystalTeamFallbackColor = Color.white;
        [Tooltip("Flash colour when a seeding fires.")]
        [SerializeField] private Color crystalArmedFlashColor = Color.white;

        // ---- Space: cone blast — gape and tally ---------------------------------------
        [Header("Space — cone blast (jaws)")]
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

        [Header("Space — blast tally")]
        [Tooltip("What the last cone destroyed. Sits on its OWN row beneath the jaws so a four- or " +
                 "five-figure claim has room to render at full size rather than auto-shrinking into " +
                 "the gape.")]
        [SerializeField] private TMP_Text blastCountText;
        [SerializeField] private Color blastFlashColor = new(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color blastRestColor = Color.white;
        [Tooltip("Seconds the blast tally stays up after a cone fires.")]
        [SerializeField, Min(0.1f)] private float blastCountHoldSeconds = 2.5f;

        // ---- Time: boost charged while drifting ---------------------------------------
        [Header("Time — boost charged while drifting")]
        [Tooltip("The boost gauge itself — the Dolphin's authored 11-step ring (Boost Display). " +
                 "Bound as the Time ability icon, because Time is what sets how fast it fills. The " +
                 "ring encodes its own level by stepping through chargeSteps below — this view " +
                 "writes NOTHING else on it (see SetDriftBoost).")]
        [SerializeField] private Image driftBoostIcon;
        [Tooltip("The 11 authored ring steps, ordered EMPTY → FULL. Driven by the boost meter.")]
        [SerializeField] private List<Sprite> chargeSteps = new();

        [Header("Space — the full-energy CTA")]
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

        Vector3 _profileRestScale = Vector3.one;
        Vector3 _crystalIconRestScale = Vector3.one;
        Vector3 _jawRestScale = Vector3.one;

        Tween _profileColorTween, _profileScaleTween;
        Tween _crystalScaleTween, _crystalColorTween;
        Tween _jawUpperTween, _jawLowerTween, _jawPunchTween, _jawColorTween;
        Tween _blastColorTween, _pilotColorTween, _faunaColorTween;

        // The jaw halves' own Graphics. The row's Space icon is a fully transparent container, so
        // these two ARE the visible Space gauge and the only thing worth tinting.
        Graphic _jawUpperGraphic, _jawLowerGraphic;
        Color _jawArmedColor = Color.white;
        float _jawArm01 = -1f;
        bool _warnedArmingUnavailable;

        Color _profileRest;
        Color _profileEngaged;
        Color _pilotRest;
        Color _faunaRest;

        float _blastCountTimer;
        float _echoCountTimer;
        float _currentJawAngle;
        bool _sightEngaged;
        bool _lastSeedsTeam;
        Color _teamColor;

        public override void Initialize()
        {
            _stepsMinusOne = Mathf.Max(0, (chargeSteps?.Count ?? 0) - 1);

            ResolveBarsConfig();

            ResolveProfileColors();
            if (blastProfile)
            {
                _profileRestScale = AbilityIconRestScale(Element.Charge);
                blastProfile.rectTransform.localScale = _profileRestScale;
                blastProfile.color = _profileRest;
            }
            _sightEngaged = false;

            if (crystalIcon)
            {
                _crystalIconRestScale = AbilityIconRestScale(Element.Mass);
                if (crystalIcon.type == Image.Type.Filled) crystalIcon.fillAmount = 0f;
            }
            // The domain colour arrives from the controller on the first push; until then a team
            // seed would have nothing to paint with, so seed the fallback.
            _teamColor = crystalTeamFallbackColor;
            _lastSeedsTeam = false;
            ApplyCrystalTierColor(false, immediate: true);

            if (jawUpper) _jawRestScale = AbilityIconRestScale(Element.Space);
            // Empty energy is the MIN gape, not a shut jaw - the blast is a short capsule at rest.
            SetJawAngleImmediate(minJawAngle);

            ResolveJawGraphics();
            _jawArm01 = -1f;             // a re-init must repaint, not early-out on a stale value
            ApplyJawArming(0f, immediate: true);

            if (blastCountText)
            {
                blastCountText.color = blastRestColor;
                blastCountText.text = string.Empty;
            }
            _blastCountTimer = 0f;

            ResolveTallyColors();
            if (pilotCountText) { pilotCountText.color = _pilotRest; pilotCountText.text = string.Empty; }
            if (faunaCountText) { faunaCountText.color = _faunaRest; faunaCountText.text = string.Empty; }
            _echoCountTimer = 0f;

            // The ring's colour is authored art - never repainted from here.
            if (driftBoostIcon && driftBoostIcon.type == Image.Type.Filled)
                driftBoostIcon.fillAmount = 0f;
        }

        /// <summary>
        /// Re-anchors this view's per-icon rest scales to the shared upgrade rest scale, so the
        /// profile pulse, the crystal arm-punch and the jaw glide all settle back to the UPGRADED
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
                    _profileRestScale = rest;
                    // The profile is driven by mesh regeneration, so nothing else re-scales it.
                    if (blastProfile) blastProfile.rectTransform.localScale = rest;
                    break;
                case Element.Mass:
                    _crystalIconRestScale = rest;
                    break;
                case Element.Space:
                    _jawRestScale = rest;
                    ApplyJawRestScale(); // the jaws are driven by rotation, so nothing else re-scales them
                    break;
                // Time needs no re-anchor: nothing in this view writes the boost ring's scale, so
                // the base class's bump is never contested.
            }
        }

        // ---------------------------------------------------------------
        // Charge: the Echo Sight, drawn as the profile of the blast it aims.
        // ---------------------------------------------------------------

        /// <summary>
        /// The cross-section of the blast as it stands right now, in world units, straight from
        /// <c>VesselExplosionByCrystalEffectSO.TryResolveProfile</c>. All three numbers arrive
        /// together so the icon can never mix a radius from one frame with a reference from another.
        /// </summary>
        public void SetBlastProfile(float radius, float halfLength, float referenceExtent)
        {
            if (!blastProfile) return;
            blastProfile.SetProfile(radius, halfLength, referenceExtent);
        }

        /// <summary>
        /// The pilot picked the sight up or put it down. The profile warms to the same colour the
        /// highlight paints on the world, so the cockpit answers the trigger even when the pilot is
        /// looking at empty space with nothing out there to light up.
        /// </summary>
        public void SetSightEngaged(bool engaged)
        {
            if (!blastProfile || engaged == _sightEngaged) return;
            _sightEngaged = engaged;

            _profileColorTween?.Kill();
            _profileColorTween = blastProfile
                .DOColor(engaged ? _profileEngaged : _profileRest, profileEngageDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(blastProfile.gameObject);

            if (!engaged) return;

            _profileScaleTween?.Kill();
            blastProfile.rectTransform.localScale = _profileRestScale;
            _profileScaleTween = blastProfile.rectTransform
                .DOScale(_profileRestScale * iconPunchScale, profileEngageDuration * 0.4f)
                .SetEase(Ease.OutQuad)
                .SetLink(blastProfile.gameObject)
                .OnComplete(() =>
                {
                    _profileScaleTween = blastProfile.rectTransform
                        .DOScale(_profileRestScale, profileEngageDuration * 0.6f)
                        .SetEase(Ease.OutQuad)
                        .SetLink(blastProfile.gameObject);
                });
        }

        // ---------------------------------------------------------------
        // Mass: crystal seeding — a PASSIVE ability. Nothing is ever carried,
        // so the icon is a pure recharge fill (0 -> 1 as the next seeding arms).
        // The pips are gone with Twin Seed: the cycle plants exactly one crystal
        // at every level, and the upgrade changes its TIER, which the colour says.
        // ---------------------------------------------------------------
        public void SetCrystalSeedState(float ready01, bool seedsTeamCrystal, Color teamColor)
        {
            if (crystalIcon && crystalIcon.type == Image.Type.Filled)
                crystalIcon.fillAmount = Mathf.Clamp01(ready01);

            // The domain can change mid-match (the freestyle domain-changer toy), so a colour change
            // repaints even when the tier did not - otherwise a re-domained pilot keeps showing the
            // colour of a team their crystals no longer belong to.
            bool colorMoved = seedsTeamCrystal && teamColor != _teamColor;
            _teamColor = teamColor;

            if (seedsTeamCrystal != _lastSeedsTeam || colorMoved)
                ApplyCrystalTierColor(seedsTeamCrystal, immediate: false);
        }

        /// <summary>
        /// A cycle just fired and a crystal was planted out in the cytoplasm. The pilot gave no
        /// input and may be looking anywhere, so the slot punches to say it happened — this is the
        /// only notification the passive ability has.
        /// </summary>
        public void PulseCrystalSeeded()
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
                .DOColor(CrystalTierColor(_lastSeedsTeam), colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(crystalIcon.gameObject);
        }

        /// <summary>
        /// Which crystal the next cycle will leave behind, said in the colour that crystal will
        /// actually wear out in the cell. Below Mass 5 the seed is a free-for-all pickup and the
        /// slot shows the lime CTA; at Mass 5 it is team-locked and the slot shows so.
        /// </summary>
        void ApplyCrystalTierColor(bool seedsTeamCrystal, bool immediate)
        {
            _lastSeedsTeam = seedsTeamCrystal;
            if (!crystalIcon) return;

            var target = CrystalTierColor(seedsTeamCrystal);

            _crystalColorTween?.Kill();
            if (immediate) { crystalIcon.color = target; return; }

            _crystalColorTween = crystalIcon.DOColor(target, colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(crystalIcon.gameObject);
        }

        /// <summary>
        /// A seeding wears the colour the CRYSTAL will wear once it is standing in the cell — lime
        /// CTA while anyone can take it, the pilot's DOMAIN once Mass 5 locks it to them. The whole
        /// point of the upgrade is that it makes a team crystal, so the slot says which team.
        /// </summary>
        Color CrystalTierColor(bool seedsTeamCrystal)
        {
            if (seedsTeamCrystal) return _teamColor;

            // The free-for-all seed wears the same lime a full element flower does, read from the
            // one shared config rather than authored a second time here.
            return barsConfig ? barsConfig.limeColor : crystalOmniColor;
        }

        /// <summary>
        /// Grey while the sight is released, white while it is held — the shared palette's own
        /// "0: not in use" and "1: in use" pair, the same two colours a petal steps through between
        /// those levels. Read from <see cref="ElementalBarsConfigSO"/> rather than authored here so
        /// idle-vs-active reads identically wherever the HUD says it.
        /// </summary>
        void ResolveProfileColors()
        {
            ResolveBarsConfig();
            _profileRest = barsConfig ? barsConfig.greyColor : profileRestColor;
            _profileEngaged = barsConfig ? barsConfig.whiteColor : profileEngagedColor;
        }

        // ---------------------------------------------------------------
        // Space: the cone fired - flash the tally and show what it took.
        // ---------------------------------------------------------------
        public void ReportBlast(int destroyedCount)
        {
            if (blastCountText)
            {
                blastCountText.text = destroyedCount > 0 ? destroyedCount.ToString() : string.Empty;
                _blastCountTimer = blastCountHoldSeconds;

                // DOVirtual rather than a DOColor extension: this project ships no DOTween TMP
                // module, so TMP_Text has no tween shortcut of its own.
                _blastColorTween?.Kill();
                blastCountText.color = blastFlashColor;
                _blastColorTween = DOVirtual.Color(blastFlashColor, blastRestColor, colorTweenDuration,
                        c => { if (blastCountText) blastCountText.color = c; })
                    .SetEase(Ease.OutQuad)
                    .SetLink(blastCountText.gameObject);
            }

            // The jaws are the Space slot's icon now, so the blast's own beat lands on them - the
            // same pair that has been showing the gape it just spent.
            ReportSkim();
        }

        void Update()
        {
            if (_blastCountTimer > 0f && blastCountText)
            {
                _blastCountTimer -= Time.deltaTime;
                if (_blastCountTimer <= 0f) blastCountText.text = string.Empty;
            }

            if (_echoCountTimer > 0f)
            {
                _echoCountTimer -= Time.deltaTime;
                if (_echoCountTimer <= 0f)
                {
                    if (pilotCountText) pilotCountText.text = string.Empty;
                    if (faunaCountText) faunaCountText.text = string.Empty;
                }
            }
        }

        /// <summary>
        /// What the last blast did to LIVING things: pilots caught and debuffed, creatures killed.
        /// The Charge slot's counterpart to the Space slot's prism tally, and deliberately the same
        /// grammar — bare numbers that flash and fade — so the row reads as one language.
        ///
        /// The two are told apart by COLOUR rather than by a label or a glyph: pilots wear the
        /// palette's white (the colour the engaged sight itself wears, because a pilot is what the
        /// sight is for) and creatures wear its blue (the neutral-lifeform range a living creature's
        /// uncollectable heart already wears). A zero side is left blank rather than printing "0" —
        /// an empty slot says "none" without competing for the eye.
        /// </summary>
        public void ReportEchoTally(int pilotsDebuffed, int faunaKilled)
        {
            bool anything = pilotsDebuffed > 0 || faunaKilled > 0;

            if (pilotCountText)
            {
                pilotCountText.text = pilotsDebuffed > 0 ? pilotsDebuffed.ToString() : string.Empty;
                if (pilotsDebuffed > 0) FlashTally(pilotCountText, _pilotRest, ref _pilotColorTween);
            }

            if (faunaCountText)
            {
                faunaCountText.text = faunaKilled > 0 ? faunaKilled.ToString() : string.Empty;
                if (faunaKilled > 0) FlashTally(faunaCountText, _faunaRest, ref _faunaColorTween);
            }

            _echoCountTimer = anything ? echoCountHoldSeconds : 0f;
        }

        /// <summary>
        /// Flash a tally to the blast's own flash colour and settle back to its resting colour.
        /// DOVirtual rather than a DOColor extension: this project ships no DOTween TMP module, so
        /// TMP_Text has no tween shortcut of its own.
        /// </summary>
        void FlashTally(TMP_Text text, Color rest, ref Tween tween)
        {
            tween?.Kill();
            text.color = blastFlashColor;
            tween = DOVirtual.Color(blastFlashColor, rest, colorTweenDuration,
                    c => { if (text) text.color = c; })
                .SetEase(Ease.OutQuad)
                .SetLink(text.gameObject);
        }

        /// <summary>
        /// The two tally colours, from the shared palette rather than authored here — same rule the
        /// jaw arming and the profile follow.
        /// </summary>
        void ResolveTallyColors()
        {
            ResolveBarsConfig();
            _pilotRest = barsConfig ? barsConfig.whiteColor : pilotCountColor;
            _faunaRest = barsConfig ? barsConfig.blueColor : faunaCountColor;
        }

        // ---------------------------------------------------------------
        // Space: banked skim energy, drawn as a jaw gape. Same angle the hull
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

        void ResolveBarsConfig()
        {
            if (!barsConfig) barsConfig = Resources.Load<ElementalBarsConfigSO>(barsConfigResourcePath);
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

            ResolveBarsConfig();
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
        /// on this HUD (every icon is a live gauge) and it targets the Space container, not these
        /// halves — so the gauge colour and the upgrade signal can never contest each other.
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
        /// Parks both jaw halves at the Space slot's current rest scale, so the level-5 upgrade bump
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
        // Time: how fast the drift banks boost. The ring is an ELEVEN-SPRITE
        // authored gauge, so stepping the sprite is the whole readout. This
        // deliberately writes nothing else: a swell keyed to charge01 and a colour
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
            _profileColorTween?.Kill();
            _profileScaleTween?.Kill();
            _crystalScaleTween?.Kill();
            _crystalColorTween?.Kill();
            _jawUpperTween?.Kill();
            _jawLowerTween?.Kill();
            _jawPunchTween?.Kill();
            _jawColorTween?.Kill();
            _blastColorTween?.Kill();
            _pilotColorTween?.Kill();
            _faunaColorTween?.Kill();
        }
    }
}

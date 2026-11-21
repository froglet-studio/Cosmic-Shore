using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rhino's HUD view. Everything it shows lives on the fleet's four-card ability row
    /// (<see cref="VesselHUDView.abilityIcons"/>, drawn by the ability lockup), driven by
    /// <see cref="RhinoHUDController"/> through the base view's card pushes.
    ///
    /// <para><b>CHARGE, MASS and SPACE cards - the plough</b> (the 2026-11-12 rework; the roster audit, 2026-11-13). Each is
    /// one icon kit sprite on the card's own Image (Gore, Heft, Blade; Tools/Build/icon_kit/specs/rhino.py), its level-5
    /// picture the binding's <c>upgradedSprite</c> (Gore Steal, Berm, Unstoppable), all written into RhinoHUDVariant by
    /// Tools/Build/author_rhino_hud.py. The katana, the afterimage trail and Blade Reach this view used to draw on those
    /// cards left with the sword for the Swordfish (<c>SwordfishVesselHUDView</c>).</para>
    ///
    /// <para><b>TIME card - Ramp Spool / Momentum</b> (Garrett 2026-10-28: "this ability is about the rhino and its
    /// invulnerability to danger prisms"). A transparent container over four prefab parts: the Rhino from above
    /// flying right (domain colour), a danger prism ahead of its nose, and - while the Time level-5 ward, Momentum,
    /// holds - a white chevron ward up in front of the nose with the prism glanced away (<see cref="SetMomentum"/>).
    /// The plate stays the speed meter; the ward's threshold (70% of top speed) is ONE mark on it, drawn the
    /// Sparrow's way: a short dim tick at each slanted edge while it is ahead, a gap through the fill once passed
    /// (<see cref="SetMomentumMark"/>).</para>
    ///
    /// <para>It used to carry a pre-lockup STATUS CLUSTER beside the row - a skimmer-size ring, a
    /// "slowed" counter, a slow-line flash, a debuff timer, laser and crystal indicators - wired
    /// through six fields here and three GameObjects the Rhino prefab added to this HUD. Those fields,
    /// GameObjects and channels were deleted on 2026-10-16 (Docs/ABILITY_LOCKUP.md, vessel skill rule 38).</para>
    /// </summary>
    public class RhinoVesselHUDView : VesselHUDView
    {
        [Header("Time card - Ramp Spool / Momentum")]
        [Tooltip("The Rhino from above, flying right (Rhino_SpoolHull), always shown in the domain colour.")]
        [SerializeField] private Image spoolHullIcon;
        [Tooltip("The chevron ward ahead of the nose (Rhino_SpoolWard), shown white while Momentum holds.")]
        [SerializeField] private Image spoolWardIcon;
        [Tooltip("The danger prism ahead of the nose (Rhino_SpoolPrism), shown until Momentum holds.")]
        [SerializeField] private Image spoolPrismIcon;
        [Tooltip("The danger prism glanced away (Rhino_SpoolGlance), shown while Momentum holds.")]
        [SerializeField] private Image spoolGlanceIcon;
        [Tooltip("The ward's colour.")]
        [SerializeField] private Color spoolWardColor = Color.white;
        [Tooltip("The danger prisms' colour: the danger tint (ElementalBarsConfigSO.FireColor).")]
        [SerializeField] private Color spoolDangerColor = new(1f, 0.3294f, 0.1020f, 1f);

        [Tooltip("Thickness, in canvas units, of the Momentum threshold's marks on the Time gauge (the Sparrow's gunHeatMarkThickness).")]
        [SerializeField, Min(0.5f)] private float momentumMarkThickness = 2f;
        [Tooltip("The threshold once speed has passed it: a GAP cut through the fill - the plate showing through " +
                 "(match AbilityLockupStyle's gauge track colour).")]
        [SerializeField] private Color momentumGapColor = new(0.086f, 0.094f, 0.129f, 0.9f);
        [Tooltip("The threshold while speed is still below it: a short tick in from each slanted edge of the plate.")]
        [SerializeField] private Color momentumMarkAheadColor = new(1f, 1f, 1f, 0.35f);
        [Tooltip("Length, in canvas units, of an ahead tick, in from each edge of the gauge.")]
        [SerializeField, Min(1f)] private float momentumTickLength = 7f;
        [Tooltip("How far the plate's slanted edge sits in from the gauge's rect at the BOTTOM of the card, in canvas " +
                 "units (AbilityLockupStyle's trapezoid inset): a tick starts at the slant at its own height.")]
        [SerializeField, Min(0f)] private float momentumTickSlantInset = 9f;

        Image _momentumGap, _momentumTickLeft, _momentumTickRight;
        float _momentumThreshold01 = -1f;

        /// <summary>Every card shows its authored picture from the first frame.</summary>
        public override void Initialize()
        {
            foreach (var binding in abilityIcons)
            {
                if (!binding.icon || !binding.icon.sprite) continue;   // Time is the parts' container: left as authored
                binding.icon.enabled = true;
            }
        }

        /// <summary>Which Time-card parts show: the prism ahead until Momentum holds; then the ward and the glanced prism.</summary>
        public struct MomentumParts
        {
            public bool Prism, Glance, Ward;
        }

        /// <summary>What <see cref="SetMomentum"/> shows, as a pure function so it is tested without a canvas.</summary>
        public static MomentumParts MomentumPartsFor(bool warded) => new() { Prism = !warded, Glance = warded, Ward = warded };

        /// <summary>
        /// The Time card: the hull in the pilot's <paramref name="domain"/> colour, and the ward up with the danger
        /// prism glanced away while Momentum is <paramref name="warded"/>, the prism ahead of the nose otherwise.
        /// </summary>
        public void SetMomentum(bool warded, Color domain)
        {
            var parts = MomentumPartsFor(warded);
            if (spoolHullIcon) spoolHullIcon.color = domain;
            var danger = ColorTrace.Retrace(ColorTrace.Ui, spoolDangerColor);
            ShowPart(spoolPrismIcon, parts.Prism, danger);
            ShowPart(spoolGlanceIcon, parts.Glance, danger);
            ShowPart(spoolWardIcon, parts.Ward, spoolWardColor);
        }

        static void ShowPart(Image part, bool shown, Color color)
        {
            if (!part) return;
            part.enabled = shown;
            part.color = color;
        }

        /// <summary>
        /// The Momentum threshold on the Time gauge, <paramref name="threshold01"/> of top speed, against the live
        /// <paramref name="speed01"/>: a dim tick in from each slanted edge while it is ahead, a gap through the fill
        /// once passed (the Sparrow's heat marks, <c>SparrowHUDView.SetGunHeatPhaseJoins</c>). A threshold of 0 or
        /// less hides the mark.
        /// </summary>
        public void SetMomentumMark(float threshold01, float speed01)
        {
            if (!TryGetAbilityGauge(Element.Time, out var gauge) || !gauge) return;

            if (!_momentumGap)
            {
                var parent = gauge.rectTransform;
                _momentumGap = ResolveGeneratedChild<Image>(parent, "MomentumGap");
                _momentumTickLeft = ResolveGeneratedChild<Image>(parent, "MomentumTickL");
                _momentumTickRight = ResolveGeneratedChild<Image>(parent, "MomentumTickR");
                _momentumGap.raycastTarget = _momentumTickLeft.raycastTarget = _momentumTickRight.raycastTarget = false;
                _momentumThreshold01 = -1f;
            }

            if (!Mathf.Approximately(threshold01, _momentumThreshold01))
            {
                _momentumThreshold01 = threshold01;
                float slant = momentumTickSlantInset * (1f - threshold01);   // the plate narrows toward its base
                PlaceMomentumMark(_momentumGap, 0f, 1f, threshold01, 0f, 0f);
                PlaceMomentumMark(_momentumTickLeft, 0f, 0f, threshold01, momentumTickLength, slant);
                PlaceMomentumMark(_momentumTickRight, 1f, 1f, threshold01, momentumTickLength, -slant);
            }

            bool shown = threshold01 > 0f;
            bool passed = speed01 >= threshold01;
            _momentumGap.gameObject.SetActive(shown && passed);
            _momentumTickLeft.gameObject.SetActive(shown && !passed);
            _momentumTickRight.gameObject.SetActive(shown && !passed);
            _momentumGap.color = ColorTrace.Retrace(ColorTrace.Ui, momentumGapColor);
            _momentumTickLeft.color = _momentumTickRight.color = momentumMarkAheadColor;
        }

        // A horizontal mark at height y01 of the gauge, spanning anchors x0..x1 (a stretched gap), or `width` canvas
        // units in from that edge (x0 == x1: a tick, pivoted on the edge so it grows inward, shifted `dx` to the slant).
        void PlaceMomentumMark(Image img, float x0, float x1, float y01, float width, float dx)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(x0, y01);
            rt.anchorMax = new Vector2(x1, y01);
            rt.pivot = new Vector2(x0, 0.5f);
            rt.anchoredPosition = new Vector2(dx, 0f);
            rt.sizeDelta = new Vector2(width, momentumMarkThickness);
        }
    }
}

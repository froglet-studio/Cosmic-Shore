using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Butterfly's HUD readouts (design: <c>R_VesselActions/BUTTERFLY.md</c> §5). The row order
    /// IS the element map, so each gauge is named by the ELEMENT it sits under, never by "the
    /// second icon": <b>Scale Dust (charge) · Spread Wings (mass) · Wingreach (space) ·
    /// Fold (time)</b>.
    ///
    /// <para>This vessel is deliberately sparse on the HUD. Two of its four abilities are PASSIVE —
    /// the dust and the wings' reach happen because you flew somewhere, and neither has a state a
    /// pilot can be waiting on — so neither gets a gauge. Drawing one anyway would be an
    /// instrument reporting a decision nobody makes, and the fleet's locked-card rule already
    /// covers "this slot is real but has nothing to say".</para>
    ///
    /// <para>The two that DO have state get one readout each:</para>
    /// <list type="number">
    /// <item><b>Wing energy</b> (mass) — a linear fill on the Spread Wings plate, which is the
    /// fleet's gauge form. It answers "how much broad stroke have I got left", and it is pinned
    /// FULL once Mass 5 ("Mural") makes the spread free, because a gauge that kept draining would
    /// be reporting a cost nobody is paying.</item>
    /// <item><b>Fold recharge</b> — the fleet's clockwise depleting veil on the Time plate, pushed
    /// by the controller. It is the ONLY feedback a refused Fold press gets, which is why it is
    /// driven every frame rather than off an edge: a veil that were merely "roughly right" would
    /// leave the pilot pressing a button that does nothing, the exact failure the platform's
    /// refusal rule exists to prevent.</item>
    /// </list>
    ///
    /// <para>Because the energy gauge paints an ability PLATE, colour here is a GAUGE channel and
    /// can carry no second meaning. The upgrade lives on the lockup's card
    /// (<c>Docs/ABILITY_LOCKUP.md</c>), so this view only has to re-anchor its captured rest scales
    /// in <see cref="SetAbilityUpgraded"/> — otherwise the next gauge write erases the upgrade's
    /// scale bump, which the Squirrel learned first.</para>
    /// </summary>
    public class ButterflyHUDView : VesselHUDView
    {
        [Header("Wing energy (Mass row — Spread Wings)")]
        [Tooltip("Filled Image driven 0..1 by the wing-energy meter. The ability lockup RE-HOMES " +
                 "and masks this to the ability plate at build time, so author it as an ordinary " +
                 "filled Image and keep writing fillAmount on this very object — no drive site " +
                 "changes when the lockup adopts it.")]
        [SerializeField] Image wingEnergyGauge;

        [Tooltip("Gauge colour while there is energy to spend.")]
        [SerializeField] Color energyColor = new(0.65f, 0.85f, 1f, 1f);

        [Tooltip("Gauge colour once the meter is dry and the wings have sagged shut.")]
        [SerializeField] Color emptyColor = new(0.55f, 0.35f, 0.35f, 1f);

        [Tooltip("Below this fraction the gauge reads as empty. A hair above zero so the colour " +
                 "flips as the wings actually close rather than a frame later.")]
        [SerializeField, Range(0f, 0.5f)] float emptyThreshold = 0.02f;

        /// <summary>Push the wing-energy meter, 0..1.</summary>
        public void SetWingEnergy(float normalized)
        {
            if (!wingEnergyGauge) return;
            float value = Mathf.Clamp01(normalized);
            wingEnergyGauge.fillAmount = value;
            wingEnergyGauge.color = value <= emptyThreshold ? emptyColor : energyColor;
        }

        /// <summary>
        /// Re-anchor the captured rest scales after the base's upgrade handling, or this view's
        /// own gauge writes settle back to the pre-upgrade scale and wipe the lockup's persistent
        /// bump. The reference implementation is <c>SquirrelVesselHUDView</c>; the trap it records
        /// is that a rest-scale field written but never READ leaves exactly one icon in the row
        /// with no L5 bump and nothing to explain it.
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Mass && wingEnergyGauge)
                wingEnergyGauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}

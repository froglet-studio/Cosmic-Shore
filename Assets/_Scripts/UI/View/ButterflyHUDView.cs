using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Butterfly's HUD readouts (design: <c>R_VesselActions/BUTTERFLY.md</c> §5). The row order
    /// IS the element map, so each gauge is named by the ELEMENT it sits under, never by "the
    /// second icon": <b>Scale Dust (charge) · Mass / Dust Mode (mass) · Dust Reach (space) ·
    /// Fold (time)</b>.
    ///
    /// <para>This vessel is deliberately sparse on the HUD. Two of its four abilities are PASSIVE —
    /// the dust's bite and its reach happen because you flew somewhere, and neither has a state a
    /// pilot can be waiting on — so neither gets a gauge. Drawing one anyway would be an
    /// instrument reporting a decision nobody makes, and the fleet's locked-card rule already
    /// covers "this slot is real but has nothing to say".</para>
    ///
    /// <para>The two that DO have state get one readout each:</para>
    /// <list type="number">
    /// <item><b>Mode</b> (mass) — a linear fill on the Mass plate that is FULL in Mass mode and
    /// EMPTY in Dust mode, travelling between the two as the wake opens or narrows. It used to be
    /// a wing-energy meter; the energy cost was retired when the right trigger became a mode
    /// switch, and a gauge whose meter is gone would be a lie (the vessel contract's rule 15), so
    /// the same plate now answers the question the trigger actually poses: which mode am I in.
    /// It is a BINARY state drawn with a transition, never a partial fill a pilot could read as a
    /// quantity.</item>
    /// <item><b>Fold recharge</b> — the fleet's clockwise depleting veil on the Time plate, pushed
    /// by the controller. It is the ONLY feedback a refused Fold press gets, which is why it is
    /// driven every frame rather than off an edge: a veil that were merely "roughly right" would
    /// leave the pilot pressing a button that does nothing, the exact failure the platform's
    /// refusal rule exists to prevent.</item>
    /// </list>
    ///
    /// <para>Because the mode gauge paints an ability PLATE, colour here is a GAUGE channel and
    /// can carry no second meaning. The upgrade lives on the lockup's card
    /// (<c>Docs/ABILITY_LOCKUP.md</c>), so this view only has to re-anchor its captured rest scales
    /// in <see cref="SetAbilityUpgraded"/> — otherwise the next gauge write erases the upgrade's
    /// scale bump, which the Squirrel learned first.</para>
    /// </summary>
    public class ButterflyHUDView : VesselHUDView
    {
        [Header("Mode (Mass row — Mass mode / Dust mode)")]
        [Tooltip("Filled Image on the Mass plate: FULL in Mass mode, EMPTY in Dust mode. The field " +
                 "keeps its old name because the HUD variant is authored by " +
                 "Tools/Build/author_butterfly_ability_row.py, which writes this key. The ability " +
                 "lockup RE-HOMES and masks it to the plate at build time, so keep writing " +
                 "fillAmount on this very object.")]
        [SerializeField] Image wingEnergyGauge;

        [Tooltip("Gauge colour in Mass mode — the wide wake.")]
        [SerializeField] Color massModeColor = new(0.65f, 0.85f, 1f, 1f);

        [Tooltip("Gauge colour as it reaches Dust mode.")]
        [SerializeField] Color dustModeColor = new(1f, 0.82f, 0.45f, 1f);

        /// <summary>
        /// Seat every readout at its resting value. Idempotent, because this re-runs on a vessel
        /// swap: the HUD component is not rebuilt, so a field left holding the previous pilot's
        /// last value would be shown for one frame as if it were this one's.
        ///
        /// <para>Full is the honest seed — a Butterfly spawns in Mass mode.</para>
        /// </summary>
        public override void Initialize()
        {
            SetMassMode(1f);
            SetAbilityCooldown(Element.Time, 0f);
        }

        /// <summary>Push the mode, 1 = Mass mode, 0 = Dust mode, anything between = switching.</summary>
        public void SetMassMode(float massMode01)
        {
            if (!wingEnergyGauge) return;
            float value = Mathf.Clamp01(massMode01);
            wingEnergyGauge.fillAmount = value;
            wingEnergyGauge.color = Color.Lerp(dustModeColor, massModeColor, value);
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

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

        [Header("Charge card: Scale Dust (icon_kit specs/tigermoth.py, Garrett picked A 2026-10-31)")]
        [Tooltip("Charge 5 Toll Gate: the wormhole ring (TigerMoth_ScaleDustRing), the domain colour; danger as a " +
                 "rival pays the toll. Hidden before the upgrade.")]
        [SerializeField] Image scaleDustRingIcon;
        [Tooltip("The dust (TigerMoth_ScaleDustMotes), the pilot's shielded colour; dim in Mass mode.")]
        [SerializeField] Image scaleDustMotesIcon;
        [Tooltip("A rival in the dust (TigerMoth_ScaleDustRival): grey, danger for a beat when the dust bites.")]
        [SerializeField] Image scaleDustRivalIcon;
        [Tooltip("The Tiger Moth, wings shut (TigerMoth_ScaleDustMoth), the domain colour.")]
        [SerializeField] Image scaleDustMothIcon;
        [Tooltip("The rival before the dust reaches it.")]
        [SerializeField] Color rivalIdleColor = new(0.588f, 0.604f, 0.667f, 1f);
        [Tooltip("A rival the dust is biting, and a wormhole a rival pays through.")]
        [SerializeField] Color hitColor = new(1f, 0.3294f, 0.102f, 1f);
        [Tooltip("How long a bite or a toll shows (seconds).")]
        [SerializeField, Min(0f)] float hitFlashSeconds = 0.6f;
        [Tooltip("Alpha of the Charge card's parts in Mass mode (no dust).")]
        [SerializeField, Range(0f, 1f)] float noDustAlpha = 0.35f;

        [Header("Time card: the Tiger Moth's Drop (icon_kit specs/tigermoth.py, Garrett picked A 2026-10-31)")]
        [Tooltip("Charge 5 Toll Gate: the wormhole the drop leaves (TigerMoth_DropMouth), the domain colour; danger as " +
                 "a rival pays the toll. Hidden before the upgrade.")]
        [SerializeField] Image dropMouthIcon;
        [Tooltip("The fall streaks (TigerMoth_DropStreaks), white: full while dropping, dimmed otherwise.")]
        [SerializeField] Image dropStreaksIcon;
        [Tooltip("The Tiger Moth, wings shut (TigerMoth_DropMoth), the domain colour.")]
        [SerializeField] Image dropMothIcon;
        [Tooltip("Alpha of the streaks while the drop is ready but not held.")]
        [SerializeField, Range(0f, 1f)] float dropIdleAlpha = 0.6f;
        [Tooltip("Alpha every Time part dims to while the drop recharges (the fleet's veil sweeps over it).")]
        [SerializeField, Range(0f, 1f)] float dropRechargingAlpha = 0.3f;

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
            _dropShown = _dustShown = -1;
            SetDrop(false, true, false, false);
            SetScaleDust(false, false, false, false);
        }

        int _dropShown = -1, _dustShown = -1;
        Color _domain = Color.white, _shielded = Color.white;

        /// <summary>The pilot's domain and shielded colours (alpha 0 = not resolved yet, keep the last).</summary>
        public void SetPilotColors(Color domain, Color shielded)
        {
            if (domain.a > 0f && domain != _domain) { _domain = domain; _dropShown = _dustShown = -1; }
            if (shielded.a > 0f && shielded != _shielded) { _shielded = shielded; _dustShown = -1; }
        }

        static Color A(Color c, float a) => new(c.r, c.g, c.b, a);

        /// <summary>
        /// The Drop on the Time card (<c>TigerMothDropExecutor.IsDropping</c> / <c>IsReady</c>): the moth in the domain
        /// colour falling, the streaks full while the trigger holds it and dim while it waits, every part dim while it
        /// recharges. The wormhole shows only at Charge 5 Toll Gate (<paramref name="tollGate"/>), in the domain colour,
        /// and in <see cref="hitColor"/> while <paramref name="tolled"/> (a rival just paid through it).
        /// </summary>
        public void SetDrop(bool dropping, bool ready, bool tollGate, bool tolled)
        {
            int key = (dropping ? 1 : 0) | (ready ? 2 : 0) | (tollGate ? 4 : 0) | (tolled ? 8 : 0);
            if (key == _dropShown) return;
            _dropShown = key;
            float live = dropping || ready ? 1f : dropRechargingAlpha;
            float streaks = dropping ? 1f : ready ? dropIdleAlpha : dropRechargingAlpha;
            if (dropMouthIcon)
            {
                dropMouthIcon.enabled = tollGate;
                dropMouthIcon.color = A(tolled ? hitColor : _domain, live);
            }
            if (dropStreaksIcon) dropStreaksIcon.color = A(Color.white, streaks);
            if (dropMothIcon) dropMothIcon.color = A(_domain, live);
        }

        /// <summary>
        /// Scale Dust on the Charge card: the moth over its dust and a rival in it, grey until the dust bites
        /// (<paramref name="hit"/>, from <c>ButterflyDustField.LastBiteTime</c>), <see cref="hitColor"/> then; all dim in
        /// Mass mode. At Charge 5 Toll Gate the wormhole ring shows, in the domain colour, red while a rival pays.
        /// </summary>
        public void SetScaleDust(bool dustOn, bool hit, bool tollGate, bool tolled)
        {
            int key = (dustOn ? 1 : 0) | (hit ? 2 : 0) | (tollGate ? 4 : 0) | (tolled ? 8 : 0);
            if (key == _dustShown) return;
            _dustShown = key;
            float a = dustOn ? 1f : noDustAlpha;
            if (scaleDustRingIcon)
            {
                scaleDustRingIcon.enabled = tollGate;
                scaleDustRingIcon.color = tolled ? hitColor : _domain;
            }
            if (scaleDustMotesIcon) scaleDustMotesIcon.color = A(_shielded, a);
            if (scaleDustRivalIcon) scaleDustRivalIcon.color = A(hit && dustOn ? hitColor : rivalIdleColor, a);
            if (scaleDustMothIcon) scaleDustMothIcon.color = A(_domain, dustOn ? 1f : noDustAlpha);
        }

        /// <summary>How long a bite or a toll shows on the cards.</summary>
        public float HitFlashSeconds => hitFlashSeconds;

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

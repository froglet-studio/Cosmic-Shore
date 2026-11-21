using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Stop — Dig In (Button1). See GRIZZLY_DIG_IN.md.
    /// Halt movement to dig in and BRACE: while planted the Grizzly fills faster, energy and cannon alike.
    ///
    /// <para><b>Charge is how hard it braces</b> (2026-11-16, the weakest-abilities pass: Dig In only raised an
    /// invisible regen rate). One dial, <see cref="dugInFill"/>: x2 at rest -> x5 at Charge 10, half strength at -5.
    /// It multiplies the energy regeneration AND the charged cannon's charge rate while dug in, so a dug-in Grizzly
    /// visibly winds its cannon faster: dig in, wind up, bombard, rush out. Charge 5 = Battle Sight.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyDigInAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Dig In")]
    public class GrizzlyDigInActionSO : ShipActionSO
    {
        [SerializeField, Tooltip("Index of the Grizzly's single Energy resource.")]
        int energyIndex = 0;
        [SerializeField, Tooltip("Charge: how much faster a dug-in Grizzly fills - energy regeneration and the charged " +
                                 "cannon's wind-up alike (x2 at rest -> x5 at level 10, x1 at level -5).")]
        ElementalFloat dugInFill = ElementalFloat.Multiplier(2f, 5f, Element.Charge, 1f);

        public int EnergyIndex => energyIndex;

        /// <summary>The dug-in fill multiplier for this vessel's live Charge level (never below 1).</summary>
        public float FillFor(IVesselStatus status) => Mathf.Max(1f, dugInFill.EvaluateLive(status));

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyDigInActionExecutor>()?.Toggle(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // NO-OP for toggle
        }
    }
}

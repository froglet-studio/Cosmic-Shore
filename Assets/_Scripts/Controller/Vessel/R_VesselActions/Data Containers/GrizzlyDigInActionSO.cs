using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Stop — Dig In &amp; Reload (Button1). See GRIZZLY_DIG_IN.md.
    /// Halt movement to dig in; Energy regenerates faster while parked.
    /// Element link: Charge scales the parked regen rate; Charge 5 = Battle Sight (stub hook).
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyDigInAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Dig In")]
    public class GrizzlyDigInActionSO : ShipActionSO
    {
        [SerializeField, Tooltip("Index of the Grizzly's single Energy resource.")]
        int energyIndex = 0;
        [SerializeField, Tooltip("Base regen multiplier while dug in, before Charge scaling.")]
        float stationaryGainMultiplier = 2f;
        [SerializeField, Tooltip("Charge element multiplier on the parked regen rate at level 10.")]
        float chargeScaleAtFull = 3f;
        [SerializeField] float chargeScaleMinMul = 0.5f;

        public int EnergyIndex => energyIndex;
        public float StationaryGainMultiplier => stationaryGainMultiplier;
        public float ChargeScaleAtFull => chargeScaleAtFull;
        public float ChargeScaleMinMul => chargeScaleMinMul;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyDigInActionExecutor>()?.Toggle(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // NO-OP for toggle
        }
    }
}

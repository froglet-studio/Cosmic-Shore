using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "GrowSkimmerAction", menuName = "ScriptableObjects/Vessel Actions/Grow Skimmer")]
    public class GrowSkimmerActionSO : ShipActionSO
    {
        [Header("Size")]
        [SerializeField] ElementalFloat maxSize = new(3f);
        [SerializeField] float growRate = 1.5f;
        /// <summary>How fast the skimmer shrinks back. Read as a RAW number
        /// (<see cref="ShrinkRate"/> returns <c>.Value</c>) on a ScriptableObject, so nothing
        /// evaluates it — authored <c>Enabled: 0</c> to say so. The Rhino's asset carried an
        /// enabled 6 -> 2 CHARGE ramp that had never run; switching it on is a balance change
        /// and a design call, not a cleanup (Docs/ElementalAbilitySystem/BACKLOG.md).</summary>
        [SerializeField] ElementalFloat shrinkRate = new(1f);

        [Header("Boost effect (future hook)")]
        [SerializeField] bool applyBoostWhileGrowing = false;
        [SerializeField] ElementalFloat boostMultiplier = new(1.25f);

        public float MaxSize => maxSize.Value;
        public float GrowRate => growRate;
        public float ShrinkRate => shrinkRate.Value;
        public bool ApplyBoostWhileGrowing => applyBoostWhileGrowing;
        public float BoostMultiplier => boostMultiplier.Value;


        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowSkimmerActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowSkimmerActionExecutor>()?.End();
    }
}

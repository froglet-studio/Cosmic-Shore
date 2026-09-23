using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "GrowSkimmerAction", menuName = "ScriptableObjects/Vessel Actions/Grow Skimmer")]
    public class GrowSkimmerActionSO : ShipActionSO
    {
        [Header("Size")]
        [SerializeField] float maxSize = 120f;
        [SerializeField] float growRate = 1.5f;
        /// <summary>How fast the skimmer shrinks back. A plain float, and the TYPE is the
        /// statement: nothing scales this number. It was an <c>ElementalFloat</c> whose asset
        /// carried an enabled 6 -> 2 CHARGE ramp that had never run, because an ElementalFloat on
        /// a ScriptableObject read as <c>.Value</c> cannot evaluate. Giving that ramp to CHARGE is
        /// still a balance change and a design call, not a cleanup
        /// (Docs/ElementalAbilitySystem/BACKLOG.md).</summary>
        [SerializeField] float shrinkRate = 6f;

        [Header("Boost effect (future hook)")]
        [SerializeField] bool applyBoostWhileGrowing = false;
        [SerializeField] float boostMultiplier = 1.25f;

        public float MaxSize => maxSize;
        public float GrowRate => growRate;
        public float ShrinkRate => shrinkRate;
        public bool ApplyBoostWhileGrowing => applyBoostWhileGrowing;
        public float BoostMultiplier => boostMultiplier;


        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowSkimmerActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowSkimmerActionExecutor>()?.End();
    }
}

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One trigger of the Grizzly's bomb pump (LT or RT). Press arms the trigger, release blows
    /// the bomb — sized by the PEAK pressure of that pull. Two assets (Left / Right) share one
    /// executor and one config; the only thing this asset carries is which trigger it is,
    /// because each trigger has its own cooldown. See GRIZZLY_BOMB_PUMP.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyBombAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Bomb")]
    public class GrizzlyBombActionSO : ShipActionSO
    {
        public enum TriggerSide { Left = 0, Right = 1 }

        [SerializeField, Tooltip("Which trigger this asset is bound to. Must match the input event it is listed under on the vessel prefab.")]
        TriggerSide side = TriggerSide.Right;

        public TriggerSide Side => side;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyBombPumpExecutor>()?.OnPress(side);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyBombPumpExecutor>()?.OnRelease(side);
    }
}

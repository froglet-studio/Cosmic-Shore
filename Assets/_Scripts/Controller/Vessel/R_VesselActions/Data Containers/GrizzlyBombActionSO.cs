using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One trigger of the Grizzly's TRIGGER BOMBS (LT or RT). Each trigger owns one bomb:
    /// pull arms it, release fires it (sized by the PEAK pressure of that pull, which also sets
    /// the ammo spent), pull again freezes it, release detonates it - launching a Grizzly caught
    /// in its own blast. Two assets (Left / Right) share one executor and one config; the only
    /// thing this asset carries is which trigger it is. See GRIZZLY_TRIGGER_BOMBS.md.
    ///
    /// Press and release both carry meaning, so StopAction is the release - not a no-op.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyBombAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Bomb")]
    public class GrizzlyBombActionSO : ShipActionSO
    {
        public enum TriggerSide { Left = 0, Right = 1 }

        [SerializeField, Tooltip("Which trigger this asset is bound to. Must match the input event it is listed under on the vessel prefab.")]
        TriggerSide side = TriggerSide.Right;

        public TriggerSide Side => side;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyTriggerBombExecutor>()?.OnPress(side);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyTriggerBombExecutor>()?.OnRelease(side);
    }
}

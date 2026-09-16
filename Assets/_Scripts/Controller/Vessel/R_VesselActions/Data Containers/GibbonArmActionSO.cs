using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One arm's trigger, bound to <c>InputEvents.LeftStickAction</c> (LT) or
    /// <c>RightStickAction</c> (RT).
    ///
    /// Both EDGES carry meaning here, so this is a real SO with a body rather than the
    /// <c>MantaAnalogTurnBoostActionSO</c> no-op-plus-per-frame-executor shape: a press drops the
    /// line this arm is holding and begins charging the next, and a release fires it. The analog
    /// depth in between is the executor's per-frame job, because the release edge arrives with the
    /// trigger already back below the deadzone.
    ///
    /// The asset is SHARED and STATELESS — which arm it is comes from <see cref="arm"/> on the
    /// asset, and all per-vessel state lives on the executor.
    /// </summary>
    [CreateAssetMenu(fileName = "GibbonArmAction",
                     menuName = "ScriptableObjects/Vessel Actions/Gibbon Arm")]
    public class GibbonArmActionSO : ShipActionSO
    {
        [SerializeField] GibbonTetherExecutor.Arm arm = GibbonTetherExecutor.Arm.Right;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
            => executors?.Get<GibbonTetherExecutor>()?.BeginCharge(arm);

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status)
            => executors?.Get<GibbonTetherExecutor>()?.Fire(arm);
    }
}

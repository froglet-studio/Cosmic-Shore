using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's DROP (Caleb, 2026-11-16): one bomb from the same energy the joust spends, let go with the Manta's
    /// velocity and falling along its own down, that goes off on the first vessel or prism it touches. All the rules
    /// and numbers live on <see cref="MantaStingConfigSO"/> and the executor (<see cref="MantaStingActionExecutor.TryDropBomb"/>);
    /// this asset is only the press. Bound to pad A (InputEvents.Button1Action) on Manta.prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "MantaDropBombAction", menuName = "ScriptableObjects/Vessel Actions/Manta Drop Bomb")]
    public class MantaDropBombActionSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<MantaStingActionExecutor>()?.TryDropBomb();

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}

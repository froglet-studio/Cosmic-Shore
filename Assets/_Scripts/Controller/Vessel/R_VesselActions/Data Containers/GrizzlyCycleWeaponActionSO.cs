using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Change Weapon (left trigger): cycles Explosives → Sniper (→ Plasma Claw
    /// at Mass 5). The active mode lives on GrizzlyWeaponModeExecutor (per vessel);
    /// GrizzlyModeSwitchingFireSO reads it when Fire is pressed.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyCycleWeaponAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Cycle Weapon")]
    public class GrizzlyCycleWeaponActionSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyWeaponModeExecutor>()?.Cycle(vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // Cycle happens on press only.
        }
    }
}

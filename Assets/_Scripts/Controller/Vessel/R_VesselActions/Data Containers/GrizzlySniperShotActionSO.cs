using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Change-Weapon mode: Sniper. Long-range precision shot with a burst
    /// that CRACKS SUPER SHIELDS (the only gameplay source of super-shield removal —
    /// opens them up for mass production, per the class doc). Large energy cost and
    /// cooldown; the Grizzly's Scout-3 tool.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlySniperShotAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Sniper Shot")]
    public class GrizzlySniperShotActionSO : ShipActionSO
    {
        [Header("Shot")]
        [SerializeField] float projectileSpeed = 900f;
        [SerializeField] float projectileScale = 4f;
        [SerializeField, Tooltip("Long flight for cross-cell range.")]
        float projectileTime = 12f;

        [Header("Economy")]
        [SerializeField] int energyIndex = 0;
        [SerializeField, Tooltip("Large flat energy cost per sniper shot.")]
        float energyCost = 0.5f;
        [SerializeField] float cooldownSeconds = 3f;

        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileScale => projectileScale;
        public float ProjectileTime => projectileTime;
        public int EnergyIndex => energyIndex;
        public float EnergyCost => energyCost;
        public float CooldownSeconds => cooldownSeconds;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlySniperShotActionExecutor>()?.OnPress(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlySniperShotActionExecutor>()?.OnRelease(this, vesselStatus);
    }
}

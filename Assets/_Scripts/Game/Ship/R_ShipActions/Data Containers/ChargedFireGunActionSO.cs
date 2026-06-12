using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "ChargedFireGunAction", menuName = "ScriptableObjects/Vessel Actions/Charged Fire Gun")]
public class ChargedFireGunActionSO : ShipActionSO
{
    [Header("Charging")]
    [Tooltip("Energy gained per second while the action is held.")]
    [SerializeField] float chargePerSecond = 0.4f;
    [SerializeField] int energyResourceIndex = 0;

    [Header("Firing")]
    [SerializeField] int ammoResourceIndex = 1;
    [Tooltip("Projectile scale is multiplied by the charge accumulated at release.")]
    [SerializeField] float projectileScale = 30f;
    [SerializeField] float projectileSpeed = 90f;
    [Tooltip("Lifetime of the charged shot. It flies until stopped or detonated.")]
    [SerializeField] float projectileTime = 9999f;

    public float ChargePerSecond => chargePerSecond;
    public int EnergyResourceIndex => energyResourceIndex;
    public int AmmoResourceIndex => ammoResourceIndex;
    public float ProjectileScale => projectileScale;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileTime => projectileTime;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<ChargedFireGunActionExecutor>()?.Begin(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<ChargedFireGunActionExecutor>()?.Release(this, vesselStatus);
}

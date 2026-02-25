using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "FireTrailBlockAction", menuName = "ScriptableObjects/Vessel Actions/Fire Trail Block")]
public class FireTrailBlockActionSO : ShipActionSO
{
    [Header("Config")]
    [SerializeField] float firingRate = 0.3f;   // slower fire
    [SerializeField] float projectileTime = 4f; // blocks live a bit longer
    [SerializeField] float projectileScale = 1.2f;
    [SerializeField] float projectileSpeed = 10f;

    [Header("Block Settings")]
    [Tooltip("Should trail blocks fired be shielded?")]
    [SerializeField] bool shielded = true;
    [Tooltip("Allow friendly fire?")]
    [SerializeField] bool friendlyFire = true;

    public float FiringRate => firingRate;
    public float ProjectileTime => projectileTime;
    public float ProjectileScale => projectileScale;
    public float ProjectileSpeed => projectileSpeed;
    public bool Shielded => shielded;
    public bool FriendlyFire => friendlyFire;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus status)
        => execs?.Get<FireTrailBlockActionExecutor>()?.Begin(this);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus status)
        => execs?.Get<FireTrailBlockActionExecutor>()?.End();
}
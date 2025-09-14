// FullAutoActionSO.cs
using CosmicShore.Game.Projectiles;
using UnityEngine;

[CreateAssetMenu(fileName="FullAutoAction", menuName="CosmicShore/Actions/Full Auto")]
public class FullAutoActionSO : ShipActionSO
{
    [Header("Config")]
    [SerializeField] int   ammoIndex = 0;
    [SerializeField] float ammoCost  = 0.03f;
    [SerializeField] bool  inherit   = false;
    [SerializeField] float projectileScale = 1f;
    [SerializeField] float firingRate = 1f;
    [SerializeField] float projectileTime = 3f;
    [SerializeField] FiringPatterns firingPattern = FiringPatterns.Default;
    [SerializeField] int   energy = 0;
    [SerializeField] ElementalFloat speedValue;

    public int AmmoIndex => ammoIndex;
    public float AmmoCost => ammoCost;
    public bool Inherit => inherit;
    public float ProjectileScale => projectileScale;
    public float FiringRate => firingRate;
    public float ProjectileTime => projectileTime;
    public FiringPatterns FiringPattern => firingPattern;
    public int Energy => energy;
    public ElementalFloat SpeedValue => speedValue;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<FullAutoActionExecutor>()?.Begin(this);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<FullAutoActionExecutor>()?.End();
}
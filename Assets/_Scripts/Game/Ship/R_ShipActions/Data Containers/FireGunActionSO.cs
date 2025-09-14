using UnityEngine;

[CreateAssetMenu(fileName = "FireGunAction", menuName = "ScriptableObjects/Vessel Actions/Fire Gun")]
public class FireGunActionSO : ShipActionSO
{
    [Header("Config")]
    [SerializeField] int ammoIndex = 0;
    [SerializeField] float ammoCost = 0.03f;
    [SerializeField] float projectileScale = 1f;
    [SerializeField] int energy = 0;
    [SerializeField] float speed = 90f;
    [SerializeField] ElementalFloat projectileTime;

    public int AmmoIndex => ammoIndex;
    public float AmmoCost => ammoCost;
    public float ProjectileScale => projectileScale;
    public int Energy => energy;
    public float Speed => speed;
    public ElementalFloat ProjectileTime => projectileTime;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<FireGunActionExecutor>()?.Fire(this, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
    {
    }
}
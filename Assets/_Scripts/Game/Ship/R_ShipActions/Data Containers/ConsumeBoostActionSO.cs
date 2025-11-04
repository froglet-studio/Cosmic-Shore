using UnityEngine;

[CreateAssetMenu(fileName = "ConsumeBoostAction", menuName = "ScriptableObjects/Vessel Actions/Consume Boost")]
public class ConsumeBoostActionSO : ShipActionSO
{
    [Header("Boost Effect")]
    [SerializeField] private ElementalFloat boostMultiplier = new(4f);
    [SerializeField] private float boostDuration = 4f;

    [Header("Magazine (charges)")]
    [SerializeField, Range(1, 4)] private int maxCharges = 4;
    [SerializeField] private float reloadCooldown = 3f;  
    [SerializeField] private float reloadFillTime = 0.8f;  

    [Header("Optional resource gate (one-time spend per shot; set <=0 to ignore)")]
    [SerializeField] private int resourceIndex = 1;
    [SerializeField] private float resourceCost = 0f;

    public ElementalFloat BoostMultiplier => boostMultiplier;
    public float BoostDuration => boostDuration;
    public int MaxCharges => maxCharges;
    public float ReloadCooldown => reloadCooldown;
    public float ReloadFillTime => reloadFillTime;
    public int ResourceIndex => resourceIndex;
    public float ResourceCost => resourceCost;

    public override void ResetRuntime() {  }

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<ConsumeBoostActionExecutor>()?.Consume(this, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<ConsumeBoostActionExecutor>()?.StopAllBoosts();
}
using UnityEngine;

[CreateAssetMenu(fileName = "DeployTeamCrystalAction", menuName = "ScriptableObjects/Vessel Actions/Deploy Team Crystal")]
public class DeployTeamCrystalActionSO : ShipActionSO
{
    [Header("Setup")]
    [SerializeField] private float forwardOffset = 12f;
    [SerializeField] private float fadeValue = 0.5f;
    [SerializeField] private LayerMask rayMask;

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 30f;

    public float ForwardOffset => forwardOffset;
    public float FadeValue => fadeValue;
    public LayerMask RayMask => rayMask;
    public float Cooldown => cooldown;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<DeployTeamCrystalActionExecutor>()?.Begin(this, Ship, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<DeployTeamCrystalActionExecutor>()?.Commit(this, Ship, ShipStatus);
}
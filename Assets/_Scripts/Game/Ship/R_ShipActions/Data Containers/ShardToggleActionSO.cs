using UnityEngine;

[CreateAssetMenu(fileName = "ShardToggleAction", menuName = "ScriptableObjects/Vessel Actions/Shard Toggle")]
public class ShardToggleActionSO : ShipActionSO
{
    [Header("Mass Centroids Settings")]
    [SerializeField] private Teams team = Teams.Jade;
    [SerializeField] private float searchRadiusHint = 0f; // optional/unused for now

    public Teams Team => team;
    public float SearchRadiusHint => searchRadiusHint;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<ShardToggleActionExecutor>()?.Toggle(this, Ship, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs) { /* no-op */ }
}
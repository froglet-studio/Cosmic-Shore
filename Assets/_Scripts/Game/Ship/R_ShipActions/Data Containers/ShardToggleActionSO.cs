using CosmicShore.Game;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ShardToggleAction", menuName = "ScriptableObjects/Vessel Actions/Shard Toggle")]
public class ShardToggleActionSO : ShipActionSO
{
    [FormerlySerializedAs("team")]
    [Header("Mass Centroids Settings")]
    [SerializeField] private Domains domain = Domains.Jade;
    [SerializeField] private float searchRadiusHint = 0f; // optional/unused for now

    public Domains Domain => domain;
    public float SearchRadiusHint => searchRadiusHint;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<ShardToggleActionExecutor>()?.Toggle(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { /* no-op */ }
}
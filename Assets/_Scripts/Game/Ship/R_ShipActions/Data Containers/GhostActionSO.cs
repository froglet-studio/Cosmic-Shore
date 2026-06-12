using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "GhostAction", menuName = "ScriptableObjects/Vessel Actions/Ghost (Intangibility)")]
public class GhostActionSO : ShipActionSO
{
    [Header("Config")]
    [Tooltip("Maximum time in seconds the vessel stays intangible before colliders re-enable.")]
    [SerializeField] float maxDuration = 3f;

    public float MaxDuration => maxDuration;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GhostActionExecutor>()?.Begin(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GhostActionExecutor>()?.End();
}

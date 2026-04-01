using CosmicShore.App.Systems.Audio;
using UnityEngine;
using CosmicShore.Game;

[CreateAssetMenu(fileName = "ModifyForwardVelocityAction", menuName = "ScriptableObjects/Vessel Actions/Modify Forward Velocity")]
public class ModifyVelocityActionSO
    : ShipActionSO
{
    [SerializeField] float magnitude;
    [SerializeField] float duration;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);
        vesselStatus.VesselTransformer.ModifyVelocity(vesselStatus.Vessel.Transform.forward * magnitude, duration);
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        // No action needed on stop
    }
    
}


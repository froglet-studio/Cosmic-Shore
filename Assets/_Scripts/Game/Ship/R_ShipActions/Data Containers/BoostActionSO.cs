using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "BoostAction", menuName = "ScriptableObjects/Vessel Actions/Boost")]
public class BoostActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus == null) return;
        AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);
        vesselStatus.IsBoosting = true;
        vesselStatus.IsStationary = false;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus == null) return;
        vesselStatus.IsBoosting = false;
    }
}
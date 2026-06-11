using CosmicShore.App.Systems.Audio;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

[CreateAssetMenu(fileName = "DriftAction", menuName = "ScriptableObjects/Vessel Actions/Drift")]
public class DriftActionSO : ShipActionSO
{
    [SerializeField] float Mult = 1.5f;
    [SerializeField] float driftDamping = 0f;
    [SerializeField] bool isSharpDrifting;

    [SerializeField] ScriptableEventNoParam OnDriftingStarted;
    [SerializeField] ScriptableEventNoParam OnDoubleDriftingStarted;
    [SerializeField] ScriptableEventNoParam OnDriftEnded;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var t = vesselStatus.VesselTransformer;
        t.BeginDrift(Mult, driftDamping, isSharpDrifting);
        vesselStatus.IsDrifting = true;

        AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.DriftStart);

        if (isSharpDrifting)
            OnDoubleDriftingStarted.Raise();
        else
            OnDriftingStarted.Raise();
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var t = vesselStatus.VesselTransformer;
        t.EndDrift(isSharpDrifting);
        vesselStatus.IsDrifting = t.IsDriftActive;

        AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.DriftEnd);
        OnDriftEnded.Raise();
    }
}
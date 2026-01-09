using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "ToggleAlignAction", menuName = "ScriptableObjects/Vessel Actions/Toggle Align")]
public sealed class ToggleAlignActionSO : ShipActionSO
{

    [SerializeField] private bool setInitialOnInitialize = false;
    [SerializeField] private bool initialAlignmentEnabled = true;

    public override void Initialize(IVesselStatus vs)
    {
        base.Initialize(vs);
        if (setInitialOnInitialize && vs != null)
            vs.AlignmentEnabled = initialAlignmentEnabled;
    }

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus == null) return;
        vesselStatus.AlignmentEnabled = false;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus == null) return;
        vesselStatus.AlignmentEnabled = true;
    }
}
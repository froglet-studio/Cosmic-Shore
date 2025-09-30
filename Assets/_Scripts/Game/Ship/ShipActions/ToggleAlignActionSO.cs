using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "ToggleAlignAction", menuName = "ScriptableObjects/Vessel Actions/Toggle Align")]
public sealed class ToggleAlignActionSO : ShipActionSO
{

    [SerializeField] private bool setInitialOnInitialize = false;
    [SerializeField] private bool initialAlignmentEnabled = true;

    public override void Initialize(IVessel ship)
    {
        base.Initialize(ship);
        if (setInitialOnInitialize && ShipStatus != null)
            ShipStatus.AlignmentEnabled = initialAlignmentEnabled;
    }

    public override void StartAction(ActionExecutorRegistry execs)
    {
        if (ShipStatus == null) return;
        ShipStatus.AlignmentEnabled = false;
    }

    public override void StopAction(ActionExecutorRegistry execs)
    {
        if (ShipStatus == null) return;
        ShipStatus.AlignmentEnabled = true;
    }
}
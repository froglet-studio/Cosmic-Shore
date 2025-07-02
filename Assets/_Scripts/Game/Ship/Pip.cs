using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utilities;
using UnityEngine;


[RequireComponent(typeof(IShipStatus))]
public class Pip : MonoBehaviour
{
    [SerializeField] Camera pipCamera;
    [SerializeField] bool mirrored;
    [SerializeField]
    PipEventChannelSO OnPipInitializedEventChannel;


    void Start()
    {
        IShipStatus shipStatus = GetComponent<IShipStatus>();


        // TODO - remove GameCanvas dependency
        // if (shipStatus.Player.GameCanvas != null) shipStatus.Player.GameCanvas.MiniGameHUD.SetPipActive(!shipStatus.AIPilot.AutoPilotEnabled, mirrored);
        OnPipInitializedEventChannel?.RaiseEvent(new PipEventData()
        {
            IsActive = !shipStatus.AIPilot.AutoPilotEnabled,
            IsMirrored = mirrored
        });
        
        if (pipCamera != null) pipCamera.gameObject.SetActive(!shipStatus.AIPilot.AutoPilotEnabled);
    }
}

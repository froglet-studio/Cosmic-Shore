using CosmicShore.Core;
using UnityEngine;

[RequireComponent(typeof(ShipStatus))]
public class Pip : MonoBehaviour
{
    [SerializeField] Camera pipCamera;
    [SerializeField] bool mirrored;


    void Start()
    {
        ShipStatus shipStatus = GetComponent<ShipStatus>();
        if (shipStatus.Player.GameCanvas != null) shipStatus.Player.GameCanvas.MiniGameHUD.SetPipActive(!shipStatus.AIPilot.AutoPilotEnabled, mirrored);
        if (pipCamera != null) pipCamera.gameObject.SetActive(!shipStatus.AIPilot.AutoPilotEnabled);
    }
}

using CosmicShore.Core;
using UnityEngine;

public class Pip : MonoBehaviour
{
    [SerializeField] Camera pipCamera;
    [SerializeField] bool mirrored;

    // Start is called before the first frame update
    void Start()
    {
        Ship ship = GetComponent<Ship>();
        if (ship.Player.GameCanvas != null) ship.Player.GameCanvas.MiniGameHUD.SetPipActive(!ship.AIPilot.AutoPilotEnabled, mirrored);
        if (pipCamera != null) pipCamera.gameObject.SetActive(!ship.AIPilot.AutoPilotEnabled);
    }
}

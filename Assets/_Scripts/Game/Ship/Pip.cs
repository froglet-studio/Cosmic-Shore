using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Pip : MonoBehaviour
{
    [SerializeField] Camera pipCamera;
    [SerializeField] bool mirrored;

    // Start is called before the first frame update
    void Start()
    {
        Ship ship = GetComponent<Ship>();
        if (ship.Player.GameCanvas != null) ship.Player.GameCanvas.MiniGameHUD.SetPipActive(!ship.AutoPilot.AutoPilotEnabled, mirrored);
        if (pipCamera != null) pipCamera.gameObject.SetActive(!ship.AutoPilot.AutoPilotEnabled);
    }
}

using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IVesselStatus))]
    public class Pip : MonoBehaviour
    {
        [SerializeField] Camera pipCamera;
        [SerializeField] bool mirrored;

        [SerializeField]
        ScriptableEventPipData _EventPipEventData;


        void Start()
        {
            IVesselStatus vesselStatus = GetComponent<IVesselStatus>();


            // TODO - remove GameCanvas dependency
            // if (vesselStatus.Player.GameCanvas != null) vesselStatus.Player.GameCanvas.MiniGameHUD.SetPipActive(!vesselStatus.AIPilot.AutoPilotEnabled, mirrored);
            _EventPipEventData.Raise(new PipData()
            {
                IsActive = !vesselStatus.AutoPilotEnabled,
                IsMirrored = mirrored
            });

            if (pipCamera != null) pipCamera.gameObject.SetActive(!vesselStatus.AutoPilotEnabled);
        }
    }
}


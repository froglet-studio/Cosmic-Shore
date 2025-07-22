using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShipStatus))]
    public class Pip : MonoBehaviour
    {
        [SerializeField] Camera pipCamera;
        [SerializeField] bool mirrored;

        [SerializeField]
        ScriptableEventPipData _EventPipEventData;


        void Start()
        {
            IShipStatus shipStatus = GetComponent<IShipStatus>();


            // TODO - remove GameCanvas dependency
            // if (shipStatus.Player.GameCanvas != null) shipStatus.Player.GameCanvas.MiniGameHUD.SetPipActive(!shipStatus.AIPilot.AutoPilotEnabled, mirrored);
            _EventPipEventData.Raise(new PipData()
            {
                IsActive = !shipStatus.AutoPilotEnabled,
                IsMirrored = mirrored
            });

            if (pipCamera != null) pipCamera.gameObject.SetActive(!shipStatus.AutoPilotEnabled);
        }
    }
}


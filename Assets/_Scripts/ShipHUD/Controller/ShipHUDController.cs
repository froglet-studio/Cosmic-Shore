using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShipStatus))]
    public class ShipHUDController : MonoBehaviour, IShipHUDController
    {
        [Header("Event Channels")]
        [SerializeField] 
        SilhouetteEventChannelSO onSilhouetteInitialized;

        IShipStatus _shipStatus;
        IShip _ship;
        
        private void Awake()
        {
            _ship = GetComponent<IShip>();
            _shipStatus = GetComponent<IShipStatus>();
        }

        private void OnEnable()
        {
            // onSilhouetteInitialized.OnEventRaised += HandleSilhouetteInitialized;
        }

        private void OnDisable()
        {
            // onSilhouetteInitialized.OnEventRaised -= HandleSilhouetteInitialized;
        }


        public void InitializeShipHUD(ShipTypes shipType)
        {
            if (_shipStatus.AutoPilotEnabled)
            {
                return;
            }

            _shipStatus.ShipHUDView = _shipStatus.ShipHUDContainer.InitializeView(this, shipType);
        }

        public void OnButtonPressed(int buttonNumber)
        {
            _ship.PerformButtonActions(buttonNumber);
        }

        private void HandleSilhouetteInitialized(SilhouetteData data)
        {
            var sil = _shipStatus.ShipHUDView.GetSilhouetteContainer();
            var trail = _shipStatus.ShipHUDView.GetTrailContainer();

            sil.gameObject.SetActive(data.IsSilhouetteActive);
            trail.gameObject.SetActive(data.IsTrailDisplayActive);

            foreach (var part in data.Silhouettes)
            {
                part.transform.SetParent(sil.transform, false);
                part.SetActive(true);
            }
            data.Sender.SetSilhouetteReference(sil.transform, trail.transform);
        }
    }
}

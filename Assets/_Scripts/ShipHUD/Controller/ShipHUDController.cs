using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore
{
    [RequireComponent(typeof(IShipStatus))]
    public class ShipHUDController : MonoBehaviour, IShipHUDController
    {
        /*[SerializeField, RequireInterface(typeof(IShipHUDView))] 
        MonoBehaviour shipHUD;
        
        [SerializeField, RequireInterface(typeof(IShip))] 
        MonoBehaviour _shipController;
        
        [SerializeField, RequireInterface(typeof(IShipStatus))] 
        MonoBehaviour _shipInstance;*/

        [SerializeField]
        ShipHUDContainer _shipHUDContainer;

        [Header("Event Channels")]
        [SerializeField] 
        SilhouetteEventChannelSO onSilhouetteInitialized;

        IShipHUDView _shipHUDView;
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


        public void InitializeShipHUD(ShipTypes _shipType)
        {
            if (_shipStatus.AutoPilotEnabled)
            {
                return;
            }

            _shipHUDView = _shipHUDContainer.Show(_shipType);
        }

        public void OnButtonPressed(int buttonNumber)
        {
            _ship.PerformButtonActions(buttonNumber);
        }

        private void HandleSilhouetteInitialized(SilhouetteData data)
        {
            var sil = _shipHUDView.GetSilhouetteContainer();
            var trail = _shipHUDView.GetTrailContainer();

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

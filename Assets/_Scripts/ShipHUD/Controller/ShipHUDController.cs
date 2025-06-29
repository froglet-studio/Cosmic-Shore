using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore
{
    [RequireComponent(typeof(IShipStatus))]
    public class ShipHUDController : MonoBehaviour, IShipHUDController
    {
        [SerializeField] internal GameObject shipHUD;

        [Header("Event Channels")]
        [SerializeField] private SilhouetteEventChannelSO onSilhouetteInitialized;

        public IShipHUDView ShipHUDView { get; private set; }

        public IShipStatus ShipStatusInstance { get; private set; }
        [SerializeField] private IShip _shipController;

        private void OnEnable()
        {
            onSilhouetteInitialized.OnEventRaised += HandleSilhouetteInitialized;
        }

        private void OnDisable()
        {
            onSilhouetteInitialized.OnEventRaised -= HandleSilhouetteInitialized;
        }

        private void Awake()
        {
            ShipStatusInstance = GetComponent<ShipStatus>();
        }

        public void InitializeShipHUD(ShipTypes _shipType)
        {
            if (ShipStatusInstance.AutoPilotEnabled)
            {
                return;
            }

            if (shipHUD != null)
            {
                shipHUD.TryGetComponent(out ShipHUDContainer container);
                var hudView = container.Show(_shipType);
                hudView?.Initialize(this);
                ShipHUDView = hudView;
            }
        }

        public void OnButtonPressed(int buttonNumber)
        {
            _shipController.PerformButtonActions(buttonNumber);
        }

        private void HandleSilhouetteInitialized(SilhouetteData data)
        {
            var sil = ShipHUDView.GetSilhouetteContainer();
            var trail = ShipHUDView.GetTrailContainer();

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

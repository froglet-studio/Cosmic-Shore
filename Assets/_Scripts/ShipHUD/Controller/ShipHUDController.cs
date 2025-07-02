using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [SerializeField] private ConsumeBoostAction _boostAction;

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
            Debug.Log(_shipStatus.AutoPilotEnabled);

            if (!_shipStatus.AutoPilotEnabled)
            {
                if (SceneManager.GetActiveScene().name == "Main_Menu") return;
                _shipStatus.ShipHUDView = _shipStatus.ShipHUDContainer.InitializeView(this, shipType);

                InitializeBoostAction();
            }
        }


        public void InitializeBoostAction()
        {
            if (_boostAction != null && _shipStatus.ShipHUDView != null)
            {
                // Animate drain
                _boostAction.OnBoostStarted += (duration, startAmount) =>
                    _shipStatus.ShipHUDView.AnimateBoostFillDown(
                        _boostAction.ResourceIndex,   
                        duration,                     
                        startAmount                   
                    );

                _boostAction.OnBoostEnded += () =>
                    _shipStatus.ShipHUDView.AnimateBoostFillUp(
                        _boostAction.ResourceIndex,   
                        _boostAction.BoostDuration,   
                        1f                            
                    );
            }
            else
            {
                Debug.LogWarning("BoostAction or HUDView missing on ship!");
            }
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

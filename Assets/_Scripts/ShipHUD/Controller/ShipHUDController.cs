using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShipStatus))]
    public class ShipHUDController : MonoBehaviour, IShipHUDController
    {
        [SerializeField] private ShipClassType shipType;
        
        [Header("Event Channels")]
        [SerializeField] 
        SilhouetteEventChannelSO onSilhouetteInitialized;

        IShipStatus _shipStatus;
        IShip _ship;

        //serpent 
        [SerializeField] private ConsumeBoostAction _boostAction;
        [SerializeField] private SeedAssemblerAction _seedAssemblerAction;
        
        //sparrow
        [SerializeField] private OverheatingAction _overheatingAction;
        [SerializeField] private FullAutoAction _fullAutoAction;
        [SerializeField] private FireGunAction _fireGunAction;
        [SerializeField] private ToggleStationaryModeAction _stationaryModeAction;
        

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


        public void InitializeShipHUD(ShipClassType shipType)
        {
            Debug.Log(_shipStatus.AutoPilotEnabled);

            if (!_shipStatus.AutoPilotEnabled)
            {
                if (SceneManager.GetActiveScene().name == "Main_Menu") return;
                _shipStatus.ShipHUDView = _shipStatus.ShipHUDContainer.InitializeView(this, shipType);

                // InitializeBoostAction();
                
                SubscribeBoostAction();
                SubscribeSeedAssemblerAction();
                SubscribeOverheatingAction();
                SubscribeFullAutoAction();
                SubscribeFireGunAction();
                SubscribeStationaryModeAction();
            }
        }

        private void SubscribeBoostAction()
        {
            if (_boostAction == null) return;
            var view = _shipStatus.ShipHUDView;
            _boostAction.OnBoostStarted += (dur, amt) =>
                view.AnimateBoostFillDown(_boostAction.ResourceIndex, dur, amt);
            _boostAction.OnBoostEnded   += () =>
                view.AnimateBoostFillUp(_boostAction.ResourceIndex, _boostAction.BoostDuration, 1f);
        }

        private void SubscribeSeedAssemblerAction()
        {
            if (_seedAssemblerAction == null) return;
            var view = _shipStatus.ShipHUDView;
            _seedAssemblerAction.OnAssembleCompleted   += () => view.OnSeedAssembleStarted();
            _seedAssemblerAction.OnAssembleCompleted += () => view.OnSeedAssembleCompleted();
        }

        private void SubscribeOverheatingAction()
        {
            if (_overheatingAction == null) return;
            var view = _shipStatus.ShipHUDView;
            _overheatingAction.OnHeatBuildStarted += () => view.OnOverheatBuildStarted();
            _overheatingAction.OnOverheated        += () => view.OnOverheated();
            _overheatingAction.OnHeatDecayCompleted += () => view.OnHeatDecayCompleted();
        }

        private void SubscribeFullAutoAction()
        {
            if (_fullAutoAction == null) return;
            var view = _shipStatus.ShipHUDView;
            _fullAutoAction.OnFullAutoStarted += () => view.OnFullAutoStarted();
            _fullAutoAction.OnFullAutoStopped += () => view.OnFullAutoStopped();
        }

        private void SubscribeFireGunAction()
        {
            if (_fireGunAction == null) return;
            var view = _shipStatus.ShipHUDView;
            _fireGunAction.OnGunFired += () => view.OnFireGunFired();
        }

        private void SubscribeStationaryModeAction()
        {
            if (_stationaryModeAction == null) return;
            var view = _shipStatus.ShipHUDView;
            _stationaryModeAction.OnStationaryToggled += isOn => view.OnStationaryToggled(isOn);
        }

        public void OnButtonPressed(int buttonNumber)
        {
            Debug.Log($"[ShipHUDController] OnButtonPressed({buttonNumber}) called!");
            _ship.PerformButtonActions(buttonNumber);
            
            if (_shipStatus.ShipHUDView != null)
                _shipStatus.ShipHUDView.OnInputPressed(buttonNumber);
        }
        
        public void OnButtonReleased(int buttonNumber)
        {
            Debug.Log($"[ShipHUDController] OnButtonReleased({buttonNumber}) called!");
            if (_shipStatus.ShipHUDView != null)
                _shipStatus.ShipHUDView.OnInputReleased(buttonNumber);
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

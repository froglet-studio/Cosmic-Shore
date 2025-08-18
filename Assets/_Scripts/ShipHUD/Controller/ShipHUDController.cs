using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShipStatus))]
    public class ShipHUDController : MonoBehaviour, IShipHUDController
    {
        [SerializeField] private ShipClassType shipType;
        
        // [Header("Event Channels")]
        // [SerializeField] private SilhouetteEventChannelSO onSilhouetteInitialized;

        private IShipStatus _shipStatus;
        private IShip _ship;

        //dolphin
        [SerializeField] private ChargeBoostAction chargeBoostAction;
        
        //serpent 
        [SerializeField] private ConsumeBoostAction boostAction;
        [SerializeField] private SeedAssemblerAction seedAssemblerAction;
        
        //sparrow
        [SerializeField] private OverheatingAction overheatingAction;
        [SerializeField] private FullAutoAction fullAutoAction;
        [SerializeField] private FireGunAction fireGunAction;
        [SerializeField] private ToggleStationaryModeAction stationaryModeAction;
        
        private System.Action<float> _chargeStartedHandler;
        private System.Action<float> _chargeProgressHandler;
        private System.Action        _chargeEndedHandler;
        private System.Action<float> _dischargeStartedHandler;
        private System.Action<float> _dischargeProgressHandler;
        private System.Action        _dischargeEndedHandler;
        
        private System.Action<float,float> _boostStartedHandler;
        private System.Action _boostEndedHandler;

        private System.Action _seedStartedHandler;
        private System.Action _seedCompletedHandler;

        private System.Action _heatBuildStartedHandler;
        private System.Action _overheatedHandler;
        private System.Action _heatDecayCompletedHandler;

        private System.Action _fullAutoStartedHandler;
        private System.Action _fullAutoStoppedHandler;

        private System.Action _gunFiredHandler;

        private System.Action<bool> _stationaryToggledHandler;
        
        [SerializeField] private bool verboseHUDLogs = true;
        private void HUDLog(string m) { if (verboseHUDLogs) Debug.Log($"[ShipHUDController] {m}", this); }

        // Small helper
        private IHUDEffects Effects
            => (_shipStatus?.ShipHUDView as ShipHUDView)?.Effects;

        private void Awake()
        {
            _ship = GetComponent<IShip>();
            _shipStatus = GetComponent<IShipStatus>();
        }

        private void OnEnable()
        {
            // onSilhouetteInitialized.OnEventRaised += HandleSilhouetteInitialized;
        }


        public void InitializeShipHUD(ShipClassType shipType)
        {
            if (_shipStatus.AutoPilotEnabled) return;
            if (SceneManager.GetActiveScene().name == "Menu_Main") return;

            // If a HUD already exists, clear it first
            if (_shipStatus.ShipHUDView != null)
            {
                // best-effort unsubscribe from old actions
                UnsubscribeBoostAction();
                UnsubscribeSeedAssemblerAction();
                UnsubscribeOverheatingAction();
                UnsubscribeFullAutoAction();
                UnsubscribeFireGunAction();
                UnsubscribeStationaryModeAction();

                var oldViewMb = _shipStatus.ShipHUDView as MonoBehaviour;
                if (oldViewMb != null && oldViewMb.gameObject)
                    Destroy(oldViewMb.gameObject);
                _shipStatus.ShipHUDView = null;
            }

            var view = _shipStatus.ShipHUDContainer.InitializeView(this, shipType);
            if (view == null)
            {
                Debug.LogError($"[ShipHUDController] Failed to initialize HUD for {shipType}");
                return;
            }
            _shipStatus.ShipHUDView = view;
            
            SubscribeToChargeBoostAction();
            SubscribeBoostAction();
            SubscribeSeedAssemblerAction();   
            SubscribeOverheatingAction();
            SubscribeFullAutoAction();
            SubscribeFireGunAction();
            SubscribeStationaryModeAction();
        }
        
        private void OnDisable()
        {
            // onSilhouetteInitialized.OnEventRaised -= HandleSilhouetteInitialized;
            UnsubscribeFromChargeBoostAction();
            UnsubscribeBoostAction();
            UnsubscribeSeedAssemblerAction();
            UnsubscribeOverheatingAction();
            UnsubscribeFullAutoAction();
            UnsubscribeFireGunAction();
            UnsubscribeStationaryModeAction();
        }


        private void SubscribeToChargeBoostAction()
        {
            if (chargeBoostAction == null || Effects == null)
            {
                HUDLog("ChargeBoostAction or Effects missing; skipping subscription.");
                return;
            }

            int idx = chargeBoostAction.BoostResourceIndex;
            float Max() => Mathf.Max(0.0001f, chargeBoostAction.MaxChargeUnits);
            float N(float units) => Mathf.Clamp01(units / Max());

            _chargeStartedHandler = (fromUnits) =>
            {
                HUDLog($"ChargeStarted from={fromUnits:F3}");
                Effects.SetToggle("ChargeBoosting", true);
                Effects.SetMeter(idx, N(fromUnits));      
            };

            _chargeProgressHandler = (units) =>
            {
                Effects.SetMeter(idx, N(units));         
            };

            _chargeEndedHandler = () =>
            {
                // Only top off visually if it actually ended at full.
                Effects.AnimateRefill(idx, 0.1f, 1f);
            };

            _dischargeStartedHandler = (fromUnits) =>
            {
                HUDLog($"DischargeStarted from={fromUnits:F3}");
                Effects.SetToggle("ChargeBoosting", false);
                Effects.SetMeter(idx, N(fromUnits));      
            };

            _dischargeProgressHandler = (units) =>
            {
                Effects.SetMeter(idx, N(units));          
            };

            _dischargeEndedHandler = () =>
            {
                Effects.AnimateDrain(idx, 0.1f, 0f);   
            };
            
            chargeBoostAction.OnChargeStarted     += _chargeStartedHandler;
            chargeBoostAction.OnChargeProgress    += _chargeProgressHandler;
            chargeBoostAction.OnChargeEnded       += _chargeEndedHandler;
            chargeBoostAction.OnDischargeStarted  += _dischargeStartedHandler;
            chargeBoostAction.OnDischargeProgress += _dischargeProgressHandler;
            chargeBoostAction.OnDischargeEnded    += _dischargeEndedHandler;
        }

        private void UnsubscribeFromChargeBoostAction()
        {
            if (chargeBoostAction == null) return;
            HUDLog("Unsubscribing ChargeBoostAction");
            if (_chargeStartedHandler     != null) chargeBoostAction.OnChargeStarted     -= _chargeStartedHandler;
            if (_chargeProgressHandler    != null) chargeBoostAction.OnChargeProgress    -= _chargeProgressHandler;
            if (_chargeEndedHandler       != null) chargeBoostAction.OnChargeEnded       -= _chargeEndedHandler;
            if (_dischargeStartedHandler  != null) chargeBoostAction.OnDischargeStarted  -= _dischargeStartedHandler;
            if (_dischargeProgressHandler != null) chargeBoostAction.OnDischargeProgress -= _dischargeProgressHandler;
            if (_dischargeEndedHandler    != null) chargeBoostAction.OnDischargeEnded    -= _dischargeEndedHandler;

            _chargeStartedHandler = null;
            _chargeProgressHandler = null;
            _chargeEndedHandler = null;
            _dischargeStartedHandler = null;
            _dischargeProgressHandler = null;
            _dischargeEndedHandler = null;
        }

        private void SubscribeBoostAction()
        {
            if (boostAction == null || Effects == null) return;

            _boostStartedHandler = (dur, from) =>
                Effects.AnimateDrain(boostAction.ResourceIndex, dur, from);

            _boostEndedHandler = () =>
                Effects.AnimateRefill(boostAction.ResourceIndex, boostAction.BoostDuration, 1f);

            boostAction.OnBoostStarted += _boostStartedHandler;
            boostAction.OnBoostEnded   += _boostEndedHandler;
        }
        
        private void UnsubscribeBoostAction()
        {
            if (boostAction == null) return;
            if (_boostStartedHandler != null) { boostAction.OnBoostStarted -= _boostStartedHandler; _boostStartedHandler = null; }

            if (_boostEndedHandler == null) return;
            boostAction.OnBoostEnded   -= _boostEndedHandler;   _boostEndedHandler   = null;
        }

        private void SubscribeSeedAssemblerAction()
        {
            if (seedAssemblerAction == null || Effects == null) return;

            _seedStartedHandler   = () => Effects.SetToggle("SeedAssembling", true);
            _seedCompletedHandler = () => Effects.SetToggle("SeedAssembling", false);

            seedAssemblerAction.OnAssembleStarted   += _seedStartedHandler;
            seedAssemblerAction.OnAssembleCompleted += _seedCompletedHandler;
        }
        private void UnsubscribeSeedAssemblerAction()
        {
            if (seedAssemblerAction == null) return;
            if (_seedStartedHandler   != null) { seedAssemblerAction.OnAssembleStarted   -= _seedStartedHandler;   _seedStartedHandler   = null; }

            if (_seedCompletedHandler == null) return;
            seedAssemblerAction.OnAssembleCompleted -= _seedCompletedHandler; _seedCompletedHandler = null;
        }

        private void SubscribeOverheatingAction()
        {
            if (overheatingAction == null || Effects == null) return;

            _heatBuildStartedHandler   = () => Effects.SetToggle("HeatWarning", true);
            _overheatedHandler         = () => Effects.TriggerAnim("Overheated");
            _heatDecayCompletedHandler = () => { Effects.SetToggle("HeatWarning", false); /* optional meter refill here */ };

            overheatingAction.OnHeatBuildStarted  += _heatBuildStartedHandler;
            overheatingAction.OnOverheated        += _overheatedHandler;
            overheatingAction.OnHeatDecayCompleted+= _heatDecayCompletedHandler;
        }
        private void UnsubscribeOverheatingAction()
        {
            if (overheatingAction == null) return;
            if (_heatBuildStartedHandler   != null) { overheatingAction.OnHeatBuildStarted   -= _heatBuildStartedHandler;   _heatBuildStartedHandler   = null; }
            if (_overheatedHandler         != null) { overheatingAction.OnOverheated         -= _overheatedHandler;         _overheatedHandler         = null; }

            if (_heatDecayCompletedHandler == null) return;
            overheatingAction.OnHeatDecayCompleted -= _heatDecayCompletedHandler; _heatDecayCompletedHandler = null;
        }

        private void SubscribeFullAutoAction()
        {
            if (fullAutoAction == null || Effects == null) return;

            _fullAutoStartedHandler = () => Effects.SetToggle("FullAutoIcon", true);
            _fullAutoStoppedHandler = () => Effects.SetToggle("FullAutoIcon", false);

            fullAutoAction.OnFullAutoStarted += _fullAutoStartedHandler;
            fullAutoAction.OnFullAutoStopped += _fullAutoStoppedHandler;
        }
        private void UnsubscribeFullAutoAction()
        {
            if (fullAutoAction == null) return;
            if (_fullAutoStartedHandler != null) { fullAutoAction.OnFullAutoStarted -= _fullAutoStartedHandler; _fullAutoStartedHandler = null; }

            if (_fullAutoStoppedHandler == null) return;
            fullAutoAction.OnFullAutoStopped -= _fullAutoStoppedHandler; _fullAutoStoppedHandler = null;
        }

        private void SubscribeFireGunAction()
        {
            if (fireGunAction == null || Effects == null) return;

            _gunFiredHandler = () => Effects.TriggerAnim("GunFired");
            fireGunAction.OnGunFired += _gunFiredHandler;
        }
        private void UnsubscribeFireGunAction()
        {
            if (fireGunAction == null) return;
            if (_gunFiredHandler == null) return;
            fireGunAction.OnGunFired -= _gunFiredHandler; _gunFiredHandler = null;
        }

        private void SubscribeStationaryModeAction()
        {
            if (stationaryModeAction == null || Effects == null) return;

            _stationaryToggledHandler = on => Effects.SetToggle("Stationary", on);
            stationaryModeAction.OnStationaryToggled += _stationaryToggledHandler;
        }
        private void UnsubscribeStationaryModeAction()
        {
            if (stationaryModeAction == null) return;
            if (_stationaryToggledHandler == null) return;
            stationaryModeAction.OnStationaryToggled -= _stationaryToggledHandler; _stationaryToggledHandler = null;
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

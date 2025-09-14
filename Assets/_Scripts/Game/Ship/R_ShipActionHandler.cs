using CosmicShore.Core;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Component responsible for mapping input and resource events to
    /// vessel actions.  This logic previously lived inside the Vessel classes.
    /// </summary>
    public class R_ShipActionHandler : MonoBehaviour
    {
        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;

        [SerializeField]
        ScriptableEventInputEvents _onButtonPressed;
        
        [SerializeField]
        ScriptableEventInputEvents _onButtonReleased;
        
        [SerializeField]
        ScriptableEventAbilityStats onAbilityExecuted;
        
        readonly Dictionary<InputEvents, List<ShipAction>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipAction>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();
        
        public event Action<InputEvents> OnInputEventStarted;
        public event Action<InputEvents> OnInputEventStopped;

        IVesselStatus vesselStatus;

        void SubscribeEvents()
        {
            _onButtonPressed.OnRaised += PerformShipControllerActions;
            _onButtonReleased.OnRaised += StopShipControllerActions;
        }

        void OnDestroy()
        {
            _onButtonPressed.OnRaised -= PerformShipControllerActions;
            _onButtonReleased.OnRaised -= StopShipControllerActions;
        }

        public void Initialize(IVesselStatus vesselStatus)
        {
            this.vesselStatus = vesselStatus;
            ShipHelper.InitializeShipControlActions(vesselStatus, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(vesselStatus, _resourceEventClassActions, _classResourceActions);

            SubscribeEvents();
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            if (!HasAction(controlType))
                return;
            ShipHelper.PerformShipControllerActions(controlType, _inputAbilityStartTimes, _shipControlActions);
            OnInputEventStarted?.Invoke(controlType);
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (!HasAction(controlType))
                return;
            
            onAbilityExecuted.Raise(new AbilityStats
            {
                PlayerName = vesselStatus.PlayerName,
                ControlType = controlType,
                Duration = Time.time - _inputAbilityStartTimes[controlType]
            });
            
            /*if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(VesselStatus.Team, VesselStatus.Player.Name, controlType,
                    Time.time - _inputAbilityStartTimes[controlType]);*/

            ShipHelper.StopShipControllerActions(controlType, _shipControlActions);
            OnInputEventStopped?.Invoke(controlType);
        }

        public bool HasAction(InputEvents inputEvent) => _shipControlActions.ContainsKey(inputEvent);
    }

    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipAction> ShipActions;
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipAction> ClassActions;
    }
}

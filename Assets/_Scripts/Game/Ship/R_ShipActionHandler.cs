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
    /// ship actions.  This logic previously lived inside the Ship classes.
    /// </summary>
    public class R_ShipActionHandler : MonoBehaviour
    {
        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;

        [SerializeField]
        ScriptableEventInputEvents _onButtonPressed;
        
        [SerializeField]
        ScriptableEventInputEvents _onButtonReleased;
        
        readonly Dictionary<InputEvents, List<ShipAction>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipAction>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();
        
        public event Action<InputEvents> OnInputEventStarted;
        public event Action<InputEvents> OnInputEventStopped;

        IShipStatus _shipStatus;

        public void SubscribeEvents()
        {
            _onButtonPressed.OnRaised += PerformShipControllerActions;
            _onButtonReleased.OnRaised += StopShipControllerActions;
        }

        public void UnsubscribeEvents()
        {
            _onButtonPressed.OnRaised -= PerformShipControllerActions;
            _onButtonReleased.OnRaised -= StopShipControllerActions;
        }

        public void Initialize(IShipStatus shipStatus)
        {
            _shipStatus = shipStatus;
            ShipHelper.InitializeShipControlActions(shipStatus, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(shipStatus, _resourceEventClassActions, _classResourceActions);
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
            
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(_shipStatus.Team, _shipStatus.Player.PlayerName, controlType,
                    Time.time - _inputAbilityStartTimes[controlType]);

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

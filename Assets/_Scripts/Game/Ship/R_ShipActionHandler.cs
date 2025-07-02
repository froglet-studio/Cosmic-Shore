using CosmicShore.Core;
using CosmicShore.Utilities;
using System.Collections.Generic;
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

        [SerializeField] InputEventsEventChannelSO OnButton1Pressed;
        [SerializeField] InputEventsEventChannelSO OnButton1Released;
        [SerializeField] InputEventsEventChannelSO OnButton2Pressed;
        [SerializeField] InputEventsEventChannelSO OnButton2Released;
        [SerializeField] InputEventsEventChannelSO OnButton3Pressed;
        [SerializeField] InputEventsEventChannelSO OnButton3Released;
        
        readonly Dictionary<InputEvents, List<ShipAction>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipAction>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();

        IShipStatus _shipStatus;

        public void SubscribeEvents()
        {
            OnButton1Pressed.OnEventRaised += PerformShipControllerActions;
            OnButton1Released.OnEventRaised += StopShipControllerActions;
            OnButton2Pressed.OnEventRaised += PerformShipControllerActions;
            OnButton2Released.OnEventRaised += StopShipControllerActions;
            OnButton3Pressed.OnEventRaised += PerformShipControllerActions;
            OnButton3Released.OnEventRaised += StopShipControllerActions;
        }

        public void UnsubscribeEvents()
        {
            OnButton1Pressed.OnEventRaised -= PerformShipControllerActions;
            OnButton1Released.OnEventRaised -= StopShipControllerActions;
            OnButton2Pressed.OnEventRaised -= PerformShipControllerActions;
            OnButton2Released.OnEventRaised -= StopShipControllerActions;
            OnButton3Pressed.OnEventRaised -= PerformShipControllerActions;
            OnButton3Released.OnEventRaised -= StopShipControllerActions;
        }

        public void Initialize(IShipStatus shipStatus)
        {
            _shipStatus = shipStatus;
            ShipHelper.InitializeShipControlActions(shipStatus, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(shipStatus, _resourceEventClassActions, _classResourceActions);
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            ShipHelper.PerformShipControllerActions(controlType, _inputAbilityStartTimes, _shipControlActions);
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(_shipStatus.Team, _shipStatus.Player.PlayerName, controlType,
                    Time.time - _inputAbilityStartTimes[controlType]);

            ShipHelper.StopShipControllerActions(controlType, _shipControlActions);
        }

        public bool HasAction(InputEvents inputEvent) => _shipControlActions.ContainsKey(inputEvent);
    }
}

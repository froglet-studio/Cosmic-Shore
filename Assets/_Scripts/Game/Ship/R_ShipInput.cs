using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Models.Enums;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Handles input mappings and event subscriptions for ships.
    /// </summary>
    public class R_ShipInput : MonoBehaviour
    {
        [Header("Event Channels")]
        [SerializeField] BoolEventChannelSO onBottomEdgeButtonsEnabled;
        [SerializeField] InputEventsEventChannelSO OnButton1Pressed;
        [SerializeField] InputEventsEventChannelSO OnButton1Released;
        [SerializeField] InputEventsEventChannelSO OnButton2Pressed;
        [SerializeField] InputEventsEventChannelSO OnButton2Released;
        [SerializeField] InputEventsEventChannelSO OnButton3Pressed;
        [SerializeField] InputEventsEventChannelSO OnButton3Released;

        [Header("Input Mappings")]
        [SerializeField] List<InputEventShipActionMapping> inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> resourceEventClassActions;

        readonly Dictionary<InputEvents, List<ShipAction>> shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipAction>> classResourceActions = new();
        readonly Dictionary<InputEvents, float> inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> resourceAbilityStartTimes = new();

        R_ShipBase ship;

        public void Initialize(R_ShipBase shipBase)
        {
            ship = shipBase;
            ShipHelper.InitializeShipControlActions(ship, inputEventShipActions, shipControlActions);
            ShipHelper.InitializeClassResourceActions(ship, resourceEventClassActions, classResourceActions);
        }

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

        public bool HasAction(InputEvents inputEvent) => shipControlActions.ContainsKey(inputEvent);

        public void PerformShipControllerActions(InputEvents @event)
        {
            inputAbilityStartTimes[@event] = Time.time;
            if (ship.actionHandler != null)
            {
                ship.actionHandler.Perform(@event);
                return;
            }
            ShipHelper.PerformShipControllerActions(@event, out _, shipControlActions);
        }

        public void StopShipControllerActions(InputEvents @event)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName, @event,
                    Time.time - inputAbilityStartTimes[@event]);

            if (ship.actionHandler != null)
            {
                ship.actionHandler.Stop(@event);
                return;
            }
            ShipHelper.StopShipControllerActions(@event, shipControlActions);
        }

        public void PerformClassResourceActions(ResourceEvents resourceEvent)
        {
            resourceAbilityStartTimes[resourceEvent] = Time.time;
            if (!classResourceActions.TryGetValue(resourceEvent, out var actions)) return;
            foreach (var action in actions)
                action.StartAction();
        }

        public void StopClassResourceActions(ResourceEvents resourceEvent)
        {
            if (!classResourceActions.TryGetValue(resourceEvent, out var actions)) return;
            foreach (var action in actions)
                action.StopAction();
        }
    }
}

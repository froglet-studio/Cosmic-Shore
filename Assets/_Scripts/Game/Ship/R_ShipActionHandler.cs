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
        [Header("Executors (one registry on this ship)")]
        [SerializeField] ActionExecutorRegistry _executors;   
        [Header("Action mappings (SO assets)")]
        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;

        [Header("Scriptable events")]
        [SerializeField] ScriptableEventInputEvents _onButtonPressed;
        [SerializeField] ScriptableEventInputEvents _onButtonReleased;
        [SerializeField] ScriptableEventAbilityStats onAbilityExecuted;

        // Runtime dictionaries now use ShipActionSO (assets; no Instantiate)
        readonly Dictionary<InputEvents, List<ShipActionSO>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipActionSO>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();

        public event Action<InputEvents> OnInputEventStarted;
        public event Action<InputEvents> OnInputEventStopped;

        IVesselStatus vesselStatus;

        void SubscribeEvents()
        {
            if (_onButtonPressed != null)  _onButtonPressed.OnRaised  += PerformShipControllerActions;
            if (_onButtonReleased != null) _onButtonReleased.OnRaised += StopShipControllerActions;
        }

        void OnDestroy()
        {
            if (_onButtonPressed != null)  _onButtonPressed.OnRaised  -= PerformShipControllerActions;
            if (_onButtonReleased != null) _onButtonReleased.OnRaised -= StopShipControllerActions;
        }

        public void Initialize(IVesselStatus v)
        {
            vesselStatus = v;

            SubscribeEvents();

            if (_executors != null)
                _executors.InitializeAll(vesselStatus);
            else
                Debug.LogWarning("[R_ShipActionHandler] ActionExecutorRegistry is not assigned.");

            ShipHelper.InitializeShipControlActions(vesselStatus, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(vesselStatus, _resourceEventClassActions, _classResourceActions);
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            if (!HasAction(controlType))
                return;

            _inputAbilityStartTimes[controlType] = Time.time;

            var actions = _shipControlActions[controlType];
            for (int i = 0; i < actions.Count; i++)
                actions[i].StartAction(_executors);   // <-- pass the registry to the SO

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

            var actions = _shipControlActions[controlType];
            for (int i = 0; i < actions.Count; i++)
                actions[i].StopAction(_executors);    // <-- pass the registry to the SO

            OnInputEventStopped?.Invoke(controlType);
        }

        public bool HasAction(InputEvents inputEvent)
            => _shipControlActions.TryGetValue(inputEvent, out var list) && list != null && list.Count > 0;
    }

    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipActionSO> ShipActions;   // <-- use SO assets
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipActionSO> ClassActions;  // <-- use SO assets
    }
}

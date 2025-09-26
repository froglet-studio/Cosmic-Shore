using CosmicShore.Core;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using CosmicShore.SOAP;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Component responsible for mapping input and resource events to
    /// vessel actions.  This logic previously lived inside the Vessel classes.
    /// </summary>
    public class R_VesselActionHandler : NetworkBehaviour
    {
        [Header("Executors")]
        [SerializeField] ActionExecutorRegistry _executors;   
        [Header("Action mappings")]
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

        void SubscribeToInputEvents()
        {
            _onButtonPressed.OnRaised  += OnButtonPressed;
            _onButtonReleased.OnRaised += OnButtonReleased;
        }

        void UnsubscribeFromInputEvents()
        {
            _onButtonPressed.OnRaised  -= OnButtonPressed;
            _onButtonReleased.OnRaised -= OnButtonReleased;
        }

        void OnDisable()
        {
            UnsubscribeFromInputEvents();
        }

        public void Initialize(IVesselStatus v, bool subscribeToInputEvents)
        {
            vesselStatus = v;

            if (subscribeToInputEvents)
                SubscribeToInputEvents();

            if (_executors)
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
            foreach (var t in actions)
                t.StartAction(_executors);

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

        bool HasAction(InputEvents inputEvent)
            => _shipControlActions.TryGetValue(inputEvent, out var list) && list != null && list.Count > 0;
        
        void OnButtonPressed(InputEvents ie)
        {
            // Skip if autopilot
            if (vesselStatus.AutoPilotEnabled)
                return;

            if (IsSpawned)
            {
                if (IsOwner)
                    SendButtonPressed_ServerRpc(ie); // Only owner can send
                return; // Non-host clients do nothing directly
            }

            // Singleplayer
            PerformShipControllerActions(ie);
        }
        
        [ServerRpc]
        private void SendButtonPressed_ServerRpc(InputEvents ie, ServerRpcParams rpcParams = default)
        {
            // Server rebroadcasts to everyone
            OnButtonPressedClientRpc(ie); 
        }
        
        [ClientRpc] 
        void OnButtonPressedClientRpc(InputEvents ie) => 
            PerformShipControllerActions(ie);
        
        void OnButtonReleased(InputEvents ie)
        {
            // Skip if autopilot
            if (vesselStatus.AutoPilotEnabled)
                return;

            if (IsSpawned)
            {
                if (IsOwner)
                    SendButtonReleased_ServerRpc(ie);
                return; // Non-host clients do nothing directly
            }

            // Singleplayer
            StopShipControllerActions(ie);
        }
        
        [ServerRpc]
        private void SendButtonReleased_ServerRpc(InputEvents ie, ServerRpcParams rpcParams = default)
        {
            // Server rebroadcasts to everyone
            OnButtonReleased_ClientRpc(ie); 
        }
    
        [ClientRpc]
        void OnButtonReleased_ClientRpc(InputEvents ie) =>
            StopShipControllerActions(ie);
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

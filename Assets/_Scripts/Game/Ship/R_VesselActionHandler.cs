using CosmicShore.Core;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using CosmicShore.SOAP;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
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

        readonly Dictionary<InputEvents, List<ShipActionSO>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipActionSO>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();

        
        // TODO - Unnecessary events added.
        // Remove and Use _onButtonPressed and _onButtonReleased.
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
            if (IsSpawned)
                return;
            
            UnsubscribeFromInputEvents();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
                return;

            UnsubscribeFromInputEvents();
        }

        public void ToggleSubscription(bool subscribe)
        {
            if (subscribe) SubscribeToInputEvents();
            else           UnsubscribeFromInputEvents();
        }

        public void Initialize(IVesselStatus v)
        {
            vesselStatus = v;

            if (_executors)
                _executors.InitializeAll(vesselStatus);
            else
                Debug.LogWarning("[R_ShipActionHandler] ActionExecutorRegistry is not assigned.");

            ShipHelper.InitializeShipControlActions(vesselStatus, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(vesselStatus, _resourceEventClassActions, _classResourceActions);
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            if (!HasAction(controlType)) return;

            _inputAbilityStartTimes[controlType] = Time.time;
            var actions = _shipControlActions[controlType];
            foreach (var t in actions)
                t.StartAction(_executors);
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (!HasAction(controlType)) return;

            float duration = 0f;
            if (_inputAbilityStartTimes.TryGetValue(controlType, out var start))
                duration = Time.time - start;

            onAbilityExecuted.Raise(new AbilityStats
            {
                PlayerName = vesselStatus.PlayerName,
                ControlType = controlType,
                Duration = duration
            });

            var actions = _shipControlActions[controlType];
            for (int i = 0; i < actions.Count; i++)
                actions[i].StopAction(_executors);
        }

        bool HasAction(InputEvents inputEvent) =>
            _shipControlActions.TryGetValue(inputEvent, out var list) && list is { Count: > 0 };

        void OnButtonPressed(InputEvents ie)
        {
            if (vesselStatus.AutoPilotEnabled) 
                return;
            
            if (IsSpawned && IsOwner)
            {
                SendButtonPressed_ServerRpc(ie);
            }
            else
            {
                PerformShipControllerActions(ie);
            }
            
            OnInputEventStarted?.Invoke(ie);
        }

        [ServerRpc]
        private void SendButtonPressed_ServerRpc(InputEvents ie) =>
            SendButtonPressed_ClientRpc(ie);

        [ClientRpc] 
        void SendButtonPressed_ClientRpc(InputEvents ie) => 
            PerformShipControllerActions(ie);

        void OnButtonReleased(InputEvents ie)
        {
            if (vesselStatus.AutoPilotEnabled) 
                return;

            if (IsSpawned && IsOwner)
            {
                SendButtonReleased_ServerRpc(ie);
            }
            else
            {
                StopShipControllerActions(ie); 
            }
            
            OnInputEventStopped?.Invoke(ie);
        }

        [ServerRpc]
        private void SendButtonReleased_ServerRpc(InputEvents ie) =>
            SendButtonReleased_ClientRpc(ie);

        [ClientRpc]
        void SendButtonReleased_ClientRpc(InputEvents ie) =>
            StopShipControllerActions(ie);
    }

    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipActionSO> ShipActions;
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipActionSO> ClassActions;
    }
}

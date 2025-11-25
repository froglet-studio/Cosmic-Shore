using CosmicShore.Core;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.SOAP;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private ScriptableEventInputEventBlock _onInputEventBlocked; 
        
        readonly Dictionary<InputEvents, List<ShipActionSO>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipActionSO>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();
        private readonly Dictionary<InputEvents, float> _inputMuteUntil = new();
        private readonly Dictionary<InputEvents, CancellationTokenSource> _muteEndCts = new();
        readonly List<ShipActionSO> _runtimeInstances = new();
        
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
            if (!IsSpawned) ShipHelper.DestroyRuntimeActions(_runtimeInstances);
            UnsubscribeFromInputEvents();
            
            // TODO - These are not static events, so unsubscribe is not necessary,
            // but better to do it for safety. but not on OnDisable, as few references will be missing,
            // better to do it earlier.
            /*if (vesselStatus.IsLocalUser)
                vesselStatus.InputStatus.OnToggleInputPaused -= OnToggleInputPaused;*/
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) UnsubscribeFromInputEvents();
            ShipHelper.DestroyRuntimeActions(_runtimeInstances);
        }

        public void ToggleSubscription(bool subscribe)
        {
            if (subscribe) SubscribeToInputEvents();
            else           UnsubscribeFromInputEvents();
        }

        public void Initialize(IVesselStatus v)
        {
            vesselStatus = v;
            if (_executors) _executors.InitializeAll(vesselStatus);

            _runtimeInstances.Clear();
            ShipHelper.InitializeShipControlActions(vesselStatus, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(vesselStatus, _resourceEventClassActions, _classResourceActions);
            
            if (vesselStatus.IsLocalUser)
                vesselStatus.InputStatus.OnToggleInputPaused += OnToggleInputPaused;
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            if (IsInputMuted(controlType)) return;
            if (!HasAction(controlType)) return;

            _inputAbilityStartTimes[controlType] = Time.time;
            var actions = _shipControlActions[controlType];

            foreach (var t in actions)
                t.StartAction(_executors, vesselStatus);
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (!HasAction(controlType)) return;

            float duration = 0f;
            if (_inputAbilityStartTimes.TryGetValue(controlType, out var start))
                duration = Time.time - start;

            onAbilityExecuted.Raise(new AbilityStats
            {
                PlayerName  = vesselStatus.PlayerName,
                ControlType = controlType,
                Duration    = duration
            });

            var actions = _shipControlActions[controlType];

            for (int i = 0; i < actions.Count; i++)
                actions[i].StopAction(_executors, vesselStatus);
        }

        void OnToggleInputPaused(bool toggle) => ToggleSubscription(!toggle);

        bool HasAction(InputEvents inputEvent) =>
            _shipControlActions.TryGetValue(inputEvent, out var list) && list is { Count: > 0 };

        void OnButtonPressed(InputEvents ie)
        {
            if (vesselStatus.AutoPilotEnabled) 
                return;
            if (IsInputMuted(ie)) return;
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

        #region Mute Input

        bool IsInputMuted(InputEvents ie) =>
            _inputMuteUntil.TryGetValue(ie, out var until) && Time.time < until;

        public void MuteInput(InputEvents ie, float seconds)
        {
            if (seconds <= 0f) return;

            float newUntil = Time.time + seconds;
            if (_inputMuteUntil.TryGetValue(ie, out var until))
                _inputMuteUntil[ie] = Mathf.Max(until, newUntil);
            else
                _inputMuteUntil[ie] = newUntil;

            _onInputEventBlocked?.Raise(new InputEventBlockPayload
            {
                Input        = ie,
                TotalSeconds = seconds,
                Started =  true,
                Ended        = false
            });

            // (Re)arm a single end notifier for this input
            if (_muteEndCts.TryGetValue(ie, out var prev))
            {
                try { prev.Cancel(); } catch { }
                prev.Dispose();
            }
            var cts = new CancellationTokenSource();
            _muteEndCts[ie] = cts;
            EndMuteWhenElapsedAsync(ie, cts.Token).Forget();
        }

        private async UniTaskVoid EndMuteWhenElapsedAsync(InputEvents ie, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (!IsInputMuted(ie)) break;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (!ct.IsCancellationRequested)
            {
                _inputMuteUntil.Remove(ie);
                _muteEndCts.Remove(ie);

                _onInputEventBlocked?.Raise(new InputEventBlockPayload
                { 
                    Input        = ie,
                    TotalSeconds = 0f,
                    Started =  false,
                    Ended        = true
                });
            }
        }

        #endregion
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

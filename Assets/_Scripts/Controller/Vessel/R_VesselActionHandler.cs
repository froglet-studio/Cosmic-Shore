using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.UI;
using System.Linq;
namespace CosmicShore.Gameplay
{
    public class R_VesselActionHandler : NetworkBehaviour
    {
        /// <summary>
        /// Replicated elemental unlock bits (bit = 1 &lt;&lt; ((int)element - 1) for
        /// Charge/Mass/Space/Time). Owner-write: the owning machine's
        /// R_VesselElementalAbilityHandler derives unlock state from its own ResourceSystem
        /// (element levels themselves never replicate) and publishes it here so every peer
        /// resolves outcome-affecting upgrades (piercing / shielded prisms / domain-sparing
        /// explosions) identically — divergent unlock state would desync the conserved
        /// prismscape. Lives on this NetworkBehaviour because VesselStatus is deliberately a
        /// plain MonoBehaviour.
        /// </summary>
        public NetworkVariable<byte> NetElementUnlocks = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [Header("Executors")]
        [SerializeField] ActionExecutorRegistry _executors;

        [Header("Action mappings")]
        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;

        [Header("Device-specific action overrides")]
        [Tooltip("Touch overrides take precedence over shared mappings for matching input events.")]
        [SerializeField] List<InputEventShipActionMapping> _touchActionOverrides;
        [Tooltip("Gamepad overrides take precedence over shared mappings for matching input events.")]
        [SerializeField] List<InputEventShipActionMapping> _gamepadActionOverrides;

        [Header("Scriptable events")]
        [SerializeField] ScriptableEventInputEvents _onButtonPressed;
        [SerializeField] ScriptableEventInputEvents _onButtonReleased;
        [SerializeField] ScriptableEventAbilityStats onAbilityExecuted;
        [SerializeField] private ScriptableEventInputEventBlock _onInputEventBlocked; 
        
        readonly Dictionary<InputEvents, List<ShipActionSO>> _shipControlActions = new();
        readonly Dictionary<InputEvents, List<ShipActionSO>> _touchOverrideActions = new();
        readonly Dictionary<InputEvents, List<ShipActionSO>> _gamepadOverrideActions = new();
        readonly Dictionary<ResourceEvents, List<ShipActionSO>> _classResourceActions = new();
        readonly Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();
        private readonly Dictionary<InputEvents, float> _inputMuteUntil = new();
        private readonly Dictionary<InputEvents, CancellationTokenSource> _muteEndCts = new();
        readonly List<ShipActionSO> _runtimeInstances = new();
        
        // TODO - Unnecessary events added. OnInputEventStarted, OnInputEventStopped
        // Remove the ones below and Use _onButtonPressed and _onButtonReleased.
        public event Action<InputEvents> OnInputEventStarted;
        public event Action<InputEvents> OnInputEventStopped;
        IVesselStatus vesselStatus;
        bool _subscribedToInputPaused;

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

            // During scene teardown the Player may already be destroyed.
            // The event lives on the Player, so it's GC'd with it - skip the unsubscribe.
            if (_subscribedToInputPaused && vesselStatus?.Player is UnityEngine.Object obj && obj != null)
            {
                vesselStatus.InputStatus.OnToggleInputPaused -= OnToggleInputPaused;
                _subscribedToInputPaused = false;
            }
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
            ShipHelper.InitializeShipControlActions(vesselStatus, _touchActionOverrides, _touchOverrideActions);
            ShipHelper.InitializeShipControlActions(vesselStatus, _gamepadActionOverrides, _gamepadOverrideActions);
            ShipHelper.InitializeClassResourceActions(_resourceEventClassActions, _classResourceActions);

            if (vesselStatus.IsLocalUser)
            {
                vesselStatus.InputStatus.OnToggleInputPaused += OnToggleInputPaused;
                _subscribedToInputPaused = true;
            }
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            if (IsInputMuted(controlType)) return;
            if (!HasAction(controlType)) return;

            _inputAbilityStartTimes[controlType] = Time.time;
            var actions = ResolveActions(controlType);

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

            var actions = ResolveActions(controlType);

            for (int i = 0; i < actions.Count; i++)
                actions[i].StopAction(_executors, vesselStatus);
        }

        /// <summary>
        /// Returns the action list for a given input event, checking device-specific
        /// overrides first and falling back to the shared mapping.
        /// </summary>
        List<ShipActionSO> ResolveActions(InputEvents controlType)
        {
            var overrides = GetActiveOverrides();
            if (overrides != null && overrides.TryGetValue(controlType, out var overrideList) && overrideList is { Count: > 0 })
                return overrideList;
            return _shipControlActions[controlType];
        }

        Dictionary<InputEvents, List<ShipActionSO>> GetActiveOverrides()
        {
            if (vesselStatus?.InputStatus == null) return null;
            return vesselStatus.InputStatus.ActiveInputDevice switch
            {
                InputDeviceType.Touch   => _touchOverrideActions,
                InputDeviceType.Gamepad => _gamepadOverrideActions,
                // DualMouse and Keyboard raise the same LeftStick/RightStick trigger events as the
                // gamepad (keyboard: Left Shift / Right Shift), so they share the gamepad's
                // per-trigger override mapping. Vessels with no gamepad overrides fall through to
                // the shared mapping exactly as before.
                InputDeviceType.DualMouse => _gamepadOverrideActions,
                InputDeviceType.Keyboard => _gamepadOverrideActions,
                _                       => null
            };
        }

        void OnToggleInputPaused(bool toggle) => ToggleSubscription(!toggle);

        /// <summary>
        /// Appends every action this vessel binds to <paramref name="inputEvent"/> - across the shared
        /// map AND both device override maps, not just the active device's. Presentation code uses it
        /// to work out which ability an input drives (the HUD's control-hint binder), which needs to
        /// see the touch and gamepad bindings together to know they are the same ability.
        /// Safe before Initialize - the maps are simply empty.
        /// </summary>
        public void CollectBoundActions(InputEvents inputEvent, List<ShipActionSO> into)
        {
            if (into == null) return;
            AppendBound(_shipControlActions, inputEvent, into);
            AppendBound(_touchOverrideActions, inputEvent, into);
            AppendBound(_gamepadOverrideActions, inputEvent, into);
        }

        /// <summary>True when this vessel binds any action to the input event, on any device.</summary>
        public bool HasBinding(InputEvents inputEvent) =>
            IsBound(_shipControlActions, inputEvent) ||
            IsBound(_touchOverrideActions, inputEvent) ||
            IsBound(_gamepadOverrideActions, inputEvent);

        static void AppendBound(Dictionary<InputEvents, List<ShipActionSO>> map,
            InputEvents inputEvent, List<ShipActionSO> into)
        {
            if (map != null && map.TryGetValue(inputEvent, out var list) && list != null)
                into.AddRange(list);
        }

        static bool IsBound(Dictionary<InputEvents, List<ShipActionSO>> map, InputEvents inputEvent)
            => map != null && map.TryGetValue(inputEvent, out var list) && list is { Count: > 0 };

        bool HasAction(InputEvents inputEvent)
        {
            var overrides = GetActiveOverrides();
            if (overrides != null && overrides.TryGetValue(inputEvent, out var overrideList) && overrideList is { Count: > 0 })
                return true;
            return _shipControlActions.TryGetValue(inputEvent, out var list) && list is { Count: > 0 };
        }

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
        private void SendButtonPressed_ServerRpc(InputEvents ie)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                SendButtonPressed_ClientRpc(ie);
            }
        }

        [ClientRpc]
        void SendButtonPressed_ClientRpc(InputEvents ie)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                PerformShipControllerActions(ie);
            }
        }

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
        private void SendButtonReleased_ServerRpc(InputEvents ie)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                SendButtonReleased_ClientRpc(ie);
            }
        }

        [ClientRpc]
        void SendButtonReleased_ClientRpc(InputEvents ie)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                StopShipControllerActions(ie);
            }
        }

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

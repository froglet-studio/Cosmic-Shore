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

        /// <summary>
        /// Replicated INTEGER element levels, four bits per element (nibble <c>(int)element - 1</c>,
        /// Charge lowest), each clamped to 0..15 — the deficit band reads as 0. Owner-write, the
        /// sibling of <see cref="NetElementUnlocks"/> and published from the same place.
        ///
        /// <para>It exists because element levels never replicate, so any ability that scales an
        /// OUTCOME continuously by an element (not merely gates it on an upgrade) resolves
        /// differently on every peer: a remote copy of the vessel sits at whatever level its
        /// replica started with. The unlock bits solved that for the qualitative half; this is
        /// the quantitative half, at integer resolution, which is all an outcome needs and what
        /// lets owner and peers compute the SAME number. Read it through
        /// <c>R_VesselElementalAbilityHandler.ReplicatedLevel</c>, never directly.</para>
        /// </summary>
        public NetworkVariable<ushort> NetElementLevels = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        /// <summary>
        /// The live SHAPE of the Dolphin's Echo Sight while its owner holds it:
        /// <c>(BlastVolume.Height, TanCorePerUnit, TanGapePerUnit)</c>, or
        /// <see cref="Vector3.zero"/> when nobody is aiming. Owner-write, for the same reason
        /// <see cref="NetElementUnlocks"/> is: the sight became visible to every player on
        /// 2026-08-19, and a remote peer cannot derive this volume for itself.
        ///
        /// Two independent reasons it cannot:
        /// <list type="bullet">
        /// <item>Element levels never replicate (see <see cref="NetElementUnlocks"/>), and the
        /// blast reads Space for its reach and Charge for its thickness — a crystal is collected
        /// server-side and <c>NetworkCrystalManager.ReplayVesselCrystalEffects</c> replays the
        /// vessel effects to the OWNER alone, so a third client's replica never sees the level
        /// change at all.</item>
        /// <item>The banked skim energy that sets the gape is simulated locally against each
        /// machine's own prisms, and it is SPENT by a crystal collection that likewise only
        /// resolves on the server and the owner — so on a third client the meter would drift
        /// upward and then never empty.</item>
        /// </list>
        ///
        /// Only these three scalars travel. The apex and both axes come off the vessel's own
        /// replicated transform, so the moving part of the volume is already free and exact and
        /// nothing has to be interpolated between ticks — what a peer draws turns with the ship at
        /// full frame rate and only changes SIZE at the network tick.
        ///
        /// The owner writes it only while engaged and zeroes it on release, so a vessel that never
        /// carries the ability never dirties it. Lives on this NetworkBehaviour because
        /// VesselStatus is deliberately a plain MonoBehaviour.
        /// </summary>
        public NetworkVariable<Vector3> NetEchoSightShape = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        /// <summary>
        /// The Stoat's live FIELD DIPOLE (<c>R_VesselActions/STOAT_DIPOLE.md</c>): the sink's world
        /// position in xyz and both poles' horizon radius in w, or <see cref="Vector4.zero"/> when the
        /// pair is closed. With <see cref="NetStoatDipoleSource"/> it is the whole pair. Owner-write, for
        /// the <see cref="NetEchoSightShape"/> reason: the poles' separation IS the analog squeeze of
        /// two triggers, which no peer receives, and their size is Space-scaled on the owner's
        /// unreplicated element level. Every peer draws, pulls, steals and kills with its own copy of
        /// the pair, so every peer needs this. Written only while a pair is open (and zeroed when it
        /// closes), so a hull that never carries the ability never dirties it.
        /// </summary>
        public NetworkVariable<Vector4> NetStoatDipoleSink = new(
            Vector4.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        /// <summary>The source (white hole) of <see cref="NetStoatDipoleSink"/>'s pair, world position.</summary>
        public NetworkVariable<Vector3> NetStoatDipoleSource = new(
            Vector3.zero,
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
        /// <summary>Input events whose actions are currently STARTED on this machine, each with the
        /// device its press was resolved against — the ledger <see cref="ReleaseHeldInputs"/> needs,
        /// and what a release resolves with, so a release stops what its press started.
        /// <c>_inputAbilityStartTimes</c> cannot serve: it records when an event LAST started and is
        /// never cleared, so it cannot tell a held ability from one released a minute ago.</summary>
        readonly Dictionary<InputEvents, byte> _heldInputs = new();
        readonly List<KeyValuePair<InputEvents, byte>> _heldScratch = new();
        readonly Dictionary<ResourceEvents, float> _resourceAbilityStartTimes = new();
        readonly HashSet<InputEvents> _suppressedInputs = new();
        private readonly Dictionary<InputEvents, float> _inputMuteUntil = new();
        private readonly Dictionary<InputEvents, CancellationTokenSource> _muteEndCts = new();
        readonly List<ShipActionSO> _runtimeInstances = new();
        
        // TODO - Unnecessary events added. OnInputEventStarted, OnInputEventStopped
        // Remove the ones below and Use _onButtonPressed and _onButtonReleased.
        public event Action<InputEvents> OnInputEventStarted;
        public event Action<InputEvents> OnInputEventStopped;
        IVesselStatus vesselStatus;
        bool _subscribedToInputPaused;
        // The status the pause handler is attached to - recorded so a re-init (which may hand
        // this vessel a different player) detaches from the right one before re-binding.
        IInputStatus _pauseSource;

        // ONE SUBSCRIPTION, EVER - and the latch is what enforces it, because a C# delegate
        // happily holds the same handler twice and nothing reports it.
        //
        // Three paths subscribe and they are not mutually exclusive: VesselController.Initialize
        // (every spawn), VesselController.ChangePlayer (a LIVE vessel handed to another player -
        // the Cellular Duel ownership swap, which Initialize never sees), and every un-pause
        // (OnToggleInputPaused). A second += therefore makes OnButtonPressed run twice per press,
        // which sends the press RPC twice, which replays PerformShipControllerActions twice on
        // every peer.
        //
        // That is invisible on almost everything the fleet binds, because a HELD ability started
        // twice is the same ability held - which is exactly why it went unnoticed. It is NOT
        // invisible on a one-shot that SPENDS: the Sparrow's skyburst charged the tank twice and
        // launched two rockets from one pull of the trigger, and the Butterfly's right trigger -
        // a TOGGLE - flipped Mass -> Dust -> Mass on every pull and read as a dead button. A
        // duplicate release is equally silent, so the pair is latched together rather than only
        // the press.
        bool _subscribedToInputEvents;

        void SubscribeToInputEvents()
        {
            if (_subscribedToInputEvents) return;
            _onButtonPressed.OnRaised  += OnButtonPressed;
            _onButtonReleased.OnRaised += OnButtonReleased;
            _subscribedToInputEvents = true;
            ReportDiagnostic("listening", "yes");
        }

        void UnsubscribeFromInputEvents()
        {
            if (!_subscribedToInputEvents) return;
            _onButtonPressed.OnRaised  -= OnButtonPressed;
            _onButtonReleased.OnRaised -= OnButtonReleased;
            _subscribedToInputEvents = false;
            ReportDiagnostic("listening", "no");
        }

        // THE BUTTON CHANNELS FOLLOW THE PAUSE STATE, NOT THE PAUSE EVENTS. For the local pilot
        // this handler is meant to be listening exactly while input is un-paused, and that used
        // to be maintained only by edges: OnToggleInputPaused, plus the explicit
        // ToggleSubscription calls at spawn and handover. An edge that is not delivered strands
        // the handler deaf with nothing to report it - the vessel still FLIES, because flight
        // reads InputStatus directly, while every ability is silently dead. Two ways an edge goes
        // missing are visible from here: n_paused is a NetworkVariable, whose OnValueChanged
        // fires only on a CHANGE, so a pause write that matches the replicated value says
        // nothing; and OnDisable drops both the button channels and the pause source, while
        // nothing re-attached them on the way back. So the state is RECONCILED: once a frame,
        // for the local pilot only (one bool compare), the subscription is brought into line with
        // the pause state it is supposed to mirror. The edges still do the work; this makes a
        // missed one heal on the next frame instead of never.
        void OnEnable()
        {
            if (HasLivePilot()) AttachInputPause();
        }

        void Update()
        {
            if (!_subscribedToInputPaused || _pauseSource == null) return;
            // The pause source lives on the PILOT; a scene teardown can destroy it first.
            if (_pauseSource is UnityEngine.Object source && source == null) return;
            bool listen = !_pauseSource.Paused;
            if (listen != _subscribedToInputEvents) ToggleSubscription(listen);
        }

        /// <summary>A pilot is on this vessel and still exists - `== null` on the IPlayer
        /// interface is a reference compare, so a destroyed Player needs the Unity check.</summary>
        bool HasLivePilot()
        {
            var player = vesselStatus?.Player;
            return player != null && !(player is UnityEngine.Object o && o == null);
        }

        void OnDisable()
        {
            if (!IsSpawned) ShipHelper.DestroyRuntimeActions(_runtimeInstances);
            UnsubscribeFromInputEvents();

            // During scene teardown the Player may already be destroyed.
            // The event lives on the Player, so it's GC'd with it - skip the unsubscribe.
            if (_subscribedToInputPaused && vesselStatus?.Player is UnityEngine.Object obj && obj != null)
            {
                if (_pauseSource != null) _pauseSource.OnToggleInputPaused -= OnToggleInputPaused;
            }
            _subscribedToInputPaused = false;
            _pauseSource = null;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) UnsubscribeFromInputEvents();
            // Stop the reconcile with it: between despawn and the deferred Destroy (a vessel swap)
            // Update would otherwise re-subscribe the dying hull to the global button channel.
            DetachInputPause();
            ShipHelper.DestroyRuntimeActions(_runtimeInstances);
        }

        public void ToggleSubscription(bool subscribe)
        {
            if (subscribe)
            {
                SubscribeToInputEvents();
                return;
            }

            // THE RELEASE EDGE IS AN INPUT EVENT, SO IT NEVER ARRIVES FOR A VESSEL THAT STOPS
            // BEING DRIVEN. Detaching the button channels with an ability still HELD strands that
            // ability on — for as long as the vessel lives, on every peer that ran the press,
            // including the server. It is not hypothetical and it is not one vessel's problem:
            // every held ability in the fleet is exposed (the Dolphin's Echo Sight and the
            // Scarab's phase grab are the two today), and the executors' own OnDisable cannot
            // reach it, because a pause deactivates nothing.
            //
            // So the state is torn down where the object goes quiet rather than trusting the edge.
            // Release BEFORE detaching: StopShipControllerActions raises the ability-duration
            // event and runs each action's StopAction, which is exactly what a real release does.
            ReleaseHeldInputs();
            UnsubscribeFromInputEvents();
        }

        /// <summary>
        /// Stop every input event this handler currently has started, as if the pilot had let go.
        ///
        /// The OWNER sends it the way a real release travels — owner → server → every peer — so a
        /// hold cannot survive on somebody else's copy of this vessel. Anything else (a non-owner
        /// replica, or the non-networked single-player path) stops locally, which is the same
        /// asymmetry <see cref="OnButtonReleased"/> already has.
        ///
        /// Deliberately NOT called from OnDisable or OnNetworkDespawn: those run during teardown,
        /// where an RPC is unsafe and the object is going away on every peer regardless. An
        /// executor's own OnDisable covers that case.
        /// </summary>
        public void ReleaseHeldInputs()
        {
            if (_heldInputs.Count == 0) return;

            _heldScratch.Clear();
            _heldScratch.AddRange(_heldInputs);      // StopPressedActions mutates the ledger
            _heldInputs.Clear();

            for (int i = 0; i < _heldScratch.Count; i++)
            {
                var ie = _heldScratch[i].Key;
                var device = _heldScratch[i].Value;
                if (IsSpawned && IsOwner) SendButtonReleased_ServerRpc(ie, device);
                else                      StopPressedActions(ie, device);
                OnInputEventStopped?.Invoke(ie);
            }
            _heldScratch.Clear();
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

            // The same one-subscription rule for the PAUSE event: Initialize re-runs on a live
            // vessel, and a second += here makes every pause toggle subscribe and unsubscribe the
            // button channels twice. Detach from whatever status we were listening to first.
            if (_subscribedToInputPaused && _pauseSource != null)
                _pauseSource.OnToggleInputPaused -= OnToggleInputPaused;
            _subscribedToInputPaused = false;
            _pauseSource = null;

            if (vesselStatus.IsLocalUser)
            {
                _pauseSource = vesselStatus.InputStatus;
                _pauseSource.OnToggleInputPaused += OnToggleInputPaused;
                _subscribedToInputPaused = true;
            }
        }

        /// <summary>Press <paramref name="controlType"/> locally, resolved against the device this
        /// vessel's pilot is flying with. For a caller on THIS machine (the server-only autopilots);
        /// a press that arrives over the wire resolves against the device it carries instead.</summary>
        public void PerformShipControllerActions(InputEvents controlType) =>
            StartPressedActions(controlType, CurrentDevice());

        void StartPressedActions(InputEvents controlType, byte device)
        {
            if (IsInputMuted(controlType)) return;
            if (!TryResolveActions(controlType, device, out var actions)) return;

            _inputAbilityStartTimes[controlType] = Time.time;
            _heldInputs[controlType] = device;
            ReportRan(controlType, actions.Count);

            foreach (var t in actions)
                t.StartAction(_executors, vesselStatus);
        }

        /// <summary>Release counterpart of <see cref="PerformShipControllerActions(InputEvents)"/>.</summary>
        public void StopShipControllerActions(InputEvents controlType) =>
            StopPressedActions(controlType, CurrentDevice());

        void StopPressedActions(InputEvents controlType, byte device)
        {
            // A RELEASE STOPS WHAT ITS PRESS STARTED. A press records the device it resolved with,
            // and that record outranks the device handed in here: a pilot who picks up a pad
            // mid-hold would otherwise resolve the release against the other map and stop nothing,
            // leaving the held ability running on every peer. The device handed in only decides
            // for a release whose press this machine never ran (a client that joined mid-hold).
            if (_heldInputs.TryGetValue(controlType, out var pressedWith)) device = pressedWith;
            if (!TryResolveActions(controlType, device, out var actions)) return;

            float duration = 0f;
            if (_inputAbilityStartTimes.TryGetValue(controlType, out var start))
                duration = Time.time - start;

            onAbilityExecuted.Raise(new AbilityStats
            {
                PlayerName  = vesselStatus.PlayerName,
                ControlType = controlType,
                Duration    = duration
            });

            _heldInputs.Remove(controlType);

            for (int i = 0; i < actions.Count; i++)
                actions[i].StopAction(_executors, vesselStatus);
        }

        bool TryResolveActions(InputEvents controlType, byte device, out List<ShipActionSO> actions) =>
            TryGetPressedActions(_shipControlActions,
                OverridesForCarried(device, _touchOverrideActions, _gamepadOverrideActions),
                controlType, out actions);

        /// <summary>
        /// The device a press is resolved against, in the form the press and release RPCs carry it:
        /// <c>(byte)InputDeviceType</c>, or <see cref="NoDevice"/> while the vessel has no pilot input
        /// (a press then runs the shared map alone).
        ///
        /// <para><b>Why a press carries its device at all.</b> Replication here is by RE-EXECUTION:
        /// the owner sends which INPUT was pressed and every peer resolves that input to actions
        /// itself. The resolution depends on the device (Touch reads the touch overrides; every
        /// desktop device reads the pad overrides), so a peer that resolved against ITS idea of the
        /// device ran a different press. Until 2026-10 that idea was its own hardware's — the field
        /// did not replicate — so on the Squirrel, whose drift (with its trail prisms) and Boost
        /// Ring live only in the override maps, a phone peer refused a PC pilot's drift outright and
        /// a PC peer refused a phone pilot's; on the Manta the same press ran the BOOST on one
        /// machine and the analog turn-boost on the other. The device now also replicates
        /// (<see cref="InputStatus"/>), which fixes every other reader, but a NetworkVariable and an
        /// RPC are not ordered against each other: the press made on the frame a device switches
        /// would reach peers ahead of the switch. So the press says what it was resolved against
        /// and every peer — the owner's own copy included — runs exactly that. One byte per press,
        /// not the enum's four.</para>
        /// </summary>
        byte CurrentDevice() =>
            vesselStatus?.InputStatus == null ? NoDevice : (byte)vesselStatus.InputStatus.ActiveInputDevice;

        /// <summary>The device the HELD <paramref name="ie"/> was pressed with, for its release to
        /// carry; the current device when this machine has no record of the press.</summary>
        byte PressedDevice(InputEvents ie) => _heldInputs.TryGetValue(ie, out var d) ? d : CurrentDevice();

        /// <summary>The carried form of "no pilot input yet" - see <see cref="CurrentDevice"/>.</summary>
        internal const byte NoDevice = byte.MaxValue;

        /// <summary><see cref="OverridesFor"/> for a device as a press carries it
        /// (<see cref="CurrentDevice"/>): <see cref="NoDevice"/> resolves against no override map,
        /// and so does any value that names no <see cref="InputDeviceType"/>.</summary>
        internal static Dictionary<InputEvents, List<ShipActionSO>> OverridesForCarried(
            byte device,
            Dictionary<InputEvents, List<ShipActionSO>> touchOverrides,
            Dictionary<InputEvents, List<ShipActionSO>> gamepadOverrides) =>
            device == NoDevice ? null : OverridesFor((InputDeviceType)device, touchOverrides, gamepadOverrides);

        /// <summary>
        /// What pressing <paramref name="inputEvent"/> runs, given the shared map and the override
        /// map of the device the press is resolved against (null when there is no device yet): a
        /// non-empty override list wins, otherwise the shared list. The ONE resolution rule — the
        /// press and its release (<see cref="TryResolveActions"/>, against the device the press
        /// carried) and the autopilot lookup (<see cref="TryGetBoundAction{T}"/>, against the
        /// current device) all ask it, so a control the lookup hands out is by construction a
        /// control the press accepts.
        /// </summary>
        internal static bool TryGetPressedActions(Dictionary<InputEvents, List<ShipActionSO>> shared,
                                                  Dictionary<InputEvents, List<ShipActionSO>> overrides,
                                                  InputEvents inputEvent, out List<ShipActionSO> actions)
        {
            if (overrides != null && overrides.TryGetValue(inputEvent, out actions) && actions is { Count: > 0 })
                return true;
            if (shared != null && shared.TryGetValue(inputEvent, out actions) && actions is { Count: > 0 })
                return true;
            actions = null;
            return false;
        }

        Dictionary<InputEvents, List<ShipActionSO>> GetActiveOverrides() =>
            OverridesForCarried(CurrentDevice(), _touchOverrideActions, _gamepadOverrideActions);

        /// <summary>Which override map a device's presses resolve against.</summary>
        internal static Dictionary<InputEvents, List<ShipActionSO>> OverridesFor(
            InputDeviceType device,
            Dictionary<InputEvents, List<ShipActionSO>> touchOverrides,
            Dictionary<InputEvents, List<ShipActionSO>> gamepadOverrides)
        {
            return device switch
            {
                InputDeviceType.Touch   => touchOverrides,
                InputDeviceType.Gamepad => gamepadOverrides,
                // DualMouse and Keyboard raise the same LeftStick/RightStick trigger events as the
                // gamepad (keyboard: Left Shift / Right Shift), so they share the gamepad's
                // per-trigger override mapping. Vessels with no gamepad overrides fall through to
                // the shared mapping exactly as before.
                InputDeviceType.DualMouse => gamepadOverrides,
                InputDeviceType.Keyboard => gamepadOverrides,
                // Same reason again for the one-thumb mouse scheme: SingleStickMouseInputStrategy
                // raises the pad's LeftStick/RightStick trigger events (LMB / RMB, and the shift
                // keys alongside them), so it wants the pad's per-trigger overrides.
                InputDeviceType.MouseKeyboard => gamepadOverrides,
                _                       => null
            };
        }

        void OnToggleInputPaused(bool toggle) => ToggleSubscription(!toggle);

        string GetActiveDeviceName() =>
            vesselStatus?.InputStatus == null ? "no input" : vesselStatus.InputStatus.ActiveInputDevice.ToString();

        /// <summary>
        /// One row of the on-screen DiagnosticsHUD ("Abilities"), for the LOCAL pilot's vessel
        /// only: whether this handler is listening to the button channels, the last press it
        /// heard and what it resolved to, and the last dispatch that reached the actions. It
        /// separates the three ways "the ability did nothing" happens - not listening, heard but
        /// unbound, dispatched but the executor produced nothing - on a device, where the
        /// console is out of reach. Compiled out (arguments included) outside the editor and
        /// development builds, where the overlay does not exist.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportDiagnostic(string label, string value)
        {
            if (!DiagnosticsWanted()) return;
            CosmicShore.Utility.PerformanceBenchmark.DiagnosticsHUD.SetStat("Abilities", label, value);
        }

        /// <summary>The press row. Its own conditional method so the bound/unbound lookup and the
        /// string are compiled out of release builds and skipped for every pilot but the local one.</summary>
        // The message is formatted INSIDE, after DiagnosticsWanted(): a [Conditional] method's
        // arguments are still evaluated in the editor and in development builds, and presses arrive
        // every few frames on every vessel (AI and autopilot included), so a `$"{ie}: ..."` built at
        // the call site allocated a string per press only to be thrown away (CLAUDE.md, logging rule).
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportPressOutcome(InputEvents ie, string outcome)
        {
            if (!DiagnosticsWanted()) return;
            CosmicShore.Utility.PerformanceBenchmark.DiagnosticsHUD.SetStat("Abilities", "press", $"{ie}: {outcome}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportPress(InputEvents ie)
        {
            if (!DiagnosticsWanted()) return;
            CosmicShore.Utility.PerformanceBenchmark.DiagnosticsHUD.SetStat("Abilities",
                HasAction(ie) ? "press" : "unbound", $"{ie} ({GetActiveDeviceName()})");
        }

        /// <summary>The dispatch row, filtered before its string is built (see <see cref="ReportPress"/>).</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportRan(InputEvents ie, int actionCount)
        {
            if (!DiagnosticsWanted()) return;
            CosmicShore.Utility.PerformanceBenchmark.DiagnosticsHUD.SetStat("Abilities", "ran", $"{ie} x{actionCount}");
        }

        bool DiagnosticsWanted() => HasLivePilot() && vesselStatus.IsLocalUser;

        /// <summary>
        /// Detach the input-pause subscription from the pilot currently on this vessel. Call
        /// BEFORE <c>VesselStatus.Player</c> changes (<c>VesselController.ChangePlayer</c>): the
        /// subscription lives on the PILOT's InputStatus, and once the pointer moves this handler
        /// can no longer reach the one it subscribed to. Left behind, the pilot who LEFT keeps
        /// switching this vessel's button channels on and off with their own pauses, and the
        /// pilot who ARRIVED never does.
        /// </summary>
        public void DetachInputPause()
        {
            if (!_subscribedToInputPaused) return;
            _subscribedToInputPaused = false;
            // Detach from the status we RECORDED, not from whatever vesselStatus resolves to now -
            // they are the same here (called before the pointer moves), and recording it is what
            // keeps Initialize's own re-bind and this pair from ever disagreeing.
            if (_pauseSource != null && vesselStatus?.Player is UnityEngine.Object obj && obj != null)
                _pauseSource.OnToggleInputPaused -= OnToggleInputPaused;
            _pauseSource = null;
        }

        /// <summary>
        /// Subscribe to the input pause of the pilot NOW on this vessel, if that pilot is the local
        /// user - the same rule <see cref="Initialize"/> applies at spawn. Idempotent.
        /// </summary>
        public void AttachInputPause()
        {
            if (_subscribedToInputPaused || vesselStatus == null || !vesselStatus.IsLocalUser) return;
            _pauseSource = vesselStatus.InputStatus;
            _pauseSource.OnToggleInputPaused += OnToggleInputPaused;
            _subscribedToInputPaused = true;
        }

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

        /// <summary>
        /// The reverse of <see cref="CollectBoundActions"/>: which control, PRESSED NOW, runs an
        /// ability of type <typeparamref name="T"/> on this vessel, if any.
        ///
        /// It exists so an autonomous pilot can press an ability WITHOUT knowing which vessel it is
        /// flying or which trigger that vessel's designer put it on — the AI asks for the concept
        /// and the binding answers. <typeparamref name="T"/> is constrained to <c>class</c> rather
        /// than to <c>ShipActionSO</c> precisely so it can be a capability INTERFACE
        /// (<see cref="IAimTelegraphAction"/>) — asking for a concrete SO type would put the
        /// caller back to naming one vessel's ability, which is the coupling this removes.
        ///
        /// <para><b>It answers for the device the vessel is being driven by</b>, by the same rule a
        /// press resolves with (<see cref="TryGetPressedActions"/>): the active device's override
        /// map first, then the shared entries that map does not shadow. Every caller is an
        /// autopilot that either PRESSES the answer (Skim Race, Tollway, Waystation, the Butterfly
        /// mode driver, the aim telegraph) or starts the returned action itself (the AI boost
        /// policies' <c>CreateDriver</c>); for both, an answer the press gate would refuse is worse
        /// than none. It used to sweep shared → touch → gamepad regardless of device, which
        /// on the Squirrel — drift and Boost Ring bound ONLY in the two override maps — handed out
        /// the TOUCH controls (12 / 11), and on a PC (Gamepad, Keyboard, DualMouse and
        /// MouseKeyboard all resolve against the gamepad overrides) the press was refused at
        /// <see cref="HasAction"/>: the Skim Race AI's drift and ring could never fire on a PC. An
        /// override-only ability on another device is deliberately NOT a fallback: that control
        /// either does nothing here or, if the shared map binds it, fires a different ability.</para>
        ///
        /// Presentation code that wants every device's bindings at once (the HUD's control-hint
        /// binder) uses <see cref="CollectBoundActions"/> / <see cref="HasBinding"/>, which stay
        /// device-agnostic.
        ///
        /// Returns false for a vessel that binds no such ability on its active device — the answer
        /// for most of the fleet, so it must be a quiet no-op rather than a warning.
        /// <paramref name="inputEvent"/> is then <c>default</c>, which is the REAL member
        /// <c>FullSpeedStraightAction</c> and not a sentinel (<see cref="InputEvents"/> deliberately
        /// has none, since every value is a control somebody's vessel binds). Check the return
        /// value; never read the out parameter on false. The device can change (a pad plugged in,
        /// a pilot swap), so ask at press time rather than caching the answer for a match.
        /// </summary>
        public bool TryGetInputForAction<T>(out InputEvents inputEvent) where T : class
            => TryGetBoundAction<T>(out _, out inputEvent);

        /// <summary>
        /// <see cref="TryGetInputForAction{T}"/>, plus the ACTION itself. The same question with
        /// one more answer, and the extra answer is what stops a caller duplicating the ability's
        /// tuning: an autonomous pilot that has to decide HOW LONG to hold a held ability needs
        /// that ability's own numbers, and reading them off its SO keeps the asset the single
        /// source of them rather than copying a reach speed into a mode's controller — where it
        /// would be right on the day it was copied and silently stale after the next retune.
        ///
        /// Same resolution and the same contract as its sibling: false for a vessel that binds no
        /// such ability on its active device, and on false neither out parameter means anything.
        /// </summary>
        public bool TryGetBoundAction<T>(out T action, out InputEvents inputEvent) where T : class
            => TryFindPressableAction(_shipControlActions, GetActiveOverrides(), out action, out inputEvent);

        /// <summary>
        /// The resolution behind <see cref="TryGetBoundAction{T}"/>, over explicit maps:
        /// <paramref name="overrides"/> is the active device's override map
        /// (<see cref="OverridesFor"/>), or null when there is no device yet — in which case a press
        /// runs the shared map alone, and so does this. Every input it returns satisfies
        /// <see cref="TryGetPressedActions"/> with a list that contains the action it returns.
        /// </summary>
        internal static bool TryFindPressableAction<T>(Dictionary<InputEvents, List<ShipActionSO>> shared,
                                                       Dictionary<InputEvents, List<ShipActionSO>> overrides,
                                                       out T action, out InputEvents inputEvent) where T : class
        {
            // Every non-empty override entry is exactly what its input runs on this device.
            if (TryFindAction(overrides, null, out action, out inputEvent)) return true;
            // A shared entry runs only where the device's overrides leave its input alone.
            if (TryFindAction(shared, overrides, out action, out inputEvent)) return true;

            action = null;
            inputEvent = default;   // meaningless on false - see TryGetInputForAction
            return false;
        }

        static bool TryFindAction<T>(Dictionary<InputEvents, List<ShipActionSO>> map,
                                     Dictionary<InputEvents, List<ShipActionSO>> shadowedBy,
                                     out T action, out InputEvents inputEvent) where T : class
        {
            action = null;
            inputEvent = default;
            if (map == null) return false;

            foreach (var kv in map)
            {
                var list = kv.Value;
                if (list == null) continue;
                if (shadowedBy != null && IsBound(shadowedBy, kv.Key)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is not T typed) continue;
                    action = typed;
                    inputEvent = kv.Key;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Press a control the way a HUMAN pilot's press travels — owner to server to every peer —
        /// for a caller that is not the input system. <see cref="AIPilot"/> is the only one today.
        ///
        /// <para><b>Why an AI needs this at all.</b> An AI pilot runs on the SERVER ONLY
        /// (<c>Player.StartPlayer</c> returns before <c>ToggleAIPilot</c> on a client), and its
        /// existing calls go straight to <see cref="PerformShipControllerActions"/>, which is local.
        /// That is exactly right for an ability whose effect is MOTION — the drift moves the vessel
        /// and the vessel's transform is replicated, so every peer sees the result without being
        /// told the cause. It is exactly wrong for an ability whose entire effect is PHOTONS: a
        /// telegraph nobody else can see is not a telegraph. So the rule is: replicate an AI's
        /// press when the ability's output does not already ride some other replicated channel.</para>
        ///
        /// Falls back to the local call when this vessel is not spawned (the non-networked
        /// single-player path) or not owned here, so it is safe to call unconditionally.
        /// </summary>
        public void PerformShipControllerActionsReplicated(InputEvents ie)
        {
            if (IsSpawned && IsOwner)
                SendButtonPressed_ServerRpc(ie, CurrentDevice());
            else
                PerformShipControllerActions(ie);
        }

        /// <summary>Release counterpart of <see cref="PerformShipControllerActionsReplicated"/>.</summary>
        public void StopShipControllerActionsReplicated(InputEvents ie)
        {
            if (IsSpawned && IsOwner)
                SendButtonReleased_ServerRpc(ie, PressedDevice(ie));
            else
                StopShipControllerActions(ie);
        }

        static void AppendBound(Dictionary<InputEvents, List<ShipActionSO>> map,
            InputEvents inputEvent, List<ShipActionSO> into)
        {
            if (map != null && map.TryGetValue(inputEvent, out var list) && list != null)
                into.AddRange(list);
        }

        static bool IsBound(Dictionary<InputEvents, List<ShipActionSO>> map, InputEvents inputEvent)
            => map != null && map.TryGetValue(inputEvent, out var list) && list is { Count: > 0 };

        bool HasAction(InputEvents inputEvent) => TryResolveActions(inputEvent, CurrentDevice(), out _);

        void OnButtonPressed(InputEvents ie)
        {
            if (vesselStatus.AutoPilotEnabled)
            {
                ReportPressOutcome(ie, "ignored (autopilot)");
                return;
            }
            if (_suppressedInputs.Contains(ie)) { ReportPressOutcome(ie, "suppressed"); return; }
            if (IsInputMuted(ie)) { ReportPressOutcome(ie, "muted"); return; }
            // Unbound presses (IdleAction, the straight-line gestures) arrive every few frames,
            // so they get their own row rather than overwriting the ability that was pressed.
            ReportPress(ie);
            if (IsSpawned && IsOwner)
            {
                SendButtonPressed_ServerRpc(ie, CurrentDevice());
            }
            else
            {
                PerformShipControllerActions(ie);
            }
            
            OnInputEventStarted?.Invoke(ie);
        }

        // The press travels with the device it was resolved against (CurrentDevice), and every
        // peer resolves with THAT - never with its own copy of the pilot's input status.
        [ServerRpc]
        private void SendButtonPressed_ServerRpc(InputEvents ie, byte device)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                SendButtonPressed_ClientRpc(ie, device);
            }
        }

        [ClientRpc]
        void SendButtonPressed_ClientRpc(InputEvents ie, byte device)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                StartPressedActions(ie, device);
            }
        }

        void OnButtonReleased(InputEvents ie)
        {
            if (vesselStatus.AutoPilotEnabled)
                return;
            if (_suppressedInputs.Contains(ie)) return;

            if (IsSpawned && IsOwner)
            {
                SendButtonReleased_ServerRpc(ie, PressedDevice(ie));
            }
            else
            {
                StopShipControllerActions(ie); 
            }
            
            OnInputEventStopped?.Invoke(ie);
        }

        [ServerRpc]
        private void SendButtonReleased_ServerRpc(InputEvents ie, byte device)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                SendButtonReleased_ClientRpc(ie, device);
            }
        }

        [ClientRpc]
        void SendButtonReleased_ClientRpc(InputEvents ie, byte device)
        {
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto())
            {
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
                StopPressedActions(ie, device);
            }
        }

        #region Mute Input

        /// <summary>
        /// Blanket on/off gate for one input event — unlike <see cref="MuteInput"/> there is no
        /// timer; the caller owns the release. Used by the Quest Graph flight school to disable
        /// the action buttons (A/X/B) while only sticks and triggers are being taught. Gated at
        /// press AND release; engage while the vessel is idle (e.g. right after a transition
        /// blend) so no held action is left running.
        /// </summary>
        public void SetInputSuppressed(InputEvents ie, bool suppressed)
        {
            if (suppressed) _suppressedInputs.Add(ie);
            else _suppressedInputs.Remove(ie);
        }

        /// <summary>Release every suppression set via <see cref="SetInputSuppressed"/>.</summary>
        public void ClearSuppressedInputs() => _suppressedInputs.Clear();

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

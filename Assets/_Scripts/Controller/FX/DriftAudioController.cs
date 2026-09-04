using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Drives an FMOD drift SFX event for a single vessel (Squirrel by
    /// default - racing/drift class). The event exposes one parameter
    /// ("Drift Amount" by default):
    ///
    ///   0 = single drift trigger held
    ///   1 = both drift triggers held
    ///
    /// Lifecycle:
    ///   - Drift START (IsDrifting goes true): create + start the FMOD
    ///     event instance, push the current trigger-driven amount.
    ///   - Drift HOLD: push the smoothed amount each frame (0 single, 1
    ///     double). The two trigger sources come from
    ///     IInputStatus.LeftTriggerAnalog / RightTriggerAnalog above
    ///     <see cref="triggerDeadzone"/>.
    ///   - Drift END (IsDrifting goes false): fire the optional one-shot
    ///     <see cref="releaseEvent"/> ("trigger off" SFX) and, if
    ///     <see cref="driveParamToOneOnRelease"/> is true, also drive the
    ///     parameter to 1 to play any 'trigger let go' stage inside the
    ///     main drift event. The main drift instance is held alive for
    ///     <see cref="releaseHoldSeconds"/> so any in-event tail can
    ///     finish, then stopped + released. Internal state resets so the
    ///     next drift starts the event fresh from 0.
    ///
    /// Attach this to the same GameObject as the vessel root (the one
    /// that owns IVesselStatus and the InputController). Recommended for
    /// the Squirrel vessel prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class DriftAudioController : MonoBehaviour
    {
        enum AttachMode { None, Ship, Listener }

        enum DriftPhase
        {
            /// <summary>No event playing, waiting for IsDrifting to go true.</summary>
            Idle,
            /// <summary>Event playing, drift_amount tracks live trigger state.</summary>
            Active,
            /// <summary>Drift just ended - ramping drift_amount to 1 ("let go"), event still playing.</summary>
            Releasing
        }

        [Header("FMOD Event")]
        [SerializeField, Tooltip(
            "FMOD drift event. Should be the looping drift SFX (the one " +
            "wired up under SFX/drifts in the FMOD project). Plays only " +
            "while the vessel is drifting; stopped on drift end after the " +
            "release tail.")]
        EventReference driftEvent;

        [SerializeField, Tooltip(
            "FMOD parameter name on the drift event that takes 0..1: " +
            "0 = single drift trigger held, 1 = both triggers held. On " +
            "release this parameter is optionally driven to 1 to play any " +
            "'trigger let go' stage inside the main drift event (see " +
            "driveParamToOneOnRelease).")]
        string driftAmountParameterName = "Drift Amount";

        [SerializeField, Tooltip(
            "One-shot FMOD event fired the moment drift ends (when both " +
            "triggers are released). Typically the 'trigger off' release " +
            "SFX. Optional - leave unassigned if the main drift event " +
            "handles its own release tail. The one-shot is fired and " +
            "released independently of the main drift instance, so it " +
            "always plays to completion even after the drift event has " +
            "stopped.")]
        EventReference releaseEvent;

        [SerializeField, Tooltip(
            "When true, the one-shot release event is attached to the " +
            "ship transform so it spatialises with the vessel. When false " +
            "(or no listener attachment is needed), it plays as a one-shot " +
            "at the ship's current world position. Has no effect if no " +
            "releaseEvent is assigned.")]
        bool attachReleaseEventToShip = true;

        [SerializeField, Tooltip(
            "When true, the main drift event's drift_amount parameter is " +
            "ramped to 1 during the release phase, in addition to firing " +
            "the one-shot releaseEvent above. Off by default once a " +
            "releaseEvent is assigned - the dedicated 'trigger off' event " +
            "should fully cover the let-go SFX. Turn on if your main " +
            "drift event also has a trigger-let-go stage gated at " +
            "drift_amount = 1.")]
        bool driveParamToOneOnRelease = false;

        [Header("Trigger Mapping")]
        [SerializeField, Range(0f, 1f), Tooltip(
            "Analog deadzone for each drift trigger. Trigger reads above " +
            "this count as 'pressed' for the purpose of single-vs-double " +
            "drift detection. Matches GamepadInputStrategy's trigger " +
            "deadzone.")]
        float triggerDeadzone = 0.05f;

        [SerializeField, Tooltip(
            "When true, the drift_amount parameter scales smoothly with " +
            "the analog trigger values: a half-pressed second trigger " +
            "drags the amount partway to 1, instead of a binary single/" +
            "double snap. Off by default - the FMOD design treats 0 and " +
            "1 as discrete states.")]
        bool useAnalogScaling = false;

        [SerializeField, Range(0f, 30f), Tooltip(
            "Exponential smoothing rate for the drift_amount parameter " +
            "during the active phase. 0 = snap, ~8-12 = quick but smooth. " +
            "Prevents zipper noise when the second trigger is tapped.")]
        float amountSmoothing = 10f;

        [Header("Release / 'Trigger Let Go'")]
        [SerializeField, Range(0f, 30f), Tooltip(
            "Smoothing rate for the parameter ramp-to-1 during the " +
            "release phase. Higher = the parameter snaps to 1 faster on " +
            "drift end. The ramp runs in parallel with the release hold " +
            "timer below.")]
        float releaseSmoothing = 20f;

        [SerializeField, Range(0f, 5f), Tooltip(
            "How long (seconds) to keep the FMOD event alive after drift " +
            "ends, with drift_amount driven to 1. This is the window for " +
            "the 'trigger let go' tail in the FMOD event to play before " +
            "the instance is stopped + released. After this, internal " +
            "state resets so the next drift starts the event fresh.")]
        float releaseHoldSeconds = 0.4f;

        [Header("Spatialisation")]
        [SerializeField, Tooltip(
            "When true, drift audio is only created when this client is " +
            "the controlling user of the vessel. Remote and AI vessels " +
            "stay silent on this client. Mirrors ShipAudioController.")]
        bool onlyAudibleToController = true;

        [SerializeField, Tooltip(
            "Force the instance to attach to the FMOD listener instead " +
            "of the ship transform - keeps the drift always audible " +
            "regardless of camera distance. No effect when " +
            "onlyAudibleToController is true (since only local vessels " +
            "create instances anyway).")]
        bool forceAttachToListener = false;

        [Header("SFX Volume")]
        [SerializeField, Tooltip(
            "When true, the FMOD drift instance volume is multiplied by " +
            "GameSetting.SFXLevel and muted by SFXEnabled, just like " +
            "ShipAudioController's engine track.")]
        bool tieVolumeToSFXSlider = true;

        [SerializeField, Range(0f, 2f), Tooltip(
            "Per-vessel volume trim applied on top of the SFX slider. " +
            "1 = no change.")]
        float baseVolumeMultiplier = 1f;

        [Header("Vessel Gating")]
        [SerializeField, Tooltip(
            "When true, this controller only runs for the configured " +
            "vessel class (Squirrel by default - racing/drift). Other " +
            "vessel classes never spawn the drift event. Turn off if you " +
            "want every vessel to use the same drift SFX.")]
        bool restrictToVesselClass = true;

        [SerializeField, Tooltip(
            "Vessel class this controller activates for. Only consulted " +
            "when restrictToVesselClass is true. Defaults to Squirrel.")]
        VesselClassType targetVesselClass = VesselClassType.Squirrel;

        [Header("Debug")]
        [SerializeField] bool debugLog = false;

        // Runtime state
        IVesselStatus _status;
        StudioListener _listener;
        EventInstance _instance;
        PARAMETER_ID _amountParamId;
        bool _hasAmountParam;
        bool _amountParamIsGlobal;
        bool _instanceStarted;
        AttachMode _attachMode;
        DriftPhase _phase = DriftPhase.Idle;
        float _smoothedAmount;
        float _releaseTimer;
        bool _classGateChecked;
        bool _classGatePass;

        void Awake()
        {
            _status = GetComponent<IVesselStatus>();
        }

        void OnEnable()
        {
            if (_instanceStarted && _instance.isValid())
                _instance.setPaused(false);

            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel += OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus += OnSFXEnabledChanged;
                ApplySFXVolume();
            }
        }

        void OnDisable()
        {
            if (_instanceStarted && _instance.isValid())
                _instance.setPaused(true);

            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel -= OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus -= OnSFXEnabledChanged;
            }
        }

        void OnDestroy()
        {
            StopAndRelease(stopMode: FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        void Update()
        {
            if (_status == null) return;

            // One-time gating: only Squirrel (or whatever class is set).
            if (!_classGateChecked)
            {
                _classGateChecked = true;
                _classGatePass = !restrictToVesselClass || _status.VesselType == targetVesselClass;
                if (!_classGatePass && debugLog)
                {
                    Debug.Log(
                        $"[DriftAudioController] '{name}' vessel class is " +
                        $"{_status.VesselType}, not {targetVesselClass} - disabling.",
                        this);
                }
            }
            if (!_classGatePass) return;

            // Local-user gating: skip remote/AI vessels in default mode. We
            // can't decide until Player is set, so retry each frame until
            // either we know it's remote (and skip forever) or we know it's
            // local and proceed.
            if (onlyAudibleToController && !forceAttachToListener)
            {
                if (_status.Player == null) return;
                if (!_status.IsLocalUser)
                {
                    enabled = false;
                    if (debugLog)
                        Debug.Log($"[DriftAudioController] '{name}' is remote/AI; disabling.", this);
                    return;
                }
            }

            float dt = Time.deltaTime;
            bool drifting = _status.IsDrifting;

            switch (_phase)
            {
                case DriftPhase.Idle:
                    if (drifting)
                        BeginActive();
                    break;

                case DriftPhase.Active:
                    if (drifting)
                        TickActive(dt);
                    else
                        BeginRelease();
                    break;

                case DriftPhase.Releasing:
                    TickRelease(dt);

                    // If the player re-engages drift mid-release, restart cleanly.
                    if (drifting)
                    {
                        StopAndRelease(stopMode: FMOD.Studio.STOP_MODE.IMMEDIATE);
                        BeginActive();
                    }
                    break;
            }
        }

        void BeginActive()
        {
            if (driftEvent.IsNull)
            {
                Debug.LogError($"[DriftAudioController] '{name}' has no Drift Event assigned.", this);
                return;
            }

            // FmodSafe: a missing event is reported once; a failed create leaves us Idle and the
            // next drift retries silently rather than throwing in Update().
            if (!FmodSafe.TryCreateInstance(driftEvent, out _instance, this))
            {
                _phase = DriftPhase.Idle;
                return;
            }

            // Resolve the drift_amount parameter.
            _hasAmountParam = false;
            if (_instance.getDescription(out EventDescription desc) == FMOD.RESULT.OK &&
                !string.IsNullOrEmpty(driftAmountParameterName) &&
                desc.getParameterDescriptionByName(driftAmountParameterName, out PARAMETER_DESCRIPTION amountDesc) == FMOD.RESULT.OK)
            {
                _amountParamId = amountDesc.id;
                _amountParamIsGlobal = (amountDesc.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                _hasAmountParam = true;
            }
            else
            {
                Debug.LogWarning(
                    $"[DriftAudioController] Event '{driftEvent}' has no parameter " +
                    $"named '{driftAmountParameterName}'. Drift will play but " +
                    $"single/double/let-go states won't drive it.",
                    this);
            }

            // Initial amount: snap to current trigger state, no smoothing
            // ramp-up - designers expect drift SFX to start at the right
            // intensity instantly.
            _smoothedAmount = ComputeTargetAmount();
            PushAmount(_smoothedAmount);

            // Spatialisation.
            AttachToTarget();

            ApplySFXVolume();

            var startResult = _instance.start();
            _instanceStarted = startResult == FMOD.RESULT.OK;
            if (!_instanceStarted)
            {
                Debug.LogError(
                    $"[DriftAudioController] '{name}' start() returned {startResult} on '{driftEvent}'. " +
                    $"Drift SFX won't play.",
                    this);
                _instance.release();
                _instance.clearHandle();
                _phase = DriftPhase.Idle;
                return;
            }

            _phase = DriftPhase.Active;
            _releaseTimer = 0f;

            if (debugLog)
                Debug.Log($"[DriftAudioController] '{name}' drift START (amount={_smoothedAmount:F2}).", this);
        }

        void TickActive(float dt)
        {
            if (!_instanceStarted || !_instance.isValid()) return;

            float target = ComputeTargetAmount();
            float alpha = amountSmoothing > 0f ? 1f - Mathf.Exp(-dt * amountSmoothing) : 1f;
            _smoothedAmount = Mathf.Lerp(_smoothedAmount, target, alpha);
            PushAmount(_smoothedAmount);
        }

        void BeginRelease()
        {
            _phase = DriftPhase.Releasing;
            _releaseTimer = 0f;

            // Fire the one-shot 'trigger off' event independently. Done
            // here (not in TickRelease) so it triggers exactly once per
            // drift cycle on the rising edge of release.
            FireReleaseOneShot();

            if (debugLog)
                Debug.Log(
                    $"[DriftAudioController] '{name}' drift END - fired " +
                    $"trigger-off one-shot" +
                    (driveParamToOneOnRelease
                        ? $" + ramping drift_amount to 1 over {releaseHoldSeconds:F2}s"
                        : "") + ".",
                    this);
        }

        /// <summary>
        /// Plays the configured release one-shot event ('trigger off') at
        /// the ship's position with the SFX slider volume applied.
        /// Independent of the main drift instance - FMOD owns its lifetime
        /// and frees the instance after playback finishes.
        ///
        /// Routed through <see cref="FMODOneShotVolumeHelper"/> rather than
        /// <see cref="RuntimeManager.PlayOneShotAttached(EventReference, GameObject)"/>
        /// because the latter has no per-instance setVolume() seam and so
        /// would ignore <c>GameSetting.SFXLevel</c>. The helper short-
        /// circuits when <see cref="ResolveSFXVolume"/> returns 0, covering
        /// both the muted case and the slider-at-zero case in one path.
        /// </summary>
        void FireReleaseOneShot()
        {
            float volume = ResolveSFXVolume();

            if (attachReleaseEventToShip)
                FMODOneShotVolumeHelper.PlaySFXOneShotAttached(releaseEvent, gameObject, volume);
            else
                FMODOneShotVolumeHelper.PlaySFXOneShot(releaseEvent, transform.position, volume);
        }

        void TickRelease(float dt)
        {
            if (!_instanceStarted || !_instance.isValid())
            {
                ResetAfterRelease();
                return;
            }

            // Optionally ramp the parameter toward 1 - only relevant if
            // the main drift event itself has a 'trigger let go' stage at
            // drift_amount = 1. Off by default since the dedicated
            // releaseEvent one-shot covers the let-go SFX.
            if (driveParamToOneOnRelease)
            {
                float alpha = releaseSmoothing > 0f ? 1f - Mathf.Exp(-dt * releaseSmoothing) : 1f;
                _smoothedAmount = Mathf.Lerp(_smoothedAmount, 1f, alpha);
                PushAmount(_smoothedAmount);
            }

            _releaseTimer += dt;
            if (_releaseTimer >= releaseHoldSeconds)
            {
                if (driveParamToOneOnRelease)
                {
                    // Force the parameter to exactly 1 right before stopping
                    // so FMOD definitely sees the let-go state at the end.
                    PushAmount(1f);
                }
                StopAndRelease(stopMode: FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                ResetAfterRelease();
            }
        }

        void ResetAfterRelease()
        {
            _phase = DriftPhase.Idle;
            _smoothedAmount = 0f;
            _releaseTimer = 0f;

            if (debugLog)
                Debug.Log($"[DriftAudioController] '{name}' drift RESET - ready for next drift.", this);
        }

        /// <summary>
        /// Computes the target drift_amount value from current trigger
        /// state. Default behaviour: 0 if exactly one trigger is held, 1
        /// if both. With <see cref="useAnalogScaling"/> on, the smaller of
        /// the two trigger analog values is used as the amount, so a
        /// half-pressed second trigger drags the parameter partway to 1.
        /// </summary>
        float ComputeTargetAmount()
        {
            var input = _status?.InputStatus;
            if (input == null) return 0f;

            float left = input.LeftTriggerAnalog;
            float right = input.RightTriggerAnalog;

            bool leftActive = left > triggerDeadzone;
            bool rightActive = right > triggerDeadzone;

            if (useAnalogScaling)
            {
                // Single trigger: amount = 0. Both triggers: amount scales
                // with the secondary (smaller) trigger so half-press lands
                // mid-curve.
                if (leftActive && rightActive)
                    return Mathf.Clamp01(Mathf.Min(left, right));
                if (leftActive || rightActive)
                    return 0f;
                return 0f;
            }

            // Discrete: 0 = single, 1 = both.
            if (leftActive && rightActive) return 1f;
            return 0f;
        }

        void PushAmount(float value)
        {
            if (!_hasAmountParam || !_instance.isValid()) return;
            float v = Mathf.Clamp01(value);
            _instance.setParameterByID(_amountParamId, v, _amountParamIsGlobal);
        }

        void AttachToTarget()
        {
            if (!_instance.isValid()) return;

            Transform target = transform;
            AttachMode mode = AttachMode.Ship;

            if (forceAttachToListener)
            {
                var listenerTf = GetListenerTransform();
                if (listenerTf != null)
                {
                    target = listenerTf;
                    mode = AttachMode.Listener;
                }
            }

            FmodSafe.Attach(_instance, target.gameObject);
            _attachMode = mode;
        }

        Transform GetListenerTransform()
        {
            if (_listener == null)
                _listener = Object.FindAnyObjectByType<StudioListener>();
            return _listener != null ? _listener.transform : null;
        }

        void StopAndRelease(FMOD.Studio.STOP_MODE stopMode)
        {
            FmodSafe.StopAndRelease(ref _instance, _instanceStarted, stopMode);
            _instanceStarted = false;
            _hasAmountParam = false;
            _attachMode = AttachMode.None;
        }

        void ApplySFXVolume()
        {
            if (!_instance.isValid()) return;
            _instance.setVolume(ResolveSFXVolume());
        }

        float ResolveSFXVolume()
        {
            if (!tieVolumeToSFXSlider)
                return Mathf.Clamp(baseVolumeMultiplier, 0f, AudioVolumeMath.MaxBaseMultiplier);
            return AudioSystem.ResolveSfxInstanceVolume(baseVolumeMultiplier);
        }

        void OnSFXLevelChanged(float level) => ApplySFXVolume();
        void OnSFXEnabledChanged(bool enabled) => ApplySFXVolume();
    }
}

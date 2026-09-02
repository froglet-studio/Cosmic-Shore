using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using FMOD.Studio;
using FMODUnity;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Drives an FMOD SFX layer for the Squirrel's proximity-speed gain - the
    /// boost the vessel earns when its <see cref="Skimmer"/> is in proximity
    /// with crystals or with another ship's trail prisms. Both proximity
    /// sources funnel through <see cref="ScriptableEventBoostChanged"/>:
    /// trail-prism contact via <c>SkimmerBoostPrismEffectSO</c>, crystal
    /// contact via the skimmer-crystal effects pipeline. This component
    /// listens to that single SOAP channel and translates it into FMOD calls.
    ///
    /// Two FMOD events are supported (each optional):
    ///
    ///   - <see cref="boostTickEvent"/>: one-shot, fired on every rising
    ///     edge of <c>BoostMultiplier</c>. Use for the per-impact "click /
    ///     pickup" SFX so each successful skim is audible.
    ///
    ///   - <see cref="boostLoopEvent"/>: looping instance whose
    ///     <see cref="boostAmountParameterName"/> parameter tracks the
    ///     normalised boost amount (0 = base, 1 = max). The instance is
    ///     created lazily on the first rising edge and stopped + released
    ///     when the multiplier decays back to base. Use for a continuous
    ///     "speed surge" / wind / engine-strain layer that intensifies with
    ///     accumulated proximity hits.
    ///
    /// Lifecycle / gating mirrors <see cref="DriftAudioController"/>:
    ///   - Only runs for the local user's vessel
    ///     (<see cref="onlyAudibleToController"/>).
    ///   - Optionally restricted to a single vessel class (Squirrel by
    ///     default - racing/drift class).
    ///   - Loop instance volume is tied to <see cref="GameSetting.SFXLevel"/>
    ///     and muted by <c>SFXEnabled</c>.
    ///   - Loop instance is paused on disable, resumed on enable, and
    ///     released on destroy.
    ///
    /// Attach to the same GameObject as the vessel root (the one that owns
    /// <c>IVesselStatus</c>). Recommended for the Squirrel vessel prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProximityBoostAudioController : MonoBehaviour
    {
        enum AttachMode { None, Ship, Listener }

        [Header("FMOD Events")]
        [SerializeField, Tooltip(
            "Optional one-shot FMOD event fired every time the vessel's " +
            "BoostMultiplier rises (i.e. every successful skim - crystal " +
            "vacuum or trail-prism contact). Leave unassigned to suppress " +
            "per-tick SFX and rely solely on the boost loop layer below.")]
        EventReference boostTickEvent;

        [SerializeField, Tooltip(
            "When true, the one-shot tick event is attached to this ship " +
            "transform so it spatialises with the vessel. When false it " +
            "plays as a one-shot at the ship's current world position. " +
            "Has no effect if no boostTickEvent is assigned.")]
        bool attachTickEventToShip = true;

        [SerializeField, Tooltip(
            "Optional looping FMOD event whose 0..1 parameter tracks the " +
            "normalised boost amount. Created lazily on the first rising " +
            "edge of BoostMultiplier and stopped + released when boost " +
            "decays back to base. Leave unassigned to skip the loop layer.")]
        EventReference boostLoopEvent;

        [SerializeField, Tooltip(
            "FMOD parameter name on the loop event that takes 0..1: " +
            "0 = base multiplier, 1 = max multiplier. Smoothed with " +
            "boostAmountSmoothing below to avoid zipper noise on rapid " +
            "skim ticks.")]
        string boostAmountParameterName = "Boost Amount";

        [Header("Boost Range (Shared Config)")]
        [SerializeField, Tooltip(
            "Shared SOAP variable for the base (no-boost) multiplier. " +
            "Should reference the same asset wired into " +
            "SkimmerBoostPrismEffectSO and SquirrelVesselHUDController so " +
            "the audio normalisation matches the gameplay clamp exactly.")]
        ScriptableVariable<float> boostBaseMultiplier;

        [SerializeField, Tooltip(
            "Shared SOAP variable for the maximum boost multiplier. " +
            "Should reference the same asset wired into " +
            "SkimmerBoostPrismEffectSO and SquirrelVesselHUDController.")]
        ScriptableVariable<float> boostMaxMultiplier;

        [Header("Boost Changed Event")]
        [SerializeField, Tooltip(
            "SOAP event raised whenever the vessel's BoostMultiplier " +
            "changes - emitted by SkimmerBoostPrismEffectSO and any other " +
            "skimmer effect that grants proximity speed. This is the " +
            "single source of truth this controller listens on; both the " +
            "trail-proximity and crystal-proximity boosts come through here.")]
        ScriptableEventBoostChanged boostChanged;

        [Header("Tick Detection")]
        [SerializeField, Range(0f, 1f), Tooltip(
            "Minimum increase in raw BoostMultiplier (in absolute units, " +
            "not normalised) to count as a 'rising edge' worth firing the " +
            "boost tick one-shot. Stops decay-frame jitter or rounding " +
            "from re-triggering ticks. Should be set just below " +
            "SkimmerBoostPrismEffectSO.addPerHit so a single skim still " +
            "registers.")]
        float tickEpsilon = 0.02f;

        [Header("Loop Smoothing")]
        [SerializeField, Range(0f, 30f), Tooltip(
            "Exponential smoothing rate for the boost amount parameter on " +
            "the loop event. 0 = snap to target every event, ~8-12 = quick " +
            "but smooth ramp between consecutive skim ticks.")]
        float boostAmountSmoothing = 12f;

        [SerializeField, Range(0f, 1f), Tooltip(
            "Normalised boost amount below which the loop event is " +
            "considered 'returned to rest' and stopped + released. Should " +
            "be a hair above 0 so floating-point decay close to base does " +
            "not keep the loop alive forever.")]
        float loopStopThreshold = 0.01f;

        [Header("Spatialisation")]
        [SerializeField, Tooltip(
            "When true, this controller is only created when this client " +
            "is the controlling user of the vessel. Remote and AI vessels " +
            "stay silent on this client. Mirrors ShipAudioController and " +
            "DriftAudioController.")]
        bool onlyAudibleToController = true;

        [SerializeField, Tooltip(
            "Force the loop instance to attach to the FMOD listener " +
            "instead of the ship transform - keeps it always audible " +
            "regardless of camera distance. No effect when " +
            "onlyAudibleToController is true (since only local vessels " +
            "create instances anyway).")]
        bool forceLoopAttachToListener = false;

        [Header("SFX Volume")]
        [SerializeField, Tooltip(
            "When true, the loop instance volume is multiplied by " +
            "GameSetting.SFXLevel and muted by SFXEnabled, just like " +
            "ShipAudioController and DriftAudioController.")]
        bool tieVolumeToSFXSlider = true;

        [SerializeField, Range(0f, 2f), Tooltip(
            "Per-vessel volume trim applied on top of the SFX slider. " +
            "1 = no change.")]
        float baseVolumeMultiplier = 1f;

        [Header("Vessel Gating")]
        [SerializeField, Tooltip(
            "When true, this controller only runs for the configured " +
            "vessel class (Squirrel by default - racing/drift). Other " +
            "vessel classes never react to the boost SOAP event. Turn " +
            "off if you want every vessel that earns proximity boost to " +
            "use the same SFX.")]
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

        EventInstance _loopInstance;
        PARAMETER_ID _amountParamId;
        bool _hasAmountParam;
        bool _amountParamIsGlobal;
        bool _loopStarted;
        AttachMode _loopAttachMode;
        float _smoothedAmount;

        bool _classGateChecked;
        bool _classGatePass;
        bool _localGatePass;
        bool _localGateResolved;
        bool _subscribed;

        // Last raw multiplier we observed - used to detect rising edges
        // independent of normalisation, so we don't miss a tick if base /
        // max change at runtime.
        float _lastRawMultiplier = float.NaN;

        void Awake()
        {
            _status = GetComponent<IVesselStatus>();
        }

        void OnEnable()
        {
            // Fail-loud per CLAUDE.md SOAP policy: a missing boostChanged
            // reference will NPE here, which is the desired surface.
            boostChanged.OnRaised += HandleBoostChanged;
            _subscribed = true;

            if (_loopStarted && _loopInstance.isValid())
                _loopInstance.setPaused(false);

            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel += OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus += OnSFXEnabledChanged;
                ApplySFXVolume();
            }
        }

        void OnDisable()
        {
            if (_subscribed)
            {
                boostChanged.OnRaised -= HandleBoostChanged;
                _subscribed = false;
            }

            if (_loopStarted && _loopInstance.isValid())
                _loopInstance.setPaused(true);

            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel -= OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus -= OnSFXEnabledChanged;
            }
        }

        void OnDestroy()
        {
            StopAndReleaseLoop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        void Update()
        {
            if (_status == null) return;

            ResolveGates();
            if (!_classGatePass || !_localGatePass) return;

            if (!_loopStarted || !_loopInstance.isValid()) return;

            // Smooth the parameter toward the latest target each frame so
            // rapid bursts of skim ticks don't produce stepwise zipper
            // artefacts on the boost loop.
            float dt = Time.deltaTime;
            float target = ComputeNormalisedAmount(_status.BoostMultiplier);
            float alpha = boostAmountSmoothing > 0f
                ? 1f - Mathf.Exp(-dt * boostAmountSmoothing)
                : 1f;
            _smoothedAmount = Mathf.Lerp(_smoothedAmount, target, alpha);
            PushAmount(_smoothedAmount);

            // If the smoothed amount has decayed back to (effectively) base,
            // stop the loop. The next rising edge will recreate it.
            if (target <= loopStopThreshold && _smoothedAmount <= loopStopThreshold)
            {
                if (debugLog)
                    Debug.Log($"[ProximityBoostAudio] '{name}' boost decayed to base - stopping loop.", this);
                StopAndReleaseLoop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
        }

        void ResolveGates()
        {
            if (!_classGateChecked)
            {
                _classGateChecked = true;
                _classGatePass = !restrictToVesselClass || _status.VesselType == targetVesselClass;
                if (!_classGatePass && debugLog)
                {
                    Debug.Log(
                        $"[ProximityBoostAudio] '{name}' vessel class is " +
                        $"{_status.VesselType}, not {targetVesselClass} - disabling.",
                        this);
                }
            }
            if (!_classGatePass) return;

            if (_localGateResolved) return;

            // Player can be null until VesselController.Initialize() finishes,
            // so we re-evaluate every frame until we know the answer.
            if (!onlyAudibleToController || forceLoopAttachToListener)
            {
                _localGatePass = true;
                _localGateResolved = true;
                return;
            }

            if (_status.Player == null) return;

            if (_status.IsLocalUser)
            {
                _localGatePass = true;
                _localGateResolved = true;
            }
            else
            {
                _localGatePass = false;
                _localGateResolved = true;
                if (debugLog)
                    Debug.Log($"[ProximityBoostAudio] '{name}' is remote/AI; disabling.", this);
                enabled = false;
            }
        }

        void HandleBoostChanged(BoostChangedPayload payload)
        {
            if (_status == null) return;

            ResolveGates();
            if (!_classGatePass || !_localGatePass) return;

            // The SOAP event is global; every vessel (incl. the remote owner's
            // per-frame DecayBoost) raises it. Filter by source-vessel identity so
            // we only react to our own vessel's boost. Robust where two vessels
            // momentarily share a multiplier - the old multiplier-match filter
            // mis-fired in that case.
            if (payload.VesselStatus != null && payload.VesselStatus != _status) return;

            // Rising edge → fire the per-skim tick one-shot. We compare
            // against the last observed raw multiplier so this is robust to
            // base/max changes at runtime.
            float prev = float.IsNaN(_lastRawMultiplier) ? ResolveBase() : _lastRawMultiplier;
            bool rising = payload.BoostMultiplier - prev >= tickEpsilon;
            _lastRawMultiplier = payload.BoostMultiplier;

            if (rising)
            {
                FireTickOneShot();
                EnsureLoopStarted();

                if (debugLog)
                    Debug.Log(
                        $"[ProximityBoostAudio] '{name}' tick - mult {prev:F2} → {payload.BoostMultiplier:F2} " +
                        $"(norm {ComputeNormalisedAmount(payload.BoostMultiplier):F2}).",
                        this);
            }

            // Snap target so Update() can smooth toward it. Initial value
            // also seeds the smoothed parameter on first start so the loop
            // doesn't fade in from 0.
            float normTarget = ComputeNormalisedAmount(payload.BoostMultiplier);
            if (rising && _loopStarted)
                _smoothedAmount = Mathf.Max(_smoothedAmount, normTarget * 0.5f);
        }

        /// <summary>
        /// Plays the configured tick one-shot event at the ship's position
        /// with the SFX slider volume applied. Independent of the loop
        /// instance - FMOD owns its lifetime and frees the instance after
        /// playback finishes.
        ///
        /// Routed through <see cref="FMODOneShotVolumeHelper"/> rather than
        /// <see cref="RuntimeManager.PlayOneShotAttached(EventReference, GameObject)"/>
        /// because the latter has no per-instance setVolume() seam and so
        /// would ignore <c>GameSetting.SFXLevel</c>. The helper short-
        /// circuits when <see cref="ResolveSFXVolume"/> returns 0, covering
        /// both the muted case and the slider-at-zero case in one path.
        /// </summary>
        void FireTickOneShot()
        {
            float volume = ResolveSFXVolume();

            if (attachTickEventToShip)
                FMODOneShotVolumeHelper.PlaySFXOneShotAttached(boostTickEvent, gameObject, volume);
            else
                FMODOneShotVolumeHelper.PlaySFXOneShot(boostTickEvent, transform.position, volume);
        }

        void EnsureLoopStarted()
        {
            if (boostLoopEvent.IsNull) return;
            if (_loopStarted && _loopInstance.isValid()) return;

            if (!FmodSafe.TryCreateInstance(boostLoopEvent, out _loopInstance, this))
                return;

            // Resolve the boost amount parameter.
            _hasAmountParam = false;
            if (_loopInstance.getDescription(out EventDescription desc) == FMOD.RESULT.OK &&
                !string.IsNullOrEmpty(boostAmountParameterName) &&
                desc.getParameterDescriptionByName(boostAmountParameterName, out PARAMETER_DESCRIPTION amountDesc) == FMOD.RESULT.OK)
            {
                _amountParamId = amountDesc.id;
                _amountParamIsGlobal = (amountDesc.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                _hasAmountParam = true;
            }
            else
            {
                Debug.LogWarning(
                    $"[ProximityBoostAudio] Event '{boostLoopEvent}' has no parameter " +
                    $"named '{boostAmountParameterName}'. Loop will play but " +
                    $"intensity won't drive it.",
                    this);
            }

            // Seed smoothed amount with a small headroom value so the loop
            // is audibly engaged on the first skim instead of fading in
            // from silence.
            float initial = Mathf.Max(_smoothedAmount, ComputeNormalisedAmount(_status.BoostMultiplier) * 0.5f);
            _smoothedAmount = initial;
            PushAmount(_smoothedAmount);

            AttachLoopToTarget();
            ApplySFXVolume();

            var startResult = _loopInstance.start();
            _loopStarted = startResult == FMOD.RESULT.OK;
            if (!_loopStarted)
            {
                Debug.LogError(
                    $"[ProximityBoostAudio] '{name}' start() returned {startResult} on '{boostLoopEvent}'. " +
                    $"Boost loop SFX won't play.",
                    this);
                _loopInstance.release();
                _loopInstance.clearHandle();
                return;
            }

            if (debugLog)
                Debug.Log($"[ProximityBoostAudio] '{name}' boost loop START (amount={_smoothedAmount:F2}).", this);
        }

        float ComputeNormalisedAmount(float rawMultiplier)
        {
            float baseMult = ResolveBase();
            float maxMult = ResolveMax(baseMult);

            if (maxMult - baseMult <= 0.0001f) return 0f;
            return Mathf.Clamp01((rawMultiplier - baseMult) / (maxMult - baseMult));
        }

        float ResolveBase()
        {
            float v = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            return Mathf.Max(0.0001f, v);
        }

        float ResolveMax(float baseMult)
        {
            float v = boostMaxMultiplier != null ? boostMaxMultiplier.Value : 5f;
            return Mathf.Max(baseMult, v);
        }

        void PushAmount(float value)
        {
            if (!_hasAmountParam || !_loopInstance.isValid()) return;
            float v = Mathf.Clamp01(value);
            _loopInstance.setParameterByID(_amountParamId, v, _amountParamIsGlobal);
        }

        void AttachLoopToTarget()
        {
            if (!_loopInstance.isValid()) return;

            Transform target = transform;
            AttachMode mode = AttachMode.Ship;

            if (forceLoopAttachToListener)
            {
                var listenerTf = GetListenerTransform();
                if (listenerTf != null)
                {
                    target = listenerTf;
                    mode = AttachMode.Listener;
                }
            }

            FmodSafe.Attach(_loopInstance, target.gameObject);
            _loopAttachMode = mode;
        }

        Transform GetListenerTransform()
        {
            if (_listener == null)
                _listener = Object.FindAnyObjectByType<StudioListener>();
            return _listener != null ? _listener.transform : null;
        }

        void StopAndReleaseLoop(FMOD.Studio.STOP_MODE stopMode)
        {
            FmodSafe.StopAndRelease(ref _loopInstance, _loopStarted, stopMode);
            _loopStarted = false;
            _hasAmountParam = false;
            _loopAttachMode = AttachMode.None;
            _smoothedAmount = 0f;
        }

        void ApplySFXVolume()
        {
            if (!_loopInstance.isValid()) return;
            _loopInstance.setVolume(ResolveSFXVolume());
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

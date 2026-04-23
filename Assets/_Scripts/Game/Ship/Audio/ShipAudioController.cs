using System.Collections.Generic;
using CosmicShore.Core;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Game.Audio
{
    /// <summary>
    /// Drives the FMOD "space ship engine main" loop for a single vessel.
    ///
    /// Usage:
    ///   - Attach to the vessel root prefab (same GameObject as the Vessel /
    ///     IVesselStatus components).
    ///   - Assign <see cref="engineEvent"/> to "event:/space ship engine main"
    ///     in the inspector.
    ///
    /// Velocity source:
    ///   Cosmic Shore vessels are moved via transform.position, not a
    ///   Rigidbody. This component measures world-space velocity each frame
    ///   as (position - lastPosition) / deltaTime, smooths it, normalises it
    ///   against <see cref="maxShipVelocity"/>, and pushes the result into
    ///   the event's "Speed" parameter.
    ///
    /// Spatialisation:
    ///   - By default (<see cref="onlyAudibleToController"/> = true) the
    ///     engine audio is ONLY created for the ship the current client is
    ///     controlling. Remote ships and AI ships never instantiate the
    ///     FMOD event on this client, so other players' ships are silent
    ///     here. This matches the intent "ship audio is only heard by the
    ///     person controlling the ship".
    ///   - If <see cref="onlyAudibleToController"/> is disabled, the
    ///     legacy behaviour applies: remote ships attach to their own
    ///     transform (normal 3D attenuation), and the LOCAL player's ship
    ///     attaches to the FMOD StudioListener instead, so the instance's
    ///     3D position is always the listener's position. That makes it
    ///     effectively 2D -> always audible, even when the camera pulls
    ///     away from the ship.
    ///   - If the local-vs-remote check fails (e.g. Player not set), the
    ///     instance falls back to attaching to the ship transform.
    ///
    /// Ownership is not guaranteed to be known at Start() (VesselController
    /// assigns the Player after Initialize()), so instance creation is
    /// deferred until we can resolve local-vs-remote. See
    /// <see cref="TryEvaluateAndCreate"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShipAudioController : MonoBehaviour
    {
        enum AttachMode
        {
            /// <summary>No routing decision yet (instance freshly created).</summary>
            None,
            /// <summary>Attached to this ship's transform (3D spatialisation).</summary>
            Ship,
            /// <summary>Attached to the listener's transform (effectively 2D / always audible).</summary>
            Listener
        }

        [Header("FMOD Event")]
        [SerializeField, Tooltip("FMOD event for the looping ship engine.")]
        EventReference engineEvent;

        [SerializeField, Tooltip(
            "Extra FMOD events to play in parallel with the main engine event " +
            "(Option C: driving nested child events directly from code). " +
            "Use this when the parent engine event contains nested/referenced " +
            "child events that don't receive the parent's local parameter " +
            "values. List each child event here and this controller will " +
            "create, attach, start, and parameter-drive them alongside the " +
            "parent. IMPORTANT: if you list a child event here, remove or " +
            "disable its nested event instrument inside the parent event in " +
            "FMOD Studio, otherwise you'll hear double playback.")]
        EventReference[] additionalEngineLayers;

        [SerializeField, Tooltip("FMOD parameter name driven by ship velocity.")]
        string speedParameterName = "Speed";

        [SerializeField, Tooltip(
            "FMOD parameter name driven by ship pitch + yaw rate " +
            "(bipolar: negative = one direction, positive = the other).")]
        string tiltParameterName = "Tilt Acel";

        [Header("Speed Mapping")]
        [SerializeField, Tooltip(
            "Ship velocity magnitude (world units / second) that maps to " +
            "paramAtMaxVelocity. Velocities above this are clamped.")]
        float maxShipVelocity = 100f;

        [SerializeField, Tooltip(
            "FMOD parameter value when ship velocity is 0. Set this to the " +
            "baseline you want the engine to sit at during normal play.")]
        float paramAtZeroVelocity = 40f;

        [SerializeField, Tooltip(
            "FMOD parameter value when ship velocity hits maxShipVelocity.")]
        float paramAtMaxVelocity = 100f;

        [SerializeField, Range(0f, 30f), Tooltip(
            "Exponential smoothing rate for measured velocity. " +
            "0 = no smoothing (instant, jittery). ~5-10 = responsive but stable.")]
        float velocitySmoothing = 6f;

        [Header("Tilt (pitch + yaw rate)")]
        [SerializeField, Tooltip(
            "Angular rate (degrees/second) that maps to paramAtMaxTilt. " +
            "Rates above this are clamped. Typical ships tilt at 90-300 deg/s.")]
        float maxTiltRate = 180f;

        [SerializeField, Tooltip(
            "FMOD param value when pitch+yaw rate is at the maximum (positive).")]
        float paramAtMaxTilt = 100f;

        [SerializeField, Tooltip(
            "FMOD param value when pitch+yaw rate is at the maximum in the " +
            "opposite direction (negative). For a symmetric bipolar FMOD " +
            "parameter this should equal -paramAtMaxTilt.")]
        float paramAtMinTilt = -100f;

        [SerializeField, Tooltip(
            "FMOD param value when ship is not tilting (zero angular rate).")]
        float paramAtZeroTilt = 0f;

        [SerializeField, Range(-2f, 2f), Tooltip(
            "How much pitch rate (local X-axis rotation) contributes to the " +
            "signed tilt signal. Set negative to invert sign.")]
        float pitchWeight = 1f;

        [SerializeField, Range(-2f, 2f), Tooltip(
            "How much yaw rate (local Y-axis rotation) contributes to the " +
            "signed tilt signal. Set negative to invert sign.")]
        float yawWeight = 1f;

        [SerializeField, Range(0f, 30f), Tooltip(
            "Exponential smoothing rate for the signed tilt value. " +
            "Higher = snappier, lower = smoother.")]
        float tiltSmoothing = 8f;

        [Header("Drift")]
        [SerializeField, Range(0f, 1f), Tooltip(
            "Multiplier applied to the FMOD param value while " +
            "VesselStatus.IsDrifting is true (drift trigger held). " +
            "1 = no effect, 0 = fully silenced.")]
        float driftSlowdownMultiplier = 0.6f;

        [SerializeField, Range(0f, 30f), Tooltip(
            "How quickly the drift multiplier ramps in/out when drift is " +
            "engaged/released. Higher = snappier.")]
        float driftSmoothingRate = 8f;

        [Header("Spatialisation")]
        [SerializeField, Tooltip(
            "When true, engine audio is ONLY created for the ship this " +
            "client is controlling (the local user's ship). Remote and AI " +
            "ships never instantiate the FMOD engine event on this client, " +
            "so other ships are silent here. Turn off to fall back to the " +
            "legacy behaviour where every ship plays audio (local -> " +
            "listener, remote -> 3D spatialised).")]
        bool onlyAudibleToController = true;

        [SerializeField, Tooltip(
            "Force all instances (local and remote) to attach to the listener " +
            "instead of the ship. Use if auto-detection of local ownership " +
            "isn't working and you want the engine always audible regardless " +
            "of camera distance. Has no effect when onlyAudibleToController " +
            "is true, since remote ships won't instantiate audio at all.")]
        bool forceAttachToListener = false;

        [Header("SFX Volume")]
        [SerializeField, Tooltip(
            "When true, the FMOD engine instance (and all additional engine " +
            "layers) have their volume multiplied by GameSetting.SFXLevel. " +
            "Toggling the in-game SFX enable/disable flag also mutes the " +
            "engine. Leave on unless you want engine audio to ignore the " +
            "player's SFX slider.")]
        bool tieVolumeToSFXSlider = true;

        [SerializeField, Range(0f, 2f), Tooltip(
            "Additional per-ship volume multiplier applied on top of the SFX " +
            "slider. Use this to balance the engine against other SFX " +
            "without touching the global slider. 1 = no change.")]
        float baseVolumeMultiplier = 1f;

        [Header("Elemental Parameters (driven from ResourceSystem)")]
        [SerializeField, Tooltip(
            "When true, the Charge/Mass/Space/Time FMOD parameters on the " +
            "engine event are driven from this ship's ResourceSystem. Use " +
            "this to activate tracks inside the event that are gated by " +
            "elemental level parameters. Requires that the event expose " +
            "parameters with the names below.")]
        bool driveElementalParams = true;

        [SerializeField] string chargeParameterName = "Charge";
        [SerializeField] string massParameterName = "Mass";
        [SerializeField] string spaceParameterName = "Space";
        [SerializeField] string timeParameterName = "Time";

        [SerializeField, Tooltip(
            "FMOD parameter value when the elemental level is at its minimum " +
            "(normalized 0). Most events want 0 here so the track is silent " +
            "when the element is depleted.")]
        float elementParamAtMin = 0f;

        [SerializeField, Tooltip(
            "FMOD parameter value when the elemental level is at its maximum " +
            "(normalized 1). Set this to match the maximum the FMOD parameter " +
            "expects — typically 100 for percentage-style params or 10 if the " +
            "event was built around the integer ElementalLevels scale.")]
        float elementParamAtMax = 100f;

        [SerializeField, Range(0f, 30f), Tooltip(
            "Exponential smoothing rate for elemental level -> FMOD parameter " +
            "value. 0 = snap instantly, ~6-10 = smooth. Prevents audible " +
            "zipper noise when the level changes in discrete steps.")]
        float elementSmoothing = 6f;

        [System.Serializable]
        public class ExtraFmodParam
        {
            [Tooltip("FMOD parameter name as exposed on the engine event.")]
            public string parameterName;

            [Tooltip("Value to push every frame. Tweak until the track becomes audible / behaves the way you want.")]
            public float value;

            [Tooltip("Optional — if enabled, this parameter is pushed once at start instead of every frame. Handy for static 'identity' parameters.")]
            public bool setOnceAtStart;
        }

        [SerializeField, Tooltip(
            "Additional FMOD parameters to drive with static (non-gameplay) " +
            "values. Use this for parameters like 'Energy' or 'Stress' that " +
            "gate tracks inside the event but don't have a gameplay source " +
            "wired up yet. Set a value high enough to unmute the track.")]
        ExtraFmodParam[] extraParameters;

        [Header("Debug")]
        [SerializeField, Tooltip("Log creation, start state, and current speed + attach mode once per second.")]
        bool debugLog = false;

        /// <summary>
        /// Tracks whether we've decided to instantiate the FMOD event for
        /// this ship. Instance creation is deferred because the owning
        /// IPlayer isn't assigned until VesselController.Initialize() runs
        /// after Start().
        /// </summary>
        enum CreationState
        {
            /// <summary>Player ownership unknown; waiting to decide.</summary>
            PendingEvaluation,
            /// <summary>Instance was created (local ship, or legacy mode).</summary>
            Created,
            /// <summary>Ship is remote/AI and onlyAudibleToController is on; no instance will ever be created.</summary>
            SkippedRemote
        }

        // Runtime state
        IVessel _vessel;
        IVesselStatus _status;
        CosmicShore.Core.ResourceSystem _resourceSystem;
        StudioListener _listener;
        EventInstance _instance;
        PARAMETER_ID _speedParamId;
        PARAMETER_ID _tiltParamId;
        bool _hasSpeedParam;
        bool _hasTiltParam;
        bool _speedParamIsGlobal;
        bool _tiltParamIsGlobal;
        bool _instanceStarted;
        AttachMode _attachMode;
        CreationState _creationState = CreationState.PendingEvaluation;
        Vector3 _lastPosition;
        Quaternion _lastRotation;
        float _smoothedVelocity;
        float _smoothedTiltSigned;
        float _currentDriftFactor = 1f;
        float _lastLogTime;

        // Elemental parameter runtime state.
        struct ElementParamRuntime
        {
            public bool found;       // Was this parameter located on the event?
            public bool isGlobal;
            public PARAMETER_ID id;
            public float smoothed;
        }
        ElementParamRuntime _chargeParam;
        ElementParamRuntime _massParam;
        ElementParamRuntime _spaceParam;
        ElementParamRuntime _timeParam;

        // Extra static parameters runtime state.
        struct ExtraParamRuntime
        {
            public bool found;
            public bool isGlobal;
            public PARAMETER_ID id;
            public bool setOnceAtStart;
            public bool alreadySetOnce;
            public float value;
            public string debugName;
        }
        ExtraParamRuntime[] _extraParams;

        // Per-layer runtime state for Option C (driving nested child events directly).
        class LayerRuntime
        {
            public string debugName;
            public EventInstance instance;
            public PARAMETER_ID speedId;
            public PARAMETER_ID tiltId;
            public bool hasSpeed;
            public bool hasTilt;
            public bool speedIsGlobal;
            public bool tiltIsGlobal;
            public bool started;
        }

        readonly List<LayerRuntime> _layers = new List<LayerRuntime>();

        void Awake()
        {
            _vessel = GetComponent<IVessel>();
            _status = GetComponent<IVesselStatus>();

            // ResourceSystem is exposed via IVesselStatus.ResourceSystem, but
            // may not be populated at Awake on all vessels; fall back to a
            // GetComponentInChildren search. If still null the elemental
            // params just won't be driven.
            _resourceSystem = GetComponentInChildren<CosmicShore.Core.ResourceSystem>(true);
        }

        void Start()
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;

            // Instance creation is gated on ownership in onlyAudibleToController
            // mode. Player may not be set yet (VesselController.Initialize()
            // runs after Start()), so this may defer to Update().
            TryEvaluateAndCreate();

            if (_creationState == CreationState.Created)
                TryRouteAttachment(); // Set an initial routing choice based on whatever info we have right now.
        }

        void OnDestroy()
        {
            StopAndRelease();
        }

        void OnDisable()
        {
            if (_instanceStarted && _instance.isValid())
                _instance.setPaused(true);

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.started && layer.instance.isValid())
                    layer.instance.setPaused(true);
            }

            // Detach from GameSetting events while disabled so we don't hold
            // a dangling reference and so we aren't pinged while paused.
            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel -= OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus -= OnSFXEnabledChanged;
            }
        }

        void OnEnable()
        {
            if (_instanceStarted && _instance.isValid())
                _instance.setPaused(false);

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.started && layer.instance.isValid())
                    layer.instance.setPaused(false);
            }

            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel += OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus += OnSFXEnabledChanged;

                // Re-sync to the current slider value in case it changed while
                // we were disabled (e.g. during scene transitions or pause).
                ApplySFXVolume();
            }
        }

        /// <summary>
        /// Computes the target FMOD volume from the current SFX slider /
        /// enable-flag state (via GameSetting) and pushes it to the engine
        /// instance plus every engine layer. Called on creation, on every
        /// SFX setting change, and on re-enable. Safe to call even if the
        /// instances haven't been created yet — it no-ops for invalid
        /// instances.
        /// </summary>
        void ApplySFXVolume()
        {
            float volume = ResolveSFXVolume();

            if (_instance.isValid())
                _instance.setVolume(volume);

            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].instance.isValid())
                    _layers[i].instance.setVolume(volume);
            }

            if (debugLog)
                Debug.Log($"[ShipAudioController] '{name}' SFX volume -> {volume:F2}", this);
        }

        /// <summary>
        /// Turns the current GameSetting SFX state into a 0..1 FMOD volume
        /// multiplier. If the SFX slider is muted, or if tie-to-slider is
        /// disabled, falls back to the base multiplier alone (or to full
        /// volume, respectively).
        /// </summary>
        float ResolveSFXVolume()
        {
            if (!tieVolumeToSFXSlider)
                return Mathf.Clamp(baseVolumeMultiplier, 0f, 2f);

            var gs = GameSetting.Instance;
            if (gs == null)
                return Mathf.Clamp(baseVolumeMultiplier, 0f, 2f);

            if (!gs.SFXEnabled)
                return 0f;

            // GameSetting.SFXLevel is a 0..1 slider value by convention in
            // this project. Clamp defensively; multiply by the per-ship
            // base multiplier so designers can trim the engine relative to
            // other SFX without fighting the slider.
            float slider = Mathf.Clamp01(gs.SFXLevel);
            return Mathf.Clamp(slider * baseVolumeMultiplier, 0f, 2f);
        }

        void OnSFXLevelChanged(float level) => ApplySFXVolume();
        void OnSFXEnabledChanged(bool enabled) => ApplySFXVolume();

        /// <summary>
        /// Decides whether to create the FMOD engine instance(s) for this
        /// ship, based on ownership. Called from Start() and from Update()
        /// until a decision can be made.
        ///
        /// In the default mode (<see cref="onlyAudibleToController"/> = true),
        /// audio is only instantiated if this client is the controlling
        /// (local) user of the ship. Remote and AI ships never create an
        /// instance, so the ship is completely silent on this client.
        ///
        /// In legacy mode (<see cref="onlyAudibleToController"/> = false),
        /// every ship creates an instance regardless of ownership.
        /// <see cref="forceAttachToListener"/> is respected as an override
        /// that guarantees creation without waiting for ownership info.
        /// </summary>
        void TryEvaluateAndCreate()
        {
            if (_creationState != CreationState.PendingEvaluation) return;

            if (onlyAudibleToController && !forceAttachToListener)
            {
                // Need ownership info to decide. If Player isn't set yet,
                // hold off — Update() will keep retrying each frame.
                if (_status == null || _status.Player == null)
                    return;

                if (!_status.IsLocalUser)
                {
                    _creationState = CreationState.SkippedRemote;

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[ShipAudioController] '{name}' is remote/AI; " +
                            $"skipping engine audio creation (onlyAudibleToController=true).",
                            this);
                    }
                    return;
                }
            }

            TryCreateAndStart();
            CreateAndStartLayers();
            _creationState = CreationState.Created;

            // Sync initial volume to the SFX slider before any audio is heard.
            // Subsequent changes come through the GameSetting events we
            // subscribe to in OnEnable.
            ApplySFXVolume();
        }

        void TryCreateAndStart()
        {
            if (engineEvent.IsNull)
            {
                Debug.LogError($"[ShipAudioController] '{name}' has no Engine Event assigned; nothing will play.", this);
                return;
            }

            _instance = RuntimeManager.CreateInstance(engineEvent);
            if (!_instance.isValid())
            {
                Debug.LogError(
                    $"[ShipAudioController] Failed to create FMOD instance for '{engineEvent}'. " +
                    $"Is the bank set to auto-load (FMOD → Edit Settings → Load Banks)?",
                    this);
                return;
            }

            if (_instance.getDescription(out EventDescription desc) == FMOD.RESULT.OK)
            {
                if (desc.getParameterDescriptionByName(speedParameterName, out PARAMETER_DESCRIPTION speedDesc) == FMOD.RESULT.OK)
                {
                    _speedParamId = speedDesc.id;
                    _speedParamIsGlobal = (speedDesc.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                    _hasSpeedParam = true;

                    if (debugLog)
                        Debug.Log($"[ShipAudioController] '{name}' '{speedParameterName}' param flags={speedDesc.flags} (global={_speedParamIsGlobal})", this);
                }
                else
                {
                    Debug.LogWarning(
                        $"[ShipAudioController] Event '{engineEvent}' has no parameter named '{speedParameterName}'. " +
                        $"Engine will loop but won't modulate with speed.",
                        this);
                }

                if (!string.IsNullOrEmpty(tiltParameterName) &&
                    desc.getParameterDescriptionByName(tiltParameterName, out PARAMETER_DESCRIPTION tiltDesc) == FMOD.RESULT.OK)
                {
                    _tiltParamId = tiltDesc.id;
                    _tiltParamIsGlobal = (tiltDesc.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                    _hasTiltParam = true;

                    if (debugLog)
                        Debug.Log($"[ShipAudioController] '{name}' '{tiltParameterName}' param flags={tiltDesc.flags} (global={_tiltParamIsGlobal})", this);
                }
                else if (!string.IsNullOrEmpty(tiltParameterName))
                {
                    Debug.LogWarning(
                        $"[ShipAudioController] Event '{engineEvent}' has no parameter named '{tiltParameterName}'. " +
                        $"Tilt won't drive the engine sound.",
                        this);
                }

                // Look up elemental level parameters (Charge/Mass/Space/Time).
                // These drive the per-element tracks inside the engine event
                // and are sourced from ResourceSystem.
                if (driveElementalParams)
                {
                    _chargeParam = ResolveParam(desc, chargeParameterName);
                    _massParam   = ResolveParam(desc, massParameterName);
                    _spaceParam  = ResolveParam(desc, spaceParameterName);
                    _timeParam   = ResolveParam(desc, timeParameterName);
                }

                // Look up the extra static parameters configured in the
                // inspector (e.g. Stress, Energy) and cache their IDs and
                // values for per-frame pushing.
                if (extraParameters != null && extraParameters.Length > 0)
                {
                    _extraParams = new ExtraParamRuntime[extraParameters.Length];
                    for (int i = 0; i < extraParameters.Length; i++)
                    {
                        var src = extraParameters[i];
                        if (src == null || string.IsNullOrEmpty(src.parameterName))
                            continue;

                        var runtime = new ExtraParamRuntime
                        {
                            debugName = src.parameterName,
                            value = src.value,
                            setOnceAtStart = src.setOnceAtStart
                        };

                        if (desc.getParameterDescriptionByName(src.parameterName, out PARAMETER_DESCRIPTION p) == FMOD.RESULT.OK)
                        {
                            runtime.id = p.id;
                            runtime.isGlobal = (p.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                            runtime.found = true;

                            if (debugLog)
                            {
                                Debug.Log(
                                    $"[ShipAudioController] '{name}' extra param '{src.parameterName}' " +
                                    $"found (global={runtime.isGlobal}, value={src.value}, setOnceAtStart={src.setOnceAtStart}).",
                                    this);
                            }
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[ShipAudioController] '{name}' extra param '{src.parameterName}' " +
                                $"not found on event '{engineEvent}'. It will be ignored.",
                                this);
                        }

                        _extraParams[i] = runtime;
                    }
                }
            }

            var startResult = _instance.start();
            _instanceStarted = startResult == FMOD.RESULT.OK;

            if (debugLog)
            {
                Debug.Log(
                    $"[ShipAudioController] '{name}' started engine event '{engineEvent}' " +
                    $"(startResult={startResult}, hasSpeedParam={_hasSpeedParam}).",
                    this);
            }
        }

        /// <summary>
        /// Creates, looks up parameter IDs for, and starts each additional
        /// engine layer. Each layer is an independent FMOD EventInstance
        /// with its own local parameter values, so we have to look up the
        /// Speed / Tilt Acel parameter IDs per-layer (parameter IDs are
        /// event-specific for local parameters).
        /// </summary>
        void CreateAndStartLayers()
        {
            if (additionalEngineLayers == null || additionalEngineLayers.Length == 0)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[ShipAudioController] '{name}' has no additionalEngineLayers configured — " +
                        $"only the main engineEvent will play.",
                        this);
                }
                return;
            }

            int configured = additionalEngineLayers.Length;
            int createdOk = 0;
            int startedOk = 0;

            for (int i = 0; i < additionalEngineLayers.Length; i++)
            {
                EventReference reference = additionalEngineLayers[i];
                if (reference.IsNull)
                {
                    Debug.LogWarning(
                        $"[ShipAudioController] '{name}' additionalEngineLayers[{i}] is unassigned (IsNull). " +
                        $"Drag an FMOD event into that slot or remove the entry.",
                        this);
                    continue;
                }

                var layer = new LayerRuntime { debugName = reference.ToString() };

                layer.instance = RuntimeManager.CreateInstance(reference);
                if (!layer.instance.isValid())
                {
                    Debug.LogError(
                        $"[ShipAudioController] '{name}' failed to create FMOD instance for layer [{i}] '{reference}'. " +
                        $"Is its bank auto-loaded (FMOD -> Edit Settings -> Load Banks)?",
                        this);
                    continue;
                }
                createdOk++;

                if (layer.instance.getDescription(out EventDescription desc) == FMOD.RESULT.OK)
                {
                    if (!string.IsNullOrEmpty(speedParameterName) &&
                        desc.getParameterDescriptionByName(speedParameterName, out PARAMETER_DESCRIPTION speedDesc) == FMOD.RESULT.OK)
                    {
                        layer.speedId = speedDesc.id;
                        layer.speedIsGlobal = (speedDesc.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                        layer.hasSpeed = true;
                    }
                    else if (!string.IsNullOrEmpty(speedParameterName))
                    {
                        Debug.LogWarning(
                            $"[ShipAudioController] Layer [{i}] '{reference}' has no parameter named '{speedParameterName}'. " +
                            $"It'll play but won't modulate with speed — make sure the child event exposes the same parameter " +
                            $"name, or accept that it'll sit at its default.",
                            this);
                    }

                    if (!string.IsNullOrEmpty(tiltParameterName) &&
                        desc.getParameterDescriptionByName(tiltParameterName, out PARAMETER_DESCRIPTION tiltDesc) == FMOD.RESULT.OK)
                    {
                        layer.tiltId = tiltDesc.id;
                        layer.tiltIsGlobal = (tiltDesc.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                        layer.hasTilt = true;
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[ShipAudioController] Layer [{i}] '{reference}' getDescription() failed — " +
                        $"parameter lookup skipped.",
                        this);
                }

                var startResult = layer.instance.start();
                layer.started = startResult == FMOD.RESULT.OK;
                if (layer.started) startedOk++;

                if (!layer.started)
                {
                    Debug.LogError(
                        $"[ShipAudioController] '{name}' layer [{i}] '{reference}' start() returned {startResult}. " +
                        $"The layer will be silent.",
                        this);
                }
                else if (debugLog)
                {
                    Debug.Log(
                        $"[ShipAudioController] '{name}' started layer [{i}] '{reference}' " +
                        $"(hasSpeed={layer.hasSpeed}, hasTilt={layer.hasTilt}).",
                        this);
                }

                _layers.Add(layer);
            }

            if (debugLog || startedOk < configured)
            {
                // Always surface a summary if any layer failed; only surface on success
                // when debug logging is on.
                var level = startedOk < configured ? LogType.Warning : LogType.Log;
                Debug.unityLogger.Log(
                    level,
                    $"[ShipAudioController] '{name}' engine layers: configured={configured}, " +
                    $"created={createdOk}, started={startedOk}. If started < configured, scroll up " +
                    $"for the specific layer error.");
            }
        }

        /// <summary>
        /// Picks a spatialisation target (ship transform vs listener) and
        /// re-attaches the instance if the target has changed. Safe to call
        /// every frame.
        /// </summary>
        void TryRouteAttachment()
        {
            if (!_instanceStarted || !_instance.isValid()) return;

            AttachMode desired = ResolveDesiredAttachMode();
            if (desired == _attachMode) return;

            // Detach from the previous target (harmless if not attached).
            RuntimeManager.DetachInstanceFromGameObject(_instance);
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].instance.isValid())
                    RuntimeManager.DetachInstanceFromGameObject(_layers[i].instance);
            }

            Transform attachTarget = null;

            switch (desired)
            {
                case AttachMode.Listener:
                    var listenerTransform = GetListenerTransform();
                    if (listenerTransform != null)
                    {
                        attachTarget = listenerTransform;
                        _attachMode = AttachMode.Listener;
                    }
                    else
                    {
                        // No listener found yet — fall back to ship attachment and try again next frame.
                        attachTarget = transform;
                        _attachMode = AttachMode.Ship;
                    }
                    break;

                case AttachMode.Ship:
                default:
                    attachTarget = transform;
                    _attachMode = AttachMode.Ship;
                    break;
            }

            RuntimeManager.AttachInstanceToGameObject(_instance, attachTarget, (Rigidbody)null);
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].instance.isValid())
                    RuntimeManager.AttachInstanceToGameObject(_layers[i].instance, attachTarget, (Rigidbody)null);
            }

            if (debugLog)
                Debug.Log($"[ShipAudioController] '{name}' attach mode -> {_attachMode} (layers={_layers.Count})", this);
        }

        AttachMode ResolveDesiredAttachMode()
        {
            if (forceAttachToListener) return AttachMode.Listener;

            // Local player -> attach to listener so attenuation is effectively zero.
            // Remote player -> attach to own ship transform for proper 3D audio.
            // Ownership unknown yet -> default to ship (safer default for remote AI ships).
            if (_status != null && _status.Player != null)
                return _status.IsLocalUser ? AttachMode.Listener : AttachMode.Ship;

            return AttachMode.Ship;
        }

        Transform GetListenerTransform()
        {
            if (_listener != null) return _listener.transform;

            // StudioListener is the canonical FMOD listener. Fall back to Camera.main only
            // if nothing else is found (rare; most FMOD projects place a StudioListener on
            // the main camera).
#if UNITY_2023_1_OR_NEWER
            _listener = Object.FindFirstObjectByType<StudioListener>();
#else
            _listener = Object.FindObjectOfType<StudioListener>();
#endif
            if (_listener != null) return _listener.transform;

            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        void StopAndRelease()
        {
            if (_instance.isValid())
            {
                if (_instanceStarted)
                {
                    RuntimeManager.DetachInstanceFromGameObject(_instance);
                    _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                }
                _instance.release();
            }
            _instance = default;
            _instanceStarted = false;
            _hasSpeedParam = false;
            _hasTiltParam = false;
            _speedParamIsGlobal = false;
            _tiltParamIsGlobal = false;
            _attachMode = AttachMode.None;
            _creationState = CreationState.PendingEvaluation;

            // Reset elemental and extra parameter state so a re-created
            // instance re-resolves parameter IDs and smoothing from scratch.
            _chargeParam = default;
            _massParam = default;
            _spaceParam = default;
            _timeParam = default;
            _extraParams = null;

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.instance.isValid())
                {
                    if (layer.started)
                    {
                        RuntimeManager.DetachInstanceFromGameObject(layer.instance);
                        layer.instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    }
                    layer.instance.release();
                }
            }
            _layers.Clear();
        }

        /// <summary>
        /// Sets a parameter value on the parent event instance, routing
        /// through either the Studio System (for global parameters) or the
        /// event instance (for per-instance parameters).
        /// </summary>
        void SetParam(PARAMETER_ID id, float value, bool isGlobal)
        {
            SetParamOnInstance(_instance, id, value, isGlobal);
        }

        void GetParam(PARAMETER_ID id, bool isGlobal, out float value)
        {
            if (isGlobal)
                RuntimeManager.StudioSystem.getParameterByID(id, out value);
            else
                _instance.getParameterByID(id, out value);
        }

        /// <summary>
        /// Variant of SetParam that targets a specific EventInstance (used
        /// for driving individual engine layers).
        /// </summary>
        void SetParamOnInstance(EventInstance inst, PARAMETER_ID id, float value, bool isGlobal)
        {
            if (isGlobal)
                RuntimeManager.StudioSystem.setParameterByID(id, value);
            else
                inst.setParameterByID(id, value);
        }

        /// <summary>
        /// Looks up a parameter by name on the given event description.
        /// Returns a populated <see cref="ElementParamRuntime"/>; its
        /// <see cref="ElementParamRuntime.found"/> flag is false if the
        /// event doesn't expose a parameter by that name. A one-shot debug
        /// warning is logged on lookup failure so missing parameter bindings
        /// are discoverable.
        /// </summary>
        ElementParamRuntime ResolveParam(EventDescription desc, string paramName)
        {
            var result = new ElementParamRuntime();
            if (string.IsNullOrEmpty(paramName)) return result;

            if (desc.getParameterDescriptionByName(paramName, out PARAMETER_DESCRIPTION p) == FMOD.RESULT.OK)
            {
                result.id = p.id;
                result.isGlobal = (p.flags & PARAMETER_FLAGS.GLOBAL) != 0;
                result.found = true;

                if (debugLog)
                    Debug.Log($"[ShipAudioController] '{name}' element param '{paramName}' found (global={result.isGlobal}).", this);
            }
            else if (debugLog)
            {
                Debug.LogWarning(
                    $"[ShipAudioController] '{name}' no parameter named '{paramName}' on engine event. " +
                    $"The corresponding element track won't be driven.",
                    this);
            }

            return result;
        }

        /// <summary>
        /// Pushes a smoothed elemental level (already normalised 0..1) into
        /// its FMOD parameter, mapping through <see cref="elementParamAtMin"/>
        /// / <see cref="elementParamAtMax"/> for the event's expected range.
        /// </summary>
        void PushElementParam(ref ElementParamRuntime runtime, float normalisedLevel, float dt)
        {
            if (!runtime.found) return;

            float target = Mathf.Clamp01(normalisedLevel);
            float alpha = elementSmoothing > 0f ? 1f - Mathf.Exp(-dt * elementSmoothing) : 1f;
            runtime.smoothed = Mathf.Lerp(runtime.smoothed, target, alpha);

            float paramValue = Mathf.Lerp(elementParamAtMin, elementParamAtMax, runtime.smoothed);
            SetParam(runtime.id, paramValue, runtime.isGlobal);
        }

        /// <summary>
        /// Computes the ship's local-frame angular velocity in degrees per
        /// second around each local axis, using a quaternion delta between
        /// the current and previous frame's orientations. This is robust
        /// against gimbal lock (unlike eulerAngles math) and produces signed
        /// pitch/yaw/roll rates.
        /// </summary>
        Vector3 ComputeLocalAngularVelocityDegPerSec(float dt)
        {
            if (dt <= 0f) return Vector3.zero;

            Quaternion current = transform.rotation;
            Quaternion delta = current * Quaternion.Inverse(_lastRotation);
            _lastRotation = current;

            delta.ToAngleAxis(out float angleDeg, out Vector3 axisWorld);

            // ToAngleAxis returns angles in [0, 360]; fold to [-180, 180].
            if (angleDeg > 180f) angleDeg -= 360f;

            // Guard against NaN/Inf from a near-identity rotation.
            if (float.IsNaN(axisWorld.x) || float.IsInfinity(axisWorld.x) ||
                float.IsNaN(angleDeg) || float.IsInfinity(angleDeg))
                return Vector3.zero;

            Vector3 angVelWorld = axisWorld.normalized * (angleDeg / dt);
            return transform.InverseTransformDirection(angVelWorld);
        }

        void Update()
        {
            // If we haven't decided whether to create the audio instance,
            // keep trying until Player info becomes available.
            if (_creationState == CreationState.PendingEvaluation)
            {
                TryEvaluateAndCreate();
                if (_creationState != CreationState.Created) return;
            }
            else if (_creationState == CreationState.SkippedRemote)
            {
                // Remote / AI ship with onlyAudibleToController on — never
                // make any sound on this client. Skip the per-frame work
                // entirely.
                return;
            }

            if (!_instanceStarted || !_instance.isValid()) return;

            // Re-evaluate attachment every frame. Cheap when nothing has changed
            // (ResolveDesiredAttachMode returns the same value, so we early-exit).
            TryRouteAttachment();

            // World-space velocity from transform delta.
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;

            float instantVelocity = dt > 0f ? delta.magnitude / dt : 0f;

            // Framerate-independent exponential smoothing.
            float a = velocitySmoothing > 0f ? 1f - Mathf.Exp(-dt * velocitySmoothing) : 1f;
            _smoothedVelocity = Mathf.Lerp(_smoothedVelocity, instantVelocity, a);

            // Smooth the drift trigger on/off so the engine dip is continuous, not a step.
            bool drifting = _status != null && _status.IsDrifting;
            float targetDriftFactor = drifting ? driftSlowdownMultiplier : 1f;
            float driftA = driftSmoothingRate > 0f ? 1f - Mathf.Exp(-dt * driftSmoothingRate) : 1f;
            _currentDriftFactor = Mathf.Lerp(_currentDriftFactor, targetDriftFactor, driftA);

            float speedParamValueToPush = 0f;
            bool pushSpeed = false;
            if (_hasSpeedParam || _layers.Count > 0)
            {
                float normalised = maxShipVelocity > 0f
                    ? Mathf.Clamp01(_smoothedVelocity / maxShipVelocity)
                    : 0f;

                // Map velocity onto [paramAtZeroVelocity, paramAtMaxVelocity] and
                // then attenuate by the smoothed drift factor.
                speedParamValueToPush = Mathf.Lerp(paramAtZeroVelocity, paramAtMaxVelocity, normalised);
                speedParamValueToPush *= _currentDriftFactor;
                pushSpeed = true;

                if (_hasSpeedParam)
                    SetParam(_speedParamId, speedParamValueToPush, _speedParamIsGlobal);
            }

            // --- Tilt (pitch + yaw rate) ---
            // Compute local-frame angular velocity (deg/sec). X = pitch, Y = yaw, Z = roll.
            Vector3 angVelLocal = ComputeLocalAngularVelocityDegPerSec(dt);
            float pitchRate = angVelLocal.x;
            float yawRate = angVelLocal.y;

            // Blend pitch + yaw into a single signed tilt value.
            float signedTiltRate = pitchRate * pitchWeight + yawRate * yawWeight;

            // Smooth (framerate-independent) so it doesn't snap per-frame.
            float tiltA = tiltSmoothing > 0f ? 1f - Mathf.Exp(-dt * tiltSmoothing) : 1f;
            _smoothedTiltSigned = Mathf.Lerp(_smoothedTiltSigned, signedTiltRate, tiltA);

            float tiltParamValueToPush = 0f;
            bool pushTilt = false;
            if (_hasTiltParam || _layers.Count > 0)
            {
                float normalisedTilt = maxTiltRate > 0f
                    ? Mathf.Clamp(_smoothedTiltSigned / maxTiltRate, -1f, 1f)
                    : 0f;

                tiltParamValueToPush = normalisedTilt >= 0f
                    ? Mathf.Lerp(paramAtZeroTilt, paramAtMaxTilt, normalisedTilt)
                    : Mathf.Lerp(paramAtZeroTilt, paramAtMinTilt, -normalisedTilt);
                pushTilt = true;

                if (_hasTiltParam)
                    SetParam(_tiltParamId, tiltParamValueToPush, _tiltParamIsGlobal);
            }

            // Push Speed / Tilt Acel into every additional engine layer. Each
            // layer has its own parameter IDs (parameter IDs are event-local
            // for non-global parameters), so we use the per-layer IDs and
            // global-flags captured at CreateAndStartLayers time.
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (!layer.started || !layer.instance.isValid()) continue;

                if (pushSpeed && layer.hasSpeed)
                    SetParamOnInstance(layer.instance, layer.speedId, speedParamValueToPush, layer.speedIsGlobal);

                if (pushTilt && layer.hasTilt)
                    SetParamOnInstance(layer.instance, layer.tiltId, tiltParamValueToPush, layer.tiltIsGlobal);
            }

            // --- Elemental parameters (Charge / Mass / Space / Time) ---
            // Drive them from ResourceSystem so the per-element tracks
            // inside the engine event unmute as the ship collects resources.
            if (driveElementalParams && _resourceSystem != null)
            {
                PushElementParam(ref _chargeParam, _resourceSystem.ChargeLevel, dt);
                PushElementParam(ref _massParam,   _resourceSystem.MassLevel,   dt);
                PushElementParam(ref _spaceParam,  _resourceSystem.SpaceLevel,  dt);
                PushElementParam(ref _timeParam,   _resourceSystem.TimeLevel,   dt);
            }

            // --- Extra static parameters (Stress, Energy, ...) ---
            // Push once-at-start parameters on the first update only, and
            // push per-frame parameters every frame.
            if (_extraParams != null)
            {
                for (int i = 0; i < _extraParams.Length; i++)
                {
                    if (!_extraParams[i].found) continue;

                    if (_extraParams[i].setOnceAtStart)
                    {
                        if (_extraParams[i].alreadySetOnce) continue;
                        _extraParams[i].alreadySetOnce = true;
                    }

                    SetParam(_extraParams[i].id, _extraParams[i].value, _extraParams[i].isGlobal);
                }
            }

            if (debugLog && Time.unscaledTime - _lastLogTime > 1f)
            {
                _lastLogTime = Time.unscaledTime;
                _instance.getPlaybackState(out PLAYBACK_STATE state);
                float speedParamValue = 0f, tiltParamValue = 0f;
                if (_hasSpeedParam) GetParam(_speedParamId, _speedParamIsGlobal, out speedParamValue);
                if (_hasTiltParam) GetParam(_tiltParamId, _tiltParamIsGlobal, out tiltParamValue);

                int layersPlaying = 0;
                var layerDetails = new System.Text.StringBuilder();
                for (int i = 0; i < _layers.Count; i++)
                {
                    var layer = _layers[i];
                    if (!layer.instance.isValid())
                    {
                        layerDetails.Append($"\n    [{i}] '{layer.debugName}' instance=INVALID started={layer.started}");
                        continue;
                    }

                    layer.instance.getPlaybackState(out PLAYBACK_STATE layerState);
                    if (layerState == PLAYBACK_STATE.PLAYING) layersPlaying++;

                    float layerSpeed = 0f, layerTilt = 0f;
                    if (layer.hasSpeed)
                    {
                        if (layer.speedIsGlobal)
                            RuntimeManager.StudioSystem.getParameterByID(layer.speedId, out layerSpeed);
                        else
                            layer.instance.getParameterByID(layer.speedId, out layerSpeed);
                    }
                    if (layer.hasTilt)
                    {
                        if (layer.tiltIsGlobal)
                            RuntimeManager.StudioSystem.getParameterByID(layer.tiltId, out layerTilt);
                        else
                            layer.instance.getParameterByID(layer.tiltId, out layerTilt);
                    }

                    // Volume gives a final sanity check: if it's 0, the layer is
                    // running but FMOD is silencing it (bus routing, modulation,
                    // virtualization, etc.).
                    layer.instance.getVolume(out float layerVolume, out _);

                    layerDetails.Append(
                        $"\n    [{i}] '{layer.debugName}' state={layerState} " +
                        $"started={layer.started} volume={layerVolume:F2} " +
                        $"hasSpeed={layer.hasSpeed}({layerSpeed:F1}) " +
                        $"hasTilt={layer.hasTilt}({layerTilt:F1})");
                }

                // Element-param readout (only if we're driving them).
                var elementSummary = new System.Text.StringBuilder();
                if (driveElementalParams && _resourceSystem != null)
                {
                    elementSummary.Append(
                        $" | elements charge={_resourceSystem.ChargeLevel:F2}(found={_chargeParam.found}) " +
                        $"mass={_resourceSystem.MassLevel:F2}(found={_massParam.found}) " +
                        $"space={_resourceSystem.SpaceLevel:F2}(found={_spaceParam.found}) " +
                        $"time={_resourceSystem.TimeLevel:F2}(found={_timeParam.found})");
                }

                if (_extraParams != null && _extraParams.Length > 0)
                {
                    elementSummary.Append(" | extras:");
                    for (int i = 0; i < _extraParams.Length; i++)
                    {
                        elementSummary.Append(
                            $" {_extraParams[i].debugName}={_extraParams[i].value:F2}" +
                            $"(found={_extraParams[i].found})");
                    }
                }

                Debug.Log(
                    $"[ShipAudioController] '{name}' attach={_attachMode} " +
                    $"velocity={_smoothedVelocity:F2} speedParam={speedParamValue:F2} " +
                    $"pitchRate={pitchRate:F1} yawRate={yawRate:F1} " +
                    $"tiltSigned={_smoothedTiltSigned:F1} tiltParam={tiltParamValue:F2} " +
                    $"drifting={drifting} driftFactor={_currentDriftFactor:F2} " +
                    $"playbackState={state} layersPlaying={layersPlaying}/{_layers.Count}" +
                    elementSummary.ToString() +
                    layerDetails.ToString(),
                    this);
            }
        }
    }
}

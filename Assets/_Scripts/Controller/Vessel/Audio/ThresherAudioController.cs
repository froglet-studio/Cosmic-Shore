using CosmicShore.Core;
using CosmicShore.Utility;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// The Thresher's ability audio: one exposed FMOD slot per sound (LOCKED convention - every slot ships
    /// EMPTY; an empty slot is silent and warns once, never a borrowed event).
    ///
    /// <list type="bullet">
    /// <item><b>Morning star (passive)</b>: a looping emitter that follows the ball for as long as the hull
    /// lives. Its 3D position AND velocity are written from the chain solver every frame (so it doppler-shifts
    /// as the ball whips round), and two optional parameters are driven: the ball's speed as a fraction of
    /// smash speed (0..1) and whether it is red-hot (0/1).</item>
    /// <item><b>Left trigger (Plant)</b> and <b>Right trigger (Winch)</b>: each has a PRESS event and a
    /// RELEASE event. The press event is started on the press and - if it is still playing (a loop, or a
    /// long one-shot) - stopped with fade-out on the release, so it may be authored either way. An optional
    /// parameter on it follows that trigger's analog depth (0..1). The release event is a one-shot fired on
    /// the release, attached to the ship.</item>
    /// </list>
    ///
    /// Edges come from <see cref="ThresherExecutor"/>'s input events (the same press/release every peer
    /// runs), not from polling the triggers, so an AI or remote pilot is heard the same way. The
    /// executor's own impact slots (smash, crush, bounce, slice, plant, yank) stay where they are; these
    /// slots are the abilities themselves.
    ///
    /// Put it on the Thresher's root (the GameObject that owns IVesselStatus).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThresherAudioController : MonoBehaviour
    {
        [Header("Morning Star - passive emitter (ship EMPTY)")]
        [SerializeField, Tooltip("LOOPING event that rides the morning-star ball for the life of the hull. " +
                                 "Positioned (with velocity, for doppler) on the ball every frame.")]
        EventReference morningStarLoop;

        [SerializeField, Tooltip("Parameter on the loop that takes the ball's speed as a fraction of smash " +
                                 "speed, 0..1 (1 = red-hot). Empty or missing on the event = not driven.")]
        string ballSpeedParameter = "Ball Speed";

        [SerializeField, Tooltip("Parameter on the loop set to 1 while the ball is at smash speed (red), else 0. " +
                                 "Empty or missing on the event = not driven.")]
        string ballHotParameter = "Ball Hot";

        [SerializeField, Range(0f, 30f), Tooltip("Smoothing rate for the ball-speed parameter, per second. 0 = snap.")]
        float ballSpeedSmoothing = 12f;

        [Header("Left Trigger - Plant (ship EMPTY)")]
        [SerializeField, Tooltip("Started on the LT press. If still playing at the release it is stopped with " +
                                 "fade-out, so a loop or a one-shot both work.")]
        EventReference leftTriggerEvent;

        [SerializeField, Tooltip("One-shot fired on the LT release (attached to the ship).")]
        EventReference leftTriggerReleaseEvent;

        [SerializeField, Tooltip("Parameter on the LT press event that follows the left trigger's analog depth, " +
                                 "0..1 (a keyboard/touch press reads 1). Empty or missing = not driven.")]
        string leftTriggerDepthParameter = "Trigger Depth";

        [Header("Right Trigger - Winch (ship EMPTY)")]
        [SerializeField, Tooltip("Started on the RT press. If still playing at the release it is stopped with " +
                                 "fade-out, so a loop or a one-shot both work.")]
        EventReference rightTriggerEvent;

        [SerializeField, Tooltip("One-shot fired on the RT release (attached to the ship).")]
        EventReference rightTriggerReleaseEvent;

        [SerializeField, Tooltip("Parameter on the RT press event that follows the right trigger's analog depth, " +
                                 "0..1 (a keyboard/touch press reads 1). Empty or missing = not driven.")]
        string rightTriggerDepthParameter = "Trigger Depth";

        [Header("Who hears it")]
        [SerializeField, Tooltip("When true, only the local pilot's Thresher makes these sounds (remote and AI " +
                                 "hulls are silent here), matching ShipAudioController and DriftAudioController.")]
        bool onlyAudibleToController = true;

        [Header("SFX Volume")]
        [SerializeField, Range(0f, 2f), Tooltip("Per-vessel trim on top of the SFX slider. 1 = no change.")]
        float baseVolumeMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] bool debugLog = false;

        /// <summary>One owned instance with an optional 0..1 parameter.</summary>
        struct Voice
        {
            public EventInstance Instance;
            public bool Started;
            public PARAMETER_ID ParamId;
            public bool HasParam;
            public bool ParamGlobal;
            public float AppliedVolume;
        }

        IVesselStatus _status;
        ThresherExecutor _thresher;
        Voice _ball, _left, _right;
        PARAMETER_ID _hotParamId;
        bool _hasHotParam, _hotParamGlobal;
        float _smoothedSpeed;
        bool _audible;
        bool _warnedNoExecutor, _warnedEmptyBall, _warnedEmptyLeft, _warnedEmptyRight;

        void Awake()
        {
            _status = GetComponent<IVesselStatus>();
            _thresher = GetComponentInChildren<ThresherExecutor>(true);
        }

        void OnEnable()
        {
            if (_thresher)
            {
                _thresher.PlantPressed += OnLeftPressed;
                _thresher.PlantReleased += OnLeftReleased;
                _thresher.WinchPressed += OnRightPressed;
                _thresher.WinchReleased += OnRightReleased;
            }
            GameSetting.OnChangeSFXLevel += OnSFXLevelChanged;
            GameSetting.OnChangeSFXEnabledStatus += OnSFXEnabledChanged;
            SetPaused(false);
        }

        void OnDisable()
        {
            if (_thresher)
            {
                _thresher.PlantPressed -= OnLeftPressed;
                _thresher.PlantReleased -= OnLeftReleased;
                _thresher.WinchPressed -= OnRightPressed;
                _thresher.WinchReleased -= OnRightReleased;
            }
            GameSetting.OnChangeSFXLevel -= OnSFXLevelChanged;
            GameSetting.OnChangeSFXEnabledStatus -= OnSFXEnabledChanged;
            SetPaused(true);
        }

        void OnDestroy() => StopAll(FMOD.Studio.STOP_MODE.IMMEDIATE);

        void Update()
        {
            if (_status == null) return;
            if (!_thresher)
            {
                if (!_warnedNoExecutor)
                {
                    _warnedNoExecutor = true;
                    CSDebug.LogWarning($"[ThresherAudioController] '{name}' found no ThresherExecutor; silent.", this);
                }
                return;
            }

            // Re-checked every frame: a live hull can change pilot (PilotSwap, Cellular Duel's round swap).
            bool audible = !onlyAudibleToController || (_status.Player != null && _status.IsLocalUser);
            if (audible != _audible)
            {
                _audible = audible;
                if (!audible) StopAll(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
            if (!audible) return;

            TickBall(Time.deltaTime);
            TickTrigger(ref _left, _thresher.LeftTriggerDepth);
            TickTrigger(ref _right, _thresher.RightTriggerDepth);
        }

        // ------------------------------------------------------------------ morning star

        void TickBall(float dt)
        {
            if (!_ball.Started)
            {
                if (morningStarLoop.IsNull)
                {
                    if (!_warnedEmptyBall)
                    {
                        _warnedEmptyBall = true;
                        CSDebug.LogWarning($"[ThresherAudioController] '{name}' has no Morning Star Loop assigned; the ball is silent until one is authored.", this);
                    }
                    return;
                }
                if (!StartVoice(ref _ball, morningStarLoop, ballSpeedParameter, attachToShip: false)) return;

                _hasHotParam = TryFindParam(_ball.Instance, ballHotParameter, out _hotParamId, out _hotParamGlobal);
                _smoothedSpeed = _thresher.Gauge01;
            }

            if (!_ball.Instance.isValid()) return;

            var attributes = RuntimeUtils.To3DAttributes(_thresher.BallPosition);
            attributes.velocity = RuntimeUtils.ToFMODVector(_thresher.BallVelocity);
            _ball.Instance.set3DAttributes(attributes);

            float alpha = ballSpeedSmoothing > 0f ? 1f - Mathf.Exp(-dt * ballSpeedSmoothing) : 1f;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, _thresher.Gauge01, alpha);
            PushParam(ref _ball, _smoothedSpeed);
            if (_hasHotParam)
                _ball.Instance.setParameterByID(_hotParamId, _thresher.IsHot ? 1f : 0f, _hotParamGlobal);
        }

        // ------------------------------------------------------------------ triggers

        void OnLeftPressed() => Press(ref _left, leftTriggerEvent, leftTriggerDepthParameter, ref _warnedEmptyLeft, "Left Trigger");
        void OnRightPressed() => Press(ref _right, rightTriggerEvent, rightTriggerDepthParameter, ref _warnedEmptyRight, "Right Trigger");
        void OnLeftReleased() => Release(ref _left, leftTriggerReleaseEvent, "LT");
        void OnRightReleased() => Release(ref _right, rightTriggerReleaseEvent, "RT");

        void Press(ref Voice voice, EventReference evt, string param, ref bool warned, string label)
        {
            if (!_audible) return;
            // A re-press while the last press is still sounding restarts it cleanly.
            StopVoice(ref voice, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            if (evt.IsNull)
            {
                if (!warned)
                {
                    warned = true;
                    CSDebug.LogWarning($"[ThresherAudioController] '{name}' has no {label} Event assigned; silent until one is authored.", this);
                }
                return;
            }
            StartVoice(ref voice, evt, param, attachToShip: true);
            if (debugLog && CSDebug.IsVerbose(CSLogChannel.Audio))
                CSDebug.LogVerbose(CSLogChannel.Audio, $"[ThresherAudioController] '{name}' {label} press.", this);
        }

        void Release(ref Voice voice, EventReference releaseEvt, string label)
        {
            StopVoice(ref voice, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            if (!_audible) return;
            FMODOneShotVolumeHelper.PlaySFXOneShotAttached(releaseEvt, gameObject, ResolveSFXVolume());
            if (debugLog && CSDebug.IsVerbose(CSLogChannel.Audio))
                CSDebug.LogVerbose(CSLogChannel.Audio, $"[ThresherAudioController] '{name}' {label} release.", this);
        }

        void TickTrigger(ref Voice voice, float depth)
        {
            if (!voice.Started || !voice.Instance.isValid()) return;
            // A one-shot press event that has finished on its own: let it go now rather than at the release.
            if (voice.Instance.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
            {
                StopVoice(ref voice, FMOD.Studio.STOP_MODE.IMMEDIATE);
                return;
            }
            PushParam(ref voice, depth);
        }

        // ------------------------------------------------------------------ plumbing

        bool StartVoice(ref Voice voice, EventReference evt, string param, bool attachToShip)
        {
            if (!FmodSafe.TryCreateInstance(evt, out voice.Instance, this)) return false;
            voice.HasParam = TryFindParam(voice.Instance, param, out voice.ParamId, out voice.ParamGlobal);
            voice.AppliedVolume = -1f;
            ApplyVolume(ref voice);
            if (attachToShip) FmodSafe.Attach(voice.Instance, gameObject);
            else voice.Instance.set3DAttributes(RuntimeUtils.To3DAttributes(_thresher.BallPosition));

            var result = voice.Instance.start();
            voice.Started = result == FMOD.RESULT.OK;
            if (!voice.Started)
            {
                CSDebug.LogError($"[ThresherAudioController] '{name}' start() returned {result} on '{evt}'.", this);
                FmodSafe.StopAndRelease(ref voice.Instance, false, FMOD.Studio.STOP_MODE.IMMEDIATE);
            }
            return voice.Started;
        }

        static bool TryFindParam(EventInstance instance, string name, out PARAMETER_ID id, out bool global)
        {
            id = default;
            global = false;
            if (string.IsNullOrEmpty(name) || !instance.isValid()) return false;
            if (instance.getDescription(out EventDescription desc) != FMOD.RESULT.OK) return false;
            if (desc.getParameterDescriptionByName(name, out PARAMETER_DESCRIPTION p) != FMOD.RESULT.OK) return false;
            id = p.id;
            global = (p.flags & PARAMETER_FLAGS.GLOBAL) != 0;
            return true;
        }

        static void PushParam(ref Voice voice, float value)
        {
            if (!voice.HasParam || !voice.Instance.isValid()) return;
            voice.Instance.setParameterByID(voice.ParamId, Mathf.Clamp01(value), voice.ParamGlobal);
        }

        static void StopVoice(ref Voice voice, FMOD.Studio.STOP_MODE mode)
        {
            FmodSafe.StopAndRelease(ref voice.Instance, voice.Started, mode);
            voice.Started = false;
            voice.HasParam = false;
        }

        void StopAll(FMOD.Studio.STOP_MODE mode)
        {
            StopVoice(ref _ball, mode);
            StopVoice(ref _left, mode);
            StopVoice(ref _right, mode);
            _hasHotParam = false;
        }

        void SetPaused(bool paused)
        {
            if (_ball.Started && _ball.Instance.isValid()) _ball.Instance.setPaused(paused);
            if (_left.Started && _left.Instance.isValid()) _left.Instance.setPaused(paused);
            if (_right.Started && _right.Instance.isValid()) _right.Instance.setPaused(paused);
        }

        void ApplyVolume(ref Voice voice)
        {
            if (!voice.Instance.isValid()) return;
            float volume = ResolveSFXVolume();
            if (Mathf.Abs(volume - voice.AppliedVolume) < 0.001f) return;
            voice.AppliedVolume = volume;
            voice.Instance.setVolume(volume);
        }

        float ResolveSFXVolume() => AudioSystem.ResolveSfxInstanceVolume(baseVolumeMultiplier);

        void OnSFXLevelChanged(float level) => ApplyAllVolumes();
        void OnSFXEnabledChanged(bool enabled) => ApplyAllVolumes();

        void ApplyAllVolumes()
        {
            ApplyVolume(ref _ball);
            ApplyVolume(ref _left);
            ApplyVolume(ref _right);
        }
    }
}

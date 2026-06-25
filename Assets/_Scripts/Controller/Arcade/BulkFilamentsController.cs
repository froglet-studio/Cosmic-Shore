using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single-player Squirrel prototype for riding Bulk energy filaments through a
    /// wormhole by timing auto-aimed latch-ring transfers.
    /// </summary>
    public partial class BulkFilamentsController : SinglePlayerMiniGameControllerBase
    {
        const string MusicResourcePath = "Audio/Music/Dopamine";
        const float MinFilamentDiameterRatio = 0.65f;
        const float MaxFilamentDiameterRatio = 1f;
        const float FilamentTravelRatio = 0.92f;
        const float TransferDistanceRatio = 0.84f;
        const float LatchTriggerPressPoint = 0.42f;

        [Header("Run Length")]
        [SerializeField, Min(1)] int intensityOneTransfers = 24;
        [SerializeField, Min(1)] int transfersAddedPerIntensity = 2;
        [SerializeField, Min(1)] int minMusicTransfers = 20;
        [SerializeField, Min(1)] int maxMusicTransfers = 30;
        [SerializeField, Min(1f)] float targetSecondsPerTransfer = 13.5f;

        [Header("Filament Motion")]
        [SerializeField, Min(1f)] float minimumSpeed = 22f;
        [SerializeField, Min(1f)] float maximumSpeed = 64f;
        [SerializeField, Min(0f)] float automaticAcceleration = 6.2f;
        [SerializeField, Min(0f)] float throttleBias = 19f;
        [SerializeField, Min(0f)] float orbitDegreesPerSecond = 130f;
        [SerializeField, Min(0f)] float orbitThrusterAcceleration = 360f;
        [SerializeField, Min(0f)] float orbitMaxAngularVelocity = 310f;
        [SerializeField, Min(0f)] float orbitAngularDrag = 7.5f;
        [SerializeField, Min(0f)] float transferAngularDamping = 0.22f;
        [SerializeField, Min(0.1f)] float orbitRadius = 18f;
        [SerializeField, Min(0.1f)] float tetherRise = 7f;
        [SerializeField, Min(0.01f)] float filamentLengthMeanDiameter = 0.84f;
        [SerializeField, Min(0.01f)] float filamentLengthStdDevDiameter = 0.1f;
        [SerializeField, Min(1f)] float filamentRisePerTransfer = 32f;
        [SerializeField, Min(0f)] float filamentTransferNudge = 22f;
        [SerializeField, Min(0f)] float filamentRotationMinDegreesPerSecond = 3.5f;
        [SerializeField, Min(0f)] float filamentRotationMaxDegreesPerSecond = 8.5f;
        [SerializeField, Min(0f)] float filamentWaveAmplitude = 3.4f;
        [SerializeField, Min(0f)] float filamentWaveSpeed = 0.34f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] float slowSpeedLatchWindow = 48f;
        [SerializeField, Min(0.1f)] float fastSpeedLatchWindow = 24f;
        [SerializeField, Min(0f)] float missCooldown = 0.35f;
        [SerializeField, Min(0f)] float respawnTimePenalty = 4f;

        [Header("Power Crystals")]
        [SerializeField, Min(0f)] float powerCrystalSpeedImpulse = 12f;
        [SerializeField, Min(0f)] float powerCrystalStackBonus = 2.8f;
        [SerializeField, Min(1f)] float speedDiamondScaleMultiplier = 4f;
        [SerializeField, Min(0.1f)] float speedDiamondPickupRadius = 10f;

        [Header("Pulse Gates")]
        [SerializeField, Range(0.05f, 0.5f)] float pulseGateRouteInterval = 0.15f;
        [SerializeField, Min(0f)] float pulseGateSpeedImpulse = 17f;
        [SerializeField, Min(0f)] float pulseGateStackBonus = 2.2f;

        [Header("Nanite Chase")]
        [SerializeField, Min(0f)] float naniteBaseSpeed = 8f;
        [SerializeField, Min(0f)] float naniteSpeedPerIntensity = 2.8f;
        [SerializeField, Min(1f)] float naniteCatchBuffer = 34f;
        [SerializeField, Min(0f)] float naniteRespawnSetback = 46f;
        [SerializeField, Min(0f)] float naniteDirectionBurstCooldown = 0.18f;
        [SerializeField, Min(1f)] float naniteVisualTailDistance = 24f;

        [Header("Wormhole Visuals")]
        [SerializeField, Min(8f)] float tubeRadius = 440f;
        [SerializeField, Min(3)] int tubeRingCount = 160;
        [SerializeField, Min(8)] int tubeRingResolution = 80;
        [SerializeField, Min(8)] int mirrorWallSegments = 96;
        [SerializeField, Min(3)] int mirrorWallRingCount = 38;
        [SerializeField, Min(0.01f)] float musicBpm = 128f;

        [Header("Camera")]
        [SerializeField, Min(0f)] float introCameraDuration = 5.5f;
        [SerializeField, Min(0f)] float cameraLookDegreesPerSecond = 72f;
        [SerializeField, Min(0f)] float cameraLookPitchLimit = 42f;

        [Header("Bulk Break Finale")]
        [SerializeField, Min(1f)] float missionFinaleDuration = 6.25f;
        [SerializeField, Min(10f)] float missionFinaleLaunchDistance = 680f;
        [SerializeField, Min(0f)] float missionFinaleStarfieldRadius = 90f;

        readonly List<FilamentRuntime> _filaments = new();
        readonly List<LineRenderer> _tubeRings = new();
        readonly List<LineRenderer> _tethers = new();
        readonly List<LineRenderer> _latchRings = new();
        readonly List<GameObject> _hazards = new();
        readonly List<GameObject> _nanites = new();
        readonly List<float> _naniteRespawnTimers = new();
        readonly List<HazardRuntime> _hazardRuntimes = new();
        readonly List<PulseGateRuntime> _pulseGates = new();
        readonly List<TransientShardRuntime> _transientShards = new();
        readonly List<GlyphSpriteRuntime> _glyphSprites = new();

        GameObject _runtimeRoot;
        GameObject _missionFinaleRoot;
        Material _activeFilamentMaterial;
        Material _nextFilamentMaterial;
        Material _whiteEnergyMaterial;
        Material _tubeMaterial;
        Material _crystalMaterial;
        Material _hazardMaterial;
        Material _naniteMaterial;
        Material _mirrorWallMaterial;
        Material _gateMaterial;
        Material _shardMaterial;
        Material _glyphMaterial;
        LineRenderer _naniteWakeLine;
        ReflectionProbe _mirrorReflectionProbe;

        AudioSource _musicSource;
        Camera _mainCamera;
        IVessel _vessel;

        int _targetTransfers;
        int _currentFilamentIndex;
        int _successfulTransfers;
        int _crystalsCollected;
        int _respawns;

        float _distanceOnFilament;
        float _orbitAngle;
        float _speed;
        float _elapsedTime;
        float _naniteRouteDistance;
        float _missTimer;
        float _swingTimer;
        float _impactTimer;
        float _orbitAngularVelocity;
        float _cameraIntroTimer;
        float _cameraLookPitch;
        float _crystalSpeedBonus;
        float _nextNaniteDirectionBurstTime;
        float _nextMirrorProbeRefreshTime;
        float _missionFinaleTimer;
        float _missionFinaleHudPulse;
        int _lastOrbitInputSign;
        bool _isRunning;
        bool _turnFinished;
        bool _missionFinaleActive;
        Vector3 _missionFinaleStartPosition;
        Vector3 _missionFinaleLaunchDirection;
        Quaternion _missionFinaleStartRotation;

        protected override bool UseGolfRules => true;

        int Intensity => Mathf.Clamp(gameData != null ? gameData.SelectedIntensity.Value : 1, 1, 4);
        FilamentRuntime CurrentFilament => _filaments[Mathf.Clamp(_currentFilamentIndex, 0, _filaments.Count - 1)];
        float PlayerRouteDistance => _filaments.Count == 0 ? _distanceOnFilament : CurrentFilament.RouteStartDistance + _distanceOnFilament;
        float RunProgress01 => _targetTransfers <= 0 || _filaments.Count == 0
            ? 0f
            : Mathf.Clamp01((_successfulTransfers + Mathf.Clamp01(_distanceOnFilament / Mathf.Max(1f, CurrentFilament.TravelLength))) / _targetTransfers);
        float FinaleIntensity01 => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, RunProgress01));
        float CurrentMaximumSpeed => maximumSpeed + Intensity * 4f + _crystalSpeedBonus + FinaleIntensity01 * 14f;

        protected override void Start()
        {
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            if (gameData != null)
                gameData.selectedVesselClass.Value = VesselClassType.Squirrel;

            DisableCopiedTurnMonitorController();
            base.Start();
        }

        protected override void SetupNewTurn()
        {
            BuildRun();
            ConfigureNewTurnStartFlow();
            base.SetupNewTurn();
        }

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded();

            _isRunning = true;
            _turnFinished = false;
            _elapsedTime = 0f;
            _missTimer = 0f;
            _speed = minimumSpeed;
            _naniteRouteDistance = -naniteCatchBuffer - naniteRespawnSetback;
            ResetLatchInputState();

            CSDebug.Log("[BulkFilaments] Countdown ended; run active.");
            StartMusic();
            AcquireVessel();
        }

        protected override void OnResetForReplay()
        {
            ResetRuntime();
            base.OnResetForReplay();
        }

        void Update()
        {
            AnimateWormhole();
            AnimateMirrorWall();
            UpdateDynamicFilamentPoses();
            AnimateFilamentColors();
            AnimateFilamentWaveforms();
            AnimateGlyphSprites();
            AnimateNanites();
            AnimatePulseGates();
            UpdateTransientShards(Time.deltaTime);
            EnforceBulkAudioMix();
            TickAutoStartCountdown();

            if (_missionFinaleActive)
            {
                UpdateMissionFinale(Time.deltaTime);
                return;
            }

            if (!_isRunning || _turnFinished || !AcquireVessel())
                return;

            float dt = Time.deltaTime;
            _elapsedTime += dt;
            _missTimer = Mathf.Max(0f, _missTimer - dt);
            _impactTimer = Mathf.Max(0f, _impactTimer - dt);
            _swingTimer = Mathf.Max(0f, _swingTimer - dt);

            Vector2 orbitInput = ReadOrbitInput();
            float throttleInput = ReadThrottleInput();
            Vector2 lookInput = ReadCameraLookInput();
            UpdateOrbitThruster(orbitInput.x, dt);
            _cameraLookPitch = Mathf.Clamp(_cameraLookPitch + lookInput.y * cameraLookDegreesPerSecond * dt, -cameraLookPitchLimit, cameraLookPitchLimit);
            UpdateCameraZoom(lookInput.x, dt);

            float speed01 = Mathf.InverseLerp(minimumSpeed, maximumSpeed, _speed);
            float acceleration = automaticAcceleration + throttleInput * throttleBias - speed01 * 2f;
            _speed = Mathf.Clamp(_speed + acceleration * dt, minimumSpeed * 0.55f, CurrentMaximumSpeed);
            _distanceOnFilament += _speed * dt;

            AdvanceNanites(dt, throttleInput);
            TickLatchState(dt);

            LatchInput latchInput = ReadLatchInput();
            if (latchInput != LatchInput.None)
                TryTransferLatch(latchInput);
            TryHeldLatchRequests();

            if (_distanceOnFilament > CurrentFilament.TravelLength)
            {
                RespawnAtPreviousFilament("late transfer wall strike");
                return;
            }

            if (_naniteRouteDistance > PlayerRouteDistance - naniteCatchBuffer)
            {
                RespawnAtPreviousFilament("filament nanites");
                return;
            }

            UpdateVesselPose();
            CollectNearbyCrystals();
            CheckPulseGatePassage();
            CheckHazardGraze();
            AnimateLightning(dt);
            UpdateRoundStats();
        }

        void LateUpdate()
        {
            if (_vessel == null)
                return;

            if (_missionFinaleActive)
                return;

            UpdateCamera();
            UpdateLatchRig();
        }

        protected override void OnDisable()
        {
            ResetRuntime();
            base.OnDisable();
        }

        void BuildRun()
        {
            ResetRuntime();

            _targetTransfers = ResolveTargetTransferCount();
            _currentFilamentIndex = 0;
            _successfulTransfers = 0;
            _crystalsCollected = 0;
            _respawns = 0;
            _distanceOnFilament = 0f;
            _orbitAngle = 0f;
            _orbitAngularVelocity = 0f;
            _lastOrbitInputSign = 0;
            _nextNaniteDirectionBurstTime = 0f;
            _nextMirrorProbeRefreshTime = 0f;
            _missionFinaleActive = false;
            _missionFinaleTimer = 0f;
            _missionFinaleHudPulse = 0f;
            _cameraLookPitch = 0f;
            _cameraIntroTimer = introCameraDuration;
            _speed = minimumSpeed;
            _crystalSpeedBonus = 0f;

            _runtimeRoot = new GameObject("Bulk Filaments Runtime");

            CreateMaterials();
            CreateWormhole();
            CreateMirrorWall();
            CreateMirrorReflectionProbe();
            CreateFilaments();
            CreatePulseGates();
            ResetLightningSchedule();
            EnsureMainCamera();
            SetEstablishingCameraPose();
            CreateLatchRig();
            CreateNaniteSwarm();
        }

        void ResetRuntime()
        {
            _isRunning = false;
            _turnFinished = false;
            _vessel = null;
            _filaments.Clear();
            _tubeRings.Clear();
            ResetFilamentWaveforms();
            _tethers.Clear();
            _latchRings.Clear();
            _hazards.Clear();
            _hazardRuntimes.Clear();
            _nanites.Clear();
            _naniteRespawnTimers.Clear();
            _pulseGates.Clear();
            _transientShards.Clear();
            _glyphSprites.Clear();
            _naniteWakeLine = null;
            _mirrorReflectionProbe = null;
            _missionFinaleRoot = null;
            _missionFinaleActive = false;

            if (_musicSource)
                _musicSource.Stop();

            RestoreBulkAudioMix();
            ResetLightningState();

            if (_runtimeRoot)
                Destroy(_runtimeRoot);

            ResetLatchInputState();
        }

        void DisableCopiedTurnMonitorController()
        {
            var monitorController = GetComponent<TurnMonitorController>();
            if (!monitorController || !monitorController.enabled)
                return;

            monitorController.enabled = false;
            CSDebug.Log("[BulkFilaments] Disabled copied scene turn monitors; Bulk owns its transfer finish condition.");
        }

        sealed class FilamentRuntime
        {
            public int Index;
            public Vector3 BaseCenter;
            public Vector3 BaseDirection;
            public Vector3 Center;
            public Vector3 Direction;
            public Vector3 Side;
            public Vector3 Up;
            public float Length;
            public float TravelLength;
            public float TransferDistance;
            public float RouteStartDistance;
            public float RotationPhaseDegrees;
            public float RotationSpeedDegrees;
            public float WaveAmplitude;
            public float WaveSpeed;
            public float WavePhase;
            public readonly float[] WaveFrequencies = new float[5];
            public readonly float[] WavePhases = new float[5];
            public readonly float[] WaveWeights = new float[5];
            public LineRenderer Beam;
            public readonly List<CrystalRuntime> Crystals = new();
        }

        sealed class CrystalRuntime
        {
            public GameObject GameObject;
            public Vector3 Position;
            public float Distance;
            public float OrbitAngleRadians;
            public float HueOffset;
            public bool Collected;
            public Renderer Renderer;
        }

        sealed class HazardRuntime
        {
            public GameObject GameObject;
            public FilamentRuntime Filament;
            public float Distance;
            public float OrbitAngleRadians;
            public float SpinDegreesPerSecond;
        }

        sealed class PulseGateRuntime
        {
            public FilamentRuntime Filament;
            public LineRenderer Ring;
            public LineRenderer Core;
            public float Distance;
            public bool Triggered;
            public float PulseTimer;
        }

        sealed class TransientShardRuntime
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public float Age;
            public float Lifetime;
            public Vector3 BaseScale;
        }

        enum GlyphAnchorKind { Filament, LatchRing }

        sealed class GlyphSpriteRuntime
        {
            public Transform Transform;
            public GlyphAnchorKind Anchor;
            public FilamentRuntime Filament;
            public float Distance;
            public float OrbitAngleRadians;
            public int RingIndex;
            public float Ring01;
            public Vector2 BaseScale;
            public float Phase;
        }
    }
}

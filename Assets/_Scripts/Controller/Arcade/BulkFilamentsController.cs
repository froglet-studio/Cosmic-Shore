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
        [SerializeField, Min(0.1f)] float orbitRadius = 18f;
        [SerializeField, Min(0.1f)] float tetherRise = 7f;
        [SerializeField, Min(0.01f)] float filamentLengthMeanDiameter = 0.84f;
        [SerializeField, Min(0.01f)] float filamentLengthStdDevDiameter = 0.1f;
        [SerializeField, Min(1f)] float filamentRisePerTransfer = 32f;
        [SerializeField, Min(0f)] float filamentTransferNudge = 22f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] float slowSpeedLatchWindow = 48f;
        [SerializeField, Min(0.1f)] float fastSpeedLatchWindow = 24f;
        [SerializeField, Min(0f)] float missCooldown = 0.35f;
        [SerializeField, Min(0f)] float respawnTimePenalty = 4f;

        [Header("Power Crystals")]
        [SerializeField, Min(0f)] float powerCrystalSpeedImpulse = 12f;
        [SerializeField, Min(0f)] float powerCrystalStackBonus = 2.8f;

        [Header("Nanite Chase")]
        [SerializeField, Min(0f)] float naniteBaseSpeed = 8f;
        [SerializeField, Min(0f)] float naniteSpeedPerIntensity = 2.8f;
        [SerializeField, Min(1f)] float naniteCatchBuffer = 34f;
        [SerializeField, Min(0f)] float naniteRespawnSetback = 46f;

        [Header("Wormhole Visuals")]
        [SerializeField, Min(8f)] float tubeRadius = 440f;
        [SerializeField, Min(3)] int tubeRingCount = 160;
        [SerializeField, Min(8)] int tubeRingResolution = 80;
        [SerializeField, Min(0.01f)] float musicBpm = 128f;

        [Header("Camera")]
        [SerializeField, Min(0f)] float introCameraDuration = 5.5f;
        [SerializeField, Min(0f)] float cameraLookDegreesPerSecond = 72f;
        [SerializeField, Min(0f)] float cameraLookPitchLimit = 42f;

        readonly List<FilamentRuntime> _filaments = new();
        readonly List<LineRenderer> _tubeRings = new();
        readonly List<LineRenderer> _tethers = new();
        readonly List<LineRenderer> _latchRings = new();
        readonly List<GameObject> _hazards = new();
        readonly List<GameObject> _nanites = new();

        GameObject _runtimeRoot;
        Material _activeFilamentMaterial;
        Material _nextFilamentMaterial;
        Material _whiteEnergyMaterial;
        Material _tubeMaterial;
        Material _crystalMaterial;
        Material _hazardMaterial;
        Material _naniteMaterial;

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
        float _cameraIntroTimer;
        float _cameraLookPitch;
        float _crystalSpeedBonus;
        bool _isRunning;
        bool _turnFinished;

        protected override bool UseGolfRules => true;

        int Intensity => Mathf.Clamp(gameData != null ? gameData.SelectedIntensity.Value : 1, 1, 4);
        FilamentRuntime CurrentFilament => _filaments[Mathf.Clamp(_currentFilamentIndex, 0, _filaments.Count - 1)];
        float PlayerRouteDistance => _filaments.Count == 0 ? _distanceOnFilament : CurrentFilament.RouteStartDistance + _distanceOnFilament;
        float CurrentMaximumSpeed => maximumSpeed + Intensity * 4f + _crystalSpeedBonus;

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
            AnimateFilamentColors();
            AnimateFilamentWaveforms();
            AnimateNanites();
            TickAutoStartCountdown();

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
            _orbitAngle += orbitInput.x * orbitDegreesPerSecond * dt;
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
            CheckHazardGraze();
            AnimateLightning(dt);
            UpdateRoundStats();
        }

        void LateUpdate()
        {
            if (_vessel == null)
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
            _cameraLookPitch = 0f;
            _cameraIntroTimer = introCameraDuration;
            _speed = minimumSpeed;
            _crystalSpeedBonus = 0f;

            _runtimeRoot = new GameObject("Bulk Filaments Runtime");

            CreateMaterials();
            CreateWormhole();
            CreateFilaments();
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
            _nanites.Clear();

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
            public Vector3 Center;
            public Vector3 Direction;
            public Vector3 Side;
            public Vector3 Up;
            public float Length;
            public float TravelLength;
            public float TransferDistance;
            public float RouteStartDistance;
            public LineRenderer Beam;
            public readonly List<CrystalRuntime> Crystals = new();
        }

        sealed class CrystalRuntime
        {
            public GameObject GameObject;
            public Vector3 Position;
            public bool Collected;
        }
    }
}

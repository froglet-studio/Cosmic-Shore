using CosmicShore.Core;
using CosmicShore.Gameplay;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public sealed class ToggleTranslationModeActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [SerializeField] private VesselPrismController vesselPrismController;

        [Header("Seeding")]
        [SerializeField] private SeedAssemblerActionExecutor seedAssemblerExecutor;
        [SerializeField] private SeedWallActionSO stationarySeedConfig;

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged;

        [Header("MiniGame")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        [Header("Camera")]
        [Tooltip("How much of the follow camera's authored HEIGHT (CameraSettingsSO.followOffset.y) the " +
                 "camera keeps while this vessel FLIES: 1 = the authored height, 0 = level with the hull, " +
                 "directly behind it. The stance always keeps the full authored height, so below 1 the " +
                 "camera rises when the vessel stops into its stance and drops again when it flies. The " +
                 "Sparrow ships 0 (its elevated view is the turret's); the Serpent and the Stoat keep 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float flightCameraHeight = 1f;

        [Tooltip("Seconds for the camera to move between its flight height and its stance height.")]
        [Min(0.01f)]
        [SerializeField] private float cameraBlendSeconds = 0.35f;

        IVessel _ship;
        IVesselStatus _status;
        ActionExecutorRegistry _registry;
        int _lastToggleFrame = -1;
        float _stanceBlend;
        CustomCameraController _camera;

        /// <summary>The camera's height scale for a stance blend (0 = flying, 1 = in the stance).</summary>
        public static float CameraHeightScale(float flightHeight, float stanceBlend) =>
            Mathf.Lerp(Mathf.Clamp01(flightHeight), 1f, Mathf.Clamp01(stanceBlend));
    
        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            if (IsFramedBy(_camera)) _camera.FollowHeightScale = 1f;
        }

        /// <summary>
        /// The stance's camera: the follow rig keeps its authored height in the stance and
        /// <see cref="flightCameraHeight"/> of it while flying. Read from the replicated
        /// <c>IsTranslationRestricted</c> every frame rather than set from <see cref="Toggle"/>, so
        /// every way out of the stance (a turn end, the Stoat's sling, a peer's replicated flag)
        /// moves the camera too. Written only while the player rig frames THIS vessel, and on the
        /// camera's own <c>FollowHeightScale</c> - never <c>followOffset</c>, which every re-applied
        /// <c>CameraSettingsSO</c> overwrites (the Butterfly's Mass mode is the same pattern).
        /// </summary>
        void Update()
        {
            if (flightCameraHeight >= 1f || _status == null) return;

            float target = _status.IsTranslationRestricted ? 1f : 0f;
            _stanceBlend = Mathf.MoveTowards(_stanceBlend, target, Time.deltaTime / cameraBlendSeconds);

            var cam = PlayerCamera();
            if (IsFramedBy(cam))
                cam.FollowHeightScale = CameraHeightScale(flightCameraHeight, _stanceBlend);
        }

        /// <summary>The player's follow rig, resolved once. Null without a <c>CameraManager</c> (tool scenes).</summary>
        CustomCameraController PlayerCamera()
        {
            if (_camera) return _camera;
            var rig = CameraManager.Instance ? CameraManager.Instance.GetCloseCamera() : null;
            if (rig) rig.TryGetComponent(out _camera);
            return _camera;
        }

        /// <summary>True while <paramref name="cam"/> follows THIS vessel (the local pilot's, or one spectated).</summary>
        bool IsFramedBy(CustomCameraController cam)
        {
            if (!cam || _status == null) return false;
            var target = _status.CameraFollowTarget;
            return target && cam.FollowTarget == target;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status   = shipStatus;
            _ship     = shipStatus?.Vessel;
            _registry = GetComponent<ActionExecutorRegistry>();

            if (vesselPrismController == null)
                vesselPrismController = shipStatus?.VesselPrismController;

            if (seedAssemblerExecutor == null && _registry != null)
                seedAssemblerExecutor = _registry.Get<SeedAssemblerActionExecutor>();

            // A fresh pilot starts at its current stance's height, not eased in from the last one.
            _stanceBlend = _status is { IsTranslationRestricted: true } ? 1f : 0f;
        }

         public void Toggle(ToggleTranslationModeActionSO so, IVessel ship, IVesselStatus status)
        {
            if (!so || status == null) return;

            if (Time.frameCount == _lastToggleFrame) return;
            _lastToggleFrame = Time.frameCount;

            var controller = status.Vessel as VesselController;
            if (!controller) return;

            bool netActive = NetworkManager.Singleton && NetworkManager.Singleton.IsListening &&
                             (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);
            bool hasAuthority = !netActive || NetworkManager.Singleton.IsServer || controller.IsOwner;
            if (!hasAuthority) return;

            bool isOn = !status.IsTranslationRestricted;

            controller.SetTranslationRestricted(isOn);

            if (so.StationaryMode == ToggleTranslationModeActionSO.Mode.Serpent && seedAssemblerExecutor)
            {
                if (isOn)
                {
                    // The stance's seed is held, not super-shielded: its wall steals until the
                    // Serpent flies again (SERPENT_SEED_WALL.md, "The stance's wall").
                    var seeded = seedAssemblerExecutor.StartSeed(stationarySeedConfig, status, heldByStance: true);
                    vesselPrismController?.StopSpawn();
                    if (seeded) seedAssemblerExecutor.BeginBonding();
                }
                else
                {
                    vesselPrismController?.StartSpawn();
                    seedAssemblerExecutor.ReleaseHeldWall();
                    seedAssemblerExecutor.StopSeedCompletely();
                }
            }
            else
            {
                if (isOn)
                {
                    vesselPrismController?.StopSpawn();
                }
                else
                {
                    vesselPrismController?.StartSpawn();
                }
            }

            stationaryModeChanged?.Raise(isOn);
        }
     
     

        /// <summary>
        /// Leave the stance now, if in it — the same exit a mini-game turn end takes (through the
        /// controller, so the replicated flag clears on every peer, prism spawning resumed,
        /// <c>stationaryModeChanged</c> raised). The Stoat's sling calls it: a slung wormhole pair
        /// is a launch, and a hull held in the stance would ignore the pull (STOAT.md §1).
        /// </summary>
        public void EndStance() => End();

        void End()
        {
            if (_status == null) return;

            if (!_status.IsTranslationRestricted) return;

            // Through the controller, the way Toggle does: writing the status alone left the
            // owner's replicated n_IsTranslationRestricted at TRUE, so every other peer went on
            // seeing this vessel stopped (and the next stance press on the owner wrote a value the
            // variable already held, which replicates nothing).
            if (_status.Vessel is VesselController controller) controller.SetTranslationRestricted(false);
            else _status.IsTranslationRestricted = false;

            vesselPrismController?.StartSpawn();
            if (seedAssemblerExecutor)
            {
                seedAssemblerExecutor.ReleaseHeldWall();
                seedAssemblerExecutor.StopSeedCompletely();
            }

            stationaryModeChanged?.Raise(false);
        }
    }
}

using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
 private R_VesselActionHandler _actions;
        private VesselHUDView _view;
        private IVesselStatus _status;

        [SerializeField] private int jawResourceIndex;

        // Angles
        private const float RestAngleDeg = 0f;
        private const float FullAngleDeg = -180f;

        [Header("Drift Thresholds")]
        [Tooltip("When |dot| >= this, we begin following vessel yaw (engaged).")]
        [Range(0f, 1f)] public float engageThreshold = 0.20f;

        [Tooltip("When |dot| >= this or yaw progress >= 1, we latch at -180.")]
        [Range(0f, 1f)] public float fullThreshold = 0.98f;

        [Header("Silhouette Feel")]
        [Tooltip("Base smoothing time for silhouette (higher = heavier).")]
        public float silhouetteSmoothTime = 0.45f;

        [Tooltip("Extra lag (seconds) that makes the UI chase the ship yaw.")]
        public float silhouetteLagSeconds = 0.12f;

        [Tooltip("How much micro-wobble to inject (degrees) when the ship spins fast.")]
        [Range(0f, 10f)] public float jankAmplitude = 3.0f;

        [Tooltip("Angular velocity (deg/s) at which wobble hits max amplitude.")]
        public float jankMaxVel = 360f;

        [Tooltip("Perlin noise frequency for wobble.")]
        public float jankNoiseFreq = 2.2f;

        [Tooltip("Extra overshoot in degrees when we finish the flip to -180.")]
        [Range(0f, 20f)] public float latchOvershootDeg = 6f;

        [Tooltip("Time to settle from overshoot back to -180 (seconds).")]
        public float latchSettleTime = 0.25f;

        [Header("Trail Feel")]
        [Tooltip("Trail per-row rotation smoothing time (seconds).")]
        public float trailSmoothTime = 0.45f;

        [Tooltip("How fast UI reacts to vessel yaw progress (0..2). Higher = snappier.")]
        [Range(0.25f, 2.0f)] public float progressResponsiveness = 1.0f;

        private float _silhouetteTargetDegY;
        private float _silhouetteCurrentDegY;
        private float _silhouetteVelDegY;

        // Drift -> follow vessel yaw
        private bool  _driftEngaged;
        private bool  _driftLatched;
        private float _yawBaselineDeg;   // captured on engage

        // Overshoot state
        private bool  _justLatched;
        private float _latchTimer;

        // Vessel yaw tracking for jank
        private float _prevYawDeg;
        private float _angVelDps; // deg per second

        private float _lastDot = 1f;

        private TrailPool _trailPool;
        private GameObject BlockPrefab { get; set; }

        public virtual void Initialize(IVesselStatus vesselStatus, VesselHUDView view)
        {
            _status = vesselStatus;
            _view   = view;

            if (_status.AutoPilotEnabled)
            {
                view.gameObject.SetActive(false);
                return;
            }

            _actions = vesselStatus.ActionHandler;
            if (_actions != null)
            {
                _actions.OnInputEventStarted += HandleStart;
                _actions.OnInputEventStopped += HandleStop;
            }

            BindJaws();
            BindDrift();
            PrimeInitialUI();
        }

        private void Update()
        {
            // Compute drift-driven target (binary -180 or 0, but the path follows vessel yaw)
            UpdateDriftDrivenTargets(Time.unscaledDeltaTime);

            // Smooth the silhouette container Y rotation
            if (_view && _view.silhouetteContainer)
            {
                _silhouetteCurrentDegY = Mathf.SmoothDampAngle(
                    _silhouetteCurrentDegY,
                    _silhouetteTargetDegY,
                    ref _silhouetteVelDegY,
                    Mathf.Max(0.0001f, silhouetteSmoothTime),
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);

                var e = _view.silhouetteContainer.localEulerAngles;
                _view.silhouetteContainer.localEulerAngles = new Vector3(e.x, _silhouetteCurrentDegY, e.z);
            }

            // Smooth per-row trail rotation + keep child world Z locked
            _trailPool?.Tick(Time.unscaledDeltaTime);
        }

        private void PrimeInitialUI()
        {
            _silhouetteCurrentDegY = RestAngleDeg;
            _silhouetteTargetDegY  = RestAngleDeg;

            if (_view.silhouetteContainer)
            {
                var e = _view.silhouetteContainer.localEulerAngles;
                _view.silhouetteContainer.localEulerAngles = new Vector3(e.x, RestAngleDeg, e.z);
            }

            var resources = _status?.ResourceSystem?.Resources;
            if (resources == null ||
                _view.jawResourceIndex < 0 ||
                _view.jawResourceIndex >= resources.Count ||
                resources[_view.jawResourceIndex] == null) return;

            var normalized = 0f;
            try { normalized = resources[_view.jawResourceIndex].CurrentAmount; } catch { /* fallback 0 */ }
            OnJawResourceChanged(normalized);
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop (InputEvents ev) => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (!_view) return;
            for (var i = 0; i < _view.highlights.Count; i++)
            {
                if (_view.highlights[i].input == ev && _view.highlights[i].image != null)
                    _view.highlights[i].image.enabled = on;
            }
        }


        #region Silhouette (resources)

        private void BindJaws()
        {
            if (_view == null) return;
            if (_view.topJaw == null || _view.bottomJaw == null) return;
            if (_view.jawResourceIndex < 0) return;

            var resources = _status?.ResourceSystem?.Resources;
            if (resources == null || _view.jawResourceIndex >= resources.Count) return;

            var res = resources[_view.jawResourceIndex];
            res.OnResourceChange += OnJawResourceChanged;
        }

        private void OnJawResourceChanged(float normalized)
        {
            if (_view.silhouetteParts != null)
            {
                foreach (var go in _view.silhouetteParts)
                    if (go) go.SetActive(true);
            }

            if (_view.topJaw)    _view.topJaw.rectTransform.localRotation    = Quaternion.Euler(0, 0,  21f * normalized);
            if (_view.bottomJaw) _view.bottomJaw.rectTransform.localRotation = Quaternion.Euler(0, 0, -21f * normalized);

            var col = normalized > 0.98f ? Color.green : Color.white;
            if (_view.topJaw)    _view.topJaw.color    = col;
            if (_view.bottomJaw) _view.bottomJaw.color = col;
        }

        #endregion

        #region Drift hookups

        private void BindDrift()
        {
            if (_view == null) return;

            // Use DriftTrailActionExecutor’s dot stream
            var exec =  _view.driftTrailAction ;
            if (exec == null)
            {
                Debug.LogWarning("HUD: no DriftTrailActionExecutor on view; drift will not drive rotations.");
                return;
            }

            exec.OnChangeDriftAltitude += OnExecutorDriftDotChanged;
        }

        private void OnExecutorDriftDotChanged(float dot)
        {
            _lastDot = dot; // consumed in UpdateDriftDrivenTargets
        }

        #endregion

        #region Drift feel (follow vessel yaw, add AAA-style jank)

        private static float GetYawDeg(Quaternion q) => q.eulerAngles.y;

        private void UpdateAngVel(float dt)
        {
            float yaw = GetYawDeg(_status.blockRotation);
            float d   = Mathf.DeltaAngle(_prevYawDeg, yaw);
            _angVelDps = dt > 0f ? d / dt : 0f;
            _prevYawDeg = yaw;
        }

        private float AngleDeltaSigned(float fromDeg, float toDeg) => Mathf.DeltaAngle(fromDeg, toDeg); // -180..180

        private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        private float ComputeWobble(float time)
        {
            // Perlin noise mapped to [-1,1], scaled by angular velocity
            float vNorm = Mathf.Clamp01(Mathf.Abs(_angVelDps) / Mathf.Max(1f, jankMaxVel));
            float n = Mathf.PerlinNoise(time * jankNoiseFreq, 0.73f) * 2f - 1f;
            return n * jankAmplitude * vNorm;
        }

        private void UpdateDriftDrivenTargets(float dt)
        {
            float absDot = Mathf.Abs(_lastDot);

            // Engage when dot crosses threshold (we do NOT rotate on press; we wait for drift)
            if (!_driftEngaged && absDot >= engageThreshold && _lastDot < 1f)
            {
                _driftEngaged   = true;
                _driftLatched   = false;
                _justLatched    = false;
                _latchTimer     = 0f;
                _yawBaselineDeg = GetYawDeg(_status.blockRotation);
            }

            float baseTarget = RestAngleDeg;

            if (_driftEngaged)
            {
                // Follow vessel yaw delta to feel diegetic
                float currentYaw = GetYawDeg(_status.blockRotation);
                float deltaYaw   = AngleDeltaSigned(_yawBaselineDeg, currentYaw); // -180..180
                float progress   = Mathf.Clamp01(Mathf.Abs(deltaYaw) / 180f);
                progress         = Mathf.Clamp01(progress * progressResponsiveness);

                // Ease so it feels heavy at start, fast at end
                float eased = EaseOutCubic(progress);

                // Latch when ship finished turning (or dot is full)
                if (!_driftLatched && (progress >= 0.99f || absDot >= fullThreshold))
                {
                    _driftLatched = true;
                    _justLatched  = true;
                    _latchTimer   = 0f;
                }

                baseTarget = _driftLatched
                    ? FullAngleDeg
                    : Mathf.Lerp(0f, FullAngleDeg, eased);

                // Overshoot after latch, then settle back to -180
                if (_driftLatched)
                {
                    if (_justLatched)
                    {
                        baseTarget = FullAngleDeg - latchOvershootDeg; // a bit past -180
                        _justLatched = false; // only on the first frame we detect latch
                        _latchTimer = Mathf.Epsilon;
                    }
                    else if (_latchTimer > 0f && _latchTimer < latchSettleTime)
                    {
                        // Interpolate overshoot -> -180 across latchSettleTime (smooth)
                        float t = Mathf.Clamp01(_latchTimer / Mathf.Max(0.0001f, latchSettleTime));
                        float back = Mathf.Lerp(FullAngleDeg - latchOvershootDeg, FullAngleDeg, t);
                        baseTarget = back;
                        _latchTimer += dt;
                    }
                }

                // Add wobble based on actual angular velocity for AAA flavor
                baseTarget += ComputeWobble(Time.unscaledTime);
            }

            // Reset when drift ends (dot ~ 1 from executor End)
            if (absDot >= 0.999f)
            {
                _driftEngaged = false;
                _driftLatched = false;
                _justLatched  = false;
                _latchTimer   = 0f;
                baseTarget    = RestAngleDeg;
            }

            _silhouetteTargetDegY = baseTarget;
            _trailPool?.SetTargetDriftAngle(baseTarget); // keep trail rows in sync
        }

        #endregion

        #region Trail

        // Caller must set a REAL prefab. We will not create any fallback.
        public void SetBlockPrefab(GameObject prefab)
        {
            if (_view == null) return;
            if (prefab == null) return;

            bool needRebuild = BlockPrefab != prefab || _trailPool == null;
            BlockPrefab = prefab;

            if (_trailPool == null)
            {
                TryBindTrail();
            }
            else if (needRebuild)
            {
                RebuildTrailPool();
            }
        }

        private void TryBindTrail()
        {
            if (_view == null) return;
            if (_view.trailSpawner == null) { Debug.LogWarning("HUD: no TrailSpawner"); return; }
            if (_view.trailDisplayContainer == null) { Debug.LogWarning("HUD: no TrailDisplayContainer"); return; }
            if (BlockPrefab == null) return; // still no real prefab -> bail

            _trailPool = new TrailPool(
                _view.trailDisplayContainer,
                BlockPrefab,
                _view.trailSpawner,
                _view.worldToUIScale,
                _view.imageScale,
                _view.swingBlocks,
                trailSmoothTime
            );

            _view.trailSpawner.OnBlockCreated += OnTrailBlockCreated;
        }

        private void RebuildTrailPool()
        {
            float lastTarget = _trailPool != null ? _trailPool.TargetDriftAngle : RestAngleDeg;

            _trailPool?.Dispose();
            _trailPool = new TrailPool(
                _view.trailDisplayContainer,
                BlockPrefab,
                _view.trailSpawner,
                _view.worldToUIScale,
                _view.imageScale,
                _view.swingBlocks,
                trailSmoothTime
            );

            _trailPool.SetTargetDriftAngle(lastTarget);
        }

        private void OnTrailBlockCreated(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (_status is { AutoPilotEnabled: true }) return;
            if (_trailPool == null) return;

            var uiScale = _trailPool.WorldToUi;
            if (_trailPool.SwingBlocks)
            {
                _trailPool.EnsurePool();
                _trailPool.UpdateHead(
                    xShift:     xShift * (scaleY / 2f) * uiScale,
                    wavelength: wavelength * uiScale,
                    scaleX:     scaleX * scaleY * _trailPool.ImageScale,
                    scaleZ:     scaleZ * _trailPool.ImageScale,
                    driftDot:   null // binary; not used
                );
            }
            else
            {
                _trailPool.EnsurePool(scaleY);
                _trailPool.UpdateHead(
                    xShift:     xShift * uiScale * scaleY,
                    wavelength: wavelength * uiScale * scaleY,
                    scaleX:     scaleX * scaleY * _trailPool.ImageScale,
                    scaleZ:     scaleZ * scaleY * _trailPool.ImageScale,
                    driftDot:   null
                );
            }
        }

        #endregion
    }
}

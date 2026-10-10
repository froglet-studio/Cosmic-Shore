using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's PATHFINDER — its Time ability, passive (<c>R_VesselActions/STOAT_DIPOLE.md</c>): the
    /// hull's own flight run forward on this frame's inputs (<see cref="StoatDipoleMath.PredictPath"/>,
    /// the studio's <c>fieldPath</c>) through its dipole's field, drawn for the pilot as evenly spaced dots
    /// from just ahead of the nose. While the poles WARP that path at all — it bends at least
    /// <see cref="StoatDipoleConfigSO.WarpDegrees"/>, or it goes through the wormhole — it turns lime and
    /// the hull flies down it faster: a warp of the hull's own FLIGHT CLOCK
    /// (<see cref="VesselTransformer.FlightTimeScale"/>), so the path stays the path and only arrives
    /// sooner. Time is rate: the boost is <see cref="StoatDipoleConfigSO.Boost"/>, read live.
    ///
    /// <para>The prediction and the boost run on the machine that flies the hull (the boost moves it);
    /// the dots are drawn only for a local human pilot. They are drawn on a screen-space overlay, AFTER
    /// the black-hole lens (<see cref="StoatPathfinderDots"/>): a prediction is not light, so the lens
    /// must not bend or double it (the studio's round-15 finding).</para>
    ///
    /// <para><b>One writer.</b> This is the only writer of <see cref="VesselTransformer.FlightTimeScale"/>
    /// on the Stoat; it hands it back at 1 whenever it stops (disable, no authority).</para>
    /// </summary>
    public sealed class StoatPathfinderExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The pathfinder's numbers live with the dipole's (one Stoat dipole config). A passive ability's config is wired on its executor.")]
        [SerializeField] StoatDipoleConfigSO config;

        const int MaxPoints = 1024;
        const int MaxJumps = 8;

        IVesselStatus _status;
        StoatDipoleExecutor _dipole;
        readonly Vector3[] _points = new Vector3[MaxPoints];
        readonly int[] _jumps = new int[MaxJumps];
        StoatDipoleMath.PathResult _path;
        float _boost = 1f;
        bool _wasWarped;
        StoatPathfinderDots _dots;
        StoatPathfinderWorldDots _worldDots;

        /// <summary>The boost multiplier on the hull's flight clock this frame (1 = none).</summary>
        public float BoostMultiplier => _boost;
        /// <summary>True while the poles warp the predicted path (the line is lime and the boost is on).</summary>
        public bool IsWarped => _path.Warped;
        /// <summary>The last prediction's verdict and length.</summary>
        public StoatDipoleMath.PathResult LastPath => _path;
        /// <summary>The last prediction's points (the first <see cref="StoatDipoleMath.PathResult.Count"/> are live).</summary>
        public Vector3[] Points => _points;

        public override void Initialize(IVesselStatus shipStatus)
        {
            ReleaseClock();
            _status = shipStatus;
            _dipole = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<StoatDipoleExecutor>() : null;
            if (!_dipole) _dipole = GetComponent<StoatDipoleExecutor>();
            _boost = 1f;
            _path = default;
            _wasWarped = false;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatPathfinderExecutor has no StoatDipoleConfig — no path, no boost. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset on the prefab.");
        }

        void Update()
        {
            if (config == null || _status == null) return;
            var transformer = _status.VesselTransformer;
            var hull = _status.Transform;
            if (!transformer || !hull || !MantaStingActionExecutor.IsSimAuthority(_status))
            {
                ReleaseClock();
                HideDots();
                return;
            }

            bool flying = transformer.IsActive && !_status.IsStationary && !_status.IsTranslationRestricted;
            if (flying) Predict(transformer, hull);
            else _path = default;

            float dt = Time.deltaTime;
            float target = Mathf.Max(1f, config.Boost.EvaluateLive(_status));
            _boost = flying ? StoatDipoleMath.StepBoost(_boost, _path.Warped, target, config.BoostRise, config.BoostFadeSeconds, dt) : 1f;
            transformer.FlightTimeScale = _boost;

            if (_path.Warped && !_wasWarped) PlayOneShot(config.WarpEvent);
            _wasWarped = _path.Warped;

            bool pilot = _status.IsLocalUser && !_status.AutoPilotEnabled;
            if (pilot && flying) DrawDots();
            else HideDots();
        }

        void Predict(VesselTransformer transformer, Transform hull)
        {
            StoatDipoleMath.Field field = default;
            bool hasField = _dipole && _dipole.TryGetField(out field);
            float clock = Mathf.Max(1e-3f, transformer.FlightTimeScale);
            float engine = Mathf.Max(0f, _status.Speed) / clock;
            var start = new StoatDipoleMath.Body
            {
                Position = hull.position, Rotation = hull.rotation, GravitySpeed = _dipole ? _dipole.GravitySpeed : 0f,
            };
            _path = StoatDipoleMath.PredictPath(start, engine, SteeringRate(transformer), field, hasField,
                config.Flight(transformer.CruiseSpeed), config.Path(), _points, _jumps);
        }

        /// <summary>
        /// The steering the pilot is holding, rad/s in the hull's own axes — the rates
        /// <c>VesselTransformer.Pitch/Yaw/Roll</c> command from the same sticks (pitch about the hull's right,
        /// yaw about its up, roll about its forward), so the line bends the way the hull is about to.
        /// </summary>
        Vector3 SteeringRate(VesselTransformer transformer)
        {
            var input = _status.InputStatus;
            if (input == null) return Vector3.zero;
            float k = Mathf.Deg2Rad * Mathf.Max(0f, transformer.ExternalTurnRateMultiplier);
            return new Vector3(input.YSum * transformer.PitchScaler, input.XSum * transformer.YawScaler,
                transformer.BankIntoTurnSuppressed ? 0f : input.YDiff * transformer.RollScaler) * k;
        }

        void DrawDots()
        {
            var cam = ResolveCamera();
            if (!cam) { HideDots(); return; }
            if (config.DotsInWorld)
            {
                if (_dots) _dots.Hide();
                _worldDots ??= new StoatPathfinderWorldDots();
                _worldDots.Draw(cam, _points, _path.Count, _jumps, _path.JumpCount,
                    _path.Warped ? config.WarpedColor : config.OpenColor,
                    config.WorldDotSize, config.WorldDotSpacing, config.WorldDotMinPixels);
                return;
            }
            if (!_dots) _dots = StoatPathfinderDots.Create(name);
            if (!_dots) return;
            var color = _path.Warped ? config.WarpedColor : config.OpenColor;
            _dots.Show(cam, _points, _path.Count, _jumps, _path.JumpCount, _path.Warped ? _path.LoopIndex : -1, color,
                config.DotPixels, config.DotGap, _path.Warped);
        }

        void HideDots()
        {
            if (_dots) _dots.Hide();
        }

        static Camera ResolveCamera()
        {
            var manager = CameraManager.Instance;
            if (manager && manager.GetActiveController() is CustomCameraController controller && controller.Camera)
                return controller.Camera;
            return BlackHoleLens.ViewCamera();
        }

        void ReleaseClock()
        {
            _boost = 1f;
            var transformer = _status?.VesselTransformer;
            if (transformer) transformer.FlightTimeScale = 1f;
        }

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        void OnDisable()
        {
            ReleaseClock();
            _path = default;
            _wasWarped = false;
            HideDots();
        }

        void OnDestroy()
        {
            if (_dots) Destroy(_dots.transform.root.gameObject);
            _worldDots?.Dispose();
            _worldDots = null;
        }
    }
}

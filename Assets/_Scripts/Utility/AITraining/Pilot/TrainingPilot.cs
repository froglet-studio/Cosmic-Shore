using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Spoofs joystick + button input on a real vessel. Replaces the existing
    /// AIPilot for the duration of a training episode by reading sensors,
    /// running every enabled policy in the loaded genome, blending their
    /// outputs, optionally dithering for difficulty, and writing the result to
    /// the vessel's IInputStatus.
    ///
    /// HARD CONSTRAINT (do not relax): this component is allowed to write to
    /// InputStatus and to call vessel.PerformShipControllerActions /
    /// StopShipControllerActions. It is NOT allowed to read or write the
    /// vessel transform, set physics velocities, teleport the vessel, or alter
    /// any non-input state. The whole point of training is to learn an input
    /// policy that is identical to what a human pilot can produce — anything
    /// else won't transfer.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingPilot : MonoBehaviour
    {
        // ── Configuration ──────────────────────────────
        [Header("Genome")]
        [SerializeField] TrainingGenome _genome;
        public int Intensity = 4;       // 4 = flawless, 1-3 = dithered

        [Header("Sensors")]
        [SerializeField] CellRuntimeDataSO cellData;
        [SerializeField] GameDataSO gameData;
        [SerializeField] LayerMask prismLayerMask = ~0;
        [SerializeField] float prismScanRange = 120f;
        [SerializeField] float threatScanRange = 200f;
        public TargetSensor.TargetMode TargetMode = TargetSensor.TargetMode.ClosestCrystal;

        // ── Runtime ───────────────────────────────────
        IVessel _vessel;
        IVesselStatus _status;
        IInputStatus _input;
        AIPilot _disabledAIPilot;

        readonly DecisionContext _ctx = new();
        readonly List<ITrainingSensor> _sensors = new();
        readonly List<IDecisionPolicy> _activePolicies = new();
        readonly IntensityDitherer _ditherer = new();

        TargetSensor _targetSensor;
        PrismSensor _prismSensor;
        ThreatSensor _threatSensor;

        bool _episodeActive;
        float _episodeStartTime;
        int _episodeFrame;
        int _populationIndexForReporting = -1;

        public TrainingGenome Genome => _genome;
        public bool EpisodeActive => _episodeActive;
        public int PopulationIndex { get => _populationIndexForReporting; set => _populationIndexForReporting = value; }

        /// <summary>
        /// Returns the most recent DecisionContext. Null if BeginEpisode hasn't been
        /// called yet. Fitness components and the runner read this between Update ticks.
        /// </summary>
        public DecisionContext GetCurrentContextOrNull() => _episodeActive ? _ctx : null;
        public DecisionContext GetCurrentContext() => _ctx;

        public delegate void EpisodeFrameCallback(DecisionContext ctx);
        public event EpisodeFrameCallback OnEpisodeFrame;

        // ── Wiring ────────────────────────────────────
        public void BindVessel(IVessel vessel, GameDataSO gameDataRef, CellRuntimeDataSO cellDataRef)
        {
            _vessel = vessel;
            gameData = gameDataRef;
            cellData = cellDataRef;
            _status = vessel?.VesselStatus;
            _input = _status?.InputStatus;

            // Disable AIPilot so its joystick writes don't fight ours.
            // Reach the GameObject through ITransform.Transform — this avoids
            // assuming the IVessel implementation is itself a MonoBehaviour.
            var vesselGo = vessel?.Transform != null ? vessel.Transform.gameObject : null;
            _disabledAIPilot = vesselGo != null ? vesselGo.GetComponentInChildren<AIPilot>() : null;
            if (_disabledAIPilot != null && _disabledAIPilot.AutoPilotEnabled)
                _disabledAIPilot.StopAIPilot();

            BuildSensors();
            BuildPolicies();
        }

        public void LoadGenome(TrainingGenome genome)
        {
            _genome = genome ?? TrainingGenome.FromRegistryDefaults();
            BuildPolicies();
        }

        void BuildSensors()
        {
            _sensors.Clear();

            _targetSensor = new TargetSensor(cellData, gameData) { Mode = TargetMode };
            _targetSensor.Bind(_vessel);
            _sensors.Add(_targetSensor);

            _prismSensor = new PrismSensor
            {
                MaxRange = prismScanRange,
                PrismLayerMask = prismLayerMask
            };
            _prismSensor.Bind(_vessel);
            _sensors.Add(_prismSensor);

            _threatSensor = new ThreatSensor(gameData) { MaxRange = threatScanRange };
            _threatSensor.Bind(_vessel);
            _sensors.Add(_threatSensor);
        }

        void BuildPolicies()
        {
            PolicyBootstrap.EnsureInitialized();
            _activePolicies.Clear();
            if (_genome == null) return;

            foreach (var p in PolicyBootstrap.RegisteredPolicies)
                if (_genome.IsModuleEnabled(p.ModuleName))
                    _activePolicies.Add(p);
        }

        // ── Episode lifecycle ─────────────────────────
        public void BeginEpisode(int populationIndex = -1)
        {
            _populationIndexForReporting = populationIndex;
            if (_genome == null) _genome = TrainingGenome.FromRegistryDefaults();

            BuildPolicies();
            foreach (var p in _activePolicies) p.OnEpisodeStart(_genome);
            foreach (var s in _sensors) s.OnEpisodeStart();

            _ditherer.Reset();
            _ctx.Genome = _genome;

            _episodeActive = true;
            _episodeStartTime = Time.time;
            _episodeFrame = 0;
        }

        public void EndEpisode()
        {
            if (!_episodeActive) return;
            _episodeActive = false;
            foreach (var p in _activePolicies) p.OnEpisodeEnd();

            // Hand control back to whoever owned input before — important so the vessel
            // doesn't keep spoofed input values after the episode ends.
            if (_input != null)
            {
                _input.XSum = 0;
                _input.YSum = 0;
                _input.XDiff = 0;
                _input.YDiff = 0;
                _input.EasedLeftJoystickPosition = Vector2.zero;
            }
        }

        // ── Per-frame ─────────────────────────────────
        void Update()
        {
            if (!_episodeActive) return;
            if (_vessel == null || _status == null || _input == null) return;
            if (_status.IsStationary || _input.Paused) return;

            // 1) Build the per-frame context.
            BuildContext();

            // 2) Sample the world.
            for (int i = 0; i < _sensors.Count; i++) _sensors[i].Sample(_ctx);

            // 3) Run policies and blend their outputs.
            DecisionOutput blended = BlendPolicyOutputs();

            // 4) Apply intensity dithering.
            blended = _ditherer.Apply(Intensity, blended, Time.time);

            // 5) Write to input.
            ApplyToInputStatus(blended);

            // 6) Fire ability requests.
            ApplyActionRequests(blended);

            OnEpisodeFrame?.Invoke(_ctx);

            _episodeFrame++;
        }

        void BuildContext()
        {
            _ctx.Clear();
            _ctx.Vessel = _vessel;
            _ctx.VesselStatus = _status;
            _ctx.MyDomain = _status.Domain;
            _ctx.PlayerName = _status.PlayerName;

            var t = _vessel.Transform;
            _ctx.Position = t.position;
            _ctx.Forward = t.forward;
            _ctx.Up = t.up;
            _ctx.Right = t.right;
            _ctx.Speed = _status.Speed;
            _ctx.Velocity = t.forward * _status.Speed;
            _ctx.IsBoosting = _status.IsBoosting;
            _ctx.IsDrifting = _status.IsDrifting;
            _ctx.IsStationary = _status.IsStationary;
            _ctx.IsAttached = _status.IsAttached;
            _ctx.GunsActive = _status.GunsActive;
            _ctx.HasLiveProjectiles = _status.HasLiveProjectiles;
            _ctx.ChargedBoostCharge = _status.ChargedBoostCharge;
            _ctx.IsChargedBoostDischarging = _status.IsChargedBoostDischarging;

            _ctx.EpisodeTime = Time.time - _episodeStartTime;
            _ctx.EpisodeFrame = _episodeFrame;
        }

        DecisionOutput BlendPolicyOutputs()
        {
            float steerWSum = 0f, throttleWSum = 0f, rollWSum = 0f;
            Vector2 steer = Vector2.zero;
            float throttle = 0f, roll = 0f;
            bool drift = false, ram = false, fire = false;
            List<InputEvents> startActions = null;
            List<InputEvents> stopActions = null;

            for (int i = 0; i < _activePolicies.Count; i++)
            {
                var p = _activePolicies[i];
                var o = p.Decide(_ctx);

                if (o.SteerWeight > 0f)
                {
                    steer += o.SteerLocal * o.SteerWeight;
                    steerWSum += o.SteerWeight;
                }
                if (o.ThrottleWeight > 0f)
                {
                    throttle += o.Throttle * o.ThrottleWeight;
                    throttleWSum += o.ThrottleWeight;
                }
                if (o.RollWeight > 0f)
                {
                    roll += o.Roll * o.RollWeight;
                    rollWSum += o.RollWeight;
                }

                drift |= o.RequestDrift;
                ram |= o.RequestRam;
                fire |= o.RequestFire;

                if (o.RequestActionsStart != null)
                {
                    startActions ??= new List<InputEvents>(8);
                    startActions.AddRange(o.RequestActionsStart);
                }
                if (o.RequestActionsStop != null)
                {
                    stopActions ??= new List<InputEvents>(8);
                    stopActions.AddRange(o.RequestActionsStop);
                }
            }

            if (steerWSum > 0f) steer /= steerWSum;
            if (throttleWSum > 0f) throttle /= throttleWSum;
            if (rollWSum > 0f) roll /= rollWSum;

            return new DecisionOutput
            {
                SteerLocal = new Vector2(Mathf.Clamp(steer.x, -1f, 1f), Mathf.Clamp(steer.y, -1f, 1f)),
                SteerWeight = steerWSum,
                Throttle = Mathf.Clamp01(throttle),
                ThrottleWeight = throttleWSum,
                Roll = Mathf.Clamp(roll, -1f, 1f),
                RollWeight = rollWSum,
                RequestDrift = drift,
                RequestRam = ram,
                RequestFire = fire,
                RequestActionsStart = startActions,
                RequestActionsStop = stopActions
            };
        }

        void ApplyToInputStatus(DecisionOutput d)
        {
            // IsSingleStickControls lives on the vessel status, not the input status.
            // Single-stick vessels expect a normalized 2D joystick; dual-stick vessels
            // expect symmetric XSum/YSum + asymmetric XDiff/YDiff like the existing AIPilot writes.
            if (_status.IsSingleStickControls)
            {
                _input.EasedLeftJoystickPosition = new Vector2(d.SteerLocal.x, -d.SteerLocal.y);
            }
            else
            {
                _input.XSum = d.SteerLocal.x;
                _input.YSum = d.SteerLocal.y;
                _input.YDiff = d.SteerLocal.x;
                _input.XDiff = d.RequestRam ? 1f : d.Throttle;
            }
        }

        readonly HashSet<InputEvents> _heldThisFrame = new();

        void ApplyActionRequests(DecisionOutput d)
        {
            if (d.RequestActionsStart != null)
            {
                for (int i = 0; i < d.RequestActionsStart.Count; i++)
                {
                    var ev = d.RequestActionsStart[i];
                    if (_heldThisFrame.Add(ev))
                        _vessel.PerformShipControllerActions(ev);
                }
            }
            if (d.RequestActionsStop != null)
            {
                for (int i = 0; i < d.RequestActionsStop.Count; i++)
                {
                    var ev = d.RequestActionsStop[i];
                    if (_heldThisFrame.Remove(ev))
                        _vessel.StopShipControllerActions(ev);
                }
            }
        }

        void OnDestroy()
        {
            // Release any actions we may still be holding so the next pilot starts clean.
            foreach (var ev in _heldThisFrame)
            {
                try { _vessel?.StopShipControllerActions(ev); }
                catch { /* vessel may already be destroyed; release-on-destroy is best-effort */ }
            }
            _heldThisFrame.Clear();
        }
    }
}

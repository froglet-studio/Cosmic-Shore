using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the Stoat's three MODES (tuning: <see cref="StoatModeActionSO"/>, design: <c>R_VesselActions/STOAT_DIPOLE.md</c>
    /// §1a, §1f). <b>Mass mode</b> (the default): the triggers lay the field dipole (<see cref="StoatDipoleExecutor"/>) and
    /// the trail is laid THICK. <b>Time mode</b>: the triggers fly the orbit slingshot (<see cref="StoatSlingExecutor"/>)
    /// and the trail is laid at its own thickness. <b>Hybrid mode</b>: Time mode's orbit with a short pathfinder line and
    /// the pathfinder's boost carried out of a well-timed release (<see cref="StoatPathfinderExecutor"/>). B steps
    /// Mass → Time → Hybrid → Mass.
    ///
    /// <para><b>The mode is simulated on EVERY peer</b> (the Butterfly's <see cref="SpreadWingsActionExecutor"/>
    /// shape): the press round-trips through the server like every ability press, so every machine flips together,
    /// and it has to — the thick trail is conserved mass every peer lays, and the trigger presses that follow are
    /// routed by it on every peer. A re-init starts every copy in Mass mode, so no copy starts out of step.</para>
    ///
    /// <para><b>A switch lets go of whatever the triggers were holding</b> — the dipole annihilates where it stands,
    /// an orbit is let go without its kick — so a pair is never stranded under a trigger that now means something
    /// else. A trigger still held after the switch does nothing until it is pressed again.</para>
    ///
    /// <para><b>One writer per frame.</b> The thickness is pushed onto
    /// <see cref="VesselPrismController.ThicknessMultiplier"/> every frame from one eased float, and the Mass-5 shield
    /// onto <see cref="VesselPrismController.ForceShielded"/>; both are handed back at 1 / false on disable.</para>
    /// </summary>
    public sealed class StoatModeExecutor : ShipActionExecutorBase
    {
        public enum Mode { Mass = 0, Time = 1, Hybrid = 2 }

        const int ModeCount = 3;

        /// <summary>The mode one B press steps to: Mass → Time → Hybrid → Mass.</summary>
        public static Mode Next(Mode mode) => (Mode)(((int)mode + 1) % ModeCount);

        /// <summary>How many B presses step <paramref name="from"/> to <paramref name="to"/> (0 when they match).</summary>
        public static int PressesTo(Mode from, Mode to) => (((int)to - (int)from) % ModeCount + ModeCount) % ModeCount;

        /// <summary>True for the modes whose triggers fly the orbit slingshot (Time and Hybrid).</summary>
        public static bool FliesOrbit(Mode mode) => mode is Mode.Time or Mode.Hybrid;

        /// <summary>Where the HUD's Mass card sits in each mode: full in Mass, half in Hybrid, empty in Time.</summary>
        public static float MassCardTarget(Mode mode) => mode switch { Mode.Mass => 1f, Mode.Hybrid => 0.5f, _ => 0f };

        [Header("Config")]
        [Tooltip("The mode switch's numbers. Wired directly (vessel rule 20) so a missing wire shows in the inspector; " +
                 "the B binding's asset is the same one.")]
        [SerializeField] StoatModeActionSO config;

        // The autopilot's numbers: the mode's own copy when it has one (AIModeTuning, e.g. StoatModeAction_Regatta learned
        // from people's editor flights), resolved once the pilot's mode is known; this config otherwise. Only the AI's
        // reads go through it - a person flying this hull reads `config`.
        StoatModeActionSO _aiConfig;
        StoatModeActionSO AIConfig
        {
            get
            {
                if (!_aiConfig) _aiConfig = AIModeTuning.ForPilot(config, _status);
                return _aiConfig ? _aiConfig : config;
            }
        }

        IVesselStatus _status;
        Mode _mode;
        float _thickness = 1f;
        float _massBlend = 1f;
        InputEvents? _boundInput;
        readonly List<ShipActionSO> _bindScratch = new();
        float _aiLastSwitch = float.NegativeInfinity;

        /// <summary>The mode the triggers are in on this machine.</summary>
        public Mode Current => _mode;
        public bool IsTimeMode => _mode == Mode.Time;
        public bool IsMassMode => _mode == Mode.Mass;
        public bool IsHybridMode => _mode == Mode.Hybrid;
        /// <summary>True while the triggers fly the orbit slingshot (Time or Hybrid mode).</summary>
        public bool UsesOrbit => FliesOrbit(_mode);
        /// <summary>Eased 1 in Mass mode, 0.5 in Hybrid, 0 in Time mode — the HUD's Mass card (a switch moving, never a meter).</summary>
        public float MassMode01 => _massBlend;
        public StoatModeActionSO Config => config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _aiConfig = null;
            _boundInput = null;
            _mode = Mode.Mass;
            _aiLastSwitch = float.NegativeInfinity;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatModeExecutor has no StoatModeAction — B switches nothing and the trail " +
                                 "stays thin. Wire Assets/_SO_Assets/VesselActions/Stoat/StoatModeAction.asset on the prefab.");
        }

        /// <summary>B: step to the next mode (Mass → Time → Hybrid → Mass). Runs on every peer (the press is replicated).</summary>
        public void Toggle() => SetMode(Next(_mode), cue: true);

        void SetMode(Mode mode, bool cue)
        {
            if (mode == _mode) return;
            ReleaseTriggers();
            _mode = mode;
            if (cue && config)
                PlayOneShot(mode switch { Mode.Mass => config.MassModeEvent, Mode.Time => config.TimeModeEvent, _ => config.HybridModeEvent });
        }

        /// <summary>Whatever the triggers hold under the old mode is let go: a pair is never stranded.</summary>
        void ReleaseTriggers()
        {
            if (!TryGetComponent<ActionExecutorRegistry>(out var registry)) return;
            var dipole = registry.Get<StoatDipoleExecutor>();
            if (dipole) dipole.ReleaseAll();
            var sling = registry.Get<StoatSlingExecutor>();
            if (sling) sling.ReleaseAll();
        }

        void Update()
        {
            if (!config || _status == null) return;
            var prisms = _status.VesselPrismController;
            if (!prisms) return;

            float dt = Time.deltaTime;
            float target = _mode == Mode.Mass ? config.MassModeThickness(_status) : 1f;
            // Timed per FULL swing between the authored height and the current target, so a Mass-10 trail thickens in
            // the same time as a Mass-0 one.
            float span = Mathf.Max(1f, Mathf.Abs(target - 1f));
            _thickness = Mathf.MoveTowards(_thickness, target, span * dt / config.BlendSeconds);
            _massBlend = Mathf.MoveTowards(_massBlend, MassCardTarget(_mode), dt / config.BlendSeconds);
            prisms.ThicknessMultiplier = _thickness;
            prisms.ForceShielded = _mode == Mode.Mass && config.ShieldsInMassMode(_status);

            if (_status.AutoPilotEnabled && MantaStingActionExecutor.IsSimAuthority(_status)) Autopilot();
        }

        /// <summary>
        /// The switch an autopilot cannot press (never assume an AI can use a human's input): Time mode — the orbit, a
        /// tight turn — when its target sits far off the nose, Mass mode — the dipole, a boosted straight — once it is
        /// back near the nose. Through the REPLICATED press, like a human's, and never while a trigger is held. The AI never
        /// chooses Hybrid mode (its orbit numbers were learned in Time mode); a hull left in Hybrid counts as orbiting, and
        /// the way back to Mass is as many presses as the cycle needs, sent in one go so no peer stops in between.
        /// </summary>
        void Autopilot()
        {
            float threshold = AIConfig.AutopilotTimeModeDegrees;
            if (threshold <= 0f || Time.time - _aiLastSwitch < AIConfig.AutopilotModeIntervalSeconds) return;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            var handler = _status.ActionHandler;
            if (!ai || !hull || !handler) return;
            if (TryGetComponent<ActionExecutorRegistry>(out var registry))
            {
                var dipole = registry.Get<StoatDipoleExecutor>();
                var sling = registry.Get<StoatSlingExecutor>();
                if ((dipole && dipole.IsHoldingAny) || (sling && sling.IsHoldingAny)) return;
            }
            var local = hull.InverseTransformPoint(ai.TargetPosition);
            float off = Vector3.Angle(Vector3.forward, local);
            var want = off >= threshold ? (UsesOrbit ? _mode : Mode.Time) : off <= threshold * 0.5f ? Mode.Mass : _mode;
            int presses = PressesTo(_mode, want);
            if (presses == 0 || !ResolveBoundInput(handler, out var ie)) return;
            _aiLastSwitch = Time.time;
            for (int i = 0; i < presses; i++)
            {
                handler.PerformShipControllerActionsReplicated(ie);
                handler.StopShipControllerActionsReplicated(ie);
            }
        }

        /// <summary>Which input B is bound to on THIS vessel — retried until the binding maps have filled (rule 6).</summary>
        bool ResolveBoundInput(R_VesselActionHandler handler, out InputEvents ie)
        {
            if (_boundInput.HasValue) { ie = _boundInput.Value; return true; }
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                foreach (var action in _bindScratch)
                    if (action is StoatModeActionSO)
                    {
                        _boundInput = candidate;
                        _bindScratch.Clear();
                        ie = candidate;
                        return true;
                    }
            }
            _bindScratch.Clear();
            ie = default;
            return false;
        }

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        // Unconditional and idempotent: a despawned or pooled vessel must not keep a thick or armoured trail.
        void OnDisable()
        {
            _mode = Mode.Mass;
            _thickness = 1f;
            _massBlend = 1f;
            _boundInput = null;
            var prisms = _status?.VesselPrismController;
            if (prisms)
            {
                prisms.ThicknessMultiplier = 1f;
                prisms.ForceShielded = false;
            }
        }
    }
}

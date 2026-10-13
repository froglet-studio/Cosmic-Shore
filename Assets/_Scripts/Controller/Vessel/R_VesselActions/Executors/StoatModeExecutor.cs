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
    /// Owns the Stoat's two MODES (tuning: <see cref="StoatModeActionSO"/>, design: <c>R_VesselActions/STOAT_DIPOLE.md</c>
    /// §1a). <b>Mass mode</b> (the default): the triggers lay the field dipole (<see cref="StoatDipoleExecutor"/>) and
    /// the trail is laid THICK. <b>Time mode</b>: the triggers fly the orbit slingshot (<see cref="StoatSlingExecutor"/>)
    /// and the trail is laid at its own thickness. B switches.
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
        public enum Mode { Mass = 0, Time = 1 }

        [Header("Config")]
        [Tooltip("The mode switch's numbers. Wired directly (vessel rule 20) so a missing wire shows in the inspector; " +
                 "the B binding's asset is the same one.")]
        [SerializeField] StoatModeActionSO config;

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
        /// <summary>Eased 1 in Mass mode, 0 in Time mode — the HUD's Mass card (a switch moving, never a meter).</summary>
        public float MassMode01 => _massBlend;
        public StoatModeActionSO Config => config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _boundInput = null;
            _mode = Mode.Mass;
            _aiLastSwitch = float.NegativeInfinity;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatModeExecutor has no StoatModeAction — B switches nothing and the trail " +
                                 "stays thin. Wire Assets/_SO_Assets/VesselActions/Stoat/StoatModeAction.asset on the prefab.");
        }

        /// <summary>B: flip the mode. Runs on every peer (the press is replicated).</summary>
        public void Toggle() => SetMode(_mode == Mode.Mass ? Mode.Time : Mode.Mass, cue: true);

        void SetMode(Mode mode, bool cue)
        {
            if (mode == _mode) return;
            ReleaseTriggers();
            _mode = mode;
            if (cue && config) PlayOneShot(mode == Mode.Mass ? config.MassModeEvent : config.TimeModeEvent);
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
            _massBlend = Mathf.MoveTowards(_massBlend, _mode == Mode.Mass ? 1f : 0f, dt / config.BlendSeconds);
            prisms.ThicknessMultiplier = _thickness;
            prisms.ForceShielded = _mode == Mode.Mass && config.ShieldsInMassMode(_status);

            if (_status.AutoPilotEnabled && MantaStingActionExecutor.IsSimAuthority(_status)) Autopilot();
        }

        /// <summary>
        /// The switch an autopilot cannot press (never assume an AI can use a human's input): Time mode — the orbit, a
        /// tight turn — when its target sits far off the nose, Mass mode — the dipole, a boosted straight — once it is
        /// back near the nose. Through the REPLICATED press, like a human's, and never while a trigger is held.
        /// </summary>
        void Autopilot()
        {
            float threshold = config.AutopilotTimeModeDegrees;
            if (threshold <= 0f || Time.time - _aiLastSwitch < config.AutopilotModeIntervalSeconds) return;
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
            var want = off >= threshold ? Mode.Time : off <= threshold * 0.5f ? Mode.Mass : _mode;
            if (want == _mode || !ResolveBoundInput(handler, out var ie)) return;
            _aiLastSwitch = Time.time;
            handler.PerformShipControllerActionsReplicated(ie);
            handler.StopShipControllerActionsReplicated(ie);
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

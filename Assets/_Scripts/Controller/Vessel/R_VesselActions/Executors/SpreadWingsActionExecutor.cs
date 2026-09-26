using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the Butterfly's two MODES (tuning: <see cref="SpreadWingsActionSO"/>): <b>Mass mode</b>
    /// — wings open, the wake several times its narrow width — and <b>Dust mode</b> — wings shut,
    /// the narrow line, and the dust capsule below the hull switched on. The right trigger
    /// switches between them. Design record: <c>R_VesselActions/BUTTERFLY.md</c> §3.2.
    ///
    /// <para><b>The mode is simulated on EVERY peer.</b> The press round-trips through the server
    /// like every ability press (<c>R_VesselActionHandler</c>), so this executor runs on every
    /// machine, and it has to: the wake is conserved mass every peer lays, and the dust capsule is
    /// a skimmer every peer observes. A mode held on the owner alone would make the same Butterfly
    /// lay a different wake depending on who was watching.</para>
    ///
    /// <para><b>One writer per frame, never an async lerp.</b> The width is pushed onto
    /// <see cref="VesselPrismController.WidthMultiplier"/> every frame from one eased float. The
    /// alternative — <c>SetNormalizedXScale</c> — starts an async lerp per call and is capped at
    /// the controller's authored maximum, so it can neither track a live element level nor reach
    /// 20x.</para>
    ///
    /// <para><b>Every peer lays the same wake.</b> The mode rides the replicated press, the Mass-5
    /// shield the replicated unlock bit, and the WIDTH the replicated integer Mass level
    /// (<see cref="SpreadWingsActionSO.MassModeWidth"/> → <c>ReplicatedLevel</c>) — so this is the
    /// one elemental trail dial in the fleet that does not diverge across peers
    /// (<c>trailVolume</c> still does).</para>
    /// </summary>
    public sealed class SpreadWingsActionExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Wired directly rather than resolved from the binding maps — a missing wire is " +
                 "then visible in the inspector instead of silently falling back to initializers.")]
        [SerializeField] SpreadWingsActionSO config;

        [Tooltip("The dust capsule this mode switches on. Resolved from the vessel's children when " +
                 "unwired, and reported once by name if there is none.")]
        [SerializeField] ButterflyDustField dustField;

        IVesselStatus _status;
        SpreadWingsActionSO _activeSo;
        ButterflyAnimation _animation;

        bool _dustMode;
        float _width = 1f;
        float _massBlend = 1f;
        bool _warnedNoDust;

        /// <summary>True while the dust capsule is live.</summary>
        public bool IsDustMode => _dustMode;

        /// <summary>True while the wake is wide — kept for readers of the pre-mode API.</summary>
        public bool IsSpread => !_dustMode;

        /// <summary>
        /// Eased 1 in Mass mode, 0 in Dust mode — the HUD's Mass card. A BINARY state drawn as a
        /// fill that travels between its two ends, so it reads as a switch that is moving rather
        /// than as a meter somebody is spending.
        /// </summary>
        public float MassMode01 => _massBlend;

        /// <summary>The width multiplier currently being laid, 1 = the narrow line.</summary>
        public float CurrentWidth => _width;

        SpreadWingsActionSO So => _activeSo ? _activeSo : config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            var root = shipStatus?.Vessel != null ? shipStatus.Vessel.Transform : transform.root;
            _animation = root ? root.GetComponentInChildren<ButterflyAnimation>(true) : null;
            if (!dustField && root) dustField = root.GetComponentInChildren<ButterflyDustField>(true);

            // A re-init hands this component to a different pilot; it starts in Mass mode like a
            // fresh spawn, on every peer, so the mode cannot start out of step.
            _dustMode = false;
            ApplyMode();
        }

        // Unconditional and idempotent: a despawned or pooled vessel must not keep a wide wake,
        // an armoured wake, or a live dust capsule it is no longer flying.
        void OnDisable()
        {
            _dustMode = false;
            _width = 1f;
            _massBlend = 1f;
            var prisms = _status?.VesselPrismController;
            if (prisms)
            {
                prisms.WidthMultiplier = 1f;
                prisms.ForceShielded = false;
            }
            if (dustField) dustField.SetActive(false);
        }

        public void Press(SpreadWingsActionSO so, IVesselStatus status)
        {
            if (!so) return;
            _activeSo = so;
            if (_status == null) _status = status;

            _dustMode = so.InputStyle == SpreadWingsActionSO.ModeInputStyle.HoldForDust
                ? true
                : !_dustMode;
            ApplyMode();
        }

        public void Release(SpreadWingsActionSO so, IVesselStatus status)
        {
            if (!so) return;
            if (so.InputStyle != SpreadWingsActionSO.ModeInputStyle.HoldForDust) return;
            _dustMode = false;
            ApplyMode();
        }

        void ApplyMode()
        {
            _animation?.SetSpread(!_dustMode);

            if (!dustField && _status != null && !_warnedNoDust)
            {
                _warnedNoDust = true;
                CSDebug.LogWarning($"[SpreadWingsActionExecutor] '{name}' has no ButterflyDustField " +
                                   "— Dust mode will narrow the wake and do nothing else. Re-run " +
                                   "FrogletTools > Vessels > Butterfly Vessel Setup.", this);
            }
            if (dustField) dustField.SetActive(_dustMode);
        }

        void Update()
        {
            var so = So;
            var prisms = _status?.VesselPrismController;
            if (!so || !prisms) return;

            float dt = Time.deltaTime;
            float target = _dustMode ? 1f : so.MassModeWidth(_status);

            // Timed per FULL swing between the narrow line and the current target, so a wide
            // Mass-15 wake opens in the same time as a Mass-0 one.
            float span = Mathf.Max(1f, Mathf.Abs(target - 1f));
            _width = Mathf.MoveTowards(_width, target, span * dt / so.WidthBlendSeconds);
            _massBlend = Mathf.MoveTowards(_massBlend, _dustMode ? 0f : 1f, dt / so.WidthBlendSeconds);

            prisms.WidthMultiplier = _width;
            prisms.ForceShielded = !_dustMode && so.ShieldsInMassMode(_status);
        }
    }
}

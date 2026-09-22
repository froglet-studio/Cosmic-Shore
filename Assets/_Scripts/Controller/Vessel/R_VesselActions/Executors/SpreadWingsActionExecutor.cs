using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the Butterfly's <b>Spread Wings</b> hold (tuning: <see cref="SpreadWingsActionSO"/>).
    /// Holding opens the wake and drains the wing-energy meter; releasing — or running dry —
    /// closes it.
    ///
    /// <para><b>Every peer opens the wake, only the OWNER spends the meter.</b> The wake is
    /// conserved mass and the prism controller runs on every machine, so the width has to be set
    /// everywhere or the same Butterfly lays different prisms on different peers. Element levels
    /// and resource meters, by contrast, are simulated on the owning machine ONLY and never
    /// replicate — so a non-owner draining its own copy of the meter would close the wings on that
    /// peer alone, which is a vessel that looks different depending on who is watching.</para>
    ///
    /// <para><b>Running dry is a CLOSE, not a refusal.</b> The wings shut, the wake narrows, and
    /// the pilot keeps flying — nothing is interrupted and nothing is taken away. A meter that
    /// blocked the press instead would make the ability feel like a cooldown, which is the one
    /// thing a brush must not feel like.</para>
    /// </summary>
    public sealed class SpreadWingsActionExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Wired directly rather than resolved from the binding maps — a missing wire is " +
                 "then visible in the inspector instead of silently falling back to initializers.")]
        [SerializeField] SpreadWingsActionSO config;

        IVesselStatus _status;
        SpreadWingsActionSO _activeSo;
        ButterflyAnimation _animation;

        bool _held;
        bool _open;

        /// <summary>True while the wake is wide. Read by the HUD's Mass card, and by the animation
        /// so the wings match what the wake is doing.</summary>
        public bool IsSpread => _open;

        /// <summary>Wing energy remaining, 0..1, for the HUD's Mass gauge. Returns 1 when the
        /// spread is free (Mass 5) — the meter genuinely does not apply then, and a gauge that
        /// kept draining would be reporting a cost nobody is paying.</summary>
        public float Energy01
        {
            get
            {
                var so = _activeSo ? _activeSo : config;
                if (so == null || _status?.ResourceSystem == null) return 0f;
                if (so.IsSpreadFree(_status)) return 1f;
                var resources = _status.ResourceSystem.Resources;
                int index = so.ResourceIndex;
                if (resources == null || index >= resources.Count) return 0f;
                var r = resources[index];
                return r.MaxAmount > 0f ? Mathf.Clamp01(r.CurrentAmount / r.MaxAmount) : 0f;
            }
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _animation = shipStatus?.Vessel != null
                ? shipStatus.Vessel.Transform.GetComponentInChildren<ButterflyAnimation>(true)
                : null;

            // A re-init hands this component to a different pilot; whatever the last one was
            // holding is not this one's.
            _held = false;
            _applied = false;     // a re-init must re-push the width onto the new pilot's wake
            SetOpen(false);
        }

        // Unconditional and idempotent: a despawned, pooled or input-paused vessel must not keep
        // a wake open it is no longer paying for.
        void OnDisable()
        {
            _held = false;
            SetOpen(false);
        }

        public void Open(SpreadWingsActionSO so, IVesselStatus status)
        {
            if (!so) return;
            _activeSo = so;
            if (_status == null) _status = status;
            if (_status == null) return;

            _held = true;
            // Opening is never refused — see the class note. A dry meter simply closes again on
            // the next tick, which the pilot reads as the wings sagging rather than as a block.
            SetOpen(true);
        }

        public void Close(SpreadWingsActionSO so, IVesselStatus status)
        {
            _held = false;
            SetOpen(false);
        }

        void Update()
        {
            if (!_open || _status == null) return;

            var so = _activeSo ? _activeSo : config;
            if (so == null) return;

            // Only the owner simulates the meter — element levels and resources never replicate.
            if (!_status.IsNetworkOwner) return;
            if (so.IsSpreadFree(_status)) return;

            var resources = _status.ResourceSystem;
            if (resources == null) return;

            resources.ChangeResourceAmount(so.ResourceIndex, -so.EnergyPerSecond * Time.deltaTime);

            var list = resources.Resources;
            int index = so.ResourceIndex;
            if (list == null || index >= list.Count) return;
            if (list[index].CurrentAmount > 0.0001f) return;

            // Dry: the wings close, but the pilot is still HOLDING. Leaving _held true means they
            // re-open by themselves the moment the meter recovers, which is what a butterfly
            // labouring at the edge of its strength looks like — and it means the pilot is never
            // asked to re-press a button they never let go of.
            SetOpen(false);
        }

        void LateUpdate()
        {
            // Re-open once the meter has recovered, if the trigger is still down.
            if (_open || !_held || _status == null || !_status.IsNetworkOwner) return;
            var so = _activeSo ? _activeSo : config;
            if (so == null) return;
            if (so.IsSpreadFree(_status)) { SetOpen(true); return; }

            var list = _status.ResourceSystem ? _status.ResourceSystem.Resources : null;
            int index = so.ResourceIndex;
            if (list == null || index >= list.Count) return;
            var r = list[index];
            // A hysteresis band, so a meter hovering at zero does not flutter the wings open and
            // shut every frame — the one failure mode that would make this read as broken.
            if (r.MaxAmount > 0f && r.CurrentAmount >= r.MaxAmount * 0.15f) SetOpen(true);
        }

        bool _applied;   // has a width ever been pushed? the first push must happen even at 'shut'

        void SetOpen(bool open)
        {
            var so = _activeSo ? _activeSo : config;
            if (_applied && _open == open) return;
            _applied = true;
            _open = open;

            if (so != null && _status?.VesselPrismController != null)
                _status.VesselPrismController.SetNormalizedXScale(open ? so.OpenWidth01 : so.ShutWidth01);

            _animation?.SetSpread(open);
        }
    }
}

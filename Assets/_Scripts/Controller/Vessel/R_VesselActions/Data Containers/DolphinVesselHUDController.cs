using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the Dolphin's four ability gauges. Element → icon, in the fleet order:
    ///
    ///   Charge → crystal seeding: carry pips + the recharge fill, polled off the deploy executor
    ///   Mass   → drift trail:     the boost banked while drifting
    ///   Space  → cone blast:      a flash plus how many prisms the last cone claimed
    ///   Time   → skim energy:     the jaw gape, which IS the width of the next blast
    ///
    /// Energy is bound BY NAME rather than by index. It used to be an index, and the prefab had it
    /// pointing at Boost — a meter ChargeBoostActionExecutor writes through CurrentAmount directly,
    /// which never raises OnResourceChanged, so the readout could not move even in principle. A
    /// name survives the resource list being reordered; the serialized index stays as the fallback.
    /// </summary>
    public class DolphinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinVesselHUDView view;

        [Header("Resource Binding")]
        [Tooltip("Resource that holds the skim ENERGY the jaws display. Matched by name first so a " +
                 "reordered resource list cannot silently repoint the gauge.")]
        [SerializeField] private string energyResourceName = "Energy";
        [Tooltip("Fallback slot used only when no resource matches the name above.")]
        [SerializeField] private int energyResourceIndex;
        [Tooltip("Resource holding the boost charged while drifting (the Mass icon's gauge).")]
        [SerializeField] private string driftBoostResourceName = "Boost";
        [SerializeField] private int driftBoostResourceIndex = 1;

        ResourceSystem _resources;
        IVesselStatus _status;
        DeployTeamCrystalActionExecutor _crystalExecutor;

        int _energyIndex = -1;
        int _driftBoostIndex = -1;
        Resource _energy;
        Resource _driftBoost;

        // Last energy pushed to the view, so a RISE (the skim) can be told from the crystal spend
        // and the prism-ram halving. Seeded to +inf so the initial seed can never read as a skim.
        float _lastEnergy = float.PositiveInfinity;

        // Last seeding count pushed to the view, so a passive seeding can be told from the first
        // frame after a bind. -1 means "not seeded yet", which never reads as a beat.
        int _lastSeedCount = -1;

        // True only for a local human pilot's Dolphin - the one cockpit that actually gets drawn.
        bool _drawGauges;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as DolphinVesselHUDView;

            _status = vesselStatus;
            _resources = vesselStatus?.ResourceSystem;

            // Cleared up front: a re-init can hand this controller an AI or remote pilot, and a
            // stale true would let OnEnable resume driving a hidden HUD.
            _drawGauges = false;
            _lastEnergy = float.PositiveInfinity; // a re-init's seed is not a skim either
            _lastSeedCount = -1;                  // ...and a re-init's crystal count is not a seeding
            Unbind();

            if (_resources == null || view == null) return;

            // Remote and AI vessels have no cockpit to draw.
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
            {
                view.Hide();
                return;
            }

            _drawGauges = true;
            _energyIndex = ResolveResource(energyResourceName, energyResourceIndex);
            _driftBoostIndex = ResolveResource(driftBoostResourceName, driftBoostResourceIndex);

            // base.Initialize already ran view.Initialize() on this same component - do not run it
            // twice, it would reset the pip row and the jaw gape a second time.

            // A vessel swap re-runs Initialize on a live controller, so detach before attaching or
            // every handler fires twice and OnDisable's single -= only removes one of them.
            Rebind();

            var hull = vesselStatus.Vessel?.Transform
                ? vesselStatus.Vessel.Transform.GetComponentInChildren<RiptideAnimation>(true)
                : null;
            _crystalExecutor = vesselStatus.Vessel?.Transform
                ? vesselStatus.Vessel.Transform.GetComponentInChildren<DeployTeamCrystalActionExecutor>(true)
                : null;

            // One source for the gape: the HULL's authored angle RANGE. The cockpit jaws and the
            // ship's own jaws are showing the same thing - the gape half-angle of the next blast -
            // so they must not drift apart through separately-authored numbers. The minimum is not
            // zero: the blast is a short capsule even at rest.
            if (hull) view.SetJawAngleRange(hull.MinJawAngleDegrees, hull.MaxJawAngleDegrees);

            SeedFromResources();
        }

        void Rebind()
        {
            Unbind();
            _energy = Bind(_energyIndex, HandleEnergyChanged);
            _driftBoost = Bind(_driftBoostIndex, HandleDriftBoostChanged);
            ExplosionImpactor.OnBlastResolved += HandleBlastResolved;
        }

        void Unbind()
        {
            if (_energy != null) _energy.OnResourceChange -= HandleEnergyChanged;
            if (_driftBoost != null) _driftBoost.OnResourceChange -= HandleDriftBoostChanged;
            _energy = null;
            _driftBoost = null;
            ExplosionImpactor.OnBlastResolved -= HandleBlastResolved;
        }

        // Symmetric with OnDisable, so a disable/enable cycle re-binds instead of silently leaving
        // the gauges dead. Gated on _drawGauges, which Initialize only sets for a LOCAL human
        // pilot - an AI or remote Dolphin must not start driving a hidden HUD on re-enable.
        void OnEnable()
        {
            if (!_drawGauges || _resources == null || view == null) return;
            Rebind();
        }

        // Detach from the resources we actually attached to, not by re-deriving indices - the
        // vessel may already be tearing down.
        void OnDisable() => Unbind();

        Resource Bind(int index, Resource.ResourceUpdateDelegate handler)
        {
            var list = _resources?.Resources;
            if (list == null || (uint)index >= list.Count) return null;

            var resource = list[index];
            resource.OnResourceChange += handler;
            return resource;
        }

        void Update()
        {
            if (!view || _crystalExecutor == null) return;

            // Crystal seeding is PASSIVE: nothing is carried, so the slot shows the cycle's yield
            // (2 once Twin Seed lands) and the recharge fill, which grows 0 -> 1 as the next
            // seeding arms.
            view.SetCrystalSeedState(
                _crystalExecutor.SeedsPerCycle,
                1f - _crystalExecutor.CooldownRemaining01);

            // The pilot gives no input for this ability and may be facing anywhere when it fires,
            // so the planted beat is edge-detected off the executor's own counter and punched onto
            // the slot. Seeded to the live value at bind so the first frame is never a false beat -
            // the same guard _lastEnergy uses for the skim.
            int seeds = _crystalExecutor.SeedCount;
            if (_lastSeedCount >= 0 && seeds > _lastSeedCount)
                view.PulseCrystalSeeded();
            _lastSeedCount = seeds;
        }

        /// <summary>
        /// Index of the resource named <paramref name="name"/>, or <paramref name="fallback"/> when
        /// nothing matches. Returns -1 when even the fallback is out of range, which every reader
        /// below treats as "this gauge is not bound".
        /// </summary>
        int ResolveResource(string name, int fallback)
        {
            var list = _resources?.Resources;
            if (list == null) return -1;

            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < list.Count; i++)
                    if (string.Equals(list[i].Name, name, System.StringComparison.OrdinalIgnoreCase))
                        return i;
            }

            return (uint)fallback < list.Count ? fallback : -1;
        }

        void SeedFromResources()
        {
            var list = _resources?.Resources;
            if (list == null) return;

            if ((uint)_energyIndex < list.Count)
                PushEnergy(list[_energyIndex].CurrentAmount, list[_energyIndex].MaxAmount);
            if ((uint)_driftBoostIndex < list.Count)
                PushDriftBoost(list[_driftBoostIndex].CurrentAmount, list[_driftBoostIndex].MaxAmount);
        }

        void HandleEnergyChanged(float current) => PushEnergy(current, _energy?.MaxAmount ?? 1f);

        void HandleDriftBoostChanged(float current) => PushDriftBoost(current, _driftBoost?.MaxAmount ?? 1f);

        void PushEnergy(float current, float max)
        {
            if (!view) return;

            // Energy only ever RISES from a skim - the crystal blast spends it all and a prism ram
            // halves it - so a rise is the skim event, and the jaws get a beat for it. Without this
            // a single skim is a ~2 degree change in gape, which on a desktop (silent haptics, and
            // no beam at all on prisms that author no ParticleEffect) is the whole feedback budget.
            if (current > _lastEnergy + 1e-4f) view.ReportSkim();
            _lastEnergy = current;

            view.SetEnergyNormalized(max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }

        // The ring reads the meter and nothing else. It used to also take an "am I drifting" flag
        // that drove a scale swell, but that arrived on every tick of a 1 Hz passive regen plus
        // every charge/discharge tick, so the icon stuttered between discrete sizes.
        void PushDriftBoost(float current, float max)
        {
            if (!view) return;
            view.SetDriftBoost(max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }

        // The blast tally is presentation only: a global channel filtered down to our own vessel,
        // because explosions are per-shot objects with nothing durable to subscribe to.
        void HandleBlastResolved(IVessel vessel, int prismsClaimed)
        {
            if (!view || vessel == null || _status?.Vessel == null) return;
            if (!ReferenceEquals(vessel, _status.Vessel)) return;
            view.ReportBlast(prismsClaimed);
        }
    }
}

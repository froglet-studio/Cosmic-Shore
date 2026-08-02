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

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as DolphinVesselHUDView;

            _status = vesselStatus;
            _resources = vesselStatus?.ResourceSystem;
            if (_resources == null || view == null) return;

            // Remote and AI vessels have no cockpit to draw.
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
            {
                view.Hide();
                return;
            }

            _energyIndex = ResolveResource(energyResourceName, energyResourceIndex);
            _driftBoostIndex = ResolveResource(driftBoostResourceName, driftBoostResourceIndex);

            view.Initialize();

            // Bind the PER-RESOURCE event, not the system-level OnResourceChanged. Both exist and
            // they are not equivalent: ChargeBoostActionExecutor fills the boost meter by assigning
            // Resource.CurrentAmount directly, which raises only the per-resource event — a gauge
            // hung off the index-based one would sit frozen through every drift.
            _energy = Bind(_energyIndex, HandleEnergyChanged);
            _driftBoost = Bind(_driftBoostIndex, HandleDriftBoostChanged);

            ExplosionImpactor.OnBlastResolved += HandleBlastResolved;

            _crystalExecutor = vesselStatus.Vessel?.Transform
                ? vesselStatus.Vessel.Transform.GetComponentInChildren<DeployTeamCrystalActionExecutor>(true)
                : null;

            SeedFromResources();
        }

        void OnDisable()
        {
            // Detach from the resources we actually attached to, not by re-deriving indices - the
            // vessel may already be tearing down.
            if (_energy != null) _energy.OnResourceChange -= HandleEnergyChanged;
            if (_driftBoost != null) _driftBoost.OnResourceChange -= HandleDriftBoostChanged;
            _energy = null;
            _driftBoost = null;

            ExplosionImpactor.OnBlastResolved -= HandleBlastResolved;
        }

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

            // Crystals in hand, the carry limit (2 once Twin Seed lands) and the recharge fill,
            // which grows 0 -> 1 as the next slot arms.
            view.SetCrystalState(
                _crystalExecutor.ChargesAvailable,
                _crystalExecutor.MaxCharges,
                1f - _crystalExecutor.CooldownRemaining01);
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
            view.SetEnergyNormalized(max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }

        void PushDriftBoost(float current, float max)
        {
            if (!view) return;
            float norm = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            // "Charging" is the fill phase - once the meter starts discharging it is spending, not
            // banking, and the icon should settle instead of swelling.
            bool charging = norm > 0f && _status != null && !_status.IsChargedBoostDischarging;
            view.SetDriftBoost(norm, charging);
            if (!charging && norm <= 0f) view.ReleaseDriftBoost();
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

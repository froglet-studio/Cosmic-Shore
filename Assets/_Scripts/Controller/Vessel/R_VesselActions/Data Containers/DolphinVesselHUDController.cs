using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the Dolphin's four ability gauges. Element → icon, in the fleet order:
    ///
    ///   Charge → Echo Sight:      the blast's cross-section profile, whether the sight is held, and
    ///                             what the last blast did to LIVING things (pilots, creatures)
    ///   Mass   → crystal seeding: the recharge fill, and which crystal tier the next cycle plants
    ///   Space  → cone blast:      the jaw gape, and what the last cone claimed
    ///   Time   → charge fill rate: the boost banked while drifting
    ///
    /// Energy is bound BY NAME rather than by index. It used to be an index, and the prefab had it
    /// pointing at Boost — a meter ChargeBoostActionExecutor writes through CurrentAmount directly,
    /// which never raises OnResourceChanged, so the readout could not move even in principle. A
    /// name survives the resource list being reordered; the serialized index stays as the fallback.
    ///
    /// <para>The blast PROFILE is polled rather than event-driven, because it is a function of an
    /// element level and the live energy meter at once and neither has a single channel that fires on
    /// every input to it. The push is guarded inside the view, so a poll that finds nothing changed
    /// rebuilds nothing.</para>
    ///
    /// <para>The Mass slot's TEAM colour is the pilot's domain, resolved live off
    /// <c>GameDataSO.ThemeManagerData.ColorSet</c> — the same path MultiplayerHUD and every other
    /// domain-tinted UI reads, so the slot can never disagree with the rest of the HUD about what
    /// colour a domain is. Live rather than cached because domain is not fixed for the match (the
    /// freestyle domain-changer toy re-picks it) and CLAUDE.md is explicit that domain must not be
    /// snapshotted at component-creation time.</para>
    ///
    /// <para><b>The Charge slot's tally splits what the blast did to LIVING things</b> from what it
    /// did to mass. Pilots debuffed comes from the blast itself
    /// (<see cref="BlastTally.Vessels"/> — the impactor keeps a per-blast ledger of the vessels it
    /// landed on, so a target loitering in a growing cone is counted once). Creatures killed cannot:
    /// a creature dies when its last body prism is destroyed, which the ECOLOGY announces several
    /// steps downstream (<c>CellRuntimeDataSO.OnFaunaKilled</c>, carrying the killer's NAME). So
    /// fauna are counted over the blast's own lifetime — zeroed on
    /// <c>ExplosionImpactor.OnBlastBegan</c>, read on <c>OnBlastResolved</c>. That window is exact in
    /// practice because the blast is the Dolphin's only prism-destroying force; two blasts overlapping
    /// inside the 0.15 s cooldown would share a count, which is acceptable for a tally and is not
    /// acceptable for anything else — do not read these numbers for scoring.</para>
    /// </summary>
    public class DolphinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinVesselHUDView view;

        [Header("Blast readouts")]
        [Tooltip("The crystal-impact blast whose cross-section the Charge slot draws. The SAME asset " +
                 "the vessel actually detonates and the Echo Sight previews - never a HUD-local copy " +
                 "of those numbers.")]
        [SerializeField] private VesselExplosionByCrystalEffectSO blastEffect;

        [Header("Resource Binding")]
        [Tooltip("Resource that holds the skim ENERGY the jaws display. Matched by name first so a " +
                 "reordered resource list cannot silently repoint the gauge.")]
        [SerializeField] private string energyResourceName = "Energy";
        [Tooltip("Fallback slot used only when no resource matches the name above.")]
        [SerializeField] private int energyResourceIndex;
        [Tooltip("Resource holding the boost charged while drifting (the Time icon's gauge).")]
        [SerializeField] private string driftBoostResourceName = "Boost";
        [SerializeField] private int driftBoostResourceIndex = 1;

        // The palette and the player roster. Injected rather than serialized so the slot reads the
        // same ColorSet the rest of the HUD does; vessels DO get GameObjectInjector.InjectRecursive
        // at spawn.
        [Inject] GameDataSO _gameData;

        ResourceSystem _resources;
        IVesselStatus _status;
        DeployTeamCrystalActionExecutor _crystalExecutor;
        EchoSightActionExecutor _sightExecutor;

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

        // The ecology's kill channel, resolved from whichever cell this vessel is flying in. Held so
        // the unsubscribe detaches from the SAME channel it attached to - a cell swap mid-flight
        // would otherwise leave a subscription on the old cell's SO forever.
        CellRuntimeDataSO _faunaKillChannelOwner;

        // Creatures this pilot has killed since the live blast began. See the class doc on why this
        // is a window rather than a number the blast can report itself.
        int _faunaKilledThisBlast;
        bool _blastWindowOpen;

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
            _faunaKilledThisBlast = 0;
            _blastWindowOpen = false;
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
            // twice, it would reset the jaw gape and the crystal tint a second time.

            // A vessel swap re-runs Initialize on a live controller, so detach before attaching or
            // every handler fires twice and OnDisable's single -= only removes one of them.
            Rebind();

            var vesselTransform = vesselStatus.Vessel?.Transform;
            var hull = vesselTransform ? vesselTransform.GetComponentInChildren<RiptideAnimation>(true) : null;

            // Both executors live on THIS vessel, so they are looked up on its own transform - never
            // by type across the scene, which would silently bind another vessel's component.
            _crystalExecutor = vesselTransform
                ? vesselTransform.GetComponentInChildren<DeployTeamCrystalActionExecutor>(true)
                : null;
            _sightExecutor = vesselTransform
                ? vesselTransform.GetComponentInChildren<EchoSightActionExecutor>(true)
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
            ExplosionImpactor.OnBlastBegan += HandleBlastBegan;
            ExplosionImpactor.OnBlastResolved += HandleBlastResolved;
            BindFaunaKillChannel();
        }

        void Unbind()
        {
            if (_energy != null) _energy.OnResourceChange -= HandleEnergyChanged;
            if (_driftBoost != null) _driftBoost.OnResourceChange -= HandleDriftBoostChanged;
            _energy = null;
            _driftBoost = null;
            ExplosionImpactor.OnBlastBegan -= HandleBlastBegan;
            ExplosionImpactor.OnBlastResolved -= HandleBlastResolved;

            // Detach from the channel we actually attached to, never a freshly-resolved one: the
            // vessel may have moved to a different cell, or be tearing down, since Rebind ran.
            if (_faunaKillChannelOwner != null && _faunaKillChannelOwner.OnFaunaKilled != null)
                _faunaKillChannelOwner.OnFaunaKilled.OnRaised -= HandleFaunaKilled;
            _faunaKillChannelOwner = null;
        }

        /// <summary>
        /// Subscribe to the kill channel of whichever cell this vessel is flying in, resolved the same
        /// way the crystal seeding executor resolves its cell — containing cell first, nearest active
        /// cell as the fallback. Resolved rather than serialized so this works in every scene with
        /// nothing per-prefab to wire; a scene with no cell simply has no creatures to count.
        ///
        /// <para><b>It must be RETRIED, not resolved once at init.</b> <c>Cell.Initialize</c> runs on
        /// <c>OnInitializeGame</c> behind <c>InitDelayMs</c> (1000 ms) while vessels spawn at
        /// <c>preSpawnDelayMs</c> (200 ms), so at this controller's Initialize there is usually no
        /// cell to find at all — binding once there would leave the creature tally reading zero for
        /// the entire match, silently. This is the same spawn-chain race CLAUDE.md documents for
        /// anything that reads the cell during spawn, and the crystal seeding executor sidesteps it
        /// by resolving its cell per seeding. Here the retry hangs off
        /// <see cref="HandleBlastBegan"/>: it is the moment the count is needed, it is rare, and by
        /// then the cell has certainly booted.</para>
        /// </summary>
        void EnsureFaunaKillChannel()
        {
            if (_faunaKillChannelOwner != null) return;
            BindFaunaKillChannel();
        }

        void BindFaunaKillChannel()
        {
            var origin = _status?.Vessel != null && _status.Vessel.Transform
                ? _status.Vessel.Transform.position
                : transform.position;

            var cell = Cell.FindCellContaining(origin) ?? Cell.FindNearestActiveCell(origin);
            var runtime = cell != null ? cell.RuntimeData : null;
            if (runtime == null || runtime.OnFaunaKilled == null) return;

            _faunaKillChannelOwner = runtime;
            runtime.OnFaunaKilled.OnRaised += HandleFaunaKilled;
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
            if (!view || !_drawGauges) return;

            PushCrystalSeeding();
            PushBlastProfile();
        }

        // Crystal seeding is PASSIVE: nothing is carried, so the slot shows the recharge fill (0 -> 1
        // as the next seeding arms) and which crystal tier that cycle will plant.
        void PushCrystalSeeding()
        {
            if (_crystalExecutor == null) return;

            view.SetCrystalSeedState(
                1f - _crystalExecutor.CooldownRemaining01,
                _crystalExecutor.SeedsTeamCrystal,
                ResolveDomainSignalColor());

            // The pilot gives no input for this ability and may be facing anywhere when it fires,
            // so the planted beat is edge-detected off the executor's own counter and punched onto
            // the slot. Seeded to the live value at bind so the first frame is never a false beat -
            // the same guard _lastEnergy uses for the skim.
            int seeds = _crystalExecutor.SeedCount;
            if (_lastSeedCount >= 0 && seeds > _lastSeedCount)
                view.PulseCrystalSeeded();
            _lastSeedCount = seeds;
        }

        // The Charge slot draws the blast's cross-section, which is a live function of BOTH the
        // Charge level (thickness) and the energy meter (extent) - so it is polled rather than
        // hung off either one's change event.
        void PushBlastProfile()
        {
            if (!blastEffect || _status == null) return;

            if (blastEffect.TryResolveProfile(_status, out var radius, out var halfLength, out var reference))
                view.SetBlastProfile(radius, halfLength, reference);

            if (_sightExecutor) view.SetSightEngaged(_sightExecutor.IsEngaged);
        }

        /// <summary>
        /// This pilot's domain, at full brightness, for the Mass slot to announce a team-locked seed.
        ///
        /// <para>It reads <c>SO_ColorSet.GetDomainSignalColor</c> — the domain UI colour with its
        /// brightest channel driven to 1, hue and saturation intact. <b>Not a crystal colour:</b>
        /// <c>DullCrystalColor</c> is authored (0,0,0) on Jade, Ruby AND Gold in the shipped palette
        /// (the domain crystals are near-black bodies with a bright fresnel rim, which is right on a
        /// faceted crystal in the world and rendered as a black square in the slot), and
        /// <c>BrightCrystalColor</c> tops out at 0.75 value. An icon has to be legible on its own,
        /// not a literal sample of a shader's base layer.</para>
        /// </summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return Color.white;
            return colorSet.GetDomainSignalColor(_status.Domain);
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
        void HandleBlastBegan(IVessel vessel)
        {
            if (vessel == null || _status?.Vessel == null) return;
            if (!ReferenceEquals(vessel, _status.Vessel)) return;

            // Late-bind the ecology's kill channel if Initialize ran before the cell existed - see
            // EnsureFaunaKillChannel. Cheap: it returns immediately once bound.
            EnsureFaunaKillChannel();

            _faunaKilledThisBlast = 0;
            _blastWindowOpen = true;
        }

        void HandleBlastResolved(IVessel vessel, BlastTally tally)
        {
            if (vessel == null || _status?.Vessel == null) return;
            if (!ReferenceEquals(vessel, _status.Vessel)) return;

            int fauna = _faunaKilledThisBlast;
            _faunaKilledThisBlast = 0;
            _blastWindowOpen = false;

            if (!view) return;

            // Space says what it did to MASS; Charge says what it did to the LIVING.
            view.ReportBlast(tally.Prisms);
            view.ReportEchoTally(tally.Vessels, fauna);
        }

        /// <summary>
        /// A creature died somewhere in the cell. The channel carries the KILLER'S NAME (stamped by
        /// the destroyed body prism), which is the ecology's own attribution and the same string
        /// StatsManager scores off — so filtering on our own display name credits exactly the kills
        /// this pilot caused, with no second bookkeeping path to keep in sync.
        /// </summary>
        void HandleFaunaKilled(string killerName)
        {
            if (!_blastWindowOpen || string.IsNullOrEmpty(killerName)) return;

            // IPlayer.Name is the exact string StatsManager.LifeformKilled compares against, so the
            // tally credits the same kills the scoreboard does. Note Fauna.Die only publishes
            // PLAYER-attributed deaths - starvation and predation are filtered there - so this can
            // never count a creature the food web ate.
            var mine = _status?.Player?.Name;
            if (string.IsNullOrEmpty(mine) || killerName != mine) return;

            _faunaKilledThisBlast++;
        }
    }
}

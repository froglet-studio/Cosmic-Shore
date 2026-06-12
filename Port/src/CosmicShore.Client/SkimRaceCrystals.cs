using System;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Client
{
    /// <summary>How a station's crystal is claimed — implied by its element + domain.</summary>
    public enum StationKind
    {
        /// <summary>Vessel-contact claim, any domain, grants all four elements (OmniCrystalImpactor).</summary>
        Omni = 0,
        /// <summary>Vessel-contact claim, domain-locked (TeamCrystalImpactor) — only the matching domain's pilot may take it.</summary>
        Team = 1,
        /// <summary>Skimmer claim (ElementalCrystalImpactor) — consumed: flies to the claimer and destroys itself.</summary>
        Elemental = 2,
    }

    /// <summary>
    /// The race's crystal manager — a game-mode manager in the REAL
    /// <see cref="CrystalManager"/> family (the LocalCrystalManager / NetworkCrystalManager
    /// pattern: each game mode owns crystal placement while crystals route their lifecycle
    /// through <c>Crystal.Respawn()</c> / <c>Crystal.NotifyManagerToExplodeCrystal()</c>).
    /// Owns the station crystals end to end:
    ///
    ///   • spawn — one crystal per track station, rigged per kind: Omni stations get the
    ///     real OmniCrystalImpactor (vessel contact, any domain), Team stations the real
    ///     TeamCrystalImpactor (vessel contact, domain-locked), elemental stations the
    ///     real ElementalCrystalImpactor (skimmer claim, consumed on collection);
    ///   • respawn — the real chain: the impactor calls <c>Crystal.Respawn()</c> →
    ///     <see cref="RespawnCrystal"/> (this manager). The race's placement rule: the
    ///     claimed station goes dark IN PLACE and relights after
    ///     <see cref="SkimRaceDirector.CrystalRespawnSeconds"/> — the seeded station
    ///     circuit is the track, so the LocalCrystalManager anchor-progression placement
    ///     (next anchor + 35u scatter) would scatter the racing line. Consumed elemental
    ///     crystals (destroyed via the real fly-to-vessel collection) respawn as fresh
    ///     crystals at their station on the same clock;
    ///   • claims — reported from INSIDE the real impactor dispatch (the per-crystal
    ///     OnCrystalCollected SOAP channel for vessel claims; the race's
    ///     <see cref="SkimRaceElementalClaimEffectSO"/> in the crystal's
    ///     elementalCrystalShipEffects for skimmer claims) and re-raised as
    ///     <see cref="OnStationClaimed"/> for the director's StatsManager-shaped
    ///     bookkeeping. Element levels and energy are NOT granted here — they flow
    ///     through the effect SOs wired into the impactors.
    /// </summary>
    public sealed class SkimRaceCrystalManager : CrystalManager
    {
        /// <summary>Seconds an elemental crystal spends flying to its claimer before it destroys itself.</summary>
        public const float ElementalFlightSeconds = 0.9f;

        SkimTrack _track;
        Domains[] _stationDomains;
        StationKind[] _kinds;
        VesselCrystalEffectSO[] _omniCrystalEffects;
        VesselCrystalEffectSO[] _teamCrystalEffects;
        SkimmerCrystalEffectSO[] _elementalCrystalEffects;

        Crystal[] _stationCrystals;
        bool[] _available;
        float[] _respawnAt; // 0 = nothing staged
        int _generation;    // invalidates stale claim channels across restarts

        /// <summary>(station, element, claimer player name) — raised from inside the real impactor dispatch.</summary>
        public event Action<int, Element, string> OnStationClaimed;

        /// <summary>Team stations sit at loop slot 3 of every 7 (the omni slot is 6 — never overlaps).</summary>
        public static bool IsTeamSlot(int station) => station % 7 == 3;

        public int StationCount => _stationCrystals.Length;
        public bool IsStationAvailable(int station) => _available[station];
        public Crystal GetStationCrystal(int station) => _stationCrystals[station];
        public Domains StationDomain(int station) => _stationDomains[station];
        public StationKind KindOf(int station) => _kinds[station];

        public void Configure(SkimTrack track, Domains[] stationDomains,
            VesselCrystalEffectSO[] omniEffects, VesselCrystalEffectSO[] teamEffects,
            SkimmerCrystalEffectSO[] elementalEffects, SkimRaceElementalClaimEffectSO elementalClaimEffect)
        {
            _track = track;
            _stationDomains = stationDomains;
            _omniCrystalEffects = omniEffects;
            _teamCrystalEffects = teamEffects;
            _elementalCrystalEffects = elementalEffects;
            elementalClaimEffect.Claimed = HandleElementalClaim;

            int stations = track.Crystals.Count;
            _stationCrystals = new Crystal[stations];
            _available = new bool[stations];
            _respawnAt = new float[stations];
            _kinds = new StationKind[stations];
            for (int i = 0; i < stations; i++)
                _kinds[i] = track.StationElements[i] == Element.Omni ? StationKind.Omni
                    : stationDomains[i] != Domains.Blue ? StationKind.Team
                    : StationKind.Elemental;
        }

        /// <summary>
        /// Fresh course for a fresh race. The director destroys the previous generation's
        /// crystal GameObjects first (CellRuntimeDataSO.ResetRuntimeData — the active
        /// force); stale in-flight claim handlers are dropped by the generation bump.
        /// </summary>
        public void ResetStations()
        {
            _generation++;
            for (int station = 0; station < _stationCrystals.Length; station++)
            {
                _stationCrystals[station] = null;
                _available[station] = false;
                _respawnAt[station] = 0f;
            }
            for (int station = 0; station < _stationCrystals.Length; station++)
                SpawnStation(station);
        }

        /// <summary>A crystal the renderer should draw: live at its station, or an elemental one mid-flight to its claimer.</summary>
        public bool TryGetDrawableCrystalPosition(int station, out Vector3 position)
        {
            var crystal = _stationCrystals[station];
            if (crystal && (_available[station] || _kinds[station] == StationKind.Elemental))
            {
                position = crystal.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        // ── the CrystalManager family API ────────────────────────────

        /// <summary>
        /// The real respawn chain (OmniCrystalImpactor → Crystal.Respawn → manager): the
        /// claimed station goes dark in place and relights after the race's respawn window.
        /// </summary>
        public override void RespawnCrystal(int crystalId)
        {
            int station = crystalId - 1;
            if (station < 0 || station >= _stationCrystals.Length) return;
            var crystal = _stationCrystals[station];
            if (!crystal || !_available[station]) return;

            var collider = crystal.GetComponent<Collider>();
            if (collider) collider.enabled = false; // dark window: not claimable
            crystal.DeactivateModels();
            _available[station] = false;
            _respawnAt[station] = Time.time + SkimRaceDirector.CrystalRespawnSeconds;
            cellData.OnCellItemsUpdated.Raise(); // the field retargets off the dark station
        }

        /// <summary>LocalCrystalManager shape: route the impactor's explode notification to the crystal.</summary>
        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
        {
            if (!cellData.TryGetCrystalById(crystalId, out var crystal)) return;
            if (crystal != null)
                crystal.Explode(explodeParams);
        }

        // ── frame: relights + fresh spawns on the respawn clock ──────

        void Update()
        {
            for (int station = 0; station < _stationCrystals.Length; station++)
            {
                if (_available[station] || _respawnAt[station] <= 0f || Time.time < _respawnAt[station])
                    continue;

                var crystal = _stationCrystals[station];
                if (crystal)
                {
                    // Vessel-claim crystal survived its claim (the real relocate-on-claim
                    // semantics) — relight in place through the base manager's move API.
                    var collider = crystal.GetComponent<Collider>();
                    if (collider) collider.enabled = true;
                    _available[station] = true;
                    _respawnAt[station] = 0f;
                    UpdateCrystalPos(station + 1, _track.Crystals[station]); // raises OnCellItemsUpdated
                }
                else
                {
                    // Elemental crystal was consumed (flew to the claimer and destroyed
                    // itself — the real collection) — a fresh crystal takes the station.
                    SpawnStation(station);
                }
            }
        }

        // ── claims (from inside the real impactor dispatch) ──────────

        void HandleVesselClaim(int generation, int station, CrystalStats stats)
        {
            if (generation != _generation) return; // stale crystal from a restarted race
            // Availability flips when the impactor calls Crystal.Respawn() right after
            // its effects — RespawnCrystal above stages the dark window.
            OnStationClaimed?.Invoke(station, stats.Element, stats.PlayerName);
        }

        void HandleElementalClaim(SkimmerImpactor impactor, CrystalImpactor impactee)
        {
            var crystal = impactee.Crystal;
            int station = -1;
            for (int i = 0; i < _stationCrystals.Length; i++)
            {
                if (!ReferenceEquals(_stationCrystals[i], crystal)) continue;
                station = i;
                break;
            }
            if (station < 0 || !_available[station]) return; // stale crystal from a restarted race

            _available[station] = false;
            _respawnAt[station] = Time.time + SkimRaceDirector.CrystalRespawnSeconds;
            cellData.OnCellItemsUpdated.Raise(); // retarget now; the crystal flies off and destroys itself
            OnStationClaimed?.Invoke(station, crystal.crystalProperties.Element,
                impactor.Skimmer.VesselStatus.PlayerName);
        }

        // ── station rigs ─────────────────────────────────────────────

        /// <summary>
        /// Station crystal with the real contact rig: trigger SphereCollider + Crystal +
        /// the kind's CrystalImpactor + ImpactCollider routing the engine trigger pass
        /// into the impactor dispatch. Ids are 1-based (CellItem ids must be non-zero so
        /// the manager family's TryGetCrystalById round-trips).
        /// </summary>
        void SpawnStation(int station)
        {
            var element = _track.StationElements[station];
            var domain = _stationDomains[station];

            var go = new GameObject($"crystal-{station}");
            go.transform.SetParent(transform);
            go.transform.position = _track.Crystals[station];

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;

            var crystal = go.AddComponent<Crystal>();
            crystal.Initialize(station + 1);
            crystal.ItemType = ItemType.Buff;
            crystal.crystalProperties = new CrystalProperties
            {
                crystal = crystal,
                Element = element,
                crystalValue = 1f,
            };
            SkimRaceFactory.SetPrivateField(crystal, "cellData", cellData);
            crystal.InjectDependencies(this);

            if (_kinds[station] == StationKind.Elemental)
            {
                // Skimmer claim surface: the near-field skimmer's reach (7u at Mass 0,
                // growing with Mass) plus this radius lands the claim at ~CollectRadius —
                // Mass claims widen your pickup reach, the real elemental hook.
                trigger.radius = SkimRaceDirector.CollectRadius - SkimRaceFactory.SkimmerReachBase;

                var impactor = go.AddComponent<ElementalCrystalImpactor>(); // Awake binds impactor.Crystal
                SkimRaceFactory.SetPrivateField(impactor, "elementalCrystalShipEffects", _elementalCrystalEffects);
                SkimRaceFactory.SetPrivateField(impactor, "moveToVesselDuration", ElementalFlightSeconds);
                WireContactRig(go, impactor);
            }
            else
            {
                // Vessel-contact claim surface (claims at CollectRadius from the vessel center).
                trigger.radius = SkimRaceDirector.CollectRadius - SkimRaceDirector.VesselContactRadius;
                // Survive the claim: Crystal.Respawn() routes into RespawnCrystal above
                // (the real relocate-on-claim semantics) instead of destroying the crystal.
                SkimRaceFactory.SetPrivateField(crystal, "allowRespawnOnImpact", true);

                OmniCrystalImpactor impactor;
                if (_kinds[station] == StationKind.Team)
                {
                    crystal.ChangeDomain(domain); // the real domain entry point — TeamCrystalImpactor gates on it
                    impactor = go.AddComponent<TeamCrystalImpactor>();
                    SkimRaceFactory.SetPrivateField(impactor, "omniCrystalShipEffects", _teamCrystalEffects);
                }
                else
                {
                    impactor = go.AddComponent<OmniCrystalImpactor>();
                    SkimRaceFactory.SetPrivateField(impactor, "omniCrystalShipEffects", _omniCrystalEffects);
                }

                var claimEvent = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
                int generation = _generation;
                int claimStation = station;
                claimEvent.OnRaised += stats => HandleVesselClaim(generation, claimStation, stats);
                SkimRaceFactory.SetPrivateField(impactor, "OnCrystalCollected", claimEvent);
                WireContactRig(go, impactor);
            }

            cellData.AddCrystalToList(crystal); // raises OnCellItemsUpdated → courses re-sort
            _stationCrystals[station] = crystal;
            _available[station] = true;
            _respawnAt[station] = 0f;
        }

        static void WireContactRig(GameObject go, ImpactorBase impactor)
        {
            var impactCollider = go.AddComponent<ImpactCollider>();
            SkimRaceFactory.SetPrivateField(impactCollider, "impactorObject", impactor);
        }
    }

    /// <summary>
    /// Race rule as an effect asset (the per-game effect pattern, like
    /// <see cref="SkimRaceTrailSkimEnergyEffectSO"/>): an Omni claim surges all four
    /// elemental levels and charges the energy bar. Runs INSIDE the real dispatch chain
    /// (OmniCrystalImpactor → VesselImpactor.ExecuteOmniCrystalImpact → these effects).
    /// </summary>
    public sealed class SkimRaceOmniCrystalSurgeEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("Energy added to the bar by an Omni claim.")]
        public float EnergyGain = 0.3f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var resources = vesselImpactor.Vessel.VesselStatus.ResourceSystem;
            resources.IncrementLevel(Element.Charge);
            resources.IncrementLevel(Element.Mass);
            resources.IncrementLevel(Element.Space);
            resources.IncrementLevel(Element.Time);
            resources.ChangeResourceAmount(0, EnergyGain);
        }
    }

    /// <summary>
    /// Race rule as an effect asset: the crystal-claim energy kicker for vessel-contact
    /// claims (team crystals) — Charge crystals charge harder. Element levels are granted
    /// by the REAL <see cref="VesselIncrementLevelByCrystalEffectSO"/> beside this in the
    /// effect list.
    /// </summary>
    public sealed class SkimRaceCrystalEnergyEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("Energy added by a Charge-element claim.")]
        public float ChargeGain = 0.3f;
        [Tooltip("Energy added by any other elemental claim.")]
        public float OtherGain = 0.15f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
            => vesselImpactor.Vessel.VesselStatus.ResourceSystem.ChangeResourceAmount(0,
                data.Element == Element.Charge ? ChargeGain : OtherGain);
    }

    /// <summary>
    /// Race rule as an effect asset, wired into every elemental crystal's
    /// elementalCrystalShipEffects EXACTLY like the original's flora/fauna crystal prefabs
    /// wire SkimmerAdjustElementLevelByCrystalEffect there: the energy kicker for skimmer
    /// claims plus the claim report the race's manager turns into StatsManager-shaped
    /// bookkeeping. Element levels are granted by the REAL
    /// <see cref="SkimmerAdjustElementLevelByCrystalEffectSO"/> beside this in the list.
    /// </summary>
    public sealed class SkimRaceElementalClaimEffectSO : SkimmerCrystalEffectSO
    {
        [Tooltip("Energy added by a Charge-element claim.")]
        public float ChargeGain = 0.3f;
        [Tooltip("Energy added by any other elemental claim.")]
        public float OtherGain = 0.15f;

        /// <summary>Claim report (set by the race factory to the crystal manager's handler).</summary>
        public Action<SkimmerImpactor, CrystalImpactor> Claimed;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor impactee)
        {
            impactor.Skimmer.VesselStatus.ResourceSystem.ChangeResourceAmount(0,
                impactee.Crystal.crystalProperties.Element == Element.Charge ? ChargeGain : OtherGain);
            Claimed?.Invoke(impactor, impactee);
        }
    }
}

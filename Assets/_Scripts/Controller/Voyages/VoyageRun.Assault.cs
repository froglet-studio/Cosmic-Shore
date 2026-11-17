using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A job voyage's hostile cells, the fauna's assaults on the Ark in them, and its breach line (Shombith, 2026-11-16,
    /// Milestone 1: "the cell faunas should attack the ark"; Crystal Run: 4 cells, 3 hostile and 1 calm).
    ///
    /// <para>Which cells of a leg start hostile is the job's (<see cref="VoyageJob.HostileCells"/>, authored per job in
    /// voyage_slice.json; without a pattern only a hostile destination, the old rule: <see cref="VoyageRules.HostileCell"/>).
    /// A hostile cell is held by a domain opposing the squad's, its armour is food, and its fauna are lured onto the Ark:
    /// steadily at the tier's <see cref="ArkEscortSO.Tier.LureShare"/>, and in announced ASSAULTS at its
    /// <see cref="ArkEscortSO.Tier.AssaultLureShare"/> (<see cref="ArkAssaultDriver"/>). The tier is the Ark escort's, at the
    /// job's difficulty: one rule for fauna that attack an Ark, wherever it sails (the escort-only lure carve-out,
    /// Docs/ECOSYSTEM.md §0.z, now covers the voyages' hostile cells too). Below the job's
    /// <see cref="VoyageJob.BreachHull"/> the Ark falls.</para>
    ///
    /// <para>The HUD reads it through <see cref="ArkVoyageSignals"/> or <see cref="Status"/>, as it reads the escort.</para>
    /// </summary>
    public sealed partial class VoyageRun : IArkStatusSource
    {
        const float AssaultTickSeconds = 1f;

        ArkAssaultDriver _assaultDriver;
        readonly Dictionary<Cell, int> _legIndexOf = new();
        readonly HashSet<Cell> _hostileCells = new();
        readonly List<Fauna> _assaultThreats = new();
        Ark _assaultArk;
        Cell _assaultCell;
        int _assaultLeg = -1;
        int _assaultThreatsNear;
        float _nextAssaultTick;
        bool _assaultKillHooked;

        ArkAssaultDriver Assaults => _assaultDriver ??= new ArkAssaultDriver(this, (line, mood) => VoyageToasts.Show(_sea, mood, line));

        ArkEscortSO AttackRules => _def ? _def.ArkEscort : null;

        /// <summary>The job's difficulty, 1-9 (its pilot's recommendation when the crew picked none).</summary>
        int AssaultDifficulty
        {
            get
            {
                var job = _state?.Current;
                if (job == null) return VoyageDifficulty.Min;
                return job.Difficulty > 0 ? VoyageDifficulty.Clamp(job.Difficulty) : VoyageDifficulty.Recommended(job.Intensity);
            }
        }

        ArkEscortSO.Tier AttackTier => AttackRules ? AttackRules.TierFor(AssaultDifficulty) : null;

        /// <summary>The voyage as it stands now, for the HUD's objective box.</summary>
        public ArkEscortStatus Status
        {
            get
            {
                var job = _state?.Current;
                var ark = Ark;
                var cell = CurrentCell;
                var s = new ArkEscortStatus
                {
                    Active = _active,
                    Difficulty = job != null ? AssaultDifficulty : 0,
                    Leg = _assaultLeg,
                    Legs = _profile?.Count ?? 0,
                    WorldName = job != null ? Content?.Location(job.To)?.Name ?? string.Empty : string.Empty,
                    NextWorldName = string.Empty,
                    WorldHostile = cell && _hostileCells.Contains(cell),
                    BreachLine = job?.BreachHull ?? 0f,
                    DistanceToCore = ark && cell ? Vector3.Distance(ark.Position, cell.transform.position) : -1f,
                    ThreatsNear = _active ? _assaultThreatsNear : 0,
                    Outcome = _fallen ? ArkEscortReport.Outcome.Fallen
                        : _docked ? ArkEscortReport.Outcome.Survived : ArkEscortReport.Outcome.Abandoned,
                };
                if (ark)
                {
                    s.InsideWorld = ArkEscortRun.ArkInsideWorld(cell, ark.Position);
                    s.Hull = ark.HealthFraction;
                    s.HullAlive = ark.AliveCount;
                    s.HullTotal = ark.TotalCount;
                    s.Healing = ark.RegrowAllowed;
                }
                Assaults.Fill(ref s);
                return s;
            }
        }

        // ── Hooks the run calls ──────────────────────────────────────────────

        /// <summary>A new leg is about to be stood (from <see cref="PrepareLeg"/>): forget the last leg's cells.</summary>
        void BeginLegAssaults()
        {
            ClearLures();
            _legIndexOf.Clear();
            _hostileCells.Clear();
            _assaultLeg = -1;
            _assaultCell = null;
            _nextRaiderWave = -1f;
            _raidersToasted = false;
            if (!_assaultKillHooked)
            {
                Fauna.Killed += OnAssaultFaunaKilled;
                _assaultKillHooked = true;
            }
            if (!ReferenceEquals(ArkVoyageSignals.Current, this)) Assaults.Begin(AttackRules, AttackTier);
            else Assaults.Retune(AttackRules, AttackTier);
        }

        /// <summary>Is route cell <paramref name="routeIndex"/> of this leg hostile? (From <see cref="OnPrepareCell"/>.)</summary>
        bool PrepareAssaultCell(Cell cell, int routeIndex)
        {
            bool hostile = VoyageRules.HostileCell(_state?.Current, routeIndex, _profile?.Count ?? 0, _hostileWorld);
            _legIndexOf[cell] = routeIndex;
            if (hostile)
            {
                _hostileCells.Add(cell);
                var rules = AttackRules;
                cell.ArmourIsFood = rules && rules.PlatesAreFood;
            }
            else
            {
                _hostileCells.Remove(cell);
                cell.ArmourIsFood = false;
            }
            return hostile;
        }

        /// <summary>The Ark is under way (from <see cref="OnUnderway"/>).</summary>
        void OnAssaultUnderway()
        {
            HookAssaultArk(Ark);
            Assaults.Underway();
        }

        /// <summary>Every frame while sailing (from <see cref="Update"/>): the leg, the lure, the assault, the breach.</summary>
        void TickAssaults()
        {
            var ark = Ark;
            if (!_active || !ark) return;
            if (ark != _assaultArk) HookAssaultArk(ark);

            var cell = CurrentCell;
            if (cell != _assaultCell)
            {
                _assaultCell = cell;
                _assaultLeg = cell && _legIndexOf.TryGetValue(cell, out int i) ? i : -1;
                var bound = _state?.Current;
                Assaults.NewWorld(bound != null ? Content?.Location(bound.To)?.Name : null);
            }

            if (Time.time < _nextAssaultTick) return;
            _nextAssaultTick = Time.time + AssaultTickSeconds;

            _assaultThreatsNear = CountThreats(ark.Position, ArkEscortPilot.GuardRadius, _assaultThreats);
            var job = _state?.Current;
            // The way home (Shombith, 2026-11-16: "when set sailing the fauna needs to get aggressive more"): any cell may
            // assault the Ark, every lure is stronger, and raiders come at it in the open water between cells.
            bool homeward = job is { IsReturn: true };
            bool hostile = cell && _hostileCells.Contains(cell);
            bool inside = ArkEscortRun.ArkInsideWorld(cell, ark.Position);
            Assaults.Tick(ark, _assaultLeg, inside, (hostile || homeward) && !_fallen && !_docked && _state is { AtSea: true });

            // The lure: every hostile cell of the leg at the tier's steady share, the Ark's own cell at the assault's. A
            // Crystal Run's calm cells, and every cell on the way home, go for the hull too ("make the cell's fauna attack
            // the hull").
            var tier = AttackTier;
            float steady = tier?.LureShare ?? 0f;
            float calm = (job is { CountsUnderway: true } || homeward) && _def ? _def.RaidCalmLureShare : 0f;
            float factor = homeward && _def ? _def.HomewardLureFactor : 1f;
            var squad = ark.Domain;
            foreach (var kv in _legIndexOf)
            {
                var c = kv.Key;
                if (!c) continue;
                float baseShare = _hostileCells.Contains(c) ? steady : calm;
                float share = Mathf.Clamp01((c == cell ? Assaults.Lure(baseShare) : baseShare) * factor);
                if (share > 0f) c.SetFeedingLure(ark.transform, squad, share);
                else c.SetFeedingLure(null, squad, 0f);
            }

            TickRaiders(ark, inside);
            CheckBreachLine(ark);
        }

        /// <summary>Below the job's breach line the Ark falls, as if its last plate had gone.</summary>
        void CheckBreachLine(Ark ark)
        {
            var job = _state?.Current;
            float line = job?.BreachHull ?? 0f;
            if (line <= 0f || _fallen || _docked || ark.TotalCount == 0 || ark.HealthFraction >= line) return;
            OnArkFallen();
            if (_sea && _sea.IsRunning) _sea.End(returnToCell: true, "the Ark's hull fell below the breach line");
        }

        /// <summary>The voyage is over (from <see cref="Finish"/>).</summary>
        void EndAssaults()
        {
            ClearLures();
            foreach (var c in _legIndexOf.Keys)
                if (c) c.ArmourIsFood = false;
            _legIndexOf.Clear();
            _hostileCells.Clear();
            HookAssaultArk(null);
            // Raiders are ordinary creatures once born: they live on (or starve) as any other; the run forgets them.
            _raiders.Clear();
            if (_assaultKillHooked) Fauna.Killed -= OnAssaultFaunaKilled;
            _assaultKillHooked = false;
            Assaults.End();
        }

        // ── Raiders between the cells on the way home ───────────────────────

        readonly List<Fauna> _raiders = new();
        float _nextRaiderWave = -1f;
        bool _raidersToasted;

        /// <summary>
        /// On the way home, while the Ark crosses the open water between cells, waves of grown creatures come at it
        /// (Shombith, 2026-11-16: "between cells bigger faunas should start spawning and attack the ark"). A raider is a
        /// species of one of the leg's cells held against the squad, born in that cell's controlling colour through the
        /// platform's one fauna spawn seam (the colour rule holds), grown by <see cref="VoyageToyDefinitionSO"/>'s raider
        /// scale, and lured onto the Ark like the rest of its cell. Each machine's own sea (a party friend's too) runs its own.
        /// </summary>
        void TickRaiders(Ark ark, bool inside)
        {
            _raiders.RemoveAll(f => !f || f.IsDying);
            var job = _state?.Current;
            if (!_def || job is not { IsReturn: true } || _fallen || _docked || _state is not { AtSea: true })
            {
                _nextRaiderWave = -1f;
                return;
            }
            float period = _def.HomewardRaiderSeconds;
            if (inside || period <= 0f || _def.HomewardRaiderCount <= 0) return;
            if (_nextRaiderWave < 0f)
            {
                _nextRaiderWave = Time.time + period * 0.5f;   // a breath of open water first
                return;
            }
            if (Time.time < _nextRaiderWave || _raiders.Count >= _def.HomewardRaiderMax) return;
            _nextRaiderWave = Time.time + period;

            var host = RaiderCell(ark.Domain, out var species);
            if (!host) return;
            float d = _def.HomewardRaiderDistance;
            int spawned = 0;
            for (int i = 0; i < _def.HomewardRaiderCount && _raiders.Count < _def.HomewardRaiderMax; i++)
            {
                var side = Vector3.ProjectOnPlane(Random.onUnitSphere, ark.Forward).normalized;
                var at = ark.Position + ark.Forward * (d * 0.6f) + side * (d * 0.8f);
                var cfg = species[Random.Range(0, species.Count)];
                var raider = CellLifeSpawnerBase.SpawnFaunaWithDomain(host, cfg.FaunaPrefab, ark.Position,
                    host.ControllingDomain, at, cfg);
                if (!raider) continue;
                raider.GrowBody(_def.HomewardRaiderScale);
                _raiders.Add(raider);
                spawned++;
            }
            if (spawned == 0 || _raidersToasted) return;
            _raidersToasted = true;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastRaiders, Lines.ToastRaidersSub);
        }

        static readonly List<FaunaConfigurationSO> s_raiderSpecies = new();

        /// <summary>A cell of this leg held by a colour other than the squad's, and its biggest species (predators when
        /// it has any): the raiders' home. Null when every cell is the squad's.</summary>
        Cell RaiderCell(Domains squad, out List<FaunaConfigurationSO> species)
        {
            species = s_raiderSpecies;
            foreach (var c in _legIndexOf.Keys)
            {
                if (!c || c.ControllingDomain == squad) continue;
                var profile = c.Config ? c.Config.SpawnProfile : null;
                if (!profile || profile.SupportedFaunas is not { Count: > 0 }) continue;
                species.Clear();
                foreach (var f in profile.SupportedFaunas)
                    if (f && f.FaunaPrefab && f.FaunaPrefab.Diet == FaunaDiet.Predator) species.Add(f);
                if (species.Count == 0)
                    foreach (var f in profile.SupportedFaunas)
                        if (f && f.FaunaPrefab) species.Add(f);
                if (species.Count > 0) return c;
            }
            return null;
        }

        // ── Plumbing ─────────────────────────────────────────────────────────

        void ClearLures()
        {
            foreach (var c in _legIndexOf.Keys)
                if (c) c.SetFeedingLure(null, Domains.Blue, 0f);
        }

        void HookAssaultArk(Ark ark)
        {
            if (_assaultArk) _assaultArk.HullPrismLost -= OnAssaultPlateLost;
            _assaultArk = ark;
            if (_assaultArk) _assaultArk.HullPrismLost += OnAssaultPlateLost;
        }

        void OnAssaultPlateLost(Prism prism, PrismKind kind)
        {
            if (_active) Assaults.PlateLost();
        }

        /// <summary>A creature of the Ark's own cell died near the Ark: it counts toward driving an assault off.</summary>
        void OnAssaultFaunaKilled(Fauna fauna, string killer)
        {
            var ark = Ark;
            if (!_active || !fauna || !ark || fauna.HomeCell != _assaultCell) return;
            float g = ArkEscortPilot.GuardRadius;
            if ((fauna.transform.position - ark.Position).sqrMagnitude <= g * g) Assaults.Kill();
        }
    }
}

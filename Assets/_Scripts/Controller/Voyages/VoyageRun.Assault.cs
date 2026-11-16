using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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

        ArkAssaultDriver Assaults => _assaultDriver ??= new ArkAssaultDriver(this, Banner);

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
                var job = _state?.Current;
                Assaults.NewWorld(job != null ? Content?.Location(job.To)?.Name : null);
            }

            if (Time.time < _nextAssaultTick) return;
            _nextAssaultTick = Time.time + AssaultTickSeconds;

            _assaultThreatsNear = CountThreats(ark.Position, ArkEscortPilot.GuardRadius, _assaultThreats);
            bool hostile = cell && _hostileCells.Contains(cell);
            bool inside = ArkEscortRun.ArkInsideWorld(cell, ark.Position);
            Assaults.Tick(ark, _assaultLeg, inside, hostile && !_fallen && !_docked && _state is { AtSea: true });

            // The lure: every hostile cell of the leg at the tier's steady share, the Ark's own cell at the assault's.
            var tier = AttackTier;
            float steady = tier?.LureShare ?? 0f;
            var squad = ark.Domain;
            foreach (var kv in _legIndexOf)
            {
                var c = kv.Key;
                if (!c) continue;
                float share = _hostileCells.Contains(c) ? (c == cell ? Assaults.Lure(steady) : steady) : 0f;
                if (share > 0f) c.SetFeedingLure(ark.transform, squad, share);
                else c.SetFeedingLure(null, squad, 0f);
            }

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
            if (_assaultKillHooked) Fauna.Killed -= OnAssaultFaunaKilled;
            _assaultKillHooked = false;
            Assaults.End();
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

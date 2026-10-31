using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The escort's tuning half: the runtime copy of the escort that named overrides write to (<see cref="ArkEscortParams"/>),
    /// the cell-aware pace handed to the Arkway, plates as food in the escort's worlds, and the metrics every
    /// <see cref="ArkEscortReport"/> line carries (seat proximity, kills on the Ark against elsewhere, plates lost per
    /// world, per-seat round stats, territory at each leg's close, time inside and outside cells, the Ark's speed).
    /// Kept apart from the run's flow so the two evolve without stepping on each other.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        /// <summary>The tighter escort ring (about the AI's station radius).</summary>
        const float CloseRadius = 300f;
        /// <summary>A creature counts as having threatened the Ark for this long after it was last seen within the guard radius.</summary>
        const float ThreatMemorySeconds = 10f;

        ArkEscortSO _escortAsset;
        ArkEscortSO _escortCopy;
        Ark _hookedArk;
        bool _killHooked;

        readonly Dictionary<IVesselStatus, int> _seatOf = new();
        readonly Dictionary<Fauna, float> _threatSeen = new();
        readonly List<Fauna> _threatScratch = new();
        readonly List<Fauna> _threatPrune = new();
        readonly List<(int seat, IVesselStatus status)> _seatScratch = new();
        Cell[] _cellOfLeg = System.Array.Empty<Cell>();

        struct StatMark
        {
            public int LifeformsKilled;
            public float VolumeCreated;
            public int BlocksCreated;
            public float HostileVolumeDestroyed;
        }

        // Round stats when each seat was first seen this run, and at this leg's start (or first seen in it), by player name.
        readonly Dictionary<string, StatMark> _runMark = new();
        readonly Dictionary<string, StatMark> _legMark = new();
        readonly Dictionary<string, (int seat, string vessel)> _seatNames = new();

        float _speedSum;
        int _speedSamples;

        // ── Set sail ─────────────────────────────────────────────────────────

        /// <summary>With overrides pending, sail a runtime COPY of the escort and write them to it (never to the asset).</summary>
        void UseRuntimeEscort(int difficulty)
        {
            if (_escortCopy) Destroy(_escortCopy);
            _escortCopy = null;
            _escort = _escortAsset ? _escortAsset : _escort;
            _pendingEcho = null;
            _paramTarget = null;
            var pending = ArkEscortParams.Pending;
            if (pending is not { Count: > 0 } || !_escort) return;
            _escortCopy = Instantiate(_escort);
            _escortCopy.name = _escort.name + " (runtime)";
            _escort = _escortCopy;
            _paramTarget = new ArkEscortParams.Target { Escort = _escortCopy, Difficulty = difficulty };
            _pendingEcho = ArkEscortParams.Apply(_paramTarget, pending);
        }

        // The overrides this run sailed with (null without any): their fauna half reaches each world in PrepareCellMetrics.
        ArkEscortParams.Target _paramTarget;

        List<ArkEscortReport.Param> _pendingEcho;

        /// <summary>The escort's cell-aware pace: the tier scales crawl and entry only, so the gaps stay short and the Ark
        /// never outruns its escort; the escort's own spacing replaces the Arkway's.</summary>
        void ApplyPacing(ArkEscortSO.Tier tier)
        {
            var pacing = _escort.ArkPacing;
            pacing.Crawl *= tier.ArkSpeed;
            pacing.Entry *= tier.ArkSpeed;
            _cfg.ArkPacing = pacing.Sanitized();
            if (_escort.CellSpacing > 0f) _cfg.CellSpacing = _escort.CellSpacing;
        }

        void BeginMetrics(ArkEscortSO.Tier tier)
        {
            _seatOf.Clear();
            _threatSeen.Clear();
            _runMark.Clear();
            _legMark.Clear();
            _seatNames.Clear();
            _speedSum = 0f;
            _speedSamples = 0;
            _cellOfLeg = new Cell[_escort.Legs.Count];
            _report.BreachHull = tier.BreachHull;
            _report.PacifyShare = _escort.PacifyShare;
            _report.ParamKeys = ArkEscortParams.KeyPatterns();
            if (_pendingEcho != null) _report.Params = _pendingEcho;
            _pendingEcho = null;
            if (!_killHooked)
            {
                Fauna.Killed += OnFaunaDied;
                _killHooked = true;
            }
        }

        /// <summary>Each escort world: armoured mass is food for species that gnaw it (the Ark's plates), and the leg's
        /// cell is remembered for its territory at the close.</summary>
        void PrepareCellMetrics(Cell cell, int leg)
        {
            cell.ArmourIsFood = _escort.PlatesAreFood;
            if (leg >= 0 && leg < _cellOfLeg.Length) _cellOfLeg[leg] = cell;

            // A run's fauna overrides (fauna.* keys): per-species values ride the cell to each creature it registers;
            // per-world scales compose onto the tier's population (production gating only, nothing is culled).
            var t = _paramTarget;
            if (t == null) return;
            if (t.Fauna is { HasAny: true }) cell.RuntimeFaunaTuning = t.Fauna;
            int world = leg + 1;
            cell.RuntimePopulationScale *= t.WorldValue(world, "populationScale", 1f);
            cell.RuntimeFaunaFloorScale = t.WorldValue(world, "floorScale", 1f);
            cell.RuntimeFaunaCapScale = t.WorldValue(world, "capScale", 1f);
            if (leg >= 0 && leg < _report.LegReports.Count) _report.LegReports[leg].Population = cell.RuntimePopulationScale;
        }

        void HookArk()
        {
            var ark = Ark;
            if (ark == _hookedArk) return;
            UnhookArk();
            if (!ark) return;
            _hookedArk = ark;
            ark.HullPrismLost += OnHullPrismLost;
            ark.HullPrismRegrown += OnHullPrismRegrown;
        }

        void UnhookArk()
        {
            if (_hookedArk)
            {
                _hookedArk.HullPrismLost -= OnHullPrismLost;
                _hookedArk.HullPrismRegrown -= OnHullPrismRegrown;
            }
            _hookedArk = null;
        }

        void UnhookMetrics()
        {
            UnhookArk();
            if (_killHooked) Fauna.Killed -= OnFaunaDied;
            _killHooked = false;
        }

        // ── Events ───────────────────────────────────────────────────────────

        void OnHullPrismLost(Prism prism, PrismKind kind)
        {
            if (!_active || _report == null) return;
            int leg = Mathf.Max(0, _leg);
            if (leg >= _report.LegReports.Count) return;
            var r = _report.LegReports[leg];
            r.PlatesLost++;
            _report.PlatesLost++;
            switch (kind)
            {
                case PrismKind.SuperShielded: r.PlatesLostSuper++; _report.PlatesLostSuper++; break;
                case PrismKind.Shielded: r.PlatesLostShielded++; _report.PlatesLostShielded++; break;
                default: r.PlatesLostPlain++; break;
            }
            bool creature = prism && prism.DestroyedByCreature;
            if (creature) { r.PlatesByCreature++; _report.PlatesByCreature++; }
            string killer = prism && !string.IsNullOrEmpty(prism.DestroyerName) ? prism.DestroyerName : "unknown";
            Bump(r.PlateKillers, killer);
        }

        void OnHullPrismRegrown(Prism prism)
        {
            if (!_active || _report == null) return;
            int leg = Mathf.Max(0, _leg);
            if (leg >= _report.LegReports.Count) return;
            _report.LegReports[leg].PlatesRegrown++;
            _report.PlatesRegrown++;
        }

        /// <summary>An attributed creature death (simulation authority only): on the Ark when the creature had been
        /// within the guard radius of the Ark in the last few seconds, or is now.</summary>
        void OnFaunaDied(Fauna fauna, string killer)
        {
            if (!_active || _report == null || !fauna) return;
            var cell = fauna.HomeCell;
            if (!cell || !_legOf.TryGetValue(cell, out int leg)) return;
            var ark = Ark;
            bool onArk = _threatSeen.TryGetValue(fauna, out float seen) && Time.time - seen <= ThreatMemorySeconds;
            if (!onArk && ark)
            {
                float g = ArkEscortPilot.GuardRadius;
                onArk = (fauna.transform.position - ark.Position).sqrMagnitude <= g * g;
            }
            _threatSeen.Remove(fauna);

            var r = _report.LegReports[leg];
            if (onArk) { r.KillsOnArk++; _report.KillsOnArk++; }
            else { r.KillsElsewhere++; _report.KillsElsewhere++; }

            if (string.IsNullOrEmpty(killer) || !_seatNames.TryGetValue(killer, out var seat)) return;
            var legSeat = SeatStatsFor(r.SeatStats, seat.seat, seat.vessel, killer);
            var runSeat = SeatStatsFor(_report.RunSeats, seat.seat, seat.vessel, killer);
            legSeat.Kills++;
            runSeat.Kills++;
            if (onArk) { legSeat.KillsOnArk++; runSeat.KillsOnArk++; }
        }

        // ── Legs and samples ─────────────────────────────────────────────────

        void OpenLegMetrics(int leg)
        {
            _legMark.Clear();
            CollectSeats();
            foreach (var (_, status) in _seatScratch)
            {
                string name = NameOf(status);
                if (name.Length > 0 && TryStats(name, out var mark)) _legMark[name] = mark;
            }
        }

        void CloseLegMetrics(int leg, ArkEscortReport.LegReport r)
        {
            var ark = Ark;
            if (ark)
            {
                ark.CountAlive(out int plain, out int shielded, out int super);
                r.PlainAtExit = plain;
                r.ShieldedAtExit = shielded;
                r.SuperAtExit = super;
            }
            r.MeanSpeed = r.SpeedSamples > 0 ? r.SpeedSum / r.SpeedSamples : 0f;
            var exitCell = leg >= 0 && leg < _cellOfLeg.Length ? _cellOfLeg[leg] : null;
            if (exitCell) r.SquadShareAtExit = SquadShare(exitCell, SquadDomain);

            // Per-seat round stats over the leg.
            foreach (var kv in _seatNames)
            {
                if (!TryStats(kv.Key, out var now)) continue;
                var start = _legMark.TryGetValue(kv.Key, out var m) ? m : _runMark.GetValueOrDefault(kv.Key, now);
                var s = SeatStatsFor(r.SeatStats, kv.Value.seat, kv.Value.vessel, kv.Key);
                Fill(s, now, start);
            }

            // Territory at the close.
            var cell = leg >= 0 && leg < _cellOfLeg.Length ? _cellOfLeg[leg] : null;
            if (cell)
            {
                r.ControllerAtExit = cell.DominantDomain.ToString();
                r.Territory.Clear();
                foreach (Domains d in System.Enum.GetValues(typeof(Domains)))
                    r.Territory.Add(new ArkEscortReport.TeamVolume
                    {
                        Domain = d.ToString(),
                        Volume = cell.GetDomainVolume(d),
                        ControlVolume = cell.GetControlVolume(d),
                    });
            }
        }

        void SampleMetrics(Ark ark, ArkEscortReport.LegReport r)
        {
            // Threat memory for the kills-on-Ark split (the creatures within the guard radius this sample).
            float now = Time.time;
            for (int i = 0; i < _threatScratch.Count; i++)
                if (_threatScratch[i]) _threatSeen[_threatScratch[i]] = now;
            if (_threatSeen.Count > 256)
            {
                _threatPrune.Clear();
                foreach (var kv in _threatSeen)
                    if (!kv.Key || now - kv.Value > ThreatMemorySeconds) _threatPrune.Add(kv.Key);
                foreach (var f in _threatPrune) _threatSeen.Remove(f);
                _threatPrune.Clear();
            }

            TryPacify();

            // Inside a cell or in the open, and the Ark's speed.
            if (ark.InsideCell) { r.InsideSeconds += SampleSeconds; _report.InsideSeconds += SampleSeconds; }
            else { r.OutsideSeconds += SampleSeconds; _report.OutsideSeconds += SampleSeconds; }
            r.SpeedSum += ark.Speed;
            r.SpeedSamples++;
            _speedSum += ark.Speed;
            _speedSamples++;

            // Each seat's distance to the Ark.
            CollectSeats();
            float c2 = CloseRadius * CloseRadius;
            float g2 = ArkEscortPilot.GuardRadius * ArkEscortPilot.GuardRadius;
            foreach (var (seat, status) in _seatScratch)
            {
                string name = NameOf(status);
                string vessel = status.VesselType.ToString();
                if (name.Length > 0 && !_seatNames.ContainsKey(name))
                {
                    _seatNames[name] = (seat, vessel);
                    if (TryStats(name, out var mark))
                    {
                        _runMark[name] = mark;
                        if (!_legMark.ContainsKey(name)) _legMark[name] = mark;
                    }
                }
                var near = SeatNearFor(r.Seats, seat, vessel, name);
                near.Samples++;
                float d2 = (status.Transform.position - ark.Position).sqrMagnitude;
                if (d2 <= c2) near.Near300++;
                if (d2 <= g2) near.Near900++;
            }
        }

        void FinishMetrics()
        {
            _report.MeanArkSpeed = _speedSamples > 0 ? _speedSum / _speedSamples : 0f;
            foreach (var kv in _seatNames)
            {
                if (!TryStats(kv.Key, out var now)) continue;
                var start = _runMark.GetValueOrDefault(kv.Key, now);
                Fill(SeatStatsFor(_report.RunSeats, kv.Value.seat, kv.Value.vessel, kv.Key), now, start);
            }
            foreach (var cell in _cellOfLeg)
                if (cell)
                {
                    cell.ArmourIsFood = false;
                    // The tuning stays on a cell its creatures still read; only the scales go back for the next run.
                    cell.RuntimeFaunaFloorScale = 1f;
                    cell.RuntimeFaunaCapScale = 1f;
                }
            _cellOfLeg = System.Array.Empty<Cell>();
            UnhookMetrics();
            _threatSeen.Clear();
            _seatOf.Clear();
        }

        // ── Pacification ─────────────────────────────────────────────────────

        Domains SquadDomain => Ark ? Ark.Domain : VoyageRun.LiveDomain(_context);

        /// <summary>The squad's share of a world's CONTROL volume (what decides who holds it), 0 when it holds nothing.</summary>
        static float SquadShare(Cell cell, Domains squad)
        {
            float mine = 0f, total = 0f;
            foreach (Domains d in System.Enum.GetValues(typeof(Domains)))
            {
                float v = cell.GetControlVolume(d);
                total += v;
                if (d == squad) mine = v;
            }
            return total > 0f ? mine / total : 0f;
        }

        /// <summary>
        /// Every standing escort world the squad now holds past the escort's pacifyShare is PACIFIED, once: its control is
        /// pinned to the squad (<see cref="Cell.SetModeControlOverride"/>: the live swarm is re-coloured, never killed, and
        /// every later creature spawns in the squad's domain), its fauna stop grazing the squad's mass
        /// (<see cref="Cell.SparedDomain"/>) and stop gnawing armour. Checked on the run's 1 Hz sample, for worlds ahead of
        /// the Ark too: a cultivator that races ahead to the next nucleus pacifies it before the Ark arrives. Server only
        /// (the pin replicates through the cell's own sync).
        /// </summary>
        void TryPacify()
        {
            float line = _escort ? _escort.PacifyShare : 0f;
            if (line <= 0f || _report == null) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) return;
            var squad = SquadDomain;
            for (int leg = 0; leg < _cellOfLeg.Length; leg++)
            {
                var cell = _cellOfLeg[leg];
                if (!cell || leg >= _report.LegReports.Count) continue;
                var r = _report.LegReports[leg];
                if (r.Pacified) continue;
                float share = SquadShare(cell, squad);
                if (share < line) continue;

                int before = HostileCount(cell, squad);
                cell.SparedDomain = squad;
                cell.ArmourIsFood = false;
                cell.SetModeControlOverride(squad);
                int turned = before - HostileCount(cell, squad);   // a creature that refuses a re-colour stays hostile

                r.Pacified = true;
                r.PacifiedAt = Time.time;
                r.PacifyShare = share;
                r.PacifiedAhead = r.EnteredAt <= 0f || leg > _leg;
                r.FaunaTurned = turned;
                _report.PacifiedLegs++;
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    $"[ArkEscort] {r.Name} pacified at {share:P0} of its control volume ({turned} creatures turned).");
            }
        }

        static int HostileCount(Cell cell, Domains squad)
        {
            int n = 0;
            var fauna = cell.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
                if (fauna[i] && fauna[i].Domain != squad) n++;
            return n;
        }

        // ── Plumbing ─────────────────────────────────────────────────────────

        /// <summary>The squad's live seats: the local vessel (its own seat) and every armed AI seat.</summary>
        void CollectSeats()
        {
            _seatScratch.Clear();
            var local = LocalVessel();
            if (local?.Transform)
                _seatScratch.Add((_seatOf.TryGetValue(local, out int ls) ? ls
                    : Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1), local));
            foreach (var w in _wingmates)
            {
                var s = Alive(w);
                if (s?.Transform == null || ReferenceEquals(s, local)) continue;
                _seatScratch.Add((_seatOf.TryGetValue(s, out int seat) ? seat : -1, s));
            }
        }

        static string NameOf(IVesselStatus status) => status?.Player != null ? status.Player.Name ?? string.Empty : string.Empty;

        bool TryStats(string playerName, out StatMark mark)
        {
            mark = default;
            var data = _context?.GameData;
            if (!data || string.IsNullOrEmpty(playerName) || !data.TryGetRoundStats(playerName, out var stats) || stats == null)
                return false;
            mark = new StatMark
            {
                LifeformsKilled = stats.LifeformsKilled,
                VolumeCreated = stats.VolumeCreated,
                BlocksCreated = stats.BlocksCreated,
                HostileVolumeDestroyed = stats.HostileVolumeDestroyed,
            };
            return true;
        }

        static void Fill(ArkEscortReport.SeatStats s, StatMark now, StatMark start)
        {
            s.LifeformsKilled = now.LifeformsKilled - start.LifeformsKilled;
            s.VolumeCreated = now.VolumeCreated - start.VolumeCreated;
            s.BlocksCreated = now.BlocksCreated - start.BlocksCreated;
            s.HostileVolumeDestroyed = now.HostileVolumeDestroyed - start.HostileVolumeDestroyed;
        }

        static ArkEscortReport.SeatStats SeatStatsFor(List<ArkEscortReport.SeatStats> list, int seat, string vessel, string name)
        {
            foreach (var s in list)
                if (s.PlayerName == name) return s;
            var made = new ArkEscortReport.SeatStats { Seat = seat, Vessel = vessel, PlayerName = name };
            list.Add(made);
            return made;
        }

        static ArkEscortReport.SeatNear SeatNearFor(List<ArkEscortReport.SeatNear> list, int seat, string vessel, string name)
        {
            foreach (var s in list)
                if (s.Seat == seat && s.PlayerName == name) return s;
            var made = new ArkEscortReport.SeatNear { Seat = seat, Vessel = vessel, PlayerName = name };
            list.Add(made);
            return made;
        }

        static void Bump(List<ArkEscortReport.NameCount> list, string name)
        {
            foreach (var n in list)
                if (n.Name == name) { n.Count++; return; }
            list.Add(new ArkEscortReport.NameCount { Name = name, Count = 1 });
        }
    }
}

using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Crystal Run on the voyage (Docs/Voyages/CRYSTAL_RUN.md; Shombith, 2026-11-16): the crystals the squad steals are
    /// counted from the moment the Ark sets sail, by seat and element (<see cref="VoyageRaidTally"/>); each cell of the
    /// leg opens with a toast, calm or hostile; the objective box reads <see cref="TryGetObjective"/>; the stop shows the
    /// scoreboard (Venture further sails the next run from here, Finish sails the Ark home with the squad escorting it),
    /// and docking home or a fallen Ark shows the last board. The rules are <see cref="VoyageRaid"/>.
    /// </summary>
    public sealed partial class VoyageRun : IVoyageObjectiveSource
    {
        readonly VoyageRaidTally _raid = new();
        readonly Dictionary<Cell, int> _legIndex = new();
        bool _raidHooked;
        bool _goalToastShown;
        ArkAlarmToasts _alarms;
        ArkAttackerMarks _attackerMarks;
        int _toastedCell = -1;
        float _legStartedAt;

        /// <summary>True while the job under way counts its work from the moment the Ark set sail.</summary>
        bool Raiding => _state?.Current is { CountsUnderway: true };

        // ── The leg ─────────────────────────────────────────────────────────

        /// <summary>A Crystal Run's corridor, fitted to its cells (<see cref="VoyageRaid.FitCorridor"/>).</summary>
        List<int> FitLeg(VoyageJob job, List<int> route) =>
            job is { Cells: > 0 } ? VoyageRaid.FitCorridor(route, job.Cells, _worlds, _state.Day, job.From + ">" + job.To) : route;

        /// <summary>A new leg: the pacing fitted like the corridor, the count started over, and the counter armed.</summary>
        void BeginLegCount(VoyageJob job)
        {
            if (job is { Cells: > 0 }) _profile = VoyageRaid.FitProfile(_profile, job.Cells);
            _legIndex.Clear();
            _toastedCell = -1;
            _goalToastShown = false;
            _legStartedAt = Time.time;
            _raid.Clear();
            HookRaid(job is { CountsUnderway: true });
        }

        /// <summary>The corridor stood a cell: which of the leg's cells it is.</summary>
        void NoteLegCell(Cell cell, int routeIndex)
        {
            if (cell) _legIndex[cell] = routeIndex;
        }

        void HookRaid(bool on)
        {
            if (on == _raidHooked) return;
            _raidHooked = on;
            if (on)
            {
                ElementalCrystalImpactor.OnCrystalCollectedAs += OnRaidCrystal;
                Fauna.Killed += OnRaidKill;
            }
            else
            {
                ElementalCrystalImpactor.OnCrystalCollectedAs -= OnRaidCrystal;
                Fauna.Killed -= OnRaidKill;
            }
        }

        /// <summary>A crystal came to someone: it counts when a seat of this squad took it, while the run is on.</summary>
        void OnRaidCrystal(string playerName, Element element)
        {
            if (!_active || !Raiding || _fallen || !IsSquadSeat(playerName)) return;
            if (!_raid.Add(playerName, element)) return;
            NoteSeatVessel(playerName);
            // A party (Docs/Voyages/PARTY.md §4): a friend tells the host, the host tells everyone.
            ReportToHost(VoyagePartyReport.ReportKind.Crystal, playerName, element, false);
            if (!_following) _tallyDirty = true;
            CheckGoalToast();
        }

        /// <summary>The goal toast, once a leg, when the squad's count (the party's, in a party) reaches it.</summary>
        void CheckGoalToast()
        {
            var job = _state?.Current;
            if (job == null || _goalToastShown || _raid.Total < job.Goal) return;
            _goalToastShown = true;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Good, TemplateText.Fill(Lines.ToastGoalReached, "goal", Mathf.CeilToInt(job.Goal).ToString()));
        }

        void OnRaidKill(Fauna fauna, string killer)
        {
            if (!_active || !Raiding || !IsSquadSeat(killer)) return;
            var ark = Ark;
            float g = ArkEscortPilot.GuardRadius;
            bool near = fauna && ark && (fauna.transform.position - ark.Position).sqrMagnitude <= g * g;
            _raid.Kill(killer, near);
            NoteSeatVessel(killer);
            ReportToHost(VoyagePartyReport.ReportKind.Kill, killer, Element.None, near);
            if (!_following) _tallyDirty = true;
        }

        /// <summary>The seat's vessel class on its count (a friend's scoreboard shows the host's seats by it).</summary>
        void NoteSeatVessel(string playerName)
        {
            var seat = _raid.SeatOf(playerName);
            if (seat == null) return;
            var local = LocalVessel();
            if (local != null && local.PlayerName == playerName) { seat.Vessel = (int)local.VesselType; return; }
            foreach (var w in _wingmates)
            {
                var s = Alive(w);
                if (s != null && s.PlayerName == playerName) { seat.Vessel = (int)s.VesselType; return; }
            }
        }

        bool IsSquadSeat(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return false;
            var local = LocalVessel();
            if (local != null && local.PlayerName == playerName) return true;
            foreach (var w in _wingmates)
            {
                var s = Alive(w);
                if (s != null && s.PlayerName == playerName) return true;
            }
            return false;
        }

        /// <summary>Each cell of the leg opens with one toast: calm or hostile, and the last is the destination.</summary>
        void TickLegToasts()
        {
            if (!_underway || _fallen || _state?.Current == null || !_state.AtSea) return;
            int cell = LegCell(out int cells);
            if (cell <= 0 || cell == _toastedCell) return;
            _toastedCell = cell;
            if (cell == cells && cells > 1)
            {
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Event, Lines.ToastFinalCell);
                return;
            }
            bool hostile = LegHostile(cell - 1);
            VoyageToasts.Show(_sea, hostile ? ArkwayVoyageHud.Mood.Threat : ArkwayVoyageHud.Mood.Event,
                TemplateText.Fill(hostile ? Lines.ToastCellHostile : Lines.ToastCellCalm, "cell", cell.ToString()),
                hostile ? Lines.ToastCellHostileSub : Lines.ToastCellCalmSub);
        }

        /// <summary>The Ark sets off: the run's goal, or the way home, as the voyage's first toast.</summary>
        void ToastSetOff(VoyageJob job)
        {
            if (job == null) return;
            var to = PlaceName(job.To);
            if (job.IsReturn)
            {
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Event, Lines.ToastHome, TemplateText.Fill(Lines.ToastHomeSub, "to", to));
                return;
            }
            if (!job.CountsUnderway) return;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Event,
                TemplateText.Fill(Lines.ToastSetOff, "goal", Mathf.CeilToInt(job.Goal).ToString(), "to", to),
                TemplateText.Fill(Lines.ToastSetOffSub, "to", to, "breach", Mathf.RoundToInt(job.BreachHull * 100f).ToString()));
        }

        /// <summary>The cell the Ark is in, from 1 (0 while unknown), and the leg's cells.</summary>
        int LegCell(out int cells)
        {
            cells = _profile?.Count ?? 0;
            var cell = CurrentCell;
            return cell && _legIndex.TryGetValue(cell, out int i) ? i + 1 : 0;
        }

        /// <summary>Whether cell <paramref name="index"/> (from 0) of the leg is held against the squad: the job's own
        /// pattern when it has one (cells past its end take its last entry), else only a hostile destination.</summary>
        bool LegHostile(int index)
        {
            return VoyageRules.HostileCell(_state?.Current, index, _profile?.Count ?? 0, _hostileWorld);
        }

        // ── The objective box ───────────────────────────────────────────────

        public bool TryGetObjective(out VoyageObjective objective)
        {
            objective = default;
            if (!_active || !_underway || _state == null || Content == null) return false;
            var job = _state.Current;
            var ark = _sea ? _sea.Ark : null;
            objective.Hull = ark ? ark.HealthFraction : 1f;
            objective.AtStake = OwedTotal();
            objective.Cell = LegCell(out objective.Cells);
            objective.HostileCells = new bool[Mathf.Max(0, objective.Cells)];
            for (int i = 0; i < objective.HostileCells.Length; i++) objective.HostileCells[i] = LegHostile(i);
            objective.TakeArk(Status);
            if (job is { CountsUnderway: true })
            {
                objective.Goal = VoyageObjective.Kind.CrystalRun;
                objective.To = PlaceName(job.To);
                objective.Done = _raid.Total;
                objective.Target = Mathf.CeilToInt(job.Goal);
                objective.Crystals = new int[(int)Element.Omni + 1];
                foreach (var e in VoyageRaid.Elements) objective.Crystals[(int)e] = _raid.Of(e);
                objective.Breach = job.BreachHull;
                objective.Hostile = objective.Cell > 0 && LegHostile(objective.Cell - 1);
                return true;
            }
            if (job is { IsReturn: true } && _state.AtSea)
            {
                objective.Goal = VoyageObjective.Kind.SailingHome;
                objective.To = PlaceName(job.To);
                objective.Hostile = objective.Cell > 0 && LegHostile(objective.Cell - 1);
                return true;
            }
            return false;
        }

        int OwedTotal()
        {
            int n = 0;
            if (_state?.Owed != null)
                foreach (var c in _state.Owed) n += c;
            return n;
        }

        string PlaceName(string locationId) => Content?.Location(locationId)?.Name ?? string.Empty;

        // ── The stop: the scoreboard ────────────────────────────────────────

        /// <summary>
        /// A Crystal Run reached its stop: what was stolen is owed (at stake until home), the job resolves on the goal,
        /// and the scoreboard asks what next. The Ark holds here until the pilot chooses.
        /// </summary>
        void ArriveRaid(VoyageJob job)
        {
            var seconds = Time.time - _legStartedAt;
            var r = VoyageRaid.Arrive(_state, Content, _raid);
            if (r == null) return;
            Chronicle(r);
            Relic(r);
            SaveState();
            VoyageHarbour.RaiseChanged();

            var board = new VoyageScoreboard
            {
                Board = VoyageScoreboard.Kind.CrystalRun,
                Succeeded = r.Outcome == JobOutcome.Succeeded,
                To = PlaceName(job.To),
                Done = _raid.Total,
                Goal = Mathf.CeilToInt(job.Goal),
                Cells = _profile?.Count ?? job.Cells,
                Seconds = seconds,
                Hull = _sea && _sea.Ark ? _sea.Ark.HealthFraction : 1f,
                Breach = job.BreachHull,
                Pay = r.Outcome == JobOutcome.Succeeded ? job.Pay : 0,
                // A friend's board: the host chooses what next (the next leg, or the way home, reaches them on its own).
                Following = _following,
            };
            foreach (var e in VoyageRaid.Elements) board.Crystals[(int)e] = _raid.Of(e);
            AddRows(board);

            if (!_following)
            {
                var next = VoyageRaid.Venture(_state, Content, job);
                if (next != null)
                {
                    board.NextGoal = Mathf.CeilToInt(next.Goal);
                    board.NextCells = next.Cells;
                    board.Venture = () => SailOn(next);
                }
                board.Finish = SailHome;
            }
            VoyageScoreboard.Show(board);
        }

        /// <summary>One row per seat, the pilot's first, with what each stole and felled on the leg: this machine's AI
        /// wingmates, then the party's other humans, then any seat the host's count names that is not here (on a friend's
        /// machine, the host's AI wingmates).</summary>
        void AddRows(VoyageScoreboard board)
        {
            var named = new HashSet<string>();
            void Row(IVesselStatus status, bool you)
            {
                if (status == null || string.IsNullOrEmpty(status.PlayerName) || !named.Add(status.PlayerName)) return;
                var seat = _raid.SeatOf(status.PlayerName);
                board.Rows.Add(new VoyageScoreboard.Row
                {
                    Name = status.PlayerName,
                    You = you,
                    Vessel = status.VesselType,
                    Crystals = seat?.Total ?? 0,
                    Kills = seat?.Kills ?? 0,
                    Guarded = seat?.Guarded ?? 0,
                });
            }
            Row(LocalVessel(), true);
            foreach (var w in _wingmates) Row(Alive(w), false);
            foreach (var (status, _) in OtherHumans()) Row(status, false);
            foreach (var seat in _raid.Seats)
            {
                if (seat == null || string.IsNullOrEmpty(seat.PlayerName) || !named.Add(seat.PlayerName)) continue;
                board.Rows.Add(new VoyageScoreboard.Row
                {
                    Name = seat.PlayerName,
                    Vessel = (VesselClassType)seat.Vessel,
                    Crystals = seat.Total,
                    Kills = seat.Kills,
                    Guarded = seat.Guarded,
                });
            }
        }

        /// <summary>Venture further: the next run sails from here.</summary>
        void SailOn(VoyageJob next)
        {
            if (!_active || next == null) return;
            var local = LocalVessel();
            if (local == null) return;
            SetSail(local, next);
        }

        /// <summary>Finish: the Ark sails home with the squad escorting it; docking banks everything.</summary>
        void SailHome()
        {
            if (!_active || _state == null) return;
            var local = LocalVessel();
            if (local == null) return;
            if (_state.IsHome())
            {
                GiveUp("finished at home");
                return;
            }
            SetSail(local, VoyageRules.ReturnJob(_state, Content));
        }

        /// <summary>Docked home: the board says what was banked.</summary>
        void ShowDocked(VoyageRules.Extraction x)
        {
            var board = new VoyageScoreboard
            {
                Board = VoyageScoreboard.Kind.Docked,
                Succeeded = true,
                To = PlaceName(_state.At),
                Hull = _sea && _sea.Ark ? _sea.Ark.HealthFraction : 1f,
                Banked = true,
                Following = _following,
            };
            if (x != null)
                for (int e = 0; e < x.Crystals.Length && e < board.Crystals.Length; e++) board.Crystals[e] = x.Crystals[e];
            AddRows(board);
            VoyageScoreboard.Show(board);
        }

        /// <summary>Everything at stake right now, by element: what is owed, and what this run has stolen so far.</summary>
        int[] StakeByElement()
        {
            var stake = new int[(int)Element.Omni + 1];
            for (int e = 0; e < stake.Length; e++) stake[e] = _state?.OwedOf((Element)e) ?? 0;
            if (Raiding)
                foreach (var e in VoyageRaid.Elements) stake[(int)e] += _raid.Of(e);
            return stake;
        }

        /// <summary>The Ark fell: everything aboard went with it (<paramref name="stake"/>, by element).</summary>
        void ShowLost(int[] stake, VoyageJob lost)
        {
            var board = new VoyageScoreboard
            {
                Board = VoyageScoreboard.Kind.Lost,
                Succeeded = false,
                To = PlaceName(lost?.To ?? _state?.At),
                Done = _raid.Total,
                Goal = Mathf.CeilToInt(lost?.Goal ?? 0f),
                Seconds = Time.time - _legStartedAt,
                Hull = 0f,
                Following = _following,
            };
            for (int e = 0; e < board.Crystals.Length && stake != null && e < stake.Length; e++) board.Crystals[e] = stake[e];
            AddRows(board);
            // Try again: the same work from wherever the fall left the crew, once the sea has ended.
            string template = lost?.TemplateId;
            if (!_following && lost is { CountsUnderway: true } && !string.IsNullOrEmpty(template))
                board.TryAgain = () => VoyageHarbour.Current?.TryAgain(template);
            VoyageScoreboard.Show(board);
        }

        void EndRaid()
        {
            HookRaid(false);
            _raid.Clear();
            _legIndex.Clear();
            _alarms?.Unhook();
            if (_attackerMarks) Destroy(_attackerMarks.gameObject);
            _attackerMarks = null;
            if (ReferenceEquals(VoyageObjective.Source, this)) VoyageObjective.Source = null;
        }
    }
}

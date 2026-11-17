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
    public sealed partial class VoyageRun : IVoyageObjectiveSource, IPauseMenuAction
    {
        readonly VoyageRaidTally _raid = new();
        /// <summary>Every leg of this voyage already sailed, seat by seat: <see cref="_raid"/> starts over each leg, and
        /// the way home's docked board shows the whole voyage (Shombith's 2026-11-16 playtest: a docked board of zeros).</summary>
        readonly VoyageRaidTally _voyageRaid = new();
        readonly Dictionary<Cell, int> _legIndex = new();
        bool _raidHooked;
        bool _goalToastShown;
        ArkAlarmToasts _alarms;
        ArkAttackerMarks _attackerMarks;
        int _toastedCell = -1;
        float _legStartedAt;
        bool _anchorWarned;
        float _nextBankCheck;
        readonly Dictionary<string, float> _spillReadyAt = new();
        const float BankCheckSeconds = 0.2f;
        float _nextLeak;
        bool _leakToasted;

        /// <summary>The run's banked crystals reach its goal: the Ark stops anchoring, and the pause window offers to
        /// complete the run here (Shombith, 2026-11-16).</summary>
        bool GoalMet => Raiding && _state.Current.Goal > 0f && _raid.Total >= Mathf.CeilToInt(_state.Current.Goal);

        /// <summary>The pause window's "Complete and set sail": the host, with the goal met, sailing a leg it has not
        /// finished.</summary>
        bool CanComplete => _active && !_following && !_fallen && GoalMet && _sea && _sea.IsRunning
                            && !_sea.IsHoldingAtRouteEnd && !VoyageScoreboard.IsOpen;

        /// <summary>A Crystal Run carries its crystals until they are banked at the Ark (P6), unless the settings turn it
        /// off.</summary>
        bool CrystalsAtRisk => !_def || _def.RaidCrystalsAtRisk;

        /// <summary>True while the job under way counts its work from the moment the Ark set sail.</summary>
        bool Raiding => _state?.Current is { CountsUnderway: true };

        // ── The leg ─────────────────────────────────────────────────────────

        /// <summary>A Crystal Run's corridor, fitted to its cells (<see cref="VoyageRaid.FitCorridor"/>).</summary>
        /// The job's named worlds (<see cref="VoyageJob.CellWorlds"/>) then stand in the cells they name.
        List<int> FitLeg(VoyageJob job, List<int> route) =>
            job is { Cells: > 0 }
                ? VoyageRaid.NameCells(VoyageRaid.FitCorridor(route, job.Cells, _worlds, _state.Day, job.From + ">" + job.To),
                    job.CellWorlds, _worldNames)
                : route;

        /// <summary>A new leg: the pacing fitted like the corridor, the count started over, and the counter armed.</summary>
        void BeginLegCount(VoyageJob job)
        {
            if (job is { Cells: > 0 }) _profile = VoyageRaid.LayCellLife(VoyageRaid.FitProfile(_profile, job.Cells), job.CellLife);
            _legIndex.Clear();
            _toastedCell = -1;
            _goalToastShown = false;
            _anchorWarned = false;
            _spillReadyAt.Clear();
            _leakToasted = false;
            _legStartedAt = Time.time;
            _voyageRaid.Absorb(_raid);
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
                VesselImpactor.PrismHullContact += OnRaidHullContact;
            }
            else
            {
                ElementalCrystalImpactor.OnCrystalCollectedAs -= OnRaidCrystal;
                Fauna.Killed -= OnRaidKill;
                VesselImpactor.PrismHullContact -= OnRaidHullContact;
            }
        }

        /// <summary>A crystal came to someone: it counts when a seat of this squad took it, while the run is on.</summary>
        void OnRaidCrystal(string playerName, Element element)
        {
            if (!_active || !Raiding || _fallen || !IsSquadSeat(playerName)) return;
            NoteSeatVessel(playerName);
            // Carried until banked at the Ark (P6), or counted at once when the settings turn that off.
            if (CrystalsAtRisk)
            {
                _raid.Carry(playerName, element);
                ToastPickup(playerName, element, true);
                return;
            }
            if (!_raid.Add(playerName, element)) return;
            ToastPickup(playerName, element, false);
            // A party (Docs/Voyages/PARTY.md §4): a friend tells the host, the host tells everyone.
            ReportToHost(VoyagePartyReport.ReportKind.Crystal, playerName, element, false);
            if (!_following) _tallyDirty = true;
            CheckGoalToast();
        }

        /// <summary>The local pilot's pickup toast (Shombith, 2026-11-17: "when we collect the crystals no popup comes"):
        /// one running toast that each pickup rewrites, so a string of crystals never floods the toast spot. No sound of its
        /// own; the crystal already sounds.</summary>
        void ToastPickup(string playerName, Element element, bool carried)
        {
            var local = LocalVessel();
            if (local == null || local.PlayerName != playerName || string.IsNullOrEmpty(Lines?.ToastCrystal)) return;
            string line = TemplateText.Fill(Lines.ToastCrystal, "element", CosmicShore.UI.VoyageHudColors.ElementName(Lines, element));
            string under;
            if (carried)
                under = TemplateText.Fill(Lines.ToastCrystalCarrySub, "carried", _raid.CarriedBy(playerName).ToString());
            else
                under = TemplateText.Fill(Lines.ToastBankedSub, "count", "1", "done", _raid.Total.ToString(),
                    "goal", Mathf.CeilToInt(_state?.Current?.Goal ?? 0f).ToString());
            if (_sea) _sea.ShowRunningBanner(line, PickupToastSeconds, ArkwayVoyageHud.Mood.Good, under);
        }

        const float PickupToastSeconds = 1.8f;

        /// <summary>The goal toast, once a leg, when the squad's count (the party's, in a party) reaches it.</summary>
        void CheckGoalToast()
        {
            var job = _state?.Current;
            if (job == null || _goalToastShown || _raid.Total < job.Goal) return;
            _goalToastShown = true;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Good, TemplateText.Fill(Lines.ToastGoalReached, "goal", Mathf.CeilToInt(job.Goal).ToString()),
                _following ? string.Empty : Lines.ToastGoalReachedSub);
        }

        // ── Complete and set sail (Shombith, 2026-11-16) ────────────────────

        public string PauseActionLabel => CanComplete ? Lines.PauseCompleteAndSail : string.Empty;

        /// <summary>The run ends where the Ark is: it holds there and the scoreboard asks what next (Venture further, or
        /// Finish and sail home).</summary>
        public void OnPauseAction()
        {
            if (CanComplete) _sea.EndRouteHere();
        }

        // ── The leaking hold (Shombith, 2026-11-16: "if the hull's health drops below 30% it loses banked crystals one
        //    by one") ─────────────────────────────────────────────────────────

        /// <summary>Below the leak line the hold loses a banked crystal every few seconds: this leg's first, then what is
        /// owed from earlier legs. Each one spills out of the Ark into the sea, where any pilot can take it back. The host
        /// decides; a party friend sees it through the host's count.</summary>
        void TickHullLeak()
        {
            if (!_active || _following || _fallen || _docked || !_def || _state is not { AtSea: true }) return;
            var ark = Ark;
            float line = _def.RaidHullLeakBelow;
            if (!ark || line <= 0f || ark.TotalCount == 0) return;
            if (ark.HealthFraction >= line)
            {
                _leakToasted = false;
                return;
            }
            if (Time.time < _nextLeak) return;
            _nextLeak = Time.time + _def.RaidHullLeakSeconds;
            var lost = Raiding ? _raid.Leak() : Element.None;
            if (lost != Element.None) _tallyDirty = true;
            else lost = _state.LeakOwed();
            if (lost == Element.None) return;
            ElementalCrystalEjector.ShedOntoSphere(ark.Position, HullHalfLength, ark.Position + Random.onUnitSphere,
                180f, lost, 1, 0f, "Ark");
            if (_leakToasted) return;
            _leakToasted = true;
            string pct = Mathf.RoundToInt(line * 100f).ToString();
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastHullLeaking,
                TemplateText.Fill(Lines.ToastHullLeakingSub, "line", pct));
        }

        // ── P1: the raid window (Shombith, 2026-11-16: "the ark park at each cell and give us time to steal") ──

        /// <summary>How long the Ark anchors at the cell it just reached: a Crystal Run's hostile or calm window, nothing
        /// for any other work (the way home, a job with a stop) or once the Ark has fallen.</summary>
        float RaidAnchorSeconds()
        {
            if (!_active || !Raiding || _fallen || !_def || GoalMet) return 0f;
            // Nothing to raid: sail through instead of waiting at an empty core.
            var ark = Ark;
            if (_def.RaidAnchorMinLife > 0 && ark && CellLife(CurrentCell, ark.Domain) < _def.RaidAnchorMinLife) return 0f;
            int cell = LegCell(out _);
            bool hostile = cell > 0 && LegHostile(cell - 1);
            float window = hostile ? _def.RaidAnchorHostileSeconds : _def.RaidAnchorCalmSeconds;
            // The job's own window for this cell, when it names one (Shombith's storyboard: 20 / 30 / 30 / 40 s).
            return Mathf.Max(0f, VoyageRaid.PerCell(_state?.Current?.CellAnchorSeconds, cell - 1, window));
        }

        /// <summary>The lifeforms in <paramref name="cell"/> the squad can take a crystal from now: creatures of another
        /// colour than <paramref name="squad"/> (the squad's own are not prey, as in <see cref="CountThreats"/>), and the
        /// cell's plants when the cell is held by another colour (plants are born in the holder's colour). A calm cell
        /// still held by the squad's colour holds nothing to steal, so the Ark sails through it. 0 for no cell.</summary>
        static int CellLife(Cell cell, Domains squad)
        {
            if (!cell) return 0;
            int live = 0;
            var fauna = cell.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
            {
                var f = fauna[i];
                if (f && f.Domain != squad && f.IsAlivePrey) live++;
            }
            var profile = cell.Config ? cell.Config.SpawnProfile : null;
            if (profile && profile.SupportedFloras != null && cell.ControllingDomain != squad)
                foreach (var f in profile.SupportedFloras)
                    if (f) live += cell.GetLiveFloraCount(f);
            return live;
        }

        void OnRaidAnchored(float seconds)
        {
            _anchorWarned = false;
            if (!_active) return;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Event, Lines.ToastAnchored,
                TemplateText.Fill(Lines.ToastAnchoredSub, "seconds", Mathf.CeilToInt(seconds).ToString()));
        }

        void OnRaidWeighed()
        {
            _anchorWarned = false;
            if (!_active || !Raiding) return;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Event, Lines.ToastWeighed);
        }

        /// <summary>Each frame of a Crystal Run: the horn before the anchored Ark sails, and every seat near the Ark banks
        /// what it carries.</summary>
        void TickRaidWindow()
        {
            if (!Raiding || _fallen || !_sea) return;
            // The goal is met: the Ark stops anchoring ("it shouldnt keep anchoring") and sails on at once.
            if (_sea.IsAnchored && GoalMet) _sea.WeighAnchor();
            if (_sea.IsAnchored && !_anchorWarned && _def && _sea.AnchorRemaining <= _def.RaidAnchorWarnSeconds)
            {
                _anchorWarned = true;
                VoyageToasts.PlayUi(_def.ArkHornSound);
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat,
                    TemplateText.Fill(Lines.ToastAnchorClosing, "seconds", Mathf.CeilToInt(_sea.AnchorRemaining).ToString()),
                    Lines.ToastAnchorClosingSub);
            }
            if (!CrystalsAtRisk || Time.unscaledTime < _nextBankCheck) return;
            _nextBankCheck = Time.unscaledTime + BankCheckSeconds;
            var ark = Ark;
            if (!ark) return;
            float r = _def ? _def.RaidBankRadius : 180f;
            BankIfNear(LocalVessel(), ark.Position, r, you: true);
            foreach (var w in _wingmates) BankIfNear(Alive(w), ark.Position, r, you: false);
        }

        void BankIfNear(IVesselStatus status, Vector3 arkAt, float radius, bool you)
        {
            if (status == null || _raid.CarriedBy(status.PlayerName) <= 0) return;
            var hull = status.Vessel?.Transform;
            if (!hull || (hull.position - arkAt).sqrMagnitude > radius * radius) return;
            // What is banked, by element, before the tally clears it: a friend tells the host each crystal (PARTY.md §4).
            var seat = _raid.SeatOf(status.PlayerName);
            var banked = seat != null ? (int[])seat.Carried.Clone() : null;
            int n = _raid.Bank(status.PlayerName);
            if (n <= 0) return;
            if (banked != null)
                foreach (var e in VoyageRaid.Elements)
                    for (int i = 0; i < banked[(int)e]; i++)
                        ReportToHost(VoyagePartyReport.ReportKind.Crystal, status.PlayerName, e, false);
            if (!_following) _tallyDirty = true;
            if (you)
            {
                var job = _state.Current;
                string goal = Mathf.CeilToInt(job?.Goal ?? 0f).ToString(), done = _raid.Total.ToString();
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Good,
                    TemplateText.Fill(Lines.ToastBanked, "count", n.ToString(), "done", done, "goal", goal),
                    TemplateText.Fill(Lines.ToastBankedSub, "count", n.ToString(), "done", done, "goal", goal));
            }
            CheckGoalToast();
        }

        // ── P6: a hit spills what a seat carries (Shombith, 2026-11-16) ─────

        /// <summary>A hull met a prism. A squad seat struck by a hostile danger prism (a creature's stinger, another
        /// domain's trap) spills everything it carries back into the sea as loose crystals anyone can take.</summary>
        void OnRaidHullContact(IVesselStatus status, Prism prism)
        {
            if (!_active || !Raiding || _fallen || !CrystalsAtRisk || status == null || !prism) return;
            if (!prism.prismProperties.IsDangerous || prism.Domain == status.Domain) return;
            string who = status.PlayerName;
            if (!IsSquadSeat(who) || _raid.CarriedBy(who) <= 0) return;
            float now = Time.time;
            if (_spillReadyAt.TryGetValue(who, out float ready) && now < ready) return;
            _spillReadyAt[who] = now + (_def ? _def.RaidSpillCooldownSeconds : 1.5f);

            var lost = _raid.Spill(who);
            int total = 0;
            foreach (var e in VoyageRaid.Elements)
            {
                int n = lost[(int)e];
                if (n <= 0) continue;
                total += n;
                ElementalCrystalEjector.Eject(status, e, n, Vector3.zero);
            }
            if (total <= 0) return;
            var local = LocalVessel();
            if (local == null || local.PlayerName != who) return;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastSpilled,
                TemplateText.Fill(Lines.ToastSpilledSub, "count", total.ToString()));
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
                if (_sea && _sea.IsAnchored)
                {
                    objective.Anchored = true;
                    objective.AnchorSeconds = _sea.AnchorRemaining;
                    objective.AnchorClosing = _def && _sea.AnchorRemaining <= _def.RaidAnchorWarnSeconds;
                }
                var you = LocalVessel();
                objective.Carried = you != null ? _raid.CarriedBy(you.PlayerName) : 0;
                objective.Leaking = Leaking(ark);
                objective.LeakLine = _def ? _def.RaidHullLeakBelow : 0f;
                objective.CanComplete = CanComplete;
                return true;
            }
            if (job is { IsReturn: true } && _state.AtSea)
            {
                objective.Goal = VoyageObjective.Kind.SailingHome;
                objective.To = PlaceName(job.To);
                objective.Leaking = Leaking(ark);
                objective.LeakLine = _def ? _def.RaidHullLeakBelow : 0f;
                objective.Breach = job.BreachHull;
                objective.Hostile = objective.Cell > 0 && LegHostile(objective.Cell - 1);
                return true;
            }
            return false;
        }

        /// <summary>The Ark's hull is below the leak line: the hold is losing crystals.</summary>
        bool Leaking(Ark ark) => ark && _def && _def.RaidHullLeakBelow > 0f && ark.TotalCount > 0
                                 && ark.HealthFraction < _def.RaidHullLeakBelow;

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
            bool forfeit = _def && _def.RaidMissForfeits;
            var r = VoyageRaid.Arrive(_state, Content, _raid, forfeit);
            if (r == null) return;
            bool missed = r.Outcome != JobOutcome.Succeeded;
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

            if (missed && Lines != null)
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat,
                    TemplateText.Fill(Lines.ToastGoalMissed, "done", _raid.Total.ToString(), "goal", board.Goal.ToString()),
                    forfeit ? Lines.ToastGoalMissedSub : string.Empty);

            if (!_following && missed && forfeit)
            {
                // Missed (Shombith's storyboard, 2026-11-17): Venture further keeps the run going, harder (the goal raised
                // and one more cell), and this run's haul comes along, owed and at stake again. Finish sails home without it.
                var kept = new VoyageRaidTally();
                kept.Absorb(_raid);
                var next = VoyageRaid.Venture(_state, Content, job);
                if (next != null)
                {
                    board.NextGoal = Mathf.CeilToInt(next.Goal);
                    board.NextCells = next.Cells;
                    board.Venture = () =>
                    {
                        VoyageRaid.Owe(_state, kept);
                        SailOn(next);
                    };
                }
                board.Finish = SailHome;
            }
            else if (!_following)
            {
                var next = VoyageRaid.Venture(_state, Content, job);
                if (next != null)
                {
                    board.NextGoal = Mathf.CeilToInt(next.Goal);
                    board.NextCells = next.Cells;
                    board.Venture = () => SailOn(next);
                }
                // Goal met (Shombith 2026-11-17): finish right here and bank now, or escort the Ark home for a bigger pay.
                float bonus = _def ? _def.HomeEscortRewardScale : 1f;
                if (!missed && bonus > 1f && !_state.IsHome())
                {
                    board.FinishHere = () => FinishHere();
                    board.HomeRewardScale = bonus;
                    board.Finish = () =>
                    {
                        _state.HomeRewardScale = bonus;
                        SaveState();
                        SailHome();
                    };
                }
                else board.Finish = SailHome;
            }
            VoyageScoreboard.Show(board);
        }

        /// <summary>One row per seat, the pilot's first, with what each stole and felled on the leg: this machine's AI
        /// wingmates, then the party's other humans, then any seat the host's count names that is not here (on a friend's
        /// machine, the host's AI wingmates).</summary>
        void AddRows(VoyageScoreboard board) => AddRows(board, _raid);

        /// <summary>The whole voyage's rows: every leg sailed, and the one under way.</summary>
        void AddVoyageRows(VoyageScoreboard board)
        {
            var all = new VoyageRaidTally();
            all.Absorb(_voyageRaid);
            all.Absorb(_raid);
            AddRows(board, all);
        }

        void AddRows(VoyageScoreboard board, VoyageRaidTally tally)
        {
            var named = new HashSet<string>();
            void Row(IVesselStatus status, bool you)
            {
                if (status == null || string.IsNullOrEmpty(status.PlayerName) || !named.Add(status.PlayerName)) return;
                var seat = tally.SeatOf(status.PlayerName);
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
            foreach (var seat in tally.Seats.ToArray())
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

        /// <summary>Finish here: bank everything owed at this stop, as it is, and end the voyage (the Ark is home again).</summary>
        void FinishHere()
        {
            if (!_active || _state == null || _state.AtSea) return;
            Dock(here: true);
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
            AddVoyageRows(board);
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
            AddVoyageRows(board);
            // Try again: the same work from wherever the fall left the crew, once the sea has ended.
            string template = lost?.TemplateId;
            if (!_following && lost is { CountsUnderway: true } && !string.IsNullOrEmpty(template))
                board.TryAgain = () => VoyageHarbour.Current?.TryAgain(template);
            // Back to Voyages ends what is left of the voyage, so the scene returns to the menu (VoyageScene.AfterChoiceAsync
            // waits for the harbour to stop being under way; a board that only closed left the pilot floating by a wreck).
            if (!_following) board.Close = () => GiveUp("back to Voyages after the Ark fell");
            VoyageScoreboard.Show(board);
        }

        void EndRaid()
        {
            HookRaid(false);
            _raid.Clear();
            _voyageRaid.Clear();
            _legIndex.Clear();
            _alarms?.Unhook();
            if (_attackerMarks) Destroy(_attackerMarks.gameObject);
            _attackerMarks = null;
            if (ReferenceEquals(VoyageObjective.Source, this)) VoyageObjective.Source = null;
            PauseMenuAction.Release(this);
        }
    }
}

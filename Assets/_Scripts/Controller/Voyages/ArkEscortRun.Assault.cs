using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// ASSAULTS (Shombith, 2026-11-16, Milestone 1: "the cell faunas should attack the ark and right now the player does not
    /// know what to do"). The feeding lure alone (<see cref="TickChaos"/>) sends 5-12% of a hostile world's goal picks to the
    /// Ark at the difficulties people play, so the Ark is grazed now and then and nothing ever reads as an attack. An
    /// assault is the same lure, turned up for a while and announced (<see cref="ArkAssaultDriver"/>): in a hostile,
    /// unpacified world the fauna turn on the Ark in waves, and the squad drives each one off by killing attackers near
    /// the Ark, or rides it out and pays in plates. Pacifying the world ends its assaults (held worlds are not lured).
    ///
    /// <para>The HUD reads the escort through <see cref="ArkVoyageSignals"/> (the same hook a job voyage raises) or
    /// <see cref="Status"/>.</para>
    /// </summary>
    public sealed partial class ArkEscortRun : IArkStatusSource
    {
        ArkAssaultDriver _assaultDriver;
        int _threatsNear;   // from the run's 1 Hz sample (the HUD polls Status every frame; it never walks the fauna)

        ArkAssaultDriver Assaults => _assaultDriver ??= new ArkAssaultDriver(this, (line, mood) => VoyageToasts.Show(_sea, mood, line));

        /// <summary>The escort as it stands now, for the HUD's objective box.</summary>
        public ArkEscortStatus Status
        {
            get
            {
                var s = new ArkEscortStatus { Active = _active, Difficulty = Difficulty, Leg = _leg, Outcome = _report?.Result ?? default };
                if (_escort)
                {
                    s.Legs = _escort.Legs.Count;
                    if (_leg >= 0 && _leg < _escort.Legs.Count) s.WorldName = WorldNameFor(_leg);
                    if (_leg + 1 >= 0 && _leg + 1 < _escort.Legs.Count) s.NextWorldName = WorldNameFor(_leg + 1);
                    s.BreachLine = _escort.TierFor(_difficulty)?.BreachHull ?? 0f;
                }
                s.WorldName ??= string.Empty;
                s.NextWorldName ??= string.Empty;

                var cell = CurrentCell;
                var ark = Ark;
                var r = LegReportNow();
                s.WorldHostile = r is { Hostile: true };
                s.WorldPacified = r is { Pacified: true };
                s.DistanceToCore = ark && cell ? Vector3.Distance(ark.Position, cell.transform.position) : -1f;
                if (ark)
                {
                    s.InsideWorld = ArkInsideWorld(cell, ark.Position);
                    s.Hull = ark.HealthFraction;
                    s.HullAlive = ark.AliveCount;
                    s.HullTotal = ark.TotalCount;
                    s.Healing = ark.RegrowAllowed;
                }
                s.ThreatsNear = _active ? _threatsNear : 0;
                Assaults.Fill(ref s);
                return s;
            }
        }

        ArkEscortReport.LegReport LegReportNow() =>
            _report != null && _leg >= 0 && _leg < _report.LegReports.Count ? _report.LegReports[_leg] : null;

        string WorldNameFor(int leg) =>
            _report != null && leg < _report.LegReports.Count && !string.IsNullOrEmpty(_report.LegReports[leg].Name)
                ? _report.LegReports[leg].Name
                : _escort.Legs[leg].Name;

        /// <summary>True when <paramref name="at"/> is inside <paramref name="cell"/>'s membrane.</summary>
        internal static bool ArkInsideWorld(Cell cell, Vector3 at)
        {
            if (!cell) return false;
            float membrane = cell.MembraneRadius > 1f ? cell.MembraneRadius : 1200f;
            return (at - cell.transform.position).sqrMagnitude <= membrane * membrane;
        }

        // ── Hooks the run calls ──────────────────────────────────────────────

        /// <summary>Set sail (from <see cref="BeginMetrics"/>, on the run's own copy of the escort).</summary>
        void BeginAssaults()
        {
            _threatsNear = 0;
            Assaults.Begin(_escort, _escort ? _escort.TierFor(_difficulty) : null);
        }

        /// <summary>The Ark is under way (from <see cref="OnUnderway"/>).</summary>
        void SignalUnderway() => Assaults.Underway();

        /// <summary>The Ark is bound for a new world (from <see cref="TrackLeg"/>).</summary>
        void OnAssaultLegChanged() => Assaults.NewWorld(_leg >= 0 && _escort && _leg < _escort.Legs.Count ? WorldNameFor(_leg) : null);

        /// <summary>Once a second (from <see cref="TickChaos"/>, before the lure is set).</summary>
        void TickAssault(Ark ark)
        {
            if (!_active || !ark) return;
            _threatsNear = _threatScratch.Count;   // Sample() just filled it with the threats inside the guard radius
            bool inside = ArkInsideWorld(CurrentCell, ark.Position);
            var r = LegReportNow();
            bool canAssault = !_fallen && !_survived && r is { Hostile: true, Pacified: false };
            Assaults.Tick(ark, _leg, inside, canAssault);
        }

        /// <summary>The lure on <paramref name="leg"/>'s world: the assault's share on the Ark's own world while one is on.</summary>
        float AssaultLure(int leg, float baseShare) => leg == _leg ? Assaults.Lure(baseShare) : baseShare;

        /// <summary>A hull plate was lost (from <see cref="OnHullPrismLost"/>).</summary>
        void NoteAssaultPlateLost() => Assaults.PlateLost();

        /// <summary>A creature of <paramref name="leg"/>'s world died near the Ark (from <see cref="OnFaunaDied"/>).</summary>
        void NoteAssaultKill(int leg)
        {
            if (leg == _leg) Assaults.Kill();
        }

        /// <summary>The squad pacified a world (from <see cref="TryPacify"/>).</summary>
        void SignalPacified() => Assaults.Pacified();

        /// <summary>The escort is over (from <see cref="FinishMetrics"/>, with the outcome set).</summary>
        void EndAssaults()
        {
            var clock = Assaults.Clock;
            _report.AssaultsRepelled = clock.Repelled;
            _report.Assaults = clock.Repelled + clock.Passed;
            Assaults.End();
        }
    }
}

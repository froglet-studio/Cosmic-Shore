using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What happened on one Ark escort (<see cref="ArkEscortRun"/>): each world's hull and threat, and how it ended.
    /// Serializable with JsonUtility, so the escort sim writes it as one JSON line per run (lists of small classes, no
    /// dictionaries). <c>Tools/Build/ark_escort_report.py</c> reads it.
    /// </summary>
    [System.Serializable]
    public sealed class ArkEscortReport
    {
        public enum Outcome
        {
            None = 0,
            Survived = 1,
            Fallen = 2,
            Abandoned = 3,
        }

        /// <summary>One seat's escort proximity: samples within 300 u and 900 u of the Ark, of all samples.</summary>
        [System.Serializable]
        public sealed class SeatNear
        {
            public int Seat;
            public string Vessel;
            public string PlayerName;
            public int Near300;
            public int Near900;
            public int Samples;
        }

        /// <summary>One seat's round stats over a leg (deltas) or the run (totals), plus its kills split by the Ark.</summary>
        [System.Serializable]
        public sealed class SeatStats
        {
            public int Seat;
            public string Vessel;
            public string PlayerName;
            public int Kills;
            public int KillsOnArk;
            public int LifeformsKilled;
            public float VolumeCreated;
            public int BlocksCreated;
            public float HostileVolumeDestroyed;
        }

        [System.Serializable]
        public sealed class NameCount
        {
            public string Name;
            public int Count;
        }

        /// <summary>A domain's volume in a world at the leg's close (whole-cell and the control volume that decides it).</summary>
        [System.Serializable]
        public sealed class TeamVolume
        {
            public string Domain;
            public float Volume;
            public float ControlVolume;
        }

        /// <summary>A named override echoed back (<see cref="ArkEscortParams"/>).</summary>
        [System.Serializable]
        public sealed class Param
        {
            public string Key;
            public float Value;
            public bool Applied;
            public string Note;
        }

        [System.Serializable]
        public sealed class LegReport
        {
            public int Leg;
            public string Name;
            public string World;
            public float Population;
            public bool Hostile;
            public bool Reached;
            public float EnteredAt;
            public float ExitedAt;
            public int HullAtEntry;
            public int HullMin;
            public int HullAtExit;
            public int PeakThreats;
            public float ThreatSeconds;
            public int Kills;
            public int EscortSamples;
            public int EscortNear;

            // ── Since the cell-aware pace and plate eating ──
            /// <summary>Attributed kills of creatures that had been threatening the Ark (within 900 u) in the last 10 s.</summary>
            public int KillsOnArk;
            public int KillsElsewhere;
            /// <summary>Hull plates lost in this world, by the tier their slot was laid in.</summary>
            public int PlatesLost;
            public int PlatesLostPlain;
            public int PlatesLostShielded;
            public int PlatesLostSuper;
            public int PlatesByCreature;
            public int PlatesRegrown;
            /// <summary>Seconds (1 Hz) the reactor was allowed to regrow: the Ark inside a held world's nucleus.</summary>
            public float RegrowSeconds;
            public List<NameCount> PlateKillers = new();
            public int PlainAtExit;
            public int ShieldedAtExit;
            public int SuperAtExit;
            /// <summary>Seconds (game time, 1 Hz samples) with the Ark inside a cell membrane, and outside.</summary>
            public float InsideSeconds;
            public float OutsideSeconds;
            public float MeanSpeed;
            public float SpeedSum;
            public int SpeedSamples;
            public List<SeatNear> Seats = new();
            public List<SeatStats> SeatStats = new();
            public string ControllerAtExit;
            public List<TeamVolume> Territory = new();

            /// <summary>The squad pacified this world (its share of the control volume reached the escort's pacifyShare).</summary>
            public bool Pacified;
            /// <summary>When (Time.time, like EnteredAt; -1 never), and the squad's share then.</summary>
            public float PacifiedAt = -1f;
            public float PacifyShare;
            /// <summary>Pacified before the Ark entered this world (a cultivator raced ahead).</summary>
            public bool PacifiedAhead;
            /// <summary>The squad's share of this world's control volume at the leg's close.</summary>
            public float SquadShareAtExit;
            /// <summary>Live fauna re-coloured into the squad's domain at pacification.</summary>
            public int FaunaTurned;
            /// <summary>Seconds (1 Hz samples) a squad seat spent inside this world's nucleus before it was pacified, and
            /// the largest squad control volume and share seen (the pacify gates' inputs, for tuning).</summary>
            public float NucleusSeconds;
            public float PeakSquadControl;
            public float PeakShare;
            /// <summary>Hull prisms lost to the squad's own fire in this world.</summary>
            public int FriendlyFire;

            // The fauna threats (Docs/Voyages/ARK_ESCORT.md; briefs/fauna_threats_DONE.md), per world.
            /// <summary>Plate latches taken by forager boids (Scarab Reef's piranha) while the Ark was in this world.</summary>
            public int Latches;
            /// <summary>Worm colony lunges at vessels while the Ark was in this world.</summary>
            public int WormLunges;
            /// <summary>Bites substrate riders (leeches) landed on the Ark's plates in this world.</summary>
            public int ArkSips;
            /// <summary>When this world's second fauna wave was released (Time.time; -1 never / no staged profile).</summary>
            public float WaveReleasedAt = -1f;
        }

        [System.Serializable]
        public struct Sample
        {
            public float T;
            public int Leg;
            public int Hull;
            public int Threats;
            public float Speed;
            public bool Inside;
        }

        public int Difficulty;
        public string Squad;
        public int Legs;
        public Outcome Result;
        public int FallenLeg = -1;
        /// <summary>The Ark fell to the tier's breach line (overrun), not to its last plate.</summary>
        public bool Overrun;
        public int ReachedLegs;
        public int HullTotal;
        /// <summary>Creatures killed by an active force (the squad, mostly) in the escort's worlds.</summary>
        public int Kills;
        public int KillsOnArk;
        public int KillsElsewhere;
        /// <summary>Ark hull prisms destroyed by something other than a creature: the squad's own fire.</summary>
        public int FriendlyFire;
        /// <summary>Who did it, as <c>"destroyer:count"</c> (the destroyer name stamped on the prism), most first.</summary>
        public List<string> FriendlyFireBy = new();
        /// <summary>One line per escort seat from its role (<c>IArkEscortRole.Notes</c>): what it did, in counters.</summary>
        public List<string> RoleNotes = new();
        /// <summary>AI wingmates that flew, and escort pilots armed (the local seat's included in the sim).</summary>
        public int Wingmates;
        public int Pilots;
        public float HullMinFraction = 1f;
        public float StartedAt;
        public float UnderwayAt;
        public float EndedAt;
        public int PlatesLost;
        public int PlatesLostShielded;
        public int PlatesLostSuper;
        public int PlatesByCreature;
        public int PlatesRegrown;
        public float RegrowSeconds;
        public float InsideSeconds;
        public float OutsideSeconds;
        public float MeanArkSpeed;
        /// <summary>The breach line the tier sailed with (0 = only the true fall).</summary>
        public float BreachHull;
        /// <summary>The share of a world's control volume that pacifies it (0 = off), and how many worlds were pacified.</summary>
        public float PacifyShare;
        public float PacifyMinVolume;
        /// <summary>The voyage's roll: its seed, its worlds in order (variant names), its modifiers by name.</summary>
        public int VoyageSeed;
        public List<string> Worlds = new();
        public List<string> Modifiers = new();
        /// <summary>Where on the galactic map the escort set out from and was bound for (location ids, empty with no
        /// course; Docs/Voyages/ARK_ESCORT.md §5), and the course measured on the real HyperSea (light years, cells,
        /// intensity I1-I4; 0 when the content is not charted).</summary>
        public string From;
        public string To;
        public float CourseLightYears;
        public int CourseCells;
        public int CourseIntensity;
        public float PacifyNucleusSeconds;
        public int PacifiedLegs;
        /// <summary>The fauna threats over the run: plate latches, worm lunges, leech bites on the Ark, worlds whose
        /// second wave was released, and the share of the membrane radius that released it.</summary>
        public int Latches;
        public int WormLunges;
        public int ArkSips;
        public int WavesReleased;
        public float SecondWaveAt;
        public List<SeatStats> RunSeats = new();
        public List<Param> Params = new();
        /// <summary>Every override key pattern this build accepts (<see cref="ArkEscortParams"/>).</summary>
        public List<string> ParamKeys = new();
        public List<LegReport> LegReports = new();
        public List<Sample> Samples = new();

        /// <summary>One line for the log: difficulty, squad, result, and each world's hull (entry > lowest > exit).</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"d{Difficulty} {Squad}: {Result}");
            if (!string.IsNullOrEmpty(To)) sb.Append($" ({From} > {To}, {CourseLightYears:0} ly)");
            if (Result == Outcome.Fallen) sb.Append($"{(Overrun ? " (overrun)" : "")} in leg {FallenLeg + 1}");
            sb.Append($", hull min {HullMinFraction:P0} of {HullTotal}, {EndedAt - UnderwayAt:0}s under way " +
                      $"({InsideSeconds:0}s in cells, {OutsideSeconds:0}s out, mean {MeanArkSpeed:0.0} u/s), " +
                      $"plates lost {PlatesLost} ({PlatesLostShielded} shielded, {PlatesLostSuper} super, " +
                      $"{FriendlyFire} to friendly fire{(FriendlyFireBy.Count > 0 ? " [" + string.Join(", ", FriendlyFireBy) + "]" : "")}), {Kills} kills ({KillsOnArk} on the Ark / {KillsElsewhere} elsewhere), " +
                      $"{PacifiedLegs} worlds pacified, {Wingmates} wingmates, {Pilots} escort pilots. Legs:");
            foreach (var l in LegReports)
            {
                if (!l.Reached && l.EnteredAt <= 0f) continue;
                sb.Append($" [{l.Leg + 1} {l.Name}{(l.Hostile ? " hostile" : "")} x{l.Population:0.##}: " +
                          $"{l.HullAtEntry}>{l.HullMin}>{l.HullAtExit}, threats peak {l.PeakThreats}, kills {l.Kills}, " +
                          $"escorts near {(l.EscortSamples > 0 ? (float)l.EscortNear / l.EscortSamples : 0f):0.0}, " +
                          $"plates -{l.PlatesLost}/+{l.PlatesRegrown}, " +
                          $"latches {l.Latches}, lunges {l.WormLunges}, sips {l.ArkSips}" +
                          $"{(l.WaveReleasedAt >= 0f ? $", wave at {l.WaveReleasedAt - l.EnteredAt:0}s" : "")}, " +
                          $"{l.ExitedAt - l.EnteredAt:0}s]");
            }
            foreach (var note in RoleNotes) sb.Append(" | ").Append(note);
            return sb.ToString();
        }
    }
}

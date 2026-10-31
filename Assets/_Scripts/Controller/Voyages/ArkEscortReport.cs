using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What happened on one Ark escort (<see cref="ArkEscortRun"/>): each world's hull and threat, and how it ended.
    /// Serializable with JsonUtility, so the escort sim writes it as one JSON line per run.
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
            /// <summary>Hull prisms lost to the squad's own fire in this world.</summary>
            public int FriendlyFire;
        }

        [System.Serializable]
        public struct Sample
        {
            public float T;
            public int Leg;
            public int Hull;
            public int Threats;
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
        public List<LegReport> LegReports = new();
        public List<Sample> Samples = new();

        /// <summary>One line for the log: difficulty, squad, result, and each world's hull (entry > lowest > exit).</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"d{Difficulty} {Squad}: {Result}");
            if (Result == Outcome.Fallen) sb.Append($"{(Overrun ? " (overrun)" : "")} in leg {FallenLeg + 1}");
            sb.Append($", hull min {HullMinFraction:P0} of {HullTotal}, {EndedAt - UnderwayAt:0}s under way, " +
                      $"{Wingmates} wingmates, {Pilots} escort pilots, {Kills} kills, {FriendlyFire} hull plates to friendly fire" +
                      $"{(FriendlyFireBy.Count > 0 ? " (" + string.Join(", ", FriendlyFireBy) + ")" : "")}. Legs:");
            foreach (var l in LegReports)
            {
                if (!l.Reached && l.EnteredAt <= 0f) continue;
                sb.Append($" [{l.Leg + 1} {l.Name}{(l.Hostile ? " hostile" : "")} x{l.Population:0.##}: " +
                          $"{l.HullAtEntry}>{l.HullMin}>{l.HullAtExit}, threats peak {l.PeakThreats}, kills {l.Kills}, " +
                          $"escorts near {(l.EscortSamples > 0 ? (float)l.EscortNear / l.EscortSamples : 0f):0.0}, " +
                          $"{l.ExitedAt - l.EnteredAt:0}s]");
            }
            foreach (var note in RoleNotes) sb.Append(" | ").Append(note);
            return sb.ToString();
        }
    }
}

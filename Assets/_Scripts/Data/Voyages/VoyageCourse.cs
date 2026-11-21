using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>What a course message says (Docs/Voyages/SLICE.md §4 step 5, Docs/Voyages/PARTY.md).</summary>
    public enum VoyageCourseKind
    {
        /// <summary>The host set out: every friend deploys their own seat and follows.</summary>
        Embark = 0,
        /// <summary>The host took a job: the galaxy as the host knows it, the job and the leg's worlds.</summary>
        Leg = 1,
        /// <summary>The host's voyage ended (<see cref="VoyageCourse.Outcome"/> says how): every friend's ends with it.</summary>
        End = 2,
        /// <summary>The host set sail on an Ark escort: every friend sails the same escort (difficulty, seed, home pose)
        /// in the seat the host gave them; AI fills the seats nobody took.</summary>
        EscortSail = 3,
        /// <summary>The host's escort ended, with its outcome: every friend's escort ends with the same one.</summary>
        EscortEnd = 4,
        /// <summary>The party's Crystal Run count as the host keeps it (<see cref="VoyageCourse.Tally"/>): every friend's
        /// objective box and scoreboard read it.</summary>
        Tally = 5,
    }

    /// <summary>How the host's voyage ended (<see cref="VoyageCourseKind.End"/>).</summary>
    public enum VoyageCourseEnd
    {
        /// <summary>Given up or left: a friend anchors where they are.</summary>
        Ended = 0,
        /// <summary>The host's Ark fell: everything aboard every Ark is lost.</summary>
        Fallen = 1,
        /// <summary>The host's Ark docked home: every friend sailing home docks with it and banks what they carry.</summary>
        Docked = 2,
    }

    /// <summary>
    /// The host's voyage, sent to the party so every friend sails the same course (SLICE.md §5: each machine runs its
    /// own Ark, fauna and finds; the course, the jobs, the count and the news are the host's). Plain fields only, for
    /// JsonUtility.
    /// </summary>
    [Serializable]
    public class VoyageCourse
    {
        public VoyageCourseKind Kind;
        public VoyageState State;
        public VoyageJob Job;
        public List<int> Route = new();

        /// <summary>The voyage's seed: the corridor's headings (and the escort's roll and hostile worlds) come from it, so
        /// every peer stands the same worlds in the same places.</summary>
        public int Seed;

        /// <summary>True when <see cref="HomeX"/>.. carry the host's home pose (the corridor's origin and heading).</summary>
        public bool HasHome;
        public float HomeX, HomeY, HomeZ, HomeQx, HomeQy, HomeQz, HomeQw;

        /// <summary>Client ids of the party's humans in seat order (seat 0 first, -1 = an AI seat).</summary>
        public List<long> SeatClients = new();

        /// <summary>How many legs the host has taken this voyage (the Ark's pose is only matched within one leg).</summary>
        public int LegSerial;

        // ── Ark escort ──
        /// <summary>EscortSail: the escort's difficulty (1-9).</summary>
        public int Difficulty;
        /// <summary>EscortEnd: the host's outcome (ArkEscortReport.Outcome as an int), the leg it fell in, overrun.</summary>
        public int Result;
        public int FallenLeg = -1;
        public bool Overrun;

        // ── End ──
        public VoyageCourseEnd Outcome;

        // ── Tally ──
        /// <summary>Tally: every seat's take on this leg (the host's count; names are the seats' player names).</summary>
        public List<VoyageRaidTally.Seat> Tally = new();

        /// <summary>The home pose as (position, rotation x/y/z/w); identity rotation when unset.</summary>
        public void SetHome(float x, float y, float z, float qx, float qy, float qz, float qw)
        {
            HasHome = true;
            HomeX = x; HomeY = y; HomeZ = z;
            HomeQx = qx; HomeQy = qy; HomeQz = qz; HomeQw = qw;
        }

        /// <summary>The client id in <paramref name="seat"/>, or -1 (an AI seat, or none).</summary>
        public long ClientAt(int seat) => SeatClients != null && seat >= 0 && seat < SeatClients.Count ? SeatClients[seat] : -1;

        /// <summary>The seat <paramref name="clientId"/> holds, or -1.</summary>
        public int SeatOf(long clientId)
        {
            if (SeatClients == null) return -1;
            for (int i = 0; i < SeatClients.Count; i++)
                if (SeatClients[i] == clientId) return i;
            return -1;
        }
    }

    /// <summary>
    /// What a friend tells the host (Docs/Voyages/PARTY.md): the seat they picked in the lobby (their pilot, element and
    /// artifact never reach the host otherwise), and on a Crystal Run each crystal and creature they took in their own
    /// world. Plain fields only, for JsonUtility.
    /// </summary>
    [Serializable]
    public class VoyagePartyReport
    {
        public enum ReportKind
        {
            /// <summary>The friend's seat in the host's lobby (<see cref="Slot"/>, <see cref="PilotName"/>).</summary>
            Seat = 0,
            /// <summary>A crystal of <see cref="Element"/> came to <see cref="PlayerName"/>.</summary>
            Crystal = 1,
            /// <summary>A creature fell to <see cref="PlayerName"/> (<see cref="NearArk"/>: within the Ark's guard).</summary>
            Kill = 2,
        }

        public ReportKind Kind;
        public string PlayerName = string.Empty;
        /// <summary>The friend's vessel class (an int: <c>VesselClassType</c>).</summary>
        public int Vessel;

        // Seat
        public int SlotVessel;
        public int SlotElement;
        public string PilotId = string.Empty;
        public string PilotName = string.Empty;
        public string ArtifactId = string.Empty;

        // Crystal, Kill
        public int Element;
        public bool NearArk;
        /// <summary>The leg the friend counted it on (a report from the last leg is dropped).</summary>
        public int LegSerial;
    }
}

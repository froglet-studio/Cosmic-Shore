using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Vessel ROLES and ARCHETYPES on voyages (Docs/Voyages/ARCHETYPES.md). Each vessel's archetypes, the counter graph
    /// and the role and archetype words are the FLEET's (<see cref="VesselArchetypesSO"/>, Resources/Vessels/VesselArchetypes,
    /// the table the Hangar and the ecology page read too); this asset holds only what voyages add: the three archetype
    /// seats each voyage type and each named voyage asks for, the crews a named voyage meets, the starters, what squad
    /// fit pays, and the voyage pages' words. An archetype never changes a vessel's kit.
    /// <para>Written by <c>Tools/Build/author_voyage_archetypes.py</c> (its <c>--check</c> is the drift gate) into
    /// <c>Resources/Voyages/VoyageArchetypes</c>. The tables are the generator's; the words under "Words" are a person's
    /// and survive a re-run.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageArchetypes", menuName = "ScriptableObjects/Voyages/Voyage Archetypes")]
    public class VoyageArchetypesSO : ScriptableObject
    {
        /// <summary>Where the shipped table lives, under Resources.</summary>
        public const string ResourcePath = "Voyages/VoyageArchetypes";

        static VoyageArchetypesSO s_loaded;

        /// <summary>The shipped table (<c>Resources/Voyages/VoyageArchetypes</c>), or null when the asset is missing.</summary>
        public static VoyageArchetypesSO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VoyageArchetypesSO>(ResourcePath);
            return s_loaded;
        }

        /// <summary>One role as the Vessels floor shows it, from the fleet's table: its name, its goal, and the vessels
        /// whose primary archetype is one of its three, in the fleet table's order.</summary>
        public sealed class RoleEntry
        {
            public VesselRole Role;
            public string Name;
            public string Line;
            public readonly List<VesselClassType> Vessels = new();
        }

        /// <summary>One archetype's words, from the fleet's table.</summary>
        public sealed class ArchetypeEntry
        {
            public VesselArchetype Archetype;
            public string Name;
            public string Line;
        }

        /// <summary>The three seats a voyage type asks for, and why each one.</summary>
        [Serializable]
        public class Ask
        {
            [Tooltip("The voyage's job template id (job.salvage.drift, escort, job.bounty.crew, job.defence.aid). A job with no " +
                     "row asks for nothing: the Crystal Run (job.raid.crystals) has none and shows no ask or fit.")]
            public string Job;
            public List<VesselArchetype> Seats = new();
            [Tooltip("Why each seat, one line per seat in seat order (the giver's reason).")]
            public List<string> Reasons = new();
        }

        /// <summary>A named voyage's own ask: the type's seats with one swapped for a counter of the crew it meets.</summary>
        [Serializable]
        public class VoyageAsk
        {
            [Tooltip("The named voyage's id (voyage_slice.json voyages[].id).")]
            public string Voyage;
            public List<VesselArchetype> Seats = new();
            [Tooltip("The crew the voyage meets, as players read it (a Bounty's rival, a Hold's retake crew).")]
            public string Crew;
            [Tooltip("The crew's hulls. A Hold the Core voyage's retake crew comes in these (VoyageRun.Core).")]
            public List<VesselClassType> CrewHulls = new();
            [Tooltip("The crew's lead archetype: its first hull's primary.")]
            public VesselArchetype Threat;
            [Tooltip("The seat the voyage asks for to counter it.")]
            public VesselArchetype Countered;
        }

        [Header("Tables (written by Tools/Build/author_voyage_archetypes.py)")]
        [SerializeField] List<Ask> asks = new();
        [SerializeField] List<VoyageAsk> voyageAsks = new();
        [SerializeField, Tooltip("The starter vessels voyages are built around.")]
        List<VesselClassType> starters = new();
        [SerializeField, Tooltip("What squad fit adds to a voyage's pay, per point of fit (0.1 = +10% a point, +30% at 3 of 3). " +
                                 "0 turns fit pay off.")]
        float fitPayPerPoint = 0.1f;

        [Header("Words")]
        [SerializeField, Tooltip("Over the floor: who the seat is. Tokens {seat} (its role) and {who}.")]
        string seatLine = "{seat}  ·  {who}  ·  choose a vessel";
        [SerializeField, Tooltip("{who} on your own seat.")]
        string whoYou = "you";
        [SerializeField, Tooltip("{who} on a wingmate's seat.")]
        string whoAi = "flown by the AI";
        [SerializeField, Tooltip("Top right, before the asked archetypes' tags.")]
        string wantedLabel = "WANTED";
        [SerializeField, Tooltip("Over the role row when the voyage asks for one of the role's archetypes. Token {role}.")]
        string wantedLine = "{role}s are wanted on this voyage";
        [SerializeField, Tooltip("Under a vessel the AI can fly, after the archetype's line.")]
        string aiCanFly = "The AI can fly it.";
        [SerializeField, Tooltip("Under a vessel the AI cannot fly when Vessels opens from an AI seat.")]
        string needsPerson = "Needs a person in the seat.";
        [SerializeField, Tooltip("On the floor when none of the role's vessels is in your hangar. Token {role}.")]
        string noneOwned = "No {role} in your hangar yet.";
        [SerializeField, Tooltip("At the foot when the pick changes the squad's fit. Tokens {from} and {to}.")]
        string fitChange = "Squad fit {from} to {to} of 3";
        [SerializeField, Tooltip("At the foot when the pick leaves the squad's fit as it is. Token {fit}.")]
        string fitSame = "Squad fit {fit} of 3";
        [SerializeField, Tooltip("The giver's reason when a named voyage meets a crew. Tokens {crew}, {threat} and {counter}.")]
        string counterLine = "{crew} fly {threat}s. Bring a {counter}.";
        [SerializeField, Tooltip("The call to action: put the picked vessel in the seat.")]
        string take = "TAKE THE VESSEL";
        [SerializeField, Tooltip("The keycap on Take the vessel.")]
        string takeKey = "A";
        [SerializeField, Tooltip("Back to Your Ark without a change.")]
        string back = "BACK";
        [SerializeField, Tooltip("The keycap on Back.")]
        string backKey = "B";
        [SerializeField, Tooltip("The keycaps over the role row's ends (the shoulders step the role).")]
        string prevRoleKey = "LB";
        [SerializeField, Tooltip("The keycap at the role row's right end.")]
        string nextRoleKey = "RB";

        /// <summary>The fleet's table every vessel fact here is read from (null when it is missing).</summary>
        static VesselArchetypesSO Fleet => VesselArchetypesSO.Load();

        /// <summary>The three roles in order, built from the fleet's table.</summary>
        public IReadOnlyList<RoleEntry> Roles
        {
            get
            {
                var list = new List<RoleEntry>(3);
                var fleet = Fleet;
                foreach (VesselRole r in Enum.GetValues(typeof(VesselRole)))
                    if (r != VesselRole.None) list.Add(EntryOf(r, fleet));
                return list;
            }
        }
        public IReadOnlyList<Ask> Asks => asks;
        public IReadOnlyList<VoyageAsk> VoyageAsks => voyageAsks;
        public IReadOnlyList<VesselClassType> Starters => starters;
        public float FitPayPerPoint => fitPayPerPoint;
        public string SeatLine => seatLine;
        public string WhoYou => whoYou;
        public string WhoAi => whoAi;
        public string WantedLabel => wantedLabel;
        public string WantedLine => wantedLine;
        public string AiCanFly => aiCanFly;
        public string NeedsPerson => needsPerson;
        public string NoneOwned => noneOwned;
        public string FitChange => fitChange;
        public string FitSame => fitSame;
        public string CounterLine => counterLine;
        public string Take => take;
        public string TakeKey => takeKey;
        public string Back => back;
        public string BackKey => backKey;
        public string PrevRoleKey => prevRoleKey;
        public string NextRoleKey => nextRoleKey;

        // ── Vessels ─────────────────────────────────────────────────────────

        /// <summary><paramref name="vessel"/>'s archetypes, primary first (the fleet's; empty for a vessel with none).</summary>
        public IReadOnlyList<VesselArchetype> ArchetypesOf(VesselClassType vessel) =>
            Fleet is { } fleet ? fleet.ArchetypesOf(vessel) : Array.Empty<VesselArchetype>();

        /// <summary><paramref name="vessel"/>'s primary archetype, or None.</summary>
        public VesselArchetype PrimaryOf(VesselClassType vessel)
        {
            var list = ArchetypesOf(vessel);
            return list.Count > 0 ? list[0] : VesselArchetype.None;
        }

        /// <summary>The role of <paramref name="vessel"/>'s primary archetype, or None.</summary>
        public VesselRole RoleOf(VesselClassType vessel) => VesselArchetypes.RoleOf(PrimaryOf(vessel));

        // ── Words (the fleet's) ─────────────────────────────────────────────

        public RoleEntry EntryOf(VesselRole role) => role == VesselRole.None ? null : EntryOf(role, Fleet);

        static RoleEntry EntryOf(VesselRole role, VesselArchetypesSO fleet)
        {
            var e = new RoleEntry { Role = role };
            if (!fleet) return e;
            e.Name = fleet.RoleName(role);
            e.Line = fleet.RoleLine(role);
            foreach (var v in fleet.Vessels)
                if (fleet.RoleOf(v) == role) e.Vessels.Add(v);
            return e;
        }

        public ArchetypeEntry EntryOf(VesselArchetype a)
        {
            if (a == VesselArchetype.None || !(Fleet is { } fleet)) return null;
            return new ArchetypeEntry { Archetype = a, Name = fleet.ArchetypeName(a), Line = fleet.ArchetypeLine(a) };
        }

        /// <summary>The role's name, or empty.</summary>
        public string NameOf(VesselRole role) => Fleet is { } fleet ? fleet.RoleName(role) : string.Empty;

        /// <summary>The archetype's name, or empty.</summary>
        public string NameOf(VesselArchetype a) => Fleet is { } fleet ? fleet.ArchetypeName(a) : string.Empty;

        // ── The ask ─────────────────────────────────────────────────────────

        /// <summary>The named voyage's own ask, or null when it keeps its type's.</summary>
        public VoyageAsk VoyageAskOf(string voyageId)
        {
            if (!string.IsNullOrEmpty(voyageId))
                foreach (var a in voyageAsks)
                    if (a != null && a.Voyage == voyageId) return a;
            return null;
        }

        /// <summary>The type's ask, or null (the Crystal Run's).</summary>
        public Ask TypeAskOf(string job)
        {
            if (!string.IsNullOrEmpty(job))
                foreach (var a in asks)
                    if (a != null && a.Job == job) return a;
            return null;
        }

        /// <summary>The three archetype seats a voyage asks for: the named voyage's own when it has one, else its type's;
        /// empty for a job that asks for nothing (the Crystal Run).</summary>
        public IReadOnlyList<VesselArchetype> AskOf(string job, string voyageId = null)
        {
            if (VoyageAskOf(voyageId) is { } own) return own.Seats;
            if (TypeAskOf(job) is { } type) return type.Seats;
            return Array.Empty<VesselArchetype>();
        }

        /// <summary>Whether the voyage asks for any archetype of <paramref name="role"/>.</summary>
        public bool Wanted(string job, string voyageId, VesselRole role)
        {
            if (role == VesselRole.None) return false;
            foreach (var a in AskOf(job, voyageId))
                if (VesselArchetypes.RoleOf(a) == role) return true;
            return false;
        }

        /// <summary>How well <paramref name="vessel"/> alone meets its best seat of the voyage's ask (0, 0.5 or 1).</summary>
        public float Meets(string job, string voyageId, VesselClassType vessel)
        {
            float best = 0f;
            var mine = ArchetypesOf(vessel);
            foreach (var seat in AskOf(job, voyageId))
                best = Mathf.Max(best, VesselArchetypes.Meets(mine, seat));
            return best;
        }

        /// <summary>The squad's fit for the voyage's ask, 0 to 3 in halves (<see cref="VesselArchetypes.Fit"/>).</summary>
        public float Fit(string job, string voyageId, IEnumerable<VesselClassType> squad)
        {
            var list = new List<IReadOnlyList<VesselArchetype>>();
            if (squad != null)
                foreach (var v in squad) list.Add(ArchetypesOf(v));
            return VesselArchetypes.Fit(AskOf(job, voyageId), list);
        }

        /// <summary>The pay a voyage worth <paramref name="pay"/> gives a squad of fit <paramref name="fit"/>.</summary>
        public int FitPay(int pay, float fit) =>
            pay <= 0 || fit <= 0f || fitPayPerPoint <= 0f ? pay : Mathf.RoundToInt(pay * (1f + fitPayPerPoint * fit));

        /// <summary>The giver's counter line for a named voyage that meets a crew ("The Quiet Spear fly Sappers. Bring a
        /// Builder."), or null.</summary>
        public string CounterReason(string voyageId)
        {
            var own = VoyageAskOf(voyageId);
            if (own == null || own.Threat == VesselArchetype.None || own.Countered == VesselArchetype.None) return null;
            return TemplateText.Fill(counterLine, "crew", own.Crew, "threat", NameOf(own.Threat), "counter", NameOf(own.Countered));
        }

        /// <summary>The archetypes that counter <paramref name="a"/> on the board (the fleet's graph).</summary>
        public List<VesselArchetype> CounteredBy(VesselArchetype a) =>
            Fleet is { } fleet ? fleet.CounteredBy(a) : new List<VesselArchetype>();

        /// <summary>The job template id behind a voyage's seat key (<c>VoyageSeatRecord.JobKey</c>: template|pilot|to; the
        /// escort's key is its own id).</summary>
        public static string JobOf(string seatKey)
        {
            if (string.IsNullOrEmpty(seatKey)) return null;
            int bar = seatKey.IndexOf('|');
            return bar < 0 ? seatKey : seatKey.Substring(0, bar);
        }

        /// <summary>A fit as players read it: "2", "2.5".</summary>
        public static string FitText(float fit) =>
            fit.ToString(Mathf.Approximately(fit, Mathf.Round(fit)) ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Milestone 2 voyage classes (Docs/Voyages/MILESTONE_2.md, Frogma Rev 136 E and Rev 138 Vessels, picked by
    /// Shombith 2026-11-24): the seven classes, the vessels each one holds, the classes each kind of voyage wants, and the
    /// words of the Vessels section (<c>VoyageVesselsPage</c>). A class is a label only: it never changes a vessel's kit.
    /// <para>Written by <c>Tools/Build/author_voyage_classes.py</c> (its <c>--check</c> is the drift gate) into
    /// <c>Resources/Voyages/VoyageClasses</c>; every word is a field a person can edit or blank here.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageClasses", menuName = "ScriptableObjects/Voyages/Voyage Classes")]
    public class VoyageClassesSO : ScriptableObject
    {
        /// <summary>Where the shipped classes live, under Resources.</summary>
        public const string ResourcePath = "Voyages/VoyageClasses";

        static VoyageClassesSO s_loaded;

        /// <summary>The shipped classes (<c>Resources/Voyages/VoyageClasses</c>), or null when the asset is missing.</summary>
        public static VoyageClassesSO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VoyageClassesSO>(ResourcePath);
            return s_loaded;
        }

        /// <summary>One class: its name, what it is for, and the vessels that carry it (in the order the floor stands
        /// them).</summary>
        [Serializable]
        public class Entry
        {
            public VoyageClass Class;
            [Tooltip("The class's name as players read it (the tag beside a vessel, the class row on Vessels).")]
            public string Name;
            [Tooltip("What the class is for, one line (under each vessel on Vessels, after the wanted line).")]
            public string Line;
            [Tooltip("The vessels of this class, in the order they stand on the Vessels floor.")]
            public List<VesselClassType> Vessels = new();
        }

        /// <summary>The classes a kind of voyage wants (the giver's ask in the Voyage Codex).</summary>
        [Serializable]
        public class Wants
        {
            [Tooltip("The voyage's job template id (job.salvage.drift, escort, job.bounty.crew, job.defence.aid). A job with " +
                     "no row wants no class: the Crystal Run (job.raid.crystals) has none and shows no wanted line or fit.")]
            public string Job;
            public List<VoyageClass> Wanted = new();
        }

        [Header("Classes")]
        [SerializeField, Tooltip("The seven classes in the order the class row draws them.")]
        List<Entry> classes = new();

        [SerializeField, Tooltip("The classes each kind of voyage wants.")]
        List<Wants> wants = new();

        [Header("Vessels section words")]
        [SerializeField, Tooltip("Over the floor: who the seat is. Tokens {seat} (its role) and {who}.")]
        string seatLine = "{seat}  ·  {who}  ·  choose a vessel";
        [SerializeField, Tooltip("{who} on your own seat.")]
        string whoYou = "you";
        [SerializeField, Tooltip("{who} on a wingmate's seat.")]
        string whoAi = "flown by the AI";
        [SerializeField, Tooltip("Top right, before the wanted classes' tags.")]
        string wantedLabel = "WANTED";
        [SerializeField, Tooltip("Over the class row when the class is wanted. Token {class}.")]
        string wantedLine = "{class}s are wanted on this voyage";
        [SerializeField, Tooltip("Under a vessel the AI can fly, after the class's line.")]
        string aiCanFly = "The AI can fly it.";
        [SerializeField, Tooltip("Under a vessel the AI cannot fly when Vessels opens from an AI seat.")]
        string needsPerson = "Needs a person in the seat.";
        [SerializeField, Tooltip("On the floor when none of the class's vessels is in your hangar. Token {class}.")]
        string noneOwned = "No {class} in your hangar yet.";
        [SerializeField, Tooltip("At the foot when the pick changes the squad's fit. Tokens {from} and {to}.")]
        string fitChange = "Squad fit {from} to {to} of 3";
        [SerializeField, Tooltip("At the foot when the pick leaves the squad's fit as it is. Token {fit}.")]
        string fitSame = "Squad fit {fit} of 3";
        [SerializeField, Tooltip("The call to action: put the picked vessel in the seat.")]
        string take = "TAKE THE VESSEL";
        [SerializeField, Tooltip("The keycap on Take the vessel.")]
        string takeKey = "A";
        [SerializeField, Tooltip("Back to Your Ark without a change.")]
        string back = "BACK";
        [SerializeField, Tooltip("The keycap on Back.")]
        string backKey = "B";
        [SerializeField, Tooltip("The keycaps over the class row's ends (the shoulders step the class).")]
        string prevClassKey = "LB";
        [SerializeField, Tooltip("The keycap at the class row's right end.")]
        string nextClassKey = "RB";

        public IReadOnlyList<Entry> Classes => classes;
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
        public string Take => take;
        public string TakeKey => takeKey;
        public string Back => back;
        public string BackKey => backKey;
        public string PrevClassKey => prevClassKey;
        public string NextClassKey => nextClassKey;

        /// <summary>The class <paramref name="vessel"/> carries, or <see cref="VoyageClass.None"/>.</summary>
        public VoyageClass ClassOf(VesselClassType vessel)
        {
            foreach (var e in classes)
                if (e != null && e.Vessels.Contains(vessel)) return e.Class;
            return VoyageClass.None;
        }

        /// <summary>The entry for <paramref name="c"/>, or null.</summary>
        public Entry EntryOf(VoyageClass c)
        {
            foreach (var e in classes)
                if (e != null && e.Class == c) return e;
            return null;
        }

        /// <summary>The class's name, or empty.</summary>
        public string NameOf(VoyageClass c) => EntryOf(c)?.Name ?? string.Empty;

        /// <summary>The classes the job <paramref name="job"/> wants (empty for a job with none, the Crystal Run's).</summary>
        public IReadOnlyList<VoyageClass> WantedBy(string job)
        {
            if (!string.IsNullOrEmpty(job))
                foreach (var w in wants)
                    if (w != null && w.Job == job) return w.Wanted;
            return Array.Empty<VoyageClass>();
        }

        /// <summary>Whether <paramref name="job"/> asks for <paramref name="c"/>.</summary>
        public bool Wanted(string job, VoyageClass c)
        {
            if (c == VoyageClass.None) return false;
            foreach (var w in WantedBy(job))
                if (w == c) return true;
            return false;
        }

        /// <summary>The job template id behind a voyage's seat key (<c>VoyageSeatRecord.JobKey</c>: template|pilot|to; the
        /// escort's key is its own id).</summary>
        public static string JobOf(string seatKey)
        {
            if (string.IsNullOrEmpty(seatKey)) return null;
            int bar = seatKey.IndexOf('|');
            return bar < 0 ? seatKey : seatKey.Substring(0, bar);
        }

        /// <summary>How many of <paramref name="vessels"/> carry a class <paramref name="job"/> wants (the squad fit, 0-3).</summary>
        public int Fit(string job, IEnumerable<VesselClassType> vessels)
        {
            int fit = 0;
            foreach (var v in vessels)
                if (Wanted(job, ClassOf(v))) fit++;
            return fit;
        }
    }
}

using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The galaxy as this player knows it, and the voyage under way (Docs/Voyages/SLICE.md §1, DEPTH.md): each port's
    /// news, the wrecks lying out there, every place's live state, the rival crews and where they stand, the Kaiju, the
    /// crew's standing with each faction type and its notoriety, where the squad's Ark is, the job it took, what it
    /// found, and the story so far. Kept on the device (<c>VoyageStore</c>) until the backend holds voyage records
    /// (VOYAGES.md §8, Shombith's).
    ///
    /// <para>Plain fields only, so JsonUtility and Newtonsoft both round-trip it. Computed answers are methods. A record
    /// saved before the depth pass (DEPTH.md) loads with every new list empty and is repaired by <see cref="Normalize"/>;
    /// the first tick fills it in.</para>
    /// </summary>
    [Serializable]
    public class VoyageState
    {
        /// <summary>Bumped on every job taken; news ages by it and the board's draws are seeded from it.</summary>
        public int Day;
        /// <summary>The squad's home harbour (the location id), fixed by the squad's domain when the voyage began.</summary>
        public string Home;
        /// <summary>Where the Ark is (or, at sea, where it sailed from).</summary>
        public string At;
        /// <summary>The last port or harbour the Ark docked at: its news is what the crew knows while out.</summary>
        public string LastPort;
        /// <summary>True while a leg is being sailed.</summary>
        public bool AtSea;
        /// <summary>The job under way; null between jobs.</summary>
        public VoyageJob Current;
        /// <summary>Artifact ids found on this voyage and stowed aboard: theirs once the Ark docks at home.</summary>
        public List<string> Aboard = new();
        /// <summary>Crystals earned by jobs, paid at extraction, by element (index = (int)Element).</summary>
        public List<int> Owed = new();
        public List<PortLedger> Ledgers = new();
        public List<WreckRecord> Wrecks = new();
        public List<StoryEntry> Story = new();

        /// <summary>Every place's live state (DEPTH.md §2); made on first touch.</summary>
        public List<LocationState> Places = new();
        /// <summary>Every rival crew's live state (DEPTH.md §3); made on the first tick.</summary>
        public List<RivalState> Rivals = new();
        /// <summary>Standing with each faction type (index = (int)FactionType).</summary>
        public List<int> Standing = new();
        public int Notoriety;
        /// <summary>The day notoriety last decayed.</summary>
        public int NotorietyDay;
        /// <summary>Saga chapters resolved, and chapters whose news is posted (so a chapter is posted once).</summary>
        public List<string> ChaptersDone = new();
        public List<string> ChaptersOpen = new();
        /// <summary>Sagas whose relic has been found (saga ids).</summary>
        public List<string> RelicsFound = new();
        /// <summary>News aboard the Ark (a courier's dispatch, a thief's lift), delivered at the next port.</summary>
        public List<NewsItem> Carrying = new();
        public KaijuState Kaiju = new();
        public Logbook Log = new();
        /// <summary>The last day the galaxy ticked (so a day is never ticked twice).</summary>
        public int TickedDay;
        /// <summary>When the record was last saved (ms since the epoch): the days away tick the galaxy on return.</summary>
        public long SavedAtMs;

        public bool HasJob() => Current != null && !string.IsNullOrEmpty(Current.TemplateId);

        public bool IsHome() => !AtSea && !string.IsNullOrEmpty(Home) && At == Home;

        /// <summary>This port's ledger, made if it has none yet.</summary>
        public PortLedger Ledger(string port)
        {
            Ledgers ??= new List<PortLedger>();
            foreach (var l in Ledgers)
                if (l != null && l.Port == port) return l;
            var made = new PortLedger { Port = port };
            Ledgers.Add(made);
            return made;
        }

        /// <summary>The wreck lying at <paramref name="location"/>, or null.</summary>
        public WreckRecord WreckAt(string location)
        {
            if (Wrecks == null) return null;
            foreach (var w in Wrecks)
                if (w != null && w.At == location && w.Artifacts is { Count: > 0 }) return w;
            return null;
        }

        /// <summary>This place's live state, made if it has none yet (at rest, uncharted until the content says otherwise).</summary>
        public LocationState Place(string id)
        {
            Places ??= new List<LocationState>();
            foreach (var p in Places)
                if (p != null && p.Id == id) return p;
            var made = new LocationState { Id = id, FirstVisitDay = -1, LastVisitDay = -1, Charted = true };
            Places.Add(made);
            return made;
        }

        /// <summary>This place's live state, or null when it has never been touched (a probe).</summary>
        public LocationState PlaceIfAny(string id)
        {
            if (Places == null) return null;
            foreach (var p in Places)
                if (p != null && p.Id == id) return p;
            return null;
        }

        /// <summary>This rival's live state, or null before the first tick made it (a probe).</summary>
        public RivalState Rival(string id)
        {
            if (Rivals == null) return null;
            foreach (var r in Rivals)
                if (r != null && r.Id == id) return r;
            return null;
        }

        public int StandingOf(FactionType type)
        {
            int i = (int)type;
            return Standing != null && i >= 0 && i < Standing.Count ? Standing[i] : 0;
        }

        /// <summary>Standing moves by <paramref name="delta"/>, never below zero. Returns the new value.</summary>
        public int AddStanding(FactionType type, int delta)
        {
            Standing ??= new List<int>();
            while (Standing.Count <= (int)type) Standing.Add(0);
            Standing[(int)type] = Math.Max(0, Standing[(int)type] + delta);
            return Standing[(int)type];
        }

        public int OwedOf(Element element)
        {
            int i = (int)element;
            return Owed != null && i >= 0 && i < Owed.Count ? Owed[i] : 0;
        }

        internal void AddOwed(Element element, int amount)
        {
            if (amount <= 0) return;
            Owed ??= new List<int>();
            while (Owed.Count <= (int)Element.Omni) Owed.Add(0);
            Owed[(int)element] += amount;
        }

        /// <summary>Repairs a record a partial save (or a save from before the depth pass) can carry. Idempotent.</summary>
        public void Normalize()
        {
            Aboard ??= new List<string>();
            Aboard.RemoveAll(string.IsNullOrEmpty);
            Owed ??= new List<int>();
            Ledgers ??= new List<PortLedger>();
            Ledgers.RemoveAll(l => l == null || string.IsNullOrEmpty(l.Port));
            foreach (var l in Ledgers)
            {
                l.News ??= new List<NewsItem>();
                l.News.RemoveAll(n => n == null);
            }
            Wrecks ??= new List<WreckRecord>();
            Wrecks.RemoveAll(w => w == null || w.Artifacts == null || w.Artifacts.Count == 0);
            Story ??= new List<StoryEntry>();
            Places ??= new List<LocationState>();
            Places.RemoveAll(p => p == null || string.IsNullOrEmpty(p.Id));
            Rivals ??= new List<RivalState>();
            Rivals.RemoveAll(r => r == null || string.IsNullOrEmpty(r.Id));
            foreach (var r in Rivals) r.Hold ??= new List<string>();
            Standing ??= new List<int>();
            for (int i = 0; i < Standing.Count; i++) if (Standing[i] < 0) Standing[i] = 0;
            if (Notoriety < 0) Notoriety = 0;
            ChaptersDone ??= new List<string>();
            ChaptersOpen ??= new List<string>();
            RelicsFound ??= new List<string>();
            Carrying ??= new List<NewsItem>();
            Carrying.RemoveAll(n => n == null);
            Kaiju ??= new KaijuState();
            Log ??= new Logbook();
            if (Current != null && string.IsNullOrEmpty(Current.TemplateId)) Current = null;
            if (Current != null)
            {
                Current.Warnings ??= new List<string>();
                // A job saved before the verbs existed (DEPTH.md §4) asks what its kind always asked.
                if (Current.Verb == WorkVerb.Arrive && Current.Goal <= 0f)
                {
                    switch (Current.Kind)
                    {
                        case JobKind.Hunt:
                        case JobKind.Salvage: Current.Verb = WorkVerb.Stow; Current.Goal = 1f; break;
                        case JobKind.Defence: Current.Verb = WorkVerb.Hold; Current.Goal = 0f; break;
                    }
                }
            }
            if (Current == null) AtSea = false;
        }
    }

    /// <summary>A job taken from the board: the template, the pilot who brought it, where it goes and what it asks.</summary>
    [Serializable]
    public class VoyageJob
    {
        public string TemplateId;
        public JobKind Kind;
        /// <summary>The seated pilot who brought it (their callsign pilot id).</summary>
        public string PilotId;
        public string From;
        public string To;
        /// <summary>The news it grew from (null for standing work).</summary>
        public NewsItem Source;
        /// <summary>A courier's dispatch: the news it carries to <see cref="To"/>. Null when it carries none.</summary>
        public NewsItem Carried;
        /// <summary>Filled at draw time from the authored template, so the board and the log read the same words.</summary>
        public string Title;
        public string Summary;
        /// <summary>True for the always-offered "sail home" job, which is no faction's work.</summary>
        public bool IsReturn;
        /// <summary>The work and how much of it (DEPTH.md §4), copied from the template so a saved job needs no lookup.</summary>
        public WorkVerb Verb;
        public float Goal;
        public float HullFloor;
        public bool NoKill;
        public SquadRole Role;
        /// <summary>The crystals it pays, the course's intensity and its lanes, as the board priced it (DEPTH.md §7).</summary>
        public int Pay;
        public int Intensity = 1;
        public int Hops = 1;
        /// <summary>The difficulty (1-9) the crew picked on the voyage page (<see cref="VoyageDifficulty"/>); 0 is the one
        /// its pilot recommends. <see cref="Intensity"/> and <see cref="Pay"/> already follow it.</summary>
        public int Difficulty;
        /// <summary>The rival crew it is about (a bounty, a parley), or empty.</summary>
        public string RivalId;
        /// <summary>The saga chapter it is, or empty.</summary>
        public string Chapter;
        /// <summary>The warnings the board attached from the world as it was (authored lines).</summary>
        public List<string> Warnings = new();

        /// <summary>A shallow copy (the warnings and news are shared): the voyage page sets a copy's difficulty, never the
        /// board's own job.</summary>
        internal VoyageJob MemberwiseCloneJob() => (VoyageJob)MemberwiseClone();
    }

    [Serializable]
    public class PortLedger
    {
        public string Port;
        public List<NewsItem> News = new();

        public bool Knows(NewsTag tag, string about)
        {
            if (News == null) return false;
            foreach (var n in News)
                if (n != null && n.Tag == tag && n.About == about) return true;
            return false;
        }

        /// <summary>The news of this tag about this place, or null.</summary>
        public NewsItem Find(NewsTag tag, string about)
        {
            if (News == null) return null;
            foreach (var n in News)
                if (n != null && n.Tag == tag && n.About == about) return n;
            return null;
        }
    }

    /// <summary>A piece of news a port holds: the seed of a job (VOYAGES.md §6.3).</summary>
    [Serializable]
    public class NewsItem
    {
        public NewsTag Tag;
        /// <summary>The location it is about.</summary>
        public string About;
        public string Line;
        /// <summary>The day it was born; fresher news is offered first.</summary>
        public int Day;
        /// <summary>The saga chapter it is (<see cref="NewsTag.Chapter"/>), or empty.</summary>
        public string Chapter;
        /// <summary>The rival crew it is about (a bounty, an accord, a rumour), or empty.</summary>
        public string Rival;

        public NewsItem Copy() => new() { Tag = Tag, About = About, Line = Line, Day = Day, Chapter = Chapter, Rival = Rival };
    }

    /// <summary>A fallen Ark and the artifacts spilled around it (ARTIFACTS.md §5).</summary>
    [Serializable]
    public class WreckRecord
    {
        public string At;
        public List<string> Artifacts = new();
        public string Line;
        /// <summary>The day it fell: the food web grazes an unclaimed wreck down after the world's wreck days.</summary>
        public int Day;
    }

    /// <summary>One line of the voyage's story: what happened, in order.</summary>
    [Serializable]
    public class StoryEntry
    {
        public int Day;
        public string Line;
    }

    /// <summary>A place as it is now (DEPTH.md §2).</summary>
    [Serializable]
    public class LocationState
    {
        public string Id;
        public float Pressure;
        /// <summary>Who holds the nucleus (cores); meaningful only when <see cref="HasHolder"/>.</summary>
        public Domains Holder;
        public bool HasHolder;
        public int Bloom;
        /// <summary>Vault shells still sealed; -1 before the first visit (every shell stands).</summary>
        public int Sealed = -1;
        /// <summary>The day a picked field is lit again; 0 when it is not picked.</summary>
        public int RestockDay;
        /// <summary>True once this crew has the place on its chart (always true for a place the content charts).</summary>
        public bool Charted = true;
        /// <summary>True once a rival crew surveyed it (an explorer who comes second is paid less).</summary>
        public bool KnownToOthers;
        public int Visits;
        public int FirstVisitDay = -1;
        public int LastVisitDay = -1;
    }

    /// <summary>A rival crew as it is now (DEPTH.md §3).</summary>
    [Serializable]
    public class RivalState
    {
        public string Id;
        /// <summary>Where it stands, or where it sailed from while bound somewhere.</summary>
        public string At;
        /// <summary>Where it is bound; empty when standing.</summary>
        public string To;
        public int ArriveDay;
        /// <summary>The job it sails, or empty.</summary>
        public string JobTemplate;
        /// <summary>What it carries: at stake when it falls.</summary>
        public List<string> Hold = new();
        public int Notoriety;
        /// <summary>Its grudge against this crew (0-3), and the day it last grew (once a day).</summary>
        public int Grudge;
        public int GrudgeDay = -1;
        /// <summary>Its live strength: a crew that takes an Ark is promoted.</summary>
        public int Strength = 1;
        /// <summary>The day it fell or was caught; -1 while it sails.</summary>
        public int WreckedDay = -1;
        /// <summary>The day it sails again from home after a fall.</summary>
        public int ReturnDay;
        /// <summary>Resolutions, wrecks and promotions it has had (the logbook's half).</summary>
        public int Deeds;

        public bool IsOut => WreckedDay < 0;
    }

    /// <summary>The Kaiju as it is now (DEPTH.md §3).</summary>
    [Serializable]
    public class KaijuState
    {
        /// <summary>The lair it holds, or empty while it is not sighted.</summary>
        public string At;
        /// <summary>How many times it has been felled: never easier than the hardest felled.</summary>
        public int Tier;
        public bool Alive;
        /// <summary>The day it was sighted or last moved.</summary>
        public int Day;
        /// <summary>The day it moves next, or is sighted again after a fall.</summary>
        public int NextDay;
        public string LastLair;
    }

    /// <summary>What this crew has done, in numbers (DEPTH.md §9).</summary>
    [Serializable]
    public class Logbook
    {
        public int Voyages;
        public int Legs;
        public int JobsDone;
        public int JobsFailed;
        public int Extracted;
        public int Relics;
        public int ArksLost;
        public int Charted;
        public int RivalsWrecked;
        public int RivalsCaught;
        public int KaijuFelled;
        public int Crystals;
        /// <summary>The rival that last took this crew's Ark, and the day.</summary>
        public string Nemesis;
        public int NemesisDay = -1;
    }
}

using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The galaxy as this player knows it, and the voyage under way (Docs/Voyages/SLICE.md §1): each port's news, the
    /// wrecks lying out there, where the squad's Ark is, the job it took, what it found, and the story so far. Kept on
    /// the device (<c>VoyageStore</c>) until the backend holds voyage records (VOYAGES.md §8, Shombith's).
    ///
    /// <para>Plain fields only, so JsonUtility and Newtonsoft both round-trip it. Computed answers are methods.</para>
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

        /// <summary>Repairs a record a partial save can carry. Idempotent.</summary>
        public void Normalize()
        {
            Aboard ??= new List<string>();
            Aboard.RemoveAll(string.IsNullOrEmpty);
            Owed ??= new List<int>();
            Ledgers ??= new List<PortLedger>();
            Ledgers.RemoveAll(l => l == null || string.IsNullOrEmpty(l.Port));
            foreach (var l in Ledgers) l.News ??= new List<NewsItem>();
            Wrecks ??= new List<WreckRecord>();
            Wrecks.RemoveAll(w => w == null || w.Artifacts == null || w.Artifacts.Count == 0);
            Story ??= new List<StoryEntry>();
            if (Current != null && string.IsNullOrEmpty(Current.TemplateId)) Current = null;
            if (Current == null) AtSea = false;
        }
    }

    /// <summary>A job taken from the board: the template, the pilot who brought it, and where it goes.</summary>
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

        public NewsItem Copy() => new() { Tag = Tag, About = About, Line = Line, Day = Day };
    }

    /// <summary>A fallen Ark and the artifacts spilled around it (ARTIFACTS.md §5).</summary>
    [Serializable]
    public class WreckRecord
    {
        public string At;
        public List<string> Artifacts = new();
        public string Line;
    }

    /// <summary>One line of the voyage's story: what happened, in order.</summary>
    [Serializable]
    public class StoryEntry
    {
        public int Day;
        public string Line;
    }
}

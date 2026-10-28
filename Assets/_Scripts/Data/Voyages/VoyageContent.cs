using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// Everything a voyage is made of, as authored data (Docs/Voyages/SLICE.md §3): the HyperSea map, the people on
    /// the crew's callsigns, the jobs each faction type brings, the events a job's outcome writes as news, and the news
    /// each port starts with. Plain serializable classes with no engine type, so the rules (<see cref="VoyageRules"/>)
    /// run in an edit-mode test or headless. The game reads it from <c>VoyageContentSO</c>
    /// (<c>Resources/Voyages/VoyageContent</c>), written from <c>Docs/Voyages/voyage_slice.json</c> by
    /// <c>Tools/Build/author_voyage_assets.py</c>.
    ///
    /// <para>Every string here a player reads is authored (the LOCKED text rule): job titles and lines are templates
    /// whose tokens the rules fill (<c>{to}</c>, <c>{from}</c>, <c>{about}</c>, <c>{pilot}</c>). An empty line shows
    /// nothing.</para>
    /// </summary>
    [Serializable]
    public class VoyageContent
    {
        public List<VoyageLocation> Locations = new();
        public List<VoyageLane> Lanes = new();
        public List<CrewPerson> Crew = new();
        public List<JobTemplate> Jobs = new();
        public List<VoyageEvent> Events = new();
        public List<SeedNews> Seeds = new();
        public List<SeedWreck> SeedWrecks = new();
        /// <summary>The wreck a fallen Ark leaves ({about} is where), and the news of it at the nearest port.</summary>
        public string WreckLine;
        public string WreckNewsLine;

        [NonSerialized] Dictionary<string, VoyageLocation> _byId;

        /// <summary>The location with this id, or null. A probe: a stale id in a saved record is a miss, not a fault.</summary>
        public VoyageLocation Location(string id)
        {
            if (string.IsNullOrEmpty(id) || Locations == null) return null;
            if (_byId == null || _byId.Count != Locations.Count)
            {
                _byId = new Dictionary<string, VoyageLocation>();
                foreach (var l in Locations)
                    if (l != null && !string.IsNullOrEmpty(l.Id)) _byId[l.Id] = l;
            }
            return _byId.TryGetValue(id, out var location) ? location : null;
        }

        /// <summary>The person on the crew who carries <paramref name="pilotId"/>'s callsign, or null.</summary>
        public CrewPerson Person(string pilotId)
        {
            if (string.IsNullOrEmpty(pilotId) || Crew == null) return null;
            foreach (var p in Crew)
                if (p != null && p.PilotId == pilotId) return p;
            return null;
        }

        public JobTemplate Job(string id)
        {
            if (string.IsNullOrEmpty(id) || Jobs == null) return null;
            foreach (var j in Jobs)
                if (j != null && j.Id == id) return j;
            return null;
        }

        public VoyageEvent Event(string id)
        {
            if (string.IsNullOrEmpty(id) || Events == null) return null;
            foreach (var e in Events)
                if (e != null && e.Id == id) return e;
            return null;
        }

        /// <summary>The harbour of <paramref name="domain"/>, or null.</summary>
        public VoyageLocation HarbourOf(Domains domain)
        {
            if (Locations == null) return null;
            foreach (var l in Locations)
                if (l != null && l.Experience == VoyageExperience.Harbour && l.Home == domain) return l;
            return null;
        }

        /// <summary>
        /// Problems an author must fix, one line each; empty when the content is sound. The generator and the edit-mode
        /// tests both run it, so a broken table fails before it reaches a player.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (var l in Locations ?? new List<VoyageLocation>())
            {
                if (l == null || string.IsNullOrEmpty(l.Id)) { errors.Add("a location has no id"); continue; }
                if (!ids.Add(l.Id)) errors.Add($"location id '{l.Id}' is used twice");
                if (l.Experience == VoyageExperience.VaultField && (l.Finds == null || l.Finds.Count == 0))
                    errors.Add($"vault field '{l.Id}' has nothing to find");
                if (l.Experience == VoyageExperience.VaultField && l.Vaults < 1)
                    errors.Add($"vault field '{l.Id}' has no vaults");
            }
            foreach (var lane in Lanes ?? new List<VoyageLane>())
            {
                if (lane == null) continue;
                if (Location(lane.From) == null) errors.Add($"a lane starts at unknown location '{lane.From}'");
                if (Location(lane.To) == null) errors.Add($"a lane ends at unknown location '{lane.To}'");
                if (lane.Cells < 0) errors.Add($"lane {lane.From}-{lane.To} has a negative length");
            }
            foreach (var p in Crew ?? new List<CrewPerson>())
                if (p == null || string.IsNullOrEmpty(p.PilotId) || p.Faction == FactionType.None)
                    errors.Add($"crew person '{p?.Name}' needs a pilot id and a faction type");
            foreach (var j in Jobs ?? new List<JobTemplate>())
            {
                if (j == null || string.IsNullOrEmpty(j.Id)) { errors.Add("a job template has no id"); continue; }
                if (j.Faction == FactionType.None) errors.Add($"job '{j.Id}' names no faction type");
                if (!string.IsNullOrEmpty(j.OnSuccess) && Event(j.OnSuccess) == null)
                    errors.Add($"job '{j.Id}' names unknown success event '{j.OnSuccess}'");
                if (!string.IsNullOrEmpty(j.OnFail) && Event(j.OnFail) == null)
                    errors.Add($"job '{j.Id}' names unknown fail event '{j.OnFail}'");
            }
            foreach (var s in Seeds ?? new List<SeedNews>())
                if (s == null || Location(s.Port) == null || Location(s.About) == null)
                    errors.Add($"seed news at '{s?.Port}' about '{s?.About}' names an unknown location");
            foreach (var w in SeedWrecks ?? new List<SeedWreck>())
                if (w == null || Location(w.At) == null) errors.Add($"seed wreck at '{w?.At}' names an unknown location");
            foreach (Domains d in new[] { Domains.Jade, Domains.Ruby, Domains.Gold })
                if (HarbourOf(d) == null) errors.Add($"no harbour for {d}");
            return errors;
        }
    }

    /// <summary>One place on the HyperSea: a cell with an experience (VOYAGES.md §3.1, §5).</summary>
    [Serializable]
    public class VoyageLocation
    {
        public string Id;
        public string Name;
        /// <summary>One line about the place, for the chart and the board.</summary>
        public string Blurb;
        public VoyageExperience Experience;
        /// <summary>The domain whose harbour this is. Only read on a <see cref="VoyageExperience.Harbour"/>.</summary>
        public Domains Home = Domains.Blue;
        /// <summary>Where it sits on the chart, in chart units (the chart scales them).</summary>
        public float ChartX, ChartY, ChartZ;
        /// <summary>The location's own world: an index into the content asset's cell pool.</summary>
        public int Cell;
        /// <summary>Vault shells standing on each visit (vault fields).</summary>
        public int Vaults;
        /// <summary>The artifact ids this location's vaults can hold.</summary>
        public List<string> Finds = new();
        /// <summary>Seconds the Ark must stay under a core to hold it (cores).</summary>
        public float HoldSeconds = 60f;
    }

    /// <summary>A lane between two locations, flown both ways (VOYAGES.md §3.1).</summary>
    [Serializable]
    public class VoyageLane
    {
        public string From;
        public string To;
        /// <summary>Open-sea cells the Ark crosses between the two, before the destination's own cell.</summary>
        public int Cells = 1;
    }

    /// <summary>A person on the crew: unique, layered on one callsign pilot (VOYAGES.md §6.2.1).</summary>
    [Serializable]
    public class CrewPerson
    {
        /// <summary>The callsign pilot this person is (<c>PilotSO.Id</c>, e.g. pilot.callsign.citadel).</summary>
        public string PilotId;
        public string Name;
        public FactionType Faction;
        public string Story;
    }

    /// <summary>One kind of work a faction type brings (VOYAGES.md §6.2).</summary>
    [Serializable]
    public class JobTemplate
    {
        public string Id;
        public JobKind Kind;
        public FactionType Faction;
        /// <summary>The news the job grows from; <see cref="NewsTag.None"/> is standing work that needs no news.</summary>
        public NewsTag Needs;
        /// <summary>For standing work: where it goes. Ignored when <see cref="Needs"/> names news (the news says where).</summary>
        public VoyageExperience Goes;
        public string Title;
        public string Summary;
        /// <summary>The story log's line when the job succeeds, and when it fails.</summary>
        public string SuccessLine;
        public string FailLine;
        /// <summary>Elemental crystals the job pays at extraction.</summary>
        public int Reward;
        public Element RewardElement = Element.Omni;
        /// <summary>Event ids written on success and on failure (see <see cref="VoyageEvent"/>). Empty writes nothing.</summary>
        public string OnSuccess;
        public string OnFail;
    }

    /// <summary>What an outcome writes into the galaxy's news (VOYAGES.md §6.3): the chain from one job to the next.</summary>
    [Serializable]
    public class VoyageEvent
    {
        public string Id;
        public List<NewsWrite> Writes = new();
        /// <summary>The job's own news is spent: every port forgets news of this tag about the destination.</summary>
        public bool SpendsNews;
    }

    [Serializable]
    public class NewsWrite
    {
        public NewsTag Tag;
        public NewsPlace At;
        public NewsAbout About;
        /// <summary>The news as the board reads it ({about}, {to}, {from} filled).</summary>
        public string Line;
    }

    /// <summary>News a port knows when the galaxy is new.</summary>
    [Serializable]
    public class SeedNews
    {
        public string Port;
        public NewsTag Tag;
        public string About;
        public string Line;
    }

    /// <summary>A wreck lying in the galaxy when it is new: its artifacts spill there for anyone.</summary>
    [Serializable]
    public class SeedWreck
    {
        public string At;
        public List<string> Artifacts = new();
        public string Line;
    }
}

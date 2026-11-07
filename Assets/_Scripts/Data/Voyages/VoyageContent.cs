using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// Everything a voyage is made of, as authored data (Docs/Voyages/SLICE.md §3, DEPTH.md): the HyperSea map, the
    /// people on the crew's callsigns, the jobs each faction type brings, the events a job's outcome writes as news, the
    /// news each port starts with, the rival crews that sail the same sea, the sagas and their relics, the Kaiju, and
    /// the rates the living galaxy ticks by. Plain serializable classes with no engine type, so the rules
    /// (<see cref="VoyageRules"/>, <see cref="VoyageWorld"/>, <see cref="VoyageRivals"/>) run in an edit-mode test or
    /// headless. The game reads it from <c>VoyageContentSO</c> (<c>Resources/Voyages/VoyageContent</c>), written from
    /// <c>Docs/Voyages/voyage_slice.json</c> by <c>Tools/Build/author_voyage_assets.py</c>.
    ///
    /// <para>Every string here a player reads is authored (the LOCKED text rule): job titles and lines are templates
    /// whose tokens the rules fill (<c>{to}</c>, <c>{from}</c>, <c>{about}</c>, <c>{pilot}</c>, <c>{rival}</c>,
    /// <c>{n}</c>). An empty line shows nothing.</para>
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

        /// <summary>The other crews on the sea (DEPTH.md §3).</summary>
        public List<RivalCrew> Rivals = new();
        /// <summary>The story threads and their relics (DEPTH.md §5).</summary>
        public List<Saga> Sagas = new();
        /// <summary>The rates the galaxy ticks by, the pay table and the tiers (DEPTH.md §2, §6, §7).</summary>
        public VoyageWorldRules World = new();
        /// <summary>The one lit beacon: the apex creature every port hears of (DEPTH.md §3).</summary>
        public KaijuSpec Kaiju = new();
        /// <summary>The lines the rules themselves write into news, the story and the warnings.</summary>
        public VoyageWords Words = new();

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

        /// <summary>
        /// Where the player's REAL crew comes from (Docs/Pilots/INVENTORY.md §3.4): the game sets it to read the account's
        /// unique pilots (<c>AccountInventoryStore</c>), so a seated pilot's id finds that person, their name and their
        /// faction type. Null in a test or headless run, where the authored <see cref="Crew"/> stands in.
        /// </summary>
        public static Func<string, CrewPerson> CrewSource;

        /// <summary>
        /// The person a seated pilot id names, or null: the player's own pilot (<see cref="CrewSource"/>) first, then the
        /// authored crew by exact id, then the authored person on the same callsign (a unique pilot id starts with its
        /// callsign's id, <see cref="UniqueId"/>), then a rival crew (a rival takes jobs as a person of its faction).
        /// </summary>
        public CrewPerson Person(string pilotId)
        {
            if (string.IsNullOrEmpty(pilotId)) return null;
            if (CrewSource?.Invoke(pilotId) is { } real) return real;
            if (Crew != null)
            {
                foreach (var p in Crew)
                    if (p != null && p.PilotId == pilotId) return p;
                string callsign = UniqueId.TemplateOf(pilotId);
                if (callsign != pilotId)
                    foreach (var p in Crew)
                        if (p != null && p.PilotId == callsign) return p;
            }
            var rival = Rival(pilotId);
            return rival?.AsPerson();
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

        public RivalCrew Rival(string id)
        {
            if (string.IsNullOrEmpty(id) || Rivals == null) return null;
            foreach (var r in Rivals)
                if (r != null && r.Id == id) return r;
            return null;
        }

        public Saga SagaOf(string sagaId)
        {
            if (string.IsNullOrEmpty(sagaId) || Sagas == null) return null;
            foreach (var s in Sagas)
                if (s != null && s.Id == sagaId) return s;
            return null;
        }

        /// <summary>The chapter with this id across every saga, and its saga; null when none.</summary>
        public SagaChapter Chapter(string chapterId, out Saga saga)
        {
            saga = null;
            if (string.IsNullOrEmpty(chapterId) || Sagas == null) return null;
            foreach (var s in Sagas)
            {
                if (s?.Chapters == null) continue;
                foreach (var c in s.Chapters)
                    if (c != null && c.Id == chapterId) { saga = s; return c; }
            }
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
                bool sealedFinds = l.Experience == VoyageExperience.VaultField || l.Experience == VoyageExperience.Hoard;
                if (sealedFinds && (l.Finds == null || l.Finds.Count == 0))
                    errors.Add($"'{l.Id}' has nothing to find");
                if (sealedFinds && l.Vaults < 1)
                    errors.Add($"'{l.Id}' has no vaults");
                if (l.RestPressure < 0f || l.RestPressure > 3f) errors.Add($"'{l.Id}' rests at pressure {l.RestPressure}; 0 to 3");
                if (l.RestBloom < 0 || l.RestBloom > 3) errors.Add($"'{l.Id}' rests at bloom {l.RestBloom}; 0 to 3");
                if (l.Uncharted && (l.Experience == VoyageExperience.Harbour || l.Experience == VoyageExperience.Port))
                    errors.Add($"'{l.Id}' is an uncharted port: every port is known");
            }
            foreach (var lane in Lanes ?? new List<VoyageLane>())
            {
                if (lane == null) continue;
                if (Location(lane.From) == null) errors.Add($"a lane starts at unknown location '{lane.From}'");
                if (Location(lane.To) == null) errors.Add($"a lane ends at unknown location '{lane.To}'");
                if (lane.Cells < 0) errors.Add($"lane {lane.From}-{lane.To} has a negative length");
                if (lane.Profile is { Count: > 0 } && lane.Profile.Count != lane.Cells)
                    errors.Add($"lane {lane.From}-{lane.To} has a profile of {lane.Profile.Count} for {lane.Cells} cells");
            }
            foreach (var p in Crew ?? new List<CrewPerson>())
                if (p == null || string.IsNullOrEmpty(p.PilotId) || p.Faction == FactionType.None)
                    errors.Add($"crew person '{p?.Name}' needs a pilot id and a faction type");
            var jobIds = new HashSet<string>();
            foreach (var j in Jobs ?? new List<JobTemplate>())
            {
                if (j == null || string.IsNullOrEmpty(j.Id)) { errors.Add("a job template has no id"); continue; }
                if (!jobIds.Add(j.Id)) errors.Add($"job id '{j.Id}' is used twice");
                if (j.Faction == FactionType.None && j.Id != VoyageRules.ReturnJobId) errors.Add($"job '{j.Id}' names no faction type");
                if (!string.IsNullOrEmpty(j.OnSuccess) && Event(j.OnSuccess) == null)
                    errors.Add($"job '{j.Id}' names unknown success event '{j.OnSuccess}'");
                if (!string.IsNullOrEmpty(j.OnFail) && Event(j.OnFail) == null)
                    errors.Add($"job '{j.Id}' names unknown fail event '{j.OnFail}'");
                if (j.Verb != WorkVerb.Arrive && j.Verb != WorkVerb.Meet && j.Verb != WorkVerb.Hold && j.Goal <= 0f)
                    errors.Add($"job '{j.Id}' asks {j.Verb} with no goal");
                if (j.HullFloor < 0f || j.HullFloor > 1f) errors.Add($"job '{j.Id}' has a hull floor of {j.HullFloor}; 0 to 1");
                if (j.Tier < 0 || j.Tier > 2) errors.Add($"job '{j.Id}' asks tier {j.Tier}; 0 to 2");
            }
            foreach (var s in Seeds ?? new List<SeedNews>())
                if (s == null || Location(s.Port) == null || Location(s.About) == null)
                    errors.Add($"seed news at '{s?.Port}' about '{s?.About}' names an unknown location");
            foreach (var w in SeedWrecks ?? new List<SeedWreck>())
                if (w == null || Location(w.At) == null) errors.Add($"seed wreck at '{w?.At}' names an unknown location");
            foreach (Domains d in new[] { Domains.Jade, Domains.Ruby, Domains.Gold })
                if (HarbourOf(d) == null) errors.Add($"no harbour for {d}");

            var rivalIds = new HashSet<string>();
            foreach (var r in Rivals ?? new List<RivalCrew>())
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) { errors.Add("a rival crew has no id"); continue; }
                if (!rivalIds.Add(r.Id)) errors.Add($"rival id '{r.Id}' is used twice");
                if (Location(r.Home) == null) errors.Add($"rival '{r.Id}' is home at unknown location '{r.Home}'");
                if (r.Faction == FactionType.None) errors.Add($"rival '{r.Id}' names no faction type");
                if (r.Strength < 1 || r.Strength > 3) errors.Add($"rival '{r.Id}' has strength {r.Strength}; 1 to 3");
                if (r.Hulls == null || r.Hulls.Count == 0) errors.Add($"rival '{r.Id}' has no hulls");
            }
            var chapterIds = new HashSet<string>();
            foreach (var s in Sagas ?? new List<Saga>())
            {
                if (s == null || string.IsNullOrEmpty(s.Id)) { errors.Add("a saga has no id"); continue; }
                if (s.Chapters == null || s.Chapters.Count == 0) { errors.Add($"saga '{s.Id}' has no chapters"); continue; }
                if (!string.IsNullOrEmpty(s.StartPort) && Location(s.StartPort) == null)
                    errors.Add($"saga '{s.Id}' starts at unknown location '{s.StartPort}'");
                if (s.Relic == null || string.IsNullOrEmpty(s.Relic.Kind) || string.IsNullOrEmpty(s.Relic.Name))
                    errors.Add($"saga '{s.Id}' has no relic (kind and name)");
                foreach (var c in s.Chapters)
                {
                    if (c == null || string.IsNullOrEmpty(c.Id)) { errors.Add($"saga '{s.Id}' has a chapter with no id"); continue; }
                    if (!chapterIds.Add(c.Id)) errors.Add($"chapter id '{c.Id}' is used twice");
                    var t = Job(c.Job);
                    if (t == null) errors.Add($"chapter '{c.Id}' names unknown job '{c.Job}'");
                    else if (t.Faction != c.Faction) errors.Add($"chapter '{c.Id}' asks a {c.Faction} but its job is {t.Faction} work");
                    if (c.Faction == FactionType.None) errors.Add($"chapter '{c.Id}' names no faction type");
                    if (!string.IsNullOrEmpty(c.At) && Location(c.At) == null) errors.Add($"chapter '{c.Id}' goes to unknown location '{c.At}'");
                    if (!string.IsNullOrEmpty(c.Opens) && Chapter(c.Opens, out _) == null && !ChapterListed(s, c.Opens))
                        errors.Add($"chapter '{c.Id}' opens unknown chapter '{c.Opens}'");
                }
                // The chain from the first chapter must reach a finale (an empty Opens) without looping.
                var seen = new HashSet<string>();
                var at = s.Chapters[0];
                while (at != null)
                {
                    if (!seen.Add(at.Id)) { errors.Add($"saga '{s.Id}' loops at chapter '{at.Id}'"); break; }
                    if (string.IsNullOrEmpty(at.Opens)) break;
                    at = Chapter(at.Opens, out _);
                }
            }
            if (World != null)
            {
                if (World.BoardSize < 1) errors.Add("world.boardSize must be at least 1");
                if (World.TierThresholds == null || World.TierThresholds.Count != 2 || World.TierThresholds[0] >= World.TierThresholds[1])
                    errors.Add("world.tierThresholds must be two rising numbers");
                if (World.IntensityPay == null || World.IntensityPay.Count != 4) errors.Add("world.intensityPay must have four multipliers");
                if (World.TierPay == null || World.TierPay.Count != 3) errors.Add("world.tierPay must have three multipliers");
            }
            if (Kaiju is { Enabled: true } && Job(Kaiju.HuntJob) == null)
                errors.Add($"the kaiju's hunt names unknown job '{Kaiju.HuntJob}'");
            return errors;
        }

        static bool ChapterListed(Saga s, string id)
        {
            foreach (var c in s.Chapters)
                if (c != null && c.Id == id) return true;
            return false;
        }
    }

    /// <summary>One place on the HyperSea: a cell with an experience (VOYAGES.md §3.1, §5; DEPTH.md §2).</summary>
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
        /// <summary>True when the place has a position on the real HyperSea (<see cref="HyperSea"/>): the Voyage
        /// window's map then draws it on the Milky Way's own sea, and a job's course is sailed cell by cell.</summary>
        public bool Charted;
        /// <summary>Its position on the real HyperSea, in light years: the galactic centre at the origin, Sol at
        /// (0, 26,000), z the height above the midplane (Docs/Voyages/VOYAGE_BOARD.md §1).</summary>
        public float GalaxyX, GalaxyY, GalaxyZ;
        /// <summary>The location's own world: an index into the content asset's cell pool.</summary>
        public int Cell;
        /// <summary>Vault shells standing on each visit (vault fields and hoards).</summary>
        public int Vaults;
        /// <summary>The artifact ids this location's vaults can hold.</summary>
        public List<string> Finds = new();
        /// <summary>Seconds the Ark must stay under a core to hold it (cores, shrines, nurseries, derelicts).</summary>
        public float HoldSeconds = 60f;
        /// <summary>The pressure the place drifts back to when nothing happens there (0-3, DEPTH.md §2).</summary>
        public float RestPressure;
        /// <summary>The bloom a nursery grows back to (0-3).</summary>
        public int RestBloom;
        /// <summary>True when this crew has to find the place before it is on its chart (DEPTH.md §8).</summary>
        public bool Uncharted;
        /// <summary>Days before a picked vault field is lit again; 0 takes the world's.</summary>
        public int RestockDays;
    }

    /// <summary>A lane between two locations, flown both ways (VOYAGES.md §3.1).</summary>
    [Serializable]
    public class VoyageLane
    {
        public string From;
        public string To;
        /// <summary>Open-sea cells the Ark crosses between the two, before the destination's own cell.</summary>
        public int Cells = 1;
        /// <summary>Each open-sea cell's population scale (one per cell; empty takes the world's leg profile): the
        /// voyage's pacing, hardest in the middle cells (the team's 2026-10-30 meeting: cells 2 and 4 hardest).</summary>
        public List<float> Profile = new();
    }

    /// <summary>A person on the crew: unique, layered on one callsign pilot (VOYAGES.md §6.2.1). The authored ones stand in
    /// for a test or headless run; in the game each is made from the player's <see cref="PilotRecord"/>.</summary>
    [Serializable]
    public class CrewPerson
    {
        /// <summary>The pilot's id: a unique pilot's (<c>pilot.callsign.citadel#...</c>), or for an authored stand-in the
        /// callsign's (<c>PilotSO.Id</c>, e.g. pilot.callsign.citadel).</summary>
        public string PilotId;
        public string Name;
        public FactionType Faction;
        public string Story;
    }

    /// <summary>One kind of work a faction type brings (VOYAGES.md §6.2, DEPTH.md §4).</summary>
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
        /// <summary>Elemental crystals the job pays at extraction, before the course and the tier scale it (DEPTH.md §7).</summary>
        public int Reward;
        public Element RewardElement = Element.Omni;
        /// <summary>Event ids written on success and on failure (see <see cref="VoyageEvent"/>). Empty writes nothing.</summary>
        public string OnSuccess;
        public string OnFail;
        /// <summary>The work the world watches for (DEPTH.md §4).</summary>
        public WorkVerb Verb;
        /// <summary>How much of it: artifacts, seconds, crystals, creatures, prisms or volume.</summary>
        public float Goal = 1f;
        /// <summary>A convoy must arrive with the hull above this share (0 = any).</summary>
        public float HullFloor;
        /// <summary>A kill undoes the work (a preservationist's tend).</summary>
        public bool NoKill;
        /// <summary>The squad role the job suits.</summary>
        public SquadRole Role;
        /// <summary>The standing tier the pilot's faction type must have reached (0 Stranger, 1 Known, 2 Trusted).</summary>
        public int Tier;
        /// <summary>Standing gained on success (lost on failure).</summary>
        public int StandingDelta = 1;
        /// <summary>Notoriety gained on success.</summary>
        public int NotorietyDelta;
        /// <summary>Standing work that goes to the nearest place this crew has not charted (explorers).</summary>
        public bool GoesUncharted;
        /// <summary>Standing work that never goes to the squad's own domain's harbour (a thief's lift).</summary>
        public bool AvoidsOwnDomain;
        /// <summary>Whether a rival crew may draw this job too (the Kaiju hunt is the player's).</summary>
        public bool RivalsMayTake = true;
        /// <summary>The line written into the pilot's chronicle on success ({to}, {about}).</summary>
        public string ChronicleLine;
    }

    /// <summary>What an outcome writes into the galaxy's news and state (VOYAGES.md §6.3, DEPTH.md §2): the chain.</summary>
    [Serializable]
    public class VoyageEvent
    {
        public string Id;
        public List<NewsWrite> Writes = new();
        /// <summary>The job's own news is spent: every port forgets news of this tag about the destination.</summary>
        public bool SpendsNews;
    }

    /// <summary>One write of an event: news at a port (when <see cref="Tag"/> is set) and what it does to the world.</summary>
    [Serializable]
    public class NewsWrite
    {
        public NewsTag Tag;
        public NewsPlace At;
        public NewsAbout About;
        /// <summary>For <see cref="NewsAbout.AnotherOf"/>: the experience of the place the news is about.</summary>
        public VoyageExperience AboutExperience;
        /// <summary>The news as the board reads it ({about}, {to}, {from}, {pilot}, {rival} filled).</summary>
        public string Line;
        /// <summary>Added to the destination's pressure (DEPTH.md §2).</summary>
        public float PressureDelta;
        /// <summary>Added to the destination's bloom.</summary>
        public int BloomDelta;
        /// <summary>The destination's nucleus is the squad's now (a held or retaken core).</summary>
        public bool HolderToSquad;
        /// <summary>The destination is on this crew's chart now.</summary>
        public bool ChartsDestination;
        /// <summary>Added to the job's rival's grudge (an accord eases it).</summary>
        public int RivalGrudgeDelta;
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

    /// <summary>Another squad on the sea (DEPTH.md §3): authored, with live state on the record (<see cref="RivalState"/>).</summary>
    [Serializable]
    public class RivalCrew
    {
        public string Id;
        public string Name;
        public FactionType Faction;
        public Domains Domain;
        /// <summary>The port it sails from and returns to.</summary>
        public string Home;
        /// <summary>1 to 3: how good it is at the work it takes.</summary>
        public int Strength = 1;
        public RivalTemper Temper;
        /// <summary>The three hulls it stands in the world (vessel class names).</summary>
        public List<string> Hulls = new();
        /// <summary>One line about the crew (the Story level).</summary>
        public string Story;
        /// <summary>The banner when the squad arrives where they stand ({rival}, {to}).</summary>
        public string ArriveLine;
        /// <summary>The story when they fall ({rival}, {about}).</summary>
        public string FallLine;
        /// <summary>The story when they beat the squad to a find ({rival}, {to}).</summary>
        public string WinLine;

        /// <summary>The crew as a person on a board: it draws jobs by the same rules a pilot does.</summary>
        public CrewPerson AsPerson() => new() { PilotId = Id, Name = Name, Faction = Faction, Story = Story };
    }

    /// <summary>A story thread across several voyages, ending in a relic (DEPTH.md §5).</summary>
    [Serializable]
    public class Saga
    {
        public string Id;
        public string Name;
        /// <summary>Where the first chapter is posted (a port or harbour); empty posts it at the squad's home.</summary>
        public string StartPort;
        /// <summary>The standing tier the first chapter's faction type needs before it is posted.</summary>
        public int StartTier;
        /// <summary>The day from which the first chapter can be posted.</summary>
        public int StartDay;
        public RelicSpec Relic = new();
        public List<SagaChapter> Chapters = new();
    }

    /// <summary>One chapter: the news a pilot of its faction finds, the job it becomes, and what it opens.</summary>
    [Serializable]
    public class SagaChapter
    {
        public string Id;
        public FactionType Faction;
        /// <summary>The standing tier the pilot's faction type needs.</summary>
        public int Tier;
        /// <summary>The job template it is drawn as (the verb, the kind, the lines).</summary>
        public string Job;
        /// <summary>Where it goes: a location id, or empty for the nearest place of <see cref="AtExperience"/>.</summary>
        public string At;
        public VoyageExperience AtExperience;
        /// <summary>The news line as the board reads it ({about}).</summary>
        public string Line;
        /// <summary>The chapter opened on success; empty for the finale, which grants the saga's relic.</summary>
        public string Opens;
        /// <summary>The story line on success ({to}, {relic}).</summary>
        public string DoneLine;
    }

    /// <summary>A relic: one unique artifact per galaxy with an authored name and lore (DEPTH.md §5).</summary>
    [Serializable]
    public class RelicSpec
    {
        /// <summary>The artifact kind id (artifact.hydra, ...).</summary>
        public string Kind;
        public string Name;
        public string Lore;
        /// <summary>The word the relic's seed is hashed from; its id is the same in every galaxy.</summary>
        public string Seed;
    }

    /// <summary>The apex creature: one at a time, sighted at every port, harder after every fall (DEPTH.md §3).</summary>
    [Serializable]
    public class KaijuSpec
    {
        public bool Enabled;
        public string Name;
        /// <summary>The hunters' job it is drawn as (a Cull at a lair).</summary>
        public string HuntJob;
        /// <summary>Days after a fall before it is sighted again.</summary>
        public int RespawnDays = 5;
        /// <summary>Days it lairs in one place before moving.</summary>
        public int MoveDays = 4;
        /// <summary>Creatures to fell at tier 0, and more per tier (the ratchet: never easier than the hardest felled).</summary>
        public float CullGoal = 6f;
        public float CullStep = 4f;
        /// <summary>Population the lair gains per tier.</summary>
        public float PopulationPerTier = 0.5f;
        public string SightedLine;
        public string MovedLine;
        public string FelledLine;
    }

    /// <summary>The rates and tables the living galaxy runs on (DEPTH.md §2, §6, §7). Authored in the spec's world block.</summary>
    [Serializable]
    public class VoyageWorldRules
    {
        /// <summary>The most jobs a board offers beside the way home (the team's 2026-10-30 meeting: about six).</summary>
        public int BoardSize = 6;
        public int NewsLifeDays = 12;
        public int RestockDays = 4;
        public int WreckDays = 8;
        public int BloomDays = 2;
        public int NotorietyDecayDays = 3;
        /// <summary>Notoriety at which a bounty is posted on a crew.</summary>
        public int BountyAt = 3;
        /// <summary>Finds aboard at which word of the laden Ark reaches the nearest port.</summary>
        public int LadenRumourAt = 2;
        /// <summary>Seconds a racing rival takes to open a vault shell.</summary>
        public float RivalVaultSeconds = 45f;
        /// <summary>The open-sea cells' population scales when a lane has no profile of its own.</summary>
        public List<float> LegProfile = new() { 1f, 1.3f, 0.9f, 1.4f, 1f };
        /// <summary>Standing at which a faction type's tier becomes Known, then Trusted.</summary>
        public List<int> TierThresholds = new() { 2, 5 };
        /// <summary>Pay multiplier per course intensity I1-I4.</summary>
        public List<float> IntensityPay = new() { 1f, 1.3f, 1.7f, 2.2f };
        /// <summary>Pay multiplier per lane beyond the first.</summary>
        public float HopsPay = 0.15f;
        /// <summary>Pay multiplier per standing tier.</summary>
        public List<float> TierPay = new() { 1f, 1.25f, 1.6f };
        /// <summary>News-grown pay fades this much per day of the news's age, to a floor.</summary>
        public float FreshnessPerDay = 0.05f;
        public float FreshnessFloor = 0.5f;
        /// <summary>The most days the galaxy ticks when the player returns after time away.</summary>
        public int MaxTicksAway = 5;
        /// <summary>How much of the way back to its rest a place's pressure drifts each day.</summary>
        public float PressureDrift = 0.5f;
        public float RivalSuccessBase = 0.35f;
        public float RivalStrengthWeight = 0.2f;
        public float RivalIntensityWeight = 0.15f;
        public float RivalPressureWeight = 0.1f;
        public float RivalWreckChance = 0.25f;
        /// <summary>Days a wrecked or caught crew is off the sea before it sails again from home.</summary>
        public int RivalRespawnDays = 6;
        /// <summary>Population scale of a lair's own world, a pen's, and per point of pressure and bloom.</summary>
        public float LairPopulation = 1.6f;
        public float PenPopulation = 1.2f;
        public float PressurePopulation = 0.25f;
        public float BloomPopulation = 0.15f;
        /// <summary>Pressure at which a core asks for aid, and a core's shell is breached.</summary>
        public float AidPressure = 2f;
        public float BreachPressure = 2.6f;
        /// <summary>Pressure a failed hold or a rival's failure adds to a place.</summary>
        public float FailurePressure = 1f;
    }

    /// <summary>The lines the rules write without a template of their own (DEPTH.md §2-§7). Every one is authored.</summary>
    [Serializable]
    public class VoyageWords
    {
        /// <summary>News: a picked vault field is lit again ({about}).</summary>
        public string RestockNews;
        /// <summary>News at every port: a price is on the squad's own Ark ({home}).</summary>
        public string BountyOnYou;
        /// <summary>News: a price is on a rival crew ({rival}, {about} where they stand).</summary>
        public string BountyOnRival;
        /// <summary>News: dock talk of the squad's laden Ark ({n} finds, {about}).</summary>
        public string LadenRumour;
        /// <summary>News: a core asks for aid once its pressure crosses the line ({about}).</summary>
        public string AidNews;
        /// <summary>News: a core's shell is breached ({about}).</summary>
        public string BreachNews;
        /// <summary>News: a nursery's bloom is ripe ({about}).</summary>
        public string HarvestNews;
        /// <summary>News: another domain holds a core ({about}, {holder}).</summary>
        public string HeldNews;
        /// <summary>News: a rival crew fell ({rival}, {about}).</summary>
        public string RivalFellNews;
        /// <summary>Story: a rival beat the squad to a find ({rival}, {to}).</summary>
        public string RivalRace;
        /// <summary>Story: a rival took the squad's Ark ({rival}, {about}).</summary>
        public string Nemesis;
        /// <summary>Story: a place is on the chart now ({to}).</summary>
        public string Charted;
        /// <summary>Story: a relic came aboard ({relic}).</summary>
        public string RelicFound;
        /// <summary>Story: the squad's standing with a faction type rose a tier ({faction}).</summary>
        public string StandingUp;
        /// <summary>Story: the crew's notoriety crossed the bounty line.</summary>
        public string Wanted;
        /// <summary>Warnings on a card, from the world as it is ({to}, {rival}, {n}).</summary>
        public string WarningPressure;
        public string WarningRival;
        public string WarningKaiju;
        public string WarningBounty;
        public string WarningLaden;
        public string WarningHeld;
        /// <summary>The chronicle line a courier's delivery writes ({to}).</summary>
        public string ChronicleDelivered;
        /// <summary>The names of the roles for {role}.</summary>
        public string RoleAny;
        public string RoleGuardian;
        public string RoleDestroyer;
        public string RoleCultivator;
        /// <summary>The names of the tiers for {tier}.</summary>
        public string TierStranger;
        public string TierKnown;
        public string TierTrusted;
        /// <summary>The relation lines on arrival ({rival}).</summary>
        public string RelationCoOp;
        public string RelationRace;
        public string RelationHostile;
    }
}

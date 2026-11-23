using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Data
{
    /// <summary>
    /// Rolls unique pilots (Docs/Pilots/INVENTORY.md §3.1): a person with a name, a human + two-animal heritage, a
    /// faction, a homeworld and a backstory, on one of the fixed callsigns.
    ///
    /// <para>Like <see cref="ArtifactGenerator"/>: a pure function of (callsign, seed, version, faction type), the seed in
    /// the id, every roll stored on the record. Names come from syllables (about 18,000 given names x 1,600 family names),
    /// heritage from 48 x 47 ordered animal pairs, factions from the founding roster of the type, and the backstory from
    /// five seeded sentences, one of which is about what this person's callsign lets them see. Everything is printable
    /// ASCII (the UI font has no other glyphs) and refers to the pilot by name or "they".</para>
    /// </summary>
    public static class PilotGenerator
    {
        public const int Version = 1;

        static readonly string[] GivenStarts = { "A", "Bel", "Ca", "Dar", "E", "Fen", "Gal", "Ha", "I", "Jo", "Ka", "Lu", "Ma", "Ne", "O", "Pe", "Qui", "Ro", "Sa", "Ta", "U", "Ve", "Wy", "Xa", "Yo", "Ze", "Bri", "Cor", "Del", "Mi" };
        static readonly string[] GivenMiddles = { "", "", "", "la", "ri", "no", "ve", "si", "ma", "lo", "ren", "tha", "di", "ko", "mi", "ra", "be", "nu", "li", "so" };
        static readonly string[] GivenEnds = { "n", "ra", "x", "lo", "ne", "ko", "th", "ri", "sa", "ve", "ndo", "lia", "rin", "mo", "sh", "ta", "ly", "ro", "dek", "wen", "ia", "ek", "ola", "us", "ix", "ane", "o", "ar", "el", "ai" };
        static readonly string[] FamilyFronts = { "Salt", "Drift", "Kelp", "Brine", "Tide", "Reef", "Gull", "Wave", "Moon", "Star", "Shell", "Foam", "Wake", "Coral", "Storm", "Ember", "Glass", "Copper", "Amber", "Frost", "Bright", "Low", "Deep", "Far", "Still", "Grey", "Silver", "Rust", "Lantern", "Hollow", "Crest", "Pearl", "Mire", "Sand", "Ash", "Dusk", "Dawn", "Whistle", "Anchor", "Spindle" };
        static readonly string[] FamilyBacks = { "whistle", "moor", "wright", "brook", "field", "song", "keel", "mark", "ward", "fall", "hollow", "wick", "more", "stone", "line", "gate", "water", "shore", "light", "runner", "weaver", "bloom", "tail", "fin", "reach", "hook", "rest", "watch", "wing", "spark", "cove", "bell", "drift", "sail", "thorn", "ridge", "hart", "well", "mere", "sworth" };

        static readonly string[] Homeworlds =
        {
            "Saltmere", "the Lantern Reach", "Hollowfen", "Cinder Bay", "Driftmoor", "the Glass Shoals", "Ember Deep",
            "Rimehollow", "Coldharbour", "Starwake", "Brightwater", "Shellmouth", "Gloamwater", "Farshore", "Stillwater",
            "Tangleweed", "Mirrorfen", "Lowlight", "Brackenmouth", "the Copper Reef", "the Kelp Throne", "Lanternfall",
            "the Grey Reach", "the Old Harbour", "Wrackline", "the Amber Shoal", "the Silver Lane", "the Coral Court",
        };

        static readonly string[] Upbringings =
        {
            "grew up mending nets", "was raised in a lighthouse", "learned to fly before they could read", "grew up on a salvage barge",
            "was raised by a choir of dockworkers", "spent a childhood racing skiffs", "grew up in the shadow of a dead Ark",
            "was apprenticed to a cartographer", "grew up sorting crystals in a market stall", "was raised among reef gardeners",
            "grew up the youngest of eleven", "spent a childhood stowing away on freighters",
        };

        static readonly string[] HeritageLines =
        {
            "shows the {one} when cornered and the {two} when bored",
            "has the patience of a {one} and the appetite of a {two}",
            "still sleeps like a {one} and eats like a {two}",
            "swears the {two} half is why they never get lost, though the {one} half disagrees",
            "moves like a {one} until something startles the {two} out of them",
            "has the {one}'s stubbornness and the {two}'s terrible sense of humour",
        };

        static readonly string[] Joinings =
        {
            "signed on with {faction} after {they} lost a bet",
            "was recruited by {faction} straight out of a bar fight",
            "joined {faction} to pay off a debt that is somehow still growing",
            "was born into {faction} and never thought to leave",
            "ran to {faction} after a voyage went badly wrong",
            "was taken in by {faction} when nobody else would have them",
            "earned a place in {faction} by flying a route nobody else would",
            "joined {faction} for the stories and stayed for the crew",
        };

        static readonly Dictionary<FactionType, string> TypeLines = new()
        {
            [FactionType.Merchant] = "merchants for whom a contract is a promise and a late delivery is a scar",
            [FactionType.Hunter] = "hunters who chase what lairs in the deep cells",
            [FactionType.Aristocrat] = "aristocrats who hold the cores and expect to be thanked for it",
            [FactionType.Pirate] = "pirates who take what other squads carry and call it fair",
            [FactionType.BountyHunter] = "bounty hunters who collect on the squads with the loudest names",
            [FactionType.Explorer] = "explorers who go where the map is blank",
            [FactionType.Activist] = "activists who keep the lanes open for everyone",
            [FactionType.Thief] = "thieves who trade in what nobody meant to share",
            [FactionType.Gatherer] = "gatherers who harvest what the cells give freely",
            [FactionType.Contractor] = "contractors who build what everyone else breaks",
            [FactionType.Sport] = "racers who fly for glory and side bets",
            [FactionType.Researcher] = "researchers who want to know why the cells grow",
            [FactionType.Preservationist] = "preservationists who guard the blooms nobody else will",
            [FactionType.FirstResponder] = "first responders who fly toward the fallen Arks",
            [FactionType.Priest] = "priests who listen to the HyperSea and claim it answers",
            [FactionType.Diplomat] = "diplomats who settle feuds before they become fleets",
        };

        /// <summary>What each callsign lets the person see, as part of their story (CALLSIGNS.md §2).</summary>
        static readonly Dictionary<PilotVision, string> SightLines = new()
        {
            [PilotVision.Ensign] = "{name} can find their own domain's mass in any storm, which is why crews call them ENSIGN",
            [PilotVision.Manger] = "{name} always knows where the fauna are heading next; the callsign MANGER stuck after the first stampede they predicted",
            [PilotVision.Anvil] = "{name} sees Mass crystals the way others see lighthouses, and ANVIL is what the haulers named them",
            [PilotVision.Volt] = "{name} can pick out a Charge crystal through a wall of light, so VOLT it was",
            [PilotVision.Zodiac] = "{name} reads Space crystals like constellations, which earned them the callsign ZODIAC",
            [PilotVision.Sundial] = "{name} feels Time crystals before they see them; SUNDIAL was a joke that never wore off",
            [PilotVision.Opal] = "{name} has an eye for Omni crystals that borders on unfair, hence OPAL",
            [PilotVision.Rampart] = "{name} sees every shielded prism as a wall to lean on, and flies as RAMPART",
            [PilotVision.Citadel] = "{name} can spot super-shielded mass from across a cell; CITADEL is a compliment, mostly",
            [PilotVision.Hornet] = "{name} sees danger prisms before anyone else, and has the stings to prove it: HORNET",
            [PilotVision.Ivy] = "{name} sees the plants of a cell as one living map, so the crew calls them IVY",
            [PilotVision.Talon] = "{name} always knows where the predators are, which is how TALON stayed alive this long",
            [PilotVision.Doe] = "{name} tracks herbivores the way a shepherd counts a flock, so DOE it is",
            [PilotVision.Wingman] = "{name} never loses sight of their own side; WINGMAN was earned in a single bad night",
            [PilotVision.Bandit] = "{name} finds opposing vessels no matter where they hide, and flies as BANDIT",
            [PilotVision.Needle] = "{name} sees every gate and switch as if it were lit, hence NEEDLE",
            [PilotVision.Galleon] = "{name} can find an Ark, and whatever it carries, from the far side of a cell: GALLEON",
            [PilotVision.Flume] = "{name} traces solitons and tunnels mouth to mouth, so the crew calls them FLUME",
        };

        static readonly string[] Quirks =
        {
            "They hum off-key during every jump.", "They keep a jar of every crystal they have ever missed.",
            "They name every vessel they fly, and apologise to it after a hard landing.", "They refuse to fly on an empty stomach.",
            "They collect shells from every port and give them away to strangers.", "They keep score of everything, including naps.",
            "They talk to the Ark at night and insist it answers.", "They have never once arrived early.",
            "They write letters to a sibling who never writes back.", "They cannot resist a dare.",
            "They tie a ribbon on the hull for every friend lost.", "They laugh at exactly the wrong moments.",
        };

        static readonly string[] Wants =
        {
            "What they want most is a quiet harbour and a long sleep.", "What they want most is to see the edge of the map.",
            "What they want most is to pay back an old debt, with interest.", "What they want most is a crew that stays.",
            "What they want most is a rematch.", "What they want most is to find the Ark that left them behind.",
            "What they want most is to be remembered in a song.", "What they want most is a vault no one else has opened.",
        };

        /// <summary>
        /// Rolls the pilot carrying <paramref name="callsign"/> from <paramref name="seed"/>. The faction type is rolled
        /// from <see cref="FactionCatalog.Recruitable"/> unless <paramref name="factionType"/> names one (a recruit drawn
        /// from a faction, an emissary). Null for None.
        /// </summary>
        public static PilotRecord Roll(PilotVision callsign, ulong seed, FactionType factionType = FactionType.None,
            string origin = "", long nowMs = 0)
        {
            if (callsign == PilotVision.None) return null;
            if (seed == 0) seed = 1;

            var names = SeededRandom.For(seed, "pilot.name");
            string given = names.Pick(GivenStarts) + names.Pick(GivenMiddles) + names.Pick(GivenEnds);
            given = char.ToUpperInvariant(given[0]) + given.Substring(1).ToLowerInvariant();
            string family = names.Pick(FamilyFronts) + names.Pick(FamilyBacks);

            var heritage = SeededRandom.For(seed, "pilot.species");
            var pool = Species.Rollable;
            int first = heritage.Next(pool.Length);
            int second = heritage.Next(pool.Length - 1);
            if (second >= first) second++;   // never the same animal twice
            var one = pool[first];
            var two = pool[second];

            var allegiance = SeededRandom.For(seed, "pilot.faction");
            var type = factionType != FactionType.None ? factionType : allegiance.Pick(FactionCatalog.Recruitable);
            var factions = FactionCatalog.FoundingNames(type);
            string factionName = factions.Count > 0 ? factions[allegiance.Next(factions.Count)] : string.Empty;

            var story = SeededRandom.For(seed, "pilot.story");
            string home = story.Pick(Homeworlds);

            var r = new PilotRecord
            {
                Id = UniqueId.Compose(Callsigns.IdOf(callsign), seed),
                CallsignId = Callsigns.IdOf(callsign),
                GeneratorVersion = Version,
                GivenName = given,
                FamilyName = family,
                SpeciesOne = one,
                SpeciesTwo = two,
                FactionType = type,
                FactionId = factionName.Length > 0 ? FactionCatalog.IdOf(type, factionName) : string.Empty,
                FactionName = factionName,
                Homeworld = home,
                Origin = origin ?? string.Empty,
                MintedAtMs = nowMs,
            };
            r.Backstory = Backstory(r, ref story);
            return r;
        }

        /// <summary>Rolls a fresh pilot on <paramref name="callsign"/> with a seed nobody has used.</summary>
        public static PilotRecord RollNew(PilotVision callsign, FactionType factionType, string origin, long nowMs) =>
            Roll(callsign, SeededRandom.NewSeed(), factionType, origin, nowMs);

        static string Backstory(PilotRecord p, ref SeededRandom story)
        {
            string name = p.GivenName;
            string faction = FactionInSentence(p.FactionName);
            string typeLine = TypeLines.TryGetValue(p.FactionType, out var tl) ? tl : string.Empty;

            var sb = new StringBuilder();
            sb.Append(name).Append(' ').Append(p.FamilyName).Append(' ').Append(story.Pick(Upbringings))
              .Append(" on ").Append(p.Homeworld).Append(". Part human, part ").Append(Species.Name(p.SpeciesOne).ToLowerInvariant())
              .Append(", part ").Append(Species.Name(p.SpeciesTwo).ToLowerInvariant()).Append(", ").Append(name).Append(' ')
              .Append(story.Pick(HeritageLines).Replace("{one}", Species.Name(p.SpeciesOne).ToLowerInvariant())
                  .Replace("{two}", Species.Name(p.SpeciesTwo).ToLowerInvariant()))
              .Append(". ");
            sb.Append(name).Append(' ').Append(story.Pick(Joinings).Replace("{faction}", faction).Replace("{they}", "they")).Append(". ");
            if (typeLine.Length > 0) sb.Append("Its members are ").Append(typeLine).Append(". ");
            if (SightLines.TryGetValue(p.Vision(), out var sight)) sb.Append(sight.Replace("{name}", name)).Append(". ");
            sb.Append(story.Pick(Quirks)).Append(' ').Append(story.Pick(Wants));
            return Printable(sb.ToString());
        }

        /// <summary>A faction's name mid-sentence: "the Black Tide", "House Verdance", "a crew with no name".</summary>
        static string FactionInSentence(string name)
        {
            if (string.IsNullOrEmpty(name)) return "a crew with no name";
            if (name.StartsWith("The ", StringComparison.Ordinal)) return "the " + name.Substring(4);
            if (name.StartsWith("House ", StringComparison.Ordinal)) return name;
            return "the " + name;
        }

        /// <summary><see cref="Printable"/> for a text of paragraphs: keeps the line breaks between them (a story).</summary>
        public static string PrintableText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if ((c >= ' ' && c <= '~') || c == '\n') sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Printable ASCII only (anything else would draw as an empty box in the UI font).</summary>
        public static string Printable(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if (c >= ' ' && c <= '~') sb.Append(c);
            return sb.ToString();
        }
    }
}

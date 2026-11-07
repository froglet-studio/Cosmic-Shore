using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The factions a pilot can belong to (Docs/Voyages/VOYAGES.md §6.2, §6.2.1; Docs/Pilots/INVENTORY.md §3.3). A
    /// faction is one of many of its <see cref="FactionType"/>, and each type belongs to a domain (pirates are Ruby).
    ///
    /// <para><b>Factions are shared, pilots are not.</b> Every crew's pirate pilots are drawn into the same handful of
    /// pirate factions, so two players' pilots can be faction-mates. The real roster, with an optimal size of 50 and
    /// new factions spawned when every faction of a type is full, is the backend's (§6.2.1, Shombith's area); until it
    /// exists the founding factions below are the roster, and a pilot's <see cref="PilotRecord.FactionId"/> names one.
    /// A backend faction later replaces the id without touching the pilot.</para>
    ///
    /// <para>Names are printable ASCII (the UI font has no other glyphs) and avoid every vessel, callsign and mode.</para>
    /// </summary>
    public static class FactionCatalog
    {
        public const string IdPrefix = "faction.";

        /// <summary>The faction types a new pilot can be minted into: the ones whose jobs exist. Since the depth pass
        /// (Docs/Voyages/DEPTH.md §4, 2026-11-07) every type brings work to the voyage board, so every type recruits.</summary>
        public static readonly FactionType[] Recruitable =
        {
            FactionType.Merchant, FactionType.Hunter, FactionType.Aristocrat, FactionType.Pirate,
            FactionType.BountyHunter, FactionType.Explorer, FactionType.Activist, FactionType.Thief, FactionType.Gatherer,
            FactionType.Contractor, FactionType.Sport, FactionType.Researcher, FactionType.Preservationist,
            FactionType.FirstResponder, FactionType.Priest, FactionType.Diplomat,
        };

        static readonly Dictionary<FactionType, string[]> Founding = new()
        {
            [FactionType.Merchant] = new[] { "Brass Lantern Company", "Tidewright Exchange", "House of Seven Scales", "Saltmark Consortium", "Amber Ledger Guild", "Quayside Mutual" },
            [FactionType.Hunter] = new[] { "Longwake Lodge", "The Quiet Spear", "Hollowfen Trackers", "Order of the Last Lure", "Moonsedge Pack", "Deepreed Stalkers" },
            [FactionType.Aristocrat] = new[] { "House Verdance", "The Jade Accord", "Court of Still Water", "House Albrine", "The Lattice Peerage", "Wardens of the Calm" },
            [FactionType.Pirate] = new[] { "Black Tide", "The Rusted Grin", "Gullbone Crew", "Red Reef Rovers", "The Unmoored", "Saltjaw Brethren" },
            [FactionType.BountyHunter] = new[] { "Writ and Hook", "The Ninth Warrant", "Copperclaim Agency" },
            [FactionType.Explorer] = new[] { "Far Shoal Society", "The Unmapped" , "Lantern Reach Cartographers" },
            [FactionType.Activist] = new[] { "Open Current", "The Unbound Choir", "Free Shoals League" },
            [FactionType.Thief] = new[] { "Velvet Gill", "The Pickled Hand", "Whisperkelp" },
            [FactionType.Gatherer] = new[] { "Driftnet Cooperative", "The Patient Harvest", "Kelpline Gleaners" },
            [FactionType.Contractor] = new[] { "Keel and Rivet", "Bright Hull Works", "Steadfast Joinery" },
            [FactionType.Sport] = new[] { "Slipstream Racing Club", "The Glittering Heats", "Spray and Spin" },
            [FactionType.Researcher] = new[] { "Institute of the Shore", "The Tidal Archive", "Lumen College" },
            [FactionType.Preservationist] = new[] { "Keepers of the Bloom", "Reefward Trust", "The Green Vigil" },
            [FactionType.FirstResponder] = new[] { "Lifeline Corps", "The Bright Buoys", "Harbour Watch" },
            [FactionType.Priest] = new[] { "Choir of the Deep", "The Ninefold Shell", "Order of the Slow Tide" },
            [FactionType.Diplomat] = new[] { "The Open Hand", "Envoys of the Accord", "The Treaty Table" },
        };

        /// <summary>The domain a faction type belongs to (VOYAGES.md §6.2: Ruby is freedom, Gold commerce, Jade order).</summary>
        public static Domains DomainOf(FactionType type) => type switch
        {
            FactionType.Hunter or FactionType.Pirate or FactionType.Explorer or FactionType.Activist or FactionType.Thief => Domains.Ruby,
            FactionType.Merchant or FactionType.BountyHunter or FactionType.Gatherer or FactionType.Contractor or FactionType.Sport => Domains.Gold,
            _ => Domains.Jade,
        };

        /// <summary>A faction type as players read it ("Bounty hunter").</summary>
        public static string TypeName(FactionType type) => type switch
        {
            FactionType.BountyHunter => "Bounty hunter",
            FactionType.FirstResponder => "First responder",
            FactionType.None => string.Empty,
            _ => type.ToString(),
        };

        /// <summary>The founding factions of <paramref name="type"/>, in order; empty for None.</summary>
        public static IReadOnlyList<string> FoundingNames(FactionType type) =>
            Founding.TryGetValue(type, out var names) ? names : Array.Empty<string>();

        /// <summary>The id of the faction named <paramref name="name"/> of <paramref name="type"/>:
        /// <c>faction.pirate.black-tide</c>.</summary>
        public static string IdOf(FactionType type, string name) =>
            IdPrefix + type.ToString().ToLowerInvariant() + "." + Slug(name);

        /// <summary>The type an id names (<c>faction.pirate.*</c> is Pirate); None for anything else.</summary>
        public static FactionType TypeOf(string factionId)
        {
            if (string.IsNullOrEmpty(factionId) || !factionId.StartsWith(IdPrefix, StringComparison.Ordinal)) return FactionType.None;
            int dot = factionId.IndexOf('.', IdPrefix.Length);
            string word = dot < 0 ? factionId.Substring(IdPrefix.Length) : factionId.Substring(IdPrefix.Length, dot - IdPrefix.Length);
            foreach (FactionType t in Enum.GetValues(typeof(FactionType)))
                if (t != FactionType.None && string.Equals(t.ToString(), word, StringComparison.OrdinalIgnoreCase)) return t;
            return FactionType.None;
        }

        /// <summary>The founding faction's name for <paramref name="factionId"/>, or empty when it is not one of them
        /// (a backend-spawned faction carries its own name on the pilot).</summary>
        public static string NameOf(string factionId)
        {
            var type = TypeOf(factionId);
            foreach (var name in FoundingNames(type))
                if (IdOf(type, name) == factionId) return name;
            return string.Empty;
        }

        /// <summary>Lowercase words joined by hyphens, ASCII letters and digits only.</summary>
        public static string Slug(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var chars = new System.Text.StringBuilder(name.Length);
            bool dash = false;
            foreach (char c in name.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (dash && chars.Length > 0) chars.Append('-');
                    chars.Append(c);
                    dash = false;
                }
                else dash = true;
            }
            return chars.ToString();
        }
    }
}

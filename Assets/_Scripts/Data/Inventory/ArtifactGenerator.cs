using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// Rolls unique artifacts (Docs/Pilots/INVENTORY.md §2.2; Garrett, 2026-11-07: "we need a system that can create
    /// unlimited unique artifacts so no two are the same").
    ///
    /// <para><b>Unlimited and unique.</b> Every artifact is a pure function of (kind, 64-bit seed, generator version).
    /// The seed is in the id, so two artifacts with different seeds are different artifacts by construction, and the
    /// space is 2^64 per kind. On top of that the roll varies the attuned element, the ceiling, the curve's half-way
    /// count, the three fixed tracks, the name (an epithet x the kind x a place, about 2,400 names per kind before
    /// the serial), the lore and the look, so two artifacts of one kind almost never even play alike. The inventory
    /// refuses a duplicate id outright (<see cref="AccountInventory.TryAddArtifact"/>).</para>
    ///
    /// <para><b>Deterministic.</b> The same kind and seed roll the same artifact on every machine (<see cref="SeededRandom"/>),
    /// so an id seen in a friend's squad, an Ark's hold or a wreck can be re-rolled where no record travels with it.</para>
    ///
    /// <para><b>Never re-tune a shipped version's tables.</b> Records snapshot what they rolled, but a re-roll from an
    /// id (a find) uses the tables of <see cref="Version"/>. Add a version and branch on it instead.</para>
    /// </summary>
    public static class ArtifactGenerator
    {
        /// <summary>The tables below. Bump it when changing any of them, and keep the old branch.</summary>
        public const int Version = 1;

        // ── Rolled ranges (version 1). The 0-10 band and the twist at 5 are the elements' (ARTIFACTS.md §4). ──
        /// <summary>Lowest and highest level a fixed (non-attuned) track is rolled at: under the twist, so only the
        /// attuned track ever twists.</summary>
        public const float FixedMin = 0f, FixedMax = 3f;
        /// <summary>The attuned track starts here...</summary>
        public const float BaseMin = 0f, BaseMax = 2f;
        /// <summary>...and creeps toward a ceiling rolled here (the band's top is 10).</summary>
        public const float CeilingMin = 7f, CeilingMax = 10f;
        /// <summary>Crystals to go half the way. The old fixed track cost 65 crystals from 0 to 10 (2 + 3 + ... + 11), so a
        /// typical artifact is past its half-way point by the time a whole old track would have been bought.</summary>
        public const float HalfMin = 25f, HalfMax = 60f;

        static readonly string[] Epithets =
        {
            "Weeping", "Gilded", "Hollow", "Restless", "Silent", "Brine-Cut", "Moonlit", "Shattered", "Patient", "Fevered",
            "Drowned", "Singing", "Splintered", "Lucky", "Crooked", "Unblinking", "Feral", "Last", "Borrowed", "Sleeping",
            "Ashen", "Glass", "Tidal", "Pale", "Thundering", "Velvet", "Bitter", "Wandering", "Iron-Hearted", "Kind",
            "Wicked", "Orphaned", "Humming", "Crowned", "Salt-Stained", "Quiet", "Burning", "Frostbitten", "Grinning", "Forgotten",
        };

        static readonly string[] Places =
        {
            "the Lantern Reach", "Low Tide", "the Ninth Vault", "Saltmere", "the Drowned Choir",
            "the Long Dark", "Hollowfen", "the Glass Shoals", "the Old Harbour", "Ember Deep",
            "the Quiet Current", "the Broken Ark", "Cinder Bay", "the Pale Nucleus", "Seven Wrecks",
            "the Kelp Throne", "Driftmoor", "the Last Lighthouse", "the Sunken Market", "Starwake",
            "the Coral Court", "the First Voyage", "Rimehollow", "the Silver Lane", "Mourning Reef",
            "the Tidewright", "Gloamwater", "the Deep Bell", "Brightwater", "the Unmapped Sea",
            "the Copper Moon", "the Wailing Shelf", "Farshore", "the Sleeping Cell", "Wrackline",
            "the Hundred Lamps", "Blackwater", "the Hollow Star", "the Lost Fleet", "Shellmouth",
            "the Murmur", "Lowlight", "the Gilded Wake", "Sorrowdeep", "the Restless Tide",
            "Mirrorfen", "the Seventh Bloom", "the Amber Shoal", "Saltwhistle", "the Far Bell",
            "the Weeping Drift", "Coldharbour", "the Ancient Fry", "Lanternfall", "the Grey Reach",
            "Brackenmouth", "the Ember Fleet", "Stillwater", "the Copper Reef", "Tangleweed",
        };

        static readonly string[] LoreOpenings =
        {
            "Pulled from a cracked vault in {place}.",
            "Traded for a song in a port that no longer exists.",
            "Found lodged in the hull of an Ark that never came home.",
            "Recovered from {place}, still warm.",
            "No crew remembers who first carried it.",
            "Fished out of a wreck by a pilot who would not say where.",
            "It turned up in the hold one morning; nobody signed for it.",
            "Won in a wager against a pirate who swore it was cursed.",
        };

        static readonly string[] LoreClosings =
        {
            "It hums when {crystal} crystals are near, and nothing else will feed it.",
            "Feed it {crystal} and it grows; offer it anything else and it goes cold.",
            "Only {crystal} wakes it. It has never refused a crystal of that kind.",
            "It drinks {crystal} crystals, slower and slower, but it never says no.",
            "The {crystal} glow in its seams brightens a little with every crystal.",
        };

        /// <summary>
        /// Rolls the artifact of <paramref name="kind"/> with <paramref name="seed"/>. Null when the kind has no track it
        /// can be attuned to. <paramref name="origin"/> and <paramref name="nowMs"/> only stamp the record.
        /// </summary>
        public static ArtifactRecord Roll(ArtifactArchetype kind, ulong seed, string origin = "", long nowMs = 0)
        {
            if (kind == null || string.IsNullOrEmpty(kind.Id)) return null;
            var elements = kind.AttunableElements();
            if (elements.Count == 0) return null;
            if (seed == 0) seed = 1;

            var traits = SeededRandom.For(seed, "artifact.traits");
            var element = elements[traits.Next(elements.Count)];
            var r = new ArtifactRecord
            {
                Id = UniqueId.Compose(kind.Id, seed),
                ArchetypeId = kind.Id,
                GeneratorVersion = Version,
                Element = element,
                Aspect = kind.AspectOf(element),
                BaseLevel = Round2(traits.Range(BaseMin, BaseMax)),
                Ceiling = Round2(traits.Range(CeilingMin, CeilingMax)),
                HalfCrystals = Round2(traits.Range(HalfMin, HalfMax)),
                FixedMass = Round2(traits.Range(FixedMin, FixedMax)),
                FixedSpace = Round2(traits.Range(FixedMin, FixedMax)),
                FixedTime = Round2(traits.Range(FixedMin, FixedMax)),
                FixedCharge = Round2(traits.Range(FixedMin, FixedMax)),
                Invested = 0,
                Origin = origin ?? string.Empty,
                MintedAtMs = nowMs,
            };

            var look = SeededRandom.For(seed, "artifact.look");
            r.Hue = Round2(look.Range(0f, 1f));
            r.Facets = 3 + look.Next(7);
            r.Glyph = look.Next(64);

            var words = SeededRandom.For(seed, "artifact.name");
            string place = words.Pick(Places);
            r.Name = words.Pick(Epithets) + " " + (string.IsNullOrEmpty(kind.DisplayName) ? "Artifact" : kind.DisplayName) + " of " + place;

            var lore = SeededRandom.For(seed, "artifact.lore");
            string crystal = element.ToString();
            r.Lore = lore.Pick(LoreOpenings).Replace("{place}", place) + " " + lore.Pick(LoreClosings).Replace("{crystal}", crystal);
            return r;
        }

        /// <summary>Rolls a fresh artifact of <paramref name="kind"/> with a seed nobody has used.</summary>
        public static ArtifactRecord RollNew(ArtifactArchetype kind, string origin, long nowMs) =>
            Roll(kind, SeededRandom.NewSeed(), origin, nowMs);

        /// <summary>
        /// Re-rolls the artifact a unique id names, from its kind and seed: what an id found in a hold or a wreck turns
        /// into when no record came with it (its crystals fed stay with whoever lost it until the backend carries
        /// records between accounts). Null for a bare template id or a kind that does not match.
        /// </summary>
        public static ArtifactRecord FromId(string id, ArtifactArchetype kind, string origin = "", long nowMs = 0)
        {
            if (kind == null || !UniqueId.TrySplit(id, out string template, out ulong seed) || template != kind.Id) return null;
            return Roll(kind, seed, origin, nowMs);
        }

        // Rolled numbers are kept to two decimals, so a record reads the same after a JSON round trip on any runtime.
        static float Round2(float v) => (float)Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }
}

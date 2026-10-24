using System.Text.RegularExpressions;

namespace CosmicShore.UI
{
    /// <summary>
    /// What an ability DOES, as the Hangar's stage shows it: pressing an ability on the Abilities tab plays
    /// this kind's move and effect (the owner, 2026-10-17: "whatever I press the vessel just shoots for every
    /// ability ... all types should be shown"). The stage is a stand-in; TRY flies the real ability.
    /// </summary>
    public enum HangarAbilityKind
    {
        /// <summary>No move of its own (a passive the words do not place): the hull glows.</summary>
        Passive = 0,
        /// <summary>Projectiles from the nose: guns, cannons, rockets, spikes, bombs.</summary>
        Shot = 1,
        /// <summary>An area blast round the hull: a bloom, a shock disc, a ripple.</summary>
        Burst = 2,
        /// <summary>Speed: a surge forward with exhaust streaming from the tail.</summary>
        Boost = 3,
        /// <summary>A jump: the hull blinks ahead and leaves a ghost (fold, slip).</summary>
        Phase = 4,
        /// <summary>Mass laid in the world: blocks set down behind the tail (trails, walls, rings, seeds).</summary>
        Build = 5,
        /// <summary>A mode or stance: the hull settles and a ring locks onto it.</summary>
        Stance = 6,
        /// <summary>Seeing: radar pings spread from the hull (sight, scope, a predicted path).</summary>
        Sense = 7,
        /// <summary>A pull: motes spiral into a point ahead (a dipole, a winch, a steal, a latch).</summary>
        Pull = 8,
        /// <summary>Melee: the hull rolls through a slash arc (cords, a wrecking ball, a joust).</summary>
        Strike = 9,
    }

    /// <summary>
    /// Reads an ability's kind from its authored name and description - the words the Abilities tab shows - so
    /// every vessel, including one added tomorrow, gets a fitting move without anything new to author. The
    /// name is read first (all rules, in order), then the description's opening. Words match whole, with an
    /// optional plural. Tools/Build/check_hangar_ability_kinds.py reads these two tables from this file and
    /// runs them over every shipped ElementalAbilityMap; HangarAbilitiesPage's overrides correct a miss.
    /// </summary>
    public static class HangarAbilityKinds
    {
        /// <summary>How much of the description is read: its opening says what the ability is.</summary>
        public const int DescriptionWindow = 240;

        static readonly (HangarAbilityKind kind, string[] words)[] NameRules =
        {
            (HangarAbilityKind.Sense, new[] { "sight", "scope", "pathfinder", "sonar", "scan", "radar", "targeting" }),
            (HangarAbilityKind.Phase, new[] { "fold", "slip", "blink", "phase", "warp" }),
            (HangarAbilityKind.Stance, new[] { "/", "stance", "dig in", "plant", "weapon systems" }),
            (HangarAbilityKind.Pull, new[] { "dipole", "winch", "steal", "rider", "tether", "grapple", "hook", "lure" }),
            (HangarAbilityKind.Strike, new[] { "wrecking", "joust", "claw", "slash", "blade", "sword" }),
            (HangarAbilityKind.Shot, new[] { "cannon", "rocket", "shot", "spike", "gun", "sting", "missile", "bomb" }),
            (HangarAbilityKind.Burst, new[] { "kabloom", "blast", "obliteration", "ripple", "bloom", "nova", "burst" }),
            (HangarAbilityKind.Build, new[] { "trail", "wall", "slab", "seed", "seeding", "creation", "forge", "switch", "track", "ring", "dust" }),
            (HangarAbilityKind.Boost, new[] { "boost", "afterburner", "rush", "soar", "throttle", "spool", "fuel", "speed", "dash", "skimming", "fill rate" }),
        };

        static readonly (HangarAbilityKind kind, string[] words)[] DescriptionRules =
        {
            (HangarAbilityKind.Sense, new[] { "lights up", "magnified", "eyepiece", "scope", "sight", "drawn as dots", "reveal" }),
            (HangarAbilityKind.Phase, new[] { "ghost", "phase out", "puts you there", "teleport", "blink" }),
            (HangarAbilityKind.Stance, new[] { "toggle", "switches the", "stop to", "dig in", "parked", "brake", "cycle your" }),
            (HangarAbilityKind.Pull, new[] { "black hole", "wormhole", "chain", "latch", "steal", "tether" }),
            (HangarAbilityKind.Strike, new[] { "cord", "slicing", "joust", "wrecking", "ram", "bounce" }),
            (HangarAbilityKind.Shot, new[] { "fire", "shot", "round", "rocket", "cannon", "gun", "bomb", "spike", "projectile", "bullet" }),
            (HangarAbilityKind.Burst, new[] { "blast", "explosion", "bloom", "punch", "ripple", "cone", "disc", "shock" }),
            (HangarAbilityKind.Build, new[] { "trail", "prism", "wall", "slab", "seed", "ring", "rail", "deploy", "mint", "forge", "dust", "wake" }),
            (HangarAbilityKind.Boost, new[] { "boost", "speed", "thrust", "fuel", "momentum", "charge forward", "cruise" }),
        };

        /// <summary>The kind <paramref name="name"/> and <paramref name="description"/> describe.</summary>
        public static HangarAbilityKind Classify(string name, string description)
        {
            if (TryMatch(NameRules, name, out var kind)) return kind;
            string opening = string.IsNullOrEmpty(description) || description.Length <= DescriptionWindow
                ? description
                : description.Substring(0, DescriptionWindow);
            return TryMatch(DescriptionRules, opening, out kind) ? kind : HangarAbilityKind.Passive;
        }

        static bool TryMatch((HangarAbilityKind kind, string[] words)[] rules, string text, out HangarAbilityKind kind)
        {
            kind = HangarAbilityKind.Passive;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var (k, words) in rules)
                foreach (var w in words)
                    if (Matches(text, w)) { kind = k; return true; }
            return false;
        }

        // Whole words with an optional plural; a word that is not a word (the "/" of "Mass / Time Mode") is a
        // plain substring.
        static bool Matches(string text, string word)
        {
            bool wordy = char.IsLetter(word[0]);
            return wordy
                ? Regex.IsMatch(text, $@"\b{Regex.Escape(word)}(?:s|es)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                : text.Contains(word);
        }
    }
}

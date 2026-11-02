using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How a prism burn is DRAWN (<see cref="PrismBurnView"/>). Docs/Shaders/PRISM_EFFECTS.md.</summary>
    public enum PrismBurnStyle : byte
    {
        /// <summary>PrismPaperBurn.shader - cream paper that chars, curls and burns through (the Grizzly's claw).</summary>
        Paper = 0,
        /// <summary>PrismLaserBurn.shader - no flame: a red-hot line peels the skin to domain-coloured ash, which crumbles away (Fulmen).</summary>
        Laser = 1,
    }

    /// <summary>
    /// The FOREST-FIRE rule of a burn: how a burning prism lights its neighbours, and how that dies down.
    /// Pure data + pure maths (EditMode-tested in <c>PrismBurnSpreadTests</c>); <see cref="PrismBurnManager"/> runs it.
    ///
    /// Generation 0 is the prism the source lit (a Fulmen stroke). A generation-g burn rolls, every
    /// <see cref="Interval"/> seconds, <see cref="ChanceAt"/>(g) = Chance * Decay^g for each live neighbour within
    /// <see cref="Radius"/>, lights at most <see cref="MaxPerBurn"/> of them in its life, and a generation
    /// <see cref="MaxGenerations"/> burn never spreads - so a fire is finite by construction
    /// (<see cref="MaxDescendants"/>), and each ring burns shorter (<see cref="SecondsAt"/>).
    /// </summary>
    [System.Serializable]
    public struct PrismBurnSpread
    {
        [Tooltip("Spread generations after the struck prism (0 = no spread). A generation-N burn never spreads.")]
        [Range(0, 6)] public int MaxGenerations;
        [Tooltip("Units round a burning prism's centre its fire can jump.")]
        [Min(0f)] public float Radius;
        [Tooltip("Chance per roll that each neighbour catches, from a generation-0 burn.")]
        [Range(0f, 1f)] public float Chance;
        [Tooltip("Chance multiplier per generation (0.5: half as likely each ring) - the fire DIMINISHES.")]
        [Range(0f, 1f)] public float Decay;
        [Tooltip("Seconds between a burning prism's spread rolls (the first roll is one interval after it is lit).")]
        [Min(0.05f)] public float Interval;
        [Tooltip("Most neighbours one burning prism may light in its life.")]
        [Range(0, 64)] public int MaxPerBurn;
        [Tooltip("Burn seconds multiplier per generation (0.8: each ring burns 20% shorter).")]
        [Range(0.1f, 1f)] public float SecondsScale;

        public bool Enabled => MaxGenerations > 0 && Chance > 0f && Radius > 0f && MaxPerBurn > 0;

        /// <summary>No spread at all.</summary>
        public static PrismBurnSpread None => default;

        /// <summary>The pre-existing (Grizzly claw) rule: every burn rolls the same chance forever - unbounded generations.</summary>
        public static PrismBurnSpread Unbounded(float radius, float chance, float interval) => new()
        {
            MaxGenerations = int.MaxValue, Radius = radius, Chance = chance, Decay = 1f, Interval = interval,
            MaxPerBurn = int.MaxValue, SecondsScale = 1f,
        };

        /// <summary>Fulmen's default: 2 rings, 7 u, 45% then 22.5%, 3 neighbours a burn, each ring 20% shorter.</summary>
        public static PrismBurnSpread FulmenDefault => new()
        {
            MaxGenerations = 2, Radius = 7f, Chance = 0.45f, Decay = 0.5f, Interval = 0.8f, MaxPerBurn = 3, SecondsScale = 0.8f,
        };

        /// <summary>Chance a generation-<paramref name="generation"/> burn lights a neighbour per roll; 0 once it may not spread.</summary>
        public readonly float ChanceAt(int generation)
        {
            if (!Enabled || generation < 0 || generation >= MaxGenerations) return 0f;
            if (Decay >= 1f) return Chance;
            return Chance * Mathf.Pow(Decay, generation);
        }

        /// <summary>May a generation-<paramref name="generation"/> burn still spread?</summary>
        public readonly bool CanSpread(int generation) => ChanceAt(generation) > 0f;

        /// <summary>Burn seconds for a generation-<paramref name="generation"/> burn whose generation-0 burned <paramref name="rootSeconds"/>.</summary>
        public readonly float SecondsAt(float rootSeconds, int generation)
        {
            if (generation <= 0 || SecondsScale >= 1f) return rootSeconds;
            return rootSeconds * Mathf.Pow(Mathf.Max(SecondsScale, 0.01f), generation);
        }

        /// <summary>Upper bound on prisms ONE generation-0 burn can light through spread (MaxPerBurn^1 + ... + ^MaxGenerations).
        /// int.MaxValue for an unbounded rule.</summary>
        public readonly long MaxDescendants()
        {
            if (!Enabled) return 0;
            if (MaxGenerations == int.MaxValue || MaxPerBurn == int.MaxValue) return int.MaxValue;
            long total = 0, ring = 1;
            for (int g = 1; g <= MaxGenerations; g++)
            {
                ring *= MaxPerBurn;
                total += ring;
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return total;
        }
    }
}

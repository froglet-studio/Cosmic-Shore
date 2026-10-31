using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Ark escort</b> (Garrett, 2026-10-29): a voyage of its own where a squad of any three vessels escorts its Ark
    /// through a run of cells on the HyperSea, at a difficulty from 1 (an easy crossing) to 9 (not meant to be survived).
    /// The cells are authored worlds with their own fauna; the difficulty is a tier of numbers applied to all of them.
    /// Nothing here is a scripted threat: a tier only says how many creatures each world seeds and caps, who controls the
    /// world when the Ark arrives, and how fast the Ark's reactor regrows its hull. The food web does the rest
    /// (Docs/Voyages/ARK_ESCORT.md).
    /// </summary>
    [CreateAssetMenu(fileName = "ArkEscort", menuName = "ScriptableObjects/Voyages/Ark Escort")]
    public sealed class ArkEscortSO : ScriptableObject
    {
        public const int MinDifficulty = 1;
        public const int MaxDifficulty = 9;

        [Serializable]
        public sealed class Leg
        {
            [Tooltip("The world's name on the banner as the Ark crosses into it.")]
            public string Name;

            [Tooltip("The world the Ark sails through (a cell config with an environment). Each leg's world must be a " +
                     "different config: the corridor stands each config once.")]
            public CellConfigDataSO World;

            [Tooltip("Harder versions of this world, easiest first (a biome's own 1-4 configs): difficulty 1-9 is spread " +
                     "evenly over them, so the world itself grows meaner as the difficulty rises. Empty = World at every " +
                     "difficulty.")]
            public List<CellConfigDataSO> Ladder = new();

            [Min(0f), Tooltip("This world's share of the tier's threat: its population is the tier's population times this.")]
            public float Threat = 1f;

            [Tooltip("The world starts controlled by a domain opposing the squad's, so its fauna graze the Ark until the " +
                     "squad takes the world. Off: the world starts as the corridor leaves it (friendly in solo play).")]
            public bool Hostile = true;

            [Range(1, 9), Tooltip("The lowest difficulty at which this world can start hostile (below it, it starts as the " +
                                  "corridor leaves it).")]
            public int HostileFrom = 1;

            [Tooltip("The banner line as the Ark crosses into this world (tokens {name}, {leg}, {legs}). Empty shows nothing.")]
            [TextArea] public string Arrival;

            [Min(0f), Tooltip("Variety: how likely this world is to be one of a voyage's worlds when the voyage sails fewer " +
                              "worlds than the escort lists (the hungriest world is always sailed, last).")]
            public float Weight = 1f;

            [Tooltip("Variety: alternative versions of this world (its signature threat variants). Each voyage rolls one " +
                     "by weight; empty = the world above, always.")]
            public List<WorldVariant> Variants = new();
        }

        /// <summary>One version of a leg's world: a voyage's seed rolls one per leg, by weight.</summary>
        [Serializable]
        public sealed class WorldVariant
        {
            [Tooltip("Shown on the card and the arrival banner instead of the leg's name. Empty keeps the leg's name.")]
            public string Name;
            [Tooltip("This variant's world (and its harder rungs). Empty keeps the leg's world and ladder.")]
            public CellConfigDataSO World;
            public List<CellConfigDataSO> Ladder = new();
            [Min(0f)] public float Weight = 1f;
            [Min(0f), Tooltip("Multiplier on the leg's threat (population share) for this variant.")]
            public float ThreatScale = 1f;
        }

        /// <summary>What a voyage modifier changes (applied to the run's copy of the escort, never to an asset).</summary>
        public enum ModifierKind
        {
            /// <summary>Every world's population (seeds and caps) times Factor.</summary>
            Population = 0,
            /// <summary>The reactor's seconds per regrown plate times Factor (above 1: thinner regrowth).</summary>
            RegrowSeconds = 1,
            /// <summary>Every creature's speed times Factor.</summary>
            FaunaSpeed = 2,
            /// <summary>Every armour bite times Factor.</summary>
            ArmourBite = 3,
            /// <summary>Every world starts hostile, at every difficulty.</summary>
            AllHostile = 4,
            /// <summary>The Ark's crawl and entry speeds times Factor (below 1: longer in every world).</summary>
            ArkCrawl = 5,
            /// <summary>The share of a world that pacifies it times Factor (below 1: easier to turn).</summary>
            PacifyShare = 6,
        }

        /// <summary>A named twist one voyage may roll (Garrett, 2026-10-31: every voyage its own experience).</summary>
        [Serializable]
        public sealed class VoyageModifier
        {
            [Tooltip("Shown on the card and the departure banner.")]
            public string Name;
            public ModifierKind Kind;
            public float Factor = 1f;
            [Min(0f)] public float Weight = 1f;
            [Range(1, 9), Tooltip("The lowest difficulty that can roll it.")]
            public int MinDifficulty = 1;
        }

        [Serializable]
        public sealed class Tier
        {
            [Min(0.05f), Tooltip("Multiplier on every world's fauna and flora seeds and caps (before the leg's threat).")]
            public float Population = 1f;

            [Min(0.05f), Tooltip("Seconds per hull prism the Ark's reactor regrows.")]
            public float RegrowSeconds = 2f;

            [Range(0f, 1f), Tooltip("Chance each Hostile leg actually starts hostile. Low tiers leave some worlds friendly.")]
            public float HostileShare = 1f;

            [Min(0.25f), Tooltip("Multiplier on the Ark's CRAWL and ENTRY speeds (the pace inside a world): a slower Ark " +
                                 "spends longer in each world. Open-water cruise is not scaled, so the gaps between " +
                                 "worlds stay short and the Ark never outruns its escort.")]
            public float ArkSpeed = 1f;

            [Range(0f, 0.95f), Tooltip("Optional: the Ark is overrun when its hull falls below this share. Since the escort's " +
                                       "fauna gnaw through the armoured plates (platesAreFood), the hull can truly fall, so " +
                                       "0 (only the true fall, the last plate) is the default; kept for experiments.")]
            public float BreachHull;
        }

        [Header("The run")]
        [SerializeField, Tooltip("The worlds, in the order the Ark crosses them.")]
        List<Leg> legs = new();

        [SerializeField, Tooltip("Difficulty 1 to 9, in order. Missing tiers repeat the last one.")]
        List<Tier> tiers = new();

        [SerializeField, Min(0.1f), Tooltip("Ceiling on a world's population multiplier (leg threat times tier), so the " +
                                            "hardest tier cannot ask a world for more creatures than a frame can carry.")]
        float maxPopulation = 4f;

        [Header("The Ark's pace (cell-aware: cruise the gaps, brake on the membrane, crawl the cytoplasm)")]
        [SerializeField, Tooltip("The escort's speed profile. The tier's ArkSpeed scales Crawl and Entry only. Cruise and " +
                                 "Ceiling stay below every hull's cruise speed: the Ark must never outrun a Squirrel.")]
        ArkSpeedProfile arkPacing = ArkSpeedProfile.Default;

        [SerializeField, Min(0f), Tooltip("Centre-to-centre spacing between the escort's worlds, overriding the Arkway " +
                                          "settings' spacing (0 keeps it). Must exceed two membrane radii (~1200-1300 " +
                                          "each) plus a short gap of open water; the corridor floors it at 2000.")]
        float cellSpacing = 2800f;

        [SerializeField, Tooltip("The escort's worlds let creatures that can gnaw armour (a species' shield / super-shield " +
                                 "bite) wear through shielded and super-shielded mass (Cell.ArmourIsFood), so the Ark's " +
                                 "plates are real food and the hull can truly fall. Off: armour is never food (the old rule).")]
        bool platesAreFood = true;

        [Header("Variety (each voyage's seed rolls its worlds, their variants and its modifiers)")]
        [SerializeField, Min(0), Tooltip("Worlds per voyage, drawn from the legs by weight (the hungriest world always last, " +
                                         "the rest ramped by threat with a little shuffle). 0 = every leg.")]
        int worldsPerVoyage = 4;

        [SerializeField, Range(0f, 0.5f), Tooltip("Shuffle in the ramp: each world's threat is jittered by up to this before " +
                                                  "ordering, so near-equal worlds trade places between voyages.")]
        float rampJitter = 0.15f;

        [SerializeField, Tooltip("Chance of 0, 1 and 2 modifiers on a voyage (normalized).")]
        Vector3 modifierCountWeights = new(0.3f, 0.45f, 0.25f);

        [SerializeField, Tooltip("The modifiers a voyage can roll.")]
        List<VoyageModifier> modifiers = new();

        [SerializeField, Range(0f, 1f), Tooltip("A HOSTILE world is PACIFIED once the squad holds this share of its control volume " +
                                                "(the nucleus claim, or the whole cell without a nucleus): its control is pinned " +
                                                "to the squad, its live fauna re-coloured into the squad's domain, and its fauna " +
                                                "stop grazing the squad's mass, the Ark included. 0 = never pacify.")]
        float pacifyShare = 0.75f;

        [SerializeField, Min(0f), Tooltip("Squad control volume (nucleus claim, cubic units) a world needs before it can be " +
                                          "pacified: a trail crossing an empty hostile nucleus is not a pacification; a " +
                                          "cultivator weaving mass into it is.")]
        float pacifyMinVolume = 25000f;

        [SerializeField, Min(0f), Tooltip("A world pacifies only after squad seats have spent this many seconds (in total) " +
                                          "inside its nucleus: cultivation is deliberate work, not a pass through the core.")]
        float pacifyNucleusSeconds = 40f;

        [Header("Words (every line is authored; empty shows nothing)")]
        [SerializeField, Tooltip("The voyage's name, on the build veil and the card.")]
        string voyageName = "ARK ESCORT";

        [SerializeField, Tooltip("The card's title (token {difficulty}).")]
        string cardTitle = "Escort the Ark - difficulty {difficulty}";

        [SerializeField, Tooltip("The card's line (tokens {legs}, {difficulty}).")]
        [TextArea] string cardLine = "Take any three vessels and see the Ark through {legs} worlds of the HyperSea.";

        [SerializeField, Tooltip("The card's button that lowers the difficulty.")]
        string lowerLabel = "<";

        [SerializeField, Tooltip("The card's button that raises the difficulty.")]
        string raiseLabel = ">";

        [SerializeField, Tooltip("When the Ark sets sail (tokens {legs}, {difficulty}).")]
        [TextArea] string departLine = "ESCORT THE ARK THROUGH {legs} WORLDS";

        [SerializeField, Tooltip("When the Ark clears the last world (tokens {difficulty}, {hull}).")]
        [TextArea] string survivedLine = "THE ARK MADE IT THROUGH";

        [SerializeField, Tooltip("When the Ark falls (tokens {name}, {leg}, {legs}).")]
        [TextArea] string fallenLine = "THE ARK FELL IN {name}";

        [SerializeField, Tooltip("When the hull falls below the tier's breach line and the Ark is overrun (tokens {name}, " +
                                 "{leg}, {legs}, {hull}).")]
        [TextArea] string overrunLine = "THE ARK IS OVERRUN IN {name}";

        public IReadOnlyList<Leg> Legs => legs;
        public IReadOnlyList<VoyageModifier> Modifiers => modifiers;
        public int WorldsPerVoyage { get => worldsPerVoyage; set => worldsPerVoyage = Mathf.Max(0, value); }
        public float RampJitter => rampJitter;
        public Vector3 ModifierCountWeights => modifierCountWeights;

        /// <summary>Replace the legs (a RUNTIME copy only: the rolled voyage's worlds, in order).</summary>
        public void SetLegs(List<Leg> rolled) => legs = rolled ?? new List<Leg>();

        /// <summary>Replace the modifier list (a runtime copy or a test).</summary>
        public void SetModifiers(List<VoyageModifier> list) => modifiers = list ?? new List<VoyageModifier>();
        public IReadOnlyList<Tier> Tiers => tiers;
        public ArkSpeedProfile ArkPacing { get => arkPacing; set => arkPacing = value; }
        public float CellSpacing { get => cellSpacing; set => cellSpacing = Mathf.Max(0f, value); }
        public bool PlatesAreFood { get => platesAreFood; set => platesAreFood = value; }
        public float PacifyShare { get => pacifyShare; set => pacifyShare = Mathf.Clamp01(value); }
        public float PacifyMinVolume { get => pacifyMinVolume; set => pacifyMinVolume = Mathf.Max(0f, value); }
        public float PacifyNucleusSeconds { get => pacifyNucleusSeconds; set => pacifyNucleusSeconds = Mathf.Max(0f, value); }
        public string OverrunLine => overrunLine;
        public float MaxPopulation { get => maxPopulation; set => maxPopulation = Mathf.Max(0.1f, value); }
        public string VoyageName => voyageName;
        public string CardTitle => cardTitle;
        public string CardLine => cardLine;
        public string LowerLabel => lowerLabel;
        public string RaiseLabel => raiseLabel;
        public string DepartLine => departLine;
        public string SurvivedLine => survivedLine;
        public string FallenLine => fallenLine;

        /// <summary>The tier for <paramref name="difficulty"/>, clamped to 1-9; null when no tier is authored.</summary>
        public Tier TierFor(int difficulty)
        {
            if (tiers.Count == 0) return null;
            int i = Mathf.Clamp(difficulty, MinDifficulty, MaxDifficulty) - 1;
            return tiers[Mathf.Min(i, tiers.Count - 1)];
        }

        /// <summary>Leg <paramref name="leg"/>'s population multiplier at <paramref name="difficulty"/>.</summary>
        public float PopulationFor(int leg, int difficulty)
        {
            var tier = TierFor(difficulty);
            if (tier == null || leg < 0 || leg >= legs.Count) return 1f;
            return Mathf.Clamp(tier.Population * legs[leg].Threat, 0.05f, maxPopulation);
        }

        /// <summary>Leg <paramref name="leg"/>'s world at <paramref name="difficulty"/>: its ladder rung, else its World.</summary>
        public CellConfigDataSO WorldFor(int leg, int difficulty)
        {
            if (leg < 0 || leg >= legs.Count) return null;
            var l = legs[leg];
            if (l.Ladder is not { Count: > 0 }) return l.World;
            int d = Mathf.Clamp(difficulty, MinDifficulty, MaxDifficulty) - 1;
            int rung = Mathf.Min(l.Ladder.Count - 1, d * l.Ladder.Count / MaxDifficulty);
            return l.Ladder[rung] ? l.Ladder[rung] : l.World;
        }

        /// <summary>The worlds in leg order at <paramref name="difficulty"/> (the corridor's route indexes into this list).</summary>
        public List<CellConfigDataSO> Worlds(int difficulty)
        {
            var worlds = new List<CellConfigDataSO>(legs.Count);
            for (int i = 0; i < legs.Count; i++) worlds.Add(WorldFor(i, difficulty));
            return worlds;
        }
    }
}

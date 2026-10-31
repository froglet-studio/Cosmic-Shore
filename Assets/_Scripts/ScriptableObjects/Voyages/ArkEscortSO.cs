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

        [SerializeField, Range(0f, 1f), Tooltip("A world is PACIFIED once the squad holds this share of its control volume " +
                                                "(the nucleus claim, or the whole cell without a nucleus): its control is pinned " +
                                                "to the squad, its live fauna re-coloured into the squad's domain, and its fauna " +
                                                "stop grazing the squad's mass, the Ark included. 0 = never pacify.")]
        float pacifyShare = 0.6f;

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
        public IReadOnlyList<Tier> Tiers => tiers;
        public ArkSpeedProfile ArkPacing { get => arkPacing; set => arkPacing = value; }
        public float CellSpacing { get => cellSpacing; set => cellSpacing = Mathf.Max(0f, value); }
        public bool PlatesAreFood { get => platesAreFood; set => platesAreFood = value; }
        public float PacifyShare { get => pacifyShare; set => pacifyShare = Mathf.Clamp01(value); }
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

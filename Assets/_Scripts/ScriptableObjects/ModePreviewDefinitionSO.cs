using CosmicShore.Utility;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Everything a game mode needs to be <b>previewed</b> — shown as a live scale model in the
    /// arcade configure modal, and flown as a short single-player Test Flight in Menu_Main.
    ///
    /// <para>A preview is deliberately NOT the mode: it never instantiates the mode's
    /// <c>MiniGameControllerBase</c>, never spawns a <c>NetworkObject</c>, and never writes
    /// <see cref="GameDataSO"/> (which is the real launch config and replicates to the
    /// party). What it reuses instead is the two things that actually make a mode look and feel
    /// like itself: its <see cref="CellConfigDataSO"/> — the Cell owns the environment, so the
    /// mode's own cell config IS its arena — and its vessel.</para>
    ///
    /// <para>The default authoring is therefore "point at the mode's shipped cell config and
    /// nothing else". <see cref="StructurePrefab"/> exists for the modes whose gameplay-bearing
    /// structure is built by the controller rather than by the cell (Scarab's hoops, Astro
    /// League's goals, HexRace's track): those need a local, non-networked stand-in prop, or
    /// they preview as an empty arena.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "ModePreview_",
        menuName = "ScriptableObjects/Game/Mode Preview",
        order = 2)]
    public class ModePreviewDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The game mode this preview stands in for. One definition per mode; the library " +
                 "resolves by this field.")]
        public GameModes Mode;

        [Tooltip("Why this preview is authored the way it is - a spawn distance that had to " +
                 "clear a cage, a target that had to be small, a mode that previews open-ended " +
                 "because its objective needs an opponent. Editor notes only; nothing reads it.")]
        [TextArea(2, 5)]
        public string Notes;

        [Header("World")]
        [Tooltip("The cell the preview swaps the menu world for. Point this at the MODE'S OWN " +
                 "CellConfigDataSO - that is the whole point, and it is why most modes need no " +
                 "new assets. Author a lighter variant only if the shipped one is too heavy to " +
                 "run alongside the menu, and re-measure its PhaseThresholds if you do " +
                 "(FrogletTools > Ecology > Measure Cell Environment Baselines): a small world " +
                 "inheriting a big world's volume ladder pins at Frenzy immediately.")]
        public CellConfigDataSO PreviewCell;

        [Tooltip("OPTIONAL local-only prop for a mode whose gameplay structure is built by its " +
                 "controller instead of by its cell (hoops, goals, a track). Instantiated at the " +
                 "cell centre after the world has bloomed in, and destroyed on exit. It MUST NOT " +
                 "carry a NetworkObject - Menu_Main hosts the party, so anything networked here " +
                 "lands on everyone.")]
        public GameObject StructurePrefab;

        [Header("Vessel")]
        [Tooltip("Hull the preview flies. Leave as Any to inherit the mode's own vessel list " +
                 "from its SO_ArcadeGame (which is what a vessel-locked mode already declares), " +
                 "or Random to keep whatever the player is already flying.")]
        public VesselClassType Vessel = VesselClassType.Any;

        [Header("Objective")]
        [Tooltip("One line telling the player what to do. Shown on the preview HUD for the whole " +
                 "flight - a preview has no countdown, no rounds and no tutorial.")]
        [TextArea(1, 3)]
        public string ObjectiveText = "Fly around. Get a feel for it.";

        [Tooltip("The stat the preview counts, read off the local player's own RoundStats. Same " +
                 "metric the mode scores on, so the number the player watches here is the number " +
                 "they will watch in the real game.")]
        public ScoringMetric ObjectiveMetric = ScoringMetric.Crystals;

        [Tooltip("Hitting this ends the preview as a success. Keep it SMALL - a taste, not a " +
                 "match. 0 means there is no target and the preview runs until the timer expires " +
                 "or the player leaves.")]
        [Min(0)] public int ObjectiveTarget = 3;

        [Tooltip("Hard time limit in seconds. The preview always ends on its own so a player " +
                 "cannot get stranded in it. 0 means no limit (they leave when they leave).")]
        [Min(0f)] public float DurationSeconds = 90f;

        [Header("Spawn")]
        [Tooltip("How far outside the cell's nucleus the vessel is placed, facing the core. " +
                 "Mirrors ServerPlayerVesselInitializer.spawnDistanceOutsideNucleus so a preview " +
                 "opens on the same framing the real mode does.")]
        [Min(0f)] public float SpawnDistanceOutsideNucleus = 70f;

        [Header("Diorama (modal preview window)")]
        [Tooltip("Samples taken across the environment for the modal's scale model. The " +
                 "silhouette is what reads at thumbnail size, so ~1k carries it; higher just " +
                 "costs vertices (24 per sample).")]
        [Min(64)] public int DioramaPointBudget = 900;

        [Tooltip("Fraction of the environment's mass kept when filtering down to its signature " +
                 "structures. 1 keeps everything.")]
        [Range(0.05f, 1f)] public float DioramaSignatureCoverage = 1f;

        [Tooltip("Degrees per second the modal's scale model turns. It is a thing you watch, so " +
                 "it must move - but slowly enough to read.")]
        public float DioramaSpinRate = 14f;

        /// <summary>
        /// True when this definition can actually be flown. A definition with no cell has
        /// nothing to swap the menu world for, so the Test Flight button must stay hidden
        /// rather than entering a preview that lands the player in the menu world with the
        /// chrome gone.
        /// </summary>
        public bool CanTestFlight => PreviewCell != null;
    }
}

using System.Collections.Generic;
using CosmicShore.Gameplay;
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

        [Tooltip("OPTIONAL per-intensity arenas, index 0 = intensity 1. This is the same shape " +
                 "the mode's own scene uses (Cell.CellTypeChoiceOptions.IntensityWise over a " +
                 "CellConfigs list), so a mode that already ships four configs is authored here " +
                 "by dropping the same four in the same order - and the preview then CHANGES when " +
                 "the player moves the intensity row, which is the whole reason the row sits next " +
                 "to the window.\n\n" +
                 "Empty means the mode previews the same arena at every intensity, which is " +
                 "correct for the modes whose intensity is not an arena at all (Skim Race's track " +
                 "length, the Maelstrom's pool). PreviewCell is then the one arena; when this " +
                 "list IS authored, PreviewCell is the fallback for an intensity past its end.")]
        public List<CellConfigDataSO> PreviewCellsByIntensity = new();

        [Tooltip("OPTIONAL local-only prop for a mode whose gameplay structure is built by its " +
                 "controller instead of by its cell (hoops, goals, a track). Instantiated at the " +
                 "cell centre after the world has bloomed in, and destroyed on exit. It MUST NOT " +
                 "carry a NetworkObject - Menu_Main hosts the party, so anything networked here " +
                 "lands on everyone.")]
        public GameObject StructurePrefab;

        [Tooltip("OPTIONAL scene-built environment, index 0 = intensity 1: the SpawnableBase " +
                 "prefabs the mode's own scene SegmentSpawner stands at match start (Joust and " +
                 "Scurry author one per intensity; Skim Race authors ONE intensity-aware waypoint " +
                 "track that serves all four). The card's scale model samples these the same way " +
                 "it samples an authored EnvironmentPrefab - generation is pure math, no prisms - " +
                 "so a mode whose arena is a TRACK rather than a cell finally shows it.\n\n" +
                 "A single entry serves every intensity (and is handed the intensity, which is " +
                 "how the waypoint track varies); a list is clamped like PreviewCellsByIntensity. " +
                 "Authored by Tools/Build/author_preview_tracks.py from the scenes' own spawners.")]
        public List<SpawnableBase> TrackSpawnablesByIntensity = new();

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

        /// <summary>
        /// True when this definition can actually be flown. A definition with no cell has
        /// nothing to swap the menu world for, so the Test Flight button must stay hidden
        /// rather than entering a preview that lands the player in the menu world with the
        /// chrome gone.
        /// </summary>
        public bool CanTestFlight => PreviewCell != null || ResolveCell(1) != null;

        /// <summary>
        /// The arena for an intensity: the <see cref="PreviewCellsByIntensity"/> entry when one is
        /// authored, else <see cref="PreviewCell"/>.
        ///
        /// <para>An intensity past the end of the list CLAMPS to the last entry, matching
        /// <c>Cell.IntensityIndex</c> exactly - a mode offering four intensities against two
        /// authored arenas serves the same arena for 3 and 4 in the real scene, and a preview that
        /// disagreed with that would be lying about the game.</para>
        /// </summary>
        public CellConfigDataSO ResolveCell(int intensity)
        {
            if (PreviewCellsByIntensity != null && PreviewCellsByIntensity.Count > 0)
            {
                int index = Mathf.Clamp(Mathf.Max(1, intensity) - 1, 0, PreviewCellsByIntensity.Count - 1);
                var config = PreviewCellsByIntensity[index];
                if (config) return config;
            }
            return PreviewCell;
        }

        /// <summary>
        /// The scene-built structure for an intensity - the same clamp rule as
        /// <see cref="ResolveCell"/>, with one deliberate difference: a SINGLE entry is not a
        /// fallback but the whole authoring, because an intensity-aware spawnable (Skim Race's
        /// waypoint track) is one asset that draws four different tracks.
        /// </summary>
        public SpawnableBase ResolveTrackSpawnable(int intensity)
        {
            if (TrackSpawnablesByIntensity == null || TrackSpawnablesByIntensity.Count == 0)
                return null;

            int index = Mathf.Clamp(Mathf.Max(1, intensity) - 1, 0, TrackSpawnablesByIntensity.Count - 1);
            return TrackSpawnablesByIntensity[index];
        }

        /// <summary>
        /// True when moving the intensity row actually changes the arena. The preview is only
        /// torn down and rebuilt when this says the world would differ - rebuilding an identical
        /// satellite cell costs a multi-second build and a networked hull swap for nothing.
        /// </summary>
        public bool ArenaVariesByIntensity =>
            (PreviewCellsByIntensity != null && PreviewCellsByIntensity.Count > 1) ||
            TrackVariesByIntensity;

        /// <summary>
        /// A multi-entry track list varies by construction. A SINGLE waypoint track also varies -
        /// intensity picks which authored waypoint set it draws - and that is exactly the case the
        /// cell-only test was blind to: Skim Race runs the Barren cell at every intensity, so
        /// "same cell" said "same arena" while the track changed completely.
        /// </summary>
        public bool TrackVariesByIntensity =>
            TrackSpawnablesByIntensity != null &&
            (TrackSpawnablesByIntensity.Count > 1 ||
             (TrackSpawnablesByIntensity.Count == 1 &&
              TrackSpawnablesByIntensity[0] is SpawnableWaypointTrack));

        /// <summary>
        /// Whether the preview CONTENT differs between two intensities - the one question the
        /// session's rebuild-skip actually asks. Kept here so every axis the arena can vary on
        /// (cell, track - and whatever comes next) is answered in one place instead of the
        /// session growing a comparison per axis.
        /// </summary>
        public bool ArenaDiffers(int intensityA, int intensityB)
        {
            if (intensityA == intensityB) return false;
            if (ResolveCell(intensityA) != ResolveCell(intensityB)) return true;
            if (ResolveTrackSpawnable(intensityA) != ResolveTrackSpawnable(intensityB)) return true;

            // One spawnable serving several intensities differs when it is intensity-aware.
            return ResolveTrackSpawnable(intensityA) is SpawnableWaypointTrack;
        }
    }
}

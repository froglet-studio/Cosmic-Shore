namespace CosmicShore.Data
{
    /// <summary>
    /// Which "fun type" a Switchback (Dolphin time game, <see cref="GameModes.Switchback"/>) run plays: a family of
    /// intensity ladders picked on the launch panel before the intensity (Yash, 2026-10-28). Each fun type x
    /// intensity (1-4) is its own world, so the three plans can be played and compared side by side.
    /// <list type="bullet">
    /// <item><see cref="Baseline"/>: the shipped ladder, unchanged.</item>
    /// <item><see cref="Plan1_LevelShapes"/>: a hairpin ladder, a corner pattern per intensity and needle rings.</item>
    /// <item><see cref="Plan2_LiveFeatures"/>: charge ribbons, moving danger prisms and hairpin shortcut rings.</item>
    /// <item><see cref="Plan3_ParMedals"/>: the shipped course, with a par time, split toasts, a medal and a
    /// personal best.</item>
    /// <item><see cref="Combined"/>: Plan 1's course, Plan 2's features and Plan 3's medals together.</item>
    /// </list>
    /// See <c>Assets/_Scripts/Controller/Arcade/Switchback/SWITCHBACK.md</c>, "Fun types".
    /// </summary>
    // Explicit values: never renumber (PlayerPrefs keys and the course broadcast carry the number).
    public enum SwitchbackFunType
    {
        Baseline = 0,
        Plan1_LevelShapes = 1,
        Plan2_LiveFeatures = 2,
        Plan3_ParMedals = 3,
        Combined = 4,
    }

    /// <summary>What each <see cref="SwitchbackFunType"/> switches on, and its launch-panel label.</summary>
    public static class SwitchbackFunTypes
    {
        /// <summary>The order the launch panel cycles through (Baseline first, then plans 1, 3, 2, then Combined).</summary>
        public static readonly SwitchbackFunType[] PickerOrder =
        {
            SwitchbackFunType.Baseline,
            SwitchbackFunType.Plan1_LevelShapes,
            SwitchbackFunType.Plan3_ParMedals,
            SwitchbackFunType.Plan2_LiveFeatures,
            SwitchbackFunType.Combined,
        };

        public static SwitchbackFunType Sanitize(int value) =>
            value >= 0 && value <= (int)SwitchbackFunType.Combined ? (SwitchbackFunType)value : SwitchbackFunType.Baseline;

        /// <summary>Plan 1's course generator: hairpin ladder, corner patterns, needle rings.</summary>
        public static bool HasLevelShapes(SwitchbackFunType t) =>
            t == SwitchbackFunType.Plan1_LevelShapes || t == SwitchbackFunType.Combined;

        /// <summary>Plan 2's live features: charge ribbons, moving danger, shortcut rings.</summary>
        public static bool HasLiveFeatures(SwitchbackFunType t) =>
            t == SwitchbackFunType.Plan2_LiveFeatures || t == SwitchbackFunType.Combined;

        /// <summary>Plan 3's par time, splits, medal and personal best.</summary>
        public static bool HasParMedals(SwitchbackFunType t) =>
            t == SwitchbackFunType.Plan3_ParMedals || t == SwitchbackFunType.Combined;

        public static string Label(SwitchbackFunType t) => t switch
        {
            SwitchbackFunType.Plan1_LevelShapes => "LEVEL SHAPES",
            SwitchbackFunType.Plan2_LiveFeatures => "LIVE FEATURES",
            SwitchbackFunType.Plan3_ParMedals => "PAR & MEDALS",
            SwitchbackFunType.Combined => "COMBINED",
            _ => "CLASSIC",
        };

        /// <summary>
        /// The fixed course seed of a world (fun type x intensity) when "fixed seed" testing is on: the same world
        /// always flies the same course, so two builds or two plans can be compared run for run. Never 0
        /// (0 means "random" to the course seed pickers).
        /// </summary>
        public static int WorldSeed(SwitchbackFunType t, int intensity) =>
            45_000 + (int)t * 100 + (intensity < 1 ? 1 : intensity > 4 ? 4 : intensity);

        /// <summary>The PlayerPrefs key of a world's personal best (seconds).</summary>
        public static string PersonalBestKey(SwitchbackFunType t, int intensity) =>
            $"Switchback.PB.{t}.I{(intensity < 1 ? 1 : intensity > 4 ? 4 : intensity)}";
    }
}

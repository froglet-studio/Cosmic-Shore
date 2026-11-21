using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// A voyage's difficulty, 1 to 9 (Shombith, 2026-11-13, Frogma Rev 100: "For Voyages, they dont use intensity rather
    /// they use the difficulty level 1-9"; Docs/MENU_REDESIGN_B.md section 201). The board still prices a job by its
    /// course's intensity (1-4, <see cref="VoyageJob.Intensity"/>); the pilot who brings it recommends the difficulty that
    /// intensity stands for, any other can be picked on the voyage page, and the crystals follow the pick:
    /// <c>pay x (0.55 + 0.1 x difficulty)</c>, scaled so the recommended difficulty pays what the board priced. Pure, so
    /// every rule runs in an edit-mode test.
    /// </summary>
    public static class VoyageDifficulty
    {
        public const int Min = 1, Max = 9;

        /// <summary><paramref name="difficulty"/> kept inside 1-9.</summary>
        public static int Clamp(int difficulty) => Math.Max(Min, Math.Min(Max, difficulty));

        /// <summary>The difficulty a pilot recommends for a course of <paramref name="intensity"/> (1-4): 2, 4, 6, 8.</summary>
        public static int Recommended(int intensity) => Clamp(Math.Max(1, intensity) * 2);

        /// <summary>The course intensity (1-4) a voyage sails at <paramref name="difficulty"/>: 1-2 is 1, 3-4 is 2, 5-6 is
        /// 3, 7-9 is 4.</summary>
        public static int IntensityOf(int difficulty) => Math.Min(4, (Clamp(difficulty) + 1) / 2);

        /// <summary>The share of a job's base pay a difficulty earns (the board's rule).</summary>
        public static double Share(int difficulty) => 0.55 + 0.1 * Clamp(difficulty);

        /// <summary>
        /// What a job the board priced at <paramref name="pay"/> for its <paramref name="recommended"/> difficulty pays at
        /// <paramref name="difficulty"/>: at least 1 when it paid anything.
        /// </summary>
        public static int PayAt(int pay, int recommended, int difficulty)
        {
            if (pay <= 0) return 0;
            double scaled = pay * Share(difficulty) / Share(recommended);
            return Math.Max(1, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// A copy of <paramref name="job"/> set to sail at <paramref name="difficulty"/>: its course intensity and its pay
        /// follow the pick. The board's own job is not changed.
        /// </summary>
        public static VoyageJob At(VoyageJob job, int difficulty)
        {
            if (job == null) return null;
            int recommended = Recommended(job.Intensity);
            difficulty = Clamp(difficulty);
            var copy = job.MemberwiseCloneJob();
            copy.Intensity = IntensityOf(difficulty);
            copy.Pay = PayAt(job.Pay, recommended, difficulty);
            copy.Difficulty = difficulty;
            return copy;
        }
    }
}

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The course VARIANT the next match must lay instead of drawing one, or -1 for "draw as usual": the
    /// variant twin of <see cref="CourseSeedOverride"/>. A mode whose course is a POOL of authored circuits
    /// (Regatta's <see cref="RegattaCoursePoolSO"/>) draws one per match on the host; the match replay sets
    /// this before it loads a recorded match's scene so the replay lays the circuit the match was flown on,
    /// and a benchmark sets it to race one circuit on purpose. Read only on the host, where the draw happens.
    /// </summary>
    public static class CourseVariantOverride
    {
        /// <summary>The pool entry to lay (0-based), or -1.</summary>
        public static int Forced = -1;

        /// <summary><see cref="Forced"/> when it names one of <paramref name="count"/> entries, else -1.</summary>
        public static int Pick(int count) => Forced >= 0 && Forced < count ? Forced : -1;
    }
}

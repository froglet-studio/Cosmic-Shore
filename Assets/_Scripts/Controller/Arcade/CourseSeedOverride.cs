namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The course seed the NEXT match must use instead of drawing one, or 0 for "draw as usual". The match
    /// replay sets it before it loads a recorded match's scene (<c>Docs/REPLAYS.md</c> §6.3), so Skim Race's
    /// track and a gate race's course are the ones the match was flown on. A controller reads it where it
    /// would draw its seed and an authored (non-zero) serialized seed still wins, exactly as before.
    /// </summary>
    public static class CourseSeedOverride
    {
        /// <summary>The seed to use, or 0.</summary>
        public static int Forced;

        /// <summary><paramref name="authored"/> when set, else <see cref="Forced"/> when set, else a fresh draw.</summary>
        public static int Pick(int authored)
        {
            if (authored != 0) return authored;
            if (Forced != 0) return Forced;
            return UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }
    }
}

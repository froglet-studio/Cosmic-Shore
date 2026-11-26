namespace CosmicShore.Player
{
    /// <summary>The judges' fixed tick, from JUDGE_DT (seconds; default 1/60). A run at 1/30 measures what a 30 fps device
    /// plays: some AI loops are frame-rate dependent (the Dolphin read 2x the Brood Rush field at 30 fps, par at 60).</summary>
    internal static class JudgeDt
    {
        public static readonly float Dt = float.TryParse(System.Environment.GetEnvironmentVariable("JUDGE_DT"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0
            ? d : 1f / 60f;
    }
}

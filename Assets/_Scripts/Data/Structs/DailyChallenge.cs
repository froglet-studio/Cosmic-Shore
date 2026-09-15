using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// <b>THE LEGACY (PlayFab-era) daily challenge's value type. Not the live one.</b>
    ///
    /// <para>The live feature is <see cref="WeeklyChallenge"/> — one curated objective per UTC week,
    /// UGS Cloud Save, <c>WeeklyChallengeService</c>, <c>Docs/WEEKLY_CHALLENGE.md</c>. This type
    /// belongs to <c>DailyChallengeSystem</c> and the PlayFab ticket/reward cluster around it,
    /// which is <b>inert</b>: nothing reads it, it is in no scene, and it is kept only because its
    /// reward ladder is an idea worth reviving. Do not wire both.</para>
    ///
    /// <para><b>Why it exists at all.</b> The rename to weekly took the shared struct with it, and
    /// the dead system stopped compiling — which is the useful half of the discovery: those two
    /// features were sharing a type, so "the legacy cluster is separate" was true of its names and
    /// not of its data. Splitting the struct is what actually separates them. The alternative —
    /// pointing the dead system at <see cref="WeeklyChallenge"/> — would re-tie a retired feature
    /// to a live one through the type system, which is precisely how two systems come to look like
    /// one.</para>
    ///
    /// <para>It carries only what <c>DailyChallengeSystem</c> reads: the mode and the intensity it
    /// rolls. It is deliberately NOT a copy of <see cref="WeeklyChallenge"/> — a dead feature's data
    /// should shrink to what it uses, not track a live one's shape.</para>
    /// </summary>
    [Serializable]
    public struct DailyChallenge
    {
        public GameModes GameMode;
        public int Intensity;
    }
}

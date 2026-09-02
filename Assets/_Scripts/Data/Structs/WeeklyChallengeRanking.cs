using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// One row of the weekly challenge leaderboard, already resolved into what a UI row needs.
    ///
    /// <para>Deliberately a project type rather than the UGS entry it is built from: the service
    /// owns every piece of Unity Gaming Services API surface, and a view that took a
    /// <c>Unity.Services.Leaderboards.Models.LeaderboardEntry</c> would drag the SDK into the UI
    /// layer and break the day the package renames a field. It also keeps the two OTHER
    /// <c>LeaderboardEntry</c> types already in this project (the PlayFab one and
    /// <see cref="LeaderboardEntry"/>) from becoming three names for one idea.</para>
    /// </summary>
    [Serializable]
    public struct WeeklyChallengeRanking
    {
        /// <summary>1-based, as UGS ranks it. 0 when the player has no entry.</summary>
        public int Rank;

        /// <summary>UGS player id — the one stable key. Names are not unique.</summary>
        public string PlayerId;

        /// <summary>Display name as UGS holds it, already stripped of its <c>#1234</c> suffix.</summary>
        public string PlayerName;

        /// <summary>
        /// SECONDS taken to complete the objective. Lower is better — the whole leaderboard is
        /// "who finished it first".
        /// </summary>
        public double Seconds;

        /// <summary>True for the signed-in player's own row, so a view can mark it.</summary>
        public bool IsLocalPlayer;

        /// <summary>mm:ss.cc — the reading a race time wants.</summary>
        public string FormatTime() => FormatSeconds(Seconds);

        /// <summary>
        /// mm:ss.cc, clamped at zero.
        ///
        /// <para>Centiseconds rather than whole seconds because this is a race and ties would
        /// otherwise be routine: a 60-second challenge at whole-second resolution has 60 possible
        /// scores, so a full board would be mostly ties broken by who submitted first — which
        /// reads as an arbitrary order.</para>
        ///
        /// <para><b>Rounded to the nearest centisecond, never floored, and the whole value is
        /// converted at once.</b> The obvious implementation — take the whole seconds, then
        /// <c>floor((seconds - whole) * 100)</c> — prints <c>47.3</c> as <b>0:47.29</b>, because
        /// the double nearest 47.3 is a hair below it and the subtraction keeps the whole error.
        /// Converting to centiseconds in one step and rounding removes both the subtraction and
        /// the bias. The 0.005 s a round can add is a time displayed marginally SLOWER than the
        /// run, which is the safe direction for a leaderboard — a floor would print times the
        /// player did not achieve.</para>
        /// </summary>
        public static string FormatSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0d) seconds = 0d;
            if (double.IsInfinity(seconds)) return "--:--.--";

            long cs = (long)Math.Round(seconds * 100d, MidpointRounding.AwayFromZero);
            if (cs < 0L) cs = 0L;

            return $"{cs / 6000L}:{cs / 100L % 60L:D2}.{cs % 100L:D2}";
        }
    }
}

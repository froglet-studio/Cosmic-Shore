using System;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The birth year the first-run age screen recorded, kept for fair play (Threat Board R13;
    /// Docs/AntiCheat/README.md §4.6): minors are never penalised automatically, and the server can only tell who is
    /// a minor if it knows the year. Until 2026-10-25 the age screen kept only "13 or older", so nobody could tell a
    /// 15-year-old from an adult.
    ///
    /// <para>Only the year is kept, on this device, and it is sent with each match report
    /// (<see cref="ServerMatchReports"/>). The server keeps the youngest year it has seen and treats anyone not
    /// surely 18 as a minor, so a year that is never entered (players from before this change) is a minor too.</para>
    /// </summary>
    public static class FairPlayAge
    {
        public const string BirthYearPrefKey = "FairPlayBirthYear";
        public const int AdultAge = 18;

        /// <summary>Records the year the age screen accepted. The youngest year entered on this device wins.</summary>
        public static void Record(int birthYear)
        {
            int stored = BirthYear;
            if (stored != 0 && birthYear <= stored) return;
            PlayerPrefs.SetInt(BirthYearPrefKey, birthYear);
            PlayerPrefs.Save();
        }

        /// <summary>The recorded birth year, or 0 when the age screen has not recorded one.</summary>
        public static int BirthYear => PlayerPrefs.GetInt(BirthYearPrefKey, 0);

        /// <summary>
        /// True unless the player is surely an adult by the year alone (born in Y: surely 18 from Y + 19), or when no
        /// year is recorded. The same rule as the server's <c>treatAsMinor</c> in match-report.js.
        /// </summary>
        public static bool TreatAsMinor(int birthYear, int nowYear) =>
            birthYear <= 0 || nowYear - birthYear - 1 < AdultAge;

        public static bool TreatAsMinorNow => TreatAsMinor(BirthYear, DateTime.UtcNow.Year);
    }
}

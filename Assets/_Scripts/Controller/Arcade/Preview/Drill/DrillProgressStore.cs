using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The drill's account memory: the two "first time" keys (D7), the Mentor tips already seen,
    /// and best practice laps (Docs/ModePreview/TRAINING_PLAN.md §3, §4.5).
    ///
    /// <para>Cloud (<see cref="UGSDataService.DrillRepo"/>, key <c>DRILL_PROGRESS</c>) is shared
    /// across devices, with a <see cref="PlayerPrefs"/> mirror so the gate still answers offline
    /// and before the cloud load lands. Because every fact only ever grows, a read MERGES the two
    /// (OR / union / min) rather than picking one - so a slow cloud load can never re-lock a
    /// Lesson a player already finished on this machine, and finishing one elsewhere still
    /// counts here.</para>
    /// </summary>
    public static class DrillProgressStore
    {
        const string AnyLessonKey = "DRILL_CompletedAnyLesson";
        const string TwoThumbLessonKey = "DRILL_CompletedTwoThumbLesson";
        const string SeenTipsKey = "DRILL_SeenTipIds";
        const char SeenTipsSeparator = '|';
        static string BestLapKey(GameModes mode, VesselClassType vessel) => $"DRILL_BestLap_{(int)mode}_{(int)vessel}";

        // Null while the backend gate is closed: every read then uses the mirror alone and every
        // cloud write is a no-op (local-only testing) - QuestProgressStore's rule.
        static DrillProgressRepository Repo =>
            ProgressionBackendGate.CloudEnabled && UGSDataService.Instance != null
                ? UGSDataService.Instance.DrillRepo
                : null;

        static DrillProgressCloudData Loaded
        {
            get
            {
                var repo = Repo;
                return repo != null && repo.IsLoaded ? repo.Data : null;
            }
        }

        // ── The first-time rule (D7) ─────────────────────────────────────

        /// <summary>
        /// May this Lesson be skipped (after the delay)? The account's first Lesson, whatever the
        /// ship, never; the first two-thumb Lesson never; everything else yes. A pure function so
        /// the rule is testable without a store.
        /// </summary>
        public static bool IsLessonSkippable(FlightScheme scheme, bool completedAnyLesson, bool completedTwoThumbLesson) =>
            completedAnyLesson && (scheme == FlightScheme.OneThumb || completedTwoThumbLesson);

        public static bool CompletedAnyLesson =>
            PlayerPrefs.GetInt(AnyLessonKey, 0) == 1 || (Loaded?.CompletedAnyLesson ?? false);

        public static bool CompletedTwoThumbLesson =>
            PlayerPrefs.GetInt(TwoThumbLessonKey, 0) == 1 || (Loaded?.CompletedTwoThumbLesson ?? false);

        public static bool IsLessonSkippable(FlightScheme scheme) =>
            IsLessonSkippable(scheme, CompletedAnyLesson, CompletedTwoThumbLesson);

        /// <summary>
        /// A Lesson was FINISHED on a ship of <paramref name="scheme"/>. Skipping never calls this:
        /// skipping is only offered once the key is already set.
        /// </summary>
        public static void RecordLessonCompleted(FlightScheme scheme)
        {
            bool twoThumb = scheme == FlightScheme.TwoThumb;
            PlayerPrefs.SetInt(AnyLessonKey, 1);
            if (twoThumb) PlayerPrefs.SetInt(TwoThumbLessonKey, 1);
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo == null || !repo.IsLoaded) return;
            bool changed = !repo.Data.CompletedAnyLesson || (twoThumb && !repo.Data.CompletedTwoThumbLesson);
            repo.Data.CompletedAnyLesson = true;
            if (twoThumb) repo.Data.CompletedTwoThumbLesson = true;
            if (changed) repo.MarkDirty();
        }

        // ── Seen tips ────────────────────────────────────────────────────

        /// <summary>Every tip id this account has been shown (mirror and cloud, merged).</summary>
        public static HashSet<string> SeenTipIds()
        {
            var seen = new HashSet<string>();
            string mirror = PlayerPrefs.GetString(SeenTipsKey, string.Empty);
            if (!string.IsNullOrEmpty(mirror))
                foreach (var id in mirror.Split(SeenTipsSeparator))
                    if (!string.IsNullOrEmpty(id)) seen.Add(id);

            var cloud = Loaded?.SeenTipIds;
            if (cloud != null)
                foreach (var id in cloud)
                    if (!string.IsNullOrEmpty(id)) seen.Add(id);
            return seen;
        }

        public static void MarkTipSeen(string tipId)
        {
            if (string.IsNullOrEmpty(tipId) || tipId.IndexOf(SeenTipsSeparator) >= 0) return;

            var seen = SeenTipIds();
            if (seen.Add(tipId))
            {
                PlayerPrefs.SetString(SeenTipsKey, string.Join(SeenTipsSeparator.ToString(), seen));
                PlayerPrefs.Save();
            }

            var repo = Repo;
            if (repo == null || !repo.IsLoaded || repo.Data.SeenTipIds.Contains(tipId)) return;
            repo.Data.SeenTipIds.Add(tipId);
            repo.MarkDirty();
        }

        // ── Best practice lap ────────────────────────────────────────────

        /// <summary>The best practice lap for this card and hull, or false when none is recorded.</summary>
        public static bool TryGetBestLap(GameModes mode, VesselClassType vessel, out float seconds)
        {
            seconds = PlayerPrefs.GetFloat(BestLapKey(mode, vessel), 0f);
            var cloud = Loaded?.BestLaps;
            if (cloud != null)
                foreach (var lap in cloud)
                    if (lap != null && lap.Mode == mode && lap.Vessel == vessel && lap.Seconds > 0f &&
                        (seconds <= 0f || lap.Seconds < seconds))
                        seconds = lap.Seconds;
            return seconds > 0f;
        }

        /// <summary>Record a lap; true when it is a new best.</summary>
        public static bool ReportLap(GameModes mode, VesselClassType vessel, float seconds)
        {
            if (seconds <= 0f) return false;
            bool hadBest = TryGetBestLap(mode, vessel, out float best);
            if (hadBest && seconds >= best) return false;

            PlayerPrefs.SetFloat(BestLapKey(mode, vessel), seconds);
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo != null && repo.IsLoaded)
            {
                DrillBestLap entry = null;
                foreach (var lap in repo.Data.BestLaps)
                    if (lap != null && lap.Mode == mode && lap.Vessel == vessel) { entry = lap; break; }
                if (entry == null)
                    repo.Data.BestLaps.Add(new DrillBestLap { Mode = mode, Vessel = vessel, Seconds = seconds });
                else
                    entry.Seconds = seconds;
                repo.MarkDirty();
            }
            return true;
        }

        /// <summary>Forget this machine's mirror (test and tooling hygiene; the cloud copy is untouched).</summary>
        public static void ResetLocal()
        {
            PlayerPrefs.DeleteKey(AnyLessonKey);
            PlayerPrefs.DeleteKey(TwoThumbLessonKey);
            PlayerPrefs.DeleteKey(SeenTipsKey);
            PlayerPrefs.Save();
        }
    }
}

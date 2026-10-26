using System;
using System.Collections.Generic;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// The Cloud Save mirror of this player's replay SUMMARIES - never their frames - so the replay
    /// browser can list a match recorded on another device. Frames stay on the device that recorded
    /// them (decided 2026-10-15: "local blob + cloud metadata").
    /// </summary>
    [Serializable]
    public class ReplayIndexCloudData
    {
        /// <summary>Newest first, at most the cap the service passes to <see cref="Upsert"/>.</summary>
        public List<ReplaySummary> Entries = new();

        public int Version = 1;

        /// <summary>
        /// Insert or replace <paramref name="summary"/>, keep the list newest first, and trim it to
        /// <paramref name="cap"/>. Pure, so the index's one rule is tested without Cloud Save.
        /// </summary>
        public static void Upsert(List<ReplaySummary> entries, ReplaySummary summary, int cap)
        {
            if (entries == null || summary == null) return;

            entries.RemoveAll(e => e == null || ReplaySummary.SameReplay(e, summary));
            entries.Add(summary);
            entries.Sort((a, b) => b.RecordedAtUtcTicks.CompareTo(a.RecordedAtUtcTicks));

            int keep = Math.Max(0, cap);
            if (entries.Count > keep) entries.RemoveRange(keep, entries.Count - keep);
        }

        /// <summary>Remove one replay from the index. True when it was there.</summary>
        public static bool Remove(List<ReplaySummary> entries, ReplaySummary summary)
        {
            if (entries == null || summary == null) return false;
            return entries.RemoveAll(e => ReplaySummary.SameReplay(e, summary)) > 0;
        }
    }
}

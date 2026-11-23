using System.Collections.Generic;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for the replay index (summaries only).
    /// Cloud key: "REPLAY_INDEX".
    ///
    /// <para>Owned by <see cref="ReplayService"/> rather than by <c>UGSDataService</c>: nothing reads
    /// it at sign-in, and the base class already refuses to upload over a record it failed to load,
    /// so loading it on first use is safe.</para>
    /// </summary>
    public sealed class ReplayIndexRepository : CloudDataRepository<ReplayIndexCloudData>
    {
        public override string CloudKey => UGSKeys.ReplayIndex;

        public ReplayIndexRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(ReplayIndexCloudData data)
        {
            data.Entries ??= new List<ReplaySummary>();
        }

        /// <summary>Insert or replace one summary, trim to <paramref name="cap"/>, and schedule the save.</summary>
        public void Upsert(ReplaySummary summary, int cap)
        {
            ReplayIndexCloudData.Upsert(_data.Entries, summary, cap);
            MarkDirty();
        }

        /// <summary>Drop one summary and schedule the save when it was there.</summary>
        public void Remove(ReplaySummary summary)
        {
            if (ReplayIndexCloudData.Remove(_data.Entries, summary)) MarkDirty();
        }
    }
}

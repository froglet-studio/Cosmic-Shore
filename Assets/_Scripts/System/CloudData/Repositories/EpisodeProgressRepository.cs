using System.Collections.Generic;
using CosmicShore.App.Systems.CloudData.Models;
using CosmicShore.Core;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for episode unlock/completion state.
    /// Cloud key: "EPISODE_PROGRESS"
    /// </summary>
    public sealed class EpisodeProgressRepository : CloudDataRepository<EpisodeProgressCloudData>
    {
        public override string CloudKey => UGSKeys.EpisodeProgress;

        public EpisodeProgressRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(EpisodeProgressCloudData data)
        {
            data.UnlockedEpisodes ??= new List<string>();
            data.CompletedEpisodes ??= new List<string>();
            data.EpisodeProgress ??= new Dictionary<string, EpisodeState>();
        }
    }
}

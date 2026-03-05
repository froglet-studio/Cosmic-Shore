using System.Collections.Generic;
using CosmicShore.App.Systems.CloudData.Models;
using CosmicShore.Core;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for training game tier progress.
    /// Cloud key: "TRAINING_PROGRESS"
    /// </summary>
    public sealed class TrainingProgressRepository : CloudDataRepository<TrainingProgressCloudData>
    {
        public override string CloudKey => UGSKeys.TrainingProgress;

        public TrainingProgressRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(TrainingProgressCloudData data)
        {
            data.Games ??= new Dictionary<string, TrainingGameState>();
        }
    }
}

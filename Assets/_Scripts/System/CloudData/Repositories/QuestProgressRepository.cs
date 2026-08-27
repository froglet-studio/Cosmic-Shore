using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for Quest Graph progress (per-node completion + resume cursor per quest).
    /// Cloud key: "QUEST_GRAPH_PROGRESS".
    /// </summary>
    public sealed class QuestProgressRepository : CloudDataRepository<QuestProgressCloudData>
    {
        public override string CloudKey => UGSKeys.QuestGraph;

        public QuestProgressRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(QuestProgressCloudData data)
        {
            data.Quests ??= new Dictionary<string, QuestProgressRecord>();
            foreach (var record in data.Quests.Values)
                if (record != null)
                    record.CompletedNodeIds ??= new List<string>();
        }
    }
}

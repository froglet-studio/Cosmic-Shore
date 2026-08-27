using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists Quest Graph progress to UGS Cloud Save — one record per quest, keyed by
    /// QuestSO.QuestId. Every node the player completes is recorded (funnel analytics +
    /// exact mid-phase resume), plus the resume cursor (phase index + current node).
    ///
    /// JSON example:
    /// {
    ///   "Quests": {
    ///     "MainQuest": {
    ///       "Completed": false,
    ///       "CurrentPhaseIndex": 1,
    ///       "CurrentNodeId": "a1b2c3...",
    ///       "CompletedNodeIds": ["0/e4f5...", "0/9a8b...", "1/c7d6..."]
    ///     }
    ///   },
    ///   "Version": 2
    /// }
    /// </summary>
    [Serializable]
    public class QuestProgressCloudData
    {
        /// <summary>Per-quest progress records, keyed by QuestId.</summary>
        public Dictionary<string, QuestProgressRecord> Quests = new();

        /// <summary>Schema version for forward-compatible migrations.</summary>
        public int Version = 2;

        public QuestProgressRecord GetOrCreate(string questId)
        {
            Quests ??= new Dictionary<string, QuestProgressRecord>();
            if (!Quests.TryGetValue(questId, out var record))
            {
                record = new QuestProgressRecord();
                Quests[questId] = record;
            }
            return record;
        }

        public QuestProgressRecord TryGet(string questId)
        {
            if (Quests != null && questId != null && Quests.TryGetValue(questId, out var record))
                return record;
            return null;
        }
    }

    /// <summary>Progress of a single quest. Node entries are "{phaseIndex}/{nodeId}".</summary>
    [Serializable]
    public class QuestProgressRecord
    {
        public bool Completed;
        public int CurrentPhaseIndex;
        public string CurrentNodeId;
        public List<string> CompletedNodeIds = new();

        public void RecordNode(int phaseIndex, string nodeId)
        {
            CompletedNodeIds ??= new List<string>();
            string entry = $"{phaseIndex}/{nodeId}";
            if (!CompletedNodeIds.Contains(entry))
                CompletedNodeIds.Add(entry);
        }
    }
}

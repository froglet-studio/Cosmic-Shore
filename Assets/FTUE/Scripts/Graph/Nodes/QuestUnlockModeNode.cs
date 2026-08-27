using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Unlocks a game mode directly from the graph (source-of-truth write). Use when a
    /// quest beat should GRANT an unlock rather than wait for the player to claim it —
    /// the mode opens at the default intensity range (1–3) exactly as a claim would.
    /// </summary>
    public class QuestUnlockModeNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Progression;
        public override string TypeTooltip => "Writes progression: unlocks the mode at the default intensity range (1–3), fires OnProgressionChanged, and saves to UGS. No-op if already unlocked.";
        public override string EditorSummary => $"Unlock {mode}";

        [Tooltip("The game mode to unlock.")]
        public GameModes mode = GameModes.HexRace;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc == null)
                Debug.LogError("[Quest] UnlockModeNode: GameModeProgressionService not alive — unlock skipped.");
            else
                svc.UnlockMode(mode);

            advance(QuestPorts.Next);
            yield break;
        }
    }
}

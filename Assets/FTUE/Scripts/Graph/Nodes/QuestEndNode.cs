using System.Collections;

namespace CosmicShore.Core
{
    /// <summary>
    /// Terminal node for the WHOLE quest. Marks the quest complete — the runner persists
    /// completion (UGS + local mirror) and returns breadcrumb authority to the progression
    /// service. Place exactly one at the end of the final phase.
    /// </summary>
    public class QuestEndNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Terminal;
        public override string TypeTooltip => "Completes the WHOLE quest: persists completion to UGS, restores the progression service's breadcrumb, and stops the runner. Use Phase End to advance between phases.";
        public override string EditorSummary => "Quest complete";
        public override System.Collections.Generic.IReadOnlyList<string> OutputPorts => System.Array.Empty<string>();

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            ctx.CompleteQuest?.Invoke();
            yield break;
        }
    }
}

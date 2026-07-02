using System.Collections;

namespace CosmicShore.Core
{
    /// <summary>
    /// Terminal node for ONE phase. Fires "phase ended": the runner records the phase as
    /// complete in UGS and starts the quest's next phase graph. Every phase graph should
    /// end in exactly one of these (or a <see cref="QuestEndNode"/> on the final phase).
    /// </summary>
    public class QuestPhaseEndNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Terminal;
        public override string TypeTooltip => "Ends the CURRENT phase and advances the quest to the next phase graph. Progress is written to UGS at the phase boundary.";
        public override string EditorSummary => "→ next phase";
        public override System.Collections.Generic.IReadOnlyList<string> OutputPorts => System.Array.Empty<string>();

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            ctx.CompletePhase?.Invoke();
            yield break;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Terminal node for ONE phase. Fires "phase ended": the runner records the phase as
    /// complete in UGS and starts the quest's next phase graph. Every phase graph should
    /// end in exactly one of these (or a <see cref="QuestEndNode"/> on the final phase).
    ///
    /// A phase boundary also CLEARS any arcade-funnel constraints: the funnel is a
    /// within-phase tool, and a funnel that leaks across the boundary locks the very mode
    /// the next phase just rewarded (glowing CTA on a locked card). A phase that wants a
    /// funnel simply applies it again.
    /// </summary>
    public class QuestPhaseEndNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Terminal;
        public override string TypeTooltip => "Ends the CURRENT phase and advances the quest to the next phase graph. Progress is written to UGS at the phase boundary — and any arcade-funnel constraints are cleared (the funnel is per-phase; re-apply it in the next phase if needed).";
        public override string EditorSummary => "→ next phase";
        public override System.Collections.Generic.IReadOnlyList<string> OutputPorts => System.Array.Empty<string>();

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (QuestArcadeConstraints.Active)
            {
                Debug.Log("[Quest] Phase end — clearing the arcade funnel (per-phase scope).");
                QuestArcadeConstraints.Clear();
            }

            ctx.CompletePhase?.Invoke();
            yield break;
        }
    }
}

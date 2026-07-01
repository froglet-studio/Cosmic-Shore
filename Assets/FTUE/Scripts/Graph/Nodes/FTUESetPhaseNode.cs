using System.Collections;

namespace CosmicShore.Core
{
    /// <summary>
    /// Records that the FTUE has reached a given <see cref="TutorialPhase"/> and persists it
    /// (for resume + analytics), then advances. Replaces the old hardcoded phase bump inside
    /// <c>TutorialFlowController.FinishFlow</c>.
    /// </summary>
    public class FTUESetPhaseNode : FTUENodeSO
    {
        public TutorialPhase phase = TutorialPhase.Phase2_GameplayTimer;

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            ctx.SaveProgress?.Invoke(phase, nodeId);
            advance(FTUEPorts.Next);
            yield break;
        }
    }
}

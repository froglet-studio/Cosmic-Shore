using System.Collections;

namespace CosmicShore.Core
{
    /// <summary>
    /// Terminal node. Marks the FTUE complete — the runner persists completion (cloud + local
    /// mirror) and raises the completion signal — then ends the flow (no outgoing edge).
    /// </summary>
    public class FTUEEndNode : FTUENodeSO
    {
        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            ctx.MarkCompleted?.Invoke();

            // No edge on Next → the runner treats this as end-of-flow.
            advance(FTUEPorts.Next);
            yield break;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Pauses the flow for a fixed number of (unscaled) seconds, then advances.
    /// Useful for pacing between beats. Uses realtime so it works even while the
    /// menu pauses <c>Time.timeScale</c>.
    /// </summary>
    public class QuestWaitNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Flow;
        public override string TypeTooltip => "Pauses the flow for a fixed number of real-time seconds (works while the menu pauses timescale).";
        public override string EditorSummary => $"{seconds:0.##}s (realtime)";

        [Tooltip("Seconds to wait (unscaled/real time).")]
        [Min(0f)] public float seconds = 1f;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            advance(QuestPorts.Next);
        }
    }
}

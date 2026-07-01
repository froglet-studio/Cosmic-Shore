using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Pauses the flow for a fixed number of (unscaled) seconds, then advances.
    /// Useful for pacing between beats. Uses realtime so it works even while the
    /// menu pauses <c>Time.timeScale</c>.
    /// </summary>
    public class FTUEWaitNode : FTUENodeSO
    {
        [Tooltip("Seconds to wait (unscaled/real time).")]
        [Min(0f)] public float seconds = 1f;

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            advance(FTUEPorts.Next);
        }
    }
}

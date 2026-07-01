using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player finishes a game — advances on <c>GameDataSO.OnMiniGameEnd</c>.
    /// Use after a <see cref="FTUEWaitForGameLaunchNode"/> to gate on "they actually played it".
    /// </summary>
    public class FTUEWaitForGamePlayedNode : FTUENodeSO
    {
        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.GameData == null || ctx.GameData.OnMiniGameEnd == null)
            {
                Debug.LogError("[FTUE] WaitForGamePlayedNode: GameData.OnMiniGameEnd not available — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            void OnEnd() => advance(FTUEPorts.Next);

            ctx.GameData.OnMiniGameEnd.OnRaised += OnEnd;
            ctx.AddCleanup(() => ctx.GameData.OnMiniGameEnd.OnRaised -= OnEnd);
        }
    }
}

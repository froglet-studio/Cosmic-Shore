using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player launches an arcade game — advances on <c>GameDataSO.OnLaunchGame</c>.
    /// This is the "they picked a game and started it" beat in the FTUE.
    /// </summary>
    public class FTUEWaitForGameLaunchNode : FTUENodeSO
    {
        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.GameData == null || ctx.GameData.OnLaunchGame == null)
            {
                Debug.LogError("[FTUE] WaitForGameLaunchNode: GameData.OnLaunchGame not available — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            void OnLaunch() => advance(FTUEPorts.Next);

            ctx.GameData.OnLaunchGame.OnRaised += OnLaunch;
            ctx.AddCleanup(() => ctx.GameData.OnLaunchGame.OnRaised -= OnLaunch);
        }
    }
}

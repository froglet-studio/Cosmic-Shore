using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player launches an arcade game — advances on <c>GameDataSO.OnLaunchGame</c>.
    /// This is the "they picked a game and started it" beat in the FTUE.
    /// </summary>
    public class QuestWaitForGameLaunchNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the player launches an arcade game (GameDataSO.OnLaunchGame).";
        public override string EditorSummary => "Until any game launches";

        /// <summary>Resolves as the player leaves the shell for a match.</summary>
        public override QuestVenue Venue => QuestVenue.Gameplay;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.GameData == null || ctx.GameData.OnLaunchGame == null)
            {
                Debug.LogError("[Quest] WaitForGameLaunchNode: GameData.OnLaunchGame not available — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            void OnLaunch() => advance(QuestPorts.Next);

            ctx.GameData.OnLaunchGame.OnRaised += OnLaunch;
            ctx.AddCleanup(() => ctx.GameData.OnLaunchGame.OnRaised -= OnLaunch);
        }
    }
}

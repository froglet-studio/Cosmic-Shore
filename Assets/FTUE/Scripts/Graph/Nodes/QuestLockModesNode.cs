using System.Collections;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Locks every arcade game card except the designated tutorial game, so the quest can
    /// funnel a first-time player into one specific mode. The game cards are supplied to
    /// the runner and referenced through the context. Advances immediately.
    /// </summary>
    public class QuestLockModesNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip => "Locks every arcade game card except the designated tutorial game, funneling the player into one mode.";
        public override string EditorSummary => $"Only {tutorialGame}";

        [Tooltip("The one game card left interactable during the tutorial.")]
        public CallToActionTargetType tutorialGame = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.GameCards != null)
            {
                foreach (var card in ctx.GameCards)
                {
                    if (card == null) continue;

                    var btn = card.GetComponentInChildren<Button>();
                    if (btn != null)
                        btn.interactable = (card.TargetID == tutorialGame);
                }
            }
            else
            {
                Debug.LogWarning("[Quest] LockModesNode: no game cards wired on runner — nothing to lock.");
            }

            advance(QuestPorts.Next);
            yield break;
        }
    }
}

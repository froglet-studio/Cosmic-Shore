using System.Collections;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Locks every arcade game card except the designated tutorial game, so the FTUE can
    /// funnel a first-time player into one specific mode. Preserves the behaviour of the
    /// old <c>TutorialExecutorAdapter.LockAllExceptTutorialGame</c> — the game cards are
    /// supplied to the runner and referenced through the context. Advances immediately.
    /// </summary>
    public class FTUELockModesNode : FTUENodeSO
    {
        [Tooltip("The one game card left interactable during the tutorial.")]
        public CallToActionTargetType tutorialGame = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
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
                Debug.LogWarning("[FTUE] LockModesNode: no game cards wired on runner — nothing to lock.");
            }

            advance(FTUEPorts.Next);
            yield break;
        }
    }
}

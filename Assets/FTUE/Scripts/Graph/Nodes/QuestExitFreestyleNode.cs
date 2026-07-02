using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits for the player to leave freestyle and return to the menu — i.e. "press back".
    /// The player triggers the return themselves (tapping the menu/back control drives
    /// <c>MenuCrystalClickHandler.ToggleTransition</c>); this node advances when that
    /// freestyle→menu blend completes (<c>OnMenuStateTransitionEnd</c>).
    /// </summary>
    public class QuestExitFreestyleNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits for the player to leave freestyle and return to the menu (press back).";
        public override string EditorSummary => "Until player returns to menu";

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.FreestyleEvents == null)
            {
                Debug.LogError("[Quest] ExitFreestyleNode: freestyle events not wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            // If the player is already back in the menu, don't wait for an event that won't come.
            if (ctx.CrystalHandler != null && !ctx.CrystalHandler.IsInFreestyle)
            {
                advance(QuestPorts.Next);
                yield break;
            }

            void OnMenuComplete() => advance(QuestPorts.Next);

            ctx.FreestyleEvents.OnMenuStateTransitionEnd.OnRaised += OnMenuComplete;
            ctx.AddCleanup(() => ctx.FreestyleEvents.OnMenuStateTransitionEnd.OnRaised -= OnMenuComplete);
        }
    }
}

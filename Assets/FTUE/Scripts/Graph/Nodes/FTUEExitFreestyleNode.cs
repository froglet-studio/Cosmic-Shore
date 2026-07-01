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
    public class FTUEExitFreestyleNode : FTUENodeSO
    {
        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.FreestyleEvents == null)
            {
                Debug.LogError("[FTUE] ExitFreestyleNode: freestyle events not wired — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            // If the player is already back in the menu, don't wait for an event that won't come.
            if (ctx.CrystalHandler != null && !ctx.CrystalHandler.IsInFreestyle)
            {
                advance(FTUEPorts.Next);
                yield break;
            }

            void OnMenuComplete() => advance(FTUEPorts.Next);

            ctx.FreestyleEvents.OnMenuStateTransitionEnd.OnRaised += OnMenuComplete;
            ctx.AddCleanup(() => ctx.FreestyleEvents.OnMenuStateTransitionEnd.OnRaised -= OnMenuComplete);
        }
    }
}

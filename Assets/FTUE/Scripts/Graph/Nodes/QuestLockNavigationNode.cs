using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Locks the main-menu footer navigation down to the Arcade button (all other nav buttons —
    /// profile, hangar, store, port — become non-interactable), or unlocks them all again.
    /// The buttons are wired on the runner (auto-resolved by the Phase 0 wirer from the
    /// ScreenSwitcher's OnClick*Nav handlers). Advances immediately.
    ///
    /// Scene-state only: a scene reload naturally restores the buttons, so re-lock after a
    /// game round-trip needs another Lock node if the funnel is still on.
    /// </summary>
    public class QuestLockNavigationNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip =>
            "Locks every footer nav button except the Arcade button (or unlocks them all). Buttons are wired on the runner; the quest teardown always restores them.";
        public override string EditorSummary => unlock ? "Unlock all nav buttons" : "Nav → Arcade only";

        [Tooltip("Restore all nav buttons instead of locking them.")]
        public bool unlock;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            ctx.SetNavLocked(!unlock);
            advance(QuestPorts.Next);
            yield break;
        }
    }
}

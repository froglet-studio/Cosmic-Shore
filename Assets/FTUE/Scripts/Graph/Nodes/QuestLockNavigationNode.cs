using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Locks the FTUE-funnel nav buttons — the HANGAR and PROFILE links (Ark/Port are
    /// permanently locked scene-side and Home stays available) — or unlocks them again.
    /// The buttons are wired on the runner (auto-resolved by the Phase 0 wirer). Advances
    /// immediately.
    ///
    /// Scene-state only: a scene reload naturally restores the buttons, so re-lock after a
    /// game round-trip needs another Lock node if the funnel is still on.
    /// </summary>
    public class QuestLockNavigationNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip =>
            "Locks the FTUE-funnel nav buttons (Hangar + Profile links; Ark/Port are permanently scene-locked, Home stays available) or unlocks them. Buttons are wired on the runner; the quest teardown always restores them.";
        public override string EditorSummary => unlock ? "Unlock nav (Hangar/Profile)" : "Lock nav (Hangar/Profile)";

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

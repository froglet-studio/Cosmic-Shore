using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Raises a Call-To-Action pointing at a UI target (e.g. the Arcade nav button or a
    /// specific game card) and waits until the player satisfies it. This is the binding
    /// into the CTA system the FTUE "continues bound with".
    ///
    /// A <see cref="CallToActionTarget"/> in the scene whose <c>TargetID</c> matches shows
    /// its indicator; the node advances when the matching <see cref="completionAction"/>
    /// user action fires (the same signal that dismisses the CTA), or when the FTUE card-click
    /// event reports this target.
    /// </summary>
    public class FTUEHighlightCTANode : FTUENodeSO
    {
        [Tooltip("Which CTA target to light up (must match a CallToActionTarget.TargetID in the scene).")]
        public CallToActionTargetType target = CallToActionTargetType.ArcadeMenu;

        [Tooltip("The user action whose completion satisfies this CTA and advances the flow.")]
        public UserActionType completionAction = UserActionType.ViewArcadeMenu;

        [Tooltip("Optional dependency targets that must light up alongside this one.")]
        public List<CallToActionTargetType> dependencies = new();

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (CallToActionSystem.Instance != null)
            {
                var deps = (dependencies != null && dependencies.Count > 0) ? dependencies : null;
                CallToActionSystem.Instance.AddCallToAction(new CallToAction(target, completionAction, deps));
            }
            else
            {
                Debug.LogWarning("[FTUE] HighlightCTANode: CallToActionSystem.Instance is null — indicator will not show.");
            }

            bool done = false;

            void Complete()
            {
                if (done) return;
                done = true;
                advance(FTUEPorts.Next);
            }

            // Primary: the completion user action fires (this is exactly when the CTA is dismissed).
            if (UserActionSystem.Instance != null)
            {
                void OnUserAction(UserAction a)
                {
                    if (a.ActionType == completionAction)
                        Complete();
                }

                UserActionSystem.Instance.OnUserActionCompleted += OnUserAction;
                ctx.AddCleanup(() => UserActionSystem.Instance.OnUserActionCompleted -= OnUserAction);
            }

            // Secondary: the FTUE card-click path reports this target directly.
            void OnCtaClicked(CallToActionTargetType clicked)
            {
                if (clicked == target)
                    Complete();
            }

            FTUEEventManager.OnCTAClicked += OnCtaClicked;
            ctx.AddCleanup(() => FTUEEventManager.OnCTAClicked -= OnCtaClicked);

            yield break;
        }
    }
}

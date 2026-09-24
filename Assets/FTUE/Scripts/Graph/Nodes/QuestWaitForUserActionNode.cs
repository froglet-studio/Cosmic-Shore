using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until a specific <see cref="UserActionType"/> fires through
    /// <see cref="UserActionSystem"/> — the generic "they did it" gate. Any UI can satisfy
    /// it by calling <c>UserActionSystem.Instance.CompleteAction(...)</c> (e.g. the hangar's
    /// vessel-unlock flow firing UnlockVessel, or a UserActionTrigger on a button).
    /// </summary>
    public class QuestWaitForUserActionNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits for a UserActionType to fire through UserActionSystem — the generic completion signal. Wire your UI to CompleteAction(...) (or a UserActionTrigger) to satisfy it.";
        public override string EditorSummary => $"Until {action}";

        [Tooltip("The user action that satisfies this gate.")]
        public UserActionType action = UserActionType.UnlockVessel;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (UserActionSystem.Instance == null)
            {
                Debug.LogError("[Quest] WaitForUserActionNode: UserActionSystem not alive — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            void OnAction(UserAction completed)
            {
                if (completed.ActionType == action)
                    advance(QuestPorts.Next);
            }

            UserActionSystem.Instance.OnUserActionCompleted += OnAction;
            ctx.AddCleanup(() =>
            {
                if (UserActionSystem.Instance != null)
                    UserActionSystem.Instance.OnUserActionCompleted -= OnAction;
            });
        }
    }
}

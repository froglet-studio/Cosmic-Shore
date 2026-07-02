using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player performs one of the accepted control inputs, then advances.
    /// Subscribes to the shared <c>OnButtonPressed</c> SOAP channel (the same asset the
    /// vessel's <c>InputStatus</c> raises), so it is decoupled from any specific vessel.
    ///
    /// Single discrete press per v1 design — the node completes on the first matching press.
    /// Leave <see cref="acceptedInputs"/> empty to accept ANY control input.
    /// </summary>
    public class QuestWaitForInputNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the player performs one of the accepted control inputs (single press). Empty list accepts any input.";
        public override string EditorSummary => acceptedInputs == null || acceptedInputs.Count == 0 ? "Any input" : string.Join(", ", acceptedInputs);

        [Tooltip("Control inputs that satisfy this step. Empty = any input completes it.")]
        public List<InputEvents> acceptedInputs = new();

        [Tooltip("Haptic pulse when the player performs the correct input.")]
        public HapticType successHaptic = HapticType.ButtonPress;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.OnButtonPressed == null)
            {
                Debug.LogError("[Quest] WaitForInputNode: OnButtonPressed event asset not wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            void OnPressed(InputEvents pressed)
            {
                if (acceptedInputs == null || acceptedInputs.Count == 0 || acceptedInputs.Contains(pressed))
                {
                    if (successHaptic != HapticType.None)
                        HapticController.PlayHaptic(successHaptic);
                    advance(QuestPorts.Next);
                }
            }

            ctx.OnButtonPressed.OnRaised += OnPressed;
            ctx.AddCleanup(() => ctx.OnButtonPressed.OnRaised -= OnPressed);
        }
    }
}

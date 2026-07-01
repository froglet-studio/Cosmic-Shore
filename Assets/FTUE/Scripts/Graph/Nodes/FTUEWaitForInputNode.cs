using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
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
    public class FTUEWaitForInputNode : FTUENodeSO
    {
        [Tooltip("Control inputs that satisfy this step. Empty = any input completes it.")]
        public List<InputEvents> acceptedInputs = new();

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.OnButtonPressed == null)
            {
                Debug.LogError("[FTUE] WaitForInputNode: OnButtonPressed event asset not wired — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            void OnPressed(InputEvents pressed)
            {
                if (acceptedInputs == null || acceptedInputs.Count == 0 || acceptedInputs.Contains(pressed))
                    advance(FTUEPorts.Next);
            }

            ctx.OnButtonPressed.OnRaised += OnPressed;
            ctx.AddCleanup(() => ctx.OnButtonPressed.OnRaised -= OnPressed);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plays an embedded <see cref="DialogueSet"/> through the existing dialogue system
    /// (<see cref="DialogueManager.PlayDialogueSet"/>) and advances when it finishes.
    /// Completion rides the <c>DialogueManager.OnDialogueFinished</c> event (added for FTUE),
    /// so there is no polling and no race.
    /// </summary>
    public class FTUEDialogueNode : FTUENodeSO
    {
        [Tooltip("The dialogue set to play. Authored in the Dialogue Editor and referenced here.")]
        public DialogueSet dialogueSet;

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.DialogueManager == null || dialogueSet == null
                || dialogueSet.lines == null || dialogueSet.lines.Count == 0)
            {
                Debug.LogWarning("[FTUE] DialogueNode: missing DialogueManager or empty DialogueSet — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            void OnFinished(DialogueSet finished)
            {
                if (finished == dialogueSet)
                    advance(FTUEPorts.Next);
            }

            ctx.DialogueManager.OnDialogueFinished += OnFinished;
            ctx.AddCleanup(() => ctx.DialogueManager.OnDialogueFinished -= OnFinished);

            ctx.DialogueManager.PlayDialogueSet(dialogueSet);
        }

        public override void Validate(FTUEGraphSO graph, List<string> errors)
        {
            if (dialogueSet == null)
                errors.Add($"'{name}': no DialogueSet assigned.");
            else if (dialogueSet.lines == null || dialogueSet.lines.Count == 0)
                errors.Add($"'{name}': DialogueSet '{dialogueSet.setId}' has no lines.");
        }
    }
}

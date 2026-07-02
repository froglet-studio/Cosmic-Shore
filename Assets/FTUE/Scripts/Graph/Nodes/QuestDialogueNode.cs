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
    public class QuestDialogueNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Presentation;
        public override string TypeTooltip => "Plays a DialogueSet through the dialogue system (any channel, including Reward) and advances when it finishes.";
        public override string EditorSummary => dialogueSet != null ? $"Set: {dialogueSet.setId}" : "(no set assigned)";

        [Tooltip("The dialogue set to play. Authored in the Dialogue Editor and referenced here.")]
        public DialogueSet dialogueSet;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.DialogueManager == null || dialogueSet == null
                || dialogueSet.lines == null || dialogueSet.lines.Count == 0)
            {
                Debug.LogWarning("[Quest] DialogueNode: missing DialogueManager or empty DialogueSet — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            void OnFinished(DialogueSet finished)
            {
                if (finished == dialogueSet)
                    advance(QuestPorts.Next);
            }

            ctx.DialogueManager.OnDialogueFinished += OnFinished;
            ctx.AddCleanup(() => ctx.DialogueManager.OnDialogueFinished -= OnFinished);

            ctx.DialogueManager.PlayDialogueSet(dialogueSet);
        }

        public override void Validate(QuestPhaseGraphSO graph, List<string> errors)
        {
            if (dialogueSet == null)
                errors.Add($"'{name}': no DialogueSet assigned.");
            else if (dialogueSet.lines == null || dialogueSet.lines.Count == 0)
                errors.Add($"'{name}': DialogueSet '{dialogueSet.setId}' has no lines.");
        }
    }
}

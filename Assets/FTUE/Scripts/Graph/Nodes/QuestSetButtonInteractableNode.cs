using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Enables (or disables) a scene Button registered on the runner's Quest Buttons list —
    /// e.g. the Episodes button is authored non-interactable and the finale phase unlocks it
    /// right before its CTA, so the "view episodes" gate is actually satisfiable.
    /// Advances immediately.
    /// </summary>
    public class QuestSetButtonInteractableNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip =>
            "Sets a runner-registered Button's interactable state by key (Quest Buttons list on the runner — e.g. 'episodes'). Use to unlock UI the FTUE gates, right before the CTA that needs it clickable.";
        public override string EditorSummary => $"'{buttonKey}' → {(interactable ? "interactable" : "disabled")}";

        [Tooltip("Key of the button on the runner's Quest Buttons list (e.g. 'episodes').")]
        public string buttonKey = "episodes";

        [Tooltip("The interactable state to apply.")]
        public bool interactable = true;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            ctx.SetQuestButtonInteractable(buttonKey, interactable);
            advance(QuestPorts.Next);
            yield break;
        }

        public override void Validate(QuestPhaseGraphSO graph, System.Collections.Generic.List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(buttonKey))
                errors.Add($"'{name}': button key is empty.");
        }
    }
}

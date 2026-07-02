using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Shows a line of tutorial instruction text via the FTUE typewriter panel
    /// (<see cref="TutorialUIView.ShowStep"/>). Two modes:
    ///  • <see cref="advanceOnNextPress"/> = true  → waits for the player to tap Next, then advances.
    ///  • <see cref="advanceOnNextPress"/> = false → shows the text and advances immediately,
    ///    leaving it on-screen (use before a <c>WaitForInputNode</c> so the prompt stays up
    ///    while the player performs the control).
    /// </summary>
    public class QuestShowInstructionNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Presentation;
        public override string TypeTooltip => "Shows typewriter instruction text on the tutorial panel. Advance on Next press, or show-and-continue so a following Wait/gate node holds the prompt on screen.";
        public override string EditorSummary => string.IsNullOrEmpty(text) ? "(no text)" : text;

        [TextArea(2, 5)]
        [Tooltip("Instruction text shown in the FTUE panel.")]
        public string text;

        [Tooltip("If true, wait for the player to press Next before advancing. If false, show and advance immediately (text stays visible).")]
        public bool advanceOnNextPress = true;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.TutorialUI == null)
            {
                Debug.LogError("[Quest] ShowInstructionNode: TutorialUIView not wired on runner — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            ctx.TutorialUI.ToggleFTUECanvas(true);

            if (advanceOnNextPress)
            {
                // ShowStep invokes onComplete when the player taps Next (or Skip).
                ctx.TutorialUI.ShowStep(text, () => advance(QuestPorts.Next));
                yield break;
            }

            ctx.TutorialUI.ShowStep(text, null);
            advance(QuestPorts.Next);
        }

        public override void Validate(QuestPhaseGraphSO graph, System.Collections.Generic.List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(text))
                errors.Add($"'{name}': instruction text is empty.");
        }
    }
}

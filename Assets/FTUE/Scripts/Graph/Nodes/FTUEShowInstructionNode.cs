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
    public class FTUEShowInstructionNode : FTUENodeSO
    {
        [TextArea(2, 5)]
        [Tooltip("Instruction text shown in the FTUE panel.")]
        public string text;

        [Tooltip("If true, wait for the player to press Next before advancing. If false, show and advance immediately (text stays visible).")]
        public bool advanceOnNextPress = true;

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.TutorialUI == null)
            {
                Debug.LogError("[FTUE] ShowInstructionNode: TutorialUIView not wired on runner — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            ctx.TutorialUI.ToggleFTUECanvas(true);

            if (advanceOnNextPress)
            {
                // ShowStep invokes onComplete when the player taps Next (or Skip).
                ctx.TutorialUI.ShowStep(text, () => advance(FTUEPorts.Next));
                yield break;
            }

            ctx.TutorialUI.ShowStep(text, null);
            advance(FTUEPorts.Next);
        }

        public override void Validate(FTUEGraphSO graph, System.Collections.Generic.List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(text))
                errors.Add($"'{name}': instruction text is empty.");
        }
    }
}

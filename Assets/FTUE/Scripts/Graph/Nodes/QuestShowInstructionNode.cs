using System.Collections;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Shows a lightweight in-game instruction (plain text + optional haptic pulse) via
    /// <see cref="QuestInstructionView"/> — the control-teaching UI. The prompt stays on
    /// screen while a following gate node (e.g. WaitForInput) holds the flow; the next
    /// instruction replaces it, or set <see cref="hideOnAdvance"/> to clear it.
    /// Falls back to the legacy TutorialUIView typewriter if no instruction view is wired.
    /// </summary>
    public class QuestShowInstructionNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Presentation;
        public override string TypeTooltip => "Shows plain instruction text (+ optional haptic pulse) on the lightweight instruction overlay, waits Min Display Seconds, then advances. Pair with a gate node to hold the prompt until the player acts.";
        public override string EditorSummary
        {
            get
            {
                string s = string.IsNullOrEmpty(text) ? "(no text)" : text;
                if (minDisplaySeconds > 0f) s += $"  ·  {minDisplaySeconds:0.#}s";
                if (haptic != HapticType.None) s += $"  ·  {haptic}";
                return s;
            }
        }

        [TextArea(2, 5)]
        [Tooltip("Instruction text shown on the overlay.")]
        public string text;

        [Tooltip("Optional haptic pulse when the instruction appears (respects the player's haptics setting).")]
        public HapticType haptic = HapticType.None;

        [Tooltip("Hold the flow this many real-time seconds before advancing (0 = advance immediately; a following gate node keeps the prompt visible).")]
        [Min(0f)] public float minDisplaySeconds;

        [Tooltip("Hide the instruction panel when this node advances (default: leave it up for the next beat).")]
        public bool hideOnAdvance;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.InstructionView != null)
            {
                ctx.InstructionView.Show(text, haptic);
            }
            else if (ctx.TutorialUI != null)
            {
                // Legacy fallback: the captain typewriter panel, no next-press wait.
                ctx.TutorialUI.ToggleFTUECanvas(true);
                ctx.TutorialUI.ShowStep(text, null);
                if (haptic != HapticType.None)
                    HapticController.PlayHaptic(haptic);
            }
            else
            {
                Debug.LogError("[Quest] ShowInstructionNode: no QuestInstructionView or TutorialUIView wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            if (minDisplaySeconds > 0f)
                yield return new WaitForSecondsRealtime(minDisplaySeconds);

            if (hideOnAdvance && ctx.InstructionView != null)
                ctx.InstructionView.Hide();

            advance(QuestPorts.Next);
        }

        public override void Validate(QuestPhaseGraphSO graph, System.Collections.Generic.List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(text))
                errors.Add($"'{name}': instruction text is empty.");
        }
    }
}

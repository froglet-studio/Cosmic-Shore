using System.Collections;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Shows a lightweight in-game instruction (plain text or a keyed hand-built panel,
    /// plus an optional haptic pulse) via <see cref="QuestInstructionView"/>. The prompt
    /// stays on screen while a following gate node (e.g. WaitForInput) holds the flow; the
    /// next instruction replaces it, or set <see cref="hideOnAdvance"/> to clear it.
    /// </summary>
    public class QuestShowInstructionNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Presentation;
        public override string TypeTooltip => "Shows plain instruction text (+ optional haptic pulse) on the lightweight instruction overlay, waits Min Display Seconds, then advances. Pair with a gate node to hold the prompt until the player acts.";
        public override string EditorSummary
        {
            get
            {
                string s = !string.IsNullOrEmpty(panelKey) ? $"[panel: {panelKey}]  {text}" : (string.IsNullOrEmpty(text) ? "(no text)" : text);
                if (minDisplaySeconds > 0f) s += $"  ·  {minDisplaySeconds:0.#}s";
                if (haptic != HapticType.None) s += $"  ·  {haptic}";
                return s;
            }
        }

        [TextArea(2, 8)]
        [Tooltip("Instruction text shown on the overlay.")]
        public string text;

        [Tooltip("Optional haptic pulse when the instruction appears (respects the player's haptics setting).")]
        public HapticType haptic = HapticType.None;

        [Tooltip("Optional key of a hand-built instruction panel registered on QuestInstructionView (icons + text as a CanvasGroup). Empty = plain text mode. The Text above OVERWRITES the panel's own TMP text — scene text is only a placeholder.")]
        public string panelKey;

        [Tooltip("Hold the flow this many real-time seconds before advancing (0 = advance immediately; a following gate node keeps the prompt visible).")]
        [Min(0f)] public float minDisplaySeconds;

        [Tooltip("Hide the instruction panel when this node advances (default: leave it up for the next beat).")]
        public bool hideOnAdvance;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.InstructionView == null)
            {
                Debug.LogError("[Quest] ShowInstructionNode: no QuestInstructionView wired on the runner — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            ctx.InstructionView.Show(text, haptic, panelKey);

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

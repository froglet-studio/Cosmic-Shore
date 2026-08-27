using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Shows the quest dialogue panel (captain portrait + typewriter text + Next button)
    /// with lines authored DIRECTLY on this node — the legacy dialogue system
    /// (DialogueManager / DialogueSet assets) is not involved. The panel is the scene's
    /// DialogueSetUI (or instantiated from the prefab wired on the runner). Advances
    /// after the player steps through the last line.
    /// </summary>
    public class QuestDialogueNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Presentation;
        public override string TypeTooltip => "Shows the dialogue panel (captain + typewriter + Next) with the lines authored on this node. Advances after the last line. No DialogueSet asset needed.";
        public override string EditorSummary
        {
            get
            {
                if (lines == null || lines.Count == 0) return "(no lines)";
                string first = lines[0] ?? string.Empty;
                if (first.Length > 60) first = first.Substring(0, 57) + "…";
                return lines.Count > 1 ? $"{first}  (+{lines.Count - 1} more)" : first;
            }
        }

        [Tooltip("Optional speaker name shown on the panel.")]
        public string speakerName = "Captain";

        [Tooltip("Optional portrait override for this beat. Empty = the panel's own captain sprite.")]
        public Sprite portrait;

        [Tooltip("The dialogue lines, in order. Next steps through them; the last line closes the panel.")]
        [TextArea(2, 8)] public List<string> lines = new();

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            var panel = ctx.GetOrCreateDialoguePanel();
            if (panel == null || lines == null || lines.Count == 0)
            {
                if (panel == null)
                    Debug.LogError("[Quest] DialogueNode: no dialogue panel wired on the runner (scene instance or prefab) — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            panel.PlayLines(speakerName, portrait, lines, () => advance(QuestPorts.Next));

            // If this node is superseded (phase end, stop), close the panel cleanly.
            ctx.AddCleanup(panel.CancelIfPlaying);
        }

        public override void Validate(QuestPhaseGraphSO graph, List<string> errors)
        {
            if (lines == null || lines.Count == 0)
                errors.Add($"'{name}': dialogue node has no lines.");
        }
    }
}

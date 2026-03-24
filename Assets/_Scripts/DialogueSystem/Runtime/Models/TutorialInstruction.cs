using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Models
{
    /// <summary>
    /// A single step in a tutorial sequence.
    /// Configured by designers in the Tutorial Sequence Editor.
    /// </summary>
    [System.Serializable]
    public class TutorialInstruction
    {
        [Header("Trigger")]
        [Tooltip("If set, this instruction waits for this SOAP event before displaying. If null, auto-plays after previous.")]
        public ScriptableEventNoParam waitForEvent;

        [Tooltip("Short label shown in the flowchart preview.")]
        public string stepLabel;

        [Header("Dialogue")]
        [Tooltip("Text shown to the player, animated as a typewriter.")]
        [TextArea(2, 5)] public string dialogueText;

        [Tooltip("Character portrait displayed alongside the dialogue.")]
        public Sprite avatarIcon;

        [Header("Arrow Guidance")]
        [Tooltip("Preset UI positions where arrows will point the player.")]
        public ArrowTarget arrowTargets = ArrowTarget.None;

        [Header("Audio")]
        [Tooltip("Optional audio clip to play during this instruction.")]
        public AudioClip audioClip;

        [Header("Flow Control")]
        [Tooltip("If true, automatically advance after text finishes instead of waiting for Next.")]
        public bool autoAdvance;

        [Tooltip("Seconds to wait before auto-advancing (only used when Auto Advance is on).")]
        [Min(0f)] public float delayBeforeNext = 1f;
    }
}

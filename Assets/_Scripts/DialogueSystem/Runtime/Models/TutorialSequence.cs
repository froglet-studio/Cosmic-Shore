using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Models
{
    /// <summary>
    /// A complete tutorial sequence — a linear list of instructions
    /// triggered by a SOAP event. Designed to be authored in the
    /// Tutorial Sequence Editor without developer assistance.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TutorialSequence",
        menuName = "CosmicShore/Dialogue/Tutorial Sequence")]
    public class TutorialSequence : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this sequence.")]
        public string sequenceId;

        [Tooltip("Designer notes — not shown in-game.")]
        [TextArea(1, 3)] public string description;

        [Header("SOAP Events")]
        [Tooltip("The SOAP event that triggers this sequence to play.")]
        public ScriptableEventNoParam triggerEvent;

        [Tooltip("If true, raises the completion event when this sequence finishes, allowing chaining.")]
        public bool autoPlayNext;

        [Tooltip("SOAP event raised when this sequence completes (used for chaining).")]
        public ScriptableEventNoParam completionEvent;

        [Header("Instructions")]
        [Tooltip("Ordered list of tutorial steps. Played top to bottom.")]
        public List<TutorialInstruction> instructions = new();
    }
}

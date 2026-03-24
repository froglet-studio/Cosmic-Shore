using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Models
{
    /// <summary>
    /// Central lookup for all tutorial sequences in the game.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TutorialSequenceLibrary",
        menuName = "CosmicShore/Dialogue/Tutorial Sequence Library")]
    public class TutorialSequenceLibrary : ScriptableObject
    {
        [Tooltip("All tutorial sequences available in the game.")]
        public List<TutorialSequence> sequences = new();

        /// <summary>
        /// Find a sequence by its ID, or null if not found.
        /// </summary>
        public TutorialSequence GetSequenceById(string id)
            => sequences.FirstOrDefault(s => s.sequenceId == id);
    }
}

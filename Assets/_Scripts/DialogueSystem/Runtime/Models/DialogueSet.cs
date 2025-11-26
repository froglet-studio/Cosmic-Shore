using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Models
{
    [CreateAssetMenu(menuName = "CosmicShore/Dialogue/Dialogue Set")]
    public class DialogueSet : ScriptableObject
    {
        public string setId;
        public DialogueModeType mode = DialogueModeType.Monologue;

        // Portraits for the entire set (instead of per?line).
        public Sprite portraitSpeaker1;
        public Sprite portraitSpeaker2;

        public List<DialogueLine> lines = new List<DialogueLine>();

        public RewardData rewardData;
    }
}
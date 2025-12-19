using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Models
{
    public enum DialogueChannel
    {
        MainMenu,
        InGameRadio,
        Reward
    }

    public enum DialogueSide
    {
        Auto,
        Left,
        Right
    }
    
    [CreateAssetMenu(menuName = "CosmicShore/Dialogue/Dialogue Set")]
    public class DialogueSet : ScriptableObject
    {
        public string setId;
        public DialogueModeType mode = DialogueModeType.Monologue;
        public DialogueChannel channel = DialogueChannel.MainMenu;

        public Sprite portraitSpeaker1;
        public Sprite portraitSpeaker2;

        public List<DialogueLine> lines = new List<DialogueLine>();
        public RewardData rewardData;
    }
}
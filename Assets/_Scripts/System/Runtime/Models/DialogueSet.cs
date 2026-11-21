using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    public enum DialogueChannel
    {
        MainMenu = 0,
        InGameRadio = 1,
        Reward = 2
    }

    public enum DialogueSide
    {
        Auto = 0,
        Left = 1,
        Right = 2
    }
    
    [CreateAssetMenu(menuName = "ScriptableObjects/Dialogue/Dialogue Set")]
    public class DialogueSet : ScriptableObject
    {
        public string setId;
        public DialogueModeType mode = DialogueModeType.Monologue;
        [SerializeField] DialogueChannel channel = DialogueChannel.MainMenu;
        public DialogueChannel Channel => channel;

        public Sprite portraitSpeaker1;
        public Sprite portraitSpeaker2;

        [SerializeField] List<DialogueLine> lines = new List<DialogueLine>();
        public List<DialogueLine> Lines => lines;
        public RewardData rewardData;
    }
}
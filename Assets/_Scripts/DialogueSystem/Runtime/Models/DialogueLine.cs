using UnityEngine;

namespace CosmicShore.DialogueSystem.Models
{
    [System.Serializable]
    public class DialogueLine
    {
        public DialogueSpeaker speaker = DialogueSpeaker.Speaker1;
        public string speakerName;
        [TextArea(2, 4)] public string text;
        public AudioClip voiceClip;

        public DialogueSide side = DialogueSide.Auto;
        public float displayTime = 3f;
        public bool isInGameMonologue;
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.DialogueSystem.Models;

namespace CosmicShore.DialogueSystem.View
{
    public class DialogueUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup dialogueCanvas;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private AudioSource audioSource;

        public void DisplayLine(DialogueLine line)
        {
            if (line == null)
            {
                HideDialogue();
                return;
            }

            dialogueCanvas.alpha = 1f;
            dialogueCanvas.interactable = true;
            dialogueCanvas.blocksRaycasts = true;

            speakerText.text = line.speakerName;
            dialogueText.text = line.text;

            //portraitImage.gameObject.SetActive(line.speaker != null);
            //portraitImage.sprite = line.speaker.;

            if (line.voiceClip != null && audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = line.voiceClip;
                audioSource.Play();
            }
        }

        public void HideDialogue()
        {
            dialogueCanvas.alpha = 0f;
            dialogueCanvas.interactable = false;
            dialogueCanvas.blocksRaycasts = false;

            speakerText.text = "";
            dialogueText.text = "";
            portraitImage.sprite = null;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
        }
    }
}

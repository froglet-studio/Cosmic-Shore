using System.Collections;
using UnityEngine;
using CosmicShore.DialogueSystem.Models;
using CosmicShore.DialogueSystem.View;

namespace CosmicShore.DialogueSystem.Controller
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance;

        [Header("References")]
        [SerializeField] private DialogueUIController uiController;
        [SerializeField] private Canvas dialogueCanvas;
        [SerializeField] private DialogueSetLibrary dialogueLibrary;

        private Coroutine _currentSequence;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(gameObject);
        }

        /// <summary>
        /// Play dialogue set by its unique ID.
        /// </summary>
        public void PlayDialogueById(string setId)
        {
            var set = dialogueLibrary.GetSetById(setId);
            if (set == null)
            {
                Debug.LogWarning($"DialogueManager: No DialogueSet found with ID '{setId}'.");
                return;
            }
            PlayDialogueSet(set);
        }

        /// <summary>
        /// Play a dialogue set (reference).
        /// </summary>
        public void PlayDialogueSet(DialogueSet set)
        {
            if (set == null || set.lines.Count == 0)
            {
                Debug.LogWarning("DialogueManager: No lines in this set.");
                return;
            }

            if (_currentSequence != null)
                StopCoroutine(_currentSequence);

            // Activate the dialogue canvas
            if (dialogueCanvas != null)
                dialogueCanvas.gameObject.SetActive(true);

            _currentSequence = StartCoroutine(PlaySequence(set));
        }

        private IEnumerator PlaySequence(DialogueSet set)
        {
            for (int i = 0; i < set.lines.Count; i++)
            {
                var line = set.lines[i];
                bool isLeft = (i % 2 == 0);
                if (set.mode == DialogueModeType.Monologue)
                    uiController.ShowMonologue(set, line, OnNextRequested);
                else if (set.mode == DialogueModeType.Dialogue)
                    uiController.ShowDialogue(set, line, OnNextRequested, isLeft);

                // Wait for Next button
                yield return new WaitUntil(() => uiController.WaitingForNextPressed);
                uiController.ResetWaitingForNext();

                // Small delay for animation out, if needed, can skip if instant
                yield return new WaitForSeconds(0.1f);
            }

            // Dialogue complete, hide UI and canvas
            uiController.Hide();
            if (dialogueCanvas != null)
                dialogueCanvas.gameObject.SetActive(false);

            _currentSequence = null;
        }


        // This can be called by UIController when the user clicks next (if you wire it up)
        private void OnNextRequested()
        {
            // Just a placeholder – real implementation can skip current WaitForSeconds, etc.
        }



        [ContextMenu("PlayDefualtSet")]
        public void PlayDefaultSetLibrary()
        {
            PlayDialogueById("Monologue");
        }
    }
}

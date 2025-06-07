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

        private Coroutine _currentSequence;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void PlayDialogueSet(DialogueSet set)
        {
            if (set == null || set.lines.Count == 0)
            {
                Debug.LogWarning("DialogueManager: No lines in this set.");
                return;
            }

            if (_currentSequence != null)
                StopCoroutine(_currentSequence);

            _currentSequence = StartCoroutine(PlaySequence(set));
        }

        private IEnumerator PlaySequence(DialogueSet set)
        {
            foreach (var line in set.lines)
            {
                uiController.DisplayLine(line);
                yield return new WaitForSeconds(line.displayTime);
            }

            uiController.HideDialogue();
            _currentSequence = null;
        }
    }
}

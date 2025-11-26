using System.Collections;
using UnityEngine;
using CosmicShore.DialogueSystem.Models;
using CosmicShore.DialogueSystem.View;
using System;

namespace CosmicShore.DialogueSystem.Controller
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance;

        [Header("References")] [SerializeField]
        private DialogueUIController _uiController;

        [SerializeField] private Canvas _dialogueCanvas;
        [SerializeField] private DialogueSetLibrary _dialogueLibrary;
        [SerializeField] private GameObject _mainGameCanvas;

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
            var set = _dialogueLibrary.GetSetById(setId);
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
            if (_dialogueCanvas != null)
            {
                _mainGameCanvas.SetActive(false);
                _dialogueCanvas.gameObject.SetActive(true);
            }

            _currentSequence = StartCoroutine(PlaySequence(set));
        }

        private IEnumerator PlaySequence(DialogueSet set)
        {
            for (int i = 0; i < set.lines.Count; i++)
            {
                var line = set.lines[i];
                bool isLeft = (i % 2 == 0);
                if (set.mode == DialogueModeType.Monologue)
                    _uiController.ShowMonologue(set, line, OnNextRequested);
                else if (set.mode == DialogueModeType.Dialogue)
                    _uiController.ShowDialogue(set, line, OnNextRequested, isLeft);

                yield return new WaitUntil(() => _uiController.WaitingForNextPressed);
                _uiController.ResetWaitingForNext();

                yield return new WaitForSeconds(0.1f);
            }

            // Dialogue complete, hide UI and canvas
            Debug.Log("<color=blue> DialogueComplete");

            bool wasMono = set.mode == DialogueModeType.Monologue;
            _uiController.HideWithAnimation(wasMono, () =>
            {
                if (_dialogueCanvas != null)
                {
                    _mainGameCanvas.SetActive(true);
                    Debug.Log("<color=green> Hide With Animation");
                    _dialogueCanvas.gameObject.SetActive(false);
                }

                _currentSequence = null;
            });
        }

        private void OnNextRequested()
        {
        }

        [ContextMenu("PlayDefualtSet")]
        public void PlayDefaultSetLibrary()
        {
            PlayDialogueById("Monologue");
        }
    }
}
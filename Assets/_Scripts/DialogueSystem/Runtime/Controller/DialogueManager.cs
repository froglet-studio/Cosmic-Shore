using System.Collections;
using UnityEngine;
using CosmicShore.DialogueSystem.Models;
using CosmicShore.DialogueSystem.Events;
using CosmicShore.DialogueSystem.View;

namespace CosmicShore.DialogueSystem.Controller
{
    public sealed class DialogueManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DialogueSetLibrary dialogueLibrary;

        [Header("View Resolution")]
        [SerializeField] private DialogueViewResolver viewResolver;

        [Header("Event Channels")]
        [SerializeField] private DialogueEventChannel dialogueEventChannel;

        [Header("Optional Canvas Control")]
        [SerializeField] private GameObject mainGameCanvas;
        [SerializeField] private Canvas dialogueCanvas;

        Coroutine _currentSequence;
        IDialogueView _activeView;
        bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        void OnEnable()
        {
            if (dialogueEventChannel != null)
                dialogueEventChannel.OnDialogueRequested += HandleDialogueRequested;
        }

        void OnDisable()
        {
            if (dialogueEventChannel != null)
                dialogueEventChannel.OnDialogueRequested -= HandleDialogueRequested;
        }

        void HandleDialogueRequested(string setId)
        {
            PlayDialogueById(setId);
        }

        public void PlayDialogueById(string setId)
        {
            var set = dialogueLibrary.GetSetById(setId);
            if (set == null)
            {
                Debug.LogWarning($"DialogueManager ({gameObject.scene.name}): No DialogueSet found with ID '{setId}'.");
                return;
            }

            PlayDialogueSet(set);
        }

        public void PlayDialogueSet(DialogueSet set)
        {
            if (set == null || set.lines == null || set.lines.Count == 0)
            {
                Debug.LogWarning("DialogueManager: DialogueSet is null or has no lines.");
                return;
            }

            if (_currentSequence != null)
                StopCoroutine(_currentSequence);

            _activeView = viewResolver.ResolveView(set);
            if (_activeView == null)
            {
                Debug.LogError($"DialogueManager: No view found for channel {set.channel}.");
                return;
            }

            if (dialogueCanvas != null)
            {
                if (mainGameCanvas) mainGameCanvas.SetActive(false);
                dialogueCanvas.gameObject.SetActive(true);
            }

            _isPlaying = true;
            _currentSequence = StartCoroutine(RunSequence(set));
        }

        IEnumerator RunSequence(DialogueSet set)
        {
            _activeView.ShowDialogueSet(set);

            for (int i = 0; i < set.lines.Count; i++)
            {
                var line = set.lines[i];
                bool lineDone = false;

                _activeView.ShowLine(set, line, () => lineDone = true);
                yield return new WaitUntil(() => lineDone);
            }

            bool hideDone = false;
            _activeView.Hide(() => hideDone = true);
            yield return new WaitUntil(() => hideDone);

            if (dialogueCanvas != null)
            {
                if (mainGameCanvas) mainGameCanvas.SetActive(true);
                dialogueCanvas.gameObject.SetActive(false);
            }

            _activeView = null;
            _currentSequence = null;
            _isPlaying = false;
        }
    }
}

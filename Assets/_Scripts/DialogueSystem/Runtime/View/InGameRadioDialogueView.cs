using System;
using System.Collections;
using CosmicShore.DialogueSystem.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.DialogueSystem.View
{
    /// <summary>
    /// Compact radio-style dialogue view for in-game use.
    /// Shows a single small panel with captain icon + text bubble.
    /// </summary>
    public sealed class InGameRadioDialogueView : MonoBehaviour, IDialogueView
    {
        [Header("Radio Root")]
        [SerializeField] private RectTransform root;       // Whole radio widget
        [SerializeField] private CanvasGroup canvasGroup;  // Optional for fade

        [Header("Visuals")]
        [SerializeField] private Image captainIcon;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Interaction")]
        [SerializeField] private Button nextButton;        // Small arrow/next button
        [SerializeField] private bool clickAnywhereAdvances = true;
        [SerializeField] private float charDelaySeconds = 0.03f;
        [SerializeField] private float autoAdvanceDelaySeconds = 0f; // 0 = no auto advance

        Action _currentOnLineComplete;
        Coroutine _typewriterRoutine;

        bool _isVisible;

        void Awake()
        {
            if (root != null)
                root.gameObject.SetActive(false);

            if (nextButton != null)
                nextButton.onClick.AddListener(HandleAdvanceRequested);
        }

        void OnDestroy()
        {
            if (nextButton != null)
                nextButton.onClick.RemoveListener(HandleAdvanceRequested);
        }

        public void ShowDialogueSet(DialogueSet set)
        {
            if (root == null) return;

            _isVisible = true;
            root.gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete)
        {
            _currentOnLineComplete = onLineComplete;

            if (root == null) return;

            // Choose portrait based on speaker
            if (captainIcon != null)
            {
                Sprite portrait = null;
                switch (line.speaker)
                {
                    case DialogueSpeaker.Speaker1:
                        portrait = set.portraitSpeaker1;
                        break;
                    case DialogueSpeaker.Speaker2:
                        portrait = set.portraitSpeaker2;
                        break;
                }
                if (portrait != null)
                    captainIcon.sprite = portrait;
            }

            if (speakerNameText != null)
                speakerNameText.text = line.speakerName;

            if (bodyText != null)
            {
                if (_typewriterRoutine != null)
                    StopCoroutine(_typewriterRoutine);

                _typewriterRoutine = StartCoroutine(TypewriterRoutine(
                    bodyText,
                    line.text,
                    () =>
                    {
                        if (nextButton != null)
                            nextButton.gameObject.SetActive(true);

                        if (autoAdvanceDelaySeconds > 0f)
                            StartCoroutine(AutoAdvanceAfterDelay(autoAdvanceDelaySeconds));
                    }));
            }

            if (nextButton != null)
                nextButton.gameObject.SetActive(false);
        }

        public void Hide(Action onHidden)
        {
            _isVisible = false;

            if (_typewriterRoutine != null)
            {
                StopCoroutine(_typewriterRoutine);
                _typewriterRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (root != null)
                root.gameObject.SetActive(false);

            _currentOnLineComplete = null;
            onHidden?.Invoke();
        }

        IEnumerator TypewriterRoutine(TMP_Text target, string fullText, Action onComplete)
        {
            target.text = string.Empty;

            foreach (char c in fullText)
            {
                target.text += c;
                yield return new WaitForSecondsRealtime(charDelaySeconds);
            }

            onComplete?.Invoke();
        }

        IEnumerator AutoAdvanceAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            // Still visible and we haven't advanced manually?
            if (_isVisible && _currentOnLineComplete != null)
            {
                HandleAdvanceRequested();
            }
        }

        void HandleAdvanceRequested()
        {
            if (_currentOnLineComplete == null)
                return;

            // Complete current line and move to next
            var callback = _currentOnLineComplete;
            _currentOnLineComplete = null;

            if (nextButton != null)
                nextButton.gameObject.SetActive(false);

            callback.Invoke();
        }

        // Optional: if you want click-anywhere to advance, hook this from the root's button/event
        public void HandleRootClicked()
        {
            if (!clickAnywhereAdvances)
                return;

            HandleAdvanceRequested();
        }
    }
}
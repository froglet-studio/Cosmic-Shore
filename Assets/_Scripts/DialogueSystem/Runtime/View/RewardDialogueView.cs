using System;
using System.Collections;
using CosmicShore.DialogueSystem.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.DialogueSystem.View
{
    /// <summary>
    /// Dialogue view that emphasizes a reward panel.
    /// Typically used at the end of a mission / FTUE segment.
    /// </summary>
    public sealed class RewardDialogueView : MonoBehaviour, IDialogueView
    {
        [Header("Root")]
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Dialogue Text (Optional)")]
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Reward Panel")]
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private Image rewardImage;
        [SerializeField] private TMP_Text rewardTitleText;
        [SerializeField] private TMP_Text rewardDescriptionText;
        [SerializeField] private TMP_Text rewardRarityText;

        [Header("Interaction")]
        [SerializeField] private Button continueButton;
        [SerializeField] private float charDelaySeconds = 0.03f;

        Action _currentOnLineComplete;
        Coroutine _typewriterRoutine;

        void Awake()
        {
            if (root != null)
                root.gameObject.SetActive(false);

            if (continueButton != null)
                continueButton.onClick.AddListener(HandleContinueClicked);
        }

        void OnDestroy()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(HandleContinueClicked);
        }

        public void ShowDialogueSet(DialogueSet set)
        {
            if (root == null) return;

            root.gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            // Initialize reward panel once per set
            SetupRewardPanel(set.rewardData);
        }

        public void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete)
        {
            _currentOnLineComplete = onLineComplete;

            if (speakerNameText != null)
                speakerNameText.text = line.speakerName;

            if (bodyText != null)
            {
                if (_typewriterRoutine != null)
                    StopCoroutine(_typewriterRoutine);

                _typewriterRoutine = StartCoroutine(TypewriterRoutine(bodyText, line.text,
                    () =>
                    {
                        if (continueButton != null)
                            continueButton.gameObject.SetActive(true);
                    }));
            }
            else
            {
                // If there's no body text, just advance on button
                if (continueButton != null)
                    continueButton.gameObject.SetActive(true);
            }

            if (continueButton != null)
                continueButton.gameObject.SetActive(false);
        }

        public void Hide(Action onHidden)
        {
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

        void SetupRewardPanel(RewardData rewardData)
        {
            if (rewardRoot != null)
                rewardRoot.gameObject.SetActive(rewardData != null);

            if (rewardData == null)
                return;

            if (rewardImage != null)
                rewardImage.sprite = rewardData.rewardImage;

            if (rewardTitleText != null)
                rewardTitleText.text = rewardData.rewardType.ToString();

            if (rewardDescriptionText != null)
                rewardDescriptionText.text = rewardData.description;

            if (rewardRarityText != null)
                rewardRarityText.text = rewardData.rarity.ToString().ToUpperInvariant();
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

        void HandleContinueClicked()
        {
            if (_currentOnLineComplete == null)
                return;

            var callback = _currentOnLineComplete;
            _currentOnLineComplete = null;

            if (continueButton != null)
                continueButton.gameObject.SetActive(false);

            callback.Invoke();
        }
    }
}
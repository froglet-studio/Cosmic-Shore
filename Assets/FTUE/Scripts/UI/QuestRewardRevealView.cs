using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Reward reveal panel (lives inside the profile screen). Implements
    /// <see cref="IDialogueView"/> for the REWARD channel: wire it into
    /// DialogueViewResolver's reward slot, and the graph's Reward-channel Dialogue
    /// nodes (played right after a claim) open it with the set's RewardData.
    ///
    /// Authoring: a reward DialogueSet needs at least ONE line (DialogueManager skips
    /// empty sets) — use the line as the flavor text; Continue steps lines and closes.
    /// </summary>
    public class QuestRewardRevealView : MonoBehaviour, IDialogueView
    {
        [Header("References")]
        [Tooltip("Root CanvasGroup faded in/out. Auto-resolved/added if unset.")]
        [SerializeField] CanvasGroup panel;
        [SerializeField] Image rewardImage;
        [Tooltip("Headline — shows RewardData.rewardValue (e.g. 'HexRace').")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text rarityText;
        [Tooltip("Optional flavor line — shows the current DialogueLine's text.")]
        [SerializeField] TMP_Text lineText;
        [SerializeField] Button continueButton;

        [Header("Timing")]
        [Min(0f)] [SerializeField] float fadeDuration = 0.25f;

        Coroutine _fade;
        Action _onLineComplete;

        void Awake()
        {
            if (panel == null && !TryGetComponent(out panel))
                panel = gameObject.AddComponent<CanvasGroup>();

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinuePressed);

            SetVisible(false, instant: true);
        }

        // ── IDialogueView ──────────────────────────────────────────────

        public void ShowDialogueSet(DialogueSet set)
        {
            var reward = set.rewardData;
            if (reward != null)
            {
                if (rewardImage != null)
                {
                    rewardImage.sprite = reward.rewardImage;
                    rewardImage.enabled = reward.rewardImage != null;
                }
                if (titleText != null) titleText.text = reward.rewardValue;
                if (descriptionText != null) descriptionText.text = reward.description;
                if (rarityText != null) rarityText.text = reward.rarity.ToString();
            }

            SetVisible(true);
        }

        public void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete)
        {
            _onLineComplete = onLineComplete;
            if (lineText != null)
                lineText.text = line.text;
        }

        public void Hide(Action onHidden)
        {
            SetVisible(false, onDone: onHidden);
        }

        // ── Internals ──────────────────────────────────────────────────

        void OnContinuePressed()
        {
            var callback = _onLineComplete;
            _onLineComplete = null;
            callback?.Invoke();
        }

        void SetVisible(bool visible, bool instant = false, Action onDone = null)
        {
            if (_fade != null) StopCoroutine(_fade);

            panel.blocksRaycasts = visible;
            panel.interactable = visible;

            if (instant || fadeDuration <= 0f || !isActiveAndEnabled)
            {
                panel.alpha = visible ? 1f : 0f;
                onDone?.Invoke();
                return;
            }

            _fade = StartCoroutine(FadeRoutine(visible ? 1f : 0f, onDone));
        }

        IEnumerator FadeRoutine(float target, Action onDone)
        {
            float start = panel.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panel.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            panel.alpha = target;
            onDone?.Invoke();
        }
    }
}

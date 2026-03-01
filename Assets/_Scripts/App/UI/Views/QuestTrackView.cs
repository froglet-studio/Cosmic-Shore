using System.Collections;
using System.Collections.Generic;
using CosmicShore.App.UI.Elements;
using CosmicShore.Core;
using CosmicShore.Game.Progression;
using CosmicShore.Models;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Displays the game-mode quest progression chain.
    /// Cards sit above a horizontal fill bar that tracks completion progress.
    ///
    /// UI Setup:
    ///   progressBarFill — Image component (Type=Filled, FillMethod=Horizontal).
    ///     Must span the same width as questItemContainer so fillAmount aligns
    ///     with card centers. This is the AAA approach: no Unity Slider involved.
    ///
    ///   Fallback: if progressBarFill is null but progressSlider is set, the old
    ///     Slider-based path is used (less reliable alignment).
    /// </summary>
    public class QuestTrackView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SO_GameModeQuestList questList;

        [Header("Prefab")]
        [Tooltip("Must have a QuestItemCard component attached")]
        [SerializeField] private GameObject questItemPrefab;

        [Header("Container")]
        [SerializeField] private Transform questItemContainer;

        [Header("Scroll")]
        [Tooltip("Optional — auto-resolved from questItemContainer if null")]
        [SerializeField] private ScrollRect scrollRect;

        [Header("Progress Bar")]
        [Tooltip("Image with Type=Filled, FillMethod=Horizontal. Must span same width as card container.")]
        [SerializeField] private Image progressBarFill;

        [Header("Slider (Legacy Fallback)")]
        [Tooltip("Only used if progressBarFill is not set")]
        [SerializeField] private Slider progressSlider;

        [Header("Animation")]
        [SerializeField] private float sliderAnimDuration = 1f;
        [SerializeField] private Ease sliderEase = Ease.OutCubic;

        private readonly List<QuestItemCard> _cards = new();
        private Tween _fillTween;

        void OnEnable()
        {
            ConfigureScrollRect();
            LoadTrack();

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged += OnProgressionChanged;
        }

        void OnDisable()
        {
            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged -= OnProgressionChanged;

            KillTween();
        }

        void OnProgressionChanged(GameModeProgressionData data)
        {
            RefreshAllCards();
            AnimateProgressBar();
        }

        public void LoadTrack()
        {
            SpawnCards();
            // Defer the initial bar set by one frame so layout is fully resolved.
            StartCoroutine(SetProgressBarDeferred());
        }

        IEnumerator SetProgressBarDeferred()
        {
            yield return null;
            SetProgressBarImmediate();
        }

        // ── Scroll ───────────────────────────────────────────────────────────

        void ConfigureScrollRect()
        {
            if (scrollRect == null && questItemContainer != null)
                scrollRect = questItemContainer.GetComponentInParent<ScrollRect>();

            if (scrollRect != null)
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        // ── Spawning ────────────────────────────────────────────────────────────

        void SpawnCards()
        {
            ClearSpawned();

            if (questList == null || questList.Quests == null || questItemPrefab == null) return;

            for (int i = 0; i < questList.Quests.Count; i++)
            {
                var quest = questList.Quests[i];
                var go = Instantiate(questItemPrefab, questItemContainer);

                if (!go.TryGetComponent<QuestItemCard>(out var card))
                {
                    Destroy(go);
                    continue;
                }

                card.Configure(quest);
                card.SetState(GetCardState(i));
                BindClaimIfNeeded(card, i);
                _cards.Add(card);
            }

            if (questItemContainer is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        // ── State evaluation ────────────────────────────────────────────────────

        QuestItemState GetCardState(int questIndex)
        {
            var quest = questList.Quests[questIndex];
            var service = GameModeProgressionService.Instance;

            bool isFirstQuest = questIndex == 0;
            bool isUnlocked = isFirstQuest || (service != null && service.IsGameModeUnlocked(quest.GameMode));

            if (!isUnlocked)
                return QuestItemState.Locked;

            bool isQuestCompleted = service != null && service.IsQuestCompleted(quest.GameMode);

            bool isNextUnlocked = false;
            if (questIndex + 1 < questList.Quests.Count && service != null)
                isNextUnlocked = service.IsGameModeUnlocked(questList.Quests[questIndex + 1].GameMode);

            if (isNextUnlocked)
                return QuestItemState.Claimed;

            if (isQuestCompleted)
                return QuestItemState.ReadyToClaim;

            return QuestItemState.Unlocked;
        }

        void BindClaimIfNeeded(QuestItemCard card, int questIndex)
        {
            var state = GetCardState(questIndex);
            if (state != QuestItemState.ReadyToClaim) return;

            var mode = questList.Quests[questIndex].GameMode;
            card.BindClaimAction(() => OnClaimPressed(mode));
        }

        void OnClaimPressed(GameModes completedMode)
        {
            GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(completedMode);
        }

        // ── Refresh ─────────────────────────────────────────────────────────────

        void RefreshAllCards()
        {
            for (int i = 0; i < _cards.Count && i < questList.Quests.Count; i++)
            {
                var card = _cards[i];
                var quest = questList.Quests[i];
                var state = GetCardState(i);

                card.SetState(state);

                if (state == QuestItemState.Claimed)
                    card.Configure(quest);

                card.SetState(state);

                if (state == QuestItemState.ReadyToClaim)
                {
                    var mode = quest.GameMode;
                    card.BindClaimAction(() => OnClaimPressed(mode));
                }
            }
        }

        // ── Progress Bar ────────────────────────────────────────────────────────

        void SetProgressBarImmediate()
        {
            float target = GetNormalizedProgress();

            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = target;
                return;
            }

            if (progressSlider != null)
                progressSlider.value = target;
        }

        void AnimateProgressBar()
        {
            KillTween();
            float target = GetNormalizedProgress();

            if (progressBarFill != null)
            {
                _fillTween = DOTween.To(
                        () => progressBarFill.fillAmount,
                        x => progressBarFill.fillAmount = x,
                        target,
                        sliderAnimDuration)
                    .SetEase(sliderEase)
                    .SetUpdate(true);
                return;
            }

            if (progressSlider != null)
            {
                _fillTween = progressSlider.DOValue(target, sliderAnimDuration)
                    .SetEase(sliderEase)
                    .SetUpdate(true);
            }
        }

        /// <summary>
        /// Returns a 0–1 value representing where the progress bar should fill to.
        /// Uses the target card's center position within the content container so
        /// the fill aligns directly below the card regardless of spacing or padding.
        /// </summary>
        float GetNormalizedProgress()
        {
            if (_cards.Count == 0) return 0f;

            var service = GameModeProgressionService.Instance;
            int claimed = service != null ? service.GetClaimedQuestCount() : 0;
            int targetIndex = Mathf.Clamp(claimed, 0, _cards.Count - 1);

            var contentRect = questItemContainer as RectTransform;
            var cardRect = _cards[targetIndex].transform as RectTransform;

            if (contentRect == null || cardRect == null)
                return _cards.Count > 0 ? (float)(1 + claimed) / _cards.Count : 0f;

            float contentWidth = contentRect.rect.width;
            if (contentWidth <= 0f) return 0f;

            // cardRect.localPosition.x is relative to parent pivot.
            // contentRect.rect.xMin is the left edge relative to pivot.
            // Subtracting gives the card center measured from the content's left edge.
            float cardCenterFromLeft = cardRect.localPosition.x - contentRect.rect.xMin;

            return Mathf.Clamp01(cardCenterFromLeft / contentWidth);
        }

        // ── Cleanup ─────────────────────────────────────────────────────────────

        void ClearSpawned()
        {
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();
        }

        void KillTween()
        {
            if (_fillTween != null && _fillTween.IsActive())
            {
                _fillTween.Kill();
                _fillTween = null;
            }
        }
    }
}

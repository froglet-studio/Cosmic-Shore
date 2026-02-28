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
    /// Each quest item card shows its own name and description via QuestItemCard.
    /// The slider sits below the card container and shows overall quest completion.
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

        [Header("Slider")]
        [SerializeField] private Slider progressSlider;

        [Header("Animation")]
        [SerializeField] private float sliderAnimDuration = 1f;
        [SerializeField] private Ease sliderEase = Ease.OutCubic;

        private readonly List<QuestItemCard> _cards = new();
        private Tween _sliderTween;

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
            AnimateSlider();
        }

        public void LoadTrack()
        {
            SpawnCards();
            SetSliderImmediate();
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

            // Check if the NEXT mode is already unlocked (meaning this quest was claimed)
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

                // Re-set description when transitioning to Claimed
                if (state == QuestItemState.Claimed)
                    card.Configure(quest); // resets description, then SetState overrides to "Completed"

                card.SetState(state);

                // Re-bind claim button if the state just became ReadyToClaim
                if (state == QuestItemState.ReadyToClaim)
                {
                    var mode = quest.GameMode;
                    card.BindClaimAction(() => OnClaimPressed(mode));
                }
            }
        }

        // ── Slider ──────────────────────────────────────────────────────────────

        void SetSliderImmediate()
        {
            if (progressSlider == null) return;

            // Force all canvas layouts so card world positions are accurate
            Canvas.ForceUpdateCanvases();
            progressSlider.value = GetNormalizedProgress();
        }

        void AnimateSlider()
        {
            if (progressSlider == null) return;

            KillTween();
            float target = GetNormalizedProgress();
            _sliderTween = progressSlider.DOValue(target, sliderAnimDuration)
                .SetEase(sliderEase)
                .SetUpdate(true);
        }

        /// <summary>
        /// Calculates the slider value so that the fill aligns with the center
        /// of the current card (the first unclaimed card) in world space.
        /// </summary>
        float GetNormalizedProgress()
        {
            if (_cards.Count == 0 || progressSlider == null) return 0f;

            var service = GameModeProgressionService.Instance;
            int claimed = service != null ? service.GetClaimedQuestCount() : 0;

            // Slider points to the center of the current active card.
            // With 0 claimed → card 0 (first, always unlocked).
            // With N claimed → card N (next unclaimed).
            int targetIndex = Mathf.Clamp(claimed, 0, _cards.Count - 1);

            var cardRect = _cards[targetIndex].transform as RectTransform;
            var sliderRect = progressSlider.transform as RectTransform;
            if (cardRect == null || sliderRect == null) return 0f;

            // Card center in world space
            Vector3[] cardCorners = new Vector3[4];
            cardRect.GetWorldCorners(cardCorners);
            float cardCenterWorldX = (cardCorners[0].x + cardCorners[2].x) * 0.5f;

            // Slider extent in world space
            Vector3[] sliderCorners = new Vector3[4];
            sliderRect.GetWorldCorners(sliderCorners);
            float sliderLeftX = sliderCorners[0].x;
            float sliderWidth = sliderCorners[2].x - sliderLeftX;

            if (sliderWidth <= 0f) return 0f;

            return Mathf.Clamp01((cardCenterWorldX - sliderLeftX) / sliderWidth);
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
            if (_sliderTween != null && _sliderTween.IsActive())
            {
                _sliderTween.Kill();
                _sliderTween = null;
            }
        }
    }
}

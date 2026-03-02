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
    public class QuestTrackView : MonoBehaviour
    {
        [SerializeField] private SO_GameModeQuestList questList;
        [SerializeField] private GameObject questItemPrefab;
        [SerializeField] private Transform questItemContainer;

        [Header("Slider")]
        [Tooltip("Unity Slider whose maxValue = quest count. Each claimed quest = 1 whole unit.")]
        [SerializeField] private Slider progressBarSlider;
        [SerializeField] private float sliderAnimDuration = 1f;
        [SerializeField] private Ease sliderEase = Ease.OutCubic;

        private readonly List<QuestItemCard> _cards = new();
        private Tween _sliderTween;

        void OnEnable()
        {
            EnsureSliderIgnoresLayout();
            SpawnCards();
            ConfigureSlider();
            RefreshAllCards();
            SetSliderImmediate();
            StartCoroutine(RebuildLayoutNextFrame());

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

        /// <summary>
        /// Ensures the slider is excluded from the HorizontalLayoutGroup.
        /// Does NOT touch position, anchors, or sibling order — those stay as set in the inspector.
        /// </summary>
        void EnsureSliderIgnoresLayout()
        {
            if (progressBarSlider == null) return;
            if (!progressBarSlider.TryGetComponent<LayoutElement>(out var le))
                le = progressBarSlider.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        void SpawnCards()
        {
            ClearSpawned();
            if (questList == null || questList.Quests == null || questItemPrefab == null) return;

            for (int i = 0; i < questList.Quests.Count; i++)
            {
                var go = Instantiate(questItemPrefab, questItemContainer);
                if (!go.TryGetComponent<QuestItemCard>(out var card)) { Destroy(go); continue; }

                card.Configure(questList.Quests[i]);
                card.SetState(GetCardState(i));
                var mode = questList.Quests[i].GameMode;
                card.BindClaimAction(() => GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(mode));
                _cards.Add(card);
            }
        }

        /// <summary>
        /// Sets slider range: min=0, max=quest count. Each quest = 1 whole unit.
        /// Padding of XPItemPanels is set manually in the inspector so cards
        /// line up with the integer tick marks on the slider.
        /// </summary>
        void ConfigureSlider()
        {
            if (progressBarSlider == null || questList == null) return;
            progressBarSlider.interactable = false;
            progressBarSlider.wholeNumbers = true;
            progressBarSlider.minValue = 0;
            progressBarSlider.maxValue = questList.Quests.Count;
        }

        QuestItemState GetCardState(int questIndex)
        {
            var quest = questList.Quests[questIndex];
            var service = GameModeProgressionService.Instance;
            bool isUnlocked = questIndex == 0 || (service != null && service.IsGameModeUnlocked(quest.GameMode));
            if (!isUnlocked) return QuestItemState.Locked;

            // Already claimed — next mode is unlocked
            if (questIndex + 1 < questList.Quests.Count && service != null
                && service.IsGameModeUnlocked(questList.Quests[questIndex + 1].GameMode))
                return QuestItemState.Claimed;

            // Placeholder quests are immediately claimable (skip straight to unlock next)
            if (quest.IsPlaceholder)
                return QuestItemState.ReadyToClaim;

            if (service != null && service.IsQuestCompleted(quest.GameMode))
                return QuestItemState.ReadyToClaim;

            return QuestItemState.Unlocked;
        }

        void RefreshAllCards()
        {
            for (int i = 0; i < _cards.Count && i < questList.Quests.Count; i++)
                _cards[i].SetState(GetCardState(i));
        }

        void SetSliderImmediate()
        {
            if (progressBarSlider != null)
                progressBarSlider.value = GetSliderValue();
        }

        void AnimateSlider()
        {
            if (progressBarSlider == null) return;
            KillTween();
            float target = GetSliderValue();
            _sliderTween = DOTween.To(
                    () => progressBarSlider.value,
                    x => progressBarSlider.value = x,
                    target, sliderAnimDuration)
                .SetEase(sliderEase).SetUpdate(true);
        }

        /// <summary>
        /// Returns the 1-indexed frontier position on the slider.
        /// Counts consecutive unlocked modes from the start of the chain.
        /// Fresh state = 1 (first quest active). All claimed = questCount.
        /// </summary>
        int GetSliderValue()
        {
            var service = GameModeProgressionService.Instance;
            if (service == null || questList == null) return 0;

            int count = 0;
            for (int i = 0; i < questList.Quests.Count; i++)
            {
                if (i == 0 || service.IsGameModeUnlocked(questList.Quests[i].GameMode))
                    count++;
                else
                    break;
            }
            return count;
        }

        /// <summary>
        /// Wait one frame so the spawned card prefabs have initialized their
        /// preferred sizes, then rebuild the layout so ContentSizeFitter on
        /// XPItemPanels calculates the correct content width for scrolling.
        /// </summary>
        IEnumerator RebuildLayoutNextFrame()
        {
            yield return null;
            if (questItemContainer is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        void ClearSpawned()
        {
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();
        }

        void KillTween()
        {
            if (_sliderTween != null && _sliderTween.IsActive()) _sliderTween.Kill();
            _sliderTween = null;
        }
    }
}

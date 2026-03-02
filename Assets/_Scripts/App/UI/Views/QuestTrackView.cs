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
        [SerializeField] private Image progressBarFill;
        [SerializeField] private float sliderAnimDuration = 1f;
        [SerializeField] private Ease sliderEase = Ease.OutCubic;

        private readonly List<QuestItemCard> _cards = new();
        private Tween _fillTween;

        void OnEnable()
        {
            EnsureProgressBarSetup();
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

        void EnsureProgressBarSetup()
        {
            if (progressBarFill == null || questItemContainer == null) return;

            var barRoot = FindDirectChild(progressBarFill.transform, questItemContainer);
            if (barRoot == null) return;

            // Keep the progress bar out of the HorizontalLayoutGroup
            if (!barRoot.TryGetComponent<LayoutElement>(out var le))
                le = barRoot.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // Render behind all cards
            barRoot.SetAsFirstSibling();

            // Stretch full width, pin to bottom
            if (barRoot is RectTransform barRect)
            {
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(1f, 0f);
                barRect.offsetMin = new Vector2(0f, 0f);
                barRect.offsetMax = new Vector2(0f, 20f);
            }
        }

        static Transform FindDirectChild(Transform descendant, Transform parent)
        {
            var current = descendant;
            while (current != null && current != parent)
            {
                if (current.parent == parent) return current;
                current = current.parent;
            }
            return null;
        }

        public void LoadTrack()
        {
            SpawnCards();
            StartCoroutine(PostLayoutSetup());
        }

        IEnumerator PostLayoutSetup()
        {
            yield return null;
            SetProgressBarImmediate();
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

            if (questItemContainer is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        QuestItemState GetCardState(int questIndex)
        {
            var quest = questList.Quests[questIndex];
            var service = GameModeProgressionService.Instance;
            bool isUnlocked = questIndex == 0 || (service != null && service.IsGameModeUnlocked(quest.GameMode));
            if (!isUnlocked) return QuestItemState.Locked;

            if (questIndex + 1 < questList.Quests.Count && service != null
                && service.IsGameModeUnlocked(questList.Quests[questIndex + 1].GameMode))
                return QuestItemState.Claimed;

            if (service != null && service.IsQuestCompleted(quest.GameMode))
                return QuestItemState.ReadyToClaim;

            return QuestItemState.Unlocked;
        }

        void RefreshAllCards()
        {
            for (int i = 0; i < _cards.Count && i < questList.Quests.Count; i++)
                _cards[i].SetState(GetCardState(i));
        }

        void SetProgressBarImmediate()
        {
            if (progressBarFill != null)
                progressBarFill.fillAmount = GetNormalizedProgress();
        }

        void AnimateProgressBar()
        {
            if (progressBarFill == null) return;
            KillTween();
            float target = GetNormalizedProgress();
            _fillTween = DOTween.To(
                    () => progressBarFill.fillAmount,
                    x => progressBarFill.fillAmount = x,
                    target, sliderAnimDuration)
                .SetEase(sliderEase).SetUpdate(true);
        }

        float GetNormalizedProgress()
        {
            if (_cards.Count <= 1) return 0f;
            int claimed = GameModeProgressionService.Instance?.GetClaimedQuestCount() ?? 0;
            if (claimed <= 0) return 0f;
            return Mathf.Clamp01((float)claimed / (_cards.Count - 1));
        }

        void ClearSpawned()
        {
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();
        }

        void KillTween()
        {
            if (_fillTween != null && _fillTween.IsActive()) _fillTween.Kill();
            _fillTween = null;
        }
    }
}
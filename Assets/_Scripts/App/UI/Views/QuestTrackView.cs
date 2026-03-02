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

        [Header("Scroll Snap")]
        [Tooltip("The ScrollRect parent. Snap to nearest card after user finishes scrolling.")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private float snapDuration = 0.3f;
        [Tooltip("Velocity threshold below which a snap triggers after user scroll")]
        [SerializeField] private float snapSpeedThreshold = 50f;

        [Header("Parallax Depth")]
        [Tooltip("Scale of cards at the edge of the viewport (1.0 = no effect)")]
        [SerializeField] private float minCardScale = 0.85f;
        [Tooltip("Alpha of cards at the edge of the viewport")]
        [SerializeField] private float minCardAlpha = 0.7f;
        [Tooltip("World-space distance from viewport center at which min values are reached")]
        [SerializeField] private float parallaxFalloff = 400f;

        private readonly List<QuestItemCard> _cards = new();
        private Tween _sliderTween;
        private Tween _snapTween;
        private bool _wasMoving;
        private bool _isSnapping;

        void OnEnable()
        {
            EnsureSliderIgnoresLayout();
            SpawnCards();
            ConfigureSlider();
            RefreshAllCards();
            UpdateActivePulse();
            SetSliderImmediate();
            StartCoroutine(PostSpawnSetup());

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged += OnProgressionChanged;
        }

        void OnDisable()
        {
            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged -= OnProgressionChanged;
            KillAllTweens();
        }

        void LateUpdate()
        {
            if (scrollRect == null || _cards.Count == 0) return;

            UpdateParallax();

            if (_isSnapping) return;

            float vel = Mathf.Abs(scrollRect.velocity.x);
            bool isMoving = vel > snapSpeedThreshold;

            // User scroll momentum just settled → snap to nearest card
            if (_wasMoving && !isMoving)
                SnapToNearestCard();

            _wasMoving = isMoving;
        }

        void OnProgressionChanged(GameModeProgressionData data)
        {
            RefreshAllCards();
            UpdateActivePulse();
            AnimateSlider();
        }

        // ── Setup ─────────────────────────────────────────────────────────────

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

                // Ensure CanvasGroup exists for parallax alpha
                if (!go.TryGetComponent<CanvasGroup>(out _))
                    go.AddComponent<CanvasGroup>();

                card.Configure(questList.Quests[i]);
                card.SetState(GetCardState(i));

                int cardIndex = i;
                card.BindClaimAction(() => HandleClaimPressed(cardIndex));
                _cards.Add(card);
            }
        }

        void ConfigureSlider()
        {
            if (progressBarSlider == null || questList == null) return;
            progressBarSlider.interactable = false;
            progressBarSlider.wholeNumbers = true;
            progressBarSlider.minValue = 0;
            progressBarSlider.maxValue = questList.Quests.Count;
        }

        /// <summary>
        /// After one frame (layout built), snap scroll to the active quest card
        /// </summary>
        IEnumerator PostSpawnSetup()
        {
            yield return null;
            if (questItemContainer is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            int activeIndex = GetActiveQuestIndex();
            if (activeIndex >= 0)
                SnapToCard(activeIndex, true);
        }

        // ── Card State ────────────────────────────────────────────────────────

        QuestItemState GetCardState(int questIndex)
        {
            var quest = questList.Quests[questIndex];
            var service = GameModeProgressionService.Instance;
            bool isUnlocked = questIndex == 0 || (service != null && service.IsGameModeUnlocked(quest.GameMode));
            if (!isUnlocked) return QuestItemState.Locked;

            if (questIndex + 1 < questList.Quests.Count && service != null
                && service.IsGameModeUnlocked(questList.Quests[questIndex + 1].GameMode))
                return QuestItemState.Claimed;

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

        /// <summary>
        /// Returns the index of the first quest that is Unlocked or ReadyToClaim (the frontier).
        /// Falls back to the last card if all are claimed.
        /// </summary>
        int GetActiveQuestIndex()
        {
            for (int i = 0; i < questList.Quests.Count; i++)
            {
                var state = GetCardState(i);
                if (state == QuestItemState.Unlocked || state == QuestItemState.ReadyToClaim)
                    return i;
            }
            return _cards.Count > 0 ? _cards.Count - 1 : -1;
        }

        void UpdateActivePulse()
        {
            int activeIndex = GetActiveQuestIndex();
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i == activeIndex)
                    _cards[i].SetActiveFrontier(true, GetCardState(i));
                else
                    _cards[i].SetActiveFrontier(false);
            }
        }

        // ── Claim Fanfare ─────────────────────────────────────────────────────

        void HandleClaimPressed(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _cards.Count || cardIndex >= questList.Quests.Count) return;

            var card = _cards[cardIndex];
            var quest = questList.Quests[cardIndex];

            // Prevent double-click
            card.SetButtonInteractable(false);

            // Scale-bounce → then process the claim
            card.PlayClaimAnimation(() =>
            {
                GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(quest.GameMode);
                // OnProgressionChanged fires → RefreshAllCards + AnimateSlider + UpdateActivePulse
                // After slider animation settles, scroll to the newly unlocked card
                if (cardIndex + 1 < _cards.Count)
                    StartCoroutine(SnapToCardDelayed(cardIndex + 1));
            });
        }

        IEnumerator SnapToCardDelayed(int cardIndex)
        {
            // Wait for slider animation to start, then scroll slowly alongside it
            yield return new WaitForSeconds(0.3f);
            SnapToCard(cardIndex, false, sliderAnimDuration);
        }

        // ── Slider ────────────────────────────────────────────────────────────

        void SetSliderImmediate()
        {
            if (progressBarSlider != null)
                progressBarSlider.value = GetSliderValue();
        }

        void AnimateSlider()
        {
            if (progressBarSlider == null) return;
            _sliderTween?.Kill();
            float target = GetSliderValue();
            _sliderTween = DOTween.To(
                    () => progressBarSlider.value,
                    x => progressBarSlider.value = x,
                    target, sliderAnimDuration)
                .SetEase(sliderEase).SetUpdate(true);
        }

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

        // ── Scroll Snap ──────────────────────────────────────────────────────

        void SnapToNearestCard()
        {
            int nearest = FindNearestCardToViewportCenter();
            if (nearest >= 0)
                SnapToCard(nearest, false);
        }

        void SnapToCard(int cardIndex, bool immediate, float duration = -1f)
        {
            if (scrollRect == null || _cards.Count == 0 || cardIndex < 0 || cardIndex >= _cards.Count) return;

            var contentRect = questItemContainer as RectTransform;
            var viewportRect = scrollRect.viewport ?? scrollRect.GetComponent<RectTransform>();
            if (contentRect == null || viewportRect == null) return;

            float contentWidth = contentRect.rect.width;
            float viewportWidth = viewportRect.rect.width;
            float scrollableRange = contentWidth - viewportWidth;

            if (scrollableRange <= 0f) return;

            // Card center in content-local x, adjusted from pivot to left edge
            var cardRect = _cards[cardIndex].GetComponent<RectTransform>();
            float cardCenterX = cardRect.localPosition.x + contentRect.rect.width * contentRect.pivot.x;

            float target = Mathf.Clamp01((cardCenterX - viewportWidth * 0.5f) / scrollableRange);

            _snapTween?.Kill();

            if (immediate)
            {
                scrollRect.horizontalNormalizedPosition = target;
                _isSnapping = false;
            }
            else
            {
                float dur = duration > 0f ? duration : snapDuration;
                _isSnapping = true;
                scrollRect.velocity = Vector2.zero;
                _snapTween = DOTween.To(
                        () => scrollRect.horizontalNormalizedPosition,
                        x => scrollRect.horizontalNormalizedPosition = x,
                        target, dur)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .OnComplete(() => { _isSnapping = false; _snapTween = null; });
            }
        }

        int FindNearestCardToViewportCenter()
        {
            if (scrollRect == null || _cards.Count == 0) return -1;

            var viewportRect = scrollRect.viewport ?? scrollRect.GetComponent<RectTransform>();
            Vector3 viewportCenter = viewportRect.TransformPoint(viewportRect.rect.center);

            int nearest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < _cards.Count; i++)
            {
                var cardRect = _cards[i].GetComponent<RectTransform>();
                Vector3 cardCenter = cardRect.TransformPoint(cardRect.rect.center);
                float dist = Mathf.Abs(cardCenter.x - viewportCenter.x);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        // ── Parallax Depth ────────────────────────────────────────────────────

        void UpdateParallax()
        {
            if (scrollRect == null || _cards.Count == 0 || parallaxFalloff <= 0f) return;

            var viewportRect = scrollRect.viewport ?? scrollRect.GetComponent<RectTransform>();
            Vector3 viewportCenter = viewportRect.TransformPoint(viewportRect.rect.center);

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null || card.IsAnimating) continue;

                var cardRect = card.GetComponent<RectTransform>();
                Vector3 cardCenter = cardRect.TransformPoint(cardRect.rect.center);
                float distance = Mathf.Abs(cardCenter.x - viewportCenter.x);
                float t = Mathf.Clamp01(distance / parallaxFalloff);

                card.transform.localScale = Vector3.one * Mathf.Lerp(1f, minCardScale, t);

                if (card.TryGetComponent<CanvasGroup>(out var cg))
                    cg.alpha = Mathf.Lerp(1f, minCardAlpha, t);
            }
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        void ClearSpawned()
        {
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();
        }

        void KillAllTweens()
        {
            _sliderTween?.Kill(); _sliderTween = null;
            _snapTween?.Kill(); _snapTween = null;
            _isSnapping = false;
            _wasMoving = false;
        }
    }
}

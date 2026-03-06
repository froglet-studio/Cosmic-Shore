using System.Collections;
using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
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

        [Header("Ghost Slider")]
        [Tooltip("Second slider that shows one step ahead of the main slider (the next quest to complete).")]
        [SerializeField] private Slider ghostSlider;

        [Header("Quest Descriptions")]
        [Tooltip("Container with HorizontalLayoutGroup inside the ScrollRect content, aligned under the quest cards.")]
        [SerializeField] private Transform questDescriptionContainer;
        [Tooltip("Prefab with TMP_Text (+ CanvasGroup). One spawned per quest, aligned 1:1 with cards.")]
        [SerializeField] private GameObject questDescriptionPrefab;
        [SerializeField] private float descriptionFadeDuration = 0.4f;

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
        private readonly List<CanvasGroup> _descriptionLabels = new();
        private Tween _sliderTween;
        private Tween _ghostSliderTween;
        private Tween _descFadeTween;
        private Tween _claimSequence;
        private Tween _snapTween;
        private bool _wasMoving;
        private bool _isSnapping;
        private bool _isPlayingClaimSequence;
        private int _lastActiveDescIndex = -1;

        void OnEnable()
        {
            EnsureSliderIgnoresLayout();
            SpawnCards();
            SpawnDescriptionLabels();
            ConfigureSlider();
            ConfigureGhostSlider();
            RefreshAllCards();
            UpdateActivePulse();
            SetSliderImmediate();
            SetGhostSliderImmediate();
            UpdateActiveDescription(true);
            StartCoroutine(PostSpawnSetup());

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged += OnProgressionChanged;
        }

        void OnDisable()
        {
            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged -= OnProgressionChanged;
            KillAllTweens();
            ClearDescriptionLabels();
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
            // During the claim sequence, visuals are driven by the choreographed timeline
            if (_isPlayingClaimSequence) return;

            RefreshAllCards();
            UpdateActivePulse();
            AnimateSlider();
            AnimateGhostSlider();
            UpdateActiveDescription(false);
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
        //
        //  Choreographed timeline:
        //  1. Button clicked → claim animation (scale bounce)
        //  2. Description text fades out
        //  3. Slider + ghost slider start moving to next position
        //     + scroll view begins panning to the next card
        //  4. As slider leaves current card (~35%) → current card → Claimed
        //  5. As slider reaches next card (~90%) → next card → Unlocked, pulse moves
        //  6. Description text fades in with new quest goal
        //

        void HandleClaimPressed(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _cards.Count || cardIndex >= questList.Quests.Count) return;
            if (_isPlayingClaimSequence) return;

            var card = _cards[cardIndex];
            var quest = questList.Quests[cardIndex];

            card.SetButtonInteractable(false);

            // Scale-bounce → then kick off the full choreographed sequence
            card.PlayClaimAnimation(() => PlayClaimSequence(cardIndex, quest));
        }

        void PlayClaimSequence(int cardIndex, SO_GameModeQuestData quest)
        {
            _isPlayingClaimSequence = true;
            _claimSequence?.Kill();

            // Commit the data change — OnProgressionChanged will be skipped due to the flag
            GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(quest.GameMode);

            // Pre-compute slider targets from the now-updated data
            int newSliderVal = GetSliderValue();
            int newGhostVal = Mathf.Min(newSliderVal + 1, questList?.Quests.Count ?? 0);
            int nextCardIndex = cardIndex + 1;
            bool hasNextCard = nextCardIndex < _cards.Count;
            float fadeDur = descriptionFadeDuration * 0.5f;

            var seq = DOTween.Sequence();

            // ── Step 1: Fade out current description ────────────────────────
            if (_lastActiveDescIndex >= 0 && _lastActiveDescIndex < _descriptionLabels.Count)
            {
                var oldCg = _descriptionLabels[_lastActiveDescIndex];
                seq.Append(DOTween.To(() => oldCg.alpha, a => oldCg.alpha = a, 0f, fadeDur));
            }

            // ── Step 2: Slider + ghost slider start moving ──────────────────
            // Also begin scrolling to the next card alongside the slider
            float sliderStart = seq.Duration();

            if (progressBarSlider != null)
            {
                _sliderTween?.Kill();
                seq.Insert(sliderStart, DOTween.To(
                    () => progressBarSlider.value,
                    x => progressBarSlider.value = x,
                    newSliderVal, sliderAnimDuration).SetEase(sliderEase));
            }

            if (ghostSlider != null)
            {
                _ghostSliderTween?.Kill();
                seq.Insert(sliderStart, DOTween.To(
                    () => ghostSlider.value,
                    x => ghostSlider.value = x,
                    newGhostVal, sliderAnimDuration).SetEase(sliderEase));
            }

            // Scroll to next card in parallel with the sliders
            if (hasNextCard && scrollRect != null)
            {
                float scrollDelay = sliderStart + 0.15f;
                seq.InsertCallback(scrollDelay, () => SnapToCard(nextCardIndex, false, sliderAnimDuration));
            }

            // ── Step 3: Slider leaves current card (~35%) → mark Claimed ────
            float claimedTime = sliderStart + sliderAnimDuration * 0.35f;
            seq.InsertCallback(claimedTime, () =>
            {
                _cards[cardIndex].SetState(QuestItemState.Claimed);
                _cards[cardIndex].SetActiveFrontier(false);
            });

            // ── Step 4: Slider reaches next card (~90%) → unlock + pulse ────
            if (hasNextCard)
            {
                float unlockTime = sliderStart + sliderAnimDuration * 0.9f;
                seq.InsertCallback(unlockTime, () =>
                {
                    _cards[nextCardIndex].SetState(GetCardState(nextCardIndex));
                    UpdateActivePulse();
                });
            }

            // ── Step 5: Fade in new description ─────────────────────────────
            float fadeInTime = sliderStart + sliderAnimDuration;
            int newActiveIndex = hasNextCard ? nextCardIndex : cardIndex;

            if (newActiveIndex >= 0 && newActiveIndex < _descriptionLabels.Count)
            {
                var newCg = _descriptionLabels[newActiveIndex];
                seq.Insert(fadeInTime, DOTween.To(() => newCg.alpha, a => newCg.alpha = a, 1f, fadeDur));
            }
            _lastActiveDescIndex = newActiveIndex;

            // ── Cleanup ─────────────────────────────────────────────────────
            seq.OnComplete(() =>
            {
                _isPlayingClaimSequence = false;
                // Final sync — ensure all cards reflect the true data state
                RefreshAllCards();
                UpdateActivePulse();
            });

            seq.SetUpdate(true);
            _claimSequence = seq;
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

        // ── Ghost Slider ────────────────────────────────────────────────────

        void ConfigureGhostSlider()
        {
            if (ghostSlider == null || questList == null) return;
            ghostSlider.interactable = false;
            ghostSlider.wholeNumbers = true;
            ghostSlider.minValue = 0;
            ghostSlider.maxValue = questList.Quests.Count;
        }

        void SetGhostSliderImmediate()
        {
            int mainVal = GetSliderValue();
            int ghostVal = Mathf.Min(mainVal + 1, questList?.Quests.Count ?? 0);

            if (ghostSlider != null)
                ghostSlider.value = ghostVal;
        }

        void AnimateGhostSlider()
        {
            int mainVal = GetSliderValue();
            int ghostVal = Mathf.Min(mainVal + 1, questList?.Quests.Count ?? 0);

            if (ghostSlider != null)
            {
                _ghostSliderTween?.Kill();
                _ghostSliderTween = DOTween.To(
                        () => ghostSlider.value,
                        x => ghostSlider.value = x,
                        ghostVal, sliderAnimDuration)
                    .SetEase(sliderEase).SetUpdate(true);
            }
        }

        // ── Quest Description Labels ─────────────────────────────────────────

        void SpawnDescriptionLabels()
        {
            ClearDescriptionLabels();
            if (questList == null || questList.Quests == null || questDescriptionPrefab == null || questDescriptionContainer == null) return;

            for (int i = 0; i < questList.Quests.Count; i++)
            {
                var go = Instantiate(questDescriptionPrefab, questDescriptionContainer);
                var tmp = go.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    var quest = questList.Quests[i];
                    tmp.text = quest.IsPlaceholder ? "Coming Soon" : quest.Description;
                }

                if (!go.TryGetComponent<CanvasGroup>(out var cg))
                    cg = go.AddComponent<CanvasGroup>();

                cg.alpha = 0f;
                _descriptionLabels.Add(cg);
            }
        }

        void ClearDescriptionLabels()
        {
            foreach (var cg in _descriptionLabels)
                if (cg != null) Destroy(cg.gameObject);
            _descriptionLabels.Clear();
            _lastActiveDescIndex = -1;
        }

        /// <summary>
        /// Shows only the current frontier quest's description label. Fades out the old, fades in the new.
        /// </summary>
        void UpdateActiveDescription(bool immediate)
        {
            if (_descriptionLabels.Count == 0) return;

            int activeIndex = GetActiveQuestIndex();

            if (activeIndex == _lastActiveDescIndex && !immediate) return;

            _descFadeTween?.Kill();

            if (immediate)
            {
                for (int i = 0; i < _descriptionLabels.Count; i++)
                    _descriptionLabels[i].alpha = (i == activeIndex) ? 1f : 0f;
                _lastActiveDescIndex = activeIndex;
                return;
            }

            // Animate: fade out old, fade in new
            var seq = DOTween.Sequence();
            float halfDur = descriptionFadeDuration * 0.5f;

            // Fade out old label
            if (_lastActiveDescIndex >= 0 && _lastActiveDescIndex < _descriptionLabels.Count)
            {
                var oldCg = _descriptionLabels[_lastActiveDescIndex];
                seq.Append(DOTween.To(() => oldCg.alpha, a => oldCg.alpha = a, 0f, halfDur));
            }

            // Fade in new label
            if (activeIndex >= 0 && activeIndex < _descriptionLabels.Count)
            {
                var newCg = _descriptionLabels[activeIndex];
                seq.Append(DOTween.To(() => newCg.alpha, a => newCg.alpha = a, 1f, halfDur));
            }

            seq.SetUpdate(true);
            _descFadeTween = seq;
            _lastActiveDescIndex = activeIndex;
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
            _ghostSliderTween?.Kill(); _ghostSliderTween = null;
            _descFadeTween?.Kill(); _descFadeTween = null;
            _claimSequence?.Kill(); _claimSequence = null;
            _snapTween?.Kill(); _snapTween = null;
            _isPlayingClaimSequence = false;
            _isSnapping = false;
            _wasMoving = false;
        }
    }
}

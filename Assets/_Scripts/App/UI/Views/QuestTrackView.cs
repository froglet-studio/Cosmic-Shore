using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Progression;
using CosmicShore.Models;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Displays the game-mode quest progression chain.
    /// Repurposes the same prefab hierarchy as the old XPTrackView:
    ///   - questItemPrefab has children: UnlockableIconBG/Icon, UnlockableDetail, LockedIcon, UnlockedObject, ClaimButton
    ///   - questLabelPrefab has a TMP_Text child for the quest name
    /// The slider shows how many quests have been completed out of the total.
    /// </summary>
    public class QuestTrackView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SO_GameModeQuestList questList;

        [Header("Prefabs")]
        [SerializeField] private GameObject questItemPrefab;
        [SerializeField] private GameObject questLabelPrefab;

        [Header("Containers")]
        [SerializeField] private Transform questItemPanels;
        [SerializeField] private Transform questLabelDisplayPanel;

        [Header("Slider")]
        [SerializeField] private Slider progressSlider;

        [Header("Status Label")]
        [SerializeField] private TMP_Text statusText;

        [Header("Animation")]
        [SerializeField] private float sliderAnimDuration = 1f;
        [SerializeField] private Ease sliderEase = Ease.OutCubic;

        private readonly List<GameObject> _spawnedItems = new();
        private readonly List<GameObject> _spawnedLabels = new();
        private Tween _sliderTween;

        void OnEnable()
        {
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
            RefreshQuestStates();
            AnimateSlider();
        }

        public void LoadTrack()
        {
            SpawnQuestItems();
            SetSliderImmediate();
        }

        void SpawnQuestItems()
        {
            ClearSpawned();

            if (questList == null || questList.Quests == null) return;

            var service = GameModeProgressionService.Instance;

            for (int i = 0; i < questList.Quests.Count; i++)
            {
                var quest = questList.Quests[i];
                string modeName = quest.GameMode.ToString();

                bool isUnlocked = i == 0 || (service != null && service.IsGameModeUnlocked(quest.GameMode));
                bool isQuestCompleted = service != null && service.IsQuestCompleted(quest.GameMode);

                // Also check if the NEXT mode is unlocked (meaning this quest was claimed)
                bool isQuestClaimed = false;
                if (i + 1 < questList.Quests.Count && service != null)
                    isQuestClaimed = service.IsGameModeUnlocked(questList.Quests[i + 1].GameMode);

                // Spawn quest item (reward/milestone display)
                if (questItemPrefab != null && questItemPanels != null)
                {
                    var itemGO = Instantiate(questItemPrefab, questItemPanels);
                    _spawnedItems.Add(itemGO);
                    SetupQuestItem(itemGO, quest, isUnlocked, isQuestCompleted, isQuestClaimed);
                }

                // Spawn quest label
                if (questLabelPrefab != null && questLabelDisplayPanel != null)
                {
                    var labelGO = Instantiate(questLabelPrefab, questLabelDisplayPanel);
                    _spawnedLabels.Add(labelGO);
                    var label = labelGO.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                        label.text = quest.DisplayName;
                }
            }

            // Force layout rebuild
            if (questItemPanels is RectTransform itemsRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            if (questLabelDisplayPanel is RectTransform labelsRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(labelsRect);
        }

        void SetupQuestItem(GameObject itemGO, SO_GameModeQuestData quest,
            bool isUnlocked, bool isQuestCompleted, bool isQuestClaimed)
        {
            // Icon
            var icon = itemGO.transform.Find("UnlockableIconBG/Icon");
            if (icon != null && quest.Icon != null)
            {
                if (icon.TryGetComponent<Image>(out var img))
                    img.sprite = quest.Icon;
            }

            // Detail text — show description or "Completed!" or placeholder text
            var detail = itemGO.transform.Find("UnlockableDetail");
            if (detail != null)
            {
                var txt = detail.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    if (quest.IsPlaceholder)
                        txt.text = "Coming Soon";
                    else if (isQuestClaimed)
                        txt.text = "Completed";
                    else
                        txt.text = quest.Description;
                }
            }

            // Locked / Unlocked state
            bool showAsUnlocked = isUnlocked && (isQuestCompleted || isQuestClaimed);

            var lockedIcon = itemGO.transform.Find("LockedIcon");
            if (lockedIcon != null)
                lockedIcon.gameObject.SetActive(!showAsUnlocked && !isUnlocked);

            var unlockedObj = itemGO.transform.Find("UnlockedObject");
            if (unlockedObj != null)
                unlockedObj.gameObject.SetActive(showAsUnlocked);

            // Claim button — visible only when quest is completed but not yet claimed
            var claimButton = itemGO.transform.Find("ClaimButton");
            if (claimButton != null)
            {
                bool showClaim = isQuestCompleted && !isQuestClaimed;
                claimButton.gameObject.SetActive(showClaim);

                if (showClaim && claimButton.TryGetComponent<Button>(out var btn))
                {
                    var capturedMode = quest.GameMode;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnClaimPressed(capturedMode));
                }
            }
        }

        void OnClaimPressed(GameModes completedMode)
        {
            if (GameModeProgressionService.Instance == null) return;

            GameModeProgressionService.Instance.ClaimQuestAndUnlockNext(completedMode);

            // The OnProgressionChanged callback will refresh the UI and animate slider
        }

        void RefreshQuestStates()
        {
            if (questList == null) return;

            var service = GameModeProgressionService.Instance;

            for (int i = 0; i < _spawnedItems.Count && i < questList.Quests.Count; i++)
            {
                var quest = questList.Quests[i];
                bool isUnlocked = i == 0 || (service != null && service.IsGameModeUnlocked(quest.GameMode));
                bool isQuestCompleted = service != null && service.IsQuestCompleted(quest.GameMode);

                bool isQuestClaimed = false;
                if (i + 1 < questList.Quests.Count && service != null)
                    isQuestClaimed = service.IsGameModeUnlocked(questList.Quests[i + 1].GameMode);

                var itemGO = _spawnedItems[i];
                bool showAsUnlocked = isUnlocked && (isQuestCompleted || isQuestClaimed);

                var lockedIcon = itemGO.transform.Find("LockedIcon");
                if (lockedIcon != null)
                    lockedIcon.gameObject.SetActive(!showAsUnlocked && !isUnlocked);

                var unlockedObj = itemGO.transform.Find("UnlockedObject");
                if (unlockedObj != null)
                    unlockedObj.gameObject.SetActive(showAsUnlocked);

                var claimButton = itemGO.transform.Find("ClaimButton");
                if (claimButton != null)
                {
                    bool showClaim = isQuestCompleted && !isQuestClaimed;
                    claimButton.gameObject.SetActive(showClaim);

                    if (showClaim && claimButton.TryGetComponent<Button>(out var btn))
                    {
                        var capturedMode = quest.GameMode;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OnClaimPressed(capturedMode));
                    }
                }

                // Update detail text
                var detail = itemGO.transform.Find("UnlockableDetail");
                if (detail != null)
                {
                    var txt = detail.GetComponentInChildren<TMP_Text>();
                    if (txt != null)
                    {
                        if (quest.IsPlaceholder)
                            txt.text = "Coming Soon";
                        else if (isQuestClaimed)
                            txt.text = "Completed";
                        else
                            txt.text = quest.Description;
                    }
                }
            }

            UpdateStatusText();
        }

        void SetSliderImmediate()
        {
            UpdateStatusText();
            if (progressSlider != null)
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

        void UpdateStatusText()
        {
            if (statusText == null) return;

            var service = GameModeProgressionService.Instance;
            if (service == null || questList == null)
            {
                statusText.text = "";
                return;
            }

            int completed = service.GetCompletedQuestCount();
            int total = questList.Quests.Count;
            statusText.text = $"{completed} / {total} Quests";
        }

        float GetNormalizedProgress()
        {
            if (questList == null || questList.Quests.Count == 0) return 0f;

            var service = GameModeProgressionService.Instance;
            if (service == null) return 0f;

            int completed = service.GetCompletedQuestCount();
            return Mathf.Clamp01((float)completed / questList.Quests.Count);
        }

        void ClearSpawned()
        {
            foreach (var go in _spawnedItems)
                if (go != null) Destroy(go);
            _spawnedItems.Clear();

            foreach (var go in _spawnedLabels)
                if (go != null) Destroy(go);
            _spawnedLabels.Clear();
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

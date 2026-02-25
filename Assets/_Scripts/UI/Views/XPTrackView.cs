using System.Collections.Generic;
using CosmicShore.App.Profile;
using CosmicShore.Models;
using DG.Tweening;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class XPTrackView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SO_XPTrackData xpTrackData;

        [Header("Prefabs")]
        [SerializeField] private GameObject xpItemPrefab;
        [SerializeField] private GameObject xpLevelPrefab;

        [Header("Containers")]
        [SerializeField] private Transform xpItemPanels;
        [SerializeField] private Transform xpLevelDisplayPanel;

        [Header("Slider")]
        [SerializeField] private Slider xpSlider;

        [Header("XP Label")]
        [SerializeField] private TMP_Text xpText;

        [Header("Animation")]
        [SerializeField] private float sliderAnimDuration = 1f;
        [SerializeField] private Ease sliderEase = Ease.OutCubic;

        [Inject] private PlayerDataService playerDataService;

        private readonly List<GameObject> _spawnedItems = new();
        private readonly List<GameObject> _spawnedLevels = new();
        private Tween _sliderTween;

        void OnEnable()
        {
            LoadTrack();

            if (playerDataService != null)
                playerDataService.OnProfileChanged += OnProfileChanged;
        }

        void OnDisable()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnProfileChanged;

            KillTween();
        }

        void OnProfileChanged(PlayerProfileData data)
        {
            UpdateXPDisplay(data.xp);
        }

        public void LoadTrack()
        {
            SpawnMilestones();
            int currentXP = playerDataService != null ? playerDataService.GetXP() : 0;
            SetXPImmediate(currentXP);
        }

        void SpawnMilestones()
        {
            ClearSpawned();

            if (xpTrackData == null) return;

            int totalMilestones = xpTrackData.milestones.Count;
            int xpPerMilestone = xpTrackData.xpPerMilestone;
            bool hasProfile = playerDataService != null &&
                              playerDataService.CurrentProfile != null;
            int currentXP = hasProfile ? playerDataService.GetXP() : 0;

            for (int i = 0; i < totalMilestones; i++)
            {
                int milestoneXP = (i + 1) * xpPerMilestone;
                var milestone = xpTrackData.milestones[i];
                bool isUnlocked = currentXP >= milestoneXP;

                // Spawn XP item (reward display)
                if (xpItemPrefab != null && xpItemPanels != null)
                {
                    var itemGO = Instantiate(xpItemPrefab, xpItemPanels);
                    _spawnedItems.Add(itemGO);
                    SetupXPItem(itemGO, milestone, isUnlocked);
                }

                // Spawn level label
                if (xpLevelPrefab != null && xpLevelDisplayPanel != null)
                {
                    var levelGO = Instantiate(xpLevelPrefab, xpLevelDisplayPanel);
                    _spawnedLevels.Add(levelGO);
                    var label = levelGO.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                        label.text = milestoneXP.ToString();
                }
            }

            // Force layout rebuild so content size fitter recalculates
            if (xpItemPanels != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(xpItemPanels as RectTransform);
            if (xpLevelDisplayPanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(xpLevelDisplayPanel as RectTransform);
        }

        void SetupXPItem(GameObject itemGO, XPMilestone milestone, bool isUnlocked)
        {
            // Icon
            var icon = itemGO.transform.Find("UnlockableIconBG/Icon");
            if (icon != null && milestone.reward != null && milestone.reward.icon != null)
            {
                var img = icon.GetComponent<Image>();
                if (img != null)
                    img.sprite = milestone.reward.icon;
            }

            // Detail text
            var detail = itemGO.transform.Find("UnlockableDetail");
            if (detail != null)
            {
                var txt = detail.GetComponentInChildren<TMP_Text>();
                if (txt != null && milestone.reward != null)
                    txt.text = milestone.reward.unlockDescription;
            }

            // Locked / Unlocked state
            var lockedIcon = itemGO.transform.Find("LockedIcon");
            if (lockedIcon != null)
                lockedIcon.gameObject.SetActive(!isUnlocked);

            var unlockedObj = itemGO.transform.Find("UnlockedObject");
            if (unlockedObj != null)
                unlockedObj.gameObject.SetActive(isUnlocked);
        }

        void SetXPImmediate(int xp)
        {
            UpdateXPText(xp);
            if (xpSlider != null)
                xpSlider.value = GetNormalized(xp);
        }

        void UpdateXPDisplay(int xp)
        {
            UpdateXPText(xp);
            AnimateSlider(xp);
            RefreshUnlockStates(xp);
        }

        void UpdateXPText(int xp)
        {
            if (xpText != null)
                xpText.text = $"{xp} XP";
        }

        void AnimateSlider(int xp)
        {
            if (xpSlider == null) return;

            KillTween();
            float target = GetNormalized(xp);
            _sliderTween = xpSlider.DOValue(target, sliderAnimDuration)
                .SetEase(sliderEase)
                .SetUpdate(true);
        }

        void RefreshUnlockStates(int currentXP)
        {
            if (xpTrackData == null) return;

            int xpPerMilestone = xpTrackData.xpPerMilestone;
            for (int i = 0; i < _spawnedItems.Count && i < xpTrackData.milestones.Count; i++)
            {
                int milestoneXP = (i + 1) * xpPerMilestone;
                bool isUnlocked = currentXP >= milestoneXP;

                var itemGO = _spawnedItems[i];
                var lockedIcon = itemGO.transform.Find("LockedIcon");
                if (lockedIcon != null)
                    lockedIcon.gameObject.SetActive(!isUnlocked);

                var unlockedObj = itemGO.transform.Find("UnlockedObject");
                if (unlockedObj != null)
                    unlockedObj.gameObject.SetActive(isUnlocked);
            }
        }

        float GetNormalized(int xp)
        {
            if (xpTrackData == null) return 0f;

            int totalXP = xpTrackData.milestones.Count * xpTrackData.xpPerMilestone;
            if (totalXP <= 0) return 0f;
            return Mathf.Clamp01((float)xp / totalXP);
        }

        void ClearSpawned()
        {
            foreach (var go in _spawnedItems)
                if (go != null) Destroy(go);
            _spawnedItems.Clear();

            foreach (var go in _spawnedLevels)
                if (go != null) Destroy(go);
            _spawnedLevels.Clear();
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

using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class GameEventFeed : MonoBehaviour
    {
        [Header("Channel")]
        [SerializeField] private ScriptableEventGameFeedPayload feedChannel;

        [Header("Config")]
        [SerializeField] private GameFeedSettingsSO settings;

        [Header("Prefab")]
        [SerializeField] private GameFeedEntry entryPrefab;

        [Header("Hierarchy (auto-created if null)")]
        [SerializeField] private RectTransform contentContainer;

        [Header("Data")]
        [SerializeField] private GameDataSO gameData;

        [Header("Domain Styling")]
        [SerializeField] private List<DomainColorDef> domainColors;

        private void Awake()
        {
            // Disable the Image component for transparent background
            var image = GetComponent<Image>();
            if (image != null)
                image.enabled = false;

            // Build scroll structure if not already wired
            if (contentContainer == null)
                BuildScrollStructure();

            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            if (feedChannel != null)
                feedChannel.OnRaised += OnFeedEvent;

            if (gameData != null)
                gameData.OnPlayerAdded += OnPlayerAdded;
        }

        private void OnDisable()
        {
            if (feedChannel != null)
                feedChannel.OnRaised -= OnFeedEvent;

            if (gameData != null)
                gameData.OnPlayerAdded -= OnPlayerAdded;
        }

        private void BuildScrollStructure()
        {
            var rt = GetComponent<RectTransform>();

            // Viewport
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;

            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = new Color(1, 1, 1, 0.003f); // Nearly invisible but required for Mask
            var mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            contentContainer = contentGo.GetComponent<RectTransform>();

            // Anchor content to bottom-right, grow upward
            contentContainer.anchorMin = new Vector2(0f, 0f);
            contentContainer.anchorMax = new Vector2(1f, 0f);
            contentContainer.pivot = new Vector2(1f, 0f);
            contentContainer.offsetMin = Vector2.zero;
            contentContainer.offsetMax = new Vector2(0f, 0f);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerRight;
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // ScrollRect on this object
            var scrollRect = gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentContainer;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 0f; // No manual scrolling — feed is automatic
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void OnPlayerAdded(string playerName, Domains domain)
        {
            var message = $"<b>{playerName}</b> joined";
            var color = GetColorForDomain(domain);
            SpawnEntry(message, color, false);
        }

        private void OnFeedEvent(GameFeedPayload payload)
        {
            bool isRichText = payload.Type == GameFeedType.JoustHit;
            var color = isRichText ? Color.white : GetColorForDomain(payload.Domain);

            if (payload.Type == GameFeedType.PlayerDisconnected)
            {
                color = GetColorForDomain(payload.Domain);
                color.a = 0.7f;
                SpawnEntry(payload.Message, color, false);
            }
            else if (isRichText)
            {
                // Joust — message already has rich text color tags
                SpawnEntry(payload.Message, color, true);
            }
            else
            {
                SpawnEntry(payload.Message, color, false);
            }
        }

        private void SpawnEntry(string message, Color color, bool isRichText)
        {
            if (contentContainer == null || settings == null)
                return;

            // Enforce max visible entries — destroy oldest
            while (contentContainer.childCount >= settings.maxVisibleEntries)
            {
                var oldest = contentContainer.GetChild(0).gameObject;
                Destroy(oldest);
            }

            GameFeedEntry entry;
            if (entryPrefab != null)
            {
                entry = Instantiate(entryPrefab, contentContainer);
            }
            else
            {
                entry = GameFeedEntry.CreateEntry(contentContainer);
            }

            entry.Setup(message, color, settings, isRichText);

            // Force layout rebuild so VerticalLayoutGroup positions correctly
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        }

        public void ClearFeed()
        {
            if (contentContainer == null) return;

            for (int i = contentContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(contentContainer.GetChild(i).gameObject);
            }
        }

        public Color GetColorForDomain(Domains domain)
        {
            var def = domainColors.FirstOrDefault(d => d.Domain == domain);
            return def.Equals(default(DomainColorDef)) ? Color.white : def.Color;
        }

        [Serializable]
        public struct DomainColorDef
        {
            public Domains Domain;
            public Color Color;
        }
    }
}

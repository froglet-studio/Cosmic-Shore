using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Spawns toast lines into an editor-authored scroll view. Nothing is generated from
    /// code: the ScrollRect, viewport, content (with VerticalLayoutGroup + ContentSizeFitter,
    /// bottom pivot) and the item prefab are all authored in the toast panel prefab and wired
    /// here. New lines appear at the bottom; older lines scroll UP and stay readable instead
    /// of disappearing (they only dim with age). Oldest lines beyond the retention cap are
    /// removed from the top.
    /// </summary>
    public class GameToastView : MonoBehaviour
    {
        [Header("References (wire on the prefab)")]
        [SerializeField] private GameToastSettingsSO settings;
        [SerializeField] private GameToastItemView itemPrefab;

        [Tooltip("The scroll view around the feed. Optional but recommended - enables " +
                 "scroll-back through history and auto-stick to the newest line.")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("The scroll view's Content (VerticalLayoutGroup + ContentSizeFitter, " +
                 "pivot at the bottom). Toast items are instantiated here.")]
        [SerializeField] private RectTransform contentContainer;

        public void Spawn(string message, Color textColor, Color accentColor, float baseAlpha)
        {
            if (settings == null || itemPrefab == null || contentContainer == null)
            {
                Debug.LogError("[GameToastView] Missing references - wire settings, itemPrefab " +
                               "and contentContainer on the toast panel prefab.", this);
                return;
            }

            // Stick to the newest line only when the player was already reading the bottom.
            bool wasAtBottom = scrollRect == null ||
                               scrollRect.verticalNormalizedPosition <= settings.stickToBottomThreshold;

            // Retention cap: remove oldest from the top. Detach before Destroy so childCount
            // drops immediately (multiple spawns in one frame must not loop forever).
            while (contentContainer.childCount >= settings.maxRetainedEntries)
            {
                var oldest = contentContainer.GetChild(0);
                oldest.SetParent(null);
                Destroy(oldest.gameObject);
            }

            var item = Instantiate(itemPrefab, contentContainer);
            item.transform.SetAsLastSibling();
            item.Setup(message, textColor, accentColor, baseAlpha);

            // Rebuild BEFORE animating so the item's rest position comes from the layout group.
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
            item.AnimateIn(settings);

            if (settings.autoScrollToBottom && wasAtBottom && scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        public void Clear()
        {
            if (contentContainer == null) return;

            for (int i = contentContainer.childCount - 1; i >= 0; i--)
            {
                var child = contentContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.UI
{
    /// <summary>
    /// A vertically looping scroll window.
    /// Items in the scroll window are duplicated above and below the original items. 
    /// The window's scroll position is reset by subtracting or adding the original window height.
    /// </summary>
    public class InfiniteScroll : MonoBehaviour
    {
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform viewPortTransform;
        [SerializeField] RectTransform contentPanelTransform;
        [SerializeField] VerticalLayoutGroup verticalLayoutGroup;
        [FormerlySerializedAs("ContentContainer")]
        [SerializeField] GameObject contentContainer;
        [SerializeField] float minVelocity = 5f;
        [SerializeField] float enableSnapVelocity = 15f;
        [SerializeField] int scrollThreshold = 3;

        List<GameObject> itemList = new();
        Vector2 previousVelocity;
        bool hasUpdatedPosition;
        bool isInitialized = false;
        bool checkForSnap = false;
        float itemHeightWithSpacing;

        public void Initialize(bool forceReInit = false)
        {
            if (isInitialized && !forceReInit) return;

            itemList.Clear();
            hasUpdatedPosition = false;
            previousVelocity = Vector2.zero;

            // Collect active child items from the content container
            foreach (Transform child in contentContainer.transform)
            {
                if (child.gameObject.activeInHierarchy)
                    itemList.Add(child.gameObject);
            }

            int itemCount = itemList.Count;
            if (itemCount == 0) return;

            if (itemCount <= scrollThreshold)
            {
                // Disable the scroll functionality if there are not enough items
                scrollRect.vertical = false;
                isInitialized = true;
                return;
            }


            // Cache the item height and spacing for performance
            itemHeightWithSpacing = itemList[0].GetComponent<RectTransform>().rect.height + verticalLayoutGroup.spacing;

            // Duplicate items for scrolling effect (above and below)
            DuplicateItems(itemCount);


            // Set the initial content position for scrolling
            contentPanelTransform.localPosition = new Vector3(
                contentPanelTransform.localPosition.x,
                (itemHeightWithSpacing * itemCount),
                contentPanelTransform.localPosition.z);

            isInitialized = true;
        }

        void Update()
        {
            if (!isInitialized) return;

            HandleScrollVelocity();
            CheckForSnap();
            LoopContent();
        }

        private void HandleScrollVelocity()
        {
            if (hasUpdatedPosition)
            {
                hasUpdatedPosition = false;
                scrollRect.velocity = previousVelocity;
            }

            if (scrollRect.velocity.y > enableSnapVelocity)
            {
                checkForSnap = true;
            }
        }

        private void CheckForSnap()
        {
            if (checkForSnap && scrollRect.velocity.y < minVelocity)
            {
                scrollRect.velocity = Vector2.zero;

                // Snap to the nearest position
                float snapPosition = Mathf.Round(contentPanelTransform.localPosition.y / itemHeightWithSpacing) * itemHeightWithSpacing;
                contentPanelTransform.localPosition = new Vector3(
                    contentPanelTransform.localPosition.x,
                    snapPosition,
                    contentPanelTransform.localPosition.z);

                Canvas.ForceUpdateCanvases();
                checkForSnap = false;
            }
        }

        private void LoopContent()
        {
            if (contentPanelTransform.localPosition.y < (itemList.Count * itemHeightWithSpacing))
            {
                previousVelocity = scrollRect.velocity;
                contentPanelTransform.localPosition += new Vector3(0, itemList.Count * itemHeightWithSpacing, 0);
                hasUpdatedPosition = true;
            }

            if (contentPanelTransform.localPosition.y > 2 * (itemList.Count * itemHeightWithSpacing))
            {
                previousVelocity = scrollRect.velocity;
                contentPanelTransform.localPosition -= new Vector3(0, itemList.Count * itemHeightWithSpacing, 0);
                hasUpdatedPosition = true;
            }
        }

        private void DuplicateItems(int itemCount)
        {
            // Create copies of the original items below the list
            for (int i = 0; i < itemCount; i++)
            {
                GameObject instance = Instantiate(itemList[i % itemCount], contentPanelTransform);
                instance.name = instance.name.Replace("(Clone)", "-pre");
                instance.GetComponent<RectTransform>().SetAsLastSibling();
            }

            // Create copies of the original items above the list
            for (int i = 0; i < itemCount; i++)
            {
                int index = itemCount - i - 1;
                while (index < 0)
                    index += itemCount;

                GameObject instance = Instantiate(itemList[index], contentPanelTransform);
                instance.name = instance.name.Replace("(Clone)", "-post");
                instance.GetComponent<RectTransform>().SetAsFirstSibling();
            }
        }
    }
}
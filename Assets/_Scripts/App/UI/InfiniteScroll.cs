using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI
{
    /// <summary>
    /// A vertically looping scroll window.
    /// Items in the scroll window are duplicated above and below the original items. 
    /// The windows scroll position is reset by subtracting out or adding in the original window height.
    /// </summary>
    public class InfiniteScroll : MonoBehaviour
    {
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform viewPortTransform;
        [SerializeField] RectTransform contentPanelTransform;
        [SerializeField] VerticalLayoutGroup verticalLayoutGroup;
        [SerializeField] GameObject ContentContainer;
        [SerializeField] float minVelocity = 5f;
        [SerializeField] float enableSnapVelocity = 15f;

        List<RectTransform> itemList = new();
        Vector2 OldVelocity;
        bool isUpdated;
        bool isInitialized = false;
        bool checkForSnap = false;

        public void Initialize(bool forceReInit=false)
        {
            if (isInitialized && !forceReInit) return;

            itemList.Clear();
            isUpdated = false;
            OldVelocity = Vector2.zero;

            foreach (Transform transform in ContentContainer.transform)
            {
                if (transform.gameObject.activeInHierarchy)
                    itemList.Add(transform.GetComponent<RectTransform>());
            }
            
            int itemCount = itemList.Count;

            for (int i=0; i < itemCount; i++)
            {
                RectTransform rt = Instantiate(itemList[i % itemCount ], contentPanelTransform);
                rt.name = rt.name.Replace("(Clone)", "-pre");
                rt.SetAsLastSibling();
            }

            for (int i = 0; i < itemCount; i++)
            {
                int index = itemCount - i - 1;
                while (index < 0)
                    index += itemCount ;

                RectTransform rt = Instantiate(itemList[index], contentPanelTransform);
                rt.name = rt.name.Replace("(Clone)", "-post");
                rt.SetAsFirstSibling();
            }

            Debug.Log($"InfiniteScroll - loop offset distance: { itemCount * (itemList[0].rect.height + verticalLayoutGroup.spacing) }");
            
            contentPanelTransform.localPosition = new Vector3(
                contentPanelTransform.localPosition.x,
                0 - (itemList[0].rect.height + verticalLayoutGroup.spacing) * itemCount,
                contentPanelTransform.localPosition.z);

            isInitialized = true;
        }

        void Update()
        {
            if (!isInitialized)
                return;

            if (isUpdated )
            {
                isUpdated = false;
                scrollRect.velocity = OldVelocity;
            }

            // only snap after we have some momentum
            if (scrollRect.velocity.y > enableSnapVelocity)
            {
                checkForSnap = true;
            }

            if (checkForSnap)
            {
                if (scrollRect.velocity.y < minVelocity)
                {
                    scrollRect.velocity = Vector2.zero;
                    float snapPosition = Mathf.Round(contentPanelTransform.localPosition.y / (itemList[0].rect.height + verticalLayoutGroup.spacing))
                                            * (itemList[0].rect.height + verticalLayoutGroup.spacing);
                    contentPanelTransform.localPosition = new Vector3
                    (
                        contentPanelTransform.localPosition.x,
                        snapPosition,
                        contentPanelTransform.localPosition.z
                    );
                    Canvas.ForceUpdateCanvases();
                
                    checkForSnap = false;
                }
            }
            
            if (contentPanelTransform.localPosition.y < itemList.Count * (itemList[0].rect.height + verticalLayoutGroup.spacing))
            {
                Debug.Log($"InfiniteScroll - contentPanelTransform.localPosition.y > 0: {contentPanelTransform.localPosition.y}");
                Canvas.ForceUpdateCanvases();
                OldVelocity = scrollRect.velocity;
                contentPanelTransform.localPosition += new Vector3(
                    0,
                    itemList.Count * (itemList[0].rect.height + verticalLayoutGroup.spacing),
                    0
                );
                isUpdated = true;
            }
            

            if (contentPanelTransform.localPosition.y > 2 * (itemList.Count * (itemList[0].rect.height + verticalLayoutGroup.spacing)))
            {
                Debug.Log($"InfiniteScroll - contentPanelTransform.localPosition.y < 0: {contentPanelTransform.localPosition.y}");
                Canvas.ForceUpdateCanvases();
                OldVelocity = scrollRect.velocity;
                contentPanelTransform.localPosition -= new Vector3(
                    0,
                    itemList.Count * (itemList[0].rect.height + verticalLayoutGroup.spacing),
                    0
                );
                isUpdated = true;
            }
        }
    }
}
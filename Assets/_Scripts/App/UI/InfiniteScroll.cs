using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class InfiniteScroll : MonoBehaviour
    {
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform viewPortTransform;
        [SerializeField] RectTransform contentPanelTransform;
        [SerializeField] VerticalLayoutGroup vlg;

        [SerializeField] RectTransform[] itemList;
        [SerializeField] float SelectedItemHeight;
        [SerializeField] float minVelocity = 5f;
        [SerializeField] float enableSnapVelocity = 15f;


        Vector2 OldVelocity;
        bool isUpdated;
        bool checkForSnap = false;

        void Start()
        {
            isUpdated = false;
            OldVelocity = Vector2.zero;

            int itemsToAdd = itemList.Length; // Mathf.CeilToInt(viewPortTransform.rect.height / itemList[0].rect.height + vlg.spacing);

            for (int i=0; i < itemsToAdd; i++)
            {
                RectTransform rt = Instantiate(itemList[i % itemList.Length], contentPanelTransform);
                rt.name = rt.name.Replace("(Clone)", "");
                rt.SetAsLastSibling();
            }

            for (int i = 0; i < itemsToAdd; i++)
            {
                int index = itemList.Length - i - 1;
                while (index < 0)
                    index += itemList.Length;

                RectTransform rt = Instantiate(itemList[index], contentPanelTransform);
                rt.name = rt.name.Replace("(Clone)", "");
                rt.SetAsFirstSibling();
            }

            Debug.Log($"InfiniteScroll - loop offset distance: {itemList.Length * (itemList[0].rect.height + vlg.spacing)}");
            
            contentPanelTransform.localPosition = new Vector3(
                contentPanelTransform.localPosition.x,
                0 - (itemList[0].rect.height + vlg.spacing) * itemsToAdd,
                contentPanelTransform.localPosition.z);
        }

        void Update()
        {
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
                    float snapPosition = Mathf.Round(contentPanelTransform.localPosition.y / (itemList[0].rect.height + vlg.spacing))
                                            * (itemList[0].rect.height + vlg.spacing);
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
            
            if (contentPanelTransform.localPosition.y < itemList.Length * (itemList[0].rect.height + vlg.spacing))
            {
                Debug.Log($"InfiniteScroll - contentPanelTransform.localPosition.y > 0: {contentPanelTransform.localPosition.y}");
                Canvas.ForceUpdateCanvases();
                OldVelocity = scrollRect.velocity;
                contentPanelTransform.localPosition += new Vector3(
                    0,
                    itemList.Length * (itemList[0].rect.height + vlg.spacing),
                    0
                );
                isUpdated = true;
            }
            

            if (contentPanelTransform.localPosition.y > 2 * (itemList.Length * (itemList[0].rect.height + vlg.spacing)))
            {
                Debug.Log($"InfiniteScroll - contentPanelTransform.localPosition.y < 0: {contentPanelTransform.localPosition.y}");
                Canvas.ForceUpdateCanvases();
                OldVelocity = scrollRect.velocity;
                contentPanelTransform.localPosition -= new Vector3(
                    0,
                    itemList.Length * (itemList[0].rect.height + vlg.spacing),
                    0
                );
                isUpdated = true;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.App.UI
{
    public class TooltipHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject tooltip;
        bool isTooltipVisible = false;

        void Start()
        {
            if (tooltip != null)
            {
                tooltip.SetActive(false);
            }
        }

        void Update()
        {
            // Detect clicks outside the button and tooltip
            if (isTooltipVisible && Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)tooltip.transform, Input.mousePosition, Camera.main))
            {
                isTooltipVisible = false;
                tooltip.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Check if the click is on the button itself
            if (EventSystem.current.currentSelectedGameObject == gameObject)
            {
                // Toggle tooltip visibility
                isTooltipVisible = !isTooltipVisible;
                tooltip.SetActive(isTooltipVisible);
            }
            else
            {
                // Hide tooltip if click is outside
                isTooltipVisible = false;
                tooltip.SetActive(false);
            }
        }
    }
}
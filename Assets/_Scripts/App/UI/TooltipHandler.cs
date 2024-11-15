using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.App.UI
{
    public class TooltipHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject tooltip;
        bool isTooltipVisible = false;
        Camera mainCamera;
        InputAction pointerClickAction;

        void OnEnable()
        {
            pointerClickAction.Enable();
        }

        void OnDisable()
        {
            pointerClickAction.Disable();
        }

        void Awake()
        {
            mainCamera = Camera.main;

            // Set up an InputAction for detecting pointer clicks (both mouse and touch)
            pointerClickAction = new InputAction(type: InputActionType.PassThrough, binding: "<Pointer>/press");
            pointerClickAction.performed += OnGlobalPointerClick; // Subscribe to pointer click event
            if (tooltip != null)
                tooltip.SetActive(false);
        }


        /// <summary>
        /// Detects global pointer clicks (outside the button and tooltip area)
        /// </summary>
        void OnGlobalPointerClick(InputAction.CallbackContext context)
        {
            if (isTooltipVisible)
            {
                Vector2 pointerPosition = Pointer.current.position.ReadValue();  // Read pointer position (works for mouse or touch)

                // Check if the click/touch was outside the tooltip
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)tooltip.transform, pointerPosition, mainCamera))
                {
                    isTooltipVisible = false;
                    tooltip.SetActive(false);
                }
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
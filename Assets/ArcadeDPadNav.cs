using CosmicShore.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    public class ArcadeDPadNav : MonoBehaviour
    {
        [SerializeField] private ScreenSwitcher ScreenSwitcher;
        [SerializeField] private ScrollRect scrollRect;
        private List<List<Button>> buttonGrid = new List<List<Button>>();
        private int currentRow = 0;
        private int currentCol = 0;
        private Button selectedButton;
        private bool initialized;

        void Start()
        {
            if (Gamepad.current != null)
            {
                InitializeNavigation();
            }
        }

        void Update()
        {
            if (Gamepad.current == null)
                return;

            // Only process input when the parent GameObject is active and interactable
            if (!gameObject.activeInHierarchy)
                return;

            if (!initialized)
            {
                InitializeNavigation();
            }

            if (Gamepad.current.dpad.up.wasPressedThisFrame) NavigateUp();
            if (Gamepad.current.dpad.down.wasPressedThisFrame) NavigateDown();
            if (Gamepad.current.dpad.left.wasPressedThisFrame) NavigateLeft();
            if (Gamepad.current.dpad.right.wasPressedThisFrame) NavigateRight();

            if (Gamepad.current.buttonSouth.wasPressedThisFrame && selectedButton != null)
            {
                selectedButton.onClick.Invoke();
            }
        }

        public void AddRow(List<Button> row)
        {
            buttonGrid.Add(row);
        }

        public void AddButtonToRow(Button button, int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < buttonGrid.Count)
            {
                buttonGrid[rowIndex].Add(button);
            }
            else
            {
                Debug.LogError("Row index out of bounds.");
            }
        }

        public void ResetNavigation()
        {
            buttonGrid.Clear();
            currentRow = 0;
            currentCol = 0;
            selectedButton = null;
            initialized = false;
        }

        void InitializeNavigation()
        {
            initialized = true;

            if (buttonGrid.Count > 0 && buttonGrid[0].Count > 0)
            {
                HighlightButton(buttonGrid[0][0]);
            }
        }

        void NavigateUp()
        {
            if (currentRow > 0)
            {
                currentRow--;
                currentCol = Mathf.Clamp(currentCol, 0, buttonGrid[currentRow].Count - 1);
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
        }

        void NavigateDown()
        {
            if (currentRow < buttonGrid.Count - 1)
            {
                currentRow++;
                currentCol = Mathf.Clamp(currentCol, 0, buttonGrid[currentRow].Count - 1);
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
        }

        void NavigateLeft()
        {
            if (currentCol > 0)
            {
                currentCol--;
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
        }

        void NavigateRight()
        {
            if (currentCol < buttonGrid[currentRow].Count - 1)
            {
                currentCol++;
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
        }

        void HighlightButton(Button button)
        {
            if (button == null) return;
            button.Select();
            selectedButton = button;
        }

        // Invocation commented out since it is currently not working as expected
        void ScrollToButton(Button button)
        {
            if (scrollRect == null) return;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            RectTransform viewportRect = scrollRect.viewport;

            if (!RectTransformUtility.RectangleContainsScreenPoint(viewportRect, buttonRect.position, Camera.main))
            {
                Vector3 viewportLocalPosition = viewportRect.InverseTransformPoint(buttonRect.position);
                Vector3 contentLocalPosition = scrollRect.content.localPosition;
                Vector3 offset = viewportRect.localPosition - viewportLocalPosition;

                scrollRect.content.localPosition = new Vector3(contentLocalPosition.x, contentLocalPosition.y - offset.y, contentLocalPosition.z);
            }
        }
    }
}

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
        private List<List<Button>> buttonGrid = new List<List<Button>>(); // Dynamic 2D list for buttons
        private int currentRow = 0;
        private int currentCol = 0;
        private Button selectedButton;
        private bool initialized;

        void Start()
        {
            if (Gamepad.current != null /*&& ScreenSwitcher.ScreenIsActive(ScreenSwitcher.MenuScreens.ARCADE)*/)
            {
                InitializeNavigation();
            }
        }

        void Update()
        {
            if (!initialized)
            {
                if (Gamepad.current != null)
                {
                    InitializeNavigation();
                }
            }

            // A modal (e.g. the game configure modal) owns the gamepad while it is open.
            // Without this gate, dpad presses inside the modal also walked the game grid
            // underneath (Select() stealing the modal's UI selection), and buttonSouth
            // invoked the grid's hidden selected card - silently re-targeting the modal
            // to the neighboring game (Maelstrom sits next to Rampage alphabetically),
            // so "Start" launched the wrong game. Mirrors ScreenSwitcher's own
            // HasActiveModal gate on trigger navigation.
            if (ScreenSwitcher != null && ScreenSwitcher.HasActiveModal) return;

            if (buttonGrid.Count == 0) return;

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame) NavigateUp();
                if (Gamepad.current.dpad.down.wasPressedThisFrame) NavigateDown();
                if (Gamepad.current.dpad.left.wasPressedThisFrame) NavigateLeft();
                if (Gamepad.current.dpad.right.wasPressedThisFrame) NavigateRight();
            }

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                // Guard against a cleared/stale selection (grid rebuilds on every
                // repopulate) and against firing a card that is inactive or locked.
                if (selectedButton != null && selectedButton.isActiveAndEnabled && selectedButton.interactable)
                    selectedButton.onClick.Invoke();
            }
        }

        public void AddRow(List<Button> row)
        {
            buttonGrid.Add(row);
        }

        /// <summary>
        /// Clears the navigation grid. Call before re-adding rows on a repopulate -
        /// without this, every PopulateGameSelectionList() appended a duplicate copy of
        /// all rows, so dpad navigation walked stale duplicate entries and jumped
        /// unpredictably through the list.
        /// </summary>
        public void ResetGrid()
        {
            buttonGrid.Clear();
            currentRow = 0;
            currentCol = 0;
            selectedButton = null;
        }

        /// <summary>
        /// Re-highlights the first grid button after a repopulate, so the gamepad
        /// selection ring survives favorite toggles / progression refreshes. No-op
        /// before gamepad init or while a modal owns the input.
        /// </summary>
        public void RefreshSelection()
        {
            if (!initialized) return;
            if (ScreenSwitcher != null && ScreenSwitcher.HasActiveModal) return;
            if (buttonGrid.Count > 0 && buttonGrid[0].Count > 0)
                HighlightButton(buttonGrid[0][0]);
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

        void InitializeNavigation()
        {
            initialized = true;

            // Ensure there is at least one row and one button
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
            Debug.Log($"ArcadeDPad - Navigate Up: {currentRow},{currentCol}");
        }

        void NavigateDown()
        {
            if (currentRow < buttonGrid.Count - 1)
            {
                currentRow++;
                currentCol = Mathf.Clamp(currentCol, 0, buttonGrid[currentRow].Count - 1);
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
            Debug.Log($"ArcadeDPad - Navigate Down: {currentRow},{currentCol}");
        }

        void NavigateLeft()
        {
            if (currentCol > 0)
            {
                currentCol--;
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
            Debug.Log($"ArcadeDPad - Navigate Left: {currentRow},{currentCol}");
        }

        void NavigateRight()
        {
            if (currentCol < buttonGrid[currentRow].Count - 1)
            {
                currentCol++;
                HighlightButton(buttonGrid[currentRow][currentCol]);
            }
            Debug.Log($"ArcadeDPad - Navigate Right: {currentRow},{currentCol}");
        }

        void HighlightButton(Button button)
        {
            button.Select();
            selectedButton = button;
            //ScrollToButton(button);   // Not working correctly yet
        }

        // Invocation commented out since it is currently not working as expected
        void ScrollToButton(Button button)
        {
            if (scrollRect == null) return;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            RectTransform viewportRect = scrollRect.viewport;

            // Check if the button is outside the viewport
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewportRect, buttonRect.position, Camera.main))
            {
                // Calculate how much to scroll to bring the button into view
                Vector3 viewportLocalPosition = viewportRect.InverseTransformPoint(buttonRect.position);
                Vector3 contentLocalPosition = scrollRect.content.localPosition;
                Vector3 offset = viewportRect.localPosition - viewportLocalPosition;

                // Adjust content position to scroll
                scrollRect.content.localPosition = new Vector3(contentLocalPosition.x, contentLocalPosition.y - offset.y, contentLocalPosition.z);
            }
        }
    }
}
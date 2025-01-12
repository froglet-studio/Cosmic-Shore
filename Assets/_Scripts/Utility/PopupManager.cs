using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore.Utilities
{
    /// <summary>
    /// Handles the display of Popup messages. 
    /// Instantiates and reuses popup panel prefabs to allow displaying multiple messages in succession
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        private const float OFFSET = 30;
        private const float MAX_OFFSET = 200;

        [SerializeField]
        private GameObject _popupPanelPrefab;

        [SerializeField]
        private NotificationUI _notificationUI;

        private List<PopupPanel> _popupPanels = new List<PopupPanel>();

        private static PopupManager _instance;

        private void Awake()
        {
            if (_instance != null)
            {
                Debug.LogWarning("PopupManager Invalid State, instance already exists");
                return;
            }

            _instance = this;            // since DontdestroyOnLoad only works on root objects, we need to make sure the canvas is a root object
        }

        private void OnDestroy()
        {
            _instance = null;
        }

        /// <summary>
        /// Displays a popup panel message with the specified title and main text
        /// </summary>
        /// <param name="titleText">The title at the top of the panel</param>
        /// <param name="mainText">the text just under the title- the main body of text</param>
        /// <param name="closeableByUser">Whether or not user can close the panel with a close button.</param>
        /// <returns></returns>
        public static PopupPanel ShowPopupPanel(string titleText, string mainText, bool closeableByUser = true)
        {
            if (_instance != null)
            {
                return _instance.DisplayPopupPanel(titleText, mainText, closeableByUser);
            }

            Debug.LogError($"No popuppanel instance found. Cannot display message: {titleText}: {mainText}");
            return null;
        }

        /// <summary>
        /// Used to display a status message notification at the top of the screen for a short duration
        /// </summary>
        public static void DisplayStatus(string status, int duration)
        {
            _instance._notificationUI.DisplayStatus(status, duration);
        }

        private PopupPanel DisplayPopupPanel(string titleText, string mainText, bool closeableByUser)
        {
            PopupPanel panel = GetNextAvailablePopupPanel();

            if (panel != null)
            {
                panel.SetupPopupPanel(titleText, mainText, closeableByUser);
            }

            return panel;
        }

        private PopupPanel GetNextAvailablePopupPanel()
        {
            PopupPanel panel = _popupPanels.Find(p => !p.gameObject.activeSelf);

            if (panel == null)
            {
                panel = Instantiate(_popupPanelPrefab, transform).GetComponent<PopupPanel>();
                panel.gameObject.transform.position += new Vector3(1, -1) * (OFFSET * _popupPanels.Count % MAX_OFFSET);
                _popupPanels.Add(panel);
            }

            return panel;
        }
    }
}
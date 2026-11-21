using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A hint the player can close with its own × (rev 3: "Tap the cell to fly should have a cross button
    /// so players can remove it"). Closing hides it through its CanvasGroup and, when a key is set,
    /// remembers that on this device, so a closed hint stays closed on the next visit.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Dismissible Hint")]
    public class DismissibleHint : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The hint's group; closing hides it (KitVisibility).")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("The × that closes the hint.")]
        [SerializeField] private Button closeButton;

        [Header("Memory")]
        [Tooltip("PlayerPrefs key that remembers a close on this device. Empty = the hint returns on every visit.")]
        [SerializeField] private string rememberKey = "menu.hint.fly.closed";

        public bool IsClosed => !string.IsNullOrEmpty(rememberKey) && PlayerPrefs.GetInt(rememberKey, 0) == 1;

        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (IsClosed) KitVisibility.SetShown(group, false);
        }

        private void OnDestroy()
        {
            if (closeButton) closeButton.onClick.RemoveListener(Close);
        }

        public void Close()
        {
            KitVisibility.SetShown(group, false);
            if (string.IsNullOrEmpty(rememberKey)) return;
            PlayerPrefs.SetInt(rememberKey, 1);
            PlayerPrefs.Save();
        }
    }
}

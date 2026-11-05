using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A side panel that becomes a sheet on a phone (Docs/UI_STYLE_GUIDE.md section 8; Docs/MENU_REDESIGN_B.md section
    /// 129: the Arcade's party panel, Configure's controls, the Hangar's abilities). On PC the panel shows as authored and
    /// the open button is hidden. On a phone (its canvas's <see cref="AdaptiveCanvasScaler"/> at the phone layout) the
    /// panel starts closed; the open button shows it over the dim, and the close button or the dim closes it. The panel's
    /// own place on a phone (a bottom sheet) is its <see cref="PhoneRect"/>'s.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Phone Sheet")]
    public class PhoneSheet : MonoBehaviour
    {
        [Header("The panel")]
        [Tooltip("The panel that is a sheet on a phone. Its CanvasGroup shows and hides it.")]
        [SerializeField] private CanvasGroup panel;
        [Tooltip("Optional: a full-screen dim behind the sheet, shown with it (a Button on it closes the sheet).")]
        [SerializeField] private CanvasGroup dim;

        [Header("Phone-only controls")]
        [Tooltip("Opens the sheet. Shown only on a phone.")]
        [SerializeField] private Button openButton;
        [Tooltip("Optional: closes the sheet (inside the sheet). Shown only on a phone.")]
        [SerializeField] private Button closeButton;
        [Tooltip("Optional: the dim's Button, which also closes the sheet.")]
        [SerializeField] private Button dimButton;

        private AdaptiveCanvasScaler _scaler;
        private bool _phone;
        private bool _open;

        /// <summary>Whether the sheet is open on a phone (always true on PC, where the panel is not a sheet).</summary>
        public bool IsShown => !_phone || _open;

        private void Awake()
        {
            if (openButton) openButton.onClick.AddListener(Open);
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (dimButton) dimButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            _scaler = GetComponentInParent<AdaptiveCanvasScaler>(true);
            if (_scaler) _scaler.PhoneLayoutChanged += OnLayout;
            OnLayout(_scaler && _scaler.UsesPhoneLayout);
        }

        private void OnDisable()
        {
            if (_scaler) _scaler.PhoneLayoutChanged -= OnLayout;
        }

        private void OnDestroy()
        {
            if (openButton) openButton.onClick.RemoveListener(Open);
            if (closeButton) closeButton.onClick.RemoveListener(Close);
            if (dimButton) dimButton.onClick.RemoveListener(Close);
        }

        /// <summary>Switch between the PC panel and the phone sheet (closed).</summary>
        public void OnLayout(bool phone)
        {
            _phone = phone;
            _open = false;
            Refresh();
        }

        public void Open()
        {
            if (!_phone) return;
            _open = true;
            Refresh();
        }

        public void Close()
        {
            if (!_phone) return;
            _open = false;
            Refresh();
        }

        private void Refresh()
        {
            bool shown = IsShown;
            Show(panel, shown);
            Show(dim, _phone && _open);
            if (openButton) openButton.gameObject.SetActive(_phone);
            if (closeButton) closeButton.gameObject.SetActive(_phone);
        }

        private static void Show(CanvasGroup g, bool on)
        {
            if (!g) return;
            g.alpha = on ? 1f : 0f;
            g.interactable = on;
            g.blocksRaycasts = on;
        }
    }
}

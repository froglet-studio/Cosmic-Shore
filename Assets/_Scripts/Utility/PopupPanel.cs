using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace CosmicShore.Utilities
{
    /// <summary>
    /// Simple popup panel to display informations to players.
    /// </summary>
    public class PopupPanel : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _titleText;

        [SerializeField]
        private TextMeshProUGUI _mainText;

        [SerializeField]
        private Button _confirmButton;

        /*[SerializeField]
        private GameObject _loadingSpinner;*/

        [SerializeField]
        private CanvasGroup _canvasGroup;

        private bool _isDisplaying;
        public bool IsDisplaying => _isDisplaying;

        private bool _closableByUser;

        private void Awake()
        {
            Hide();
        }

        public void OnConfirmClick()
        {
            if (_closableByUser)
            {
                Hide();
            }
        }

        public virtual void Hide()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isDisplaying = false;
        }

        public void SetupPopupPanel(string titleText, string mainText, bool closeableByUser = true)
        {
            _titleText.text = titleText;
            _mainText.text = mainText;
            _closableByUser = closeableByUser;
            _confirmButton.gameObject.SetActive(closeableByUser);
            // _loadingSpinner.SetActive(!closeableByUser);
            Show();
        }

        protected virtual void Show()
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _isDisplaying = true;
        }
    }
}

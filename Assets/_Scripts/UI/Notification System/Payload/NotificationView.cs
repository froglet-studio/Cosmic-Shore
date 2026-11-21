using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    public sealed class NotificationView : MonoBehaviour
    {
        [Header("Bindings")]
        public RectTransform container;  // the moving rect (this or a child)
        public CanvasGroup   canvasGroup;
        [SerializeField] TMP_Text headerText;
        public TMP_Text HeaderText => headerText;
        [SerializeField] TMP_Text titleText;
        public TMP_Text TitleText => titleText;
        public CanvasFitHelper fitHelper;

        public void Bind(NotificationPayload p)
        {
            if (headerText) headerText.text = p.Header ?? string.Empty;
            if (titleText)  titleText.text  = p.Title  ?? string.Empty;
        }
    }
}
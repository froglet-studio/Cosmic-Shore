using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public sealed class NotificationView : MonoBehaviour
    {
        [Header("Bindings")]
        public RectTransform container;  // the moving rect (this or a child)
        public CanvasGroup   canvasGroup;
        public TMP_Text      headerText;
        public TMP_Text      titleText;
        public CanvasFitHelper fitHelper;

        public void Bind(NotificationPayload p)
        {
            if (headerText) headerText.text = p.Header ?? string.Empty;
            if (titleText)  titleText.text  = p.Title  ?? string.Empty;
        }
    }
}
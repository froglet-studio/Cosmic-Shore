using UnityEngine;

namespace CosmicShore.Game.IO
{

    [RequireComponent(typeof(RectTransform))]
    public class FlipUIScreen : MonoBehaviour
    {

        RectTransform rectTransform;
        
        void OnEnable()
        {
            PhoneFlipDetector.onPhoneFlip += OnPhoneFlip;
        }

        void OnDisable()
        {
            PhoneFlipDetector.onPhoneFlip -= OnPhoneFlip;
        }

        void Start()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        void OnPhoneFlip(bool state)
        {
            if (state)
            {
                // Flip Off
                rectTransform.anchorMin = new Vector2(1, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.anchoredPosition = new Vector3(-50, -50, 0);
            }
            else
            {
                // Flip On
                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(0, 0);
                rectTransform.anchoredPosition = new Vector3(50, 50, 0);
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace StarWriter.Core.Input
{
    public class FlipUIScreen : MonoBehaviour
    {
        private void OnEnable()
        {
            GameManager.onPhoneFlip += OnPhoneFlip;
        }

        private void OnDisable()
        {
            GameManager.onPhoneFlip -= OnPhoneFlip;
        }

        RectTransform rectTransform;

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnPhoneFlip(bool state)
        {
            if (state)
            {
                rectTransform.anchorMin = new Vector2(1, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.anchoredPosition = new Vector3(-50, -50, 0);
                
            }
            else
            {
                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(0, 0);
                rectTransform.anchoredPosition = new Vector3(50, 50, 0);
                
            }
        }
    }

}

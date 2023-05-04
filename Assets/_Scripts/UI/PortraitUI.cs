using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortraitUI : MonoBehaviour
{
    [SerializeField] Player player;
    RectTransform rectTransform;

    [SerializeField] Vector2 portraitAnchorMin;
    [SerializeField] Vector2 portraitAnchorMax;
    [SerializeField] Vector2 anchorMin;
    [SerializeField] Vector2 anchorMax;
    [SerializeField] Vector2 portraitPosition;
    [SerializeField] Vector2 position;

    // Start is called before the first frame update
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        if (player.Ship.InputController.Portrait)
        {
            // Set the anchorMin and anchorMax values to center the RectTransform
            rectTransform.anchorMin = portraitAnchorMin;
            rectTransform.anchorMax = portraitAnchorMax;

            // Set the position of the RectTransform
            rectTransform.localPosition = portraitPosition;
            // Rotate the RectTransform 90 degrees around the z-axis
            rectTransform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else
        {
      

            // Set the anchorMin and anchorMax values to center the RectTransform
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;

            // Set the position of the RectTransform
            rectTransform.localPosition = position;

            // Rotate the RectTransform 90 degrees around the z-axis
            rectTransform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
}

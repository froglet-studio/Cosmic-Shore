using System.Collections;
using UnityEngine;

public class PortraitUI : MonoBehaviour
{
    [SerializeField] Player player;
    RectTransform rectTransform;
    bool playerReady;

    [SerializeField] Vector2 anchorMin;
    [SerializeField] Vector2 anchorMax;
    [SerializeField] Vector2 position;
    [SerializeField] Vector2 portraitAnchorMin;
    [SerializeField] Vector2 portraitAnchorMax;
    [SerializeField] Vector2 portraitPosition;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        StartCoroutine(InitializeCoroutine());
    }

    IEnumerator InitializeCoroutine()
    {
        yield return new WaitUntil(() => Player.ActivePlayer != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship.InputController != null);

        playerReady = true;
    }

    void Update()
    {
        if (!playerReady) return;

        if (Player.ActivePlayer.Ship.InputController.Portrait)
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

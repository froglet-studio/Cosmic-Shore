using System.Collections;
using UnityEngine;


namespace CosmicShore.Game.UI
{
    public class PortraitUI : MonoBehaviour
    {
        [SerializeField] R_Player player;
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
            // TODO - Can't have ActivePlayer as static, find other way! Use Player Ready event!
            // yield return new WaitUntil(() => Player.ActivePlayer != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship.ShipStatus.InputController != null);
            // TEMP
            yield return null;
            enabled = false;

            playerReady = true;
        }

        void Update()
        {
            if (!playerReady) return;

            // TODO - Can't have ActivePlayer as static
            // if (Player.ActivePlayer.Ship.ShipStatus.InputController.Portrait)
            if (true) // TEMP
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

}
using System.Collections;
using UnityEngine;


namespace CosmicShore.Game.UI
{
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
            // TODO - Can't have LocalPlayer as static, find other way! Use Player Ready event!
            // yield return new WaitUntil(() => Player.LocalPlayer != null && Player.LocalPlayer.Vessel != null && Player.LocalPlayer.Vessel != null && Player.LocalPlayer.Vessel.VesselStatus.InputController != null);
            // TEMP
            yield return null;
            enabled = false;

            playerReady = true;
        }

        void Update()
        {
            if (!playerReady) return;

            // TODO - Can't have LocalPlayer as static
            // if (Player.LocalPlayer.Vessel.VesselStatus.InputController.Portrait)
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
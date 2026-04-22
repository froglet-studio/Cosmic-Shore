using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
        fileName = "GameFeedSettings",
        menuName = "ScriptableObjects/UI/Game Feed Settings")]
    public class GameFeedSettingsSO : ScriptableObject
    {
        [Header("Slide In")]
        public float slideInDuration = 0.25f;
        public float slideInOffset = 200f;
        public Ease slideInEase = Ease.OutBack;

        [Header("Hold")]
        public float holdDuration = 3.0f;

        [Header("Fade Out")]
        public float fadeOutDuration = 0.5f;

        [Header("Capacity")]
        public int maxVisibleEntries = 6;

        [Header("Time")]
        public bool useUnscaledTime = true;

        [Header("Text")]
        [Tooltip("Font asset for feed entries. Assign Aldrich Regular SDF.")]
        public TMP_FontAsset fontAsset;

        [Tooltip("Minimum auto-size font size")]
        public float fontSizeMin = 8f;

        [Tooltip("Maximum auto-size font size")]
        public float fontSizeMax = 14f;
    }
}

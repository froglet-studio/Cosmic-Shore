using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Game.UI
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
    }
}

using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "ScorePopupSettings",
        menuName = "ScriptableObjects/UI/Score Popup Settings")]
    public class ScorePopupSettingsSO : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("How long the popup is visible before fully fading out")]
        public float displayDuration = 2f;

        [Tooltip("Window in seconds to stack consecutive scores into a combo")]
        public float comboWindow = 2f;

        [Header("Animation")]
        [Tooltip("How far the text floats upward (world units)")]
        public float floatDistance = 0.5f;

        [Tooltip("Start scale of the text (punch in)")]
        public float startScale = 0.5f;

        [Tooltip("Peak scale during punch")]
        public float punchScale = 1.3f;

        [Tooltip("Duration of the scale punch in")]
        public float punchDuration = 0.15f;

    }
}

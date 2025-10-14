using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public enum SlideDirection { FromRight, FromLeft, FromTop, FromBottom }

    [CreateAssetMenu(fileName = "NotificationSettings",
        menuName = "ScriptableObjects/UI/Notification Settings")]
    public class NotificationSettingsSO : ScriptableObject
    {
        [Header("Slide Directions")]
        public SlideDirection inFrom  = SlideDirection.FromRight;
        public SlideDirection outTo   = SlideDirection.FromRight;

        [Header("Padding (px beyond fully hidden)")]
        public float inPadding  = 24f;   // extra pixels off-screen for enter
        public float outPadding = 24f;   // extra pixels off-screen for exit

        [Header("Timing")]
        public float inDuration   = 0.20f;
        public float holdDuration = 1.25f;
        public float outDuration  = 0.20f;
        public bool  useUnscaledTime = true;

        [Header("Ease")]
        public Ease inEase  = Ease.OutCubic;
        public Ease outEase = Ease.InCubic;

        [Header("Alpha")]
        public bool  fade       = true;
        public float startAlpha = 0f;
        public float endAlpha   = 1f;

        [Header("Scale (optional)")]
        public bool    scale      = false;
        public Vector3 startScale = Vector3.one * 0.98f;
        public Vector3 endScale   = Vector3.one;

        [Header("Queue")]
        public int maxQueue = 3;
    }
}
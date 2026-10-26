using DG.Tweening;
using UnityEngine;

namespace CosmicShore.UI
{
    public enum SlideDirection { FromRight = 0, FromLeft = 1, FromTop = 2, FromBottom = 3 }

    [CreateAssetMenu(fileName = "NotificationSettings",
        menuName = "ScriptableObjects/UI/Notification Settings")]
    public class NotificationSettingsSO : ScriptableObject
    {
        [Header("Slide Directions")]
        [SerializeField] SlideDirection inFrom  = SlideDirection.FromRight;
        public SlideDirection InFrom => inFrom;
        [SerializeField] SlideDirection outTo   = SlideDirection.FromRight;
        public SlideDirection OutTo => outTo;

        [Header("Padding (px beyond fully hidden)")]
        [SerializeField] float inPadding  = 24f;   // extra pixels off-screen for enter
        public float InPadding => inPadding;
        [SerializeField] float outPadding = 24f;   // extra pixels off-screen for exit
        public float OutPadding => outPadding;

        [Header("Timing")]
        [SerializeField] float inDuration   = 0.20f;
        public float InDuration => inDuration;
        [SerializeField] float holdDuration = 1.25f;
        public float HoldDuration => holdDuration;
        [SerializeField] float outDuration  = 0.20f;
        public float OutDuration => outDuration;
        [SerializeField] bool useUnscaledTime = true;
        public bool UseUnscaledTime => useUnscaledTime;

        [Header("Ease")]
        [SerializeField] Ease inEase  = Ease.OutCubic;
        public Ease InEase => inEase;
        [SerializeField] Ease outEase = Ease.InCubic;
        public Ease OutEase => outEase;

        [Header("Alpha")]
        [SerializeField] bool fade       = true;
        public bool Fade => fade;
        [SerializeField] float startAlpha = 0f;
        public float StartAlpha => startAlpha;
        [SerializeField] float endAlpha   = 1f;
        public float EndAlpha => endAlpha;

        [Header("Scale (optional)")]
        public bool    scale      = false;
        [SerializeField] Vector3 startScale = Vector3.one * 0.98f;
        public Vector3 StartScale => startScale;
        [SerializeField] Vector3 endScale   = Vector3.one;
        public Vector3 EndScale => endScale;

        [Header("Queue")]
        [SerializeField] int maxQueue = 3;
        public int MaxQueue => maxQueue;
    }
}
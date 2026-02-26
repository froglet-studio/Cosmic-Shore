using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class EllipsisTextLooper : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string baseText = "CONNECTING TO THE SHORE";
        
        [Tooltip("Delay between each dot update (seconds)")]
        [SerializeField] private float stepDuration = 0.3f;
        
        [Tooltip("Hold time when all 3 dots are shown")]
        [SerializeField] private float holdDuration = 0.5f;
        
        [Tooltip("Ease used for small text flicker/fade optional")]
        [SerializeField] private Ease flickerEase = Ease.Linear;
        
        [Tooltip("Optional flicker intensity (0 = off)")]
        [Range(0, 1f)] [SerializeField] private float flickerIntensity = 0.05f;

        private TMP_Text tmp;
        private Sequence seq;

        void OnEnable()
        {
            tmp = GetComponent<TMP_Text>();
            StartLoop();
        }

        void OnDisable()
        {
            KillSequence();
        }

        void KillSequence()
        {
            if (seq != null)
            {
                seq.Kill();
                seq = null;
            }
        }

        void StartLoop()
        {
            KillSequence();
            if (!tmp) return;

            seq = DOTween.Sequence().SetUpdate(true); // ignore timescale (works on paused state)
            seq.AppendCallback(() => SetDots(1));
            seq.AppendInterval(stepDuration);
            seq.AppendCallback(() => SetDots(2));
            seq.AppendInterval(stepDuration);
            seq.AppendCallback(() => SetDots(3));
            seq.AppendInterval(holdDuration);
            seq.AppendCallback(() => SetDots(0));
            seq.AppendInterval(stepDuration);
            seq.SetLoops(-1);

            // Optional subtle flicker pulse if enabled
            if (flickerIntensity > 0f)
            {
                seq.Join(tmp.DOFade(1f - flickerIntensity, stepDuration * 0.5f)
                    .SetEase(flickerEase)
                    .SetLoops(-1, LoopType.Yoyo));
            }
        }

        void SetDots(int count)
        {
            string dots = new string('.', count);
            tmp.text = $"{baseText}{dots}";
        }
    }
}
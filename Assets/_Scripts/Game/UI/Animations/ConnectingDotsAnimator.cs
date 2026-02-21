using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Animates trailing dots on a text label (e.g. "Connecting to Shore" -> "Connecting to Shore." -> "..." -> repeat).
    /// Attach to the same GameObject as the TMP_Text, or assign it manually.
    /// </summary>
    public sealed class ConnectingDotsAnimator : MonoBehaviour
    {
        [SerializeField] private TMP_Text tmpText;
        [SerializeField] private string baseText = "Connecting to Shore";
        [SerializeField] private int maxDots = 3;
        [SerializeField] private float dotInterval = 0.4f;

        private CancellationTokenSource _cts;

        void OnValidate()
        {
            if (tmpText == null) tmpText = GetComponent<TMP_Text>();
        }

        public void StartAnimation()
        {
            StopAnimation();
            _cts = new CancellationTokenSource();
            RunDotsLoop(_cts.Token).Forget();
        }

        public void StopAnimation()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private async UniTaskVoid RunDotsLoop(CancellationToken ct)
        {
            int dotCount = 0;
            while (!ct.IsCancellationRequested)
            {
                dotCount = (dotCount % (maxDots + 1));
                string dots = new string('.', dotCount);
                // Pad with spaces to prevent text jitter
                string padding = new string(' ', maxDots - dotCount);
                if (tmpText) tmpText.text = baseText + dots + padding;

                dotCount++;
                await UniTask.Delay(
                    (int)(dotInterval * 1000),
                    cancellationToken: ct
                );
            }
        }

        void OnDisable()
        {
            StopAnimation();
        }
    }
}

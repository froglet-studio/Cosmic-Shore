using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Typewriter-style text animator that reveals characters one at a time.
    /// Uses a baked-in fullText field set in the inspector.
    /// </summary>
    public class DoTweenTypewriterAnimator : MonoBehaviour
    {
        [SerializeField] private TMP_Text textDisplay;
        [SerializeField] private string fullText = "INITIALIZING COSMIC SHORE";
        [SerializeField] private float characterDelay = 0.03f;

        public async UniTaskVoid PlayIn(CancellationToken ct = default)
        {
            if (textDisplay == null) return;

            textDisplay.text = "";
            for (int i = 0; i < fullText.Length; i++)
            {
                if (ct.IsCancellationRequested) return;
                textDisplay.text = fullText[..(i + 1)];
                await UniTask.Delay((int)(characterDelay * 1000), cancellationToken: ct);
            }
        }

        public void ClearInstant()
        {
            if (textDisplay != null)
                textDisplay.text = "";
        }
    }
}

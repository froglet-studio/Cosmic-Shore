using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Animates text with a typewriter effect using DOTween.
    /// </summary>
    public class DoTweenTypewriterAnimator : MonoBehaviour
    {
        public UniTaskVoid PlayIn(CancellationToken ct)
        {
            return UniTask.CompletedTask.AsUniTask().AsUniTaskVoid();
        }

        public void ClearInstant()
        {
        }
    }
}

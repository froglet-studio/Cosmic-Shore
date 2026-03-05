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
        public async UniTaskVoid PlayIn(CancellationToken ct)
        {
            await UniTask.CompletedTask;
        }

        public void ClearInstant()
        {
        }
    }
}

using System.Threading;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Original contract: the UniTask <c>GameObject.GetCancellationTokenOnDestroy()</c> extension.
    /// Backs the token with a lightweight component whose own destroy token fires when the
    /// GameObject is destroyed (DestroyNow tears down every component). Ported statics that own a
    /// bare GameObject (e.g. ToyFactory.ScaleOutAndDestroy) call it just like the MonoBehaviour form.
    /// </summary>
    public static class GameObjectCancellationExtensions
    {
        public static CancellationToken GetCancellationTokenOnDestroy(this GameObject go)
        {
            var src = go.GetComponent<DestroyTokenSource>() ?? go.AddComponent<DestroyTokenSource>();
            return src.GetCancellationTokenOnDestroy();
        }

        sealed class DestroyTokenSource : MonoBehaviour { }
    }
}

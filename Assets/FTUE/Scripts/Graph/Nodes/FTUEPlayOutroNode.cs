using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plays the FTUE outro (captain slides out, panel fades) via
    /// <see cref="FTUEIntroAnimator.PlayOutro"/>, then advances.
    /// </summary>
    public class FTUEPlayOutroNode : FTUENodeSO
    {
        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.IntroAnimator == null)
            {
                Debug.LogWarning("[FTUE] PlayOutroNode: FTUEIntroAnimator not wired — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            yield return ctx.IntroAnimator.PlayOutro(null);
            advance(FTUEPorts.Next);
        }
    }
}

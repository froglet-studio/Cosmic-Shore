using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plays the FTUE intro cinematic (captain slides in, black overlay dims) via
    /// <see cref="FTUEIntroAnimator.PlayIntro"/>, then advances.
    /// </summary>
    public class FTUEPlayIntroNode : FTUENodeSO
    {
        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.IntroAnimator == null)
            {
                Debug.LogWarning("[FTUE] PlayIntroNode: FTUEIntroAnimator not wired — skipping.");
                advance(FTUEPorts.Next);
                yield break;
            }

            yield return ctx.IntroAnimator.PlayIntro(null);
            advance(FTUEPorts.Next);
        }
    }
}

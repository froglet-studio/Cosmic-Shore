using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plays the FTUE intro cinematic (captain slides in, black overlay dims) via
    /// <see cref="FTUEIntroAnimator.PlayIntro"/>, then advances.
    /// </summary>
    public class QuestPlayIntroNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Flow;
        public override string TypeTooltip => "Plays the captain intro cinematic (overlay dim + captain slide-in).";
        public override string EditorSummary => "Captain slides in";

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.IntroAnimator == null)
            {
                Debug.LogWarning("[Quest] PlayIntroNode: FTUEIntroAnimator not wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            yield return ctx.IntroAnimator.PlayIntro(null);
            advance(QuestPorts.Next);
        }
    }
}

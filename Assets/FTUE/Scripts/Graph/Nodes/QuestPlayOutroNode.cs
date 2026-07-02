using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plays the FTUE outro (captain slides out, panel fades) via
    /// <see cref="FTUEIntroAnimator.PlayOutro"/>, then advances.
    /// </summary>
    public class QuestPlayOutroNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Flow;
        public override string TypeTooltip => "Plays the captain outro cinematic (slide-out + fade).";
        public override string EditorSummary => "Captain slides out";

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.IntroAnimator == null)
            {
                Debug.LogWarning("[Quest] PlayOutroNode: FTUEIntroAnimator not wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            yield return ctx.IntroAnimator.PlayOutro(null);
            advance(QuestPorts.Next);
        }
    }
}

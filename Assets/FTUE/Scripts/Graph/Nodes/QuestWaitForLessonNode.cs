using System.Collections;
using CosmicShore.Gameplay;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until a microgame Lesson has ended (Docs/ModePreview/TRAINING_PLAN.md §5): the beat
    /// that holds the first-login railroad's navigation lock until the new player has learned to
    /// fly.
    ///
    /// <para>Two ways to end, both needed. <see cref="DrillProgressStore.CompletedAnyLesson"/> is
    /// the same key that makes the account's first Lesson unskippable, so "the quest may move on"
    /// and "the Lesson may be skipped" cannot disagree, and a returning account advances at once.
    /// <see cref="DrillRunner.AnyLessonEnded"/> covers the one skip a fresh account can make - a
    /// party guest's, which is never forced - and which sets no key, so waiting on the key alone
    /// would hold that guest forever.</para>
    ///
    /// <para><see cref="QuestOpenMicrogameNode"/> carries the same wait (its
    /// <c>holdUntilLessonEnds</c>), which is what the railroad uses: a quest RESUMES at its saved
    /// node, so a wait that lived on its own node would resume on a home screen with nothing
    /// open. This node is for a graph that opened the microgame some other way.</para>
    /// </summary>
    public class QuestWaitForLessonNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until a microgame Lesson has been completed or skipped.";
        public override string EditorSummary => "Until a Lesson ends";

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            yield return Until(ctx);
            advance(QuestPorts.Next);
        }

        /// <summary>Yields until a Lesson has ended, or at once if the account already finished one.</summary>
        public static IEnumerator Until(QuestRuntimeContext ctx)
        {
            if (DrillProgressStore.CompletedAnyLesson) yield break;

            bool ended = false;
            void Handle(bool _) => ended = true;
            DrillRunner.AnyLessonEnded += Handle;
            ctx.AddCleanup(() => DrillRunner.AnyLessonEnded -= Handle);

            while (!ended && !DrillProgressStore.CompletedAnyLesson)
                yield return null;
        }

        public override void DebugForceSatisfy(QuestRuntimeContext ctx) =>
            DrillProgressStore.RecordLessonCompleted(FlightScheme.OneThumb);   // sets "any lesson" only
    }
}

using System.Collections;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>Which mode a <see cref="QuestOpenMicrogameNode"/> opens.</summary>
    public enum QuestMicrogameSource
    {
        /// <summary>This week's Game of the Week (<c>Resources/GameOfTheWeek</c>).</summary>
        GameOfTheWeek = 0,
        /// <summary>The node's own <see cref="QuestOpenMicrogameNode.mode"/>.</summary>
        SpecificMode = 1,
    }

    /// <summary>
    /// Walks the player onto an arcade card's microgame: opens the Arcade, selects the card (as if
    /// it were pressed), and - when <see cref="forceEntry"/> is on - arms the preview to fly the
    /// vessel in by itself the moment it goes live (Docs/ModePreview/TRAINING_PLAN.md §5).
    ///
    /// <para>It does not force the Lesson. Whether the Lesson can be skipped is decided by the
    /// drill's own account keys, so a player who already learned to fly is walked in and still gets
    /// Skip.</para>
    ///
    /// <para><see cref="holdUntilLessonEnds"/> keeps the quest on THIS node until the Lesson ends.
    /// That is deliberate rather than a separate wait node: a quest resumes at its saved node, so
    /// a player who quits mid-Lesson must resume HERE, where the microgame is opened again, and
    /// not on a wait that would sit on the home screen with nothing open.</para>
    ///
    /// <para>Fails open: a card the roster does not carry is logged and the node advances, so a
    /// mis-authored rotation can never trap a new player on the home screen.</para>
    /// </summary>
    public class QuestOpenMicrogameNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip =>
            "Opens the Arcade on a card's microgame (the Game of the Week, or a named mode) and, if forced, flies the vessel in.";
        public override string EditorSummary =>
            (source == QuestMicrogameSource.GameOfTheWeek ? "Game of the Week" : mode.ToString()) +
            (forceEntry ? " (forced)" : string.Empty) +
            (holdUntilLessonEnds ? ", until the Lesson ends" : string.Empty);

        [Tooltip("Where the mode comes from.")]
        public QuestMicrogameSource source = QuestMicrogameSource.GameOfTheWeek;

        [Tooltip("Used when Source is SpecificMode.")]
        public GameModes mode = GameModes.SkimRace;

        [Tooltip("On = the preview takes focus by itself as soon as it is live, as if the player tapped it.")]
        public bool forceEntry = true;

        [Tooltip("On = this node advances only once the Lesson has ended (completed, or skipped where " +
                 "skipping is allowed). A resumed quest then re-opens the microgame instead of waiting " +
                 "on the home screen.")]
        public bool holdUntilLessonEnds = true;

        [Tooltip("Seconds between opening the Arcade and selecting the card, so the screen has arrived.")]
        [Min(0f)] public float settleSeconds = 0.6f;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            var target = ResolveMode();

            if (ctx.ScreenSwitcher != null) ctx.ScreenSwitcher.OnClickArcadeNav();
            else Debug.LogWarning("[Quest] OpenMicrogameNode: ScreenSwitcher not wired - selecting the card without opening the Arcade.");

            if (settleSeconds > 0f) yield return new WaitForSecondsRealtime(settleSeconds);

            if (forceEntry) ModePreviewSession.ArmForcedEntry(target);

            bool opened = false;
            var views = Object.FindObjectsByType<ArcadeExploreView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var view in views)
                if (view && view.TrySelectMode(target)) { opened = true; break; }

            if (!opened)
            {
                ModePreviewSession.DisarmForcedEntry();
                Debug.LogWarning($"[Quest] OpenMicrogameNode: no arcade card for {target} - moving on.");
            }

            if (opened && holdUntilLessonEnds)
                yield return QuestWaitForLessonNode.Until(ctx);

            advance(QuestPorts.Next);
        }

        GameModes ResolveMode()
        {
            if (source == QuestMicrogameSource.SpecificMode) return mode;
            var gotw = GameOfTheWeekSO.Load();
            if (gotw) return gotw.Current();
            Debug.LogWarning($"[Quest] OpenMicrogameNode: no Resources/{GameOfTheWeekSO.ResourcePath} - using {mode}.");
            return mode;
        }
    }
}

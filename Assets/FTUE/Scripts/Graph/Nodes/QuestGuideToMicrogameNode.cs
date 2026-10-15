using System.Collections;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>Which mode a <see cref="QuestGuideToMicrogameNode"/> leads to.</summary>
    public enum QuestMicrogameSource
    {
        /// <summary>This week's Game of the Week (<c>Resources/GameOfTheWeek</c>).</summary>
        GameOfTheWeek = 0,
        /// <summary>The node's own <see cref="QuestGuideToMicrogameNode.mode"/>.</summary>
        SpecificMode = 1,
    }

    /// <summary>
    /// Guides a player to an arcade card's microgame and holds until they have finished (or
    /// skipped) its Lesson - by SHOWING the way, never by taking them there.
    ///
    /// <para><b>The guided-path rule</b> (Docs/HomeHub/ARCHITECTURE.md §8): every step spotlights
    /// the one control the player should press next - the Arcade entry, then this week's card, then
    /// the preview window - with a call to action on it and everything else dimmed and dead, except
    /// Settings. The player presses each one themselves. Nothing here opens a screen, selects a card
    /// or flies the vessel in, because a player who was carried somewhere cannot find their way
    /// back, and one who walked a locked path by hand can.</para>
    ///
    /// <para><b>The step is RE-DERIVED every frame from where the player is</b>, not advanced
    /// through a sequence: preview open → spotlight the window; Arcade open → the card; home → the
    /// Arcade entry. So a resumed quest, a window that closed, or a player who wandered into
    /// Settings and back always lands on the right step, and there is no step counter to drift.
    /// While the player is flying (the window has focus) or Settings is open, the spotlight stands
    /// aside.</para>
    ///
    /// <para><b>Not a lock under the master developer unlock.</b> <see cref="QuestNodeSO.AppliesLock"/>
    /// stays false: the spotlight takes nothing away permanently - it is a guide for the length of
    /// one beat, released when the beat ends - and it opens the card it points at
    /// (<see cref="MenuGuide.ExemptsMode"/>) rather than closing any.</para>
    ///
    /// <para><b>Fails open.</b> If no step can be found for a moment the spotlight fades away
    /// rather than dimming a screen with no way forward; it returns the moment a step reappears.
    /// The node still holds, so a resume walks the player in again.</para>
    /// </summary>
    public class QuestGuideToMicrogameNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip =>
            "Spotlights the way to an arcade card's microgame (the Game of the Week, or a named mode) - " +
            "Arcade entry, then the card, then the preview - for the player to press themselves, and " +
            "holds until the Lesson ends.";
        public override string EditorSummary =>
            "Guide to " + (source == QuestMicrogameSource.GameOfTheWeek ? "Game of the Week" : mode.ToString()) +
            ", until the Lesson ends";

        [Header("Destination")]
        [Tooltip("Where the mode comes from.")]
        public QuestMicrogameSource source = QuestMicrogameSource.GameOfTheWeek;

        [Tooltip("Used when Source is SpecificMode.")]
        public GameModes mode = GameModes.SkimRace;

        [Header("Captions (empty = no caption)")]
        [Tooltip("Shown beside the Arcade entry on the home screen.")]
        [TextArea(1, 3)] public string arcadeCaption = "Your first race is waiting. Open the Arcade.";

        [Tooltip("Shown beside the card in the Arcade.")]
        [TextArea(1, 3)] public string cardCaption = "This week's game. Open it.";

        [Tooltip("Shown beside the preview window.")]
        [TextArea(1, 3)] public string previewCaption = "Tap the window to fly.";

        [Header("Look")]
        [Tooltip("How dark everything off the path is (0 = clear, 1 = black).")]
        [Range(0f, 1f)] public float dimAlpha = 0.7f;

        [Tooltip("Seconds without a step to spotlight before the spotlight fades away (fail open). " +
                 "Covers the frames a window takes to open.")]
        [Min(0.1f)] public float lostStepGraceSeconds = 1.5f;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (DrillProgressStore.CompletedAnyLesson)
            {
                advance(QuestPorts.Next);
                yield break;
            }

            var target = ResolveMode();
            var finder = new StepFinder(ctx, target);
            var spot = MenuSpotlight.Ensure();
            var cta = ResolveCtaColor(ctx);

            bool ended = false;
            void HandleEnded(bool _) => ended = true;
            DrillRunner.AnyLessonEnded += HandleEnded;

            MenuGuide.Begin(target);
            ctx.AddCleanup(() =>
            {
                DrillRunner.AnyLessonEnded -= HandleEnded;
                MenuGuide.End();
                if (spot) spot.Hide();
            });

            float lostFor = 0f;
            bool reportedLost = false;

            while (!ended && !DrillProgressStore.CompletedAnyLesson)
            {
                switch (finder.Resolve(out var rect))
                {
                    case Step.StandAside:
                        spot.Hide();
                        lostFor = 0f;
                        break;

                    case Step.Arcade:  spot.Show(rect, arcadeCaption, cta, dimAlpha);  lostFor = 0f; break;
                    case Step.Card:    spot.Show(rect, cardCaption, cta, dimAlpha);    lostFor = 0f; break;
                    case Step.Preview: spot.Show(rect, previewCaption, cta, dimAlpha); lostFor = 0f; break;

                    default:
                        lostFor += Time.unscaledDeltaTime;
                        if (lostFor >= lostStepGraceSeconds)
                        {
                            spot.Hide();
                            if (!reportedLost)
                            {
                                reportedLost = true;
                                Debug.LogWarning($"[Quest] GuideToMicrogame: nothing on screen leads to {target} " +
                                                 "- the spotlight stood aside so the player is not stuck under it.");
                            }
                        }
                        break;
                }

                yield return null;
            }

            advance(QuestPorts.Next);
        }

        public override void DebugForceSatisfy(QuestRuntimeContext ctx) =>
            DrillProgressStore.RecordLessonCompleted(FlightScheme.OneThumb);   // sets "any lesson" only

        GameModes ResolveMode()
        {
            if (source == QuestMicrogameSource.SpecificMode) return mode;
            var gotw = GameOfTheWeekSO.Load();
            if (gotw) return gotw.Current();
            Debug.LogWarning($"[Quest] GuideToMicrogame: no Resources/{GameOfTheWeekSO.ResourcePath} - using {mode}.");
            return mode;
        }

        /// <summary>The palette's call-to-action colour - the colour a free pickup wears.</summary>
        static Color ResolveCtaColor(QuestRuntimeContext ctx)
        {
            var set = ctx.GameData && ctx.GameData.ThemeManagerData ? ctx.GameData.ThemeManagerData.ColorSet : null;
            var c = set ? set.GetCtaSignalColor() : default;
            return c.a > 0f ? c : new Color(0.55f, 1f, 0.2f, 1f);
        }

        enum Step { None, StandAside, Arcade, Card, Preview }

        /// <summary>Works out, from the live menu, which control is the next step.</summary>
        sealed class StepFinder
        {
            const float RefindSeconds = 0.5f;

            readonly QuestRuntimeContext _ctx;
            readonly GameModes _mode;
            float _refindAt;
            ModePreviewSession[] _sessions;
            ArcadeExploreView _arcade;
            ArcadeExploreView[] _views = System.Array.Empty<ArcadeExploreView>();
            MenuHubButton _hubArcade;

            public StepFinder(QuestRuntimeContext ctx, GameModes mode)
            {
                _ctx = ctx;
                _mode = mode;
            }

            public Step Resolve(out RectTransform rect)
            {
                rect = null;
                var sw = _ctx.ScreenSwitcher;

                if (ModePreviewWindow.AnyHasFocus) return Step.StandAside;
                if (sw && (sw.ModalIsActive(ScreenSwitcher.ModalWindows.SETTINGS)
                           || sw.ModalIsActive(ScreenSwitcher.ModalWindows.CREDITS)))
                    return Step.StandAside;

                Refind();

                if (_sessions != null)
                    foreach (var session in _sessions)
                        if (session && session.IsActive && session.ActiveMode == _mode
                            && session.Window && session.Window.isActiveAndEnabled)
                        {
                            rect = session.Window.SurfaceRect;
                            return Step.Preview;
                        }

                if (!sw) return Step.None;

                if (sw.ModalIsActive(ScreenSwitcher.ModalWindows.ARCADE))
                {
                    if (_arcade && _arcade.TryRevealCard(_mode, out rect)) return Step.Card;
                    // The Arcade's grid is found by its host modal; should that ever not resolve,
                    // any grid that actually shows this card is the right one.
                    foreach (var view in _views)
                        if (view && view.TryRevealCard(_mode, out rect)) return Step.Card;
                    return Step.None;
                }

                if (sw.HasActiveModal) return Step.None;

                if (sw.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME) && _hubArcade && _hubArcade.isActiveAndEnabled)
                {
                    rect = (RectTransform)_hubArcade.transform;
                    return Step.Arcade;
                }

                var nav = _ctx.AllowedNavButton;
                if (nav && nav.isActiveAndEnabled)
                {
                    rect = (RectTransform)nav.transform;
                    return Step.Arcade;
                }
                return Step.None;
            }

            void Refind()
            {
                if (Time.unscaledTime < _refindAt) return;
                _refindAt = Time.unscaledTime + RefindSeconds;

                _sessions = Object.FindObjectsByType<ModePreviewSession>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                _views = Object.FindObjectsByType<ArcadeExploreView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (!_arcade)
                    foreach (var view in _views)
                    {
                        var host = view ? view.GetComponentInParent<ModalWindowManager>(true) : null;
                        if (host && host.ModalType == ScreenSwitcher.ModalWindows.ARCADE) { _arcade = view; break; }
                    }

                if (!_hubArcade)
                    foreach (var hub in Object.FindObjectsByType<MenuHubButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (hub && hub.Target == ScreenSwitcher.ModalWindows.ARCADE) { _hubArcade = hub; break; }
            }
        }
    }
}

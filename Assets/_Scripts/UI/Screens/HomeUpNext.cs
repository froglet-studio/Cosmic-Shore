using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Home's two docked cards under the pilot chip (Loop Atlas <c>era.tide</c>, the picked Frogma option C,
    /// Garrett 2026-11-21): <b>The Tide</b> - a strip with the week of the Tide (<see cref="TideCalendar"/>), a
    /// six-week clock and this week's card, opening to the challenge's mode, objective, "Ends in" countdown and
    /// your best - and <b>the quest chain</b> - the current quest with the ones around it as linked nodes (done,
    /// current, ahead), its next goal and its intensity. Both read data the game already keeps
    /// (<see cref="WeeklyChallengeService"/>, <see cref="GameModeProgressionService"/>); the Tide card opens the
    /// challenge in the Arcade's configure window, the quest card the Voyage window. Each folds at rest and
    /// opens on focus or hover (<see cref="HomeDockCard"/>).
    ///
    /// <para>A card with nothing to show hides through its CanvasGroup (<see cref="KitVisibility"/>) rather than
    /// showing an empty frame.</para>
    /// </summary>
    public class HomeUpNext : MonoBehaviour
    {
        [Header("The Tide")]
        [SerializeField] Button weeklyButton;
        [SerializeField] CanvasGroup weeklyGroup;
        [SerializeField, Tooltip("The strip's one line: the week of the Tide and this week's card.")]
        TMP_Text tideSummaryText;
        [SerializeField, Tooltip("One per week of the Tide, first first: passed weeks soft, this week light, the rest dark.")]
        KitThemedGraphic[] tideSegments = Array.Empty<KitThemedGraphic>();
        [SerializeField] TMP_Text weeklyModeText;
        [SerializeField] TMP_Text weeklyObjectiveText;
        [SerializeField] TMP_Text weeklyCountdownText;
        [SerializeField] TMP_Text weeklyBestText;

        [Header("The Tide's clock")]
        [SerializeField, Tooltip("The day Tide 1 began, yyyy-MM-dd in UTC. Empty hides the clock.")]
        string tideStartUtc = "2026-11-16";
        [SerializeField, Min(1), Tooltip("Weeks in a Tide.")]
        int tideWeeks = 6;

        [Header("Quest chain")]
        [SerializeField] Button questButton;
        [SerializeField] CanvasGroup questGroup;
        [SerializeField, Tooltip("The strip's one line: the current quest and where it sits in the chain.")]
        TMP_Text questSummaryText;
        [SerializeField] TMP_Text questNextText;
        [SerializeField] TMP_Text questMeterText;
        [SerializeField, Tooltip("The chain's nodes, left to right: done soft, current dark inside its ring, ahead dark.")]
        KitThemedGraphic[] chainNodes = Array.Empty<KitThemedGraphic>();
        [SerializeField, Tooltip("One ring per node; only the current quest's shows.")]
        KitThemedGraphic[] chainRings = Array.Empty<KitThemedGraphic>();
        [SerializeField, Tooltip("The links between nodes (one fewer than the nodes): lit up to the current quest.")]
        KitThemedGraphic[] chainLinks = Array.Empty<KitThemedGraphic>();
        [SerializeField, Tooltip("Each node's quest name.")]
        KitText[] chainLabels = Array.Empty<KitText>();

        [Header("Data")]
        [SerializeField, Tooltip("The arcade cards: a mode's name comes from its card.")]
        SO_GameList games;
        [SerializeField, Tooltip("Leave empty to find the one in the scene.")]
        ScreenSwitcher screenSwitcher;

        [SerializeField, Tooltip("Seconds between redraws (the countdown's resolution).")]
        float refreshSeconds = 1f;

        [Header("Words")]
        [SerializeField, Tooltip("The Tide strip; {week}, {weeks} and {mode} (this week's card) are filled in.")]
        string tideSummaryTemplate = "Week {week} of {weeks} \u00b7 {mode}";
        [SerializeField, Tooltip("The Tide strip when there is no card this week; {week} and {weeks} are filled in.")]
        string tideWeekOnlyTemplate = "Week {week} of {weeks}";
        [SerializeField, Tooltip("The Tide strip when there is no clock; {mode} is this week's card.")]
        string tideNoClockTemplate = "{mode}";
        [SerializeField, Tooltip("The weekly best when nothing has been played this week. Empty shows nothing.")]
        string noBestText = "None yet";
        [SerializeField, Tooltip("The quest strip; {quest}, {number} and {count} are filled in.")]
        string questSummaryTemplate = "{quest} \u00b7 {number} of {count}";
        [SerializeField, Tooltip("The quest line when its reward waits in the Quests window.")]
        string readyToClaimText = "Ready to claim in Quests";
        [SerializeField, Tooltip("The quest line for the next goal; {goal} is the quest's authored goal.")]
        string nextGoalTemplate = "Next: {goal}";
        [SerializeField, Tooltip("The quest line once every intensity is open.")]
        string allUnlockedText = "Every intensity unlocked";
        [SerializeField, Tooltip("The meter caption; {level} and {max} are filled in.")]
        string meterTemplate = "Intensity {level} of {max}";
        float _nextRefresh;
        string _lastCountdown;

        void Start()
        {
            if (!screenSwitcher) screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (weeklyButton) weeklyButton.onClick.AddListener(OpenWeekly);
            if (questButton) questButton.onClick.AddListener(OpenQuest);
            Refresh();
        }

        void OnDestroy()
        {
            if (weeklyButton) weeklyButton.onClick.RemoveListener(OpenWeekly);
            if (questButton) questButton.onClick.RemoveListener(OpenQuest);
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, refreshSeconds);
            Refresh();
        }

        void Refresh()
        {
            DrawWeekly();
            DrawQuest();
        }

        void OpenWeekly()
        {
            if (screenSwitcher) screenSwitcher.OpenWeeklyChallenge();
        }

        void OpenQuest()
        {
            if (screenSwitcher) screenSwitcher.OnClickQuests();
        }

        // ── The Tide ───────────────────────────────────────────────────────

        void DrawWeekly()
        {
            var service = WeeklyChallengeService.Instance;
            bool hasWeek = service != null && service.ThisWeek.IsValid;
            var start = TideCalendar.ParseStart(tideStartUtc);
            bool show = hasWeek || start.HasValue;
            if (weeklyGroup) KitVisibility.SetShown(weeklyGroup, show);
            if (!show) return;

            string mode = string.Empty;
            if (hasWeek)
            {
                var challenge = service.ThisWeek;
                var card = CardFor(challenge.GameMode);
                mode = card ? card.DisplayName : challenge.GameMode.ToString();
                Set(weeklyModeText, mode);
                Set(weeklyObjectiveText, challenge.ObjectiveText);
                string countdown = WeeklyChallengeCard.FormatCountdown(service.TimeUntilNextChallenge);
                if (countdown != _lastCountdown)
                {
                    _lastCountdown = countdown;
                    Set(weeklyCountdownText, countdown);
                }
                Set(weeklyBestText, BestText(service.BestTimeThisWeekMs, service.BestValueThisWeek, noBestText));
            }

            if (start.HasValue)
            {
                var week = TideCalendar.At(DateTime.UtcNow, start.Value, tideWeeks);
                Set(tideSummaryText, TemplateText.Fill(hasWeek ? tideSummaryTemplate : tideWeekOnlyTemplate,
                    "week", week.Week.ToString(), "weeks", week.Weeks.ToString(), "mode", mode));
                for (int i = 0; i < tideSegments.Length; i++)
                {
                    if (!tideSegments[i]) continue;
                    int n = i + 1;
                    if (n > week.Weeks) tideSegments[i].SetToken(UIColorToken.SurfaceDark, 0f);
                    else tideSegments[i].SetToken(n < week.Week ? UIColorToken.NeutralLightest
                                                : n == week.Week ? UIColorToken.TextLight : UIColorToken.SurfaceDark);
                }
            }
            else
            {
                Set(tideSummaryText, TemplateText.Fill(tideNoClockTemplate, "mode", mode));
                foreach (var seg in tideSegments)
                    if (seg) seg.SetToken(UIColorToken.SurfaceDark, 0f);
            }
        }

        /// <summary>The week's best as the card shows it: a race time, else a score, else "None yet".</summary>
        public static string BestText(long bestTimeMs, int bestValue, string noBest) =>
            bestTimeMs > 0 ? WeeklyChallengeRanking.FormatSeconds(bestTimeMs / 1000.0)
            : bestValue > 0 ? bestValue.ToString()
            : noBest ?? string.Empty;

        // ── Quest chain ────────────────────────────────────────────────────

        void DrawQuest()
        {
            var service = GameModeProgressionService.Instance;
            var list = service ? service.QuestList : null;
            int index = service && service.IsInitialized ? QuestFrontier.FrontierIndex(list, service) : -1;
            bool show = index >= 0 && list.Quests[index];
            if (questGroup) KitVisibility.SetShown(questGroup, show);
            if (!show) return;

            var quest = list.Quests[index];
            var state = QuestFrontier.StateOf(list, index, service);
            int maxIntensity = service.Config ? service.Config.MaxIntensity : 4;
            int unlocked = quest.FeatureKind == FeatureKind.GameMode ? Mathf.Max(1, service.GetMaxUnlockedIntensity(quest.GameMode)) : 0;
            int count = list.Quests.Count;

            Set(questSummaryText, TemplateText.Fill(questSummaryTemplate,
                "quest", quest.DisplayName, "number", (index + 1).ToString(), "count", count.ToString()));
            Set(questNextText, NextGoal(quest, state, service.IsQuestCompleted(quest.GameMode), unlocked, maxIntensity));
            Set(questMeterText, unlocked > 0
                ? TemplateText.Fill(meterTemplate, "level", unlocked.ToString(), "max", maxIntensity.ToString())
                : string.Empty);
            DrawChain(list, index, count);
        }

        void DrawChain(SO_UnlockList list, int current, int count)
        {
            int slots = chainNodes.Length;
            int first = TideCalendar.ChainStart(count, current, slots);
            for (int i = 0; i < slots; i++)
            {
                int q = first + i;
                bool used = first >= 0 && q < count && list.Quests[q];
                if (chainNodes[i])
                    chainNodes[i].SetToken(q < current ? UIColorToken.NeutralLightest
                                         : q == current ? UIColorToken.SurfaceVeryDark : UIColorToken.SurfaceDark, used ? 1f : 0f);
                if (i < chainRings.Length && chainRings[i])
                    chainRings[i].SetToken(UIColorToken.TextLight, used && q == current ? 1f : 0f);
                if (i < chainLabels.Length && chainLabels[i])
                {
                    chainLabels[i].SetText(used ? list.Quests[q].DisplayName : string.Empty);
                    chainLabels[i].SetColorToken(q == current ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
                }
                if (i < chainLinks.Length && chainLinks[i])
                    chainLinks[i].SetToken(q < current ? UIColorToken.NeutralLightest : UIColorToken.SurfaceDark,
                                           used && q + 1 < count ? 1f : 0f);
            }
        }

        /// <summary>
        /// The line under the quest's name: claimable, its own objective while unfinished, else the next
        /// intensity's goal (the quest's authored Intensity 3 / 4 descriptions).
        /// </summary>
        string NextGoal(SO_UnlockData quest, QuestItemState state, bool questCompleted, int unlocked, int maxIntensity)
        {
            if (state == QuestItemState.ReadyToClaim) return readyToClaimText ?? string.Empty;
            if (!questCompleted && !string.IsNullOrWhiteSpace(quest.Description)) return Goal(quest.Description);
            if (unlocked >= maxIntensity) return allUnlockedText ?? string.Empty;
            string goal = unlocked + 1 >= 4 ? quest.Intensity4GoalDescription
                        : unlocked + 1 == 3 ? quest.Intensity3GoalDescription
                        : quest.Description;
            return string.IsNullOrWhiteSpace(goal) ? string.Empty : Goal(goal);
        }

        string Goal(string goal) => string.IsNullOrEmpty(nextGoalTemplate) ? string.Empty : nextGoalTemplate.Replace("{goal}", goal);

        SO_ArcadeGame CardFor(GameModes mode)
        {
            if (!games || games.Games == null) return null;
            foreach (var g in games.Games)
                if (g && g.Mode == mode) return g;
            return null;
        }

        static void Set(TMP_Text label, string value)
        {
            if (label && label.text != value) label.text = value;
        }
    }
}

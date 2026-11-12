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
    /// Home's two "Up next" cards (Docs/MENU_REDESIGN_B.md section 21.2): this week's challenge - its
    /// mode, objective, a live "Ends in" countdown and your best - and the current quest - its mode,
    /// the next goal and the intensity meter. Both read data the game already keeps
    /// (<see cref="WeeklyChallengeService"/>, <see cref="GameModeProgressionService"/>); the weekly card
    /// opens the challenge in the Arcade's configure window, the quest card the Voyage window (Rev 13: the quest track
    /// moved there from the Profile).
    ///
    /// <para>A card whose service is missing or has nothing to show hides through its CanvasGroup
    /// (<see cref="KitVisibility"/>) rather than showing an empty frame.</para>
    /// </summary>
    public class HomeUpNext : MonoBehaviour
    {
        [Header("Weekly challenge")]
        [SerializeField] Button weeklyButton;
        [SerializeField] CanvasGroup weeklyGroup;
        [SerializeField] Image weeklyArt;
        [SerializeField] TMP_Text weeklyModeText;
        [SerializeField] TMP_Text weeklyObjectiveText;
        [SerializeField] TMP_Text weeklyCountdownText;
        [SerializeField] TMP_Text weeklyBestText;

        [Header("Current quest")]
        [SerializeField] Button questButton;
        [SerializeField] CanvasGroup questGroup;
        [SerializeField] Image questArt;
        [SerializeField] TMP_Text questTitleText;
        [SerializeField] TMP_Text questNextText;
        [SerializeField] TMP_Text questMeterText;
        [SerializeField, Tooltip("One per intensity, lowest first: Light while unlocked, Neutral Dark ahead.")]
        KitThemedGraphic[] questMeterSegments = Array.Empty<KitThemedGraphic>();

        [Header("Data")]
        [SerializeField, Tooltip("The arcade cards: a mode's name and art come from its card.")]
        SO_GameList games;
        [SerializeField, Tooltip("Leave empty to find the one in the scene.")]
        ScreenSwitcher screenSwitcher;

        [SerializeField, Tooltip("Seconds between redraws (the countdown's resolution).")]
        float refreshSeconds = 1f;

        [Header("Docking (rev 3)")]
        [SerializeField, Tooltip("The weekly card's fold / close. A closed card stays hidden for the session.")]
        HomeDockCard weeklyDock;
        [SerializeField, Tooltip("The quest card's fold / close.")]
        HomeDockCard questDock;

        [Header("Words")]
        [SerializeField, Tooltip("The weekly best when nothing has been played this week. Empty shows nothing.")]
        string noBestText = "None yet";
        [SerializeField, Tooltip("The quest line when its reward waits in the Voyage window.")]
        string readyToClaimText = "Ready to claim in Voyage";
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
            if (screenSwitcher) screenSwitcher.OnClickMissionNav();
        }

        // ── Weekly challenge ───────────────────────────────────────────────

        void DrawWeekly()
        {
            var service = WeeklyChallengeService.Instance;
            bool show = service != null && service.ThisWeek.IsValid && !(weeklyDock && weeklyDock.IsClosed);
            if (weeklyGroup) KitVisibility.SetShown(weeklyGroup, show);
            if (!show) return;

            var challenge = service.ThisWeek;
            var card = CardFor(challenge.GameMode);
            Set(weeklyModeText, card ? card.DisplayName : challenge.GameMode.ToString());
            Set(weeklyObjectiveText, challenge.ObjectiveText);
            if (weeklyArt && card && card.CardBackground && weeklyArt.sprite != card.CardBackground)
                weeklyArt.sprite = card.CardBackground;

            string countdown = WeeklyChallengeCard.FormatCountdown(service.TimeUntilNextChallenge);
            if (countdown != _lastCountdown)
            {
                _lastCountdown = countdown;
                Set(weeklyCountdownText, countdown);
            }
            Set(weeklyBestText, BestText(service.BestTimeThisWeekMs, service.BestValueThisWeek, noBestText));
        }

        /// <summary>The week's best as the card shows it: a race time, else a score, else "None yet".</summary>
        public static string BestText(long bestTimeMs, int bestValue, string noBest) =>
            bestTimeMs > 0 ? WeeklyChallengeRanking.FormatSeconds(bestTimeMs / 1000.0)
            : bestValue > 0 ? bestValue.ToString()
            : noBest ?? string.Empty;

        // ── Current quest ──────────────────────────────────────────────────

        void DrawQuest()
        {
            var service = GameModeProgressionService.Instance;
            var list = service ? service.QuestList : null;
            int index = service && service.IsInitialized ? QuestFrontier.FrontierIndex(list, service) : -1;
            bool show = index >= 0 && list.Quests[index] && !(questDock && questDock.IsClosed);
            if (questGroup) KitVisibility.SetShown(questGroup, show);
            if (!show) return;

            var quest = list.Quests[index];
            var state = QuestFrontier.StateOf(list, index, service);
            int maxIntensity = service.Config ? service.Config.MaxIntensity : 4;
            int unlocked = quest.FeatureKind == FeatureKind.GameMode ? Mathf.Max(1, service.GetMaxUnlockedIntensity(quest.GameMode)) : 0;

            Set(questTitleText, quest.DisplayName);
            Set(questNextText, NextGoal(quest, state, service.IsQuestCompleted(quest.GameMode), unlocked, maxIntensity));
            Set(questMeterText, unlocked > 0 && !string.IsNullOrEmpty(meterTemplate)
                ? meterTemplate.Replace("{level}", unlocked.ToString()).Replace("{max}", maxIntensity.ToString())
                : string.Empty);
            for (int i = 0; i < questMeterSegments.Length; i++)
            {
                if (!questMeterSegments[i]) continue;
                questMeterSegments[i].SetToken(i < unlocked ? UIColorToken.Cta : UIColorToken.SurfaceDark);   // earned progress is good: lime
            }
            var card = CardFor(quest.GameMode);
            var art = card && card.CardBackground ? card.CardBackground : quest.Icon;
            if (questArt && art && questArt.sprite != art) questArt.sprite = art;
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

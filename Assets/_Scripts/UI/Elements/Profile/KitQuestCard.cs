using System;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One quest card of the style-B Profile screen (Docs/MENU_REDESIGN_B.md section 15): the mode's
    /// card art with its step numeral, the state line (Completed / Ready to claim / In progress /
    /// Locked), the quest's name and blurb, its two intensity goals with done / next / to-do marks,
    /// and a footer that is the Claim button, the Play button with the intensity meter, or the
    /// "Unlocks after ..." line. Above the card sits its node on the quest track and the segment
    /// that joins it to the previous one.
    ///
    /// <para>State is the legacy <see cref="QuestItemCard"/>'s, driven by <c>QuestTrackView</c>
    /// exactly as before; this class only draws it (DrawState, SetContext, SetActiveFrontier).</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Quest Card")]
    public class KitQuestCard : QuestItemCard
    {
        [Serializable]
        private class GoalRow
        {
            [Tooltip("The row's group; hidden on a quest with no intensity goals (the hangar).")]
            public CanvasGroup group;
            [Tooltip("The check, shown once the goal is met.")]
            public Graphic doneMark;
            [Tooltip("The diamond: filled for the next goal, faint for one still to come.")]
            public Graphic pendingMark;
            [Tooltip("\"Unlock intensity 3\" (Label role).")]
            public KitText caption;
            [Tooltip("The goal's own copy (SO_UnlockData.Intensity3/4GoalDescription).")]
            public KitText description;
        }

        [Header("Words (empty shows nothing)")]
        [Tooltip("The play button. {quest} is the game's name.")]
        [SerializeField] private string playLine = "Play {quest}";
        [Tooltip("Under a locked card. {after} names what unlocks it.")]
        [SerializeField] private string unlocksAfterLine = "Unlocks after {after}";
        [Tooltip("The meter's caption. {level} is the highest unlocked intensity, {max} the top one.")]
        [SerializeField] private string meterLine = "Intensity {level} of {max}";
        [Tooltip("A goal row. {level} is the intensity it unlocks.")]
        [SerializeField] private string goalLine = "Unlock intensity {level}";
        // Not "completedLine": QuestItemCard (the base) already serializes that name for its
        // description line, and Unity refuses a field name repeated down a hierarchy ("The same
        // field name is serialized multiple times", Bug Ledger E-2c9bfa16f7).
        [Tooltip("The state line on a claimed quest.")]
        [SerializeField] private string claimedLine = "Completed";
        [Tooltip("The state line on a quest whose reward waits to be claimed.")]
        [SerializeField] private string readyLine = "Ready to claim";
        [Tooltip("The state line on an unlocked quest still being played.")]
        [SerializeField] private string inProgressLine = "In progress";
        [Tooltip("The state line on a locked quest.")]
        [SerializeField] private string lockedLine = "Locked";

        [Header("Style-B Card")]
        [Tooltip("The chrome theme.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("The card border: Cta 2 px while ready to claim, Light while in progress, Neutral otherwise.")]
        [SerializeField] private Image border;
        [Tooltip("Border sprites: the 2 px band (ready / in progress) and the 1 px band.")]
        [SerializeField] private Sprite borderStrong;
        [SerializeField] private Sprite borderQuiet;
        [Tooltip("The step numeral over the art, \"01\".")]
        [SerializeField] private KitText numeralText;
        [Tooltip("The state line over the name.")]
        [SerializeField] private KitText stateText;
        [Tooltip("The quest's name (also the base nameText's KitText, for its colour).")]
        [SerializeField] private KitText titleText;
        [Tooltip("The card art: dimmed while locked.")]
        [SerializeField] private Graphic art;
        [Tooltip("The lock glyph over the art, shown while locked.")]
        [SerializeField] private CanvasGroup lockGroup;
        [Tooltip("The \"Completed\" chip over the art, shown once claimed.")]
        [SerializeField] private CanvasGroup completedChip;

        [Header("Goals")]
        [Tooltip("The goals block (title + rows); hidden on the hangar quest, which has none.")]
        [SerializeField] private CanvasGroup goalsGroup;
        [SerializeField] private GoalRow[] goals = new GoalRow[2];

        [Header("Footer")]
        [Tooltip("The Claim button's group (ready to claim).")]
        [SerializeField] private CanvasGroup claimGroup;
        [Tooltip("The meter + Play button's group (in progress).")]
        [SerializeField] private CanvasGroup playGroup;
        [Tooltip("The four meter bars, shortest first.")]
        [SerializeField] private Image[] meterBars = { };
        [Tooltip("\"Intensity 2 of 4\".")]
        [SerializeField] private KitText meterText;
        [Tooltip("The Play button's label, \"Play Joust\".")]
        [SerializeField] private KitText playLabel;
        [Tooltip("\"Unlocks after JOUST\" (locked).")]
        [SerializeField] private CanvasGroup lockedLineGroup;
        [SerializeField] private KitText lockedLineText;

        [Header("Track")]
        [Tooltip("This card's node on the quest track: filled once reached, outlined otherwise.")]
        [SerializeField] private Image node;
        [SerializeField] private Sprite nodeFilled;
        [SerializeField] private Sprite nodeOutline;
        [Tooltip("The segment from the previous card's node to this one; absent on the first card.")]
        [SerializeField] private Image segment;
        [Tooltip("The travelling spark on the segment, shown on the frontier card only (ambient).")]
        [SerializeField] private CanvasGroup segmentSpark;
        [Tooltip("The ceremony pop played on claim. Optional.")]
        [SerializeField] private KitEntrance claimPop;

        private const int MaxIntensity = 4;
        private const float LockedArtAlpha = 0.35f;
        private int _index;
        private int _maxUnlocked;
        private string _unlocksAfter;
        private bool _ready;

        private Color Tok(UIColorToken t, float a = 1f)
        {
            var c = UITheme.Resolve(theme, t);
            c.a *= a;
            return c;
        }

        protected override void OnConfigured(SO_UnlockData quest)
        {
            if (playLabel) playLabel.SetText(TemplateText.Fill(playLine, "quest", quest.DisplayName));
        }

        public override void SetContext(int index, int maxUnlockedIntensity, string unlocksAfter)
        {
            _index = index;
            _maxUnlocked = Mathf.Clamp(maxUnlockedIntensity, 0, MaxIntensity);
            _unlocksAfter = unlocksAfter;
            if (numeralText) numeralText.SetText((index + 1).ToString("00"));
            Redraw();
        }

        protected override void DrawState(QuestItemState state, bool readyToClaim)
        {
            _ready = readyToClaim;
            Redraw();
        }

        public override void SetActiveFrontier(bool isActive, QuestItemState state = QuestItemState.Unlocked)
        {
            KitVisibility.SetShown(segmentSpark, isActive && _index > 0);
        }

        public override void PlayClaimAnimation(Action onComplete)
        {
            if (claimPop) claimPop.Play();
            base.PlayClaimAnimation(onComplete);
        }

        private void Redraw()
        {
            var state = State;
            bool locked = state == QuestItemState.Locked;
            bool claimed = state == QuestItemState.Claimed;
            bool ready = !locked && !claimed && _ready;
            bool progress = !locked && !claimed && !ready;
            bool hasGoals = Quest && Quest.FeatureKind == FeatureKind.GameMode;

            if (stateText)
            {
                stateText.SetText(claimed ? claimedLine : ready ? readyLine : progress ? inProgressLine : lockedLine);
                stateText.SetColorToken(ready ? UIColorToken.Cta : UIColorToken.NeutralLightest);
            }
            if (titleText) titleText.SetColorToken(locked ? UIColorToken.NeutralLightest : UIColorToken.TextLight);
            if (border)
            {
                var sprite = ready || progress ? borderStrong : borderQuiet;
                if (sprite) border.sprite = sprite;
                border.color = Tok(ready ? UIColorToken.Cta : progress ? UIColorToken.TextLight : UIColorToken.SurfaceDark);
            }
            if (art) art.color = new Color(1f, 1f, 1f, locked ? LockedArtAlpha : 1f);
            KitVisibility.SetShown(lockGroup, locked);
            KitVisibility.SetShown(completedChip, claimed);

            KitVisibility.SetShown(goalsGroup, hasGoals);
            if (hasGoals) DrawGoals(locked, claimed);

            KitVisibility.SetShown(claimGroup, ready);
            KitVisibility.SetShown(playGroup, progress && hasGoals);
            KitVisibility.SetShown(lockedLineGroup, locked && !string.IsNullOrEmpty(_unlocksAfter));
            if (lockedLineText && !string.IsNullOrEmpty(_unlocksAfter))
                lockedLineText.SetText(TemplateText.Fill(unlocksAfterLine, "after", _unlocksAfter));

            if (meterText) meterText.SetText(TemplateText.Fill(meterLine, "level", Mathf.Max(1, _maxUnlocked).ToString(), "max", MaxIntensity.ToString()));
            for (int i = 0; i < meterBars.Length; i++)
                if (meterBars[i])
                    meterBars[i].color = Tok(i < _maxUnlocked ? UIColorToken.TextLight : UIColorToken.SurfaceDark);

            // The track: a reached node is Control Active - filled once its quest is ready or
            // claimed, outlined while it is being played; a locked node is a faint outline.
            if (node)
            {
                var sprite = claimed || ready ? nodeFilled : nodeOutline;
                if (sprite) node.sprite = sprite;
                node.color = Tok(locked ? UIColorToken.SurfaceLight : UIColorToken.ControlActive);
            }
            if (segment) segment.color = Tok(locked ? UIColorToken.SurfaceDark : UIColorToken.ControlActive);
        }

        // Done = the mode has already reached that intensity; next = the first goal not done on a
        // card being played; to-do = everything else (Docs/MENU_REDESIGN_B.md section 15).
        private void DrawGoals(bool locked, bool claimed)
        {
            bool nextTaken = false;
            for (int i = 0; i < goals.Length; i++)
            {
                var row = goals[i];
                if (row == null) continue;
                int level = 3 + i;
                bool done = claimed || _maxUnlocked >= level;
                bool next = !done && !locked && !nextTaken;
                if (next) nextTaken = true;
                KitVisibility.SetShown(row.group, true);
                if (row.doneMark) row.doneMark.enabled = done;
                if (row.pendingMark)
                {
                    row.pendingMark.enabled = !done;
                    row.pendingMark.color = Tok(next ? UIColorToken.TextLight : UIColorToken.NeutralLightest, next ? 1f : 0.6f);
                }
                if (row.caption)
                {
                    row.caption.SetText(TemplateText.Fill(goalLine, "level", level.ToString()));
                    row.caption.SetColorToken(next ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
                }
                if (row.description)
                {
                    row.description.SetText(level == 3 ? Quest.Intensity3GoalDescription : Quest.Intensity4GoalDescription);
                    row.description.SetColorToken(next || done ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
                }
            }
        }
    }
}

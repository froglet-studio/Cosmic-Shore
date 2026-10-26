using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The scoreboard's Rev 23 D "team versus" layout (Docs/MENU_REDESIGN_B.md §76; Shombith, 2026-10-26: "For scoreboard
    /// I liked D"): two teams face each other - the winner on the left - each with its name in its colour, WINNER under
    /// the winning one, its score, and its pilots below; a bar across the top splits the count the teams shared (the
    /// crystals in a Skim Race), and VS sits between them.
    ///
    /// <para>Shown instead of the ladder only when <see cref="ScoreboardLadder.BuildVersus"/> says the match was two teams
    /// (2v2, 3v1, 3v3 ...); a free-for-all, a 1v1 or three teams keep the ladder. <see cref="Scoreboard"/> spawns the rows
    /// into <see cref="Rows"/>; this view draws the heads and the bar.</para>
    /// </summary>
    public class ScoreboardVersusView : MonoBehaviour
    {
        [System.Serializable]
        public class TeamSide
        {
            public TMP_Text nameText;
            [Tooltip("WINNER under the winning team; empty under the other.")]
            public TMP_Text statusText;
            [Tooltip("The team's score: the time every member shares, or the members' scores added.")]
            public TMP_Text scoreText;
            [Tooltip("The rule under the head, in the team's colour.")]
            public Image rule;
            [Tooltip("Where Scoreboard spawns this team's rows (a vertical layout group).")]
            public Transform rows;
            [Tooltip("This team's part of the share bar.")]
            public Image shareFill;
            [Tooltip("The line under the bar on this team's side; {domain} and {percent} are filled in.")]
            public TMP_Text shareText;
        }

        [Tooltip("This layout; shown for a two-team match.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("The ladder's list; hidden while the versus layout shows.")]
        [SerializeField] private CanvasGroup ladderGroup;
        [Tooltip("Left (the winner), then right.")]
        [SerializeField] private TeamSide[] sides = new TeamSide[2];
        [Tooltip("The share bar and its two lines; hidden when the count cannot be split.")]
        [SerializeField] private CanvasGroup shareGroup;
        [Tooltip("Units of sea between the two parts of the share bar.")]
        [SerializeField, Min(0f)] private float shareGap = 4f;
        [Tooltip("The loser's part of the bar, as a fraction of its colour's alpha.")]
        [SerializeField, Range(0f, 1f)] private float loserShareAlpha = 0.6f;

        [Header("Words")]
        [SerializeField] private string winnerWord = "Winner";
        [Tooltip("Under the losing team's name. Empty shows nothing.")]
        [SerializeField] private string loserWord = "";
        [Tooltip("Under the share bar; {domain} and {percent} are filled in.")]
        [SerializeField] private string shareTemplate = "{domain} {percent}%";

        /// <summary>Wired: two sides, each with somewhere to put its rows.</summary>
        public bool IsAvailable => sides is { Length: 2 } && sides[0]?.rows && sides[1]?.rows;

        public Transform Rows(int side) => sides[side].rows;

        /// <summary>The versus layout on (and the ladder off), or the reverse.</summary>
        public void SetShown(bool shown)
        {
            KitVisibility.SetShown(group, shown);
            KitVisibility.SetShown(ladderGroup, !shown);
        }

        public void BindSide(int index, string domain, Color color, bool won, string score)
        {
            var side = sides[index];
            if (side.nameText)
            {
                side.nameText.text = domain.ToUpperInvariant();
                side.nameText.color = color;
            }
            if (side.statusText) side.statusText.text = won ? winnerWord : loserWord;
            if (side.scoreText) side.scoreText.text = score ?? string.Empty;
            if (side.rule) side.rule.color = color;
            if (side.shareFill) side.shareFill.color = new Color(color.r, color.g, color.b, color.a * (won ? 1f : loserShareAlpha));
            if (side.shareText) side.shareText.text = domain;
        }

        /// <summary>The bar split at <paramref name="leftShare"/> (0-1); a negative share hides it.</summary>
        public void SetShare(float leftShare, string leftDomain, string rightDomain)
        {
            bool shown = leftShare >= 0f;
            KitVisibility.SetShown(shareGroup, shown);
            if (!shown) return;
            leftShare = Mathf.Clamp01(leftShare);
            Place(sides[0].shareFill, 0f, leftShare, 0f, -shareGap / 2f);
            Place(sides[1].shareFill, leftShare, 1f, shareGap / 2f, 0f);
            int left = Mathf.RoundToInt(leftShare * 100f);
            if (sides[0].shareText)
                sides[0].shareText.text = TemplateText.Fill(shareTemplate, "domain", leftDomain, "percent", left.ToString());
            if (sides[1].shareText)
                sides[1].shareText.text = TemplateText.Fill(shareTemplate, "domain", rightDomain, "percent", (100 - left).ToString());
        }

        static void Place(Image fill, float from, float to, float inLeft, float inRight)
        {
            if (!fill) return;
            var rt = fill.rectTransform;
            rt.anchorMin = new Vector2(from, rt.anchorMin.y);
            rt.anchorMax = new Vector2(to, rt.anchorMax.y);
            rt.offsetMin = new Vector2(inLeft, rt.offsetMin.y);
            rt.offsetMax = new Vector2(inRight, rt.offsetMax.y);
        }
    }
}

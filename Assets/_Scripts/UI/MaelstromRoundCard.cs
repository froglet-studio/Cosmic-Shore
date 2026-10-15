using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Maelstrom Data Card</b> - one round in the Maelstrom scroll. Shows the round header
    /// (the round number, the mode, the winning domain) and instantiates one <b>Player Data Card</b>
    /// (<see cref="MaelstromPlayerCard"/>) per player who finished that round, with their Round Score
    /// and Total Score. The card background tints to the winning domain via the colour resolver the
    /// view passes in (the theme's per-domain UI accent); the winning-domain name is coloured to match.
    ///
    /// Two setup paths: <see cref="Setup"/> (a completed round) and <see cref="SetupPreview"/> (the
    /// round about to be played - roster only, no round score, "Up next" in place of a winner).
    ///
    /// <para>Style B (Docs/MENU_REDESIGN_B.md section 27): a "Round n" eyebrow over the mode's name on
    /// the mode's own card art, the winner as a domain pill, and ROUND / TOTAL column heads over the
    /// rows. The round about to be played carries the Control Active outline
    /// (<see cref="currentRoundHighlight"/>).</para>
    /// </summary>
    public class MaelstromRoundCard : MonoBehaviour
    {
        [Header("Round header")]
        [Tooltip("\"Round 1\" (the eyebrow).")]
        [SerializeField] TMP_Text roundNumberText;
        [Tooltip("The mode's name (\"To be drawn\" before the draw lands).")]
        [SerializeField] TMP_Text roundNameText;
        [Tooltip("The winning domain's name, in its colour.")]
        [SerializeField] TMP_Text winningDomainText;
        [Tooltip("Optional accent for the most-recently-played round (the auto-scroll target).")]
        [SerializeField] GameObject currentRoundHighlight;

        [Header("Domain colour")]
        [Tooltip("The card background image that tints to the winning domain colour.")]
        [SerializeField] Image cardBackground;

        [Header("Style B (optional)")]
        [Tooltip("The mode's card art across the top of the card.")]
        [SerializeField] Image cardArt;
        [Tooltip("Alpha of the art while the round is still to be played.")]
        [SerializeField, Range(0f, 1f)] float upcomingArtAlpha = 0.45f;
        [Tooltip("The winner pill (plate, mark, name = winningDomainText). Hidden with no winner.")]
        [SerializeField] CanvasGroup winnerPill;
        [SerializeField] Image winnerPillBackground;
        [SerializeField] Image winnerPillMark;
        [SerializeField, Range(0f, 1f)] float pillAlpha = 0.14f;
        [Tooltip("\"Up next\" / \"-\" in the pill's place.")]
        [SerializeField] TMP_Text winnerPlaceholderText;

        [Header("Player Data Cards")]
        [SerializeField] MaelstromPlayerCard playerCardPrefab;
        [SerializeField] Transform playerCardContainer;

        readonly List<MaelstromPlayerCard> _spawned = new();

        // Domain → colour resolver handed in by the view (theme UI accent); grey when absent.
        Func<Domains, Color> _colorOf;

        public void Setup(MaelstromRoundRecord record, Func<MaelstromPlayerSnapshot, Sprite> avatarOf,
                          Func<Domains, int> totalOf, Func<Domains, Color> colorOf, bool isCurrent = false,
                          Func<Domains, int> pointsOf = null, Sprite art = null)
        {
            if (record == null) return;
            _colorOf = colorOf;
            SetHeader(record.RoundNumber, record.ModeDisplayName, record.WinningDomain, showWinner: true, upcoming: false);
            SetArt(art, upcoming: false);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(isCurrent);
            BuildPlayers(record.Players, avatarOf, totalOf, showRoundScore: true, pointsOf);
        }

        public void SetupPreview(int roundNumber, IReadOnlyList<MaelstromPlayerSnapshot> roster,
                                 Func<MaelstromPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf,
                                 Func<Domains, Color> colorOf, string modeName = null, Sprite art = null)
        {
            _colorOf = colorOf;
            _previewRound = roundNumber;
            SetHeader(roundNumber, modeName, winner: Domains.Blue, showWinner: false, upcoming: true);
            SetArt(art, upcoming: true);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(true);
            BuildPlayers(roster, avatarOf, totalOf, showRoundScore: false, null);
        }

        /// <summary>The draw landed: name the round about to be played and show its art.</summary>
        public void SetUpcomingRound(string modeName, Sprite art)
        {
            SetHeader(_previewRound, modeName, Domains.Blue, showWinner: false, upcoming: true);
            SetArt(art, upcoming: true);
        }

        int _previewRound = 1;

        void SetArt(Sprite art, bool upcoming)
        {
            if (!cardArt) return;
            cardArt.sprite = art;
            cardArt.enabled = art != null;
            cardArt.color = new Color(1f, 1f, 1f, upcoming ? upcomingArtAlpha : 1f);
        }

        Color DomainColor(Domains domain) => _colorOf != null ? _colorOf(domain) : Color.gray;

        void SetHeader(int roundNumber, string modeName, Domains winner, bool showWinner, bool upcoming)
        {
            if (roundNumberText) roundNumberText.text = $"Round {roundNumber}";
            if (roundNameText)
                roundNameText.text = !string.IsNullOrEmpty(modeName) ? modeName : upcoming ? "To be drawn" : "-";

            bool hasWinner = showWinner && winner != Domains.Blue;
            Color wc = DomainColor(hasWinner ? winner : Domains.Blue);

            if (winningDomainText)
            {
                winningDomainText.text = hasWinner ? winner.ToString().ToUpperInvariant() : string.Empty;
                winningDomainText.color = wc;
            }
            KitVisibility.SetShown(winnerPill, hasWinner);
            if (winnerPillMark) winnerPillMark.color = wc;
            if (winnerPillBackground) winnerPillBackground.color = new Color(wc.r, wc.g, wc.b, pillAlpha);
            if (winnerPlaceholderText) winnerPlaceholderText.text = hasWinner ? string.Empty : upcoming ? "Up next" : "-";

            if (cardBackground) cardBackground.color = wc;
        }

        void BuildPlayers(IReadOnlyList<MaelstromPlayerSnapshot> players,
                          Func<MaelstromPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf, bool showRoundScore,
                          Func<Domains, int> pointsOf)
        {
            Clear();
            if (players == null || !playerCardPrefab || !playerCardContainer) return;

            // Order rows by cumulative Total Score, highest first, so the leading domain is always on
            // top - matching the summary panel and the spec ("reorder by the overall leader"). Without
            // this the rows kept the round's finishing order, so the round where the round-winner was
            // NOT the overall leader (e.g. the deciding round) read as mis-ordered against the totals it
            // shows. OrderByDescending/ThenBy is stable, so same-domain players keep their round rank;
            // in the round-0 preview every total is 0, so it falls back to domain enum order.
            var ordered = totalOf == null
                ? players
                : players.OrderByDescending(p => totalOf(p.Domain)).ThenBy(p => (int)p.Domain).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var s = ordered[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);
                string round = showRoundScore ? (s.ScoreText ?? string.Empty) : string.Empty;
                string total = totalOf != null ? totalOf(s.Domain).ToString() : string.Empty;
                card.Setup(s.Name, avatarOf != null ? avatarOf(s) : null, s.Domain, round, total, DomainColor(s.Domain));
                card.SetPoints(pointsOf != null ? pointsOf(s.Domain) : -1);
                _spawned.Add(card);
            }
        }

        void Clear()
        {
            foreach (var c in _spawned) if (c) Destroy(c.gameObject);
            _spawned.Clear();

            if (playerCardContainer)
                for (int i = playerCardContainer.childCount - 1; i >= 0; i--)
                    Destroy(playerCardContainer.GetChild(i).gameObject);
        }
    }
}

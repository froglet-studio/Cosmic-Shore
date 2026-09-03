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
    /// (ROUND INDEX, ROUND NAME, WINNING DOMAIN) and instantiates one <b>Player Data Card</b>
    /// (<see cref="MaelstromPlayerCard"/>) per player who finished that round, with their Round Score
    /// and Total Score. The card background tints to the winning domain via the colour resolver the
    /// view passes in (the theme's per-domain UI accent); the winning-domain name is coloured to match.
    ///
    /// Two setup paths: <see cref="Setup"/> (a completed round) and <see cref="SetupPreview"/> (the
    /// round-0 lobby preview - roster only, no round score, winner "-").
    /// </summary>
    public class MaelstromRoundCard : MonoBehaviour
    {
        [Header("Round header")]
        [Tooltip("\"ROUND INDEX : 1\".")]
        [SerializeField] TMP_Text roundNumberText;
        [Tooltip("\"ROUND NAME : SCRUM\" (the mode; \"-\" in the preview).")]
        [SerializeField] TMP_Text roundNameText;
        [Tooltip("\"WINNING DOMAIN : JADE\" (domain name coloured).")]
        [SerializeField] TMP_Text winningDomainText;
        [Tooltip("Optional accent for the most-recently-played round (the auto-scroll target).")]
        [SerializeField] GameObject currentRoundHighlight;

        [Header("Domain colour")]
        [Tooltip("The card background image that tints to the winning domain colour.")]
        [SerializeField] Image cardBackground;

        [Header("Player Data Cards")]
        [SerializeField] MaelstromPlayerCard playerCardPrefab;
        [SerializeField] Transform playerCardContainer;

        readonly List<MaelstromPlayerCard> _spawned = new();

        // Domain → colour resolver handed in by the view (theme UI accent); grey when absent.
        Func<Domains, Color> _colorOf;

        public void Setup(MaelstromRoundRecord record, Func<MaelstromPlayerSnapshot, Sprite> avatarOf,
                          Func<Domains, int> totalOf, Func<Domains, Color> colorOf, bool isCurrent = false)
        {
            if (record == null) return;
            _colorOf = colorOf;
            SetHeader(record.RoundNumber, record.ModeDisplayName, record.WinningDomain, showWinner: true);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(isCurrent);
            BuildPlayers(record.Players, avatarOf, totalOf, showRoundScore: true);
        }

        public void SetupPreview(int roundNumber, IReadOnlyList<MaelstromPlayerSnapshot> roster,
                                 Func<MaelstromPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf,
                                 Func<Domains, Color> colorOf)
        {
            _colorOf = colorOf;
            SetHeader(roundNumber, modeName: null, winner: Domains.Blue, showWinner: false);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(true);
            BuildPlayers(roster, avatarOf, totalOf, showRoundScore: false);
        }

        Color DomainColor(Domains domain) => _colorOf != null ? _colorOf(domain) : Color.gray;

        void SetHeader(int roundNumber, string modeName, Domains winner, bool showWinner)
        {
            if (roundNumberText) roundNumberText.text = $"ROUND INDEX : {roundNumber}";
            if (roundNameText)
                roundNameText.text = $"ROUND NAME : {(string.IsNullOrEmpty(modeName) ? "-" : modeName.ToUpperInvariant())}";

            bool hasWinner = showWinner && winner != Domains.Blue;
            Color wc = DomainColor(hasWinner ? winner : Domains.Blue);

            if (winningDomainText)
                winningDomainText.text = hasWinner
                    ? $"WINNING DOMAIN : <color=#{ColorUtility.ToHtmlStringRGB(wc)}>{winner.ToString().ToUpperInvariant()}</color>"
                    : "WINNING DOMAIN : -";

            if (cardBackground) cardBackground.color = wc;
        }

        void BuildPlayers(IReadOnlyList<MaelstromPlayerSnapshot> players,
                          Func<MaelstromPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf, bool showRoundScore)
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

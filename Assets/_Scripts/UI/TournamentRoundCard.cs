using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Tournament Data Card</b> — one round in the Maelstrom scroll. Shows the round header
    /// (ROUND INDEX, ROUND NAME, WINNING DOMAIN) and instantiates one <b>Player Data Card</b>
    /// (<see cref="TournamentPlayerCard"/>) per player who finished that round, with their Round Score
    /// and Total Score. The card background tints to the winning domain via the
    /// <see cref="DomainColorPaletteSO"/>; the winning-domain name is coloured to match.
    ///
    /// Two setup paths: <see cref="Setup"/> (a completed round) and <see cref="SetupPreview"/> (the
    /// round-0 lobby preview — roster only, no round score, winner "—").
    /// </summary>
    public class TournamentRoundCard : MonoBehaviour
    {
        [Header("Round header")]
        [Tooltip("\"ROUND INDEX : 1\".")]
        [SerializeField] TMP_Text roundNumberText;
        [Tooltip("\"ROUND NAME : SCRUM\" (the mode; \"—\" in the preview).")]
        [SerializeField] TMP_Text roundNameText;
        [Tooltip("\"WINNING DOMAIN : JADE\" (domain name coloured).")]
        [SerializeField] TMP_Text winningDomainText;
        [Tooltip("Optional accent for the most-recently-played round (the auto-scroll target).")]
        [SerializeField] GameObject currentRoundHighlight;

        [Header("Domain colour")]
        [Tooltip("The card background image that tints to the winning domain colour.")]
        [SerializeField] Image cardBackground;
        [Tooltip("Palette mapping Domain → colour (Assets/_SO_Assets/DomainColorPalette.asset).")]
        [SerializeField] DomainColorPaletteSO palette;

        [Header("Player Data Cards")]
        [SerializeField] TournamentPlayerCard playerCardPrefab;
        [SerializeField] Transform playerCardContainer;

        readonly List<TournamentPlayerCard> _spawned = new();

        public void Setup(TournamentRoundRecord record, Func<TournamentPlayerSnapshot, Sprite> avatarOf,
                          Func<Domains, int> totalOf, bool isCurrent = false)
        {
            if (record == null) return;
            SetHeader(record.RoundNumber, record.ModeDisplayName, record.WinningDomain, showWinner: true);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(isCurrent);
            BuildPlayers(record.Players, avatarOf, totalOf, showRoundScore: true);
        }

        public void SetupPreview(int roundNumber, IReadOnlyList<TournamentPlayerSnapshot> roster,
                                 Func<TournamentPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf)
        {
            SetHeader(roundNumber, modeName: null, winner: Domains.Blue, showWinner: false);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(true);
            BuildPlayers(roster, avatarOf, totalOf, showRoundScore: false);
        }

        void SetHeader(int roundNumber, string modeName, Domains winner, bool showWinner)
        {
            if (roundNumberText) roundNumberText.text = $"ROUND INDEX : {roundNumber}";
            if (roundNameText)
                roundNameText.text = $"ROUND NAME : {(string.IsNullOrEmpty(modeName) ? "—" : modeName.ToUpperInvariant())}";

            bool hasWinner = showWinner && winner != Domains.Blue;
            Color wc = palette ? palette.Get(hasWinner ? winner : Domains.Blue) : Color.gray;

            if (winningDomainText)
                winningDomainText.text = hasWinner
                    ? $"WINNING DOMAIN : <color=#{ColorUtility.ToHtmlStringRGB(wc)}>{winner.ToString().ToUpperInvariant()}</color>"
                    : "WINNING DOMAIN : —";

            if (cardBackground && palette) cardBackground.color = wc;
        }

        void BuildPlayers(IReadOnlyList<TournamentPlayerSnapshot> players,
                          Func<TournamentPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf, bool showRoundScore)
        {
            Clear();
            if (players == null || !playerCardPrefab || !playerCardContainer) return;

            for (int i = 0; i < players.Count; i++)
            {
                var s = players[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);
                string round = showRoundScore ? (s.ScoreText ?? string.Empty) : string.Empty;
                string total = totalOf != null ? totalOf(s.Domain).ToString() : string.Empty;
                card.Setup(s.Name, avatarOf != null ? avatarOf(s) : null, s.Domain, round, total);
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

using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Tournament Data Card</b> — one round in the Maelstrom scroll. Shows the round header (mode
    /// name + winning domain) and instantiates one <b>Player Data Card</b> (<see cref="PlayerScoreCard"/>)
    /// per player who finished that round, tinted to each player's domain.
    ///
    /// Two setup paths:
    ///   • <see cref="Setup"/> — a completed round (mode + winning domain + per-player scores).
    ///   • <see cref="SetupPreview"/> — the round-0 lobby preview (the upcoming roster, no scores, no
    ///     winner) so the first intro isn't empty.
    ///
    /// Pure view: the caller passes domain→colour and snapshot→avatar resolvers so the card stays
    /// decoupled from the theme / profile systems.
    /// </summary>
    public class TournamentRoundCard : MonoBehaviour
    {
        [Header("Round header")]
        [Tooltip("Mode that was played, e.g. \"Hex Race\" (\"UP NEXT\" in the round-0 preview).")]
        [SerializeField] TMP_Text roundNameText;
        [Tooltip("Optional \"ROUND 3\" label.")]
        [SerializeField] TMP_Text roundNumberText;
        [Tooltip("\"WINNING DOMAIN : JADE\".")]
        [SerializeField] TMP_Text winningDomainText;
        [Tooltip("Graphics tinted to the winning domain's colour (header accent, border, …).")]
        [SerializeField] Graphic[] winnerColorTargets;
        [Tooltip("Optional root for the winning-domain block — hidden in the preview (no winner yet).")]
        [SerializeField] GameObject winningDomainRoot;
        [Tooltip("Optional accent for the most-recently-played round (the auto-scroll target).")]
        [SerializeField] GameObject currentRoundHighlight;

        [Header("Player Data Cards")]
        [SerializeField] PlayerScoreCard playerCardPrefab;
        [SerializeField] Transform playerCardContainer;

        readonly List<PlayerScoreCard> _spawned = new();

        public void Setup(TournamentRoundRecord record, Func<Domains, Color> colorOf,
                          Func<TournamentPlayerSnapshot, Sprite> avatarOf, bool isCurrent = false)
        {
            if (record == null) return;
            SetHeader(record.RoundNumber, record.ModeDisplayName, record.WinningDomain, colorOf, showWinner: true);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(isCurrent);
            BuildPlayers(record.Players, colorOf, avatarOf, showScores: true);
        }

        public void SetupPreview(int roundNumber, IReadOnlyList<TournamentPlayerSnapshot> roster,
                                 Func<Domains, Color> colorOf, Func<TournamentPlayerSnapshot, Sprite> avatarOf)
        {
            SetHeader(roundNumber, modeName: null, winner: Domains.Blue, colorOf, showWinner: false);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(true);
            BuildPlayers(roster, colorOf, avatarOf, showScores: false);
        }

        void SetHeader(int roundNumber, string modeName, Domains winner, Func<Domains, Color> colorOf, bool showWinner)
        {
            if (roundNumberText) roundNumberText.text = $"ROUND {roundNumber}";
            if (roundNameText) roundNameText.text = string.IsNullOrEmpty(modeName) ? "UP NEXT" : modeName;

            if (winningDomainRoot) winningDomainRoot.SetActive(showWinner && winner != Domains.Blue);
            if (winningDomainText)
                winningDomainText.text = (showWinner && winner != Domains.Blue)
                    ? $"WINNING DOMAIN : {winner.ToString().ToUpperInvariant()}"
                    : string.Empty;

            if (winnerColorTargets != null && colorOf != null)
            {
                var c = colorOf(winner);
                foreach (var g in winnerColorTargets) if (g) g.color = c;
            }
        }

        void BuildPlayers(IReadOnlyList<TournamentPlayerSnapshot> players, Func<Domains, Color> colorOf,
                          Func<TournamentPlayerSnapshot, Sprite> avatarOf, bool showScores)
        {
            Clear();
            if (players == null || !playerCardPrefab || !playerCardContainer) return;

            for (int i = 0; i < players.Count; i++)
            {
                var s = players[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);
                string score = showScores ? (s.ScoreText ?? string.Empty) : string.Empty;
                card.Setup(s.Name, score, colorOf != null ? colorOf(s.Domain) : Color.gray, i);
                if (avatarOf != null) card.SetAvatar(avatarOf(s));
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

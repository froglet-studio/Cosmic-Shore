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
    /// name + winning domain) and instantiates one <b>Player Data Card</b> (<see cref="TournamentPlayerCard"/>)
    /// per player who finished that round, tinted to each player's domain, with their Round Score
    /// (that round's result) and Total Score (their domain's cumulative tournament points, as-of that round).
    ///
    /// Two setup paths:
    ///   • <see cref="Setup"/> — a completed round (mode + winning domain + per-player round/total scores).
    ///   • <see cref="SetupPreview"/> — the round-0 lobby preview (the upcoming roster, no round score, no
    ///     winner) so the first intro isn't empty.
    ///
    /// Pure view: the caller passes domain→colour, snapshot→avatar, and domain→total resolvers so the card
    /// stays decoupled from the theme / profile / standings systems.
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
        [SerializeField] TournamentPlayerCard playerCardPrefab;
        [SerializeField] Transform playerCardContainer;

        readonly List<TournamentPlayerCard> _spawned = new();

        public void Setup(TournamentRoundRecord record, Func<Domains, Color> colorOf,
                          Func<TournamentPlayerSnapshot, Sprite> avatarOf, Func<Domains, int> totalOf,
                          bool isCurrent = false)
        {
            if (record == null) return;
            SetHeader(record.RoundNumber, record.ModeDisplayName, record.WinningDomain, colorOf, showWinner: true);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(isCurrent);
            BuildPlayers(record.Players, colorOf, avatarOf, totalOf, showRoundScore: true);
        }

        public void SetupPreview(int roundNumber, IReadOnlyList<TournamentPlayerSnapshot> roster,
                                 Func<Domains, Color> colorOf, Func<TournamentPlayerSnapshot, Sprite> avatarOf,
                                 Func<Domains, int> totalOf)
        {
            SetHeader(roundNumber, modeName: null, winner: Domains.Blue, colorOf, showWinner: false);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(true);
            BuildPlayers(roster, colorOf, avatarOf, totalOf, showRoundScore: false);
        }

        void SetHeader(int roundNumber, string modeName, Domains winner, Func<Domains, Color> colorOf, bool showWinner)
        {
            if (roundNumberText) roundNumberText.text = $"ROUND {roundNumber}";
            if (roundNameText) roundNameText.text = string.IsNullOrEmpty(modeName) ? "UP NEXT" : modeName;

            // The winning-domain block stays visible; the preview / undecided round shows a clean "—"
            // placeholder (tinted neutral) rather than disappearing. winningDomainRoot can still be left
            // unassigned to hide the block entirely.
            bool hasWinner = showWinner && winner != Domains.Blue;
            if (winningDomainRoot) winningDomainRoot.SetActive(true);
            if (winningDomainText)
                winningDomainText.text = hasWinner
                    ? $"WINNING DOMAIN : {winner.ToString().ToUpperInvariant()}"
                    : "WINNING DOMAIN : —";

            if (winnerColorTargets != null && colorOf != null)
            {
                var c = colorOf(hasWinner ? winner : Domains.Blue);
                foreach (var g in winnerColorTargets) if (g) g.color = c;
            }
        }

        void BuildPlayers(IReadOnlyList<TournamentPlayerSnapshot> players, Func<Domains, Color> colorOf,
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
                card.Setup(s.Name,
                           avatarOf != null ? avatarOf(s) : null,
                           colorOf != null ? colorOf(s.Domain) : Color.gray,
                           round, total);
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

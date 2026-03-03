using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Displays cumulative tournament standings between rounds and at tournament end.
    /// Includes vessel selection for the next round.
    ///
    /// Subscribes to tournament SOAP events via <see cref="TournamentEventsContainerSO"/>.
    /// Placed in game scenes (or instantiated as a prefab) alongside the Scoreboard.
    /// </summary>
    public class TournamentStandingsPanel : MonoBehaviour
    {
        [Header("Data")]
        [Inject] TournamentDataSO tournamentData;
        [Inject] TournamentEventsContainerSO tournamentEvents;
        [Inject] TournamentManager tournamentManager;
        [Inject] GameDataSO gameData;

        [Header("Panel Root")]
        [SerializeField] GameObject panelRoot;

        [Header("Header")]
        [SerializeField] TMP_Text headerText;
        [SerializeField] TMP_Text roundIndicatorText;

        [Header("Standings Table")]
        [SerializeField] Transform standingsContainer;
        [SerializeField] TournamentStandingRowUI standingRowPrefab;

        [Header("Vessel Selection")]
        [SerializeField] GameObject vesselSelectionSection;
        [SerializeField] List<ShipCardView> vesselCards;

        [Header("Buttons")]
        [SerializeField] Button nextRoundButton;
        [SerializeField] TMP_Text nextRoundButtonText;
        [SerializeField] Button quitTournamentButton;

        VesselClassType _selectedVessel;
        readonly List<TournamentStandingRowUI> _spawnedRows = new();

        void Awake()
        {
            if (panelRoot) panelRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (tournamentEvents?.OnTournamentRoundCaptured != null)
                tournamentEvents.OnTournamentRoundCaptured.OnRaised += ShowBetweenRounds;

            if (tournamentEvents?.OnTournamentComplete != null)
                tournamentEvents.OnTournamentComplete.OnRaised += ShowFinalResults;

            if (tournamentEvents?.OnTournamentAdvancing != null)
                tournamentEvents.OnTournamentAdvancing.OnRaised += HidePanel;

            if (tournamentEvents?.OnTournamentEnded != null)
                tournamentEvents.OnTournamentEnded.OnRaised += HidePanel;

            if (nextRoundButton) nextRoundButton.onClick.AddListener(OnNextRoundPressed);
            if (quitTournamentButton) quitTournamentButton.onClick.AddListener(OnQuitTournamentPressed);
        }

        void OnDisable()
        {
            if (tournamentEvents?.OnTournamentRoundCaptured != null)
                tournamentEvents.OnTournamentRoundCaptured.OnRaised -= ShowBetweenRounds;

            if (tournamentEvents?.OnTournamentComplete != null)
                tournamentEvents.OnTournamentComplete.OnRaised -= ShowFinalResults;

            if (tournamentEvents?.OnTournamentAdvancing != null)
                tournamentEvents.OnTournamentAdvancing.OnRaised -= HidePanel;

            if (tournamentEvents?.OnTournamentEnded != null)
                tournamentEvents.OnTournamentEnded.OnRaised -= HidePanel;

            if (nextRoundButton) nextRoundButton.onClick.RemoveListener(OnNextRoundPressed);
            if (quitTournamentButton) quitTournamentButton.onClick.RemoveListener(OnQuitTournamentPressed);
        }

        void ShowBetweenRounds()
        {
            if (tournamentData == null || !tournamentData.IsTournamentActive) return;

            PopulateStandings();

            if (headerText)
            {
                var lastRound = tournamentData.CompletedRounds[^1];
                headerText.text = $"Round {lastRound.RoundIndex + 1} Complete: {lastRound.GameDisplayName}";
            }

            // Show next round info
            bool isLast = tournamentData.IsLastRound;
            if (roundIndicatorText)
            {
                if (isLast)
                    roundIndicatorText.text = "Final Round Complete!";
                else
                {
                    var nextRound = tournamentData.ActiveTournament.Rounds[tournamentData.CurrentRoundIndex + 1];
                    roundIndicatorText.text = $"Next: {nextRound.DisplayName} (Round {tournamentData.CurrentRoundIndex + 2} of {tournamentData.TotalRounds})";
                }
            }

            // Vessel selection visible between rounds (not after final)
            if (vesselSelectionSection) vesselSelectionSection.SetActive(!isLast);
            _selectedVessel = gameData.selectedVesselClass.Value;

            // Button text
            if (nextRoundButtonText)
                nextRoundButtonText.text = isLast ? "View Final Results" : "Next Round";

            if (nextRoundButton) nextRoundButton.gameObject.SetActive(true);
            if (panelRoot) panelRoot.SetActive(true);
        }

        void ShowFinalResults()
        {
            if (tournamentData == null) return;

            PopulateStandings();

            if (headerText) headerText.text = "Tournament Complete!";
            if (roundIndicatorText) roundIndicatorText.text = $"{tournamentData.ActiveTournament?.TournamentName ?? "Tournament"}";
            if (vesselSelectionSection) vesselSelectionSection.SetActive(false);

            if (nextRoundButtonText) nextRoundButtonText.text = "Return to Menu";
            if (nextRoundButton) nextRoundButton.gameObject.SetActive(true);
            if (panelRoot) panelRoot.SetActive(true);
        }

        void HidePanel()
        {
            if (panelRoot) panelRoot.SetActive(false);
        }

        void PopulateStandings()
        {
            // Clear existing rows
            foreach (var row in _spawnedRows)
            {
                if (row) Destroy(row.gameObject);
            }
            _spawnedRows.Clear();

            if (tournamentData == null || standingsContainer == null || standingRowPrefab == null)
                return;

            for (int i = 0; i < tournamentData.Standings.Count; i++)
            {
                var standing = tournamentData.Standings[i];
                var row = Instantiate(standingRowPrefab, standingsContainer);
                row.Initialize(i + 1, standing);
                _spawnedRows.Add(row);
            }
        }

        public void SetSelectedVessel(VesselClassType vesselClass)
        {
            _selectedVessel = vesselClass;
        }

        void OnNextRoundPressed()
        {
            if (tournamentData == null || tournamentManager == null) return;

            // After final round's "View Final Results" → complete state
            // After complete state's "Return to Menu" → end tournament
            if (tournamentData.CurrentRoundIndex >= tournamentData.TotalRounds)
            {
                tournamentManager.EndTournament();
                return;
            }

            if (tournamentData.IsLastRound)
            {
                // This is the between-rounds state after the last round was captured
                // Advance will detect it's past the last round and call CompleteTournament
                tournamentManager.AdvanceToNextRound(_selectedVessel);
                return;
            }

            tournamentManager.AdvanceToNextRound(_selectedVessel);
        }

        void OnQuitTournamentPressed()
        {
            tournamentManager?.EndTournament();
        }
    }
}

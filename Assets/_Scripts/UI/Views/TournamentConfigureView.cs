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
    /// UI for selecting and launching a tournament from the Arcade screen.
    /// Displays available tournaments and lets the player configure player count,
    /// intensity, and initial vessel before starting.
    /// </summary>
    public class TournamentConfigureView : MonoBehaviour
    {
        [Header("Data")]
        [Inject] TournamentManager tournamentManager;
        [Inject] GameDataSO gameData;

        [Header("Tournament Selection")]
        [SerializeField] SO_TournamentList tournamentList;
        [SerializeField] Transform tournamentButtonContainer;
        [SerializeField] Button tournamentButtonPrefab;

        [Header("Tournament Info")]
        [SerializeField] TMP_Text tournamentNameText;
        [SerializeField] TMP_Text tournamentDescriptionText;
        [SerializeField] TMP_Text roundsPreviewText;

        [Header("Configuration")]
        [SerializeField] Slider playerCountSlider;
        [SerializeField] TMP_Text playerCountText;
        [SerializeField] Slider intensitySlider;
        [SerializeField] TMP_Text intensityText;

        [Header("Vessel Selection")]
        [SerializeField] List<ShipCardView> vesselCards;

        [Header("Actions")]
        [SerializeField] Button startButton;
        [SerializeField] Button closeButton;

        SO_Tournament _selectedTournament;
        VesselClassType _selectedVessel = VesselClassType.Manta;
        int _playerCount = 2;
        int _intensity = 1;

        void Start()
        {
            if (startButton) startButton.onClick.AddListener(OnStartTournament);
            if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (playerCountSlider)
            {
                playerCountSlider.onValueChanged.AddListener(val =>
                {
                    _playerCount = Mathf.RoundToInt(val);
                    if (playerCountText) playerCountText.text = _playerCount.ToString();
                });
            }

            if (intensitySlider)
            {
                intensitySlider.onValueChanged.AddListener(val =>
                {
                    _intensity = Mathf.RoundToInt(val);
                    if (intensityText) intensityText.text = _intensity.ToString();
                });
            }

            PopulateTournamentList();
        }

        void PopulateTournamentList()
        {
            if (tournamentList == null || tournamentList.Tournaments == null) return;

            foreach (var tournament in tournamentList.Tournaments)
            {
                if (tournamentButtonPrefab == null || tournamentButtonContainer == null) continue;

                var btn = Instantiate(tournamentButtonPrefab, tournamentButtonContainer);
                var text = btn.GetComponentInChildren<TMP_Text>();
                if (text) text.text = tournament.TournamentName;

                var captured = tournament;
                btn.onClick.AddListener(() => SelectTournament(captured));
            }

            if (tournamentList.Tournaments.Count > 0)
                SelectTournament(tournamentList.Tournaments[0]);
        }

        void SelectTournament(SO_Tournament tournament)
        {
            _selectedTournament = tournament;

            if (tournamentNameText) tournamentNameText.text = tournament.TournamentName;
            if (tournamentDescriptionText) tournamentDescriptionText.text = tournament.Description;

            if (roundsPreviewText && tournament.Rounds != null)
            {
                var roundNames = new List<string>();
                for (int i = 0; i < tournament.Rounds.Count; i++)
                    roundNames.Add($"{i + 1}. {tournament.Rounds[i].DisplayName}");
                roundsPreviewText.text = string.Join("\n", roundNames);
            }

            _playerCount = tournament.DefaultPlayerCount;
            _intensity = tournament.DefaultIntensity;

            if (playerCountSlider) playerCountSlider.value = _playerCount;
            if (playerCountText) playerCountText.text = _playerCount.ToString();
            if (intensitySlider) intensitySlider.value = _intensity;
            if (intensityText) intensityText.text = _intensity.ToString();
        }

        public void SetSelectedVessel(VesselClassType vesselClass)
        {
            _selectedVessel = vesselClass;
        }

        void OnStartTournament()
        {
            if (_selectedTournament == null || tournamentManager == null)
            {
                Debug.LogWarning("[TournamentConfigureView] No tournament selected or manager not injected.");
                return;
            }

            int humanCount = 1; // TODO: read from party system when multiplayer tournament is supported
            tournamentManager.StartTournament(
                _selectedTournament,
                _playerCount,
                humanCount,
                _intensity,
                _selectedVessel
            );
        }
    }
}

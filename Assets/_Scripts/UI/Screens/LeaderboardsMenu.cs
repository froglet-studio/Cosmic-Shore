using CosmicShore.ScriptableObjects;
using CosmicShore.Data;
using CosmicShore.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using System.Linq;
namespace CosmicShore.UI
{
    [RequireComponent(typeof(MenuAudio))]
    public class LeaderboardsMenu : MonoBehaviour, IScreen
    {
        [Inject] SO_GameList allGames;
        [Inject] AuthenticationDataVariable authenticationDataVariable;

        /// <summary>
        /// Was <c>LeaderboardManager.LeaderboardEntry</c>. It carries no PlayFab type, so it moved
        /// here with the PlayFab manager's removal rather than being deleted with it. Nested
        /// deliberately: the project already has two other top-level types called
        /// <c>LeaderboardEntry</c>.
        /// </summary>
        public struct LeaderboardEntry
        {
            public int Position;
            public int Score;
            public string DisplayName;
            public string PlayerId;
            public string AvatarUrl;

            public LeaderboardEntry(string displayName, string playerId, int score, int position, string avatarUrl)
            {
                DisplayName = displayName;
                PlayerId = playerId;
                Score = score;
                Position = position;
                AvatarUrl = avatarUrl;
            }
        }

        // Never null: PopulateGameHighScores enumerates it unconditionally, and before this it was
        // only ever assigned by a fetch callback that could not fire.
        List<LeaderboardEntry> LeaderboardEntriesV2 = new();

        [SerializeField] Transform GameSelectionContainer;
        [SerializeField] GameObject HighScoresContainer;
        [SerializeField] TMP_Dropdown ShipClassSelection;

        List<SO_ArcadeGame> LeaderboardEligibleGames = new();
        SO_ArcadeGame SelectedGame;
        GameModes SelectedGameMode = GameModes.BlockBandit;
        VesselClassType selectedVesselType = VesselClassType.Any;

        MenuAudio _menuAudio;

        int _displayCount;
        void Start()
        {
            _menuAudio = GetComponent<MenuAudio>();
            // Copy the game list, but skip non-competitive modes -- IMPORTANT to copy the list so we don't modify the SO
            foreach (var game in allGames.Games)
                if (game.Mode != GameModes.Elimination)
                    LeaderboardEligibleGames.Add(game);

            var gamesCount = LeaderboardEligibleGames.Count;
            var containerCount = GameSelectionContainer.childCount;
            _displayCount = Math.Min(gamesCount, containerCount);
            SelectedGame = LeaderboardEligibleGames[0];

            ShipClassSelection.onValueChanged.AddListener(SelectShipType);
        }

        public void OnScreenEnter() => LoadView();
        public void OnScreenExit() { }

        public void LoadView()
        {
            PopulateGameSelectionList();
        }

        /// <summary>
        /// This screen has no backend. It used to read the PlayFab <c>LeaderboardManager</c>, whose
        /// prefab is in no scene — so <c>Instance</c> was null and every call from
        /// <see cref="SelectShipType"/> threw, on every open of the Records screen. The UGS
        /// replacement (<c>WeeklyChallengeLeaderboardService</c>) is a different board and this
        /// screen is not on it, so the honest state is an empty list and the screen's own empty
        /// rendering. Porting it is the follow-up (<c>Docs/PLAYFAB_RETIREMENT.md</c> §4).
        /// </summary>
        void FetchLeaderboard()
        {
            LeaderboardEntriesV2.Clear();
            PopulateGameHighScores();
        }

        IEnumerator SelectShipTypeCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectShipType(index);
        }

        public void SelectShipType(int optionValue)
        {
            var shiptypeName = ShipClassSelection.options[optionValue].text;
            selectedVesselType = Enum.Parse<VesselClassType>(shiptypeName);

            FetchLeaderboard();
        }

        IEnumerator SelectGameCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectGame(index);
        }

        public void SelectGame(int index)
        {
            // Deselect them all
            for (var i = 0; i < _displayCount; i++)
                GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = LeaderboardEligibleGames[i].IconInactive;

            // Select the one
            SelectedGame = LeaderboardEligibleGames[index];
            SelectedGameMode = SelectedGame.Mode;
            GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.IconActive;
            PopulateShipClassSelectionDropdown();
        }

        void PopulateGameSelectionList()
        {
            // Deactivate All
            for (var i = 0; i < GameSelectionContainer.transform.childCount; i++)
                GameSelectionContainer.GetChild(i).gameObject.SetActive(false);

            // Reactivate based on the number of games for the given vessel
            for (var i = 0; i < _displayCount; i++)
            {
                var selectionIndex = i;
                var game = LeaderboardEligibleGames[i];
                try
                {
                    var gameSelection = GameSelectionContainer.GetChild(i).gameObject;
                    gameSelection.SetActive(true);
                    gameSelection.GetComponent<Image>().sprite = game.IconInactive;
                    gameSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                    gameSelection.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
                    gameSelection.GetComponent<Button>().onClick
                        .AddListener(() => _menuAudio.PlayAudio());
                }
                catch (UnityException outOfBoundException)
                {
                    CSDebug.LogWarningFormat("{0} Leaderboard entries are more than the UI selections can handle, please add more game selections for them./n See error: {1}", nameof(LeaderboardsMenu), outOfBoundException.Message);
                }
                
            }

            StartCoroutine(SelectGameCoroutine(0));
        }

        void PopulateShipClassSelectionDropdown()
        {
            var options = new List<TMP_Dropdown.OptionData>();

            // Only add "Any" selection if there is more than one vessel available
            if (SelectedGame.Vessels != null && SelectedGame.Vessels.Count > 1)
                options.Add(new TMP_Dropdown.OptionData("Any"));

            if (SelectedGame.Vessels != null)
            {
                foreach (var vessel in SelectedGame.Vessels)
                {
                    if (vessel != null)
                        options.Add(new TMP_Dropdown.OptionData(vessel.Class.ToString()));
                }
            }

            ShipClassSelection.options = options;
            ShipClassSelection.value = 0;
            StartCoroutine(SelectShipTypeCoroutine(0));
        }

        /// <summary>
        /// The UGS player id. Was <c>AuthenticationManager.PlayFabAccount.ID</c>, which was always
        /// empty — so the "this row is you" highlight could never match.
        /// </summary>
        string LocalPlayerId => authenticationDataVariable?.Value?.PlayerId ?? string.Empty;

        void PopulateGameHighScores()
        {
            // High Scores Container null check
            if (HighScoresContainer == null)
            {
                CSDebug.LogWarning($"{nameof(HighScoresContainer)} game object destroyed.");
                return;
            }


            for (var i = 0; i < HighScoresContainer.transform.childCount; i++)
                HighScoresContainer.transform.GetChild(i).gameObject.SetActive(false);

            for (var i = 0; i < LeaderboardEntriesV2.Count; i++)
            {
                var score = LeaderboardEntriesV2[i];

                if (SelectedGame.GolfScoring)
                    score.Score *= -1;

                HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().text = (score.Position + 1).ToString();
                if (string.IsNullOrEmpty(score.DisplayName))
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().text = "[NAMELESS PILOT]";
                    HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().fontSize = 14;
                }
                else
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().text = score.DisplayName;
                    HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().fontSize = 18;
                }
                HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().text = score.Score.ToString();
                HighScoresContainer.transform.GetChild(i).gameObject.SetActive(true);

                // Highlight the player's Score
                if (!string.IsNullOrEmpty(score.PlayerId) && score.PlayerId == LocalPlayerId)
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = new Color(.1f, .7f, .7f);
                    HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().color = new Color(.1f, .7f, .7f);
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = new Color(.1f, .7f, .7f);
                }
                else
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = Color.white;
                    HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().color = Color.white;
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = Color.white;
                }
            }
        }
    }
}
using CosmicShore.Core;
using CosmicShore._Core.Playfab.Authentication;
using CosmicShore._Core.Playfab.PlayStream;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    [RequireComponent(typeof(MenuAudio))]
    public class LeaderboardsMenu : MonoBehaviour
    {
        List<LeaderboardManager.LeaderboardEntry> LeaderboardEntriesV2;

        [SerializeField] Transform GameSelectionContainer;
        [SerializeField] GameObject HighScoresContainer;
        [SerializeField] TMP_Dropdown ShipClassSelection;

        List<SO_ArcadeGame> LeaderboardEligibleGames = new();
        SO_ArcadeGame SelectedGame;
        MiniGames SelectedGameMode = MiniGames.BlockBandit;
        ShipTypes SelectedShipType = ShipTypes.Any;

        // Start is called before the first frame update
        void Start()
        {
            // TODO: Reconsider this implementation for avoiding displaying Freestyle on the scoreboard
            // Copy the game list, but skip Freestyle -- IMPORTANT to copy the list so we don't modify the SO
            foreach (var game in GameManager.Instance.AllGames.GameList)
                if (game.Mode != MiniGames.Freestyle && game.Mode != MiniGames.Elimination)
                    LeaderboardEligibleGames.Add(game);

            AuthenticationManager.OnProfileLoaded += FetchLeaderboard;

            ShipClassSelection.onValueChanged.AddListener(SelectShipType);

            PopulateGameSelectionList();
        }

        void FetchLeaderboard()
        {
            LeaderboardManager.Instance.FetchLeaderboard(
                LeaderboardManager.Instance.GetGameplayStatKey(SelectedGameMode, SelectedShipType),
                new() { { "Intensity", "1" } },
                OnFetchLeaderboard);
        }

        void OnFetchLeaderboard(List<LeaderboardManager.LeaderboardEntry> results)
        {
            Debug.Log("OnFetchLeaderboard");
            foreach (var result in results)
            {
                Debug.Log($"Leaderboard Result - {result.Position} | {result.DisplayName} | {result.Score}");
            }

            LeaderboardEntriesV2 = results;
            PopulateGameHighScores();
        }

        IEnumerator SelectShipTypeCoroutine(int index)
        {
            yield return new WaitUntil(() => AuthenticationManager.PlayerAccount != null);
            SelectShipType(index);
        }

        public void SelectShipType(int optionValue)
        {
            var shiptypeName = ShipClassSelection.options[optionValue].text;
            SelectedShipType = Enum.Parse<ShipTypes>(shiptypeName);

            FetchLeaderboard();
        }

        IEnumerator SelectGameCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectGame(index);
        }

        public void SelectGame(int index)
        {
            Debug.Log($"SelectGame: {index}");

            // Deselect them all
            for (var i = 0; i < LeaderboardEligibleGames.Count; i++)
                GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = LeaderboardEligibleGames[i].Icon;

            // Select the one
            SelectedGame = LeaderboardEligibleGames[index];
            SelectedGameMode = SelectedGame.Mode;
            GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;
            PopulateShipClassSelectionDropdown();
        }

        void PopulateGameSelectionList()
        {
            // Deactivate All
            for (var i = 0; i < GameSelectionContainer.transform.childCount; i++)
                GameSelectionContainer.GetChild(i).gameObject.SetActive(false);

            // Reactivate based on the number of games for the given ship
            for (var i = 0; i < LeaderboardEligibleGames.Count; i++)
            {
                var selectionIndex = i;
                var game = LeaderboardEligibleGames[i];
                Debug.Log($"Populating Game Select List: {game.Name}");

                var gameSelection = GameSelectionContainer.GetChild(i).gameObject;
                gameSelection.SetActive(true);
                gameSelection.GetComponent<Image>().sprite = game.Icon;
                gameSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                gameSelection.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
                gameSelection.GetComponent<Button>().onClick.AddListener(() => GetComponent<MenuAudio>().PlayAudio());
            }

            StartCoroutine(SelectGameCoroutine(0));
        }

        void PopulateShipClassSelectionDropdown()
        {
            var options = new List<TMP_Dropdown.OptionData>();

            // Only add "Any" selection if there is more than one vessel class available
            if (SelectedGame.Vessels.Count > 1)
                options.Add(new TMP_Dropdown.OptionData("Any"));

            foreach (var vessel in SelectedGame.Vessels)
                options.Add(new TMP_Dropdown.OptionData(vessel.Ship.Class.ToString()));

            ShipClassSelection.options = options;
            ShipClassSelection.value = 0;
            StartCoroutine(SelectShipTypeCoroutine(0));
        }

        void PopulateGameHighScores()
        {
            Debug.Log($"PopulateGameHighScores: {SelectedGame.Name}");

            // High Scores Container null check
            if (HighScoresContainer == null)
            {
                Debug.LogWarning($"{nameof(HighScoresContainer)} game object destroyed.");
                return;
            }


            for (var i = 0; i < HighScoresContainer.transform.childCount; i++)
                HighScoresContainer.transform.GetChild(i).gameObject.SetActive(false);

            for (var i = 0; i < LeaderboardEntriesV2.Count; i++)
            {
                var score = LeaderboardEntriesV2[i];
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

                // Highlight the player's score
                if (score.PlayerId == AuthenticationManager.PlayerAccount.PlayFabId)
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
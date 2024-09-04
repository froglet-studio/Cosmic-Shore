using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.Integrations.PlayFab.PlayStream;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class DailyChallengeLeaderboardView : View
    {
        // TODO: Need a ProfileManager managing this
        [SerializeField] SO_ProfileIconList ProfileIcons;

        [SerializeField] GameObject HighScoresContainer;
        [HideInInspector] public List<LeaderboardManager.LeaderboardEntry> LeaderboardEntries;

        void Start()
        {
            PlayerDataController.OnProfileLoaded += FetchLeaderboard;
            FetchLeaderboard();
        }

        public override void UpdateView()
        {
            
        }

        void FetchLeaderboard()
        {
            Debug.Log("DailyChallengeLeaderboardView.FetchLeaderboard");
            LeaderboardManager.Instance.FetchLeaderboard(
                LeaderboardManager.DailyChallengeStatisticName,
                new() { { "", "" } },
                OnFetchLeaderboard);
        }

        void OnFetchLeaderboard(List<LeaderboardManager.LeaderboardEntry> results)
        {
            Debug.Log("OnFetchLeaderboard");
            foreach (var result in results)
            {
                Debug.Log($"Leaderboard Result - {result.Position} | {result.DisplayName} | {result.Score}");
            }

            LeaderboardEntries = results;
            PopulateGameHighScores();
        }

        void PopulateGameHighScores()
        {
            Debug.Log($"PopulateGameHighScores: Daily Challenge");

            // High Scores Container null check
            if (HighScoresContainer == null)
            {
                Debug.LogWarning($"{nameof(HighScoresContainer)} game object destroyed.");
                return;
            }


            for (var i = 0; i < HighScoresContainer.transform.childCount; i++)
                HighScoresContainer.transform.GetChild(i).gameObject.SetActive(false);

            for (var i = 0; i < LeaderboardEntries.Count; i++)
            {
                var score = LeaderboardEntries[i];

                // TODO: need to solve this one
                //if (SelectedGame.GolfScoring)
                //    score.Score *= -1;

                HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().text = (score.Position + 1).ToString();
                HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<Image>().sprite = GetProfileIconByID(int.Parse(score.AvatarUrl)).IconSprite;
                if (string.IsNullOrEmpty(score.DisplayName))
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().text = "[NAMELESS PILOT]";
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().fontSize = 14;
                }
                else
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().text = score.DisplayName;
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().fontSize = 18;
                }
                HighScoresContainer.transform.GetChild(i).GetChild(3).GetComponent<TMP_Text>().text = score.Score.ToString();
                HighScoresContainer.transform.GetChild(i).gameObject.SetActive(true);

                // Highlight the player's score
                if (score.PlayerId == AuthenticationManager.PlayFabAccount.ID)
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = new Color(.1f, .7f, .7f);
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = new Color(.1f, .7f, .7f);
                    HighScoresContainer.transform.GetChild(i).GetChild(3).GetComponent<TMP_Text>().color = new Color(.1f, .7f, .7f);
                }
                else
                {
                    HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = Color.white;
                    HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = Color.white;
                    HighScoresContainer.transform.GetChild(i).GetChild(3).GetComponent<TMP_Text>().color = Color.white;
                }
            }
        }


        public ProfileIcon GetProfileIconByID(int id)
        {
            return ProfileIcons.profileIcons.Where(x => x.Id == id).FirstOrDefault();
        }
    }
}
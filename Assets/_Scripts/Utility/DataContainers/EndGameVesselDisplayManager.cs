using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Manages spawning and displaying vessel icons for end-game screen.
    /// Handles both single-player (1 vessel) and multiplayer (up to 4 vessels).
    /// Works with Layout Groups for automatic organization.
    /// </summary>
    public class EndGameVesselDisplayManager : MonoBehaviour
    {
        [Header("References")]
        [Inject] private GameDataSO gameData;
        [SerializeField] private VesselIconLibrarySO vesselIconLibrary;

        [Header("Vessel Display")]
        [SerializeField] private EndGameVesselDisplay vesselDisplayPrefab;
        [SerializeField] private Transform vesselDisplayContainer;

        [Header("Animation Settings")]
        [SerializeField] private bool animateFadeIn = true;
        [SerializeField] private float delayBetweenVessels = 0.15f;
        [SerializeField] private float initialDelay = 0.2f;

        [Header("Layout")]
        [Tooltip("Optional: Different container positions for different player counts")]
        [SerializeField] private RectTransform singlePlayerLayout;
        [SerializeField] private RectTransform multiPlayerLayout;

        private List<EndGameVesselDisplay> activeDisplays = new List<EndGameVesselDisplay>();
        private bool isDisplaying;

        /// <summary>
        /// Display vessel icons based on game data
        /// </summary>
        public void DisplayVessels()
        {
            if (isDisplaying)
            {
                CSDebug.LogWarning("Already displaying vessels!");
                return;
            }

            StartCoroutine(DisplayVesselsCoroutine());
        }

        IEnumerator DisplayVesselsCoroutine()
        {
            isDisplaying = true;

            // Clear any existing displays
            ClearDisplays();

            // Get vessel data from game
            List<VesselDisplayData> vesselDataList = GatherVesselData();

            if (vesselDataList.Count == 0)
            {
                CSDebug.LogWarning("No vessel data to display!");
                isDisplaying = false;
                yield break;
            }

            // Setup appropriate layout
            SetupLayout(vesselDataList.Count);

            // Initial delay before starting
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            // Spawn and animate each vessel display
            for (int i = 0; i < vesselDataList.Count; i++)
            {
                SpawnVesselDisplay(vesselDataList[i], i);

                // Delay between spawns for staggered animation
                if (i < vesselDataList.Count - 1 && delayBetweenVessels > 0f)
                    yield return new WaitForSeconds(delayBetweenVessels);
            }

            isDisplaying = false;
        }

        /// <summary>
        /// Gather vessel display data from game data
        /// </summary>
        List<VesselDisplayData> GatherVesselData()
        {
            List<VesselDisplayData> vesselData = new List<VesselDisplayData>();

            if (gameData.Players == null || gameData.Players.Count == 0)
            {
                CSDebug.LogWarning("No players found in game data!");
                return vesselData;
            }

            // Rank from the authoritative, rule-produced results list — the single source of truth
            // every other end-game surface reads (Docs/ScoringSystem/REFACTOR.md R10). It is
            // golf-aware (HexRace/Joust: lower finish time = better), which a raw Score sort here is
            // NOT: a descending-Score sort ranked the loser's 10000+ sentinel above the winner's time.
            bool hasResults = gameData.Results != null && gameData.Results.Count > 0;

            // Fallback only for modes that never produce Results (e.g. WildlifeBlitz, which doesn't
            // call SetResults): keep the legacy descending-Score order so they don't regress.
            List<IRoundStats> fallbackOrder = null;
            if (!hasResults)
            {
                fallbackOrder = new List<IRoundStats>(gameData.RoundStatsList);
                fallbackOrder.Sort((a, b) => b.Score.CompareTo(a.Score));
            }

            // Create display data for each player
            for (int i = 0; i < gameData.Players.Count; i++)
            {
                var player = gameData.Players[i];
                if (player?.Vessel == null)
                {
                    CSDebug.LogWarning($"Player {i} has no vessel!");
                    continue;
                }

                int ranking = hasResults
                    ? ResolveRankingFromResults(player.Name)
                    : ResolveRankingFromFallback(player.Name, fallbackOrder);

                // Get vessel type
                VesselClassType vesselType = player.Vessel.VesselStatus.VesselType;
                int score = (int)player.RoundStats.Score;

                var data = new VesselDisplayData(
                    player.Name,
                    vesselType,
                    ranking,
                    player.Domain,
                    score
                );

                vesselData.Add(data);
            }

            // Sort by ranking for display order
            vesselData.Sort((a, b) => a.ranking.CompareTo(b.ranking));

            return vesselData;
        }

        /// <summary>Rank from the rule-produced <see cref="GameDataSO.Results"/> SSOT (matched by name).</summary>
        int ResolveRankingFromResults(string playerName)
        {
            foreach (var result in gameData.Results)
                if (result.Name == playerName)
                    return result.Rank;

            // Not in Results (shouldn't happen) — rank last so a missing entry never claims 1st.
            return gameData.Results.Count + 1;
        }

        /// <summary>Legacy descending-Score order, used only when the mode produced no Results.</summary>
        int ResolveRankingFromFallback(string playerName, List<IRoundStats> ordered)
        {
            for (int j = 0; j < ordered.Count; j++)
                if (ordered[j].Name == playerName)
                    return j + 1;
            return 1;
        }

        /// <summary>
        /// Spawn individual vessel display
        /// </summary>
        void SpawnVesselDisplay(VesselDisplayData data, int index)
        {
            var display = Instantiate(vesselDisplayPrefab, vesselDisplayContainer);
            display.name = $"VesselDisplay_{data.playerName}";

            // Initialize with data
            display.Initialize(data, vesselIconLibrary);

            // Animate or show instantly
            if (animateFadeIn)
                display.FadeIn();
            else
                display.ShowInstant();

            activeDisplays.Add(display);
        }

        /// <summary>
        /// Setup layout based on player count
        /// </summary>
        void SetupLayout(int playerCount)
        {
            // If we have specific layouts for single/multiplayer, use them
            if (playerCount == 1 && singlePlayerLayout)
            {
                vesselDisplayContainer = singlePlayerLayout;
                if (multiPlayerLayout) multiPlayerLayout.gameObject.SetActive(false);
                singlePlayerLayout.gameObject.SetActive(true);
            }
            else if (playerCount > 1 && multiPlayerLayout)
            {
                vesselDisplayContainer = multiPlayerLayout;
                if (singlePlayerLayout) singlePlayerLayout.gameObject.SetActive(false);
                multiPlayerLayout.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Clear all active displays
        /// </summary>
        public void ClearDisplays()
        {
            foreach (var display in activeDisplays.Where(display => display))
            {
                Destroy(display.gameObject);
            }

            activeDisplays.Clear();
        }

        /// <summary>
        /// Hide all displays without destroying
        /// </summary>
        public void HideDisplays()
        {
            foreach (var display in activeDisplays)
            {
                if (display != null)
                    display.Hide();
            }
        }

        /// <summary>
        /// Instant display without animations (useful for debugging)
        /// </summary>
        public void DisplayVesselsInstant()
        {
            ClearDisplays();

            var vesselDataList = GatherVesselData();
            SetupLayout(vesselDataList.Count);

            foreach (var data in vesselDataList)
            {
                var display = Instantiate(vesselDisplayPrefab, vesselDisplayContainer);
                display.Initialize(data, vesselIconLibrary);
                display.ShowInstant();
                activeDisplays.Add(display);
            }
        }

        void OnDisable()
        {
            // Stop any running coroutines
            StopAllCoroutines();
            isDisplaying = false;
        }

        #region Editor Helpers
        
        /// <summary>
        /// For testing in editor
        /// </summary>
        [ContextMenu("Test Display")]
        void TestDisplay()
        {
            DisplayVessels();
        }

        [ContextMenu("Clear Displays")]
        void TestClear()
        {
            ClearDisplays();
        }

        #endregion
    }
}
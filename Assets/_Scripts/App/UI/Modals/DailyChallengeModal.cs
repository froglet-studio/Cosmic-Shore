using CosmicShore.App.Systems;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class DailyChallengeModal : ModalWindowManager
    {
        [SerializeField] DailyChallengeGameView GameView;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] TMP_Text TicketBalance;
        GameModes GameMode;

        void OnEnable()
        {
            CatalogManager.OnLoadInventory += SetTicketBalance;
            CatalogManager.OnInventoryChange += SetTicketBalance;
        }

        void OnDisable()
        {
            CatalogManager.OnLoadInventory -= SetTicketBalance;
            CatalogManager.OnInventoryChange -= SetTicketBalance;
        }

        protected override void Start()
        {
            base.Start();

            if (CatalogManager.CatalogLoaded)
                SetTicketBalance();
        }

        void SetTicketBalance()
        {
            TicketBalance.text = CatalogManager.Instance.GetDailyChallengeTicketBalance().ToString();
        }

        void Update()
        {
            if (GameMode == GameModes.Random)
                AssignGameMode();

            DateTime current = DateTime.UtcNow;
            DateTime tomorrow = current.AddDays(1).Date;
            double secondsUntilMidnight = (tomorrow - current).TotalSeconds;

            if (secondsUntilMidnight > 0)
            {
                TimeSpan timespan = TimeSpan.FromSeconds(secondsUntilMidnight);
                TimeRemaining.text = string.Format("Time left: {0:D2}:{1:D2}:{2:D2}",
                                timespan.Hours,
                                timespan.Minutes,
                                timespan.Seconds);
            }
            else
            {
                AssignGameMode();
            }
        }

        void AssignGameMode()
        {
            GameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
            GameView.AssignModel(Arcade.Instance.GetTrainingGameByMode(GameMode));
        }

        public void Play()
        {
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}
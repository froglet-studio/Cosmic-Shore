using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Data;
using CosmicShore.Core;
using System;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
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

        protected override void OnDisable()
        {
            base.OnDisable();

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

        // [DAILY CHALLENGE DISABLED] The feature is not enabled in the game, so this
        // countdown tick is commented out rather than left running every frame. NOT deleted -
        // restore it verbatim when the feature comes back, and see the note below first.
        //
        // Note for whoever re-enables this: as written below it declared 'void Update()',
        // which HID the base 'ModalWindowManager.Update()' rather than overriding it (CS0114).
        // The side effect is that this modal alone would not close on gamepad B. When the
        // feature returns, decide deliberately whether to 'override' and call base.Update()
        // (matching every other modal) or to 'new' it and keep suppressing that. Do not
        // resolve the warning by accident in either direction.
        //
        // protected override void Update()
        // {
        //     base.Update();
        //
        //     if (GameMode == GameModes.Random)
        //         AssignGameMode();
        //
        //     DateTime current = DateTime.UtcNow;
        //     DateTime tomorrow = current.AddDays(1).Date;
        //     double secondsUntilMidnight = (tomorrow - current).TotalSeconds;
        //
        //     if (secondsUntilMidnight > 0)
        //     {
        //         TimeSpan timespan = TimeSpan.FromSeconds(secondsUntilMidnight);
        //         TimeRemaining.text = string.Format("Time left: {0:D2}:{1:D2}:{2:D2}",
        //                         timespan.Hours,
        //                         timespan.Minutes,
        //                         timespan.Seconds);
        //     }
        //     else
        //     {
        //         AssignGameMode();
        //     }
        // }

        void AssignGameMode()
        {
            // GameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
            // GameView.AssignModel(Arcade.Instance.GetTrainingGameByMode(GameMode));
        }

        public void Play()
        {
            audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}
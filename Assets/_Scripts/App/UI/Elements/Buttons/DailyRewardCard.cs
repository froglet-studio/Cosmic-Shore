using CosmicShore.App.Systems;
using CosmicShore.App.Systems.Ads;
using CosmicShore.Integrations.PlayFab.CloudScripts;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.UI;

namespace CosmicShore
{
    enum ButtonMode
    {
        Free,
        Ad,
        Clock
    }

    public class DailyRewardCard : PurchaseCard
    {
        [SerializeField] Image FreeButton;
        [SerializeField] Image AdButton;
        [SerializeField] Image ClockButton;
        [SerializeField] Sprite FreeButtonBackgroundSprite;
        [SerializeField] Sprite AdButtonBackgroundSprite;
        [SerializeField] Sprite ClockButtonBackgroundSprite;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] AdsSystem adsManager;

        ButtonMode Mode = ButtonMode.Free;
        string LastFreeClaimedDatePrefKey = "DailyRewardLastFreeClaimedDate";
        string LastAdClaimedDatePrefKey = "DailyRewardLastAdClaimedDate";

        void Start()
        {
            InitializePlayerPrefs();
            EnterClockMode();

            var lastClaimedDate = DateTime.Parse(PlayerPrefs.GetString(LastFreeClaimedDatePrefKey), null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            if (lastClaimedDate < DateTime.UtcNow.Date)
            {
                EnterFreeMode();

            }
            else
            {
                var lastAdDate = DateTime.Parse(PlayerPrefs.GetString(LastAdClaimedDatePrefKey), null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                if (lastAdDate < DateTime.UtcNow.Date)
                {
                    EnterAdMode();
                }
            }
        }

        void Update()
        {
            if (Mode == ButtonMode.Clock)
            {
                DateTime current = DateTime.UtcNow;
                DateTime tomorrow = current.AddDays(1).Date;
                double secondsUntilMidnight = (tomorrow - current).TotalSeconds;

                if (secondsUntilMidnight > 0)
                {
                    TimeSpan timespan = TimeSpan.FromSeconds(secondsUntilMidnight);
                    TimeRemaining.text = string.Format("<mspace=.6em>{0:D2}:{1:D2}:{2:D2}",
                                    timespan.Hours,
                                    timespan.Minutes,
                                    timespan.Seconds);
                }
                else
                {
                    // Player watched the clock roll over
                    EnterFreeMode();
                }
            }
        }

        void EnterFreeMode()
        {
            Mode = ButtonMode.Free;
            BackgroundImage.sprite = FreeButtonBackgroundSprite;
            FreeButton.gameObject.SetActive(true);

            AdButton.gameObject.SetActive(false);
            ClockButton.gameObject.SetActive(false);
        }

        void EnterAdMode()
        {
            Mode = ButtonMode.Ad;
            BackgroundImage.sprite = AdButtonBackgroundSprite;
            AdButton.gameObject.SetActive(true);

            ClockButton.gameObject.SetActive(false);
            FreeButton.gameObject.SetActive(false);
        }
        void EnterClockMode()
        {
            Mode = ButtonMode.Clock;
            BackgroundImage.sprite = ClockButtonBackgroundSprite;
            ClockButton.gameObject.SetActive(true);

            AdButton.gameObject.SetActive(false);
            FreeButton.gameObject.SetActive(false);
        }

        public override void Purchase()
        {
            switch (Mode)
            {
                case ButtonMode.Free:
                    PlayerPrefs.SetString(LastFreeClaimedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
                    PlayerPrefs.Save();
                    DailyRewardHandler.Instance.Claim();
                    EnterAdMode();
                    break;
                case ButtonMode.Ad:
                    PlayerPrefs.SetString(LastAdClaimedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
                    PlayerPrefs.Save();
                    AdsSystem.AdShowComplete += ClaimAdWatchReward;
                    adsManager.ShowAd();
                    EnterClockMode();
                    break;
                case ButtonMode.Clock:
                    break;
            }
        }

        void ClaimAdWatchReward(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
            Debug.Log("Claim Daily Reward via ad watch");
            DailyRewardHandler.Instance.Claim();
        }

        void InitializePlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(LastFreeClaimedDatePrefKey))
                PlayerPrefs.SetString(LastFreeClaimedDatePrefKey, DateTime.MinValue.Date.ToString("o"));
            if (!PlayerPrefs.HasKey(LastAdClaimedDatePrefKey))
                PlayerPrefs.SetString(LastAdClaimedDatePrefKey, DateTime.MinValue.Date.ToString("o"));

            PlayerPrefs.Save();
        }
    }
}
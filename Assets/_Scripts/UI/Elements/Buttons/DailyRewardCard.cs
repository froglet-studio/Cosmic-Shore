using CosmicShore.Core;
using CosmicShore.UI;
using System;
using System.Collections;
using System.Globalization;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    enum ButtonMode
    {
        Free,
        Clock
    }

    public class DailyRewardCard : PurchaseCard
    {
        [Inject] AudioSystem audioSystem;

        [SerializeField] Image FreeButton;
        [Tooltip("Legacy ad button. Ads were removed; this reference exists only so the " +
                 "object stays hidden until it is deleted from Menu_Main and Screens.prefab.")]
        [SerializeField] Image AdButton;
        [SerializeField] Image ClockButton;
        [SerializeField] IconEmitter IconEmitter;
        [SerializeField] Sprite FreeButtonBackgroundSprite;
        [SerializeField] Sprite ClockButtonBackgroundSprite;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] float duration = .5f; // Duration for the full rotation

        ButtonMode Mode = ButtonMode.Free;
        string LastFreeClaimedDatePrefKey = "DailyRewardLastFreeClaimedDate";

        void Start()
        {
            if (AdButton) AdButton.gameObject.SetActive(false);

            InitializePlayerPrefs();
            EnterClockMode();

            var lastClaimedDate = DateTime.Parse(PlayerPrefs.GetString(LastFreeClaimedDatePrefKey), null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            if (lastClaimedDate < DateTime.UtcNow.Date)
                EnterFreeMode();
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
                    StartCoroutine(RotateCoroutine(EnterFreeMode));
                }
            }
        }

        void EnterFreeMode()
        {
            Mode = ButtonMode.Free;
            BackgroundImage.sprite = FreeButtonBackgroundSprite;
            FreeButton.gameObject.SetActive(true);
            ClockButton.gameObject.SetActive(false);
        }

        void EnterClockMode()
        {
            Mode = ButtonMode.Clock;
            BackgroundImage.sprite = ClockButtonBackgroundSprite;
            ClockButton.gameObject.SetActive(true);
            FreeButton.gameObject.SetActive(false);
        }

        public override void Purchase()
        {
            switch (Mode)
            {
                case ButtonMode.Free:
                    audioSystem.PlayMenuAudio(MenuAudioCategory.Confirmed);
                    PlayerPrefs.SetString(LastFreeClaimedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
                    PlayerPrefs.Save();
                    DailyRewardHandler.Instance.Claim();
                    IconEmitter?.EmitIcons();

                    StartCoroutine(RotateCoroutine(EnterClockMode));
                    break;
                case ButtonMode.Clock:
                    break;
            }
        }

        void InitializePlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(LastFreeClaimedDatePrefKey))
                PlayerPrefs.SetString(LastFreeClaimedDatePrefKey, DateTime.MinValue.Date.ToString("o"));
            PlayerPrefs.Save();
        }

        public override void SetVirtualItem(VirtualItem virtualItem)
        {
            // TODO: reconsider implementation now that this class is not using the abstract method
            throw new NotImplementedException();
        }

        IEnumerator RotateCoroutine(Action modeChangeRoutine)
        {
            float halfDuration = duration / 2f;
            float elapsedTime = 0f;

            // Rotate to 90 degrees Y
            while (elapsedTime < halfDuration)
            {
                float t = elapsedTime / halfDuration;
                float easedT = EaseInOutQuad(t);
                float angle = Mathf.Lerp(0, 90, easedT);
                transform.localRotation = Quaternion.Euler(0, angle, 0);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure the final rotation is exactly 90 degrees
            transform.localRotation = Quaternion.Euler(0, 90, 0);

            modeChangeRoutine?.Invoke();

            // Reset the timer
            elapsedTime = 0f;

            // Rotate back to 0 degrees Y
            while (elapsedTime < halfDuration)
            {
                float t = elapsedTime / halfDuration;
                float easedT = EaseInOutQuad(t);
                float angle = Mathf.Lerp(90, 0, easedT);
                transform.localRotation = Quaternion.Euler(0, angle, 0);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure the final rotation is exactly 0 degrees
            transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
    }
}
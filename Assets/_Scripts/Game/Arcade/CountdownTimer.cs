using CosmicShore.App.Systems.Audio;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Arcade
{
    public class CountdownTimer : MonoBehaviour
    {
        [SerializeField] Image CountdownDisplay;
        [SerializeField] Sprite Countdown3;
        [SerializeField] Sprite Countdown2;
        [SerializeField] Sprite Countdown1;
        [SerializeField] Sprite Countdown0;
        [SerializeField] AudioClip CountdownBeep;
        [SerializeField] float CountdownGrowScale = 1.5f;

        IEnumerator CountdownDigitCoroutine(Sprite digit)
        {
            var elapsedTime = 0f;
            CountdownDisplay.transform.localScale = Vector3.one;
            CountdownDisplay.sprite = digit;

            AudioSystem.Instance.PlaySFXClip(CountdownBeep);

            while (elapsedTime < 1)
            {
                elapsedTime += Time.deltaTime;
                CountdownDisplay.transform.localScale = Vector3.one + Vector3.one * ((CountdownGrowScale - 1) * elapsedTime);
                yield return null;
            }
        }

        public void BeginCountdown(Action countZero)
        {
            StartCoroutine(CountdownCoroutine(countZero));
        }

        IEnumerator CountdownCoroutine(Action countZero)
        {
            CountdownDisplay.gameObject.SetActive(true);

            yield return StartCoroutine(CountdownDigitCoroutine(Countdown3));
            yield return StartCoroutine(CountdownDigitCoroutine(Countdown2));
            yield return StartCoroutine(CountdownDigitCoroutine(Countdown1));
            yield return StartCoroutine(CountdownDigitCoroutine(Countdown0));

            CountdownDisplay.transform.localScale = Vector3.one;
            CountdownDisplay.gameObject.SetActive(false);

            countZero.Invoke();
        }
    }
}
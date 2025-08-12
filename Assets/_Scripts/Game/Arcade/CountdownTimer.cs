using CosmicShore.App.Systems.Audio;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Arcade
{
    public class CountdownTimer : MonoBehaviour
    {
        [SerializeField] Image   countdownDisplay;
        [SerializeField] Sprite  countdown3;
        [SerializeField] Sprite  countdown2;
        [SerializeField] Sprite  countdown1;
        [SerializeField] Sprite  countdown0;
        [SerializeField] AudioClip countdownBeep;
        [SerializeField] float     countdownDuration  = 1f;
        [SerializeField] float     countdownGrowScale = 1.5f;

        Sprite[] _sprites;

        void Awake()
        {
            // cache into an array so we can loop
            _sprites = new[] { countdown3, countdown2, countdown1, countdown0 };
        }

        public void BeginCountdown(Action onComplete)
        {
            // stop any running countdown first
            StopAllCoroutines();
            StartCoroutine(CountdownCoroutine(onComplete));
        }

        IEnumerator CountdownCoroutine(Action onComplete)
        {
            countdownDisplay.gameObject.SetActive(true);

            foreach (var spr in _sprites)
            {
                countdownDisplay.sprite = spr;
                countdownDisplay.transform.localScale = Vector3.one;
                AudioSystem.Instance.PlaySFXClip(countdownBeep);

                float elapsed = 0f;
                while (elapsed < countdownDuration)
                {
                    elapsed += Time.deltaTime;
                    // Lerp scale from 1 → countdownGrowScale over the duration
                    float t = Mathf.Clamp01(elapsed / countdownDuration);
                    countdownDisplay.transform.localScale = Vector3.Lerp(
                        Vector3.one,
                        Vector3.one * countdownGrowScale,
                        t
                    );
                    yield return null;
                }
            }

            countdownDisplay.gameObject.SetActive(false);
            onComplete?.Invoke();
        }
    }
}
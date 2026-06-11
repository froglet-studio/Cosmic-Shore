using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Displays a random sprite and cycles through game tips on the connecting panel.
    /// Tips are sourced from an SO_GameTipsList based on the current game mode.
    /// </summary>
    public class ConnectingPanel : MonoBehaviour
    {
        [Header("Sprite Display")]
        [SerializeField] private SO_ConnectingPanelSpriteList spriteList;
        [SerializeField] private Image displayImage;

        [Header("Tips")]
        [SerializeField] private SO_GameTipsList tipsList;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private CanvasGroup tipCanvasGroup;

        [Header("Tips Timing")]
        [Tooltip("Seconds each tip is displayed before cycling.")]
        [SerializeField] private float tipDisplayDuration = 5f;
        [Tooltip("Seconds for the fade in/out transition.")]
        [SerializeField] private float tipFadeDuration = 0.5f;

        private CancellationTokenSource _tipCts;
        private List<string> _activeTips;
        private int _lastTipIndex = -1;

        private void OnEnable()
        {
            if (displayImage == null)
                displayImage = GetComponentInChildren<Image>();

            if (spriteList != null && displayImage != null)
            {
                var sprite = spriteList.GetRandomSprite();
                if (sprite != null)
                    displayImage.sprite = sprite;
            }
        }

        /// <summary>
        /// Starts displaying tips for the given game mode.
        /// Called by MiniGameHUDView when the connecting panel becomes active.
        /// </summary>
        public void StartTips(GameModes gameMode)
        {
            StopTips();

            if (tipsList == null || tipText == null)
                return;

            _activeTips = tipsList.GetTips(gameMode);
            if (_activeTips == null || _activeTips.Count == 0)
                return;

            _lastTipIndex = -1;

            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;

            _tipCts = new CancellationTokenSource();
            RunTipCycle(_tipCts.Token).Forget();
        }

        /// <summary>
        /// Stops the tip cycling loop and hides the tip text.
        /// </summary>
        public void StopTips()
        {
            if (_tipCts != null)
            {
                _tipCts.Cancel();
                _tipCts.Dispose();
                _tipCts = null;
            }

            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;

            _activeTips = null;
        }

        private async UniTaskVoid RunTipCycle(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                string tip = PickNextTip();
                if (tip == null) return;

                tipText.text = tip;

                // Fade in
                await FadeTipAlpha(0f, 1f, tipFadeDuration, ct);

                // Hold
                await UniTask.Delay(
                    (int)(tipDisplayDuration * 1000),
                    ignoreTimeScale: true,
                    cancellationToken: ct);

                // Fade out
                await FadeTipAlpha(1f, 0f, tipFadeDuration, ct);
            }
        }

        private async UniTask FadeTipAlpha(float from, float to, float duration, CancellationToken ct)
        {
            if (tipCanvasGroup == null)
                return;

            float elapsed = 0f;
            tipCanvasGroup.alpha = from;

            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tipCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                await UniTask.Yield(PlayerLoopTiming.PreUpdate, ct);
            }

            tipCanvasGroup.alpha = to;
        }

        private string PickNextTip()
        {
            if (_activeTips == null || _activeTips.Count == 0)
                return null;

            if (_activeTips.Count == 1)
                return _activeTips[0];

            // Pick a random index different from the last one shown
            int index;
            do
            {
                index = Random.Range(0, _activeTips.Count);
            } while (index == _lastTipIndex);

            _lastTipIndex = index;
            return _activeTips[index];
        }

        private void OnDisable()
        {
            StopTips();
        }
    }
}

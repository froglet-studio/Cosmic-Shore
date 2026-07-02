// Ported from Assets/_Scripts/Controller/Arcade/CountdownTimer.cs (controller-chain arc).
// The public contract — BeginCountdown(onComplete) runs one beat per countdown sprite
// (4 × countdownDuration seconds, unscaled time by default) then invokes onComplete —
// is preserved exactly; the DOTween/UI presentation (Image fades, scale grows, urgent
// tinting, beep SFX) is deviation-marked until the UI/DOTween arc lands. The headless
// beat loop below reproduces the original sequence timing: per sprite, a callback at
// the beat start, then fadeIn + (countdownDuration − fadeIn) = countdownDuration of
// tween time, with onComplete after the final beat.
using CosmicShore.Core;
using CosmicShore.UI;
// PORT Deviation (UI shell, restore with the DOTween arc): using DG.Tweening;
using System;
using System.Threading;
using CosmicShore.Engine;
using CosmicShore.Engine.Tasks;

namespace CosmicShore.Gameplay
{
    public class CountdownTimer : MonoBehaviour
    {
        // PORT Deviation (UI shell, restore when UI Image ports): [SerializeField] Image   countdownDisplay;
        [SerializeField] Sprite  countdown3;
        [SerializeField] Sprite  countdown2;
        [SerializeField] Sprite  countdown1;
        [SerializeField] Sprite  countdown0;
        // PORT Deviation (audio arc, restore when AudioClip ports): [SerializeField] AudioClip countdownBeep;
        [SerializeField] float     countdownDuration  = 1f;
        [SerializeField] float     countdownGrowScale = 1.5f;

        [Header("Animation (optional)")]
        // PORT Deviation (UI shell, restore when HUDAnimationSettingsSO ports): [SerializeField] private HUDAnimationSettingsSO animSettings;

        Sprite[] _sprites;
        // PORT Deviation (UI shell): Sequence _seq; — the beat loop below carries the same
        // kill semantics through a CancellationTokenSource (_seq?.Kill() → cancel).
        CancellationTokenSource _seq;

        void Awake()
        {
            EnsureSpritesInitialized();
        }

        void EnsureSpritesInitialized()
        {
            _sprites ??= new[] { countdown3, countdown2, countdown1, countdown0 };
        }

        public void BeginCountdown(Action onComplete)
        {
            EnsureSpritesInitialized();
            _seq?.Cancel(); // original: _seq?.Kill();
            _seq?.Dispose();
            _seq = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            // Original: unscaled unless animSettings overrides (animSettings is a UI-shell
            // deviation, so the default — unscaled — always applies headless).
            bool unscaled = true; // animSettings == null || animSettings.useUnscaledTime;

            RunCountdownAsync(onComplete, unscaled, _seq.Token).Forget();
        }

        /// <summary>
        /// PORT Deviation (UI shell): the DOTween sequence body — sprite swap, fade-in,
        /// scale grow with easing, urgent tinting, per-beat beep — is replaced by this
        /// timing-equivalent beat loop. Each sprite holds for countdownDuration
        /// (fadeIn + remaining grow time in the original); onComplete fires after the
        /// last beat, exactly when the original's OnComplete ran.
        /// </summary>
        async System.Threading.Tasks.Task RunCountdownAsync(Action onComplete, bool unscaled, CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < _sprites.Length; i++)
                {
                    // PORT Deviation (UI shell): countdownDisplay.sprite/scale/color writes +
                    // AudioSystem.Instance.PlaySFXClip(countdownBeep) run here in the original.
                    await GameTask.Delay(countdownDuration, unscaledTime: unscaled, cancellationToken: ct);
                }

                // PORT Deviation (UI shell): countdownDisplay.gameObject.SetActive(false);
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Killed by a newer BeginCountdown or teardown — same as a DOTween Kill.
            }
        }

        private void OnDestroy()
        {
            _seq?.Cancel(); // original: _seq?.Kill();
            _seq?.Dispose();
            _seq = null;
        }
    }
}

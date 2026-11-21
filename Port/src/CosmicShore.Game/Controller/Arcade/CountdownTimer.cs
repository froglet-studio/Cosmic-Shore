// Ported from Assets/_Scripts/Controller/Arcade/CountdownTimer.cs (controller-chain arc).
// The public contract — BeginCountdown(onComplete) runs one beat per countdown sprite
// (4 × countdownDuration seconds, unscaled time by default) then invokes onComplete —
// is preserved exactly. The non-tween presentation (Image display activation, per-beat
// sprite swap / scale reset / urgent tint, between-beat alpha reset, final hide) was
// RESTORED 2026-07-09 (UI arc B2); only the DOTween tweens (DOFade/DOScale), the beep
// SFX, and the HUDAnimationSettingsSO override source remain deviation-marked. The
// headless beat loop reproduces the original sequence timing: per sprite, a callback
// at the beat start, then fadeIn + (countdownDuration − fadeIn) = countdownDuration of
// tween time, with onComplete after the final beat.
using CosmicShore.Core;
using CosmicShore.UI;
// PORT Deviation (UI shell, restore with the DOTween arc): using DG.Tweening;
using System;
using System.Threading;
using CosmicShore.Engine;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.UI;

namespace CosmicShore.Gameplay
{
    public class CountdownTimer : MonoBehaviour
    {
        [SerializeField] Image   countdownDisplay; // (RESTORED 2026-07-09, UI arc B2)
        [SerializeField] Sprite  countdown3;
        [SerializeField] Sprite  countdown2;
        [SerializeField] Sprite  countdown1;
        [SerializeField] Sprite  countdown0;
        [SerializeField] AudioClip countdownBeep; // (RESTORED 2026-07-10, AudioSystem unit)
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

            countdownDisplay.gameObject.SetActive(true); // (RESTORED 2026-07-09, UI arc B2)

            RunCountdownAsync(onComplete, unscaled, _seq.Token).Forget();
        }

        /// <summary>
        /// PORT Deviation (UI shell): the DOTween tweens — DOFade fade-in, DOScale grow
        /// with easing — and the per-beat beep are replaced by this timing-equivalent
        /// beat loop; the beat-start writes and between-beat alpha reset are the
        /// original callback bodies (RESTORED 2026-07-09, UI arc B2). Each sprite holds
        /// for countdownDuration (fadeIn + remaining grow time in the original);
        /// onComplete fires after the last beat, exactly when the original's
        /// OnComplete ran.
        /// </summary>
        async System.Threading.Tasks.Task RunCountdownAsync(Action onComplete, bool unscaled, CancellationToken ct)
        {
            try
            {
                // animSettings is a carried deviation (always null headless), so the
                // original's null-branch defaults apply verbatim:
                // var urgentColor = animSettings ? animSettings.countdownUrgentColor : ...;
                // int urgentStart = animSettings ? animSettings.countdownUrgentStartIndex : 2;
                var urgentColor = new Color(1f, 0.3f, 0.2f, 1f);
                int urgentStart = 2;

                for (int i = 0; i < _sprites.Length; i++)
                {
                    int idx = i;
                    Sprite spr = _sprites[i];

                    // The original's beat-start AppendCallback body (RESTORED 2026-07-09,
                    // UI arc B2; beep RESTORED 2026-07-10, AudioSystem unit):
                    countdownDisplay.sprite = spr;
                    countdownDisplay.transform.localScale = Vector3.one;
                    countdownDisplay.color = idx >= urgentStart
                        ? urgentColor
                        : Color.white;
                    AudioSystem.Instance.PlaySFXClip(countdownBeep);

                    // PORT Deviation (UI shell): DOFade(1f, fadeIn) + DOScale(grow) run
                    // across this beat in the original; the hold time is identical.
                    await GameTask.Delay(countdownDuration, unscaledTime: unscaled, cancellationToken: ct);

                    // Reset alpha before next sprite (skip on last) — the original's
                    // between-beat AppendCallback body (RESTORED 2026-07-09, UI arc B2).
                    if (idx < _sprites.Length - 1)
                    {
                        var c = countdownDisplay.color;
                        countdownDisplay.color = new Color(c.r, c.g, c.b, 0f);
                    }
                }

                countdownDisplay.gameObject.SetActive(false); // (RESTORED 2026-07-09, UI arc B2)
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

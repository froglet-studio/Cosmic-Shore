using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Hacking-style typewriter: progressively reveals the target string.
    /// Before each character locks in, it rapidly scrambles random glyphs.
    /// Supports PlayIn(text) and PlayOut() (redact).
    /// </summary>
    public sealed class DoTweenTypewriterAnimator : MonoBehaviour
    {
        [Header("Target Text")]
        [SerializeField] private TMP_Text tmpText;
        [TextArea(3, 10)]
        [SerializeField] private string fullText;

        [Header("Type-In Timing")]
        [SerializeField] private float charLockDelay = 0.03f;
        [SerializeField] private int scrambleFramesPerChar = 4;
        [SerializeField] private float scrambleFrameDelay = 0.015f;

        [Header("Redact Timing")]
        [SerializeField] private float charRedactDelay = 0.02f;
        [SerializeField] private int scrambleFramesPerRedactChar = 3;
        [SerializeField] private float redactScrambleFrameDelay = 0.012f;

        [Header("Scramble Glyph Set")]
        [SerializeField] private string glyphs = "01{}[]()<>=+-/*#@$%&_^~|";

        System.Random _rng = new();

        public void SetText(string text)
        {
            fullText = text ?? string.Empty;
            SetDisplay(string.Empty);
        }

        public void ClearInstant()
        {
            fullText = string.Empty;
            SetDisplay(string.Empty);
        }

        public UniTask PlayIn(string text, CancellationToken ct)
        {
            fullText = text ?? string.Empty;
            return RunTypeIn(fullText, ct);
        }

        public UniTask PlayOut(CancellationToken ct)
        {
            var current = tmpText ? tmpText.text : string.Empty;
            return RunRedact(current, ct);
        }

        void SetDisplay(string s)
        {
            if (tmpText) tmpText.text = s;
        }

        async UniTask RunTypeIn(string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(text))
            {
                SetDisplay(string.Empty);
                return;
            }

            var locked = new StringBuilder(text.Length);
            locked.Append(' ', text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                char target = text[i];

                // Keep whitespace stable (except newline)
                if (char.IsWhiteSpace(target) && target != '\n')
                {
                    locked[i] = target;
                    SetDisplay(locked.ToString());
                    await DelaySeconds(charLockDelay, ct);
                    continue;
                }

                for (int f = 0; f < scrambleFramesPerChar; f++)
                {
                    ct.ThrowIfCancellationRequested();

                    var frame = new StringBuilder(locked.ToString());

                    frame[i] = RandomGlyph();

                    int noiseCount = Mathf.Min(3, text.Length - i - 1);
                    for (int n = 1; n <= noiseCount; n++)
                        frame[i + n] = RandomGlyph();

                    SetDisplay(frame.ToString());
                    await DelaySeconds(scrambleFrameDelay, ct);
                }

                locked[i] = target;
                SetDisplay(locked.ToString());
                await DelaySeconds(charLockDelay, ct);
            }
        }

        async UniTask RunRedact(string currentText, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(currentText))
            {
                SetDisplay(string.Empty);
                return;
            }

            var locked = new StringBuilder(currentText);

            for (int i = locked.Length - 1; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();

                char c = locked[i];

                // Skip stable whitespace (except newline)
                if (char.IsWhiteSpace(c) && c != '\n')
                {
                    locked[i] = c;
                    SetDisplay(locked.ToString());
                    await DelaySeconds(charRedactDelay, ct);
                    continue;
                }

                for (int f = 0; f < scrambleFramesPerRedactChar; f++)
                {
                    ct.ThrowIfCancellationRequested();

                    var frame = new StringBuilder(locked.ToString());
                    frame[i] = RandomGlyph();
                    SetDisplay(frame.ToString());
                    await DelaySeconds(redactScrambleFrameDelay, ct);
                }

                locked[i] = ' ';
                SetDisplay(locked.ToString());
                await DelaySeconds(charRedactDelay, ct);
            }

            SetDisplay(string.Empty);
        }

        async UniTask DelaySeconds(float seconds, CancellationToken ct)
        {
            if (seconds <= 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                return;
            }

            int ms = Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
            await UniTask.Delay(ms, cancellationToken: ct);
        }

        char RandomGlyph()
        {
            if (string.IsNullOrEmpty(glyphs))
                return '#';

            return glyphs[_rng.Next(glyphs.Length)];
        }
    }
}

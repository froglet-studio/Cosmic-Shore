using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Hacking-style typewriter: progressively reveals the target string.
    /// Before each character locks in, it rapidly scrambles random glyphs.
    /// Looping, attach directly to your text object.
    /// </summary>
    public sealed class HackingTypewriter : MonoBehaviour
    {
        [Header("Target Text")]
        [SerializeField] private TMP_Text tmpText;    
        [TextArea(3, 10)]
        [SerializeField] private string fullText;

        [Header("Timing")]
        [SerializeField] private float charLockDelay = 0.03f;     // time between locked-in characters
        [SerializeField] private int scrambleFramesPerChar = 4;   // how many scramble frames before locking each char
        [SerializeField] private float scrambleFrameDelay = 0.015f;
        [SerializeField] private float loopHoldAtEnd = 0.8f;      // hold before restarting
        [SerializeField] private bool loop = true;

        [Header("Scramble Glyph Set")]
        [SerializeField] private string glyphs = "01{}[]()<>=+-/*#@$%&_^~|";

        Coroutine _runner;

        void OnEnable()
        {
            StartRun();
        }

        void OnDisable()
        {
            StopRun();
        }

        public void SetText(string text)
        {
            fullText = text;
            Restart();
        }

        public void Restart()
        {
            StopRun();
            StartRun();
        }

        void StartRun()
        {
            if (_runner == null)
                _runner = StartCoroutine(Run());
        }

        void StopRun()
        {
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }
        }

        void SetDisplay(string s)
        {
            if (tmpText) tmpText.text = s;
        }

        System.Random _rng = new System.Random();

        IEnumerator Run()
        {
            if (string.IsNullOrEmpty(fullText))
            {
                SetDisplay(string.Empty);
                yield break;
            }

            while (true)
            {
                var locked = new StringBuilder(fullText.Length);
                locked.Append(' ', fullText.Length);

                for (int i = 0; i < fullText.Length; i++)
                {
                    // For non-printing like newline, just lock immediately
                    if (char.IsWhiteSpace(fullText[i]) && fullText[i] != '\n')
                    {
                        locked[i] = fullText[i];
                        SetDisplay(locked.ToString());
                        yield return new WaitForSeconds(charLockDelay);
                        continue;
                    }

                    // Scramble preview frames for this position
                    for (int f = 0; f < scrambleFramesPerChar; f++)
                    {
                        var frame = new StringBuilder(locked.ToString());

                        char g = glyphs[_rng.Next(glyphs.Length)];
                        frame[i] = g;

                        int noiseCount = Mathf.Min(3, fullText.Length - i - 1);
                        for (int n = 1; n <= noiseCount; n++)
                        {
                            frame[i + n] = glyphs[_rng.Next(glyphs.Length)];
                        }

                        SetDisplay(frame.ToString());
                        yield return new WaitForSeconds(scrambleFrameDelay);
                    }

                    locked[i] = fullText[i];
                    SetDisplay(locked.ToString());
                    yield return new WaitForSeconds(charLockDelay);
                }

                // End reached
                yield return new WaitForSeconds(loopHoldAtEnd);
                if (!loop) yield break;

                // Clear & loop
                SetDisplay(string.Empty);
                yield return null;
            }
        }
    }
}
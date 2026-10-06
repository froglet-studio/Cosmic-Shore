using System;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Voice for the CLAUDE page, on Windows' own speech engine (offline, no account): dictation
    /// into the message box, and replies read aloud. Elsewhere it reports itself unsupported.
    /// </summary>
    public sealed class Voice : IDisposable
    {
        readonly Action<string> _heard;
        object? _recognizer, _synth;

        public Voice(Action<string> heard) { _heard = heard; }

        public bool Supported => OperatingSystem.IsWindows();
        public bool Listening { get; private set; }
        public string? Error { get; private set; }

        public void StartListening()
        {
            if (!OperatingSystem.IsWindows() || Listening) return;
            try { StartWindows(); Listening = true; Error = null; }
            catch (Exception e) { Error = "Speech recognition is not available: " + e.Message; }
        }

        public void StopListening()
        {
            if (!OperatingSystem.IsWindows() || !Listening) return;
            Listening = false;
            try { StopWindows(); } catch { }
        }

        public void Speak(string text)
        {
            if (!OperatingSystem.IsWindows()) return;
            try { SpeakWindows(Plain(text)); } catch (Exception e) { Error = "Speech output failed: " + e.Message; }
        }

        public void StopSpeaking()
        {
            if (!OperatingSystem.IsWindows()) return;
            try { if (_synth is System.Speech.Synthesis.SpeechSynthesizer s) s.SpeakAsyncCancelAll(); } catch { }
        }

        /// <summary>What a person would read out: no code blocks, no Markdown marks, no paths spelled out.</summary>
        static string Plain(string text)
        {
            text = Regex.Replace(text, "```.*?```", " (code omitted) ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"[`*#>_|]", "");
            text = Regex.Replace(text, @"\S+/\S+", m => m.Value.Length > 30 ? "a file path" : m.Value);
            return text.Length > 1500 ? text[..1500] : text;
        }

        [SupportedOSPlatform("windows")]
        void StartWindows()
        {
            var r = new System.Speech.Recognition.SpeechRecognitionEngine();
            r.LoadGrammar(new System.Speech.Recognition.DictationGrammar());
            r.SetInputToDefaultAudioDevice();
            r.SpeechRecognized += (_, e) => { if (e.Result.Confidence > 0.3f) _heard(e.Result.Text); };
            r.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple);
            _recognizer = r;
        }

        [SupportedOSPlatform("windows")]
        void StopWindows()
        {
            if (_recognizer is System.Speech.Recognition.SpeechRecognitionEngine r)
            {
                r.RecognizeAsyncCancel();
                r.Dispose();
            }
            _recognizer = null;
        }

        [SupportedOSPlatform("windows")]
        void SpeakWindows(string text)
        {
            if (_synth is not System.Speech.Synthesis.SpeechSynthesizer s)
            {
                s = new System.Speech.Synthesis.SpeechSynthesizer();
                s.SetOutputToDefaultAudioDevice();
                _synth = s;
            }
            s.SpeakAsyncCancelAll();
            s.SpeakAsync(text);
        }

        public void Dispose()
        {
            StopListening();
            if (OperatingSystem.IsWindows() && _synth is IDisposable d) d.Dispose();
        }
    }
}

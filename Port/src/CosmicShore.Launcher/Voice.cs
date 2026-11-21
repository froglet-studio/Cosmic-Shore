using System;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Voice for the CLAUDE page: dictation into the message box, and replies read aloud.
    /// Dictation uses Windows' modern recognizer (<c>Windows.Media.SpeechRecognition</c>, the cloud
    /// model behind Win + H), which knows words like "Unity", "prism" and "vessel". That needs
    /// Settings > Privacy &amp; security > Speech > Online speech recognition. When it is off, or the
    /// launcher was built without the Windows target, the mic falls back to the old offline engine
    /// (System.Speech) and says why in its tooltip. Elsewhere voice reports itself unsupported.
    /// </summary>
    public sealed class Voice : IDisposable
    {
        /// <summary>The old engine guesses at anything it doesn't know; below this it is mostly wrong.</summary>
        const float LegacyMinConfidence = 0.5f;

        readonly Action<string> _heard;
        object? _recognizer, _synth;
        int _generation;

        public Voice(Action<string> heard) { _heard = heard; }

        public bool Supported => OperatingSystem.IsWindows();
        public bool Listening { get; private set; }
        public string? Error { get; private set; }

        /// <summary>Which recognizer is listening, and why it isn't the modern one when it isn't.</summary>
        public string? Engine { get; private set; }

        public void StartListening()
        {
            if (!OperatingSystem.IsWindows() || Listening) return;
            Listening = true; Error = null; Engine = "starting";
            _ = StartAsync(++_generation);
        }

        public void StopListening()
        {
            if (!OperatingSystem.IsWindows() || !Listening) return;
            Listening = false;
            _generation++;
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

        /// <summary>The modern recognizer first; on any failure the old one, with the reason kept.</summary>
        async Task StartAsync(int generation)
        {
            if (!OperatingSystem.IsWindows()) return;
            string? why = null;
#if WINDOWS10_0_19041_0_OR_GREATER
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                try
                {
                    await StartModern(generation);
                    return;
                }
                catch (Exception e) { why = ModernFailure(e); }
            }
            else why = "this Windows is older than 10 (2004)";
#else
            await Task.CompletedTask;
            why = "this Amoebius was built without the Windows 10 target";
#endif
            if (generation != _generation) return;
            try
            {
                StartLegacy();
                Engine = "old offline engine (less accurate): " + why;
            }
            catch (Exception e)
            {
                Listening = false; Engine = null;
                Error = "Speech recognition is not available: " + why + "; the old engine failed too: " + e.Message;
            }
        }

#if WINDOWS10_0_19041_0_OR_GREATER
        [SupportedOSPlatform("windows10.0.19041.0")]
        async Task StartModern(int generation)
        {
            var r = new Windows.Media.SpeechRecognition.SpeechRecognizer();
            try
            {
                r.Constraints.Add(new Windows.Media.SpeechRecognition.SpeechRecognitionTopicConstraint(
                    Windows.Media.SpeechRecognition.SpeechRecognitionScenario.Dictation, "dictation"));
                var compiled = await r.CompileConstraintsAsync().AsTask();
                if (compiled.Status != Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success)
                    throw new InvalidOperationException("dictation could not start (" + compiled.Status + ")");

                var session = r.ContinuousRecognitionSession;
                // The default stops after a few seconds of silence; the mic button decides instead.
                session.AutoStopSilenceTimeout = TimeSpan.FromHours(4);
                session.ResultGenerated += (_, e) =>
                {
                    var res = e.Result;
                    if (res.Status == Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success &&
                        res.Confidence != Windows.Media.SpeechRecognition.SpeechRecognitionConfidence.Rejected &&
                        !string.IsNullOrWhiteSpace(res.Text))
                        _heard(res.Text.Trim());
                };
                session.Completed += (_, e) =>
                {
                    if (!ReferenceEquals(_recognizer, r) || !Listening) return;
                    Listening = false; Engine = null;
                    if (e.Status != Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success)
                        Error = "Dictation stopped: " + e.Status;
                };

                if (generation != _generation) { r.Dispose(); return; }
                _recognizer = r;
                await session.StartAsync().AsTask();
                Engine = "Windows online dictation";
            }
            catch
            {
                if (ReferenceEquals(_recognizer, r)) _recognizer = null;
                r.Dispose();
                throw;
            }
        }

        /// <summary>The failures a person can fix, in the words of the setting that fixes them.</summary>
        static string ModernFailure(Exception e) => unchecked((uint)e.HResult) switch
        {
            0x80045509 => "Online speech recognition is off (Settings > Privacy & security > Speech)",
            0x80070005 => "microphone access is off for desktop apps (Settings > Privacy & security > Microphone)",
            _ => "Windows dictation failed (" + e.Message + ")",
        };

        [SupportedOSPlatform("windows10.0.19041.0")]
        static async Task StopModern(Windows.Media.SpeechRecognition.SpeechRecognizer r)
        {
            try { await r.ContinuousRecognitionSession.StopAsync().AsTask(); } catch { }
            r.Dispose();
        }
#endif

        [SupportedOSPlatform("windows")]
        void StartLegacy()
        {
            var r = new System.Speech.Recognition.SpeechRecognitionEngine();
            r.LoadGrammar(new System.Speech.Recognition.DictationGrammar());
            r.SetInputToDefaultAudioDevice();
            r.SpeechRecognized += (_, e) => { if (e.Result.Confidence >= LegacyMinConfidence) _heard(e.Result.Text); };
            r.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple);
            _recognizer = r;
        }

        [SupportedOSPlatform("windows")]
        void StopWindows()
        {
            var rec = _recognizer;
            _recognizer = null;
            Engine = null;
#if WINDOWS10_0_19041_0_OR_GREATER
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) &&
                rec is Windows.Media.SpeechRecognition.SpeechRecognizer modern)
            {
                _ = StopModern(modern);
                return;
            }
#endif
            if (rec is System.Speech.Recognition.SpeechRecognitionEngine r)
            {
                r.RecognizeAsyncCancel();
                r.Dispose();
            }
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

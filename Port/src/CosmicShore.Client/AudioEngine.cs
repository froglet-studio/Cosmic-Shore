using System;
using Silk.NET.OpenAL;

namespace CosmicShore.Client
{
    /// <summary>
    /// Fully procedural audio: every clip is synthesized PCM (no asset files), played
    /// through OpenAL Soft. Construction is fail-safe — no audio device (or no native
    /// lib) just disables the engine silently, the game runs identically.
    /// </summary>
    public sealed unsafe class AudioEngine : IDisposable
    {
        const int SampleRate = 44100;

        AL _al;
        ALContext _alc;
        Device* _device;
        Context* _context;
        public bool Enabled { get; private set; }

        uint _humSource, _boostSource, _skimSource, _driftSource;
        uint _chime, _rivalChime, _beep, _goBeep, _winJingle, _loseTone;
        readonly uint[] _oneShotSources = new uint[6];
        int _nextOneShot;

        public AudioEngine(bool disabled = false)
        {
            if (disabled) return;
            try
            {
                _alc = ALContext.GetApi(soft: true);
                _al = AL.GetApi(soft: true);
                _device = _alc.OpenDevice("");
                if (_device == null) return;
                _context = _alc.CreateContext(_device, null);
                _alc.MakeContextCurrent(_context);

                _chime = MakeBuffer(Chime(880f, 0.35f, 0.5f));
                _rivalChime = MakeBuffer(Chime(587f, 0.3f, 0.22f));
                _beep = MakeBuffer(Tone(440f, 0.12f, 0.35f));
                _goBeep = MakeBuffer(Tone(880f, 0.22f, 0.4f));
                _winJingle = MakeBuffer(Jingle(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.16f, 0.45f));
                _loseTone = MakeBuffer(Slide(392f, 277f, 0.6f, 0.4f));

                _humSource = MakeLoopingSource(MakeBuffer(HumLoop()), 0.0f);
                _boostSource = MakeLoopingSource(MakeBuffer(BoostLoop()), 0.0f);
                _skimSource = MakeLoopingSource(MakeBuffer(SkimLoop()), 0.0f);
                _driftSource = MakeLoopingSource(MakeBuffer(DriftLoop()), 0.0f);
                _al.SourcePlay(_humSource);
                _al.SourcePlay(_boostSource);
                _al.SourcePlay(_skimSource);
                _al.SourcePlay(_driftSource);

                for (int i = 0; i < _oneShotSources.Length; i++)
                    _oneShotSources[i] = _al.GenSource();

                Enabled = true;
            }
            catch
            {
                Enabled = false; // no device / no native lib → silent mode
            }
        }

        // ── synthesis ───────────────────────────────────────────────

        static short[] Alloc(float seconds) => new short[(int)(SampleRate * seconds)];

        static short Clip(float v) => (short)(Math.Clamp(v, -1f, 1f) * short.MaxValue);

        /// <summary>Crystal chime: fundamental + sparkle harmonic, exponential decay.</summary>
        static short[] Chime(float freq, float seconds, float gain)
        {
            var data = Alloc(seconds);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = MathF.Exp(-t * 9f);
                float v = MathF.Sin(MathF.Tau * freq * t) * 0.7f
                        + MathF.Sin(MathF.Tau * freq * 2.01f * t) * 0.25f
                        + MathF.Sin(MathF.Tau * freq * 3.02f * t) * 0.12f;
                data[i] = Clip(v * envelope * gain);
            }
            return data;
        }

        static short[] Tone(float freq, float seconds, float gain)
        {
            var data = Alloc(seconds);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = MathF.Min(1f, t * 60f) * MathF.Exp(-t * 6f);
                data[i] = Clip(MathF.Sin(MathF.Tau * freq * t) * envelope * gain);
            }
            return data;
        }

        static short[] Jingle(float[] notes, float noteSeconds, float gain)
        {
            var data = Alloc(noteSeconds * notes.Length + 0.3f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * noteSeconds * SampleRate);
                for (int i = 0; i < (int)(SampleRate * (noteSeconds + 0.25f)) && start + i < data.Length; i++)
                {
                    float t = i / (float)SampleRate;
                    float envelope = MathF.Exp(-t * 7f);
                    float v = MathF.Sin(MathF.Tau * notes[n] * t) * 0.7f
                            + MathF.Sin(MathF.Tau * notes[n] * 2f * t) * 0.2f;
                    data[start + i] = Clip(data[start + i] / (float)short.MaxValue + v * envelope * gain);
                }
            }
            return data;
        }

        static short[] Slide(float from, float to, float seconds, float gain)
        {
            var data = Alloc(seconds);
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                float freq = from + (to - from) * t;
                phase += MathF.Tau * freq / SampleRate;
                float envelope = MathF.Min(1f, i / (SampleRate * 0.02f)) * (1f - t * 0.6f);
                data[i] = Clip(MathF.Sin(phase) * envelope * gain);
            }
            return data;
        }

        /// <summary>1s engine hum loop: low fundamental + octave, integer cycles = seamless.</summary>
        static short[] HumLoop()
        {
            var data = Alloc(1f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float v = MathF.Sin(MathF.Tau * 82f * t) * 0.55f
                        + MathF.Sin(MathF.Tau * 164f * t) * 0.25f
                        + MathF.Sin(MathF.Tau * 246f * t) * 0.1f;
                data[i] = Clip(v * 0.5f);
            }
            return data;
        }

        /// <summary>0.5s boost loop: bright saw stack + airy noise, seamless.</summary>
        static short[] BoostLoop()
        {
            var data = Alloc(0.5f);
            var rng = new Random(7);
            float noiseState = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float saw = 2f * (t * 110f % 1f) - 1f;
                noiseState = noiseState * 0.92f + ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
                data[i] = Clip((saw * 0.3f + noiseState * 6f * 0.35f) * 0.5f);
            }
            return data;
        }

        /// <summary>1s skim shimmer: glassy detuned fifths tremolo — the trail-charging glow.</summary>
        static short[] SkimLoop()
        {
            var data = Alloc(1f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float tremolo = 0.6f + 0.4f * MathF.Sin(MathF.Tau * 6f * t);
                float v = MathF.Sin(MathF.Tau * 1318.5f * t) * 0.4f      // E6
                        + MathF.Sin(MathF.Tau * 1975.5f * t) * 0.25f     // B6
                        + MathF.Sin(MathF.Tau * 1320.8f * t) * 0.2f;     // detune beat
                data[i] = Clip(v * tremolo * 0.35f);
            }
            return data;
        }

        /// <summary>0.5s drift rush: band-passed noise wash, seamless — tyres-on-starlight.</summary>
        static short[] DriftLoop()
        {
            var data = Alloc(0.5f);
            var rng = new Random(13);
            float lp = 0f, bp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += 0.12f * (white - lp);       // low-pass
                bp += 0.3f * (lp - bp);           // soften further
                data[i] = Clip((lp - bp) * 5.5f * 0.5f);
            }
            return data;
        }

        // ── OpenAL plumbing ─────────────────────────────────────────

        uint MakeBuffer(short[] pcm)
        {
            uint buffer = _al.GenBuffer();
            fixed (short* p = pcm)
                _al.BufferData(buffer, BufferFormat.Mono16, p, pcm.Length * sizeof(short), SampleRate);
            return buffer;
        }

        uint MakeLoopingSource(uint buffer, float gain)
        {
            uint source = _al.GenSource();
            _al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);
            _al.SetSourceProperty(source, SourceBoolean.Looping, true);
            _al.SetSourceProperty(source, SourceFloat.Gain, gain);
            return source;
        }

        void PlayOneShot(uint buffer, float gain)
        {
            if (!Enabled) return;
            uint source = _oneShotSources[_nextOneShot];
            _nextOneShot = (_nextOneShot + 1) % _oneShotSources.Length;
            _al.SourceStop(source);
            _al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);
            _al.SetSourceProperty(source, SourceFloat.Gain, gain);
            _al.SourcePlay(source);
        }

        // ── game-facing API ─────────────────────────────────────────

        /// <summary>Engine bed: hum pitch/volume scale with speed; boost layer fades with boosting.</summary>
        public void SetEngineState(float speed01, bool boosting)
        {
            if (!Enabled) return;
            _al.SetSourceProperty(_humSource, SourceFloat.Gain, 0.18f + speed01 * 0.2f);
            _al.SetSourceProperty(_humSource, SourceFloat.Pitch, 0.85f + speed01 * 0.5f);
            _al.SetSourceProperty(_boostSource, SourceFloat.Gain, boosting ? 0.3f : 0f);
        }

        public void CrystalChime(bool player) => PlayOneShot(player ? _chime : _rivalChime, player ? 0.8f : 0.45f);
        public void CountdownBeep() => PlayOneShot(_beep, 0.7f);
        public void GoBeep() => PlayOneShot(_goBeep, 0.8f);
        public void FinishJingle(bool won) => PlayOneShot(won ? _winJingle : _loseTone, 0.9f);

        /// <summary>Trail-skim shimmer scales with contact strength; drift rush with trigger pull.</summary>
        public void SetSkimDriftState(float skim01, float drift01)
        {
            if (!Enabled) return;
            _al.SetSourceProperty(_skimSource, SourceFloat.Gain, skim01 * 0.35f);
            _al.SetSourceProperty(_driftSource, SourceFloat.Gain, drift01 * 0.4f);
            _al.SetSourceProperty(_driftSource, SourceFloat.Pitch, 0.8f + drift01 * 0.5f);
        }

        public void Dispose()
        {
            if (!Enabled) return;
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
            _alc.CloseDevice(_device);
            Enabled = false;
        }
    }
}

using System;
using FMOD;
using FMODUnity;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Core
{
    /// <summary>
    /// Real-time loudness + brightness tap on an FMOD bus, feeding the
    /// continuous haptic bed.
    ///
    /// Reads INPUT metering (per-mix-block RMS, pre-fader) from the bus channel
    /// group's head DSP — the same DSP FMOD's own debug overlay meters
    /// (Plugins/FMOD/src/RuntimeManager.cs UpdateDebugText) — and optionally
    /// inserts one FFT DSP at the head to read the SPECTRAL_CENTROID float
    /// parameter (no spectrum marshaling). Input metering is deliberate: the
    /// head DSP is the fader AudioSystem.ApplySfxBus drives with the SFX
    /// slider, so pre-fader keeps the haptic bed from scaling with the audio
    /// volume knob (haptics have their own setting). Muted SFX still silences
    /// the bed naturally — muted one-shots are never created and the
    /// continuous emitters zero their instance volume, so no signal reaches
    /// the bus at all. Because every SFX event routes through this bus while
    /// music runs on the legacy Unity AudioSource path, the signal is exactly
    /// "what the game's SFX mix is doing right now", spatial attenuation and
    /// mix throttles included.
    ///
    /// Resolution is lazy with a retry cooldown: FMOD banks (and thus the bus)
    /// load after scene start, mirroring AudioSystem.ApplySfxBusWhenReady.
    /// Main-thread only, like all FMOD Studio API use in the project.
    /// </summary>
    public sealed class FmodSfxBusMeter : IDisposable
    {
        const float RetryIntervalSeconds = 1f;

        readonly string _busPath;
        readonly bool _useFft;
        readonly int _fftWindowSize;

        FMOD.Studio.Bus _bus;
        ChannelGroup _channelGroup;
        DSP _headDsp;
        DSP _fftDsp;
        bool _resolved;
        bool _lockedChannelGroup;
        float _nextRetryTime;

        public FmodSfxBusMeter(string busPath, bool useFft, int fftWindowSize)
        {
            _busPath = string.IsNullOrEmpty(busPath) ? "bus:/" : busPath;
            _useFft = useFft;
            _fftWindowSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(fftWindowSize, 128, 4096));
        }

        public bool IsResolved => _resolved;

        /// <summary>
        /// Reads the current bus output level and spectral centroid. Returns
        /// false (and zeroes) while the bus/banks are still loading or after an
        /// FMOD teardown; callers should treat that as silence.
        /// </summary>
        public bool TryRead(out float rms, out float centroidHz)
        {
            rms = 0f;
            centroidHz = 0f;

            if (!_resolved && !TryResolve())
                return false;

            if (!_headDsp.hasHandle())
            {
                Invalidate();
                return false;
            }

            if (_headDsp.getMeteringInfo(out DSP_METERING_INFO input, IntPtr.Zero) != RESULT.OK)
            {
                Invalidate();
                return false;
            }

            int channels = Mathf.Min(input.numchannels, 32);
            if (channels > 0)
            {
                float sum = 0f;
                for (int i = 0; i < channels; i++)
                    sum += input.rmslevel[i] * input.rmslevel[i];
                rms = Mathf.Sqrt(sum / channels);
            }

            if (_useFft && _fftDsp.hasHandle())
                _fftDsp.getParameterFloat((int)DSP_FFT.SPECTRAL_CENTROID, out centroidHz);

            return true;
        }

        bool TryResolve()
        {
            if (Time.unscaledTime < _nextRetryTime)
                return false;
            _nextRetryTime = Time.unscaledTime + RetryIntervalSeconds;

            try
            {
                if (!RuntimeManager.IsInitialized)
                    return false;

                _bus = RuntimeManager.GetBus(_busPath);
                if (!_bus.isValid())
                    return false;

                // Pin the channel group so it exists even while the bus is
                // silent (FMOD frees unlocked groups of inaudible buses).
                if (!_lockedChannelGroup && _bus.lockChannelGroup() == RESULT.OK)
                    _lockedChannelGroup = true;

                if (_bus.getChannelGroup(out _channelGroup) != RESULT.OK || !_channelGroup.hasHandle())
                    return false;

                if (_channelGroup.getDSP(CHANNELCONTROL_DSP_INDEX.HEAD, out _headDsp) != RESULT.OK || !_headDsp.hasHandle())
                    return false;

                // Input metering = pre-fader (see class doc for why).
                _headDsp.setMeteringEnabled(true, false);

                if (_useFft)
                    TryAttachFft();

                _resolved = true;
                return true;
            }
            catch (BusNotFoundException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FmodSfxBusMeter] Failed to resolve bus '{_busPath}': {ex.Message}");
                return false;
            }
        }

        void TryAttachFft()
        {
            if (_fftDsp.hasHandle())
                return;

            if (RuntimeManager.CoreSystem.createDSPByType(DSP_TYPE.FFT, out _fftDsp) != RESULT.OK)
                return;

            _fftDsp.setParameterInt((int)DSP_FFT.WINDOWSIZE, _fftWindowSize);
            _fftDsp.setParameterInt((int)DSP_FFT.WINDOW, (int)DSP_FFT_WINDOW_TYPE.HANNING);
            _fftDsp.setParameterInt((int)DSP_FFT.DOWNMIX, (int)DSP_FFT_DOWNMIX_TYPE.MONO);

            if (_channelGroup.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, _fftDsp) != RESULT.OK)
            {
                _fftDsp.release();
                _fftDsp.clearHandle();
            }
        }

        void Invalidate()
        {
            // A read failed: either FMOD tore down (bank unload / system
            // restart) or the bus group was rebuilt under us. Best-effort
            // cleanup first — on a live system this prevents leaking the FFT
            // DSP (a re-resolve would otherwise stack a second one) and the
            // channel-group lock; on a dead system the calls fail harmlessly
            // (the wrapper returns error codes, it does not throw).
            try
            {
                if (_fftDsp.hasHandle())
                {
                    if (_channelGroup.hasHandle())
                        _channelGroup.removeDSP(_fftDsp);
                    _fftDsp.release();
                }
                if (_lockedChannelGroup && _bus.isValid())
                    _bus.unlockChannelGroup();
            }
            catch (Exception)
            {
                // FMOD may be mid-shutdown; nothing to salvage.
            }

            _resolved = false;
            _lockedChannelGroup = false;
            _headDsp.clearHandle();
            _fftDsp.clearHandle();
            _channelGroup.clearHandle();
        }

        public void Dispose()
        {
            try
            {
                if (_fftDsp.hasHandle())
                {
                    if (_channelGroup.hasHandle())
                        _channelGroup.removeDSP(_fftDsp);
                    _fftDsp.release();
                }
                if (_headDsp.hasHandle())
                    _headDsp.setMeteringEnabled(false, false);
                if (_lockedChannelGroup && _bus.isValid())
                    _bus.unlockChannelGroup();
            }
            catch (Exception)
            {
                // FMOD may already be shut down during application exit.
            }

            _fftDsp.clearHandle();
            _headDsp.clearHandle();
            _channelGroup.clearHandle();
            _resolved = false;
            _lockedChannelGroup = false;
        }
    }
}

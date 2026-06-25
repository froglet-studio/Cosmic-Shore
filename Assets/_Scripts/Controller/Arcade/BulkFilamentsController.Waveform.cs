using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        const int FilamentWaveformPointCount = 73;
        const int MusicWaveformSampleCount = 512;

        readonly List<LineRenderer> _filamentWaveforms = new();
        readonly float[] _musicWaveformSamples = new float[MusicWaveformSampleCount];
        float _waveformScrollDistance;
        float _waveformSmoothedPeak = 0.08f;
        float _waveformEnergy;

        void CreateFilamentWaveform(FilamentRuntime filament)
        {
            float beamWidth = FilamentBeamWidth(filament);
            var waveform = MakeLine(
                $"Filament {filament.Index:00} Live Waveform",
                FilamentWaveformPointCount,
                beamWidth * 2f,
                _whiteEnergyMaterial);
            waveform.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waveform.receiveShadows = false;
            _filamentWaveforms.Add(waveform);
        }

        void ResetFilamentWaveforms()
        {
            _filamentWaveforms.Clear();
            _waveformScrollDistance = 0f;
            _waveformSmoothedPeak = 0.08f;
            _waveformEnergy = 0f;
        }

        void AnimateFilamentWaveforms()
        {
            if (_filamentWaveforms.Count == 0 || _filaments.Count == 0)
                return;

            CaptureMusicWaveform();
            _waveformScrollDistance += Mathf.Max(minimumSpeed, _speed) * 8f * Time.deltaTime;
            if (_waveformScrollDistance > tubeRadius * 100f)
                _waveformScrollDistance = Mathf.Repeat(_waveformScrollDistance, tubeRadius);

            int count = Mathf.Min(_filaments.Count, _filamentWaveforms.Count);
            for (int i = 0; i < count; i++)
                UpdateFilamentWaveform(_filaments[i], _filamentWaveforms[i]);
        }

        void CaptureMusicWaveform()
        {
            if (!_musicSource || !_musicSource.isPlaying)
            {
                ClearWaveformSamples();
                _waveformEnergy = Mathf.Lerp(_waveformEnergy, 0f, 0.18f);
                return;
            }

            try
            {
                _musicSource.GetOutputData(_musicWaveformSamples, 0);
            }
            catch
            {
                ClearWaveformSamples();
                return;
            }

            float peak = 0f;
            for (int i = 0; i < _musicWaveformSamples.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(_musicWaveformSamples[i]));

            _waveformSmoothedPeak = Mathf.Lerp(_waveformSmoothedPeak, Mathf.Max(0.08f, peak), 0.28f);
            _waveformEnergy = Mathf.Lerp(_waveformEnergy, Mathf.Clamp01(peak * 7f), 0.24f);
        }

        void ClearWaveformSamples()
        {
            for (int i = 0; i < _musicWaveformSamples.Length; i++)
                _musicWaveformSamples[i] = 0f;
        }

        void UpdateFilamentWaveform(FilamentRuntime filament, LineRenderer waveform)
        {
            if (!waveform)
                return;

            waveform.sharedMaterial = filament.Beam ? filament.Beam.sharedMaterial : _whiteEnergyMaterial;

            float beamWidth = FilamentBeamWidth(filament);
            waveform.widthMultiplier = beamWidth * Mathf.Lerp(1.85f, 2.15f, _waveformEnergy);

            Vector3 lateral = Vector3.ProjectOnPlane(filament.Side, Vector3.up);
            if (lateral.sqrMagnitude < 0.01f)
                lateral = filament.Side;
            lateral.Normalize();

            Vector3 topOffset = filament.Up * (beamWidth * 2.6f + 0.3f);
            float amplitude = beamWidth * 2f;
            float scroll01 = Mathf.Repeat(_waveformScrollDistance / Mathf.Max(1f, filament.Length), 1f);

            for (int i = 0; i < waveform.positionCount; i++)
            {
                float axis01 = i / (float)(waveform.positionCount - 1);
                float sample01 = Mathf.Repeat(axis01 + scroll01 + filament.Index * 0.071f, 1f);
                float sample = SampleMusicWaveform(sample01);
                float envelope = 0.35f + Mathf.Sin(axis01 * Mathf.PI) * 0.65f;
                Vector3 baseline = FilamentSurfacePoint(filament, axis01);
                waveform.SetPosition(i, baseline + topOffset + lateral * (sample * amplitude * envelope));
            }
        }

        float SampleMusicWaveform(float sample01)
        {
            float rawIndex = sample01 * (_musicWaveformSamples.Length - 1);
            int index = Mathf.Clamp(Mathf.FloorToInt(rawIndex), 0, _musicWaveformSamples.Length - 1);
            int next = (index + 1) % _musicWaveformSamples.Length;
            float sample = Mathf.Lerp(_musicWaveformSamples[index], _musicWaveformSamples[next], rawIndex - index);
            return Mathf.Clamp(sample / Mathf.Max(0.08f, _waveformSmoothedPeak), -1f, 1f);
        }

        float FilamentBeamWidth(FilamentRuntime filament)
        {
            return filament.Beam ? filament.Beam.widthMultiplier : Mathf.Max(0.72f, tubeRadius * 0.0032f);
        }
    }
}

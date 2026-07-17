using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor tooling for the audio-matched haptics system.
    ///
    ///  - Create Config Asset: authors Assets/Resources/AudioHapticsConfig.asset
    ///    pre-populated with the measured code defaults so every category is
    ///    inspectable and tunable.
    ///  - Bake Envelopes From FMOD Source Audio: re-analyzes the FMOD Studio
    ///    project's source wavs ("Cosmic Shore/Assets/&lt;file&gt;.wav" next to the
    ///    Unity project) with HapticWaveformAnalyzer and writes fresh envelopes
    ///    into the config asset. Which wav feeds which category comes from each
    ///    spec's bakedFrom field (seeded by the defaults; editable per entry).
    ///  - Log Status: quick health dump of the runtime chain.
    /// </summary>
    public static class AudioHapticsBaker
    {
        const string ConfigAssetPath = "Assets/Resources/AudioHapticsConfig.asset";
        const string FmodProjectAssetsFolder = "Cosmic Shore/Assets";

        [MenuItem("Tools/Cosmic Shore/Audio Haptics/Create Config Asset")]
        public static void CreateConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioHapticsConfigSO>(ConfigAssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                Debug.Log($"[AudioHapticsBaker] Config already exists at {ConfigAssetPath}.");
                return;
            }

            var config = ScriptableObject.CreateInstance<AudioHapticsConfigSO>();
            PopulateFromDefaults(config);

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigAssetPath)!);
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log($"[AudioHapticsBaker] Created {ConfigAssetPath} with measured defaults for every category.");
        }

        [MenuItem("Tools/Cosmic Shore/Audio Haptics/Bake Envelopes From FMOD Source Audio")]
        public static void BakeFromSourceAudio()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioHapticsConfigSO>(ConfigAssetPath);
            if (config == null)
            {
                CreateConfigAsset();
                config = AssetDatabase.LoadAssetAtPath<AudioHapticsConfigSO>(ConfigAssetPath);
                if (config == null) return;
            }

            string sourceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FmodProjectAssetsFolder));
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogWarning($"[AudioHapticsBaker] FMOD source folder not found at '{sourceRoot}' — envelopes keep their current values.");
                return;
            }

            int baked = 0, skipped = 0;
            for (int i = 0; i < config.gameplayTransients.Count; i++)
                if (TryBakeSpec(config.gameplayTransients[i].spec, sourceRoot)) baked++; else skipped++;
            for (int i = 0; i < config.menuTransients.Count; i++)
                if (TryBakeSpec(config.menuTransients[i].spec, sourceRoot)) baked++; else skipped++;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioHapticsBaker] Bake complete: {baked} envelopes re-measured, {skipped} kept (no/unreadable source file). Source: {sourceRoot}");
        }

        [MenuItem("Tools/Cosmic Shore/Audio Haptics/Log Status")]
        public static void LogStatus()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioHapticsConfigSO>(ConfigAssetPath);
            var orchestrator = UnityEngine.Object.FindFirstObjectByType<AudioHapticsOrchestrator>();
            Debug.Log(
                "[AudioHapticsBaker] Status:\n" +
                $"  Config asset: {(config != null ? ConfigAssetPath : "none (runtime uses AudioHapticsBakedDefaults)")}\n" +
                $"  Gameplay entries: {(config != null ? config.gameplayTransients.Count : 0)} / menu entries: {(config != null ? config.menuTransients.Count : 0)}\n" +
                $"  Orchestrator in scene: {(orchestrator != null ? $"yes (active={orchestrator.HapticsActive})" : "no (created by AudioSystem at play time)")}\n" +
                "  Reminder: mobile vibration only fires on device builds; in the editor, haptics play through a connected gamepad's rumble motors.");
        }

        /// <summary>
        /// Seeds the config with every category's measured code default so the
        /// asset is a complete, inspectable picture of the haptic mix.
        /// </summary>
        static void PopulateFromDefaults(AudioHapticsConfigSO config)
        {
            config.gameplayTransients.Clear();
            foreach (GameplaySFXCategory category in Enum.GetValues(typeof(GameplaySFXCategory)))
                config.gameplayTransients.Add(new AudioHapticsConfigSO.GameplayEntry
                {
                    category = category,
                    // Clone: the defaults are memoized shared instances.
                    spec = AudioHapticsBakedDefaults.ForGameplay(category).Clone(),
                });

            config.menuTransients.Clear();
            foreach (MenuAudioCategory category in Enum.GetValues(typeof(MenuAudioCategory)))
                config.menuTransients.Add(new AudioHapticsConfigSO.MenuEntry
                {
                    category = category,
                    spec = AudioHapticsBakedDefaults.ForMenu(category).Clone(),
                });

            config.skimPulse = AudioHapticsBakedDefaults.SkimPulse().Clone();
        }

        static bool TryBakeSpec(HapticTransientSpec spec, string sourceRoot)
        {
            if (spec == null || string.IsNullOrEmpty(spec.bakedFrom) || spec.bakedFrom == "fallback")
                return false;

            string path = Path.Combine(sourceRoot, spec.bakedFrom);
            if (!File.Exists(path))
                return false;

            float[] mono;
            int sampleRate;
            try
            {
                if (!WavFileReader.TryRead(path, out mono, out sampleRate))
                    return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioHapticsBaker] Failed to read '{spec.bakedFrom}': {ex.Message}");
                return false;
            }

            // Preserve the authored duration cap: re-trim the fresh analysis to
            // the existing envelope's length so gameplay pacing decisions stick.
            float previousDuration = HapticPatternBuilder.Duration(spec.envelope);

            var settings = HapticWaveformAnalyzer.Settings.Default;
            var envelope = HapticWaveformAnalyzer.Analyze(mono, sampleRate, settings);
            if (envelope.Count == 0)
                return false;

            if (previousDuration > 0.01f)
                envelope = HapticPatternBuilder.Trim(envelope, previousDuration);

            spec.envelope = envelope;
            return true;
        }

        /// <summary>
        /// Minimal chunk-walking RIFF/WAVE reader for the baker: PCM 16/24/32
        /// and IEEE float 32, incl. WAVE_FORMAT_EXTENSIBLE, downmixed to mono.
        /// The DAW-exported source files carry LIST/bext chunks before fmt, so
        /// fixed-offset parsing is not an option.
        /// </summary>
        static class WavFileReader
        {
            public static bool TryRead(string path, out float[] mono, out int sampleRate)
            {
                mono = null;
                sampleRate = 0;

                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 12 ||
                    data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F' ||
                    data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
                    return false;

                int audioFormat = 0, channels = 0, bitsPerSample = 0;
                int dataOffset = -1, dataLength = 0;

                int pos = 12;
                while (pos + 8 <= data.Length)
                {
                    uint chunkSize = BitConverter.ToUInt32(data, pos + 4);
                    int body = pos + 8;
                    if (HasChunkId(data, pos, "fmt ") && body + 16 <= data.Length)
                    {
                        audioFormat = BitConverter.ToUInt16(data, body);
                        channels = BitConverter.ToUInt16(data, body + 2);
                        sampleRate = (int)BitConverter.ToUInt32(data, body + 4);
                        bitsPerSample = BitConverter.ToUInt16(data, body + 14);
                        // WAVE_FORMAT_EXTENSIBLE: the real format lives in the SubFormat GUID's first two bytes.
                        if (audioFormat == 0xFFFE && body + 26 <= data.Length)
                            audioFormat = BitConverter.ToUInt16(data, body + 24);
                    }
                    else if (HasChunkId(data, pos, "data"))
                    {
                        dataOffset = body;
                        dataLength = (int)Math.Min(chunkSize, (uint)(data.Length - body));
                    }
                    pos = body + (int)chunkSize + ((int)chunkSize & 1);
                }

                if (channels <= 0 || sampleRate <= 0 || dataOffset < 0)
                    return false;

                float[] samples = audioFormat switch
                {
                    1 when bitsPerSample == 16 => ReadPcm16(data, dataOffset, dataLength),
                    1 when bitsPerSample == 24 => ReadPcm24(data, dataOffset, dataLength),
                    1 when bitsPerSample == 32 => ReadPcm32(data, dataOffset, dataLength),
                    3 when bitsPerSample == 32 => ReadFloat32(data, dataOffset, dataLength),
                    _ => null,
                };
                if (samples == null)
                    return false;

                mono = HapticWaveformAnalyzer.InterleavedToMono(samples, channels);
                return true;
            }

            static bool HasChunkId(byte[] data, int pos, string id) =>
                data[pos] == id[0] && data[pos + 1] == id[1] && data[pos + 2] == id[2] && data[pos + 3] == id[3];

            static float[] ReadPcm16(byte[] data, int offset, int length)
            {
                int count = length / 2;
                var result = new float[count];
                for (int i = 0; i < count; i++)
                    result[i] = BitConverter.ToInt16(data, offset + i * 2) / 32768f;
                return result;
            }

            static float[] ReadPcm24(byte[] data, int offset, int length)
            {
                int count = length / 3;
                var result = new float[count];
                for (int i = 0; i < count; i++)
                {
                    int b = offset + i * 3;
                    int value = data[b] | (data[b + 1] << 8) | ((sbyte)data[b + 2] << 16);
                    result[i] = value / 8388608f;
                }
                return result;
            }

            static float[] ReadPcm32(byte[] data, int offset, int length)
            {
                int count = length / 4;
                var result = new float[count];
                for (int i = 0; i < count; i++)
                    result[i] = BitConverter.ToInt32(data, offset + i * 4) / 2147483648f;
                return result;
            }

            static float[] ReadFloat32(byte[] data, int offset, int length)
            {
                int count = length / 4;
                var result = new float[count];
                for (int i = 0; i < count; i++)
                    result[i] = BitConverter.ToSingle(data, offset + i * 4);
                return result;
            }
        }
    }
}

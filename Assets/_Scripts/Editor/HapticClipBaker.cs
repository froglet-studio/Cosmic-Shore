using System.IO;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor-side baker: reads sample data from an AudioClip, runs HapticEnvelopeAnalysis,
    /// and writes a .haptic file that NiceVibrations' HapticImporter auto-imports as a
    /// HapticClip ScriptableObject. The analysis math itself lives in the runtime utility
    /// CosmicShore.Utility.HapticEnvelopeAnalysis so it can be unit-tested.
    /// </summary>
    public static class HapticClipBaker
    {
        public static string BakeAudioClipToHapticFile(AudioClip clip, string outputAssetPath, HapticEnvelopeAnalysis.Settings settings)
        {
            if (clip == null) return null;

            float[] interleaved = new float[clip.samples * clip.channels];
            if (!clip.GetData(interleaved, 0))
            {
                Debug.LogError($"[HapticClipBaker] Failed to read samples from '{clip.name}'. " +
                               "Ensure the import setting 'Load Type' is Decompress On Load and the clip is not Streaming.");
                return null;
            }

            float[] mono = HapticEnvelopeAnalysis.InterleavedToMono(interleaved, clip.channels);
            var env = HapticEnvelopeAnalysis.Analyze(mono, clip.frequency, settings);
            string json = HapticEnvelopeAnalysis.RenderHapticJson(env, clip.name, clip.name);

            string fullPath = Path.GetFullPath(outputAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, json);

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
            return outputAssetPath;
        }
    }
}

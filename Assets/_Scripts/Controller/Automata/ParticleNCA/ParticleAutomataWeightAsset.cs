using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Loads trained Particle NCA weights exported by
    /// Tools/NCA_Training/train_particle_nca.py. The .bytes file is
    /// self-describing: a 48-byte header (magic 'PNCA', version, network
    /// dimensions, simulation constants baked at training time) followed by
    /// the two dense layers' weights and biases as flat float32 arrays.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ParticleAutomataWeights",
        menuName = "ScriptableObjects/Automata/Particle Automata Weight Asset")]
    public class ParticleAutomataWeightAsset : ScriptableObject
    {
        const int Magic = 0x41434e50; // 'PNCA' little-endian
        const int SupportedVersion = 1;
        const int HeaderBytes = 48;

        [Header("Exported Weights")]
        [Tooltip("Self-describing .bytes file exported from Python training (PNCA header + weights1, biases1, weights2, biases2).")]
        [SerializeField] TextAsset weightsFile;

        public int ChannelCount { get; private set; }
        public int KNeighbors { get; private set; }
        public int HiddenSize { get; private set; }
        public int PerceptionDim { get; private set; }
        public int OutputDim { get; private set; }
        public int StepsPerLoop { get; private set; }
        public float PerceptionRadius { get; private set; }
        public float StepScale { get; private set; }
        public float FireRate { get; private set; }

        public bool TryLoadWeights(out float[] weights1, out float[] biases1,
                                   out float[] weights2, out float[] biases2)
        {
            weights1 = biases1 = weights2 = biases2 = null;

            if (weightsFile == null)
            {
                Debug.LogError($"[{nameof(ParticleAutomataWeightAsset)}] No weights file assigned on {name}.");
                return false;
            }

            byte[] raw = weightsFile.bytes;
            if (raw.Length < HeaderBytes)
            {
                Debug.LogError($"[{nameof(ParticleAutomataWeightAsset)}] Weights file on {name} is smaller than the PNCA header ({raw.Length} bytes).");
                return false;
            }

            if (BitConverter.ToInt32(raw, 0) != Magic)
            {
                Debug.LogError($"[{nameof(ParticleAutomataWeightAsset)}] Bad magic in weights file on {name} — not a PNCA export.");
                return false;
            }

            int version = BitConverter.ToInt32(raw, 4);
            if (version != SupportedVersion)
            {
                Debug.LogError($"[{nameof(ParticleAutomataWeightAsset)}] Unsupported PNCA version {version} on {name} (expected {SupportedVersion}).");
                return false;
            }

            ChannelCount = BitConverter.ToInt32(raw, 8);
            KNeighbors = BitConverter.ToInt32(raw, 12);
            HiddenSize = BitConverter.ToInt32(raw, 16);
            PerceptionDim = BitConverter.ToInt32(raw, 20);
            OutputDim = BitConverter.ToInt32(raw, 24);
            StepsPerLoop = BitConverter.ToInt32(raw, 28);
            PerceptionRadius = BitConverter.ToSingle(raw, 32);
            StepScale = BitConverter.ToSingle(raw, 36);
            FireRate = BitConverter.ToSingle(raw, 40);

            int w1Count = PerceptionDim * HiddenSize;
            int b1Count = HiddenSize;
            int w2Count = HiddenSize * OutputDim;
            int b2Count = OutputDim;
            int expectedBytes = HeaderBytes + (w1Count + b1Count + w2Count + b2Count) * sizeof(float);

            if (raw.Length != expectedBytes)
            {
                Debug.LogError($"[{nameof(ParticleAutomataWeightAsset)}] Weights file size mismatch on {name}. Header declares {expectedBytes} bytes, file is {raw.Length}.");
                return false;
            }

            int offset = HeaderBytes;
            weights1 = new float[w1Count];
            Buffer.BlockCopy(raw, offset, weights1, 0, w1Count * sizeof(float));
            offset += w1Count * sizeof(float);

            biases1 = new float[b1Count];
            Buffer.BlockCopy(raw, offset, biases1, 0, b1Count * sizeof(float));
            offset += b1Count * sizeof(float);

            weights2 = new float[w2Count];
            Buffer.BlockCopy(raw, offset, weights2, 0, w2Count * sizeof(float));
            offset += w2Count * sizeof(float);

            biases2 = new float[b2Count];
            Buffer.BlockCopy(raw, offset, biases2, 0, b2Count * sizeof(float));

            return true;
        }
    }
}

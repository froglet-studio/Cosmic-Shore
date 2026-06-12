using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "NCAWeights",
        menuName = "ScriptableObjects/Automata/NCA Weight Asset")]
    public class NCAWeightAsset : ScriptableObject
    {
        [Header("Network Architecture")]
        [Tooltip("Must match the trained model. Default: 48 (16 channels * 3 filters)")]
        [SerializeField] int perceptionDim = 48;

        [Tooltip("Must match the trained model. Default: 128")]
        [SerializeField] int hiddenSize = 128;

        [Tooltip("Must match the trained model. Default: 16")]
        [SerializeField] int channelCount = 16;

        [Header("Exported Weights")]
        [Tooltip("Raw .bytes file exported from Python training. Layout: weights1, biases1, weights2, biases2 as flat float32 arrays.")]
        [SerializeField] TextAsset weightsFile;

        public int PerceptionDim => perceptionDim;
        public int HiddenSize => hiddenSize;
        public int ChannelCount => channelCount;

        public int ExpectedWeights1Count => perceptionDim * hiddenSize;
        public int ExpectedBiases1Count => hiddenSize;
        public int ExpectedWeights2Count => hiddenSize * channelCount;
        public int ExpectedBiases2Count => channelCount;
        public int TotalFloatCount => ExpectedWeights1Count + ExpectedBiases1Count + ExpectedWeights2Count + ExpectedBiases2Count;

        public bool TryLoadWeights(out float[] weights1, out float[] biases1, out float[] weights2, out float[] biases2)
        {
            weights1 = null;
            biases1 = null;
            weights2 = null;
            biases2 = null;

            if (weightsFile == null)
            {
                Debug.LogError($"[{nameof(NCAWeightAsset)}] No weights file assigned on {name}.");
                return false;
            }

            byte[] raw = weightsFile.bytes;
            int expectedBytes = TotalFloatCount * sizeof(float);
            if (raw.Length != expectedBytes)
            {
                Debug.LogError($"[{nameof(NCAWeightAsset)}] Weight file size mismatch on {name}. Expected {expectedBytes} bytes, got {raw.Length}.");
                return false;
            }

            float[] all = new float[TotalFloatCount];
            System.Buffer.BlockCopy(raw, 0, all, 0, raw.Length);

            int offset = 0;

            weights1 = new float[ExpectedWeights1Count];
            System.Array.Copy(all, offset, weights1, 0, ExpectedWeights1Count);
            offset += ExpectedWeights1Count;

            biases1 = new float[ExpectedBiases1Count];
            System.Array.Copy(all, offset, biases1, 0, ExpectedBiases1Count);
            offset += ExpectedBiases1Count;

            weights2 = new float[ExpectedWeights2Count];
            System.Array.Copy(all, offset, weights2, 0, ExpectedWeights2Count);
            offset += ExpectedWeights2Count;

            biases2 = new float[ExpectedBiases2Count];
            System.Array.Copy(all, offset, biases2, 0, ExpectedBiases2Count);

            return true;
        }
    }
}

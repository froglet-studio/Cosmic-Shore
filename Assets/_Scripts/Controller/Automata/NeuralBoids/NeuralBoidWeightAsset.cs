using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "NeuralBoidWeights",
        menuName = "ScriptableObjects/Automata/Neural Boid Weight Asset")]
    public class NeuralBoidWeightAsset : ScriptableObject
    {
        [Header("Network Architecture")]
        [SerializeField] int inputDim = 13;
        [SerializeField] int hiddenSize = 64;
        [SerializeField] int outputDim = 3;

        [Header("Exported Weights")]
        [Tooltip("Raw .bytes file exported from Python training. Layout: weights1, biases1, weights2, biases2 as flat float32.")]
        [SerializeField] TextAsset weightsFile;

        public int InputDim => inputDim;
        public int HiddenSize => hiddenSize;
        public int OutputDim => outputDim;

        public int Weights1Count => inputDim * hiddenSize;
        public int Biases1Count => hiddenSize;
        public int Weights2Count => hiddenSize * outputDim;
        public int Biases2Count => outputDim;
        public int TotalFloatCount => Weights1Count + Biases1Count + Weights2Count + Biases2Count;

        public bool TryLoadWeights(out float[] weights1, out float[] biases1, out float[] weights2, out float[] biases2)
        {
            weights1 = null;
            biases1 = null;
            weights2 = null;
            biases2 = null;

            if (weightsFile == null)
            {
                Debug.LogError($"[{nameof(NeuralBoidWeightAsset)}] No weights file assigned on {name}.");
                return false;
            }

            byte[] raw = weightsFile.bytes;
            int expectedBytes = TotalFloatCount * sizeof(float);
            if (raw.Length != expectedBytes)
            {
                Debug.LogError($"[{nameof(NeuralBoidWeightAsset)}] Size mismatch on {name}. Expected {expectedBytes} bytes, got {raw.Length}.");
                return false;
            }

            float[] all = new float[TotalFloatCount];
            System.Buffer.BlockCopy(raw, 0, all, 0, raw.Length);

            int offset = 0;

            weights1 = new float[Weights1Count];
            System.Array.Copy(all, offset, weights1, 0, Weights1Count);
            offset += Weights1Count;

            biases1 = new float[Biases1Count];
            System.Array.Copy(all, offset, biases1, 0, Biases1Count);
            offset += Biases1Count;

            weights2 = new float[Weights2Count];
            System.Array.Copy(all, offset, weights2, 0, Weights2Count);
            offset += Weights2Count;

            biases2 = new float[Biases2Count];
            System.Array.Copy(all, offset, biases2, 0, Biases2Count);

            return true;
        }
    }
}

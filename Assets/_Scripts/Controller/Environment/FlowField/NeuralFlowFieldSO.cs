namespace CosmicShore.Gameplay
{
    using UnityEngine;

    /// <summary>
    /// A FlowFieldSO whose flow vectors come from a trained neural network
    /// instead of hand-designed Gaussian/Elliptical/Polar equations.
    /// Weights are exported by Tools/NCA_Training/export_weights.py.
    /// </summary>
    [CreateAssetMenu(fileName = "NeuralFlowData", menuName = "ScriptableObjects/Flow/NeuralFlow", order = 40)]
    [System.Serializable]
    public class NeuralFlowFieldSO : FlowFieldSO
    {
        [Header("Neural Network Weights")]
        [SerializeField] TextAsset weightsFile;

        [Header("Network Architecture")]
        [Tooltip("Input: normalized position (3).")]
        [SerializeField] int inputDim = 3;

        [Tooltip("Hidden layer size.")]
        [SerializeField] int hiddenSize = 32;

        [Tooltip("Output: flow vector (3).")]
        [SerializeField] int outputDim = 3;

        float[] _w1, _b1, _w2, _b2;
        bool _loaded;

        void EnsureLoaded()
        {
            if (_loaded) return;
            if (weightsFile == null) return;

            int w1Count = inputDim * hiddenSize;
            int b1Count = hiddenSize;
            int w2Count = hiddenSize * outputDim;
            int b2Count = outputDim;
            int total = w1Count + b1Count + w2Count + b2Count;

            byte[] raw = weightsFile.bytes;
            if (raw.Length != total * sizeof(float))
            {
                Debug.LogError($"[{nameof(NeuralFlowFieldSO)}] Weight file size mismatch on {name}.");
                return;
            }

            float[] all = new float[total];
            System.Buffer.BlockCopy(raw, 0, all, 0, raw.Length);

            int offset = 0;
            _w1 = new float[w1Count];
            System.Array.Copy(all, offset, _w1, 0, w1Count); offset += w1Count;
            _b1 = new float[b1Count];
            System.Array.Copy(all, offset, _b1, 0, b1Count); offset += b1Count;
            _w2 = new float[w2Count];
            System.Array.Copy(all, offset, _w2, 0, w2Count); offset += w2Count;
            _b2 = new float[b2Count];
            System.Array.Copy(all, offset, _b2, 0, b2Count);

            _loaded = true;
        }

        public override Vector3 FlowVector(Transform node)
        {
            EnsureLoaded();
            if (!_loaded) return Vector3.zero;

            // Normalize input position relative to field dimensions
            float nx = node.position.x / fieldWidth;
            float ny = node.position.y / fieldHeight;
            float nz = node.position.z / fieldThickness;

            float[] input = { nx, ny, nz };

            // Layer 1: input(3) -> hidden(hiddenSize) with ReLU
            float[] hidden = new float[hiddenSize];
            for (int h = 0; h < hiddenSize; h++)
            {
                float sum = _b1[h];
                for (int i = 0; i < inputDim; i++)
                    sum += input[i] * _w1[i * hiddenSize + h];
                hidden[h] = Mathf.Max(0f, sum);
            }

            // Layer 2: hidden(hiddenSize) -> output(3) linear
            float[] output = new float[outputDim];
            for (int o = 0; o < outputDim; o++)
            {
                float sum = _b2[o];
                for (int h = 0; h < hiddenSize; h++)
                    sum += hidden[h] * _w2[h * outputDim + o];
                output[o] = sum;
            }

            // Scale output to fieldMax and apply thickness falloff (matching existing convention)
            float thicknessFalloff = 1f - Mathf.Clamp01(Mathf.Abs(node.position.z / fieldThickness));
            return new Vector3(output[0], output[1], output[2]) * fieldMax * thicknessFalloff;
        }
    }
}

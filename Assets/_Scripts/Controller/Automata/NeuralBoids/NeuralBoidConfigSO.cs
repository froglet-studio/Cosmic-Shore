using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "NeuralBoidConfig",
        menuName = "ScriptableObjects/Automata/Neural Boid Config")]
    public class NeuralBoidConfigSO : ScriptableObject
    {
        [Header("Perception")]
        [Tooltip("Radius within which each boid perceives neighbors.")]
        [SerializeField] float perceptionRadius = 100f;

        [Tooltip("Maximum neighbors to consider per boid.")]
        [SerializeField] int maxNeighbors = 16;

        [Header("Network Architecture")]
        [Tooltip("Input: aggregated perception vector size. Default: relative_pos(3) + relative_vel(3) + distance(1) aggregated stats = 13")]
        [SerializeField] int inputDim = 13;

        [Tooltip("Hidden layer size.")]
        [SerializeField] int hiddenSize = 64;

        [Tooltip("Output: velocity delta (3).")]
        [SerializeField] int outputDim = 3;

        [Header("Assets")]
        [SerializeField] NeuralBoidWeightAsset weights;
        [SerializeField] ComputeShader computeShader;

        [Header("Behavior")]
        [Tooltip("Scale factor on the neural network output velocity delta.")]
        [SerializeField] float outputScale = 1f;

        [Tooltip("Maximum speed for boids.")]
        [SerializeField] float maxSpeed = 5f;

        public float PerceptionRadius => perceptionRadius;
        public int MaxNeighbors => maxNeighbors;
        public int InputDim => inputDim;
        public int HiddenSize => hiddenSize;
        public int OutputDim => outputDim;
        public NeuralBoidWeightAsset Weights => weights;
        public ComputeShader ComputeShader => computeShader;
        public float OutputScale => outputScale;
        public float MaxSpeed => maxSpeed;
    }
}

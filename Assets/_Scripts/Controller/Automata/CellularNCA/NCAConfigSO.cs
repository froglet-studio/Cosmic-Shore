using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "NCAConfig",
        menuName = "ScriptableObjects/Automata/NCA Config")]
    public class NCAConfigSO : ScriptableObject
    {
        [Header("Grid")]
        [SerializeField] int gridWidth = 72;
        [SerializeField] int gridHeight = 72;

        [Header("Simulation")]
        [Tooltip("Fraction of cells that update each step. 0.5 matches the paper default.")]
        [SerializeField, Range(0.01f, 1f)] float fireRate = 0.5f;

        [Tooltip("Number of NCA steps to run per Unity frame.")]
        [SerializeField, Range(1, 16)] int stepsPerFrame = 1;

        [Tooltip("Multiplier on the state delta each step.")]
        [SerializeField] float stepSize = 1f;

        [Tooltip("Rotation angle (degrees) applied to Sobel perception kernels for angle invariance.")]
        [SerializeField] float perceptionAngleDegrees = 0f;

        [Header("Assets")]
        [SerializeField] NCAWeightAsset weights;
        [SerializeField] ComputeShader computeShader;

        [Header("Animated Targets (optional)")]
        [Tooltip("For time-varying NCA: sequence of target frames the simulation can track.")]
        [SerializeField] Texture2D[] targetFrames;

        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public float FireRate => fireRate;
        public int StepsPerFrame => stepsPerFrame;
        public float StepSize => stepSize;
        public float PerceptionAngleRadians => perceptionAngleDegrees * Mathf.Deg2Rad;
        public NCAWeightAsset Weights => weights;
        public ComputeShader ComputeShader => computeShader;
        public Texture2D[] TargetFrames => targetFrames;
    }
}

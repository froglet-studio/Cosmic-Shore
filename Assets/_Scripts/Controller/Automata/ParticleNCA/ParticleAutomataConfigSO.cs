using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tunable parameters for a Particle NCA simulation (learned continuous
    /// automaton). Network architecture and trained simulation constants
    /// (perception radius, step scale, fire rate, steps per loop) come from
    /// the weight asset's PNCA header — this config holds everything that is
    /// safe to vary at deploy time without retraining.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ParticleAutomataConfig",
        menuName = "ScriptableObjects/Automata/Particle Automata Config")]
    public class ParticleAutomataConfigSO : ScriptableObject
    {
        [Header("Population")]
        [Tooltip("Particle count. Train and deploy at similar counts — the learned density target depends on it. Brute-force kNN supports up to ~4096.")]
        [SerializeField, Range(32, 4096)] int particleCount = 256;

        [Header("Simulation")]
        [Tooltip("Automaton steps per second. With the trained stepsPerLoop this sets the animation playback rate (e.g. 96 steps/loop at 48 steps/sec = 2s loop).")]
        [SerializeField, Range(1f, 240f)] float stepsPerSecond = 48f;

        [Tooltip("Hidden state channels are clamped to ±this value each step. Must match training (default 4).")]
        [SerializeField] float stateClamp = 4f;

        [Tooltip("Radius of the gaussian seed ball, in local/unit space. Must match training (default 0.12).")]
        [SerializeField] float seedRadius = 0.12f;

        [Tooltip("Scatter radius for damaged particles, in local/unit space. Must keep blasted particles within perceptual contact of the body — matches training's damage curriculum (default 0.6).")]
        [SerializeField] float scatterRadius = 0.6f;

        [Header("World Placement")]
        [Tooltip("The automaton simulates in the unit-scale space it was trained in; this scales it into world units.")]
        [SerializeField] float worldScale = 20f;

        [Header("Assets")]
        [SerializeField] ParticleAutomataWeightAsset weights;
        [SerializeField] ComputeShader computeShader;

        public int ParticleCount => particleCount;
        public float StepsPerSecond => stepsPerSecond;
        public float StateClamp => stateClamp;
        public float SeedRadius => seedRadius;
        public float ScatterRadius => scatterRadius;
        public float WorldScale => worldScale;
        public ParticleAutomataWeightAsset Weights => weights;
        public ComputeShader ComputeShader => computeShader;
    }
}

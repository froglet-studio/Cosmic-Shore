using UnityEngine;
using Unity.Profiling;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives a Particle NCA — a learned continuous automaton whose particle
    /// population grows from a seed into a trained, looping 3D animation and
    /// self-heals after damage (see ParticleAutomataStep.compute and
    /// Tools/NCA_Training/train_particle_nca.py).
    ///
    /// The simulation runs entirely on the GPU in the unit-scale local space
    /// the model was trained in; <see cref="LocalToWorld"/> places it in the
    /// world for rendering (see ParticleAutomataRenderer).
    /// </summary>
    public class ParticleAutomataSimulator : MonoBehaviour
    {
        // Must mirror the #defines in ParticleAutomataStep.compute.
        const int ShaderChannelCount = 8;
        const int ShaderPerceptionDim = 46;
        const int ShaderOutputDim = 11;
        const int ShaderMaxHidden = 128;
        const int ShaderMaxNeighbors = 16;
        const int ParticleStride = sizeof(float) * (3 + 4 + 4);

        [Header("Configuration")]
        [SerializeField] ParticleAutomataConfigSO config;

        static readonly ProfilerMarker s_StepMarker = new("ParticleAutomataSimulator.Step");

        ComputeBuffer _readBuffer;
        ComputeBuffer _writeBuffer;
        ComputeBuffer _weights1Buffer;
        ComputeBuffer _biases1Buffer;
        ComputeBuffer _weights2Buffer;
        ComputeBuffer _biases2Buffer;

        int _stepKernel;
        int _damageKernel;
        int _seedKernel;
        uint _stepSeed;
        float _phase;
        float _stepAccumulator;
        bool _initialized;

        /// <summary>Current animation phase in [0,1).</summary>
        public float Phase => _phase;

        /// <summary>Particle buffer for rendering. Stride: position(3) + state0(4) + state1(4) floats.</summary>
        public ComputeBuffer ParticleBuffer => _readBuffer;

        public int ParticleCount => config != null ? config.ParticleCount : 0;

        public bool IsInitialized => _initialized;

        /// <summary>Transform from simulation (unit) space into world space.</summary>
        public Matrix4x4 LocalToWorld =>
            transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * config.WorldScale);

        void OnEnable()
        {
            if (config == null)
            {
                Debug.LogError($"[{nameof(ParticleAutomataSimulator)}] No config assigned on {name}.");
                return;
            }
            Initialize();
        }

        void OnDisable()
        {
            Cleanup();
        }

        void Update()
        {
            if (!_initialized) return;

            _stepAccumulator += Time.deltaTime * config.StepsPerSecond;
            int steps = Mathf.Min((int)_stepAccumulator, 8); // cap catch-up after hitches
            _stepAccumulator -= (int)_stepAccumulator;

            using (s_StepMarker.Auto())
            {
                for (int i = 0; i < steps; i++)
                    Step();
            }
        }

        void Initialize()
        {
            if (config.ComputeShader == null || config.Weights == null)
            {
                Debug.LogError($"[{nameof(ParticleAutomataSimulator)}] Config missing compute shader or weights on {name}.");
                return;
            }

            if (!config.Weights.TryLoadWeights(out var w1, out var b1, out var w2, out var b2))
                return;

            var weights = config.Weights;
            if (weights.ChannelCount != ShaderChannelCount ||
                weights.PerceptionDim != ShaderPerceptionDim ||
                weights.OutputDim != ShaderOutputDim ||
                weights.HiddenSize > ShaderMaxHidden ||
                weights.KNeighbors > ShaderMaxNeighbors)
            {
                Debug.LogError($"[{nameof(ParticleAutomataSimulator)}] Weight asset {weights.name} " +
                    $"(C={weights.ChannelCount}, perception={weights.PerceptionDim}, out={weights.OutputDim}, " +
                    $"H={weights.HiddenSize}, K={weights.KNeighbors}) does not fit the compute shader " +
                    $"(C={ShaderChannelCount}, perception={ShaderPerceptionDim}, out={ShaderOutputDim}, " +
                    $"maxH={ShaderMaxHidden}, maxK={ShaderMaxNeighbors}).");
                return;
            }

            int count = config.ParticleCount;
            _readBuffer = new ComputeBuffer(count, ParticleStride);
            _writeBuffer = new ComputeBuffer(count, ParticleStride);

            _weights1Buffer = new ComputeBuffer(w1.Length, sizeof(float));
            _weights1Buffer.SetData(w1);
            _biases1Buffer = new ComputeBuffer(b1.Length, sizeof(float));
            _biases1Buffer.SetData(b1);
            _weights2Buffer = new ComputeBuffer(w2.Length, sizeof(float));
            _weights2Buffer.SetData(w2);
            _biases2Buffer = new ComputeBuffer(b2.Length, sizeof(float));
            _biases2Buffer.SetData(b2);

            var cs = config.ComputeShader;
            _stepKernel = cs.FindKernel("StepParticles");
            _damageKernel = cs.FindKernel("ScatterDamage");
            _seedKernel = cs.FindKernel("ResetToSeed");

            _stepSeed = (uint)Random.Range(1, int.MaxValue);
            _initialized = true;

            ResetToSeed();
        }

        void SetCommonParams(ComputeShader cs)
        {
            cs.SetInt("_ParticleCount", config.ParticleCount);
            cs.SetInt("_KNeighbors", config.Weights.KNeighbors);
            cs.SetInt("_HiddenSize", config.Weights.HiddenSize);
            cs.SetFloat("_PerceptionRadius", config.Weights.PerceptionRadius);
            cs.SetFloat("_StepScale", config.Weights.StepScale);
            cs.SetFloat("_FireRate", config.Weights.FireRate);
            cs.SetFloat("_StateClamp", config.StateClamp);
        }

        void Step()
        {
            var cs = config.ComputeShader;
            _stepSeed++;

            SetCommonParams(cs);
            cs.SetFloat("_Phase", _phase);
            cs.SetInt("_StepSeed", (int)_stepSeed);
            cs.SetBuffer(_stepKernel, "_ParticlesRead", _readBuffer);
            cs.SetBuffer(_stepKernel, "_ParticlesWrite", _writeBuffer);
            cs.SetBuffer(_stepKernel, "_Weights1", _weights1Buffer);
            cs.SetBuffer(_stepKernel, "_Biases1", _biases1Buffer);
            cs.SetBuffer(_stepKernel, "_Weights2", _weights2Buffer);
            cs.SetBuffer(_stepKernel, "_Biases2", _biases2Buffer);

            cs.Dispatch(_stepKernel, Mathf.CeilToInt(config.ParticleCount / 64f), 1, 1);
            (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);

            _phase = (_phase + 1f / config.Weights.StepsPerLoop) % 1f;
        }

        void Cleanup()
        {
            _initialized = false;
            _readBuffer?.Release();
            _writeBuffer?.Release();
            _weights1Buffer?.Release();
            _biases1Buffer?.Release();
            _weights2Buffer?.Release();
            _biases2Buffer?.Release();
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Restart the automaton from its trained seed: a tight
        /// particle ball that regrows the full animated shape.</summary>
        public void ResetToSeed()
        {
            if (!_initialized) return;

            var cs = config.ComputeShader;
            _stepSeed++;

            SetCommonParams(cs);
            cs.SetInt("_StepSeed", (int)_stepSeed);
            cs.SetFloat("_SeedRadius", config.SeedRadius);
            cs.SetBuffer(_seedKernel, "_ParticlesWrite", _readBuffer);
            cs.Dispatch(_seedKernel, Mathf.CeilToInt(config.ParticleCount / 64f), 1, 1);

            _phase = 0f;
        }

        /// <summary>Blast particles inside a world-space sphere to random
        /// positions (state wiped). The learned rule re-forms the shape —
        /// the 3D continuous analog of the self-healing emoji demos.</summary>
        public void DamageSphere(Vector3 worldCenter, float worldRadius)
        {
            if (!_initialized) return;

            Vector3 localCenter = LocalToWorld.inverse.MultiplyPoint3x4(worldCenter);
            float localRadius = worldRadius / config.WorldScale;

            var cs = config.ComputeShader;
            _stepSeed++;

            SetCommonParams(cs);
            cs.SetInt("_StepSeed", (int)_stepSeed);
            cs.SetVector("_DamageCenter", localCenter);
            cs.SetFloat("_DamageRadius", localRadius);
            cs.SetFloat("_ScatterRadius", config.ScatterRadius);
            cs.SetBuffer(_damageKernel, "_ParticlesRead", _readBuffer);
            cs.SetBuffer(_damageKernel, "_ParticlesWrite", _writeBuffer);
            cs.Dispatch(_damageKernel, Mathf.CeilToInt(config.ParticleCount / 64f), 1, 1);

            (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);
        }
    }
}

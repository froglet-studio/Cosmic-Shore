using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace CosmicShore.Gameplay
{
    public struct NeuralBoidEntity
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 goalDirection;
        public int team;

        public const int Stride = sizeof(float) * 9 + sizeof(int);
    }

    /// <summary>
    /// GPU-driven boid flock whose steering rule is a trained neural network
    /// (see NeuralBoidStep.compute and Tools/NCA_Training/train_neural_boids.py)
    /// instead of hardcoded separation/cohesion/alignment.
    /// </summary>
    public class NeuralBoidSimulator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] NeuralBoidConfigSO config;

        [Header("Spawning")]
        [SerializeField] int initialBoidCount = 100;
        [SerializeField] float spawnRadius = 50f;
        [SerializeField] Transform goal;

        [Header("Visualization")]
        [Tooltip("Optional visual to instantiate per boid. Positions sync from the GPU each frame.")]
        [SerializeField] Transform boidVisualPrefab;

        static readonly ProfilerMarker s_SimMarker = new("NeuralBoidSimulator.Step");

        ComputeBuffer _readBuffer;
        ComputeBuffer _writeBuffer;
        ComputeBuffer _weights1Buffer;
        ComputeBuffer _biases1Buffer;
        ComputeBuffer _weights2Buffer;
        ComputeBuffer _biases2Buffer;

        NeuralBoidEntity[] _entities;
        readonly List<Transform> _boidVisuals = new();
        int _kernel;
        bool _initialized;

        void OnEnable()
        {
            if (config == null)
            {
                Debug.LogError($"[{nameof(NeuralBoidSimulator)}] No config assigned on {name}.");
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

            using (s_SimMarker.Auto())
            {
                Step();
            }

            SyncVisualsFromGPU();
        }

        void Initialize()
        {
            if (config.ComputeShader == null || config.Weights == null)
            {
                Debug.LogError($"[{nameof(NeuralBoidSimulator)}] Config missing compute shader or weights on {name}.");
                return;
            }

            SpawnBoids();

            _readBuffer = new ComputeBuffer(_entities.Length, NeuralBoidEntity.Stride);
            _writeBuffer = new ComputeBuffer(_entities.Length, NeuralBoidEntity.Stride);
            _readBuffer.SetData(_entities);

            if (!LoadWeights()) return;

            _kernel = config.ComputeShader.FindKernel("NeuralBoidUpdate");
            _initialized = true;
        }

        void SpawnBoids()
        {
            _entities = new NeuralBoidEntity[initialBoidCount];
            Vector3 goalPos = goal != null ? goal.position : Vector3.zero;

            for (int i = 0; i < initialBoidCount; i++)
            {
                Vector3 pos = transform.position + Random.insideUnitSphere * spawnRadius;
                Vector3 vel = Random.onUnitSphere;

                _entities[i] = new NeuralBoidEntity
                {
                    position = pos,
                    velocity = vel,
                    goalDirection = (goalPos - pos).normalized,
                    team = 0
                };

                if (boidVisualPrefab != null)
                {
                    var visual = Instantiate(boidVisualPrefab, pos, Quaternion.LookRotation(vel));
                    visual.SetParent(transform);
                    _boidVisuals.Add(visual);
                }
            }
        }

        bool LoadWeights()
        {
            if (!config.Weights.TryLoadWeights(out var w1, out var b1, out var w2, out var b2))
                return false;

            _weights1Buffer = new ComputeBuffer(w1.Length, sizeof(float));
            _weights1Buffer.SetData(w1);
            _biases1Buffer = new ComputeBuffer(b1.Length, sizeof(float));
            _biases1Buffer.SetData(b1);
            _weights2Buffer = new ComputeBuffer(w2.Length, sizeof(float));
            _weights2Buffer.SetData(w2);
            _biases2Buffer = new ComputeBuffer(b2.Length, sizeof(float));
            _biases2Buffer.SetData(b2);

            return true;
        }

        void Step()
        {
            var cs = config.ComputeShader;

            cs.SetInt("_BoidCount", _entities.Length);
            cs.SetFloat("_PerceptionRadius", config.PerceptionRadius);
            cs.SetInt("_MaxNeighbors", config.MaxNeighbors);
            cs.SetInt("_InputDim", config.InputDim);
            cs.SetInt("_HiddenSize", config.HiddenSize);
            cs.SetInt("_OutputDim", config.OutputDim);
            cs.SetFloat("_OutputScale", config.OutputScale);
            cs.SetFloat("_MaxSpeed", config.MaxSpeed);
            cs.SetFloat("_DeltaTime", Time.deltaTime);

            cs.SetBuffer(_kernel, "_BoidBufferRead", _readBuffer);
            cs.SetBuffer(_kernel, "_BoidBufferWrite", _writeBuffer);
            cs.SetBuffer(_kernel, "_Weights1", _weights1Buffer);
            cs.SetBuffer(_kernel, "_Biases1", _biases1Buffer);
            cs.SetBuffer(_kernel, "_Weights2", _weights2Buffer);
            cs.SetBuffer(_kernel, "_Biases2", _biases2Buffer);

            int groups = Mathf.CeilToInt(_entities.Length / 64f);
            cs.Dispatch(_kernel, groups, 1, 1);

            // Swap buffers
            (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);
        }

        void SyncVisualsFromGPU()
        {
            if (_boidVisuals.Count == 0) return;

            _readBuffer.GetData(_entities);

            for (int i = 0; i < _entities.Length && i < _boidVisuals.Count; i++)
            {
                var boid = _boidVisuals[i];
                if (boid == null) continue;

                var entity = _entities[i];
                boid.position = entity.position;

                if (entity.velocity.sqrMagnitude > 0.0001f)
                    boid.rotation = Quaternion.LookRotation(entity.velocity);
            }
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

        // ── Public API ──────────────────────────────────────

        public void SetGoal(Vector3 goalPosition)
        {
            if (_entities == null) return;

            for (int i = 0; i < _entities.Length; i++)
            {
                _entities[i].goalDirection = (goalPosition - _entities[i].position).normalized;
            }
            _readBuffer.SetData(_entities);
        }
    }
}

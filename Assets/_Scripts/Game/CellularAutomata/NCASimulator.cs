using UnityEngine;
using Unity.Profiling;

namespace CosmicShore.Game.CellularAutomata
{
    public class NCASimulator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] NCAConfigSO config;

        [Header("Debug Visualization")]
        [Tooltip("Assign a renderer to display the NCA RGBA output in the scene.")]
        [SerializeField] Renderer debugRenderer;

        static readonly ProfilerMarker s_StepMarker = new("NCASimulator.Step");
        static readonly ProfilerMarker s_PerceiveMarker = new("NCASimulator.Perceive");
        static readonly ProfilerMarker s_UpdateMarker = new("NCASimulator.Update");
        static readonly ProfilerMarker s_ApplyMarker = new("NCASimulator.Apply");

        // Double-buffered state: 4 RenderTextures × 2 sets (read/write)
        RenderTexture[] _stateA = new RenderTexture[4];
        RenderTexture[] _stateB = new RenderTexture[4];
        bool _readFromA = true;

        // Perception (48 channels = 12 RGBA textures)
        RenderTexture[] _perception = new RenderTexture[12];

        // Delta output (16 channels = 4 RGBA textures)
        RenderTexture[] _delta = new RenderTexture[4];

        // Weight buffers
        ComputeBuffer _weights1Buffer;
        ComputeBuffer _biases1Buffer;
        ComputeBuffer _weights2Buffer;
        ComputeBuffer _biases2Buffer;

        // Random values buffer for stochastic masking
        ComputeBuffer _randomBuffer;
        float[] _randomValues;

        // Kernel IDs
        int _perceiveKernel;
        int _updateKernel;
        int _applyKernel;

        // Output texture for external sampling
        public RenderTexture OutputRGBA => ReadState[0];

        RenderTexture[] ReadState => _readFromA ? _stateA : _stateB;
        RenderTexture[] WriteState => _readFromA ? _stateB : _stateA;

        bool _initialized;

        void OnEnable()
        {
            if (config == null)
            {
                Debug.LogError($"[{nameof(NCASimulator)}] No config assigned on {name}.");
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

            for (int i = 0; i < config.StepsPerFrame; i++)
            {
                Step();
            }

            if (debugRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                debugRenderer.GetPropertyBlock(mpb);
                mpb.SetTexture("_MainTex", OutputRGBA);
                debugRenderer.SetPropertyBlock(mpb);
            }
        }

        void Initialize()
        {
            if (config.ComputeShader == null || config.Weights == null)
            {
                Debug.LogError($"[{nameof(NCASimulator)}] Config missing compute shader or weights on {name}.");
                return;
            }

            int w = config.GridWidth;
            int h = config.GridHeight;

            CreateTextureSet(_stateA, w, h, 4);
            CreateTextureSet(_stateB, w, h, 4);
            CreateTextureSet(_perception, w, h, 12);
            CreateTextureSet(_delta, w, h, 4);

            // Clear all state
            ClearTextureSet(_stateA);
            ClearTextureSet(_stateB);

            if (!LoadWeights()) return;

            _randomValues = new float[w * h];
            _randomBuffer = new ComputeBuffer(w * h, sizeof(float));

            _perceiveKernel = config.ComputeShader.FindKernel("Perceive");
            _updateKernel = config.ComputeShader.FindKernel("Update");
            _applyKernel = config.ComputeShader.FindKernel("Apply");

            _initialized = true;
        }

        void CreateTextureSet(RenderTexture[] set, int w, int h, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBFloat)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                rt.Create();
                set[i] = rt;
            }
        }

        void ClearTextureSet(RenderTexture[] set)
        {
            var prev = RenderTexture.active;
            foreach (var rt in set)
            {
                if (rt == null) continue;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }
            RenderTexture.active = prev;
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
            using (s_StepMarker.Auto())
            {
                var cs = config.ComputeShader;
                var read = ReadState;
                var write = WriteState;
                int w = config.GridWidth;
                int h = config.GridHeight;
                int groupsX = Mathf.CeilToInt(w / 8f);
                int groupsY = Mathf.CeilToInt(h / 8f);

                // Shared parameters
                cs.SetInt("_GridWidth", w);
                cs.SetInt("_GridHeight", h);
                cs.SetFloat("_FireRate", config.FireRate);
                cs.SetFloat("_StepSize", config.StepSize);
                cs.SetFloat("_AngleRadians", config.PerceptionAngleRadians);

                // --- Perceive ---
                using (s_PerceiveMarker.Auto())
                {
                    BindReadState(cs, _perceiveKernel, read);
                    BindPerception(cs, _perceiveKernel);
                    cs.Dispatch(_perceiveKernel, groupsX, groupsY, 1);
                }

                // --- Update ---
                using (s_UpdateMarker.Auto())
                {
                    BindPerception(cs, _updateKernel);
                    BindDelta(cs, _updateKernel);
                    cs.SetBuffer(_updateKernel, "_Weights1", _weights1Buffer);
                    cs.SetBuffer(_updateKernel, "_Biases1", _biases1Buffer);
                    cs.SetBuffer(_updateKernel, "_Weights2", _weights2Buffer);
                    cs.SetBuffer(_updateKernel, "_Biases2", _biases2Buffer);
                    cs.Dispatch(_updateKernel, groupsX, groupsY, 1);
                }

                // --- Apply ---
                using (s_ApplyMarker.Auto())
                {
                    GenerateRandomValues();
                    BindReadState(cs, _applyKernel, read);
                    BindWriteState(cs, _applyKernel, write);
                    BindDelta(cs, _applyKernel);
                    cs.SetBuffer(_applyKernel, "_RandomValues", _randomBuffer);
                    cs.Dispatch(_applyKernel, groupsX, groupsY, 1);
                }

                _readFromA = !_readFromA;
            }
        }

        void GenerateRandomValues()
        {
            for (int i = 0; i < _randomValues.Length; i++)
                _randomValues[i] = Random.value;
            _randomBuffer.SetData(_randomValues);
        }

        void BindReadState(ComputeShader cs, int kernel, RenderTexture[] state)
        {
            cs.SetTexture(kernel, "_StateTexRead0", state[0]);
            cs.SetTexture(kernel, "_StateTexRead1", state[1]);
            cs.SetTexture(kernel, "_StateTexRead2", state[2]);
            cs.SetTexture(kernel, "_StateTexRead3", state[3]);
        }

        void BindWriteState(ComputeShader cs, int kernel, RenderTexture[] state)
        {
            cs.SetTexture(kernel, "_StateTexWrite0", state[0]);
            cs.SetTexture(kernel, "_StateTexWrite1", state[1]);
            cs.SetTexture(kernel, "_StateTexWrite2", state[2]);
            cs.SetTexture(kernel, "_StateTexWrite3", state[3]);
        }

        void BindPerception(ComputeShader cs, int kernel)
        {
            for (int i = 0; i < 12; i++)
                cs.SetTexture(kernel, "_PerceptionTex" + i, _perception[i]);
        }

        void BindDelta(ComputeShader cs, int kernel)
        {
            cs.SetTexture(kernel, "_DeltaTex0", _delta[0]);
            cs.SetTexture(kernel, "_DeltaTex1", _delta[1]);
            cs.SetTexture(kernel, "_DeltaTex2", _delta[2]);
            cs.SetTexture(kernel, "_DeltaTex3", _delta[3]);
        }

        void Cleanup()
        {
            _initialized = false;
            ReleaseTextureSet(_stateA);
            ReleaseTextureSet(_stateB);
            ReleaseTextureSet(_perception);
            ReleaseTextureSet(_delta);
            _weights1Buffer?.Release();
            _biases1Buffer?.Release();
            _weights2Buffer?.Release();
            _biases2Buffer?.Release();
            _randomBuffer?.Release();
        }

        void ReleaseTextureSet(RenderTexture[] set)
        {
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    set[i].Release();
                    set[i] = null;
                }
            }
        }

        // ── Public API ──────────────────────────────────────

        public void Seed(Vector2Int position, float[] channelValues = null)
        {
            if (!_initialized) return;

            int x = Mathf.Clamp(position.x, 0, config.GridWidth - 1);
            int y = Mathf.Clamp(position.y, 0, config.GridHeight - 1);

            // Default seed: alpha=1, all hidden channels=0
            float[] values = channelValues ?? new float[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            var read = ReadState;
            var seedTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);

            for (int g = 0; g < 4; g++)
            {
                seedTex.SetPixel(0, 0, new Color(
                    values[g * 4 + 0],
                    values[g * 4 + 1],
                    values[g * 4 + 2],
                    values[g * 4 + 3]
                ));
                seedTex.Apply();
                Graphics.CopyTexture(seedTex, 0, 0, 0, 0, 1, 1, read[g], 0, 0, x, y);
            }

            DestroyImmediate(seedTex);
        }

        public void Damage(Vector2Int center, int radius)
        {
            if (!_initialized) return;

            var read = ReadState;
            var clearTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            clearTex.SetPixel(0, 0, Color.clear);
            clearTex.Apply();

            int rSq = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > rSq) continue;

                    int x = center.x + dx;
                    int y = center.y + dy;
                    if (x < 0 || x >= config.GridWidth || y < 0 || y >= config.GridHeight) continue;

                    for (int g = 0; g < 4; g++)
                        Graphics.CopyTexture(clearTex, 0, 0, 0, 0, 1, 1, read[g], 0, 0, x, y);
                }
            }

            DestroyImmediate(clearTex);
        }

        public void ResetState()
        {
            if (!_initialized) return;
            ClearTextureSet(_stateA);
            ClearTextureSet(_stateB);
            _readFromA = true;
        }
    }
}

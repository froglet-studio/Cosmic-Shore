using System;
using System.Collections.Generic;

// UnityEngine.VFX (the live-src sync maps it to CosmicShore.Engine.VFX).
namespace CosmicShore.Engine.VFX
{
    /// <summary>An exposed property of a <see cref="VisualEffectAsset"/> (name + CLR value type).</summary>
    public struct VFXExposedProperty
    {
        public string name;
        public Type type;
    }

    /// <summary>
    /// A compiled VFX Graph. The port has no VFX runtime, so an asset is only its identity plus
    /// the list of properties it exposes (which a content importer may fill in via
    /// <see cref="DeclareExposedProperty"/>); <see cref="VisualEffect"/>'s <c>HasX</c> reads it.
    /// </summary>
    public class VisualEffectAsset : Object
    {
        readonly List<VFXExposedProperty> _exposed = new();

        public const string PlayEventName = "OnPlay";
        public const string StopEventName = "OnStop";
        public static readonly int PlayEventID = Shader.PropertyToID(PlayEventName);
        public static readonly int StopEventID = Shader.PropertyToID(StopEventName);

        public void GetExposedProperties(List<VFXExposedProperty> exposedProperties)
        {
            exposedProperties.Clear();
            exposedProperties.AddRange(_exposed);
        }

        /// <summary>Port: records that the graph exposes <paramref name="name"/> of type <paramref name="type"/>.</summary>
        public void DeclareExposedProperty(string name, Type type)
        {
            _exposed.RemoveAll(p => p.name == name);
            _exposed.Add(new VFXExposedProperty { name = name, type = type });
        }

        internal bool Exposes(int id, Type type)
        {
            foreach (var p in _exposed)
                if (p.type == type && Shader.PropertyToID(p.name) == id) return true;
            return false;
        }
    }

    /// <summary>Payload attributes for <see cref="VisualEffect.SendEvent(string, VFXEventAttribute)"/>.</summary>
    public class VFXEventAttribute : IDisposable
    {
        readonly Dictionary<int, object> _values = new();

        public bool HasFloat(string name) => _values.TryGetValue(Shader.PropertyToID(name), out var v) && v is float;
        public void SetFloat(string name, float value) => _values[Shader.PropertyToID(name)] = value;
        public void SetFloat(int nameID, float value) => _values[nameID] = value;
        public float GetFloat(string name) => _values.TryGetValue(Shader.PropertyToID(name), out var v) && v is float f ? f : 0f;
        public void SetInt(string name, int value) => _values[Shader.PropertyToID(name)] = value;
        public void SetBool(string name, bool value) => _values[Shader.PropertyToID(name)] = value;
        public void SetVector3(string name, Vector3 value) => _values[Shader.PropertyToID(name)] = value;
        public void SetVector3(int nameID, Vector3 value) => _values[nameID] = value;
        public Vector3 GetVector3(string name) => _values.TryGetValue(Shader.PropertyToID(name), out var v) && v is Vector3 x ? x : default;
        public void SetVector4(string name, Vector4 value) => _values[Shader.PropertyToID(name)] = value;
        public void Dispose() => _values.Clear();
    }

    /// <summary>
    /// A VFX Graph instance. The port has no VFX Graph runtime; it keeps the component honest as
    /// state (property overrides set with <c>SetX</c> read back with <c>GetX</c>, <c>HasX</c> is true
    /// for what the asset exposes or what has been set, play/stop/pause toggle the awake state, sent
    /// events are counted) and DRAWS the project's graphs APPROXIMATELY from their exposed
    /// properties: <see cref="Approximations"/>, keyed by the asset's name, submit ribbons through
    /// <see cref="ProceduralLines"/> each frame while the effect is awake. A graph with no
    /// approximation draws nothing.
    /// </summary>
    public class VisualEffect : Behaviour
    {
        readonly Dictionary<int, object> _overrides = new();
        bool _awake = true;

        static readonly List<VisualEffect> s_live = new();

        /// <summary>Approximate drawings of the project's VFX Graphs, by asset name.</summary>
        public static readonly Dictionary<string, Action<VisualEffect>> Approximations = new(StringComparer.Ordinal)
        {
            ["vfxgraph_arclightning"] = ArcLightning,
        };

        public VisualEffect()
        {
            lock (s_live)
            {
                if (s_live.Count == 0) ProceduralLines.Tick += TickAll;
                s_live.Add(this);
            }
        }

        static void TickAll()
        {
            lock (s_live)
            {
                for (int i = s_live.Count - 1; i >= 0; i--)
                {
                    var ve = s_live[i];
                    if (!ve) { s_live.RemoveAt(i); continue; }
                    if (!ve._awake || ve.pause || !ve.isActiveAndEnabled) continue;
                    if (ve.gameObject.transform.root.gameObject.isPrefabAsset) continue;
                    if (ve.visualEffectAsset != null && Approximations.TryGetValue(ve.visualEffectAsset.name ?? "", out var draw)) draw(ve);
                }
            }
        }

        /// <summary>
        /// vfxgraph_arclightning: two particle strips of 200, each a cubic Bezier through Pos1..Pos4
        /// (and Pos5..Pos8), displaced by fractal noise (Noise Frequency / Power / Speed / Octaves /
        /// Roughness) with the ends held, drawn Thickness wide in Color, additively.
        /// </summary>
        static void ArcLightning(VisualEffect ve)
        {
            float F(string n, float d) => ve.HasFloat(n) ? ve.GetFloat(n) : d;
            var c4 = ve.HasVector4("Color") ? ve.GetVector4("Color") : new Vector4(0.5f, 0.8f, 1f, 1f);
            var color = new Color(c4.x, c4.y, c4.z, 1f);
            float width = Math.Max(0.005f, F("Thickness", 0.1f));
            float freq = F("Noise Frequency", 3f), power = Math.Abs(F("NoisePower", 0.1f)), speed = F("NoiseSpeed", 1f);
            int octaves = Math.Clamp((int)F("NoiseOctaves", 3f), 1, 8);
            float rough = Math.Clamp(F("NoiseRoughness", 0.5f), 0f, 1f);
            float time = Time.time * speed;
            var t = ve.transform;
            for (int bolt = 0; bolt < 2; bolt++)
            {
                int k = bolt * 4;
                var p = new Vector3[4];
                bool any = false;
                for (int i = 0; i < 4; i++)
                {
                    string name = "Pos" + (k + i + 1);
                    p[i] = ve.HasVector3(name) ? ve.GetVector3(name) : Vector3.zero;
                    any |= p[i] != Vector3.zero;
                }
                if (!any) continue;
                // The graph's noise frequency is per unit of length: sample densely enough to show it.
                float length = (p[1] - p[0]).magnitude + (p[2] - p[1]).magnitude + (p[3] - p[2]).magnitude;
                int N = Math.Clamp((int)(length * freq * 6f), 24, 200);
                var pts = new Vector3[N];
                for (int i = 0; i < N; i++)
                {
                    float s = (float)i / (N - 1), u = 1f - s;
                    var pos = u * u * u * p[0] + 3f * u * u * s * p[1] + 3f * u * s * s * p[2] + s * s * s * p[3];
                    var tan = (3f * u * u * (p[1] - p[0]) + 6f * u * s * (p[2] - p[1]) + 3f * s * s * (p[3] - p[2])).normalized;
                    var n1 = Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    var n2 = Vector3.Cross(tan, n1);
                    float hold = Mathf.Sin(s * Mathf.PI);
                    float x = s * length * freq;
                    pos += (n1 * Fbm(x + time, bolt * 17.3f, octaves, rough) + n2 * Fbm(x - time, bolt * 31.7f + 5f, octaves, rough)) * (power * length * 0.25f * hold);
                    pts[i] = t.TransformPoint(pos);
                }
                ProceduralLines.Add(pts, width, color, additive: true, layer: ve.gameObject.layer);
            }
        }

        static float Hash(float n) { float v = Mathf.Sin(n) * 43758.5453f; return v - Mathf.Floor(v); }

        static float Noise1(float x, float seed)
        {
            float i = Mathf.Floor(x), f = x - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i + seed * 57.1f), Hash(i + 1f + seed * 57.1f), f) * 2f - 1f;
        }

        static float Fbm(float x, float seed, int octaves, float roughness)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++) { sum += Noise1(x, seed + o) * amp; norm += amp; x *= 2f; amp *= roughness; }
            return norm > 0f ? sum / norm : 0f;
        }

        public VisualEffectAsset visualEffectAsset { get; set; }
        public bool pause { get; set; }
        public float playRate { get; set; } = 1f;
        public uint startSeed { get; set; }
        public bool resetSeedOnPlay { get; set; } = true;
        public string initialEventName { get; set; } = VisualEffectAsset.PlayEventName;
        public int initialEventID
        {
            get => Shader.PropertyToID(initialEventName ?? string.Empty);
            set { }
        }
        public bool culled => !isActiveAndEnabled;
        public int aliveParticleCount => 0;

        /// <summary>Port: how many events have been sent (Play/Stop count as events).</summary>
        public int sentEventCount { get; private set; }
        /// <summary>Port: the name ID of the most recent event (0 when none).</summary>
        public int lastEventID { get; private set; }

        public bool HasAnySystemAwake() => _awake && isActiveAndEnabled;

        public VFXEventAttribute CreateVFXEventAttribute() => new VFXEventAttribute();

        public void Play() => SendEvent(VisualEffectAsset.PlayEventID);
        public void Play(VFXEventAttribute eventAttribute) => SendEvent(VisualEffectAsset.PlayEventID, eventAttribute);
        public void Stop() => SendEvent(VisualEffectAsset.StopEventID);
        public void Stop(VFXEventAttribute eventAttribute) => SendEvent(VisualEffectAsset.StopEventID, eventAttribute);

        public void SendEvent(string eventName) => SendEvent(Shader.PropertyToID(eventName ?? string.Empty));
        public void SendEvent(string eventName, VFXEventAttribute eventAttribute) => SendEvent(Shader.PropertyToID(eventName ?? string.Empty), eventAttribute);
        public void SendEvent(int eventNameID) => SendEvent(eventNameID, null);

        public void SendEvent(int eventNameID, VFXEventAttribute eventAttribute)
        {
            sentEventCount++;
            lastEventID = eventNameID;
            if (eventNameID == VisualEffectAsset.PlayEventID) _awake = true;
            else if (eventNameID == VisualEffectAsset.StopEventID) _awake = false;
        }

        /// <summary>Restarts the effect: re-sends the initial event (the original's Reinit).</summary>
        public void Reinit()
        {
            _awake = true;
            if (!string.IsNullOrEmpty(initialEventName)) SendEvent(initialEventID);
        }

        public void AdvanceOneFrame() { }
        public void Simulate(float stepDeltaTime, uint stepCount = 1) { }

        // ---------------------------------------------------------------- properties

        bool Has<T>(int id) => (_overrides.TryGetValue(id, out var v) && v is T) || (visualEffectAsset != null && visualEffectAsset.Exposes(id, typeof(T)));
        T Get<T>(int id) => _overrides.TryGetValue(id, out var v) && v is T t ? t : default;
        void Set<T>(int id, T value) => _overrides[id] = value;
        static int Id(string name) => Shader.PropertyToID(name ?? string.Empty);

        public void ResetOverride(string name) => _overrides.Remove(Id(name));
        public void ResetOverride(int nameID) => _overrides.Remove(nameID);

        public bool HasFloat(string name) => Has<float>(Id(name));
        public bool HasFloat(int nameID) => Has<float>(nameID);
        public float GetFloat(string name) => Get<float>(Id(name));
        public float GetFloat(int nameID) => Get<float>(nameID);
        public void SetFloat(string name, float f) => Set(Id(name), f);
        public void SetFloat(int nameID, float f) => Set(nameID, f);

        public bool HasInt(string name) => Has<int>(Id(name));
        public bool HasInt(int nameID) => Has<int>(nameID);
        public int GetInt(string name) => Get<int>(Id(name));
        public int GetInt(int nameID) => Get<int>(nameID);
        public void SetInt(string name, int i) => Set(Id(name), i);
        public void SetInt(int nameID, int i) => Set(nameID, i);

        public bool HasUInt(string name) => Has<uint>(Id(name));
        public bool HasUInt(int nameID) => Has<uint>(nameID);
        public uint GetUInt(string name) => Get<uint>(Id(name));
        public uint GetUInt(int nameID) => Get<uint>(nameID);
        public void SetUInt(string name, uint i) => Set(Id(name), i);
        public void SetUInt(int nameID, uint i) => Set(nameID, i);

        public bool HasBool(string name) => Has<bool>(Id(name));
        public bool HasBool(int nameID) => Has<bool>(nameID);
        public bool GetBool(string name) => Get<bool>(Id(name));
        public bool GetBool(int nameID) => Get<bool>(nameID);
        public void SetBool(string name, bool b) => Set(Id(name), b);
        public void SetBool(int nameID, bool b) => Set(nameID, b);

        public bool HasVector2(string name) => Has<Vector2>(Id(name));
        public bool HasVector2(int nameID) => Has<Vector2>(nameID);
        public Vector2 GetVector2(string name) => Get<Vector2>(Id(name));
        public Vector2 GetVector2(int nameID) => Get<Vector2>(nameID);
        public void SetVector2(string name, Vector2 v) => Set(Id(name), v);
        public void SetVector2(int nameID, Vector2 v) => Set(nameID, v);

        public bool HasVector3(string name) => Has<Vector3>(Id(name));
        public bool HasVector3(int nameID) => Has<Vector3>(nameID);
        public Vector3 GetVector3(string name) => Get<Vector3>(Id(name));
        public Vector3 GetVector3(int nameID) => Get<Vector3>(nameID);
        public void SetVector3(string name, Vector3 v) => Set(Id(name), v);
        public void SetVector3(int nameID, Vector3 v) => Set(nameID, v);

        public bool HasVector4(string name) => Has<Vector4>(Id(name));
        public bool HasVector4(int nameID) => Has<Vector4>(nameID);
        public Vector4 GetVector4(string name) => Get<Vector4>(Id(name));
        public Vector4 GetVector4(int nameID) => Get<Vector4>(nameID);
        public void SetVector4(string name, Vector4 v) => Set(Id(name), v);
        public void SetVector4(int nameID, Vector4 v) => Set(nameID, v);

        public bool HasTexture(string name) => Has<Texture>(Id(name));
        public bool HasTexture(int nameID) => Has<Texture>(nameID);
        public Texture GetTexture(string name) => Get<Texture>(Id(name));
        public Texture GetTexture(int nameID) => Get<Texture>(nameID);
        public void SetTexture(string name, Texture t) => Set(Id(name), t);
        public void SetTexture(int nameID, Texture t) => Set(nameID, t);

        public bool HasMesh(string name) => Has<Mesh>(Id(name));
        public Mesh GetMesh(string name) => Get<Mesh>(Id(name));
        public void SetMesh(string name, Mesh m) => Set(Id(name), m);
        public void SetMesh(int nameID, Mesh m) => Set(nameID, m);

        public bool HasAnimationCurve(string name) => Has<AnimationCurve>(Id(name));
        public AnimationCurve GetAnimationCurve(string name) => Get<AnimationCurve>(Id(name));
        public void SetAnimationCurve(string name, AnimationCurve c) => Set(Id(name), c);

        public bool HasGradient(string name) => Has<Gradient>(Id(name));
        public Gradient GetGradient(string name) => Get<Gradient>(Id(name));
        public void SetGradient(string name, Gradient g) => Set(Id(name), g);
    }
}

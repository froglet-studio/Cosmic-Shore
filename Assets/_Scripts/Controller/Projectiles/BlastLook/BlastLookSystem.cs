using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws every live blast LOOK (BLAST_LOOK.md, <see cref="BlastLookSO"/>): the white-hot core, the
    /// faceted shock shell, the flash's point light, and the casing shards and embers that hang for
    /// seconds after the blast itself is gone.
    ///
    /// <para><b>Why a system and not a child of the blast.</b> A blast's GameObject is destroyed (or
    /// pooled) the moment its sweep ends, and the aftermath is meant to outlive it by seconds. So a
    /// blast hands its look over at the release (<see cref="Release"/>) and this one object, made on
    /// demand in the active scene, simulates and draws them all: no GameObject per shard, one dynamic
    /// mesh per blast (every shard and ember of it), drawn with <c>Graphics.RenderMesh</c> for every
    /// camera. It goes with the scene; a blast still hanging when the scene unloads goes with it.</para>
    ///
    /// <para>Visual only. Nothing here touches a collider, a prism or a score, and nothing reads it.
    /// Game time throughout, so slow motion and the replay director's frame stepping slow it too; the
    /// debris is seeded from the blast's position, so a replay throws the same shards.</para>
    /// </summary>
    public sealed class BlastLookSystem : MonoBehaviour
    {
        /// <summary>More blasts than this at once and the oldest look is retired early.</summary>
        const int MaxLiveBlasts = 12;

        static BlastLookSystem s_instance;
        static readonly ProfilerMarker s_frame = new("BlastLookSystem.LateUpdate");

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int HotColorId = Shader.PropertyToID("_HotColor");
        static readonly int HeatId = Shader.PropertyToID("_Heat");
        static readonly int HeatIntensityId = Shader.PropertyToID("_HeatIntensity");
        static readonly int SpreadId = Shader.PropertyToID("_Spread");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        readonly List<Run> _live = new();
        readonly Stack<Run> _spare = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        /// <summary>
        /// Start a blast's look at <paramref name="origin"/>. <paramref name="radius"/> is the blast's
        /// reach (half its MaxScale), <paramref name="blastSeconds"/> its sweep (the shell grows over it,
        /// on the sweep's own curve), <paramref name="signal"/> the shooter's colour (the domain rim the
        /// blast is drawn in). Returns false, and draws nothing, when the look has no materials.
        /// </summary>
        public static bool Release(BlastLookSO look, Vector3 origin, float radius, float blastSeconds, Color signal)
        {
            if (!look || radius <= 0f) return false;
            if (!look.CoreMaterial && !look.ShardMaterial && !look.ShellMaterial) return false;
            if (!s_instance)
            {
                var go = new GameObject("[Blast Look]");
                s_instance = go.AddComponent<BlastLookSystem>();
            }
            s_instance.Begin(look, origin, radius, blastSeconds, signal);
            return true;
        }

        /// <summary>Blasts whose look is still on screen (for the tests and the profiler).</summary>
        public static int LiveCount => s_instance ? s_instance._live.Count : 0;

        void Begin(BlastLookSO look, Vector3 origin, float radius, float blastSeconds, Color signal)
        {
            if (_live.Count >= MaxLiveBlasts)
            {
                Retire(_live[0]);
                _live.RemoveAt(0);
            }

            var run = _spare.Count > 0 ? _spare.Pop() : new Run();
            run.Start(look, origin, radius, blastSeconds, signal, transform);
            _live.Add(run);
        }

        void LateUpdate()
        {
            using (s_frame.Auto())
            {
                float dt = Time.deltaTime;
                for (int i = _live.Count - 1; i >= 0; i--)
                {
                    var run = _live[i];
                    if (!run.Tick(dt))
                    {
                        Retire(run);
                        _live.RemoveAt(i);
                        continue;
                    }
                    run.Draw();
                }
            }
        }

        void Retire(Run run)
        {
            run.Stop();
            _spare.Push(run);
        }

        void OnDestroy()
        {
            foreach (var run in _live) run.Dispose();
            foreach (var run in _spare) run.Dispose();
            _live.Clear();
            _spare.Clear();
            if (s_instance == this) s_instance = null;
        }

        /// <summary>One blast's look, from its release until its last ember has shrunk away.</summary>
        sealed class Run
        {
            BlastLookSO _look;
            Vector3 _origin;
            float _radius, _blastSeconds, _age, _end;
            Color _bright, _body, _hot;
            Quaternion _coreRotation;

            BlastDebris[] _debris = new BlastDebris[0];
            int _count;
            Mesh _mesh;
            Vector3[] _vertices = new Vector3[0];
            Vector2[] _uvs = new Vector2[0];
            int[] _indices = new int[0];
            int _topologyFor = -1;

            readonly MaterialPropertyBlock _coreProps = new();
            readonly MaterialPropertyBlock _shardProps = new();
            readonly MaterialPropertyBlock _shellProps = new();
            Light _light;

            public void Start(BlastLookSO look, Vector3 origin, float radius, float blastSeconds, Color signal,
                Transform parent)
            {
                _look = look;
                _origin = origin;
                _radius = radius;
                _blastSeconds = Mathf.Max(0.01f, blastSeconds);
                _age = 0f;
                _end = look.LongestLife(_blastSeconds);

                // The fleet's two-tone from the shooter's colour: the signal at full value, the body a
                // fraction of it, the hot colour the signal leant toward white (white is the identity in
                // every colour set, so nothing here leaves the worn set).
                Color.RGBToHSV(signal, out float hue, out float sat, out _);
                _bright = Color.HSVToRGB(hue, sat, 1f);
                _bright.a = 1f;
                _body = _bright * look.BodyShare;
                _body.a = 1f;
                _hot = Color.Lerp(_bright, Color.white, look.HotWhiteness);
                _hot.a = 1f;

                uint seed = BlastLookMath.SeedFor(origin);
                _coreRotation = Quaternion.LookRotation(
                    BlastLookMath.SphereDirection(BlastLookMath.Hash01(seed, -1, 0), BlastLookMath.Hash01(seed, -1, 1)));

                int shards = look.ShardMaterial ? look.ShardCount : 0;
                int embers = look.ShardMaterial ? look.EmberCount : 0;
                _count = shards + embers;
                if (_debris.Length < _count) _debris = new BlastDebris[_count];
                var shardSpec = look.Shards;
                var emberSpec = look.Embers;
                for (int i = 0; i < shards; i++)
                    _debris[i] = BlastLookMath.Seed(seed, i, origin, radius, shardSpec);
                for (int i = 0; i < embers; i++)
                    _debris[shards + i] = BlastLookMath.Seed(seed, 1000 + i, origin, radius, emberSpec);
                EnsureMesh();

                if (look.PointLight)
                {
                    if (!_light)
                    {
                        var go = new GameObject("Blast Flash");
                        go.transform.SetParent(parent, false);
                        _light = go.AddComponent<Light>();
                        _light.type = LightType.Point;
                        _light.shadows = LightShadows.None;
                        _light.renderMode = LightRenderMode.Auto;
                    }
                    _light.transform.position = origin;
                    _light.range = radius * look.LightRangeShare;
                    _light.color = _hot;
                    _light.intensity = look.LightIntensity;
                    _light.enabled = true;
                }
                else if (_light) _light.enabled = false;
            }

            public bool Tick(float dt)
            {
                _age += dt;
                for (int i = 0; i < _count; i++)
                    if (BlastLookMath.Alive(_debris[i])) BlastLookMath.Step(ref _debris[i], dt);

                if (_light && _light.enabled)
                {
                    float tau = Mathf.Max(0.01f, _look.LightSeconds);
                    if (_age > tau * 3f) _light.enabled = false;
                    else
                    {
                        float k = Mathf.Exp(-_age / tau);
                        _light.intensity = _look.LightIntensity * k;
                        _light.color = Color.Lerp(_bright, _hot, k);
                    }
                }
                return _age < _end;
            }

            public void Draw()
            {
                DrawCore();
                DrawShell();
                DrawDebris();
            }

            void DrawCore()
            {
                var material = _look.CoreMaterial;
                if (!material) return;
                float grow = _look.CoreGrowSeconds, hold = _look.CoreHoldSeconds, fade = _look.CoreFadeSeconds;
                float scale = BlastLookMath.CoreScale(_age, grow, hold, fade);
                if (scale <= 0.001f) return;
                float coreRadius = Mathf.Clamp(_radius * _look.CoreRadiusShare, _look.CoreRadiusClamp.x, _look.CoreRadiusClamp.y);

                _coreProps.SetColor(BrightColorId, _bright);
                _coreProps.SetColor(DarkColorId, _body);
                _coreProps.SetColor(HotColorId, _hot);
                _coreProps.SetFloat(HeatId, BlastLookMath.CoreHeat(_age, grow, hold, fade));
                _coreProps.SetFloat(HeatIntensityId, _look.CoreHeatIntensity);
                _coreProps.SetFloat(SpreadId, 0f);
                var rp = Params(material, _coreProps);
                // The icosphere's radius is 0.5, so its scale is the diameter. It turns slowly as it
                // collapses so its facets catch the camera.
                var rotation = Quaternion.AngleAxis(_age * 90f, Vector3.up) * _coreRotation;
                Graphics.RenderMesh(rp, BlastLookMeshes.Core, 0,
                    Matrix4x4.TRS(_origin, rotation, Vector3.one * (2f * coreRadius * scale)));
            }

            void DrawShell()
            {
                var material = _look.ShellMaterial;
                if (!material || _age >= _blastSeconds) return;
                float growth = BlastLookMath.ShellGrowth(_age, _blastSeconds);
                if (growth <= 0.001f) return;
                float opacity = _look.ShellOpacity * Mathf.Pow(1f - growth, Mathf.Max(0.01f, _look.ShellFadePower));
                if (opacity <= 0.002f) return;

                _shellProps.SetColor(BrightColorId, _bright);
                _shellProps.SetColor(HotColorId, _hot);
                _shellProps.SetFloat(HeatId, BlastLookMath.Heat(_age, _look.ShellHeatSeconds));
                _shellProps.SetFloat(OpacityId, opacity);
                _shellProps.SetFloat(IntensityId, _look.ShellIntensity);
                var rp = Params(material, _shellProps);
                Graphics.RenderMesh(rp, BlastLookMeshes.Shell, 0,
                    Matrix4x4.TRS(_origin, _coreRotation, Vector3.one * (2f * _radius * growth)));
            }

            void DrawDebris()
            {
                var material = _look.ShardMaterial;
                if (!material || _count == 0) return;

                var shape = BlastLookMeshes.ShardVertices;
                float shrinkShare = _look.ShrinkShare, streak = _look.EmberStreakSeconds;
                int live = 0;
                for (int i = 0; i < _count; i++)
                {
                    ref var d = ref _debris[i];
                    int v0 = i * BlastLookMeshes.ShardVertexCount;
                    float s = BlastLookMath.Alive(d) ? BlastLookMath.Shrink(d.Age, d.Life, shrinkShare) : 0f;
                    if (s <= 0f)
                    {
                        // A spent piece collapses to a point: no triangle area, nothing drawn.
                        for (int k = 0; k < BlastLookMeshes.ShardVertexCount; k++) _vertices[v0 + k] = d.Position;
                        continue;
                    }
                    live++;

                    var size = d.Size * s;
                    var rotation = d.Rotation;
                    if (d.Streaks)
                    {
                        // An ember points along its flight and stretches with its speed: a streak while it
                        // is fast, a speck once drag has stopped it.
                        float speed = d.Velocity.magnitude;
                        if (speed > 1e-3f) rotation = Quaternion.LookRotation(d.Velocity / speed, d.SpinAxis);
                        size.z += speed * streak * s;
                    }

                    float heat = BlastLookMath.Heat(d.Age, d.Cool);
                    for (int k = 0; k < BlastLookMeshes.ShardVertexCount; k++)
                    {
                        _vertices[v0 + k] = d.Position + rotation * Vector3.Scale(shape[k], size);
                        _uvs[v0 + k] = new Vector2(heat, 0f);
                    }
                }
                if (live == 0) return;

                _mesh.SetVertices(_vertices, 0, _count * BlastLookMeshes.ShardVertexCount);
                _mesh.SetUVs(0, _uvs, 0, _count * BlastLookMeshes.ShardVertexCount);
                // World-space vertices: the bounds are the whole reach, so no camera culls a hanging shard.
                _mesh.bounds = new Bounds(_origin, Vector3.one * (_radius * 2.4f + 10f));

                _shardProps.SetColor(BrightColorId, _bright);
                _shardProps.SetColor(DarkColorId, _body);
                _shardProps.SetColor(HotColorId, _hot);
                _shardProps.SetFloat(HeatIntensityId, _look.ShardHeatIntensity);
                _shardProps.SetFloat(SpreadId, 0f);
                Graphics.RenderMesh(Params(material, _shardProps), _mesh, 0, Matrix4x4.identity);
            }

            static RenderParams Params(Material material, MaterialPropertyBlock props) => new(material)
            {
                matProps = props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
            };

            /// <summary>One mesh for every shard and ember of the blast: the vertices move each frame,
            /// the triangles only when the piece count changes.</summary>
            void EnsureMesh()
            {
                if (!_mesh)
                {
                    _mesh = new Mesh { name = "BlastDebris", hideFlags = HideFlags.DontSave };
                    _mesh.MarkDynamic();
                }
                int vertexCount = _count * BlastLookMeshes.ShardVertexCount;
                if (_vertices.Length < vertexCount)
                {
                    _vertices = new Vector3[vertexCount];
                    _uvs = new Vector2[vertexCount];
                }
                if (_topologyFor == _count) return;

                _mesh.Clear();
                _indices = new int[_count * BlastLookMeshes.ShardIndexCount];
                var tris = BlastLookMeshes.ShardTriangles;
                for (int i = 0; i < _count; i++)
                    for (int k = 0; k < BlastLookMeshes.ShardIndexCount; k++)
                        _indices[i * BlastLookMeshes.ShardIndexCount + k] = i * BlastLookMeshes.ShardVertexCount + tris[k];
                for (int i = 0; i < vertexCount; i++) _vertices[i] = _origin;
                _mesh.SetVertices(_vertices, 0, vertexCount);
                _mesh.SetUVs(0, _uvs, 0, vertexCount);
                _mesh.SetTriangles(_indices, 0, false);
                _topologyFor = _count;
            }

            public void Stop()
            {
                if (_light) _light.enabled = false;
                _look = null;
            }

            public void Dispose()
            {
                if (_mesh) Object.Destroy(_mesh);
                _mesh = null;
            }
        }
    }
}

using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark's leash, shown: a ghostly lattice sphere centred on the Ark at the leash radius (the Arkway's
    /// <c>TickLeash</c>, three cell radii). It is dark until the pilot nears it, brightens in a patch where they are about
    /// to cross, and pulses while they are past it and the recall countdown runs. Garrett, 2026-11-01: "when we get to a
    /// point where we are about to cross the maximum distance from the ark, show a cool ghostly surface that represents the
    /// point where we only have a few seconds to return."
    ///
    /// <para>A view of a rule that already exists, never a rule of its own: the leash, its grace and its recall stay in
    /// <see cref="ArkwayRun"/>; this only draws where that line is. One per voyage, owned by the run.</para>
    /// </summary>
    public sealed class ArkLeashShell : MonoBehaviour
    {
        const string ShaderPath = "Voyages/ArkLeashShell";
        const float ShowFrom = 0.8f;          // the shell starts to show at this fraction of the leash
        const float FullAt = 0.97f;           // and is at full strength here
        const float RevealFraction = 0.35f;   // the bright patch's radius, as a fraction of the leash
        const float FadeSeconds = 0.35f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RevealPosId = Shader.PropertyToID("_RevealPos");
        static readonly int RevealRadiusId = Shader.PropertyToID("_RevealRadius");
        static readonly int GridScaleId = Shader.PropertyToID("_GridScale");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");

        /// <summary>The live shell, or null (one voyage runs at a time).</summary>
        public static ArkLeashShell Current { get; private set; }

        Material _material;
        MeshRenderer _renderer;
        float _radius;
        float _intensity, _targetIntensity;
        float _pulse;
        Vector3 _reveal;
        float _previewUntil = -1f;

        /// <summary>The leash radius the shell is drawn at (0 before the first update).</summary>
        public float Radius => _radius;

        /// <summary>How strongly the shell shows now, 0..1.</summary>
        public float Intensity => _intensity;

        public static ArkLeashShell Create(Transform parent, Color colour)
        {
            var shader = Resources.Load<Shader>(ShaderPath);
            if (!shader)
            {
                CSDebug.LogWarning($"[Arkway] No leash shell shader at Resources/{ShaderPath} - the leash is not drawn.");
                return null;
            }
            var go = new GameObject("ArkLeashShell");
            go.transform.SetParent(parent, false);
            var shell = go.AddComponent<ArkLeashShell>();
            shell._material = new Material(shader) { name = "ArkLeashShell (runtime)" };
            shell._material.SetColor(ColorId, colour);
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            shell._renderer = go.AddComponent<MeshRenderer>();
            shell._renderer.sharedMaterial = shell._material;
            shell._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell._renderer.receiveShadows = false;
            shell._renderer.enabled = false;
            Current = shell;
            return shell;
        }

        /// <summary>
        /// Where the leash is and where the pilot is: <paramref name="centre"/> (the Ark), <paramref name="leash"/> (its
        /// radius), <paramref name="pilot"/>, and whether the pilot is past it with the countdown running.
        /// </summary>
        public void Track(Vector3 centre, float leash, Vector3 pilot, bool breached)
        {
            if (leash <= 0f) { _targetIntensity = 0f; return; }
            _radius = leash;
            transform.position = centre;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * leash;
            var to = pilot - centre;
            float dist = to.magnitude;
            _reveal = centre + (dist > 1e-3f ? to / dist : Vector3.up) * leash;
            float t = Mathf.InverseLerp(ShowFrom * leash, FullAt * leash, dist);
            _targetIntensity = breached ? 1f : t * t * (3f - 2f * t);
            _pulse = breached ? 1f : 0f;
        }

        /// <summary>The shell off (no pilot, or the voyage between legs).</summary>
        public void Hide() => _targetIntensity = 0f;

        /// <summary>Show the patch at <paramref name="at"/> at <paramref name="amount"/> now, for a still (the sim's
        /// camera); 0 hands the shell back to <see cref="Track"/>.</summary>
        public void PreviewReveal(Vector3 at, float amount)
        {
            if (amount <= 0f) { _previewUntil = -1f; return; }
            _previewUntil = Time.unscaledTime + 2f;
            _reveal = at;
            _intensity = amount;
            Apply();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < _previewUntil) return;
            float step = Time.unscaledDeltaTime / FadeSeconds;
            _intensity = Mathf.MoveTowards(_intensity, _targetIntensity, step);
            Apply();
        }

        void Apply()
        {
            if (!_material || !_renderer) return;
            bool show = _intensity > 0.001f && _radius > 0f;
            _renderer.enabled = show;
            if (!show) return;
            _material.SetFloat(IntensityId, _intensity);
            _material.SetVector(RevealPosId, _reveal);
            _material.SetFloat(RevealRadiusId, _radius * RevealFraction);
            _material.SetFloat(GridScaleId, Mathf.Max(8f, _radius / 55f));
            _material.SetFloat(PulseId, _pulse);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_material) Destroy(_material);
        }

        static Mesh s_sphere;

        /// <summary>A unit UV sphere, normals out, fine enough to stay round at thousands of units.</summary>
        static Mesh SphereMesh()
        {
            if (s_sphere) return s_sphere;
            const int lon = 96, lat = 48;
            var verts = new Vector3[(lon + 1) * (lat + 1)];
            var tris = new int[lon * lat * 6];
            int v = 0;
            for (int y = 0; y <= lat; y++)
            {
                float a = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float b = 2f * Mathf.PI * x / lon;
                    verts[v++] = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                }
            }
            int t = 0;
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i0 = y * (lon + 1) + x, i1 = i0 + 1, i2 = i0 + lon + 1, i3 = i2 + 1;
                    tris[t++] = i0; tris[t++] = i1; tris[t++] = i2;
                    tris[t++] = i1; tris[t++] = i3; tris[t++] = i2;
                }
            s_sphere = new Mesh { name = "ArkLeashShellSphere" };
            s_sphere.vertices = verts;
            s_sphere.normals = verts;
            s_sphere.triangles = tris;
            s_sphere.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return s_sphere;
        }
    }
}

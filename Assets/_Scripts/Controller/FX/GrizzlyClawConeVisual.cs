using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The plasma claw's spray visual: an additive cone drawn from the vessel's nose
    /// while the claw is held.
    ///
    /// The cone is GENERATED from the ability's own authored <c>Range</c> and
    /// <c>ConeHalfAngle</c> rather than authored as art, so the thing the player sees
    /// is the thing that ignites — a hand-modelled cone would drift from the query the
    /// first time either value is retuned, and a weapon whose visual lies about its
    /// reach is worse than one with no visual at all.
    ///
    /// Created on demand (no prefab wiring), parented to the vessel so it inherits
    /// aim for free, and rebuilt only when the shape actually changes.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GrizzlyClawConeVisual : MonoBehaviour
    {
        const int RadialSegments = 24;

        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _mesh;
        Material _material;

        float _builtRange = -1f;
        float _builtHalfAngle = -1f;

        // Hot plasma: additive, so it can only ADD light and never darkens what it sweeps.
        static readonly Color CoreColor = new Color(1f, 0.45f, 0.12f, 1f);

        /// <summary>Gets (or creates) the claw cone hanging off this vessel.</summary>
        public static GrizzlyClawConeVisual EnsureFor(Transform vessel)
        {
            if (vessel == null) return null;

            var existing = vessel.GetComponentInChildren<GrizzlyClawConeVisual>(true);
            if (existing != null) return existing;

            var go = new GameObject("PlasmaClawCone");
            go.transform.SetParent(vessel, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.AddComponent<GrizzlyClawConeVisual>();
        }

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "PlasmaClawCone" };
            _filter.sharedMesh = _mesh;

            _material = BuildAdditiveMaterial();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _renderer.enabled = false;
        }

        /// <summary>Show the cone for an ability whose reach is <paramref name="range"/>.</summary>
        public void Show(float range, float halfAngleDegrees)
        {
            if (range <= 0f || halfAngleDegrees <= 0f) return;

            if (!Mathf.Approximately(range, _builtRange) ||
                !Mathf.Approximately(halfAngleDegrees, _builtHalfAngle))
            {
                Rebuild(range, halfAngleDegrees);
                _builtRange = range;
                _builtHalfAngle = halfAngleDegrees;
            }

            if (_renderer) _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer) _renderer.enabled = false;
        }

        /// <summary>
        /// Apex at the origin, opening along local +z — the same axis the ignite query
        /// tests against the vessel's forward. Side surface only (no base cap) and
        /// double-sided, so the cone still reads when the camera is inside it.
        /// </summary>
        void Rebuild(float range, float halfAngleDegrees)
        {
            float radius = range * Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad);

            var verts = new Vector3[RadialSegments + 1];
            verts[0] = Vector3.zero;
            for (int i = 0; i < RadialSegments; i++)
            {
                float t = (float)i / RadialSegments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, range);
            }

            var tris = new int[RadialSegments * 6];
            for (int i = 0; i < RadialSegments; i++)
            {
                int a = i + 1;
                int b = (i + 1) % RadialSegments + 1;
                int o = i * 6;
                tris[o] = 0; tris[o + 1] = a; tris[o + 2] = b;          // outside
                tris[o + 3] = 0; tris[o + 4] = b; tris[o + 5] = a;      // inside
            }

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();
        }

        static Material BuildAdditiveMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "PlasmaClawCone" };

            mat.SetFloat("_Surface", 1f);                 // transparent
            mat.SetFloat("_Blend", 2f);                   // additive
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Kept dim on purpose: the cone is a reach indicator drawn over live mass,
            // and at full strength it washes out the prisms it is telling you about.
            var c = CoreColor * 0.22f;
            c.a = 1f;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

            return mat;
        }

        void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
            if (_material) Destroy(_material);
        }
    }
}

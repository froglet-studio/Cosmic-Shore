using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    /// <summary>
    /// Ability: Build a fully procedural "assembly branch" structure in one shot (no progressive growth).
    /// TODO This class will be deleted in the future
    /// </summary>
    public class AssembledArchBurstAction : ShipAction
    {
    // ---------- Config ----------
        [Header("Block/Rod Prefab")]
        [Tooltip("A thin bar/rod prefab (the yellow segment). Its local Z axis should point along length.")]
        [SerializeField] private GameObject rodPrefab;

        [Header("Placement Ray")]
        [SerializeField] private float rayDistance = 200f;
        [SerializeField] private LayerMask rayMask = ~0;
        [SerializeField] private float fallbackForward = 60f;

        [Header("Surface Patch (Vessel-local)")]
        [Tooltip("Total lateral width (Right axis).")]
        [SerializeField] private float width = 80f;
        [Tooltip("Total height (Up axis).")]
        [SerializeField] private float height = 50f;
        [Tooltip("How far forward the patch bows (Forward axis).")]
        [SerializeField] private float bowDepth = 35f;
        [Tooltip("Forward offset from the ray hit/fallback point.")]
        [SerializeField] private float forwardPush = 0f;

        [Header("UV Tessellation")]
        [Tooltip("Number of divisions across width (U).")]
        [SerializeField] private int uDiv = 8;
        [Tooltip("Number of divisions across height (V).")]
        [SerializeField] private int vDiv = 6;

        public enum LatticeType { Tri, Hex }
        [SerializeField] private LatticeType lattice = LatticeType.Tri;

        [Header("Organic Variation")]
        [Tooltip("0 = random each activation; otherwise deterministic.")]
        [SerializeField] private int seed = 0;
        [SerializeField] private float jitter = 0.15f;    // UV-space jitter
        [SerializeField] private float surfaceNoise = 0.2f; // meters, pushes along surface binormal
        [SerializeField] private float surfaceNoiseScale = 1.4f;

        [Header("Thickness (layers)")]
        [Tooltip("Duplicate the lattice N layers along the patch normal.")]
        [SerializeField] private int layers = 1;
        [SerializeField] private float layerSpacing = 1.25f;

        [Header("Parenting")]
        [SerializeField] private Transform structureParent;
        
        [Tooltip("Uniform local scale applied to every spawned block/rod.")]
        [SerializeField] private Vector3 blockScale;
        [SerializeField] private bool enforceScaleNextFrame = true; 

        private Transform _container;
        private System.Random _rng;
        private float _perlinBase;

        public override void StartAction()
        {
            if (rodPrefab == null) return;
            
            // Seed RNG
            int s = (seed != 0) ? seed : (int)(Random.value * int.MaxValue);
            _rng = new System.Random(s);
            _perlinBase = (float)_rng.NextDouble() * 1000f;

            // Local vessel frame & ray
            Transform t = Vessel.Transform;
            Vector3 origin = t.position;
            Vector3 fwd = t.forward;
            Vector3 up = t.up;
            Vector3 right = t.right;

            bool hitFound = Physics.Raycast(origin, fwd, out var hit, rayDistance, rayMask, QueryTriggerInteraction.Ignore);
            Vector3 basePoint = hitFound ? hit.point : origin + fwd * fallbackForward;
            basePoint += fwd * forwardPush;

            // Build parametric surface: position(U,V) and frame
            // Spine (U) as quadratic Bezier across width, bowed forward by bowDepth.
            Vector3 uP0 = basePoint - right * (width * 0.5f);
            Vector3 uP2 = basePoint + right * (width * 0.5f);
            Vector3 uP1 = basePoint + fwd * bowDepth; // bow apex

            // Cross-section (V) as symmetric vertical arc around the spine (mild lift).
            // We'll compute final position as: P(u) + up * h(v) + small forward bow on the edges.
            // h(v) is centered, so v in [0..1] maps to [-height/2..+height/2].
            float halfH = height * 0.5f;

            // Generate UV points
            var points = new Vector3[(uDiv + 1) * (vDiv + 1)];
            var normals = new Vector3[(uDiv + 1) * (vDiv + 1)];
            for (int iu = 0; iu <= uDiv; iu++)
            {
                float u = iu / (float)uDiv;
                Vector3 Pu = EvalQBez(uP0, uP1, uP2, u);
                Vector3 Tu = EvalQBezTangent(uP0, uP1, uP2, u).normalized;

                // Local frame along spine
                Vector3 Nu = Vector3.Cross(Tu, up).sqrMagnitude > 1e-6f ? Vector3.Cross(Tu, up).normalized : right;
                Vector3 Bu = Vector3.Cross(Nu, Tu).normalized; // "up-ish" binormal

                for (int iv = 0; iv <= vDiv; iv++)
                {
                    float v = iv / (float)vDiv;            // [0..1]
                    float vCentered = (v - 0.5f) * 2f;     // [-1..1]
                    Vector3 p = Pu + Bu * (vCentered * halfH);

                    // UV jitter
                    float jU = ((float)_rng.NextDouble() - 0.5f) * jitter;
                    float jV = ((float)_rng.NextDouble() - 0.5f) * jitter;
                    Vector3 jittered = p + Nu * (jU * (width / uDiv)) + Bu * (jV * (height / vDiv));

                    // light surface wobble
                    float n = Mathf.PerlinNoise(_perlinBase + u * surfaceNoiseScale, _perlinBase + v * surfaceNoiseScale);
                    Vector3 pFinal = jittered + Nu * ((n - 0.5f) * 2f * surfaceNoise);

                    int idx = Index(iu, iv);
                    points[idx] = pFinal;
                    normals[idx] = Vector3.Cross(Nu, Tu).normalized; // approx surface normal
                }
            }

            // Build edges for chosen lattice in UV index space (avoid duplicates)
            var edges = new HashSet<(int a, int b)>();
            switch (lattice)
            {
                case LatticeType.Tri:
                    BuildTriEdges(uDiv, vDiv, edges);
                    break;
                case LatticeType.Hex:
                    BuildHexEdges(uDiv, vDiv, edges);
                    break;
            }

            // Create container & instantiate rods per layer
            if (_container != null) Destroy(_container.gameObject);
            _container = new GameObject($"Lattice_{gameObject.name}_{Time.frameCount}").transform;
            _container.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _container.localScale = Vector3.one;      
            if (structureParent) _container.SetParent(structureParent, true);

            int total = 0;
            for (int layer = 0; layer < Mathf.Max(1, layers); layer++)
            {
                float offset = (layer - (layers - 1) * 0.5f) * layerSpacing;

                foreach (var e in edges)
                {
                    Vector3 a = points[e.a] + normals[e.a] * offset;
                    Vector3 b = points[e.b] + normals[e.b] * offset;

                    SpawnRod(a, b, origin, up);
                    total++;
                }
            }

            Debug.Log($"[SpawnLatticeShieldAction] Spawned {total} rods. hit={hitFound} seed={s} type={lattice}");
        }

        public override void StopAction()
        {
            if (_container != null)
            {
                // Destroy(_container.gameObject);
                // _container = null;
                // Debug.Log("[SpawnLatticeShieldAction] Cleared lattice.");
            }
        }

        // ---------- Helpers ----------
        int Index(int iu, int iv) => iu * (vDiv + 1) + iv;

        void BuildTriEdges(int uN, int vN, HashSet<(int a, int b)> edges)
        {
            for (int iu = 0; iu < uN; iu++)
            {
                for (int iv = 0; iv < vN; iv++)
                {
                    int i00 = Index(iu, iv);
                    int i10 = Index(iu + 1, iv);
                    int i01 = Index(iu, iv + 1);
                    int i11 = Index(iu + 1, iv + 1);

                    // Two triangles per quad; alternate diagonal for variation
                    bool diag = ((iu + iv) & 1) == 0;

                    AddEdge(edges, i00, i10);
                    AddEdge(edges, i00, i01);
                    AddEdge(edges, i10, i11);
                    AddEdge(edges, i01, i11);

                    if (diag) AddEdge(edges, i00, i11);
                    else      AddEdge(edges, i10, i01);
                }
            }
        }

        void BuildHexEdges(int uN, int vN, HashSet<(int a, int b)> edges)
        {
            // Hex pattern from offset rows; connect near neighbors (right/up-right/up-left)
            for (int iu = 0; iu <= uN; iu++)
            {
                for (int iv = 0; iv <= vN; iv++)
                {
                    int i = Index(iu, iv);

                    int iuR = Mathf.Min(uN, iu + 1);
                    int ivU = Mathf.Min(vN, iv + 1);

                    // horizontal
                    AddEdge(edges, i, Index(iuR, iv));

                    // diagonals based on row parity (stagger)
                    bool odd = (iv & 1) == 1;
                    int iuDiagA = Mathf.Clamp(iu + (odd ? 1 : 0), 0, uN);
                    int iuDiagB = Mathf.Clamp(iu + (odd ? 0 : -1), 0, uN);

                    AddEdge(edges, i, Index(iuDiagA, ivU));
                    AddEdge(edges, i, Index(iuDiagB, ivU));
                }
            }
        }

        void AddEdge(HashSet<(int a, int b)> set, int a, int b)
        {
            if (a == b) return;
            if (a > b) (a, b) = (b, a);
            set.Add((a, b));
        }

        static Vector3 EvalQBez(in Vector3 p0, in Vector3 p1, in Vector3 p2, float t)
        {
            float u = 1f - t;
            return (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
        }

        static Vector3 EvalQBezTangent(in Vector3 p0, in Vector3 p1, in Vector3 p2, float t)
        {
            return 2f * ((1f - t) * (p1 - p0) + t * (p2 - p1));
        }

        void SpawnRod(Vector3 a, Vector3 b, Vector3 shipPos, Vector3 up)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = (b - a).normalized;

            SafeLookRotation.TryGet(dir, up, out var rotation, _container ? _container.gameObject : null);

            var go = Instantiate(rodPrefab, mid, rotation, _container);

            ApplyBlockScale(go.transform);
        }

        void ApplyBlockScale(Transform t)
        {
            t.localScale = blockScale;

            if (enforceScaleNextFrame) StartCoroutine(ApplyScaleNextFrame(t));
        }

        System.Collections.IEnumerator ApplyScaleNextFrame(Transform t)
        {
            yield return null;         
            if (t != null) t.localScale = blockScale;
        }
    }
}
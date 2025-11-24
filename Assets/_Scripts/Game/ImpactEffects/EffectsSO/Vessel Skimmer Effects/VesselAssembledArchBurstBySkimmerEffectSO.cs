using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselAssembledArchBurstBySkimmerEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselAssembledArchBurstBySkimmerEffectSO")]
    public class VesselAssembledArchBurstBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        #region Config Values

        [Header("Block/Rod Prefab")]
        [Tooltip("Thin bar/rod prefab. Local Z should point along the length.")]
        [SerializeField] private GameObject rodPrefab;

        [Header("Placement Ray (from IMPACTEE vessel)")]
        [SerializeField] private float rayDistance = 200f;
        [SerializeField] private LayerMask rayMask = ~0;
        [SerializeField] private float fallbackForward = 60f;
        [SerializeField] private float forwardPush = 0f;

        [Header("Surface Patch (Impactee-local)")]
        [SerializeField] private float width = 80f;   // right axis
        [SerializeField] private float height = 50f;  // up axis
        [SerializeField] private float bowDepth = 35f;

        [Header("UV Tessellation")]
        [SerializeField] private int uDiv = 8;
        [SerializeField] private int vDiv = 6;

        public enum LatticeType { Tri, Hex }
        [SerializeField] private LatticeType lattice = LatticeType.Tri;

        [Header("Organic Variation")]
        [Tooltip("0 = random each activation; otherwise deterministic.")]
        [SerializeField] private int seed = 0;
        [SerializeField] private float jitter = 0.15f;          // UV-space jitter
        [SerializeField] private float surfaceNoise = 0.2f;     // meters along surface normal
        [SerializeField] private float surfaceNoiseScale = 1.4f;

        [Header("Thickness (layers)")]
        [SerializeField] private int layers = 1;
        [SerializeField] private float layerSpacing = 1.25f;

        [Header("Rod Scale")]
        [SerializeField] private Vector3 blockScale = Vector3.one;

        [SerializeField] private bool verbose = false;

        #endregion
        
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (rodPrefab == null) return;
            var targetShip = impactee.Skimmer.VesselStatus;

            // RNG
            int s = (seed != 0) ? seed : (int)(Random.value * int.MaxValue);
            var rng = new System.Random(s);
            float perlinBase = (float)rng.NextDouble() * 1000f;

            // ---- Impactee frame (NOT the impactor) ----
            Transform t = targetShip.Transform;
            Vector3 origin = t.position;
            Vector3 fwd    = t.forward;
            Vector3 up     = t.up;
            Vector3 right  = t.right;

            bool hitFound = Physics.Raycast(origin, fwd, out var hit, rayDistance, rayMask, QueryTriggerInteraction.Ignore);
            Vector3 basePoint = hitFound ? hit.point : origin + fwd * fallbackForward;
            basePoint += fwd * forwardPush;

            // Bézier spine across width, bowed forward by bowDepth
            Vector3 uP0 = basePoint - right * (width * 0.5f);
            Vector3 uP2 = basePoint + right * (width * 0.5f);
            Vector3 uP1 = basePoint + fwd * bowDepth;

            float halfH = height * 0.5f;
            int uCount = uDiv + 1;
            int vCount = vDiv + 1;

            var points  = new Vector3[uCount * vCount];
            var normals = new Vector3[uCount * vCount];

            for (int iu = 0; iu <= uDiv; iu++)
            {
                float u = iu / (float)uDiv;
                Vector3 Pu = EvalQBez(uP0, uP1, uP2, u);
                Vector3 Tu = EvalQBezTangent(uP0, uP1, uP2, u).normalized;

                Vector3 Nu = Vector3.Cross(Tu, up).sqrMagnitude > 1e-6f ? Vector3.Cross(Tu, up).normalized : right;
                Vector3 Bu = Vector3.Cross(Nu, Tu).normalized;

                for (int iv = 0; iv <= vDiv; iv++)
                {
                    float v = iv / (float)vDiv;
                    float vCentered = (v - 0.5f) * 2f;

                    Vector3 p = Pu + Bu * (vCentered * halfH);

                    // UV jitter
                    float jU = ((float)rng.NextDouble() - 0.5f) * jitter;
                    float jV = ((float)rng.NextDouble() - 0.5f) * jitter;
                    Vector3 jittered = p + Nu * (jU * (width / uDiv)) + Bu * (jV * (height / vDiv));

                    // surface wobble along Nu
                    float n = Mathf.PerlinNoise(perlinBase + u * surfaceNoiseScale, perlinBase + v * surfaceNoiseScale);
                    Vector3 pFinal = jittered + Nu * ((n - 0.5f) * 2f * surfaceNoise);

                    int idx = Index(iu, iv, vCount);
                    points[idx]  = pFinal;
                    normals[idx] = Vector3.Cross(Nu, Tu).normalized;
                }
            }

            // Edges
            var edges = new HashSet<(int a, int b)>();
            switch (lattice)
            {
                case LatticeType.Tri: BuildTriEdges(uDiv, vDiv, vCount, edges); break;
                case LatticeType.Hex: BuildHexEdges(uDiv, vDiv, vCount, edges); break;
            }

            // Container in WORLD (no parent)
            var container = new GameObject($"Lattice_{targetShip}_{Time.frameCount}").transform;
            container.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            container.SetParent(null, true); // explicit: no parent

            // Instantiate rods per layer
            int total = 0;
            int layerCount = Mathf.Max(1, layers);

            for (int layer = 0; layer < layerCount; layer++)
            {
                float offset = (layer - (layerCount - 1) * 0.5f) * layerSpacing;

                foreach (var e in edges)
                {
                    Vector3 a = points[e.a] + normals[e.a] * offset;
                    Vector3 b = points[e.b] + normals[e.b] * offset;
                    SpawnRod(a, b, up, container);
                    total++;
                }
            }

            if (verbose)
                Debug.Log($"[AssembledArchBurstEffectSO] Spawned {total} rods in front of IMPACTEE. hit={hitFound} seed={s} type={lattice}", container);
        }

        // ---- Helpers (pure/static) ----
        static int Index(int iu, int iv, int vCount) => iu * vCount + iv;

        static void BuildTriEdges(int uN, int vN, int vCount, HashSet<(int a, int b)> edges)
        {
            for (int iu = 0; iu < uN; iu++)
            {
                for (int iv = 0; iv < vN; iv++)
                {
                    int i00 = Index(iu,     iv,     vCount);
                    int i10 = Index(iu + 1, iv,     vCount);
                    int i01 = Index(iu,     iv + 1, vCount);
                    int i11 = Index(iu + 1, iv + 1, vCount);

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

        static void BuildHexEdges(int uN, int vN, int vCount, HashSet<(int a, int b)> edges)
        {
            for (int iu = 0; iu <= uN; iu++)
            {
                for (int iv = 0; iv <= vN; iv++)
                {
                    int i   = Index(iu, iv, vCount);
                    int iuR = Mathf.Min(uN, iu + 1);
                    int ivU = Mathf.Min(vN, iv + 1);

                    AddEdge(edges, i, Index(iuR, iv,  vCount)); // horizontal

                    bool odd = (iv & 1) == 1;
                    int iuDiagA = Mathf.Clamp(iu + (odd ? 1 : 0), 0, uN);
                    int iuDiagB = Mathf.Clamp(iu + (odd ? 0 : -1), 0, uN);

                    AddEdge(edges, i, Index(iuDiagA, ivU, vCount));
                    AddEdge(edges, i, Index(iuDiagB, ivU, vCount));
                }
            }
        }

        static void AddEdge(HashSet<(int a, int b)> set, int a, int b)
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

        void SpawnRod(Vector3 a, Vector3 b, Vector3 up, Transform parent)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = (b - a).normalized;

            SafeLookRotation.TryGet(dir, up, out var rotation, parent ? parent.gameObject : null);

            var go = Object.Instantiate(rodPrefab, mid, rotation, parent);
            go.transform.localScale = blockScale;
        }
    }
}
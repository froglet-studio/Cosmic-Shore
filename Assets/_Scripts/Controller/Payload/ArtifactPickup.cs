using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An artifact loose in the world (Docs/Pilots/ARTIFACTS.md §5): spilled by an Ark that fauna
    /// felled, so nobody earned it. <b>Anyone</b> takes it with RB in reach (<see cref="PayloadCarrier"/>),
    /// carries it on the hull, and RB beside their own squad's Ark stows it in that hold; RB anywhere
    /// else lets it go where it is.
    ///
    /// <para><b>Not mass.</b> An artifact is a thing aboard a ship, not plating, so a pickup is not a
    /// prism: fauna cannot eat it, nothing scores it, and it is in no cell's sums. It is drawn as a
    /// small lit, faceted body (the octahedron below; placeholder art until /vessel-model or
    /// /ability-icon gives artifacts a look) with <c>PayloadConfig</c>'s pickup material.</para>
    ///
    /// <para><b>Gone when the instance ends.</b> A pickup is a scene root, so it dies with the scene;
    /// a mode that ends while its scene stays calls <see cref="StrikeAll"/>. Uncollected artifacts are
    /// lost for good, never returned to their owner.</para>
    ///
    /// <para><b>The authority decides every claim</b>, as the hold decides every grab: today the local
    /// toy's machine (<see cref="ArkHold.IsAuthority"/>).</para>
    /// </summary>
    public sealed class ArtifactPickup : MonoBehaviour
    {
        const float SpinDegreesPerSecond = 40f;

        static readonly List<ArtifactPickup> s_loose = new();
        static Mesh s_mesh;

        PayloadCarrier _holder;
        Transform _body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loose.Clear();
            s_mesh = null;
        }

        /// <summary>The artifact this is (the id <c>SquadSlot.ArtifactId</c> names).</summary>
        public string ArtifactId { get; private set; }

        /// <summary>The vessel carrying it, or null while it floats.</summary>
        public PayloadCarrier Holder => _holder;

        /// <summary>Pickups standing now, carried or not.</summary>
        public static int Count => s_loose.Count;

        /// <summary>Spill <paramref name="artifactId"/> at <paramref name="position"/>.</summary>
        public static ArtifactPickup Spill(string artifactId, Vector3 position)
        {
            if (string.IsNullOrEmpty(artifactId)) return null;
            var go = new GameObject("ArtifactPickup");
            go.transform.position = position;
            var pickup = go.AddComponent<ArtifactPickup>();
            pickup.ArtifactId = artifactId;
            pickup.BuildBody();
            if (!s_loose.Contains(pickup)) s_loose.Add(pickup);
            return pickup;
        }

        /// <summary>The nearest pickup nobody carries within <paramref name="reach"/>, or null. A probe:
        /// no pickup nearby is the ordinary case.</summary>
        public static ArtifactPickup FindNear(Vector3 position, float reach)
        {
            ArtifactPickup best = null;
            float bestSqr = reach * reach;
            for (int i = 0; i < s_loose.Count; i++)
            {
                var pickup = s_loose[i];
                if (!pickup || pickup._holder) continue;
                float d = (pickup.transform.position - position).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = pickup; }
            }
            return best;
        }

        /// <summary>Strike every pickup (the instance ended). Returns how many were lost.</summary>
        public static int StrikeAll()
        {
            int n = s_loose.Count;
            for (int i = s_loose.Count - 1; i >= 0; i--)
            {
                var pickup = s_loose[i];
                if (pickup) pickup.Strike();
            }
            s_loose.Clear();
            return n;
        }

        void OnEnable()
        {
            if (!s_loose.Contains(this)) s_loose.Add(this);
        }

        void OnDisable() => s_loose.Remove(this);

        /// <summary>Decide a claim: true when <paramref name="carrier"/> now holds this.</summary>
        internal bool TryClaim(PayloadCarrier carrier)
        {
            if (!IsAuthority || !carrier || _holder) return false;
            _holder = carrier;
            return true;
        }

        /// <summary>Let go where it is: it floats again for anyone.</summary>
        internal void Drop() => _holder = null;

        /// <summary>Remove it from the world (stowed in a hold, or lost).</summary>
        internal void Strike()
        {
            _holder = null;
            s_loose.Remove(this);
            if (!this) return;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>True on the machine that decides claims: the same one that decides the hold's grabs.</summary>
        public static bool IsAuthority => true;

        void Update()
        {
            if (_holder)
            {
                var hull = _holder.transform;
                transform.SetPositionAndRotation(hull.TransformPoint(ArkHold.Config.CarryOffset), hull.rotation);
            }
            if (_body) _body.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        void BuildBody()
        {
            var cfg = ArkHold.Config;
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.transform.localScale = Vector3.one * cfg.PickupSize;
            body.AddComponent<MeshFilter>().sharedMesh = Octahedron();
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cfg.PickupMaterial;
            _body = body.transform;
        }

        // Flat-shaded unit octahedron: every face carries its own three vertices and normal, so the
        // body reads as cut facets under light, never as a smooth or flat blob.
        static Mesh Octahedron()
        {
            if (s_mesh) return s_mesh;
            Vector3[] tips =
            {
                Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            };
            int[,] faces =
            {
                { 0, 4, 2 }, { 0, 2, 5 }, { 0, 5, 3 }, { 0, 3, 4 },
                { 1, 2, 4 }, { 1, 5, 2 }, { 1, 3, 5 }, { 1, 4, 3 },
            };
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var triangles = new int[24];
            for (int f = 0; f < 8; f++)
            {
                var a = tips[faces[f, 0]];
                var b = tips[faces[f, 1]];
                var c = tips[faces[f, 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                for (int k = 0; k < 3; k++)
                {
                    int v = f * 3 + k;
                    vertices[v] = tips[faces[f, k]];
                    normals[v] = n;
                    triangles[v] = v;
                }
            }
            s_mesh = new Mesh { name = "ArtifactPickupOctahedron", vertices = vertices, normals = normals, triangles = triangles };
            s_mesh.RecalculateBounds();
            return s_mesh;
        }

        /// <summary>The outward winding, for tests: every face's normal points away from the centre.</summary>
        internal static bool FacesPointOutward()
        {
            var mesh = Octahedron();
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i += 3)
            {
                var centre = (v[i] + v[i + 1] + v[i + 2]) / 3f;
                if (Vector3.Dot(centre, n[i]) <= 0f) return false;
            }
            return true;
        }
    }
}

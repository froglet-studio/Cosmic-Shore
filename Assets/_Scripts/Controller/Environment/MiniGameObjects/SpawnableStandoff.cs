using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Posts" - Standoff's arena, the Grizzly's CHARGE game (<c>Assets/_Scripts/Controller/Arcade/STANDOFF.md</c>).
    /// Square POSTS of prism tiles stand on a sphere round an open centre, one per point of a Fibonacci sphere (no up),
    /// each facing the centre like a parapet. Higher rungs add an inner ring of smaller posts, a SHIELDED rim on every
    /// post and DANGER corners.
    ///
    ///   • <b>Cover.</b> A Grizzly digs in behind a post (Dig In: the hull stops, the gun still turns, Energy comes back
    ///     faster) and lobs over or round it. A trigger bomb flies THROUGH prisms (it breaks none), so a post hides you
    ///     and your bomb's approach; it does not stop the bomb.
    ///   • <b>It wears away.</b> Every tile is neutral (<see cref="Domains.Blue"/>), so every blast breaks the tiles it
    ///     reaches: a post that drew fire all match is a gap by the end. A shielded rim takes two hits.
    ///   • <b>Hazards.</b> <see cref="dangerCorners"/> turns each post's four corner tiles to danger, so hugging the edge
    ///     of cover costs petals.
    ///
    /// <para><b>Mirrored exactly by <c>Tools/Build/standoff_budget.py</c></b>, which the generator imports for the
    /// census, the volume and the PhaseThresholds. There are no draws at all: every post's place and every tile's place
    /// is closed form, so the census is exact.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): a post's tiles are coplanar on a
    /// <see cref="TilePitch"/> grid one unit wider than a tile, and posts stand far enough apart that no two tiles of
    /// different posts come within a tile's diagonal (the mirror asserts it).</para>
    ///
    /// <para><b>No intensity read.</b> The layout reads only the serialized fields below, one prefab per intensity, so
    /// each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by <c>Tools/Build/author_standoff_assets.py</c>.</para>
    /// </summary>
    public class SpawnableStandoff : CellEnvironmentSpawnableBase
    {
        [Header("The outer posts")]
        [Tooltip("Posts on the outer sphere, one per point of a Fibonacci sphere.")]
        [SerializeField, Min(1)] int postCount = 12;
        [Tooltip("Radius (u) of the outer sphere the posts stand on, round the arena centre.")]
        [SerializeField, Min(60f)] float postRadius = 280f;
        [Tooltip("Tiles along each side of an outer post (a post is postGrid x postGrid tiles).")]
        [SerializeField, Min(2)] int postGrid = 6;

        [Header("The inner posts")]
        [Tooltip("Posts on the inner sphere. 0 = none.")]
        [SerializeField, Min(0)] int innerPostCount = 0;
        [Tooltip("Radius (u) of the inner sphere.")]
        [SerializeField, Min(40f)] float innerPostRadius = 130f;
        [Tooltip("Tiles along each side of an inner post.")]
        [SerializeField, Min(2)] int innerPostGrid = 4;

        [Header("Hardening")]
        [Tooltip("Every post's border tiles are SHIELDED (they take two hits).")]
        [SerializeField] bool shieldedRim = false;
        [Tooltip("Every post's four corner tiles are DANGER (they cost petals to brush).")]
        [SerializeField] bool dangerCorners = false;

        // ── A tile ──
        /// <summary>A tile's width (u, square), its thickness along the post's normal, and the grid pitch (u). The pitch
        /// is one unit wider than a tile, so neighbours never touch.</summary>
        public const float TileWidth = 8f;
        public const float TileThickness = 3f;
        public const float TilePitch = 9f;

        /// <summary>The inner sphere's Fibonacci points are turned this far (degrees) about <see cref="InnerTwistAxis"/>,
        /// so an inner post never sits square in front of an outer one.</summary>
        public const float InnerTwistDegrees = 23f;
        static readonly Vector3 InnerTwistAxis = new Vector3(0.6f, 0.48f, 0.64f);

        protected override int DefaultSeed => 12525;
        protected override int LayCapacity => 1536;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(postCount, postRadius, postGrid, innerPostCount, innerPostRadius, innerPostGrid,
                                    shieldedRim, dangerCorners);

        // ── The geometry the AI reads (this structure's local space) ──
        public int PostCount => postCount + innerPostCount;
        public float OuterRadius => postRadius;

        /// <summary>The unit direction of post <paramref name="post"/> (outer posts first, then inner).</summary>
        public Vector3 PostDirection(int post) =>
            post < postCount
                ? Fibonacci(post, postCount)
                : Quaternion.AngleAxis(InnerTwistDegrees, InnerTwistAxis.normalized) *
                  Fibonacci(post - postCount, innerPostCount);

        /// <summary>The radius (u) post <paramref name="post"/> stands at.</summary>
        public float PostRadiusOf(int post) => post < postCount ? postRadius : innerPostRadius;

        /// <summary>Half the width (u) of post <paramref name="post"/>, edge to centre.</summary>
        public float PostHalfWidth(int post) =>
            0.5f * ((post < postCount ? postGrid : innerPostGrid) - 1) * TilePitch + 0.5f * TileWidth;

        protected override void BuildEnvironment()
        {
            for (int p = 0; p < postCount + innerPostCount; p++)
                LayPost(PostDirection(p), PostRadiusOf(p), p < postCount ? postGrid : innerPostGrid);
        }

        /// <summary>Point <paramref name="k"/> of <paramref name="n"/> on the unit Fibonacci sphere.</summary>
        static Vector3 Fibonacci(int k, int n)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = GoldenAngle * k;
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
        }

        void LayPost(Vector3 d, float radius, int grid)
        {
            var u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(d, u);
            var rot = Quaternion.LookRotation(d, v);   // the tile's thin axis (z) along the post's normal
            var size = new Vector3(TileWidth, TileWidth, TileThickness);
            var centre = d * radius;
            float half = 0.5f * (grid - 1);
            int last = grid - 1;
            for (int a = 0; a < grid; a++)
            for (int b = 0; b < grid; b++)
            {
                bool edgeA = a == 0 || a == last, edgeB = b == 0 || b == last;
                var kind = dangerCorners && edgeA && edgeB ? PrismKind.Danger
                         : shieldedRim && (edgeA || edgeB) ? PrismKind.Shielded
                         : PrismKind.Plain;
                Emit(centre + u * ((a - half) * TilePitch) + v * ((b - half) * TilePitch), rot, size, Domains.Blue, kind);
            }
        }
    }
}

using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Drum" - Drumfire's target, and the whole of that cell's environment: a great porous
    /// ball of prism panes hung at the cell centre for pilots to carve. Built out of the Orrery's
    /// sun-shell vocabulary (a phyllotaxis point set on each sphere, panes laid tangent to the
    /// surface, noise gaps punched through) but scaled up from a 46u ornament to a ~320u arena
    /// feature and stacked into concentric shells, so a shot fired ACROSS the ball passes through
    /// several skins and a shot fired at its middle punches one small hole. That difference is
    /// the mode's aiming lesson, and it is geometry rather than a rule.
    ///
    /// <para><b>Every scoring pane is <see cref="Domains.Blue"/>, deliberately.</b>
    /// <c>StatsManager.IsFriendlyEnvironmentPrism</c> counts a prism as friendly only when it
    /// wears the attacker's own colour, so a Blue drum is hostile to every domain and each pilot
    /// is shooting at exactly the same target. Painting it in the three playable colours would
    /// have made a third of the ball worthless to whichever team drew that colour, decided by a
    /// spawn slot nobody picked.</para>
    ///
    /// <para><b>What is NOT plain skin:</b> shielded meridian RIBS (two passes to break, and
    /// worth more volume - the reason to aim at structure), a super-shielded CORE that no blast
    /// can touch so the drum always leaves a landmark and can never be scored to nothing, and a
    /// scatter of danger STUDS on the outer skin only, which is what makes flying in close to
    /// graze the surface for jaw energy a real risk rather than a free upgrade.</para>
    ///
    /// <para>Collider budget: the plain and danger panes ride the LOD-cullable BoxCollider, so
    /// their active count is bounded by <c>PrismColliderLodManager</c> rather than by population,
    /// exactly like the freestyle cell environments. Only the shielded ribs and the core carry
    /// always-on convex MeshColliders - see <c>Tools/Build/drumfire_arena.py</c>, which counts
    /// them and fails if the always-on total leaves the shipped band.</para>
    /// </summary>
    public class SpawnableDrum : CellEnvironmentSpawnableBase
    {
        protected override int DefaultSeed => 45;

        [Header("Drum")]
        [Tooltip("Radius of the OUTERMOST shell, in world units. The lane offset authored on the " +
                 "scene's crystal manager must stay comfortably outside this or a pilot flying " +
                 "their line will clip the ball.")]
        [SerializeField, Min(1f)] float outerRadius = 320f;

        [Tooltip("Concentric skins, evenly spaced from the outer radius down toward the core. " +
                 "Each is a phyllotaxis point set; point counts fall as r^2 so every shell is " +
                 "covered to the same fraction and the panes stay one size throughout.")]
        [SerializeField, Min(1)] int shellCount = 5;

        [Tooltip("Points on the OUTER shell before the noise gaps are punched. Inner shells scale " +
                 "by (r/outerRadius)^2.")]
        [SerializeField, Min(1)] int outerShellPoints = 14074;

        [Tooltip("Value-noise gaps: a point whose noise sample falls below this is skipped, so " +
                 "the skin reads as a lattice you can see and shoot through rather than a solid " +
                 "ball. 0 = no gaps.")]
        [SerializeField, Range(0f, 0.9f)] float gapThreshold = 0.25f;

        [Tooltip("Spatial frequency of the gap noise, in 1/units. Lower = bigger, blobbier holes.")]
        [SerializeField, Min(0.0001f)] float gapNoiseFrequency = 0.012f;

        [Tooltip("One pane: X/Y span the shell surface, Z is its thickness along the normal.")]
        [SerializeField] Vector3 paneSize = new(8f, 8f, 0.7f);

        [Header("Ribs (shielded)")]
        [Tooltip("Meridian bands of SHIELDED panes that brace the outer shell. Tougher (a hit " +
                 "sheds the shield instead of destroying the prism) and heavier, so they are the " +
                 "structure worth aiming at. Always-on mesh colliders - keep the total small.")]
        [SerializeField, Min(0)] int ribCount = 3;

        [SerializeField, Min(0)] int panesPerRib = 72;

        [SerializeField] Vector3 ribPaneSize = new(14f, 5f, 2.4f);

        [Header("Core (super-shielded)")]
        [Tooltip("A small invulnerable cage at the very centre. It can never be destroyed, so the " +
                 "drum always leaves a marker and the arena never becomes an empty sphere.")]
        [SerializeField, Min(0)] int corePanes = 24;

        [SerializeField, Min(0f)] float coreRadius = 34f;

        [SerializeField] Vector3 corePaneSize = new(9f, 9f, 3f);

        [Header("Studs (danger)")]
        [Tooltip("Danger prisms studded across the OUTER skin only. Ramming one costs a Dolphin " +
                 "half its banked energy and parks it for three seconds, which is the price of " +
                 "flying close enough to skim the drum for a wider jaw.")]
        [SerializeField, Min(0)] int dangerStuds = 120;

        [SerializeField] Vector3 studSize = new(7f, 7f, 5f);

        protected override int LayCapacity => 34000;

        protected override int BuildParameterHash() => System.HashCode.Combine(
            System.HashCode.Combine(nameof(SpawnableDrum), 1, outerRadius, shellCount,
                outerShellPoints, gapThreshold, gapNoiseFrequency, paneSize),
            System.HashCode.Combine(ribCount, panesPerRib, ribPaneSize, corePanes, coreRadius,
                corePaneSize, dangerStuds, studSize));

        protected override void BuildEnvironment()
        {
            BuildShells();
            BuildRibs();
            BuildCore();
            BuildStuds();
        }

        // ── Shared point/pose vocabulary (the Orrery's, verbatim) ────────────

        /// <summary>Phyllotaxis point on the unit sphere.</summary>
        static Vector3 SpherePoint(int i, int n)
        {
            float u = (i + 0.5f) / n;
            float y = 1f - 2f * u;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        /// <summary>Pane tangent to a sphere at direction d (thin axis along the normal).</summary>
        static Quaternion ShellRot(Vector3 d, int i)
        {
            float a = i * GoldenAngle;
            return SpawnPoint.LookRotation(d, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
        }

        /// <summary>The gap test - shared by the skin and the studs so a stud can never land in a hole.</summary>
        bool IsGap(Vector3 p, int shell) =>
            gapThreshold > 0f &&
            N01(p.x * gapNoiseFrequency, p.y * gapNoiseFrequency, p.z * gapNoiseFrequency, shell) < gapThreshold;

        // ── The body ────────────────────────────────────────────────────────

        void BuildShells()
        {
            for (int s = 0; s < shellCount; s++)
            {
                // Evenly spaced skins from the outer radius inward. The innermost sits at
                // outerRadius/shellCount rather than at 0, so the ball has a hollow middle for
                // the core cage and a shot across it always crosses skins rather than mush.
                float frac = (shellCount - s) / (float)shellCount;
                float r = outerRadius * frac;

                // Constant coverage: pane area is fixed, so the point count has to fall with r^2
                // or the inner shells would be solid while the outer one is lace.
                int n = Scaled(Mathf.Max(1, Mathf.FloorToInt(outerShellPoints * frac * frac + 0.5f)));

                for (int i = 0; i < n; i++)
                {
                    var d = SpherePoint(i, n);
                    var p = d * r;
                    if (IsGap(p, s)) continue;
                    Emit(p, ShellRot(d, i), Jit(paneSize), Domains.Blue);
                }
            }
        }

        void BuildRibs()
        {
            if (ribCount <= 0 || panesPerRib <= 0) return;

            // Great circles through the poles, evenly rotated about Y. Laid slightly proud of the
            // outer skin so the shield octahedra (which reach 1.5x the pane's own half-extent -
            // see Docs/ECOSYSTEM.md 35) stand clear of the panes they brace instead of fusing
            // into them.
            float ribRadius = outerRadius + ribPaneSize.z * 1.5f;

            for (int rIdx = 0; rIdx < ribCount; rIdx++)
            {
                float lon = Mathf.PI * rIdx / ribCount;
                var axis = new Vector3(Mathf.Cos(lon), 0f, Mathf.Sin(lon));

                for (int i = 0; i < panesPerRib; i++)
                {
                    float t = 2f * Mathf.PI * i / panesPerRib;
                    var d = (axis * Mathf.Cos(t) + Vector3.up * Mathf.Sin(t)).normalized;
                    var p = d * ribRadius;
                    // The rib's long axis runs ALONG the band, so the brace reads as a hoop.
                    var along = (Vector3.up * Mathf.Cos(t) - axis * Mathf.Sin(t)).normalized;
                    Emit(p, SpawnPoint.LookRotation(d, along), ribPaneSize, Domains.Blue,
                        PrismKind.Shielded);
                }
            }
        }

        void BuildCore()
        {
            for (int i = 0; i < corePanes; i++)
            {
                var d = SpherePoint(i, Mathf.Max(1, corePanes));
                Emit(d * coreRadius, ShellRot(d, i), corePaneSize, Domains.Blue,
                    PrismKind.SuperShielded);
            }
        }

        void BuildStuds()
        {
            if (dangerStuds <= 0) return;

            // Offset the phyllotaxis index so the studs do not simply re-walk the outer shell's
            // own point order and land on top of its first N panes.
            const int studIndexOffset = 7919;

            for (int i = 0; i < dangerStuds; i++)
            {
                var d = SpherePoint(i + studIndexOffset, dangerStuds + studIndexOffset);
                var p = d * (outerRadius + studSize.z * 0.5f);
                if (IsGap(d * outerRadius, 0)) continue;   // never stud a hole
                Emit(p, ShellRot(d, i), studSize, Domains.Blue, PrismKind.Danger);
            }
        }
    }
}

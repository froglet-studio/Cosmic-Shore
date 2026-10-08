using System;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Black Hole cell</b>'s environment: ONE black hole at the cell's centre, and nothing
    /// else — no prisms, no structure (Docs/BLACK_HOLE.md §11). It is a <see cref="SpawnableBase"/>
    /// only so a <see cref="CellConfigDataSO"/> can name it as its <c>EnvironmentPrefab</c>, which is
    /// how the Cell Selector offers a world and how <see cref="Cell"/> builds and retires one.
    ///
    /// <para><b>Spawn</b> lays nothing. It spawns the hole through <see cref="BlackHoleRegistry.Spawn"/>
    /// (so the hole budget and the config's sanity gate apply exactly as they do to the console's
    /// spawn) and parents it under the container the Cell adopts, so the hole sits at the cell's
    /// centre and lives and dies with the world. Its own arrival is continuous: the hole eases its
    /// warp and lens in over <c>BlackHoleConfig.warpEaseSeconds</c>.</para>
    ///
    /// <para><b>The generated points are the scale model, not mass.</b> The Cell Selector builds each
    /// mini-cell from a generator's point data (<see cref="CellMiniatureBuilder"/>); a world with no
    /// laid mass would show an empty slot. So <see cref="SpawnableBase.GenerateTrailData"/> emits the
    /// hole's SHADOW — a ball of plates on the sphere of radius 3√3/2 · r_s, the silhouette the lens
    /// draws — and <see cref="Spawn(int)"/> never lays them.</para>
    ///
    /// <para><b>Retirement.</b> When the cell swaps away, it re-parents the environment under its
    /// suction root. <see cref="BlackHoleCellAnchor"/> sees that re-parent and begins the hole's
    /// despawn, so the hole lets go of the world it is leaving at once and its shadow eases out with
    /// the suction instead of being scaled away under a lens still tracing the full horizon.</para>
    /// </summary>
    public sealed class SpawnableBlackHole : SpawnableBase
    {
        /// <summary>The Schwarzschild shadow radius in horizon radii: the photon sphere's capture
        /// impact parameter, b = 3√3/2 · r_s.</summary>
        const float ShadowPerHorizon = 2.598076f;

        [Header("Black hole")]
        [SerializeField, Min(0f), Tooltip("The hole's PULL (BlackHole.Strength). GM and the influence radius " +
                                          "scale from it through BlackHoleConfig; with Horizon Radius at 0 so " +
                                          "does the size. 10 is the console's modest hole; 50 swallows a cell's " +
                                          "worth of mass.")]
        float strength = 10f;

        [SerializeField, Min(0f), Tooltip("The hole's SIZE: its event-horizon radius r_s in world units " +
                                          "(BlackHole.Size). 0 = derived from Strength " +
                                          "(BlackHoleConfig.horizonPerStrength).")]
        float horizonRadius = 0f;

        [SerializeField, Tooltip("Axis the hole spins about. Its spacetime is dragged around this axis " +
                                 "(Lense-Thirring, strength set by BlackHoleConfig.spin), so infalling " +
                                 "mass winds up about it near the horizon.")]
        Vector3 spinAxis = Vector3.up;

        [Header("Scale model")]
        [SerializeField, Range(16, 63), Tooltip("Plates in the Cell Selector's scale model of the hole's " +
                                                "shadow. Kept under 64 so CellMiniatureBuilder's signature " +
                                                "filter keeps all of them - a ball of 64+ evenly spread " +
                                                "plates has no densest voxels to keep, and the filter would " +
                                                "bite holes in it.")]
        int modelPlates = 48;

        protected override int GetParameterHash() =>
            HashCode.Combine(strength, horizonRadius, modelPlates);

        /// <summary>
        /// The hole's shadow as a ball of plates, tangent to the sphere and facing out — for the scale
        /// model only (see the class summary). Fibonacci-spread, so it is deterministic and even.
        /// </summary>
        protected override SpawnTrailData[] GenerateTrailData()
        {
            float rs = BlackHoleRegistry.Config.HorizonRadius(strength, horizonRadius);
            float shadow = rs * ShadowPerHorizon;

            int n = Mathf.Max(1, modelPlates);
            // Plates sized to the mean spacing of n points on the sphere, a little under so they
            // read as tiles rather than a welded shell.
            float plate = 0.8f * shadow * Mathf.Sqrt(4f * Mathf.PI / n);
            var scale = new Vector3(plate, plate, plate * 0.15f);

            const float GoldenAngle = 2.39996323f;
            var points = new SpawnPoint[n];
            for (int i = 0; i < n; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / n;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float phi = i * GoldenAngle;
                var normal = new Vector3(ring * Mathf.Cos(phi), y, ring * Mathf.Sin(phi));
                var up = Mathf.Abs(normal.y) > 0.99f ? Vector3.forward : Vector3.up;
                points[i] = new SpawnPoint(normal * shadow, SpawnPoint.LookRotation(normal, up), scale);
            }

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        /// <summary>
        /// A container holding the one hole — never the scale-model points. Edit mode spawns an
        /// empty container: the registry's driver is a play-mode object, and an edit-mode build of
        /// this world (a baseline measure, a scene setup) has no mass to account for either way.
        /// </summary>
        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            trails.Clear();

            var container = new GameObject(name);
            if (!Application.isPlaying) return container;

            var hole = BlackHoleRegistry.Spawn(Vector3.zero, strength, Vector3.zero, spinAxis, horizonRadius);
            if (!hole)
            {
                // The registry already warned with the reason (hole budget, insane config).
                return container;
            }

            // worldPositionStays:false - the hole sits at the container's origin, which the Cell
            // places at its own centre when it adopts the container.
            hole.transform.SetParent(container.transform, false);
            container.AddComponent<BlackHoleCellAnchor>().Bind(hole);
            return container;
        }
    }
}

using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Black Hole cell</b>'s environment (Docs/BLACK_HOLE.md §11–§12): a black hole at the
    /// cell's centre and — as a <b>dipole</b> — its antisymmetric twin, a white hole, at
    /// <see cref="sourceOffset"/>, joined by a wormhole. Nothing else: no prisms, no structure. It
    /// is a <see cref="SpawnableBase"/> only so a <see cref="CellConfigDataSO"/> can name it as its
    /// <c>EnvironmentPrefab</c>, which is how the Cell Selector offers a world and how
    /// <see cref="Cell"/> builds and retires one.
    ///
    /// <para><b>Spawn</b> lays nothing. The holes come from <see cref="BlackHoleRegistry.Spawn"/> (so
    /// the hole budget and the config's sanity gate apply as they do to the console's spawn), parented
    /// under the container the Cell adopts, so the sink sits at the cell's centre and both live and die
    /// with the world. Their arrival is continuous: each eases its warp and lens in.</para>
    ///
    /// <para><b>The dipole.</b> The SINK pulls and the SOURCE pushes with the same strength; the sink's
    /// <see cref="BlackHole.Throat"/> is the source, so a prism the sink captures is carried through to
    /// the source instead of destroyed. A Butterfly-fold <see cref="WormholeMouth"/> pair is seated in
    /// the two centres, each the size of the sink's shadow: the sink's mouth REPLACES its black disc
    /// (the lens leaves a pixel alone wherever the scene in front of the hole's centre — the mouth's
    /// face — is drawn), so instead of black the player sees out of the source, and flying in carries
    /// them there. Each pole is also a pole of the cell's warp field (<see cref="WarpFieldRuntime.AddPole"/>):
    /// a pilot shrinks toward either one, so they fall into a hole that grows and climb out of a white
    /// hole that shrinks behind them.</para>
    ///
    /// <para><b>The generated points are the scale model, not mass.</b> The Cell Selector builds each
    /// mini-cell from a generator's point data (<see cref="CellMiniatureBuilder"/>); a world with no
    /// laid mass would show an empty slot. So <see cref="SpawnableBase.GenerateTrailData"/> emits each
    /// hole's shadow as a ball of plates — exaggerated for a dipole, where the true shadows are specks
    /// hundreds of their own radii apart — and <see cref="Spawn(int)"/> never lays them.</para>
    ///
    /// <para><b>Retirement.</b> <see cref="BlackHoleCellAnchor"/> sees the cell re-parent the
    /// environment into its suction root and despawns the holes and withers the mouths, so the world
    /// lets go at once and nothing pops.</para>
    /// </summary>
    public sealed class SpawnableBlackHole : SpawnableBase
    {
        /// <summary>The Schwarzschild shadow radius in horizon radii: the photon sphere's capture
        /// impact parameter, b = 3√3/2 · r_s.</summary>
        public const float ShadowPerHorizon = 2.598076f;

        [Header("Black hole")]
        [SerializeField, Min(0f), Tooltip("The hole's PULL (BlackHole.Strength). GM and the influence radius " +
                                          "scale from it through BlackHoleConfig; with Horizon Radius at 0 so " +
                                          "does the size. 10 is the console's modest hole; 50 swallows a cell's " +
                                          "worth of mass. A dipole's source pushes with the same strength.")]
        float strength = 10f;

        [SerializeField, Min(0f), Tooltip("The hole's SIZE: its event-horizon radius r_s in world units " +
                                          "(BlackHole.Size). 0 = derived from Strength " +
                                          "(BlackHoleConfig.horizonPerStrength).")]
        float horizonRadius = 0f;

        [SerializeField, Tooltip("Axis the hole spins about. Its spacetime is dragged around this axis " +
                                 "(Lense-Thirring, strength set by BlackHoleConfig.spin), so infalling " +
                                 "mass winds up about it near the horizon. A dipole's source is dragged " +
                                 "the other way about the same axis.")]
        Vector3 spinAxis = Vector3.up;

        [Header("Dipole (Docs/BLACK_HOLE.md §12)")]
        [SerializeField, Tooltip("Pair the black hole with a WHITE HOLE — same strength, repelling — joined " +
                                 "by a wormhole: what the sink captures comes out of the source.")]
        bool dipole;

        [SerializeField, Tooltip("Where the source sits, relative to the sink (the cell's centre). Keep it " +
                                 "clear of the toy ring (horizontal, ~0.82 of the membrane radius) and of " +
                                 "both warp-field poles' reach of each other.")]
        Vector3 sourceOffset = new(0f, 500f, 0f);

        [Header("Wormhole (the Butterfly fold's mouths)")]
        [SerializeField, Tooltip("Seat a WormholeMouth pair in the two centres, each the size of the sink's " +
                                 "shadow: the sink's mouth replaces its black disc, and flying into it " +
                                 "carries a pilot to the source. Dipole only.")]
        bool seatWormhole = true;

        [SerializeField, Tooltip("The mouths' surface (CosmicShore/Wormhole) — the fold's own Wormhole.mat.")]
        Material wormholeMaterial;

        [SerializeField, Tooltip("Whose vessels a mouth may carry: the session's runtime GameData (its " +
                                 "Players list). The fold reads the same list.")]
        GameDataSO gameData;

        [SerializeField, Min(1f), Tooltip("Mouth radius in multiples of the sink's shadow (3√3/2 r_s). Above 1 " +
                                          "the portal covers the shadow AND the inner rings, so as you fall in " +
                                          "the portal's own view is what fills the screen — not lensing soup — " +
                                          "and you are through about a second after it does.")]
        float mouthToShadow = 2.5f;

        [SerializeField, Min(0.01f), Tooltip("Seconds a mouth takes to bloom in.")]
        float mouthBloomSeconds = 0.45f;

        [SerializeField, Min(0f), Tooltip("Furthest the player's camera may be for a mouth's exact view " +
                                          "(the fold's portalWindowRange).")]
        float mouthExactRange = 2500f;

        [SerializeField, Min(1f), Tooltip("Distance past the exact range over which the view fades to the " +
                                          "panorama (the fold's portalWindowFadeBand).")]
        float mouthExactFadeBand = 600f;

        [SerializeField, Range(0.1f, 1f), Tooltip("Exact view resolution as a fraction of the gameplay " +
                                                  "camera's (the fold's portalWindowRenderScale).")]
        float mouthExactRenderScale = 0.75f;

        [SerializeField, Range(32, 1024), Tooltip("Texels per side of each panorama face.")]
        int mouthPanoramaFaceSize = 256;

        [Header("Felt pull on vessels (BlackHole.vesselFelt*, Docs/BLACK_HOLE.md §12)")]
        [SerializeField, Min(0f), Tooltip("k: how hard both poles pull/push VESSELS, measured in each hull's own " +
                                          "cruise speed and the throat — felt acceleration = k · cruise² · R_throat " +
                                          "· s / r². 0 = the physical pull (a hole this small is barely felt).")]
        float vesselFeltStrength = 3f;

        [SerializeField, Min(0f), Tooltip("The felt pull's ceiling, × cruise. 1.3: the white hole holds off any " +
                                          "hull at cruise (boost through), the black hole carries one in at 2.3× cruise.")]
        float vesselFeltCap = 1.3f;

        [SerializeField, Min(1f), Tooltip("How far each pole's felt pull reaches, in throat radii.")]
        float vesselFeltReach = 12f;

        [Header("Scale model")]
        [SerializeField, Range(16, 63), Tooltip("Plates in the Cell Selector's scale model (split between " +
                                                "the two holes of a dipole). Kept under 64 so " +
                                                "CellMiniatureBuilder's signature filter keeps all of them.")]
        int modelPlates = 48;

        [SerializeField, Range(0.02f, 0.4f), Tooltip("A dipole's model draws each hole this fraction of the " +
                                                     "pole separation across - NOT to scale: the true " +
                                                     "shadows would be invisible specks at thumbnail size.")]
        float modelDipoleBallFraction = 0.12f;

        public bool IsDipole => dipole;
        public float MouthToShadow => seatWormhole ? mouthToShadow : 1f;
        public float VesselFeltStrength => vesselFeltStrength;
        public float VesselFeltCap => vesselFeltCap;
        public float VesselFeltReach => vesselFeltReach;
        public Vector3 SourceOffset => sourceOffset;
        public float Strength => strength;

        protected override int GetParameterHash() =>
            HashCode.Combine(strength, horizonRadius, modelPlates, dipole, sourceOffset, modelDipoleBallFraction);

        /// <summary>
        /// Each hole's shadow as a ball of plates, tangent to the sphere and facing out — for the scale
        /// model only (see the class summary). Fibonacci-spread, so it is deterministic and even.
        /// </summary>
        protected override SpawnTrailData[] GenerateTrailData()
        {
            float rs = BlackHoleRegistry.Config.HorizonRadius(strength, horizonRadius);
            float shadow = rs * ShadowPerHorizon;
            int n = Mathf.Max(1, modelPlates);

            if (!dipole)
                return new[] { new SpawnTrailData(Ball(Vector3.zero, shadow, n), false, domain) };

            float ball = Mathf.Max(shadow, sourceOffset.magnitude * modelDipoleBallFraction);
            int half = Mathf.Max(1, n / 2);
            return new[]
            {
                new SpawnTrailData(Ball(Vector3.zero, ball, half), false, domain),
                new SpawnTrailData(Ball(sourceOffset, ball, half), false, domain),
            };
        }

        static SpawnPoint[] Ball(Vector3 centre, float radius, int n)
        {
            // Plates sized to the mean spacing of n points on the sphere, a little under so they
            // read as tiles rather than a welded shell.
            float plate = 0.8f * radius * Mathf.Sqrt(4f * Mathf.PI / n);
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
                points[i] = new SpawnPoint(centre + normal * radius, SpawnPoint.LookRotation(normal, up), scale);
            }
            return points;
        }

        /// <summary>
        /// A container holding the hole(s) and the mouths — never the scale-model points. Edit mode
        /// spawns an empty container: the registry's driver is a play-mode object, and an edit-mode
        /// build of this world (a baseline measure, a scene setup) has no mass to account for either way.
        /// </summary>
        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            trails.Clear();

            var container = new GameObject(name);
            if (!Application.isPlaying) return container;

            var sink = BlackHoleRegistry.Spawn(Vector3.zero, strength, Vector3.zero, spinAxis, horizonRadius);
            if (!sink)
            {
                // The registry already warned with the reason (hole budget, insane config).
                return container;
            }

            // worldPositionStays:false - the holes sit at the container's origin and offset, which the
            // Cell places at its own centre when it adopts the container.
            sink.transform.SetParent(container.transform, false);
            var anchor = container.AddComponent<BlackHoleCellAnchor>();
            anchor.Bind(sink);

            if (!dipole) return container;

            var source = BlackHoleRegistry.Spawn(sourceOffset, strength, Vector3.zero, spinAxis, horizonRadius,
                HolePolarity.Source);
            if (!source)
            {
                CSDebug.LogWarning("[BlackHole] The dipole's source could not be spawned - the black hole stands alone.");
                return container;
            }
            source.transform.SetParent(container.transform, false);
            source.transform.localPosition = sourceOffset;
            sink.Throat = source;
            source.Throat = sink;
            anchor.BindSource(source);

            if (seatWormhole) SeatWormhole(container.transform, sink, source, anchor);

            // After the mouths: the felt law is measured from the throat they set.
            sink.ConfigureFeltVesselLaw(vesselFeltStrength, vesselFeltCap, vesselFeltReach);
            source.ConfigureFeltVesselLaw(vesselFeltStrength, vesselFeltCap, vesselFeltReach);
            return container;
        }

        void SeatWormhole(Transform container, BlackHole sink, BlackHole source, BlackHoleCellAnchor anchor)
        {
            if (!wormholeMaterial)
                CSDebug.LogWarning("[BlackHole] The dipole's wormhole has no material - its mouths will carry " +
                                   "pilots but draw nothing, and the sink keeps its black shadow.");
            IReadOnlyList<IPlayer> players = gameData ? gameData.Players : null;
            if (players == null)
                CSDebug.LogWarning("[BlackHole] The dipole's wormhole has no GameData - its mouths will carry no one.");

            // Each mouth is mouthToShadow × the sink's SHADOW: it covers the disc the lens would have
            // painted black and the strongest rings around it (Docs/BLACK_HOLE.md §12).
            float radius = sink.HorizonRadius * ShadowPerHorizon * mouthToShadow;
            var sinkMouth = BuildMouth(container, sink.transform.localPosition, "Wormhole (sink)", players, radius,
                rim: default);                  // the material's own rim
            var sourceMouth = BuildMouth(container, source.transform.localPosition, "Wormhole (source)", players, radius,
                rim: Color.white);              // a white hole's rim is white
            WormholeMouth.Pair(sinkMouth, sourceMouth);

            sink.ThroatRadius = radius;
            source.ThroatRadius = radius;
            anchor.BindMouths(sinkMouth, sourceMouth);
        }

        WormholeMouth BuildMouth(Transform container, Vector3 localPosition, string mouthName,
            IReadOnlyList<IPlayer> players, float radius, Color rim)
        {
            var go = new GameObject(mouthName);
            go.transform.SetParent(container, false);
            go.transform.localPosition = localPosition;
            var mouth = go.AddComponent<WormholeMouth>();
            mouth.Build(new WormholeMouth.Settings
            {
                SurfaceMaterial = wormholeMaterial,
                BloomSeconds = mouthBloomSeconds,
                ExactRange = mouthExactRange,
                ExactFadeBand = mouthExactFadeBand,
                ExactRenderScale = mouthExactRenderScale,
                PanoramaFaceSize = mouthPanoramaFaceSize,
                RimTint = rim,
                // A natural throat belongs to nobody: no toll, no owner, every pilot rides.
                DomainTolled = false,
                Domain = CosmicShore.Data.Domains.Blue,
            }, players, radius);
            return mouth;
        }
    }
}

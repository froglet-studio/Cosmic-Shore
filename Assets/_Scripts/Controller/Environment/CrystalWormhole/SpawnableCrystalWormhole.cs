using System;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Crystal Wormhole cell</b>'s environment (Docs/CRYSTAL_WORMHOLE.md): one
    /// <see cref="CrystalWormhole"/> — an attractor at the cell's centre and a repulsor at
    /// <see cref="repulsorOffset"/>, joined by a seamless wormhole — and nothing else. It is a
    /// <see cref="SpawnableBase"/> only so a <see cref="CellConfigDataSO"/> can name it as its
    /// <c>EnvironmentPrefab</c>, which is how the Cell Selector offers a world and how <see cref="Cell"/>
    /// builds and retires one.
    ///
    /// <para><b>Spawn</b> lays nothing: it opens the wormhole on the container the Cell adopts, which
    /// FORMS out of nothing; when the cell retires the world, the pair ANNIHILATES inside the suction.
    /// This is a stand-in for the crystals that will open a wormhole in play — every number a crystal
    /// mechanic needs is the <see cref="CrystalWormhole.Settings"/> built here.</para>
    ///
    /// <para><b>The generated points are the scale model, not mass.</b> The Cell Selector builds each
    /// mini-cell from a generator's point data (<see cref="CellMiniatureBuilder"/>), so this emits a
    /// ball of plates for each pole — NOT to scale (the true throats would be specks at thumbnail
    /// size) — and <see cref="Spawn(int)"/> never lays them.</para>
    /// </summary>
    public sealed class SpawnableCrystalWormhole : SpawnableBase
    {
        [Header("The pair")]
        [SerializeField, Tooltip("Where the repulsor sits relative to the attractor (the cell's centre). " +
                                 "Keep both poles' reach clear of the toy ring (horizontal, ~0.82 of the " +
                                 "membrane) and of each other.")]
        Vector3 repulsorOffset = new(0f, 500f, 0f);

        [SerializeField, Min(0f), Tooltip("Each pole's gravity on PRISMS (BlackHoleConfig: GM = strength × " +
                                          "gmPerStrength), Plummer-softened by the throat.")]
        float strength = 15f;

        [SerializeField, Min(1f), Tooltip("The throat, world units: each mouth's radius, the wells' soft core " +
                                          "and the lens's width. Everything else is measured in it.")]
        float throatRadius = 26f;

        [SerializeField, Range(0f, 0.9f), Tooltip("Lens strength A: the attractor magnifies what lies behind it " +
                                                  "by up to 1/(1−A), the repulsor shrinks it by 1/(1+A). Under 1, " +
                                                  "so the warp never folds.")]
        float lensStrength = 0.6f;

        [SerializeField, Tooltip("Axis the pair is laid along by default (the wells drag no frame).")]
        Vector3 spinAxis = Vector3.up;

        [Header("Felt pull on vessels (Docs/CRYSTAL_WORMHOLE.md §3)")]
        [SerializeField, Min(0f), Tooltip("k: how hard both poles pull/push VESSELS, measured in each hull's own " +
                                          "cruise speed and the throat.")]
        float vesselFeltStrength = 4f;

        [SerializeField, Min(0f), Tooltip("The felt pull's ceiling, × cruise. 1.3: the repulsor holds off any hull " +
                                          "at cruise (boost through); the attractor carries one in at 2.3× cruise.")]
        float vesselFeltCap = 1.3f;

        [SerializeField, Min(1f), Tooltip("How far each pole's felt pull reaches, in throat radii.")]
        float vesselFeltReach = 12f;

        [Header("Life (Docs/CRYSTAL_WORMHOLE.md §5)")]
        [SerializeField, Min(0f), Tooltip("Seconds the pair takes to form out of nothing.")]
        float formSeconds = 6f;

        [SerializeField, Min(0f), Tooltip("Seconds the poles take to spiral together and annihilate.")]
        float annihilateSeconds = 9f;

        [SerializeField, Min(0f), Tooltip("Seconds the pair stands before it annihilates on its own; 0 = until " +
                                          "its world retires.")]
        float lifetimeSeconds = 0f;

        [SerializeField, Min(0f), Tooltip("Turns the poles spiral through on their way together.")]
        float spiralTurns = 2.5f;

        [SerializeField, Min(0f), Tooltip("Amplitude beats over the annihilation — quickening, anti-phase between " +
                                          "the poles, so the warp convolutes as they close.")]
        float beatCycles = 7f;

        [SerializeField, Range(0f, 1f), Tooltip("How deep each beat swings the poles' amplitudes against each other.")]
        float beatDepth = 0.5f;

        [Header("Wormhole (the Butterfly fold's mouths, seamless)")]
        [SerializeField, Tooltip("The seamless mouth material — WormholeSeamless.mat (alpha-dissolved, no rim).")]
        Material mouthMaterial;

        [SerializeField, Tooltip("Whose vessels a mouth may carry: the session's runtime GameData.")]
        GameDataSO gameData;

        [SerializeField, Min(0f)] float mouthExactRange = 2500f;
        [SerializeField, Min(1f)] float mouthExactFadeBand = 600f;
        [SerializeField, Range(0.1f, 1f)] float mouthExactRenderScale = 0.75f;
        [SerializeField, Range(32, 1024)] int mouthPanoramaFaceSize = 256;

        [Header("Scale model")]
        [SerializeField, Range(16, 63), Tooltip("Plates in the Cell Selector's scale model, split between the " +
                                                "poles. Kept under 64 so the signature filter keeps all of them.")]
        int modelPlates = 48;

        [SerializeField, Range(0.02f, 0.4f), Tooltip("Each pole's ball, as a fraction of the pole separation — " +
                                                     "NOT to scale.")]
        float modelBallFraction = 0.12f;

        public Vector3 RepulsorOffset => repulsorOffset;
        public float Strength => strength;
        public float ThroatRadius => throatRadius;
        public float LensStrength => lensStrength;
        public float VesselFeltStrength => vesselFeltStrength;
        public float VesselFeltCap => vesselFeltCap;
        public float VesselFeltReach => vesselFeltReach;
        public float SpiralTurns => spiralTurns;
        public float BeatCycles => beatCycles;
        public float BeatDepth => beatDepth;

        protected override int GetParameterHash() =>
            HashCode.Combine(repulsorOffset, modelPlates, modelBallFraction);

        /// <summary>A ball of plates for each pole — for the scale model only (see the class summary).</summary>
        protected override SpawnTrailData[] GenerateTrailData()
        {
            float ball = Mathf.Max(throatRadius, repulsorOffset.magnitude * modelBallFraction);
            int half = Mathf.Max(1, modelPlates / 2);
            return new[]
            {
                new SpawnTrailData(Ball(Vector3.zero, ball, half), false, domain),
                new SpawnTrailData(Ball(repulsorOffset, ball, half), false, domain),
            };
        }

        static SpawnPoint[] Ball(Vector3 centre, float radius, int n)
        {
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

        /// <summary>The settings this environment opens its wormhole with — what a crystal mechanic would pass.</summary>
        public CrystalWormhole.Settings BuildSettings() => new()
        {
            Strength = strength,
            ThroatRadius = throatRadius,
            LensStrength = lensStrength,
            FeltStrength = vesselFeltStrength,
            FeltCap = vesselFeltCap,
            FeltReach = vesselFeltReach,
            SpinAxis = spinAxis,
            MouthMaterial = mouthMaterial,
            Players = gameData ? gameData.Players : null,
            MouthExactRange = mouthExactRange,
            MouthExactFadeBand = mouthExactFadeBand,
            MouthExactRenderScale = mouthExactRenderScale,
            MouthPanoramaFaceSize = mouthPanoramaFaceSize,
            FormSeconds = formSeconds,
            AnnihilateSeconds = annihilateSeconds,
            LifetimeSeconds = lifetimeSeconds,
            SpiralTurns = spiralTurns,
            BeatCycles = beatCycles,
            BeatDepth = beatDepth,
        };

        /// <summary>
        /// A container that opens the wormhole — never the scale-model points. Edit mode spawns an empty
        /// container: the wells' driver is a play-mode object.
        /// </summary>
        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            trails.Clear();

            var container = new GameObject(name);
            if (!Application.isPlaying) return container;

            if (!gameData)
                CSDebug.LogWarning("[CrystalWormhole] No GameData on the environment - its mouths will carry no one.");
            CrystalWormhole.Open(container, Vector3.zero, repulsorOffset, BuildSettings());
            return container;
        }
    }
}

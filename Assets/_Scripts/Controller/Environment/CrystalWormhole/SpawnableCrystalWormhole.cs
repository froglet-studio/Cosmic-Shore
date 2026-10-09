using System;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Crystal Wormhole cell</b>'s environment (Docs/CRYSTAL_WORMHOLE.md): one
    /// <see cref="CrystalWormhole"/> — an attractor and a repulsor opened either side of the cell's centre
    /// (<see cref="repulsorOffset"/>), glued through their throats — and nothing else. It is a
    /// <see cref="SpawnableBase"/> only so a <see cref="CellConfigDataSO"/> can name it as its
    /// <c>EnvironmentPrefab</c>, which is how the Cell Selector offers a world and how <see cref="Cell"/>
    /// builds and retires one.
    ///
    /// <para><b>Spawn</b> lays nothing: it opens the wormhole on the container the Cell adopts. The pair
    /// forms, drifts together for <see cref="lifeSeconds"/>, meets at the centre and annihilates, and opens
    /// again <see cref="reformSeconds"/> later; when the cell retires the world, it annihilates inside the
    /// suction and stays gone.
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
        [SerializeField, Tooltip("From the cell's centre to the repulsor; the attractor opens at the opposite " +
                                 "point, so the pair meets at the centre. Keep both necks (each reaches " +
                                 "ThroatWarp's 4.5 λ beyond its throat) clear of the toy ring (horizontal, " +
                                 "~0.82 of the membrane).")]
        Vector3 repulsorOffset = new(0f, 360f, 0f);

        [SerializeField, Min(0f), Tooltip("Each pole's gravity on PRISMS (BlackHoleConfig: GM = strength × " +
                                          "gmPerStrength), Plummer-softened by the throat.")]
        float strength = 15f;

        [SerializeField, Min(1f), Tooltip("The throats' radius, world units: the spheres glued to each other. MUST " +
                                          "equal the cell's ThroatWarp throatRadius (CrystalWormholeTests holds it).")]
        float throatRadius = 30f;

        [SerializeField, Range(0.02f, 0.9f), Tooltip("The scale on the throat, used only if the live warp field is " +
                                                     "not a ThroatWarp (the cell's is; its own value wins).")]
        float throatScale = 0.2f;

        [SerializeField, Tooltip("Axis the wells spin about (they drag no frame).")]
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
        [SerializeField, Min(0f), Tooltip("Seconds from opening until the poles meet and annihilate. 0 = they " +
                                          "stand until the world retires.")]
        float lifeSeconds = 60f;

        [SerializeField, Min(0f), Tooltip("Seconds the pair takes to form where it opens.")]
        float formSeconds = 4f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Separation (a fraction of the opening one) at which the poles " +
                                                   "touch and start to annihilate — 0.45 is the last ~12 s of 60.")]
        float touch = 0.45f;

        [SerializeField, Min(0f), Tooltip("Turns the pair orbits through on its way together.")]
        float spiralTurns = 2f;

        [SerializeField, Min(0f), Tooltip("Amplitude beats over the annihilation — quickening, anti-phase between " +
                                          "the poles, so the warp convolutes as they close.")]
        float beatCycles = 7f;

        [SerializeField, Range(0f, 1f), Tooltip("How deep each beat swings the poles' amplitudes against each other.")]
        float beatDepth = 0.6f;

        [SerializeField, Tooltip("Seconds after annihilating that the pair opens again, so it can be watched " +
                                 "again; negative = never.")]
        float reformSeconds = 8f;

        [Header("View (CrystalWormholeView)")]
        [SerializeField, Tooltip("Whose vessels the throats carry: the session's runtime GameData.")]
        GameDataSO gameData;

        [SerializeField, Range(0.25f, 1f), Tooltip("The far eye's resolution, × the screen's (the device tier caps it).")]
        float farEyeRenderScale = 0.75f;

        [SerializeField, Range(64, 1024), Tooltip("Each pole's panorama face, texels: what the far eye's frame misses.")]
        int panoramaFaceSize = 256;

        [SerializeField, Min(1f), Tooltip("Distance a panorama assumes what it shows is at (the membrane).")]
        float proxyRadius = 1200f;

        [SerializeField, Range(16, 256), Tooltip("Ray steps per pixel at most. Rays near the crystal ball's ring use " +
                                                 "them all; everything else leaves in a few dozen.")]
        int lensSteps = 96;

        [Header("Scale model")]
        [SerializeField, Range(16, 63), Tooltip("Plates in the Cell Selector's scale model, split between the " +
                                                "poles. Kept under 64 so the signature filter keeps all of them.")]
        int modelPlates = 48;

        [SerializeField, Range(0.02f, 0.4f), Tooltip("Each pole's ball, as a fraction of the pole separation — " +
                                                     "NOT to scale.")]
        float modelBallFraction = 0.12f;

        public Vector3 RepulsorOffset => repulsorOffset;
        public Vector3 AttractorOffset => -repulsorOffset;
        public float Strength => strength;
        public float ThroatRadius => throatRadius;
        public float VesselFeltStrength => vesselFeltStrength;
        public float VesselFeltCap => vesselFeltCap;
        public float VesselFeltReach => vesselFeltReach;
        public float LifeSeconds => lifeSeconds;
        public float Touch => touch;
        public float SpiralTurns => spiralTurns;
        public float BeatCycles => beatCycles;
        public float BeatDepth => beatDepth;

        protected override int GetParameterHash() =>
            HashCode.Combine(repulsorOffset, modelPlates, modelBallFraction);

        /// <summary>A ball of plates for each pole — for the scale model only (see the class summary).</summary>
        protected override SpawnTrailData[] GenerateTrailData()
        {
            float ball = Mathf.Max(throatRadius, 2f * repulsorOffset.magnitude * modelBallFraction);
            int half = Mathf.Max(1, modelPlates / 2);
            return new[]
            {
                new SpawnTrailData(Ball(AttractorOffset, ball, half), false, domain),
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
            ThroatScale = throatScale,
            FeltStrength = vesselFeltStrength,
            FeltCap = vesselFeltCap,
            FeltReach = vesselFeltReach,
            SpinAxis = spinAxis,
            Players = gameData ? gameData.Players : null,
            LifeSeconds = lifeSeconds,
            FormSeconds = formSeconds,
            Touch = touch,
            SpiralTurns = spiralTurns,
            BeatCycles = beatCycles,
            BeatDepth = beatDepth,
            ReformSeconds = reformSeconds,
            FarEyeRenderScale = farEyeRenderScale,
            PanoramaFaceSize = panoramaFaceSize,
            ProxyRadius = proxyRadius,
            LensSteps = lensSteps,
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
                CSDebug.LogWarning("[CrystalWormhole] No GameData on the environment - its throats will carry no one.");
            CrystalWormhole.Open(container, AttractorOffset, repulsorOffset, BuildSettings());
            return container;
        }
    }
}

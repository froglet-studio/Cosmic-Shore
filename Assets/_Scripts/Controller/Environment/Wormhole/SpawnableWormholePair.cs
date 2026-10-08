using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Wormhole</b> cell's environment: two sphere mouths that are one place
    /// (<see cref="WormholeMouth"/>, <see cref="WormholeGeometry"/>). It is a cell ENVIRONMENT in
    /// the ordinary sense — a <see cref="CellConfigDataSO.EnvironmentPrefab"/>, spawned by the Cell
    /// on every machine, parented to it, suctioned away with it on a swap — so the world joins the
    /// Cell Selector's rotation the way every other one does: by being in the cell's
    /// <c>CellConfigs</c>, with nothing the toy has to know about it.
    ///
    /// <para><b>It lays no prisms.</b> <see cref="Spawn"/> builds the two mouths and nothing else,
    /// so the build is instant and the cell's mass is all grown and flown. The generator half
    /// (<see cref="GenerateTrailData"/>) still answers, because the Cell Selector's scale model
    /// (<see cref="CellMiniatureBuilder"/>) samples it: it emits the two sphere shells and a dotted
    /// throat between them, so the world's thumbnail is two linked spheres rather than an empty
    /// slot. Those points are never instantiated.</para>
    ///
    /// <para><b>The tuning lives here</b>, on the prefab — the environment-prefab pattern every
    /// other cell world follows. It is copied into each mouth when the world is built
    /// (<see cref="WormholeMouth.Settings"/>), so a change takes effect on the next build.</para>
    ///
    /// <para><b>An OPEN pair.</b> These mouths carry every pilot; the rim wears this spawnable's
    /// <c>domain</c> (Blue, the platform's no-team colour) — the same hue rule as the Butterfly's
    /// domain-locked fold pairs, with the neutral domain.</para>
    /// </summary>
    public sealed class SpawnableWormholePair : SpawnableBase
    {
        [Header("Mouths")]
        [SerializeField, Tooltip("First mouth's position relative to the cell centre. The two mouths " +
                                 "are most legible in DIFFERENT surroundings - one near the nucleus, " +
                                 "one out by the membrane - so the view through each is unmistakably " +
                                 "somewhere else.")]
        Vector3 mouthAPosition = new(0f, 0f, 620f);

        [SerializeField, Tooltip("Second mouth's position relative to the cell centre.")]
        Vector3 mouthBPosition = new(-700f, 450f, -480f);

        [SerializeField, Min(5f), Tooltip("Radius of each mouth's sphere, world units. Both mouths share " +
                                          "it: the interior of one IS the interior of the other.")]
        float mouthRadius = 70f;

        [SerializeField, Min(0.01f), Tooltip("Seconds a mouth takes to grow from nothing when the world " +
                                              "is built. A mouth carries nobody until both ends have " +
                                              "fully bloomed.")]
        float bloomSeconds = 1.5f;

        [Header("Surface")]
        [SerializeField, Tooltip("Material for the sphere surfaces (CosmicShore/Wormhole). It shows the " +
                                 "exact view and the partner's panorama; its rim and proxy distance are " +
                                 "tuned on the material.")]
        Material surfaceMaterial;

        [Header("Exact view (the player-camera render)")]
        [SerializeField, Min(0f), Tooltip("Furthest the player's camera may be from a mouth for it to get " +
                                          "an EXACT view - one render of the world from the player's own " +
                                          "vantage carried through the pair. Beyond this plus the fade band " +
                                          "the mouth shows only its partner's panorama.")]
        float exactRange = 1500f;

        [SerializeField, Min(1f), Tooltip("Distance over which the exact view crossfades into the panorama " +
                                          "past Exact Range, so a mouth never pops between the two.")]
        float exactFadeBand = 400f;

        [SerializeField, Range(0.25f, 1f), Tooltip("Exact view resolution as a fraction of the gameplay " +
                                                   "camera's. The device tier's own ceiling " +
                                                   "(PlatformProfileSO.FoldGateWindowMaxRenderScale) " +
                                                   "still applies on top.")]
        float exactRenderScale = 0.75f;

        [Header("Panorama (the all-directions capture)")]
        [SerializeField, Range(64, 1024), Tooltip("Texels per side of each of the six panorama faces a " +
                                                  "mouth's camera captures. One face is re-captured per " +
                                                  "frame while the partner is on screen.")]
        int panoramaFaceSize = 256;

        [Header("Audio")]
        [SerializeField, Tooltip("FMOD event played at the exit when a pilot comes through either mouth. " +
                                 "Shipped EMPTY on purpose (IMPACT_EFFECTS_AND_AUDIO.md - never a temp " +
                                 "event): silence until the audio owner assigns one.")]
        FMODUnity.EventReference transitEvent;

        [Header("Players")]
        [SerializeField, Tooltip("The shared runtime GameData asset. Its Players list is who a mouth can " +
                                 "carry; each machine only moves the vessels it owns.")]
        GameDataSO gameData;

        [Header("Scale model (Cell Selector thumbnail)")]
        [SerializeField, Range(50, 2000), Tooltip("Points sampled on each mouth's shell for the Cell " +
                                                  "Selector's scale model. Never spawned.")]
        int modelPointsPerMouth = 420;

        [SerializeField, Range(0, 200), Tooltip("Points on the dotted throat drawn between the mouths in " +
                                                "the scale model. Never spawned.")]
        int modelThroatPoints = 40;

        /// <summary>
        /// Build the two mouths under one container and pair them. Called on the prefab ASSET by
        /// the Cell (as every environment is), which parents the container to itself at the cell's
        /// centre — so the authored positions are cell-relative.
        /// </summary>
        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            var container = new GameObject(name);

            IReadOnlyList<IPlayer> players = gameData ? gameData.Players : null;
            if (players == null && Application.isPlaying)
                CSDebug.LogWarning($"[Wormhole] {name} has no GameData assigned - the mouths will draw " +
                                   "but carry nobody.", this);

            var a = MakeMouth(container.transform, "Wormhole Mouth A", mouthAPosition, players);
            var b = MakeMouth(container.transform, "Wormhole Mouth B", mouthBPosition, players);
            WormholeMouth.Pair(a, b);
            return container;
        }

        WormholeMouth MakeMouth(Transform parent, string mouthName, Vector3 localPosition,
                                IReadOnlyList<IPlayer> players)
        {
            var go = new GameObject(mouthName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var mouth = go.AddComponent<WormholeMouth>();
            mouth.Build(MouthSettings(), players, mouthRadius);
            return mouth;
        }

        WormholeMouth.Settings MouthSettings()
        {
            var theme = gameData ? gameData.ThemeManagerData : null;
            return new WormholeMouth.Settings
            {
                SurfaceMaterial = surfaceMaterial,
                BloomSeconds = bloomSeconds,
                ExactRange = exactRange,
                ExactFadeBand = exactFadeBand,
                ExactRenderScale = exactRenderScale,
                PanoramaFaceSize = panoramaFaceSize,
                TransitEvent = transitEvent,
                RimTint = ToyFactory.DomainAccentColor(theme, domain),
                DomainLocked = false,
                Domain = domain,
            };
        }

        // ---- the scale model's points (never laid) -------------------------------------------

        protected override SpawnTrailData[] GenerateTrailData()
        {
            int shell = Mathf.Max(8, modelPointsPerMouth);
            var mouthA = ShellPoints(mouthAPosition, shell);
            var mouthB = ShellPoints(mouthBPosition, shell);

            var throat = new SpawnPoint[Mathf.Max(0, modelThroatPoints)];
            Vector3 along = mouthBPosition - mouthAPosition;
            float gap = along.magnitude - 2f * mouthRadius;
            if (throat.Length > 0 && gap > 0f && SafeLookRotation.TryGet(along, Vector3.up, out var rot))
            {
                Vector3 dir = along / along.magnitude;
                Vector3 start = mouthAPosition + dir * mouthRadius;
                for (int i = 0; i < throat.Length; i++)
                {
                    float t = (i + 0.5f) / throat.Length;
                    throat[i] = new SpawnPoint(start + dir * (gap * t), rot, Vector3.one * (mouthRadius * 0.12f));
                }
            }
            else throat = System.Array.Empty<SpawnPoint>();

            return new[]
            {
                new SpawnTrailData(mouthA, true, domain),
                new SpawnTrailData(mouthB, true, domain),
                new SpawnTrailData(throat, false, domain),
            };
        }

        /// <summary>An even (Fibonacci) scatter over a mouth's shell, each shard lying flat on it.</summary>
        SpawnPoint[] ShellPoints(Vector3 centre, int count)
        {
            var points = new SpawnPoint[count];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            var shard = new Vector3(mouthRadius * 0.08f, mouthRadius * 0.08f, mouthRadius * 0.02f);
            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = golden * i;
                var normal = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);
                Vector3 up = Mathf.Abs(normal.y) > 0.99f ? Vector3.right : Vector3.up;
                points[i] = new SpawnPoint(centre + normal * mouthRadius,
                                           Quaternion.LookRotation(normal, up), shard);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + mouthAPosition.GetHashCode();
                h = h * 31 + mouthBPosition.GetHashCode();
                h = h * 31 + mouthRadius.GetHashCode();
                h = h * 31 + modelPointsPerMouth;
                h = h * 31 + modelThroatPoints;
                h = h * 31 + (int)domain;
                return h;
            }
        }
    }
}

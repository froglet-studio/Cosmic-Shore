using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment.Spawning;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Models.Enums;

namespace CosmicShore.Game.Environment.MiniGameObjects
{
    /// <summary>
    /// Spawns prisms along a helicoid — the only ruled minimal surface besides the plane.
    ///
    /// The helicoid is parameterized as:
    ///   x = v · cos(u)
    ///   y = c · u          (height along axis)
    ///   z = v · sin(u)
    ///
    /// where u is the angular parameter and v is the radial distance from the central axis.
    /// The result is a sweeping helical ramp — like a spiral staircase made of blocks.
    ///
    /// Multiple interleaved helicoids at angular offsets create parallel ramps that
    /// spiral around each other, offering layered flight paths at different altitudes.
    ///
    /// The helicoid is locally isometric to the catenoid via the Bonnet transformation;
    /// both are minimal surfaces (zero mean curvature everywhere).
    /// </summary>
    public class SpawnableHelicoid : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2.5f, 2.5f, 2.5f);

        [Header("Helicoid Shape")]
        [Tooltip("Number of full turns of the helix.")]
        [SerializeField] float turns = 4f;

        [Tooltip("Total height of the structure.")]
        [SerializeField] float height = 200f;

        [Tooltip("Inner radius — distance from axis where blocks start.")]
        [SerializeField] float innerRadius = 8f;

        [Tooltip("Outer radius — distance from axis where blocks end.")]
        [SerializeField] float outerRadius = 55f;

        [Header("Density")]
        [Tooltip("Angular sample points per full turn.")]
        [SerializeField] int samplesPerTurn = 40;

        [Tooltip("Radial sample points from inner to outer radius.")]
        [SerializeField] int radialSamples = 8;

        [Header("Multi-Ramp")]
        [Tooltip("Number of interleaved helicoid sheets. 2 = double helix, 3 = triple, etc.")]
        [SerializeField] int sheets = 2;

        [Header("Visual")]
        [SerializeField] bool colorBySheet = true;
        [SerializeField] Domains[] sheetDomains = new Domains[]
        {
            Domains.Blue,
            Domains.Gold,
            Domains.Jade,
        };

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();

            float pitch = height / (turns * 2f * Mathf.PI); // c in the parametrization
            int totalAngularSamples = Mathf.RoundToInt(turns * samplesPerTurn);

            for (int sheet = 0; sheet < sheets; sheet++)
            {
                float sheetOffset = 2f * Mathf.PI * sheet / sheets;

                Domains sheetDomain = colorBySheet && sheetDomains.Length > 0
                    ? sheetDomains[sheet % sheetDomains.Length]
                    : domain;

                var points = new List<SpawnPoint>();

                for (int iu = 0; iu <= totalAngularSamples; iu++)
                {
                    float u = 2f * Mathf.PI * turns * iu / totalAngularSamples + sheetOffset;

                    for (int iv = 0; iv < radialSamples; iv++)
                    {
                        float v = Mathf.Lerp(innerRadius, outerRadius, (float)iv / (radialSamples - 1));

                        Vector3 position = new Vector3(
                            v * Mathf.Cos(u),
                            pitch * u - height * 0.5f, // center vertically
                            v * Mathf.Sin(u)
                        );

                        // Look along the spiral tangent direction
                        float uNext = u + 0.01f;
                        Vector3 tangent = new Vector3(
                            v * Mathf.Cos(uNext),
                            pitch * uNext - height * 0.5f,
                            v * Mathf.Sin(uNext)
                        );

                        var rotation = SpawnPoint.LookRotation(tangent, position, Vector3.up);

                        points.Add(new SpawnPoint(position, rotation, blockScale));
                    }
                }

                trailDataList.Add(new SpawnTrailData(points.ToArray(), false, sheetDomain));
            }

            return trailDataList.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(turns, height, innerRadius, outerRadius,
                System.HashCode.Combine(samplesPerTurn, radialSamples, sheets, blockScale, seed));
        }
    }
}

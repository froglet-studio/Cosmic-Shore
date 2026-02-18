using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms along the Schwarz P minimal surface — the simplest triply periodic
    /// minimal surface (TPMS).
    ///
    /// The surface is approximated by the zero-level-set of:
    ///   f(x,y,z) = cos(x) + cos(y) + cos(z) = 0
    ///
    /// This creates a network of interconnected tunnels running in all three coordinate
    /// directions, with cubic symmetry. Each tunnel opens into six neighbors, forming
    /// a labyrinth that rewards exploration. The structure tiles infinitely — we sample
    /// a finite number of periods.
    ///
    /// Schwarz discovered this surface in 1865; it appears naturally in block copolymers,
    /// butterfly wing scales, and lipid membranes.
    /// </summary>
    public class SpawnableSchwarzPSurface : SpawnableAbstractBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 2f);

        [Header("Surface Sampling")]
        [Tooltip("Number of full periods of the surface in each direction.")]
        [SerializeField] int periods = 3;

        [Tooltip("Sample points per period per axis. Higher = denser, smoother surface.")]
        [SerializeField] int samplesPerPeriod = 10;

        [Tooltip("How close to zero the implicit function must be to place a block. " +
                 "Lower = thinner shell, higher = thicker walls.")]
        [SerializeField] float surfaceThreshold = 0.4f;

        [Tooltip("World-space size of one period. Controls overall scale.")]
        [SerializeField] float periodScale = 30f;

        [Header("Visual")]
        [Tooltip("Alternate domains based on which side of the surface a block sits near.")]
        [SerializeField] bool colorBySide = true;
        [SerializeField] Domains positiveDomain = Domains.Blue;
        [SerializeField] Domains negativeDomain = Domains.Jade;

        static int ObjectsSpawned = 0;

        public override GameObject Spawn()
        {
            var container = new GameObject($"SchwarzP{ObjectsSpawned++}");
            int blockIndex = 0;

            var trail = new Trail();
            trails.Add(trail);

            int totalSamples = periods * samplesPerPeriod;
            float halfExtent = periods * Mathf.PI; // f uses raw radians; one period = 2π

            for (int ix = 0; ix < totalSamples; ix++)
            {
                for (int iy = 0; iy < totalSamples; iy++)
                {
                    for (int iz = 0; iz < totalSamples; iz++)
                    {
                        // Map grid index to [0, periods*2π)
                        float u = 2f * Mathf.PI * ix / samplesPerPeriod;
                        float v = 2f * Mathf.PI * iy / samplesPerPeriod;
                        float w = 2f * Mathf.PI * iz / samplesPerPeriod;

                        float f = Mathf.Cos(u) + Mathf.Cos(v) + Mathf.Cos(w);

                        if (Mathf.Abs(f) > surfaceThreshold) continue;

                        // Map to world coordinates centered at origin
                        float worldScale = periodScale / (2f * Mathf.PI);
                        Vector3 position = new Vector3(
                            (u - halfExtent) * worldScale,
                            (v - halfExtent) * worldScale,
                            (w - halfExtent) * worldScale
                        );

                        // Gradient of f gives surface normal — use it for block orientation
                        Vector3 gradient = new Vector3(
                            -Mathf.Sin(u),
                            -Mathf.Sin(v),
                            -Mathf.Sin(w)
                        );

                        Vector3 lookTarget = gradient.sqrMagnitude > 0.001f
                            ? position + gradient.normalized
                            : position + Vector3.forward;

                        Domains blockDomain = colorBySide
                            ? (f >= 0 ? positiveDomain : negativeDomain)
                            : domain;

                        CreateBlock(position, lookTarget,
                            $"{container.name}::SURFACE::{blockIndex}",
                            trail, blockScale, prism, container, blockDomain);

                        blockIndex++;
                    }
                }
            }

            return container;
        }

        public override GameObject Spawn(int intensityLevel)
        {
            return Spawn();
        }
    }
}

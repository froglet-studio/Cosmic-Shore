using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Answers "what SHAPE of prismscape does this prism belong to?" - the 0-3 dimension
    /// ladder of <see cref="PrismscapeDimension"/>.
    ///
    /// Two tiers of evidence, cheapest first:
    ///
    ///  * A prism that carries a <see cref="Trail"/> takes the dimension its LAYER declared on
    ///    the container (<see cref="Trail.Dimension"/> - default 1D, the vessel wake; the
    ///    gyroid/Schwarz surface spawnables declare 2D), downgraded to Singleton when the
    ///    container holds a single block. `Trail` is the general lay container, so its
    ///    presence is membership evidence, never shape evidence. No geometry is consulted.
    ///  * Anything else is classified from its NEIGHBOURHOOD via
    ///    <see cref="PrismSpatialIndex.QuerySphere"/> - the canonical spatial store, never
    ///    physics. A shell (gyroid / Schwarz-P flora, walls) fills its neighbourhood like an
    ///    r² patch; a solid fills it like an r³ ball. The census radius is measured in
    ///    multiples of the prism's own largest extent, so the read is scale-free.
    ///
    /// The census is a heuristic and is priced like one: a handful of distance checks against
    /// an already-maintained index, no allocation beyond the shared scratch list, and only on
    /// demand (an attach, a query) - never per frame.
    /// </summary>
    public static class PrismscapeTopology
    {
        /// <summary>Census radius in multiples of the prism's largest world extent.</summary>
        const float CensusRadiusScale = 3f;

        /// <summary>
        /// Below this many neighbours the prism is effectively alone. A radius-3s patch of a
        /// contiguous surface holds tens of blocks; two or three is scattered debris.
        /// </summary>
        const int SingletonBelow = 3;

        /// <summary>
        /// Above this many neighbours in the census ball the structure reads as filled rather
        /// than shell-like. Geometry says a 3-extent-radius PATCH is on the order of pi*3^2/1
        /// ~ 28 same-size blocks and a filled BALL on the order of (4/3)*pi*3^3 ~ 113; the
        /// threshold sits between them with room for ragged edges and mixed block sizes.
        /// </summary>
        const int VolumeAbove = 55;

        // Main-thread only, like every QuerySphere consumer; the census is non-reentrant.
        static readonly List<Prism> s_census = new(128);

        public static PrismscapeDimension DimensionOf(Prism prism)
        {
            if (!prism) return PrismscapeDimension.Singleton;

            // Authored evidence first: the layer declared the container's dimension.
            if (prism.Trail != null)
            {
                return prism.Trail.TrailList != null && prism.Trail.TrailList.Count <= 1
                    ? PrismscapeDimension.Singleton
                    : prism.Trail.Dimension;
            }

            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return PrismscapeDimension.Singleton;

            var s = prism.transform.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            float radius = Mathf.Max(1f, extent) * CensusRadiusScale;

            int n = index.QuerySphere(prism.transform.position, radius, s_census);

            if (n < SingletonBelow) return PrismscapeDimension.Singleton;
            return n > VolumeAbove ? PrismscapeDimension.Volume : PrismscapeDimension.Surface;
        }
    }
}

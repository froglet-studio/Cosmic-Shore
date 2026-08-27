using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A scoped, opt-in THINNING of prism trail lays: inside a scope of stride N, every dense
    /// trail keeps only every Nth prism. The SHAPE survives - the strided points are a uniform
    /// subsample along the authored lay order, so a torus is still a torus and a helicoid still
    /// a helicoid - at 1/N the prisms, colliders, spatial-index entries and draw work.
    ///
    /// <para><b>This exists for the mode preview's flight arena and nothing else.</b> A preview
    /// is a taste of a world beside a menu that is still running; the real scene builds the real
    /// world. Default stride is 1 (no effect), the scope restores on dispose, and short trails
    /// (below <see cref="MinPointsToDecimate"/>) are never thinned - a 12-prism feature IS its
    /// twelve prisms, while a 400-prism ribbon reads the same at 200.</para>
    ///
    /// <para>This is "not creating mass", which the conserved-mass law permits - nothing here
    /// removes a prism that exists. Application point: <see cref="SpawnableBase.SpawnPrismTrail"/>
    /// strides its points BEFORE handing them to the trail builder, so streamed
    /// (<c>layAcrossFrames</c>) lays that outlive the scope were already thinned at call time.</para>
    /// </summary>
    public static class PrismLayDecimation
    {
        /// <summary>Trails shorter than this are laid in full - thinning small furniture
        /// changes what it IS rather than how dense it is.</summary>
        public const int MinPointsToDecimate = 25;

        static int _stride = 1;

        /// <summary>The stride currently in effect. 1 = lay everything (the default).</summary>
        public static int Stride => _stride;

        /// <summary>Enter a decimation scope: <c>using (PrismLayDecimation.At(2)) { ... }</c>.</summary>
        public static Scope At(int stride) => new(stride);

        public readonly struct Scope : IDisposable
        {
            readonly int _previous;

            public Scope(int stride)
            {
                _previous = _stride;
                _stride = Math.Max(1, stride);
            }

            public void Dispose() => _stride = _previous;
        }

        /// <summary>The points to actually lay under the current stride. Returns the input
        /// array untouched when no thinning applies.</summary>
        public static SpawnPoint[] Apply(SpawnPoint[] points)
        {
            if (_stride <= 1 || points == null || points.Length < MinPointsToDecimate)
                return points;

            int count = (points.Length + _stride - 1) / _stride;
            var strided = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
                strided[i] = points[i * _stride];
            return strided;
        }
    }
}

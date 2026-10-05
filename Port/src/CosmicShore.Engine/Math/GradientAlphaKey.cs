namespace CosmicShore.Engine
{
    /// <summary>Alpha stop for a <see cref="Gradient"/> (E14).</summary>
    public struct GradientAlphaKey
    {
        public float alpha;

        /// <summary>Normalized position of the key, [0, 1].</summary>
        public float time;

        public GradientAlphaKey(float alpha, float time)
        {
            this.alpha = alpha;
            this.time = time;
        }
    }
}

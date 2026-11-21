namespace CosmicShore.Engine
{
    /// <summary>Color stop for a <see cref="Gradient"/> (E14). Alpha rides on <see cref="GradientAlphaKey"/>.</summary>
    public struct GradientColorKey
    {
        public Color color;

        /// <summary>Normalized position of the key, [0, 1].</summary>
        public float time;

        public GradientColorKey(Color col, float time)
        {
            color = col;
            this.time = time;
        }
    }
}

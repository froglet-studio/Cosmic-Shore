namespace CosmicShore.Engine
{
    /// <summary>
    /// What a shader's source declares beyond its properties, as the original engine reads it:
    /// the colour and texture tagged [MainColor] / [MainTexture] (what <see cref="Material.color"/>
    /// and <see cref="Material.mainTexture"/> address) and the queue its SubShader tags ask for.
    /// </summary>
    public sealed class ShaderSourceInfo
    {
        /// <summary>The [MainColor] property, or null (then _Color).</summary>
        public string MainColor;
        /// <summary>The [MainTexture] property, or null (then _MainTex).</summary>
        public string MainTexture;
        /// <summary>The "Queue" tag as a number ("Transparent+1" = 3001), -1 when untagged.</summary>
        public int Queue = -1;
    }
}

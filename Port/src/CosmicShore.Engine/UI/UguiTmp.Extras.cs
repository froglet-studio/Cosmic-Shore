namespace CosmicShore.Engine.UI
{
    public abstract partial class TMP_Text
    {
        /// <summary>Original contract: the alpha of <see cref="Graphic.color"/>; setting it keeps the RGB.</summary>
        public float alpha
        {
            get => color.a;
            set { var c = color; c.a = value; color = c; }
        }

        /// <summary>
        /// Original contract: TMP skips the per-frame lossy-scale check for a static text. The port lays text out on
        /// demand and never polls scale, so the flag is kept and changes nothing.
        /// </summary>
        public bool isTextObjectScaleStatic { get; set; }
    }

    /// <summary>
    /// TMP's rebuild queue (original: TMPro.TMP_UpdateManager). The port has no per-frame text queue (a text lays out
    /// when it is read or drawn), so registration is accepted and does nothing.
    /// </summary>
    public static class TMP_UpdateManager
    {
        public static void RegisterTextElementForLayoutRebuild(TMP_Text element) { }
        public static void RegisterTextElementForGraphicRebuild(TMP_Text element) { }
        public static void RegisterTextElementForCullingUpdate(TMP_Text element) { }
        public static void UnRegisterTextElementForRebuild(TMP_Text element) { }
    }

    public partial struct ColorBlock : System.IEquatable<ColorBlock>
    {
        public bool Equals(ColorBlock other) =>
            normalColor == other.normalColor && highlightedColor == other.highlightedColor && pressedColor == other.pressedColor &&
            selectedColor == other.selectedColor && disabledColor == other.disabledColor &&
            colorMultiplier == other.colorMultiplier && fadeDuration == other.fadeDuration;
        public override bool Equals(object obj) => obj is ColorBlock other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(normalColor, highlightedColor, pressedColor, selectedColor, disabledColor, colorMultiplier, fadeDuration);
        public static bool operator ==(ColorBlock a, ColorBlock b) => a.Equals(b);
        public static bool operator !=(ColorBlock a, ColorBlock b) => !a.Equals(b);
    }
}

namespace CosmicShore.Engine.Sprites
{
    /// <summary>Sprite geometry helpers (original: UnityEngine.Sprites.DataUtility).</summary>
    public static class DataUtility
    {
        /// <summary>The sprite's UV rect on its texture: (xMin, yMin, xMax, yMax).</summary>
        public static Vector4 GetOuterUV(Sprite sprite) => sprite ? sprite.outerUV : new Vector4(0f, 0f, 1f, 1f);
        /// <summary>The UV rect of the 9-slice centre (the sprite rect inset by its border).</summary>
        public static Vector4 GetInnerUV(Sprite sprite) => sprite ? sprite.innerUV : new Vector4(0f, 0f, 1f, 1f);
        public static Vector4 GetPadding(Sprite sprite) => Vector4.zero;
        public static Vector2 GetMinSize(Sprite sprite) => sprite ? new Vector2(sprite.border.x + sprite.border.z, sprite.border.y + sprite.border.w) : Vector2.zero;
    }
}

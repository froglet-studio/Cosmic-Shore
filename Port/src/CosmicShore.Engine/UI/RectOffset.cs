namespace CosmicShore.Engine
{
    /// <summary>Original contract: integer edge offsets (layout-group padding).</summary>
    public class RectOffset
    {
        public int left;
        public int right;
        public int top;
        public int bottom;

        public RectOffset() { }

        public RectOffset(int left, int right, int top, int bottom)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }

        public int horizontal => left + right;
        public int vertical => top + bottom;

        public override string ToString() => $"RectOffset (l:{left} r:{right} t:{top} b:{bottom})";
    }
}

namespace CosmicShore.Engine
{
    /// <summary>
    /// Base texture asset (UI arc B2). Headless-first: dimensions are the data the
    /// engine consumes today (RawImage.SetNativeSize, Sprite geometry); pixel storage
    /// and GPU upload arrive with the presentation phase (Arc C), same convention as
    /// the <see cref="Material"/>/<see cref="Sprite"/> stubs.
    /// </summary>
    public abstract class Texture : Object
    {
        public virtual int width { get; set; }
        public virtual int height { get; set; }
    }

    /// <summary>2D texture asset. Headless: a named (width, height) reference.</summary>
    public class Texture2D : Texture
    {
        public Texture2D(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }
}

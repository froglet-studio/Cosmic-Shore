namespace CosmicShore.Engine
{
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }

    namespace Rendering
    {
        public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    }

    /// <summary>
    /// The active scene's lighting environment (original contract: UnityEngine.RenderSettings).
    /// A Single scene load replaces it with that scene's authored settings; the 3D pass reads
    /// the skybox, fog and ambient terms from here.
    /// </summary>
    public static class RenderSettings
    {
        public static Material skybox { get; set; }
        public static bool fog { get; set; }
        public static Color fogColor { get; set; } = new(0.5f, 0.5f, 0.5f, 1f);
        public static FogMode fogMode { get; set; } = FogMode.ExponentialSquared;
        public static float fogDensity { get; set; } = 0.01f;
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; } = 300f;
        public static Rendering.AmbientMode ambientMode { get; set; } = Rendering.AmbientMode.Skybox;
        public static Color ambientSkyColor { get; set; } = new(0.212f, 0.227f, 0.259f, 1f);
        public static Color ambientEquatorColor { get; set; } = new(0.114f, 0.125f, 0.133f, 1f);
        public static Color ambientGroundColor { get; set; } = new(0.047f, 0.043f, 0.035f, 1f);
        public static Color ambientLight { get => ambientSkyColor; set => ambientSkyColor = value; }
        public static float ambientIntensity { get; set; } = 1f;
        public static float reflectionIntensity { get; set; } = 1f;
        public static Light sun { get; set; }
        public static Color subtractiveShadowColor { get; set; } = new(0.42f, 0.478f, 0.627f, 1f);
    }
}

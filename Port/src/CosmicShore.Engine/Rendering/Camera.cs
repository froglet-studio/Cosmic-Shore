namespace CosmicShore.Engine
{
    /// <summary>
    /// E2 camera data stub (VESSEL_LAYER.md): data-only — pose lives on the Transform,
    /// projection parameters live here, nothing renders until the presentation phase.
    /// <see cref="main"/> is the first enabled camera created, mirroring Unity's
    /// MainCamera-tagged lookup closely enough for ported call sites.
    /// </summary>
    public class Camera : Component
    {
        public static Camera main { get; internal set; }

        public bool enabled = true;
        public float fieldOfView = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
        public bool orthographic;
        public float orthographicSize = 5f;
        public float depth;

        void Awake()
        {
            if (main is null && enabled)
                main = this;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(main, this))
                main = null;
        }
    }
}

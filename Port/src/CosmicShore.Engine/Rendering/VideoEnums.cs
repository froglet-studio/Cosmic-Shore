// UnityEngine.Video's value types, owned by the engine beside VideoPlayer/VideoClip.
// Numeric values are frozen to the original's so serialized scenes/prefabs read back unchanged.
namespace CosmicShore.Engine.Video
{
    public enum VideoRenderMode
    {
        CameraFarPlane = 0,
        CameraNearPlane = 1,
        RenderTexture = 2,
        MaterialOverride = 3,
        APIOnly = 4,
    }

    public enum VideoAudioOutputMode
    {
        None = 0,
        AudioSource = 1,
        Direct = 2,
        APIOnly = 3,
    }

    public enum VideoSource
    {
        VideoClip = 0,
        Url = 1,
    }

    public enum VideoAspectRatio
    {
        NoScaling = 0,
        FitVertically = 1,
        FitHorizontally = 2,
        FitInside = 3,
        FitOutside = 4,
        Stretch = 5,
    }

    public enum VideoTimeSource
    {
        AudioDSPTimeSource = 0,
        GameTimeSource = 1,
    }

    public enum VideoTimeReference
    {
        Freerun = 0,
        InternalTime = 1,
        ExternalTime = 2,
    }

    public enum VideoTimeUpdateMode
    {
        DSPTime = 0,
        GameTime = 1,
        UnscaledGameTime = 2,
    }

    public enum Video3DLayout
    {
        No3D = 0,
        SideBySide3D = 1,
        OverUnder3D = 2,
    }
}

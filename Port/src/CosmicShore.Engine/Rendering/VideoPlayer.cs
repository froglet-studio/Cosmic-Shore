namespace CosmicShore.Engine
{
    /// <summary>
    /// Stand-in for <c>UnityEngine.Video.VideoPlayer</c> (engine addition for SA1:
    /// SO_Game / SO_VesselAbility hold preview-clip references). Headless-first —
    /// playback semantics arrive with the presentation phase; until then the reference
    /// is the asset, exactly like the <see cref="Sprite"/> stub.
    /// </summary>
    public class VideoPlayer : Behaviour
    {
    }
}

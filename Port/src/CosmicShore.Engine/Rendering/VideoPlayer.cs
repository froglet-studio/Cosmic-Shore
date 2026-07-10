namespace CosmicShore.Engine
{
    /// <summary>
    /// Stand-in for <c>UnityEngine.Video.VideoClip</c> (engine addition for Arc F
    /// 2b-iii(b): ArcadeGameConfigureModal re-targets an existing preview player via
    /// <c>_previewVideo.clip = game.PreviewClip.clip</c>). Reference-only, like
    /// <see cref="Sprite"/> — decode/playback arrive with the presentation phase.
    /// </summary>
    public class VideoClip : Object
    {
    }

    /// <summary>
    /// Stand-in for <c>UnityEngine.Video.VideoPlayer</c> (engine addition for SA1:
    /// SO_Game / SO_VesselAbility hold preview-clip references). Headless-first —
    /// playback semantics arrive with the presentation phase; until then the reference
    /// is the asset, exactly like the <see cref="Sprite"/> stub.
    /// </summary>
    public class VideoPlayer : Behaviour
    {
        public VideoClip clip { get; set; }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.UI
{
    /// <summary>
    /// A looping clip in the launch panel's frame — the Maelstrom's stand-in for the live preview
    /// window.
    ///
    /// <para><b>This is not the return of the video fallback.</b> Every playable mode previews
    /// live (<c>Docs/ModePreview/ARCHITECTURE.md</c>) and the fallback branches were deleted
    /// precisely so nothing stale could draw in a preview frame. Maelstrom is the one card that is
    /// structurally unable to preview: it is a meta-mode that draws OTHER modes, so it has no
    /// arena of its own to stand up, and a video is the only honest thing to show. It draws in the
    /// Maelstrom panel's own frame, never in a <c>ModePreviewWindow</c>.</para>
    ///
    /// <para>The clip renders into a <see cref="RenderTexture"/> sized to the frame, so it never
    /// fights the layout, and the whole view switches off when the card authors no clip — an empty
    /// black rectangle reads as a broken preview.</para>
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class ModeVideoView : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Tooltip("The RawImage the clip renders into. Its rect decides the render " +
                                 "texture's size.")]
        RawImage surface;

        [SerializeField, Tooltip("Shown instead of the surface when the card authors no clip.")]
        GameObject unavailableLabel;

        [Header("Playback")]
        [SerializeField, Tooltip("Loop the clip. Off plays it once and holds the last frame.")]
        bool loop = true;

        [SerializeField, Tooltip("Play the clip's own audio. Off is the default - a menu panel " +
                                 "that talks over the menu music is not what anyone wants.")]
        bool playAudio;

        [SerializeField, Tooltip("Render-texture height in pixels. Width follows the frame's own " +
                                 "aspect, so a wide frame is not stretched.")]
        [Min(64)] int textureHeight = 360;

        VideoPlayer _player;
        RenderTexture _texture;
        VideoClip _clip;

        void Awake()
        {
            _player = GetComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = loop;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = playAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;
            if (!playAudio) _player.SetDirectAudioMute(0, true);
        }

        void OnDisable() => Stop();

        void OnDestroy() => ReleaseTexture();

        /// <summary>
        /// Show and play a clip. A null clip is an ordinary answer, not a fault: the view says so
        /// and plays nothing.
        /// </summary>
        public void Show(VideoClip clip)
        {
            _clip = clip;

            bool hasClip = clip;
            if (surface) surface.gameObject.SetActive(hasClip);
            if (unavailableLabel) unavailableLabel.SetActive(!hasClip);

            if (!hasClip)
            {
                Stop();
                return;
            }

            EnsureTexture();
            _player.clip = clip;
            _player.isLooping = loop;
            _player.targetTexture = _texture;
            _player.Play();
        }

        /// <summary>Stop playback and blank the frame. Safe to call repeatedly.</summary>
        public void Stop()
        {
            if (_player && _player.isPlaying) _player.Stop();
        }

        /// <summary>Resume the clip the view was last shown — used when the panel comes back.</summary>
        public void Resume()
        {
            if (_clip) Show(_clip);
        }

        void EnsureTexture()
        {
            int height = Mathf.Max(64, textureHeight);
            int width = height;

            if (surface)
            {
                var rect = surface.rectTransform.rect;
                if (rect.height > 1f)
                    width = Mathf.Max(64, Mathf.RoundToInt(height * (rect.width / rect.height)));
            }

            if (_texture && _texture.width == width && _texture.height == height) return;

            ReleaseTexture();
            _texture = new RenderTexture(width, height, 0, RenderTextureFormat.Default)
            {
                name = "ModeVideoView"
            };
            _texture.Create();
            if (surface) surface.texture = _texture;
        }

        void ReleaseTexture()
        {
            if (!_texture) return;
            if (surface && surface.texture == _texture) surface.texture = null;
            if (_player && _player.targetTexture == _texture) _player.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }
    }
}

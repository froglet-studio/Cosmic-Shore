using UnityEngine;
using UnityEngine.Video;

namespace CosmicShore.UI
{
    /// <summary>
    /// Plays the Ark screen's backdrop clips (<see cref="CosmicShore.ScriptableObjects.ArkBackdropConfigSO"/>) into one
    /// texture <see cref="ElementalConverterView"/> shows behind every tab. Plays on unscaled time: the menu is paused
    /// behind every screen but Home. The texture exists only while the Ark is the screen (<see cref="Release"/> on leaving).
    /// </summary>
    public sealed class ArkBackdrop : MonoBehaviour
    {
        VideoPlayer _player;
        RenderTexture _texture;
        VideoClip _clip;
        bool _finished;

        /// <summary>The texture the clips play into; null until the first <see cref="Show"/>.</summary>
        public Texture Texture => _texture;

        /// <summary>True once a clip shown with loop off has played to its end.</summary>
        public bool Finished => _finished;

        /// <summary>The clip playing (or last played).</summary>
        public VideoClip Current => _clip;

        /// <summary>How far through its loop the clip is, 0..1 (the render's own loop time: the hangar clips' camera breathes
        /// on it, and SquadVesselLayer moves its camera with it); -1 while nothing plays.</summary>
        public float LoopPhase
        {
            get
            {
                if (!_player || !_clip || !_player.isPlaying || _clip.length <= 0.0) return -1f;
                return Mathf.Repeat((float)(_player.time / _clip.length), 1f);
            }
        }

        public static ArkBackdrop Create(Transform parent)
        {
            var go = new GameObject("ArkBackdrop");
            go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<ArkBackdrop>();
            var p = go.AddComponent<VideoPlayer>();
            p.playOnAwake = false;
            p.renderMode = VideoRenderMode.RenderTexture;
            p.audioOutputMode = VideoAudioOutputMode.None;
            p.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            p.skipOnDrop = true;
            p.waitForFirstFrame = true;
            p.loopPointReached += backdrop.OnEnd;
            backdrop._player = p;
            return backdrop;
        }

        /// <summary>Plays <paramref name="clip"/> (from its start when it is a new clip); a clip already playing carries on.</summary>
        public void Show(VideoClip clip, bool loop, Vector2Int size)
        {
            if (!clip || !_player) return;
            EnsureTexture(size);
            if (clip == _clip && _player.isPlaying && _player.isLooping == loop) return;
            _clip = clip;
            _finished = false;
            _player.isLooping = loop;
            _player.clip = clip;
            _player.time = 0;
            _player.Play();
        }

        /// <summary>Stops and lets the texture go (on leaving the Ark: at 1920 x 1080 it is 8 MB nothing shows).</summary>
        public void Release()
        {
            if (_player)
            {
                _player.Stop();
                _player.targetTexture = null;
            }
            _clip = null;
            _finished = false;
            if (!_texture) return;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        void EnsureTexture(Vector2Int size)
        {
            int w = Mathf.Max(64, size.x), h = Mathf.Max(64, size.y);
            if (_texture && _texture.width == w && _texture.height == h) return;
            if (_texture)
            {
                _texture.Release();
                Destroy(_texture);
            }
            _texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Ark backdrop" };
            _texture.Create();
            _player.targetTexture = _texture;
        }

        void OnEnd(VideoPlayer source)
        {
            if (!source.isLooping) _finished = true;
        }

        void OnDestroy()
        {
            if (_player) _player.loopPointReached -= OnEnd;
            Release();
        }
    }
}

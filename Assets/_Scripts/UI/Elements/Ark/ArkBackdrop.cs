using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Video;

namespace CosmicShore.UI
{
    /// <summary>How the Ark's picture goes from one clip to the next.</summary>
    public enum ArkPassKind
    {
        /// <summary>At once (arriving on the Ark: the screen switcher has its own transition).</summary>
        Cut = 0,
        /// <summary>A short cross-dissolve (the pier's runs: at rest, the conversion, the Omni made).</summary>
        Dissolve = 1,
        /// <summary>The HyperSea pass: through the Ark's pearl membrane into another room (Docs/MENU_REDESIGN_B.md
        /// section 197).</summary>
        Pass = 2,
    }

    /// <summary>
    /// Plays the Ark screen's backdrop clips (<see cref="CosmicShore.ScriptableObjects.ArkBackdropConfigSO"/>) behind every
    /// tab, and carries the picture from one clip to the next (Shombith, 2026-11-13: "make the transitions smoother and
    /// realistic as they belong in the hypersea"). Two players take turns: the room shown keeps playing while the next clip
    /// prepares on the other, so a change never shows a black or frozen frame, and once the next clip has a frame the
    /// <c>CosmicShore/UI/ArkPass</c> shader carries the picture across (<see cref="ArkPassKind"/>). Plays on unscaled time:
    /// the menu is paused behind every screen but Home. The textures exist only while the Ark is the screen
    /// (<see cref="Release"/> on leaving).
    /// </summary>
    public sealed class ArkBackdrop : MonoBehaviour
    {
        sealed class Slot
        {
            public VideoPlayer Player;
            public RenderTexture Texture;
            public VideoClip Clip;
            public bool Finished;
        }

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int ToTexId = Shader.PropertyToID("_ToTex");
        static readonly int PassId = Shader.PropertyToID("_Pass");
        static readonly int ModeId = Shader.PropertyToID("_Mode");
        static readonly int DirId = Shader.PropertyToID("_Dir");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int FromOnId = Shader.PropertyToID("_FromOn");
        static readonly int ToOnId = Shader.PropertyToID("_ToOn");
        static readonly int StyleId = Shader.PropertyToID("_Style");
        static readonly int OriginId = Shader.PropertyToID("_Origin");

        // A clip that has not shown a frame by then passes anyway (a slow decoder must not strand the old room).
        const float PrepareTimeout = 0.6f;

        readonly Slot[] _slots = { new(), new() };
        int _front;
        bool _frontDark = true;        // nothing shown yet, or the dark of a tab with no room (Pilots, Decor)
        bool _hasShown;                // a picture has been shown since the last Release: later changes pass
        Material _material;
        float _passSeconds = 0.5f, _dissolveSeconds = 0.3f;
        readonly List<ArkPassStyle> _styles = new();
        ArkPassStyle _style;
        Vector2 _origin = new(0.5f, 0.9f);
        float _clock;

        // The change under way: waiting for the next clip's first frame, then passing.
        bool _pending, _passing, _toDark;
        ArkPassKind _kind;
        float _dir = 1f, _progress, _waited;

        Slot Front => _slots[_front];
        Slot Back => _slots[1 - _front];

        /// <summary>The texture of the room shown (the room left, during a change); null while dark or before the first
        /// <see cref="Show"/>. The picture's RawImage shows this.</summary>
        public Texture Texture => _frontDark ? null : Front.Texture;

        /// <summary>The material the picture draws with (the pass shader), or null when there is no shader: the picture
        /// then changes at the end of the wait, as a cut.</summary>
        public Material Material => _material;

        /// <summary>True once a clip shown with loop off has played to its end.</summary>
        public bool Finished => !_frontDark && Front.Finished && !_pending && !_passing;

        /// <summary>The clip playing, or the one a change is going to.</summary>
        public VideoClip Current => _pending || _passing ? (_toDark ? null : Back.Clip) : (_frontDark ? null : Front.Clip);

        /// <summary>True while a change is under way (its clip preparing, or the pass itself).</summary>
        public bool Changing => _pending || _passing;

        /// <summary>True when the picture is dark and no change is going anywhere else: the picture can be hidden.</summary>
        public bool RestingDark => _frontDark && !_pending && !_passing;

        /// <summary>The pass the change under way (or the last one) uses.</summary>
        public ArkPassStyle Style => _style;

        public static ArkBackdrop Create(Transform parent, Shader passShader, float passSeconds, float dissolveSeconds,
                                         IReadOnlyList<ArkPassStyle> styles = null)
        {
            var go = new GameObject("ArkBackdrop");
            go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<ArkBackdrop>();
            // Two players: one VideoPlayer per GameObject.
            for (int i = 0; i < 2; i++)
            {
                var host = i == 0 ? go : new GameObject("ArkBackdrop B");
                if (i > 0) host.transform.SetParent(go.transform, false);
                var p = host.AddComponent<VideoPlayer>();
                p.playOnAwake = false;
                p.renderMode = VideoRenderMode.RenderTexture;
                p.audioOutputMode = VideoAudioOutputMode.None;
                p.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                p.skipOnDrop = true;
                p.waitForFirstFrame = true;
                p.loopPointReached += backdrop.OnEnd;
                backdrop._slots[i].Player = p;
            }
            backdrop._passSeconds = Mathf.Max(0.05f, passSeconds);
            backdrop._dissolveSeconds = Mathf.Max(0.05f, dissolveSeconds);
            if (styles != null) backdrop._styles.AddRange(styles);
            if (passShader && passShader.isSupported)
                backdrop._material = new Material(passShader) { name = "Ark pass (runtime)" };
            backdrop.Apply();
            return backdrop;
        }

        /// <summary>Shows <paramref name="clip"/>: at once the first time, then by <paramref name="kind"/> in direction
        /// <paramref name="dir"/> (+1: the next room is to the right), a ripple starting at <paramref name="origin"/> (0-1 across
        /// the picture: the tab pressed). A clip already shown carries on.</summary>
        public void Show(VideoClip clip, bool loop, Vector2Int size, ArkPassKind kind = ArkPassKind.Pass, int dir = 1,
                         Vector2? origin = null)
        {
            if (origin.HasValue) _origin = origin.Value;
            if (!clip || !Front.Player) return;
            EnsureTextures(size);
            if (!_pending && !_passing && !_frontDark && Front.Clip == clip && Front.Player.isPlaying &&
                Front.Player.isLooping == loop) return;
            if ((_pending || _passing) && !_toDark && Back.Clip == clip && Back.Player.isLooping == loop) return;

            if (!_hasShown || kind == ArkPassKind.Cut)
            {
                SettleChange();
                Play(Front, clip, loop);
                _frontDark = false;
                _hasShown = true;
                Apply();
                return;
            }

            // The next clip prepares on the other player while this room keeps playing. Without the shader the change
            // is still a cut, but at the next clip's first frame: never a black or frozen one.
            SettleChange();
            Play(Back, clip, loop);
            Begin(kind, dir, toDark: false);
        }

        /// <summary>Takes the picture into the dark (a tab with no room) by <paramref name="kind"/>; the view hides the
        /// picture once <see cref="RestingDark"/>.</summary>
        public void ShowDark(ArkPassKind kind = ArkPassKind.Pass, int dir = 1, Vector2? origin = null)
        {
            if (origin.HasValue) _origin = origin.Value;
            if (_frontDark && !_pending && !_passing) return;
            if ((_pending || _passing) && _toDark) return;
            SettleChange();
            if (kind == ArkPassKind.Cut || !_material || _frontDark)
            {
                Stop(Front);
                _frontDark = true;
                Apply();
                return;
            }
            Begin(kind, dir, toDark: true);
        }

        /// <summary>Stops and lets both textures go (on leaving the Ark: at 1920 x 1080 each is 8 MB nothing shows).</summary>
        public void Release()
        {
            _pending = _passing = false;
            foreach (var s in _slots)
            {
                Stop(s);
                if (s.Player) s.Player.targetTexture = null;
                if (!s.Texture) continue;
                s.Texture.Release();
                Destroy(s.Texture);
                s.Texture = null;
            }
            _frontDark = true;
            _hasShown = false;
            Apply();
        }

        void Begin(ArkPassKind kind, int dir, bool toDark)
        {
            _kind = kind == ArkPassKind.Cut ? ArkPassKind.Dissolve : kind;
            if (_kind == ArkPassKind.Pass) _style = PickStyle(_styles, Random.value);
            _dir = dir >= 0 ? 1f : -1f;
            _toDark = toDark;
            _progress = 0f;
            _waited = 0f;
            _pending = !toDark;
            _passing = toDark;
            Apply();
        }

        /// <summary>Ends a change under way where it is going, so the next one starts from what it reached.</summary>
        void SettleChange()
        {
            if (!_pending && !_passing) return;
            Swap();
        }

        void Swap()
        {
            _pending = _passing = false;
            Stop(Front);
            if (_toDark)
            {
                _frontDark = true;
            }
            else
            {
                _front = 1 - _front;
                _frontDark = false;
                _hasShown = true;
            }
            _progress = 0f;
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _clock += dt;
            if (_pending)
            {
                _waited += dt;
                // The next clip has a frame in its texture (or took too long): the pass can start.
                bool ready = Back.Player.isPlaying && Back.Player.frame > 0;
                if (ready || _waited >= PrepareTimeout)
                {
                    _pending = false;
                    if (_material) _passing = true;
                    else Swap();
                }
            }
            if (_passing)
            {
                float seconds = _kind == ArkPassKind.Pass ? _passSeconds : _dissolveSeconds;
                _progress += dt / seconds;
                if (_progress >= 1f) Swap();
            }
            Apply();
        }

        void Apply()
        {
            if (!_material) return;
            bool changing = _passing;
            var from = _frontDark ? null : Front.Texture;
            _material.SetTexture(MainTexId, from);
            _material.SetTexture(ToTexId, changing && !_toDark ? Back.Texture : null);
            _material.SetFloat(FromOnId, from ? 1f : 0f);
            _material.SetFloat(ToOnId, changing && !_toDark && Back.Texture ? 1f : 0f);
            _material.SetFloat(ModeId, changing ? (float)_kind : 0f);
            _material.SetFloat(PassId, changing ? Smootherstep(_progress) : 0f);
            _material.SetFloat(DirId, _dir);
            _material.SetFloat(StyleId, (float)_style);
            _material.SetVector(OriginId, new Vector4(_origin.x, _origin.y, 0f, 0f));
            _material.SetFloat(ClockId, _clock);
            var t = Front.Texture ? Front.Texture : Back.Texture;
            _material.SetFloat(AspectId, t ? (float)t.width / Mathf.Max(1, t.height) : 16f / 9f);
        }

        /// <summary>The pass for one change: one of <paramref name="styles"/> by <paramref name="roll"/> (0-1), the membrane when
        /// there are none.</summary>
        public static ArkPassStyle PickStyle(IReadOnlyList<ArkPassStyle> styles, float roll)
        {
            if (styles == null || styles.Count == 0) return ArkPassStyle.Membrane;
            int i = Mathf.Clamp(Mathf.FloorToInt(roll * styles.Count), 0, styles.Count - 1);
            return styles[i];
        }

        /// <summary>Ken Perlin's smootherstep: still at both ends, so the glide starts and lands without a jolt.</summary>
        public static float Smootherstep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        static void Play(Slot slot, VideoClip clip, bool loop)
        {
            slot.Clip = clip;
            slot.Finished = false;
            slot.Player.isLooping = loop;
            slot.Player.clip = clip;
            slot.Player.time = 0;
            slot.Player.Play();
        }

        static void Stop(Slot slot)
        {
            if (slot.Player) slot.Player.Stop();
            slot.Clip = null;
            slot.Finished = false;
        }

        void EnsureTextures(Vector2Int size)
        {
            int w = Mathf.Max(64, size.x), h = Mathf.Max(64, size.y);
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s.Texture && s.Texture.width == w && s.Texture.height == h) continue;
                if (s.Texture)
                {
                    s.Texture.Release();
                    Destroy(s.Texture);
                }
                s.Texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = i == 0 ? "Ark backdrop A" : "Ark backdrop B" };
                s.Texture.Create();
                s.Player.targetTexture = s.Texture;
            }
        }

        void OnEnd(VideoPlayer source)
        {
            if (source.isLooping) return;
            foreach (var s in _slots)
                if (s.Player == source) s.Finished = true;
        }

        void OnDestroy()
        {
            foreach (var s in _slots)
                if (s.Player) s.Player.loopPointReached -= OnEnd;
            Release();
            if (_material) Destroy(_material);
        }
    }
}

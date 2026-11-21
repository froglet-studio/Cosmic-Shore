using System;

namespace CosmicShore.Engine.Video
{
    /// <summary>
    /// <c>UnityEngine.Video.VideoClip</c>: a reference to an imported movie. The content
    /// bridge fills <see cref="originalPath"/>, <see cref="length"/> and the frame metrics
    /// from the asset's import settings when it knows them.
    /// </summary>
    public class VideoClip : Object
    {
        public string originalPath { get; set; }
        public double length { get; set; }
        public ulong frameCount { get; set; }
        public double frameRate { get; set; } = 30.0;
        public uint width { get; set; }
        public uint height { get; set; }
        public ushort audioTrackCount { get; set; }
    }

    /// <summary>
    /// <c>UnityEngine.Video.VideoPlayer</c>. Owns the playback CLOCK exactly as the
    /// original reports it — <see cref="Prepare"/>, <see cref="Play"/>/<see cref="Pause"/>/
    /// <see cref="Stop"/>, <see cref="time"/>, <see cref="frame"/>, looping and the
    /// <see cref="loopPointReached"/>/<see cref="started"/>/<see cref="prepareCompleted"/>
    /// events — so UI that sequences on a clip's progress behaves identically. Pixels come
    /// from <see cref="FrameDecoder"/> when a platform decoder is installed; without one the
    /// target texture keeps its contents (the "LEVEL PREVIEW NOT AVAILABLE" path the menu
    /// already handles).
    /// </summary>
    public class VideoPlayer : Behaviour
    {
        public delegate void EventHandler(VideoPlayer source);
        public delegate void ErrorEventHandler(VideoPlayer source, string message);
        public delegate void FrameReadyEventHandler(VideoPlayer source, long frameIdx);

        /// <summary>Installed by a platform decoder: renders the frame at <c>time</c> into the player's target.</summary>
        public static Action<VideoPlayer, double> FrameDecoder;

        VideoClip _clip;
        double _time;
        double _lastTick = -1;
        bool _prepared;

        public VideoClip clip { get => _clip; set { _clip = value; _prepared = false; _time = 0; } }
        public string url { get; set; }
        public VideoSource source { get; set; } = VideoSource.VideoClip;
        public RenderTexture targetTexture { get; set; }
        public Camera targetCamera { get; set; }
        public Renderer targetMaterialRenderer { get; set; }
        public string targetMaterialProperty { get; set; } = "_MainTex";
        public float targetCameraAlpha { get; set; } = 1f;
        public VideoRenderMode renderMode { get; set; } = VideoRenderMode.RenderTexture;
        public VideoAspectRatio aspectRatio { get; set; } = VideoAspectRatio.FitVertically;
        public VideoAudioOutputMode audioOutputMode { get; set; } = VideoAudioOutputMode.Direct;
        public VideoTimeReference timeReference { get; set; } = VideoTimeReference.Freerun;
        public bool isLooping { get; set; }
        public bool playOnAwake { get; set; } = true;
        public bool waitForFirstFrame { get; set; } = true;
        public bool skipOnDrop { get; set; } = true;
        public bool sendFrameReadyEvents { get; set; }
        public float playbackSpeed { get; set; } = 1f;
        public bool isPlaying { get; private set; }
        public bool isPaused { get; private set; }
        public bool isPrepared => _prepared;
        public bool canSetTime => true;
        public ushort audioTrackCount => _clip?.audioTrackCount ?? 0;
        public ushort controlledAudioTrackCount { get; set; }
        public Texture texture => targetTexture;
        public double length => _clip?.length ?? 0;
        public double frameRate => _clip?.frameRate ?? 30.0;
        public ulong frameCount => _clip?.frameCount ?? (ulong)Math.Round(length * frameRate);
        public uint width => _clip?.width ?? 0;
        public uint height => _clip?.height ?? 0;

        public double time
        {
            get { Tick(); return _time; }
            set { _time = Math.Max(0, value); _lastTick = Time.unscaledTimeAsDouble; }
        }

        public long frame
        {
            get => (long)Math.Floor(time * frameRate);
            set => time = value / frameRate;
        }

        bool[] _mutes = new bool[1];

        public event EventHandler prepareCompleted;
        public event EventHandler started;
        public event EventHandler loopPointReached;
        public event EventHandler seekCompleted;
        public event ErrorEventHandler errorReceived;
        public event FrameReadyEventHandler frameReady;

        protected virtual void OnEnable()
        {
            if (playOnAwake) Play();
        }

        protected virtual void OnDisable() => Stop();

        public void Prepare()
        {
            if (_prepared) return;
            if (_clip == null && string.IsNullOrEmpty(url))
            {
                errorReceived?.Invoke(this, "VideoPlayer cannot play: no clip or URL assigned.");
                return;
            }
            _prepared = true;
            prepareCompleted?.Invoke(this);
        }

        public void Play()
        {
            Prepare();
            if (!_prepared) return;
            if (isPlaying) return;
            isPlaying = true;
            isPaused = false;
            _lastTick = Time.unscaledTimeAsDouble;
            started?.Invoke(this);
            Present();
        }

        public void Pause() { Tick(); isPlaying = false; isPaused = true; }

        public void Stop()
        {
            isPlaying = false;
            isPaused = false;
            _time = 0;
            _lastTick = -1;
        }

        public void StepForward() { time += 1.0 / frameRate; Present(); }

        public void SetDirectAudioMute(ushort trackIndex, bool mute)
        {
            if (trackIndex >= _mutes.Length) Array.Resize(ref _mutes, trackIndex + 1);
            _mutes[trackIndex] = mute;
        }

        public bool GetDirectAudioMute(ushort trackIndex) => trackIndex < _mutes.Length && _mutes[trackIndex];
        public void SetDirectAudioVolume(ushort trackIndex, float volume) { }
        public float GetDirectAudioVolume(ushort trackIndex) => 1f;
        public void EnableAudioTrack(ushort trackIndex, bool enabled) { }
        public bool IsAudioTrackEnabled(ushort trackIndex) => true;

        /// <summary>Advances the clock to "now"; the renderer calls this once per presented frame.</summary>
        public void Tick()
        {
            if (!isPlaying) return;
            double now = Time.unscaledTimeAsDouble;
            if (_lastTick < 0) _lastTick = now;
            _time += (now - _lastTick) * playbackSpeed;
            _lastTick = now;

            double len = length;
            if (len > 0 && _time >= len)
            {
                loopPointReached?.Invoke(this);
                if (isLooping) _time %= len;
                else { _time = len; isPlaying = false; }
            }
            Present();
        }

        void Present()
        {
            FrameDecoder?.Invoke(this, _time);
            if (sendFrameReadyEvents) frameReady?.Invoke(this, frame);
        }
    }
}

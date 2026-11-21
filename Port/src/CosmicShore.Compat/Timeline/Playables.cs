using System;
using System.Collections.Generic;

// UnityEngine.Playables (the live-src sync maps it to CosmicShore.Engine.Playables).
namespace CosmicShore.Engine.Playables
{
    public enum PlayState
    {
        Paused = 0,
        Playing = 1,
        Delayed = 2,
    }

    public enum DirectorWrapMode
    {
        Hold = 0,
        Loop = 1,
        None = 2,
    }

    public enum DirectorUpdateMode
    {
        DSPClock = 0,
        GameTime = 1,
        UnscaledGameTime = 2,
        Manual = 3,
    }

    /// <summary>A playable graph asset. The port evaluates no graphs; <see cref="duration"/> is data.</summary>
    public abstract class PlayableAsset : ScriptableObject
    {
        public virtual double duration { get; set; }
    }

    /// <summary>
    /// Drives a <see cref="PlayableAsset"/>. The port has no playable graph, so nothing is
    /// animated — but the director is an honest state machine: <see cref="Play()"/>,
    /// <see cref="Pause"/>, <see cref="Resume"/> and <see cref="Stop"/> move <see cref="state"/>
    /// and raise <see cref="played"/>/<see cref="paused"/>/<see cref="stopped"/> exactly when the
    /// original does, generic bindings read back, and <see cref="time"/> is plain data (advanced
    /// only by whoever sets it — there is no player-loop driver).
    /// </summary>
    public class PlayableDirector : Behaviour
    {
        readonly Dictionary<Object, Object> _bindings = new();

        public PlayableAsset playableAsset { get; set; }
        public PlayState state { get; private set; } = PlayState.Paused;
        public DirectorWrapMode extrapolationMode { get; set; } = DirectorWrapMode.Hold;
        public DirectorUpdateMode timeUpdateMode { get; set; } = DirectorUpdateMode.GameTime;
        public bool playOnAwake { get; set; } = true;
        public double time { get; set; }
        public double initialTime { get; set; }
        public double duration => playableAsset != null ? playableAsset.duration : 0d;

        public event Action<PlayableDirector> played;
        public event Action<PlayableDirector> paused;
        public event Action<PlayableDirector> stopped;

        public void Play()
        {
            state = PlayState.Playing;
            played?.Invoke(this);
        }

        public void Play(PlayableAsset asset)
        {
            playableAsset = asset;
            Play();
        }

        public void Play(PlayableAsset asset, DirectorWrapMode mode)
        {
            extrapolationMode = mode;
            Play(asset);
        }

        public void Pause()
        {
            if (state != PlayState.Playing) return;
            state = PlayState.Paused;
            paused?.Invoke(this);
        }

        public void Resume()
        {
            if (state == PlayState.Playing) return;
            state = PlayState.Playing;
            played?.Invoke(this);
        }

        public void Stop()
        {
            time = initialTime;
            state = PlayState.Paused;
            stopped?.Invoke(this);
        }

        public void Evaluate() { }
        public void RebuildGraph() { }
        public void DeferredEvaluate() { }

        public void SetGenericBinding(Object key, Object value)
        {
            if (key is null) return;
            if (value is null) _bindings.Remove(key);
            else _bindings[key] = value;
        }

        public Object GetGenericBinding(Object key) => key is not null && _bindings.TryGetValue(key, out var v) ? v : null;
        public void ClearGenericBinding(Object key) { if (key is not null) _bindings.Remove(key); }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine.Playables;

// UnityEngine.Timeline (the live-src sync maps it to CosmicShore.Engine.Timeline).
namespace CosmicShore.Engine.Timeline
{
    /// <summary>A clip on a <see cref="TrackAsset"/>: a time range plus the asset it plays (data only).</summary>
    public class TimelineClip
    {
        public string displayName;
        public double start;
        public double duration;
        public double clipIn;
        public double timeScale = 1d;
        public Object asset;

        public double end => start + duration;
        public TrackAsset parentTrack { get; internal set; }
    }

    /// <summary>A track of clips. Tracks own their clips; nothing evaluates them in the port.</summary>
    public abstract class TrackAsset : PlayableAsset
    {
        readonly List<TimelineClip> _clips = new();
        readonly List<TrackAsset> _children = new();

        public bool muted { get; set; }
        public bool locked { get; set; }
        public TimelineAsset timelineAsset { get; internal set; }
        public PlayableAsset parent { get; internal set; }

        public override double duration
        {
            get => _clips.Count == 0 ? 0d : _clips.Max(c => c.end);
            set { }
        }

        public IEnumerable<TimelineClip> GetClips() => _clips;
        public IEnumerable<TrackAsset> GetChildTracks() => _children;
        public bool hasClips => _clips.Count > 0;

        public TimelineClip CreateDefaultClip()
        {
            var clip = new TimelineClip { displayName = GetType().Name, start = duration, duration = 5d, parentTrack = this };
            _clips.Add(clip);
            return clip;
        }

        /// <summary>Adds a clip playing <paramref name="asset"/> at the end of the track.</summary>
        public TimelineClip CreateClip(Object asset)
        {
            var clip = CreateDefaultClip();
            clip.asset = asset;
            if (asset != null) clip.displayName = asset.name;
            return clip;
        }

        public bool DeleteClip(TimelineClip clip) => _clips.Remove(clip);

        internal void AddChild(TrackAsset child) => _children.Add(child);
    }

    /// <summary>
    /// A timeline: an ordered set of tracks (data only — the port evaluates no playable graph).
    /// </summary>
    public class TimelineAsset : PlayableAsset
    {
        public enum DurationMode
        {
            BasedOnClips = 0,
            FixedLength = 1,
        }

        readonly List<TrackAsset> _tracks = new();

        public DurationMode durationMode { get; set; } = DurationMode.BasedOnClips;
        public double fixedDuration { get; set; }

        public override double duration
        {
            get => durationMode == DurationMode.FixedLength ? fixedDuration : (_tracks.Count == 0 ? 0d : _tracks.Max(t => t.duration));
            set => fixedDuration = value;
        }

        public int outputTrackCount => _tracks.Count;
        public int rootTrackCount => _tracks.Count(t => t.parent == this || t.parent == null);

        public IEnumerable<TrackAsset> GetOutputTracks() => _tracks;
        public IEnumerable<TrackAsset> GetRootTracks() => _tracks.Where(t => t.parent == this || t.parent == null);
        public TrackAsset GetOutputTrack(int index) => _tracks[index];

        public T CreateTrack<T>(string name) where T : TrackAsset, new() => CreateTrack<T>(null, name);

        public T CreateTrack<T>(TrackAsset parent, string name) where T : TrackAsset, new()
        {
            var track = new T { name = name };
            track.timelineAsset = this;
            track.parent = (PlayableAsset)parent ?? this;
            parent?.AddChild(track);
            _tracks.Add(track);
            return track;
        }

        public TrackAsset CreateTrack(Type type, TrackAsset parent, string name)
        {
            var track = (TrackAsset)Activator.CreateInstance(type, nonPublic: true);
            track.name = name;
            track.timelineAsset = this;
            track.parent = (PlayableAsset)parent ?? this;
            parent?.AddChild(track);
            _tracks.Add(track);
            return track;
        }

        public bool DeleteTrack(TrackAsset track) => _tracks.Remove(track);
    }

    /// <summary>Animates a bound Animator (inert: the port has no animation playback).</summary>
    public class AnimationTrack : TrackAsset { }

    /// <summary>Toggles a bound GameObject active over its clips (inert).</summary>
    public class ActivationTrack : TrackAsset { }

    /// <summary>Plays audio clips (inert).</summary>
    public class AudioTrack : TrackAsset { }

    /// <summary>Emits signals (inert).</summary>
    public class SignalTrack : TrackAsset { }

    /// <summary>Controls nested directors / particle systems (inert).</summary>
    public class ControlTrack : TrackAsset { }

    /// <summary>A track group (holds child tracks only).</summary>
    public class GroupTrack : TrackAsset { }
}

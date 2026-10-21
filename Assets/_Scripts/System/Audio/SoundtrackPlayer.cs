using System;
using System.Collections.Generic;
using CosmicShore.Gameplay.Audio;
using CosmicShore.ScriptableObjects;
using FMOD.Studio;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The music the player chose (Docs/MENU_REDESIGN_B.md §56): which tracks are in the rotation, shuffle, and the
    /// transport (play a track now, back, pause, skip, stop). Owned by <see cref="AudioSystem"/>, which ticks it.
    /// <para>
    /// Two modes. PLAYLIST: the soundtrack has per-track events, so this plays the ticked tracks one after another and
    /// knows which one is playing. LEGACY: no track has an event yet (today), so the old random music event keeps
    /// playing through <see cref="ILegacyMusic"/>; back / skip restart it (it picks again), pause and stop work, and
    /// no track can be picked. It can still be NAMED: AudioSystem reports each sound the event starts
    /// (<see cref="NoteLegacySound"/>), matched to a track by its authored <c>soundName</c>.
    /// </para>
    /// The ticks and shuffle are saved on this device (PlayerPrefs). Stop lasts for the session.
    /// </summary>
    public sealed class SoundtrackPlayer
    {
        public interface ILegacyMusic
        {
            void SetLegacyMusicPaused(bool paused);
            void RestartLegacyMusic();
            void StopLegacyMusic();
            float MusicInstanceVolume { get; }
        }

        const string OffKey = "Soundtrack.Off";
        const string ShuffleKey = "Soundtrack.Shuffle";

        readonly SoundtrackSO _soundtrack;
        readonly ILegacyMusic _legacy;
        readonly UnityEngine.Object _context;
        readonly HashSet<string> _off = new();
        readonly List<int> _history = new();
        EventInstance _instance;
        bool _started;

        /// <summary>Raised when the playing track, the ticks, shuffle, pause or stop change.</summary>
        public event Action Changed;

        public SoundtrackPlayer(SoundtrackSO soundtrack, ILegacyMusic legacy, UnityEngine.Object context)
        {
            _soundtrack = soundtrack;
            _legacy = legacy;
            _context = context;
            Shuffle = PlayerPrefs.GetInt(ShuffleKey, 1) == 1;
            string saved = PlayerPrefs.GetString(OffKey, null);
            if (saved == null)
            {
                for (int i = 0; i < TrackCount; i++)
                    if (!_soundtrack.Tracks[i].onByDefault) _off.Add(_soundtrack.Tracks[i].id);
            }
            else
            {
                foreach (var id in saved.Split('|'))
                    if (!string.IsNullOrEmpty(id)) _off.Add(id);
            }
            EnsureOneOn();
        }

        public int TrackCount => _soundtrack ? _soundtrack.Tracks.Count : 0;
        /// <summary>Tracks can be picked and named (some track has its own event).</summary>
        public bool CanPickTracks => _soundtrack && _soundtrack.AnyTrackPlayable;
        /// <summary>The playing track, or -1 when none is known (legacy mode, or stopped).</summary>
        public int Current { get; private set; } = -1;
        /// <summary>The track the legacy random event is playing, as FMOD reported it, or -1.</summary>
        public int Heard { get; private set; } = -1;
        /// <summary>The track to name on screen: the picked one, else the one the random event is heard playing.</summary>
        public int NowPlaying => Stopped ? -1 : Current >= 0 ? Current : CanPickTracks ? -1 : Heard;
        public bool Paused { get; private set; }
        public bool Stopped { get; private set; }
        public bool Shuffle { get; private set; }

        public string Title(int i) => Valid(i) ? _soundtrack.Tracks[i].title : string.Empty;

        public string Artist(int i) =>
            !Valid(i) ? string.Empty : string.IsNullOrEmpty(_soundtrack.Tracks[i].artist) ? _soundtrack.ArtistLine : _soundtrack.Tracks[i].artist;

        public string ArtistLine => _soundtrack ? _soundtrack.ArtistLine : string.Empty;

        public bool IsOn(int i) => Valid(i) && !_off.Contains(_soundtrack.Tracks[i].id);

        public int OnCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < TrackCount; i++) if (IsOn(i)) n++;
                return n;
            }
        }

        public bool Playable(int i) => Valid(i) && !_soundtrack.Tracks[i].trackEvent.IsNull;

        /// <summary>0-1 through the playing track, or -1 when unknown.</summary>
        public float Progress
        {
            get
            {
                if (!_started || !_instance.isValid()) return -1f;
                if (_instance.getDescription(out var desc) != FMOD.RESULT.OK || desc.getLength(out int length) != FMOD.RESULT.OK || length <= 0)
                    return -1f;
                _instance.getTimelinePosition(out int pos);
                return Mathf.Clamp01(pos / (float)length);
            }
        }

        // ── Ticks and shuffle ───────────────────────────────────

        /// <summary>Puts a track in or out of the rotation. The last ticked track stays ticked; unticking the playing
        /// track moves on.</summary>
        public void SetOn(int i, bool on)
        {
            if (!Valid(i)) return;
            if (!on && IsOn(i) && OnCount <= 1) return;
            string id = _soundtrack.Tracks[i].id;
            if (on) _off.Remove(id); else _off.Add(id);
            SavePrefs();
            if (!on && i == Current && !Stopped) Next();
            else Changed?.Invoke();
        }

        public void SetShuffle(bool shuffle)
        {
            Shuffle = shuffle;
            SavePrefs();
            Changed?.Invoke();
        }

        // ── Transport ───────────────────────────────────────────

        /// <summary>Starts the music at launch (playlist mode); legacy mode leaves AudioSystem's event alone.</summary>
        public void Begin()
        {
            if (CanPickTracks) Next();
        }

        /// <summary>Plays a track now (playlist mode only) and ticks it if it was off.</summary>
        public void PlayTrack(int i)
        {
            if (!Playable(i)) return;
            if (!IsOn(i)) { _off.Remove(_soundtrack.Tracks[i].id); SavePrefs(); }
            StartTrack(i, true);
        }

        public void Next()
        {
            if (!CanPickTracks)
            {
                Stopped = Paused = false;
                Heard = -1;
                _legacy?.RestartLegacyMusic();
                Changed?.Invoke();
                return;
            }
            int next = PickNext();
            if (next < 0) { Stop(); return; }
            StartTrack(next, true);
        }

        public void Previous()
        {
            if (!CanPickTracks) { Next(); return; }
            // The history ends with the playing track; step back to the one before it.
            if (_history.Count >= 2)
            {
                _history.RemoveAt(_history.Count - 1);
                int back = _history[^1];
                _history.RemoveAt(_history.Count - 1);
                StartTrack(back, true);
            }
            else if (Current >= 0) StartTrack(Current, false);
        }

        public void TogglePause()
        {
            if (Stopped) { Resume(); return; }
            Paused = !Paused;
            if (CanPickTracks) { if (_instance.isValid()) _instance.setPaused(Paused); }
            else _legacy?.SetLegacyMusicPaused(Paused);
            Changed?.Invoke();
        }

        public void Stop()
        {
            Stopped = true;
            Paused = false;
            if (CanPickTracks) Release();
            else _legacy?.StopLegacyMusic();
            Current = -1;
            Heard = -1;
            Changed?.Invoke();
        }

        public void Resume()
        {
            Stopped = false;
            Next();
        }

        /// <summary>Called every frame by AudioSystem: moves on when a track ends.</summary>
        public void Tick()
        {
            if (!_started || Stopped || Paused || !_instance.isValid()) return;
            if (_instance.getPlaybackState(out var state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                Next();
        }

        /// <summary>The legacy random event started <paramref name="soundName"/> (main thread). Names the track whose
        /// authored soundName matches, ignoring case and extension; an unknown sound leaves the title generic.</summary>
        public void NoteLegacySound(string soundName)
        {
            int heard = -1;
            string key = SoundKey(soundName);
            for (int i = 0; i < TrackCount && key.Length > 0; i++)
                if (Valid(i) && SoundKey(_soundtrack.Tracks[i].soundName) == key) { heard = i; break; }
            if (heard == Heard) return;
            Heard = heard;
            Changed?.Invoke();
        }

        /// <summary>"SPACE GLIDING.ogg" and "space gliding" are the same sound.</summary>
        public static string SoundKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();
            int dot = name.LastIndexOf('.');
            if (dot > 0 && name.Length - dot <= 5) name = name.Substring(0, dot);
            return name.Trim().ToLowerInvariant();
        }

        public void ApplyVolume()
        {
            if (_instance.isValid() && _legacy != null) _instance.setVolume(_legacy.MusicInstanceVolume);
        }

        public void Dispose() => Release();

        // ── Internals ───────────────────────────────────────────

        void StartTrack(int i, bool record)
        {
            Release();
            Stopped = Paused = false;
            if (!FmodSafe.TryCreateInstance(_soundtrack.Tracks[i].trackEvent, out _instance, _context))
            {
                Current = -1;
                Changed?.Invoke();
                return;
            }
            ApplyVolume();
            _started = _instance.start() == FMOD.RESULT.OK;
            Current = _started ? i : -1;
            if (_started && record)
            {
                _history.Add(i);
                if (_history.Count > 32) _history.RemoveAt(0);
            }
            Changed?.Invoke();
        }

        void Release()
        {
            FmodSafe.StopAndRelease(ref _instance, _started, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _started = false;
        }

        int PickNext()
        {
            var pool = new List<int>();
            for (int i = 0; i < TrackCount; i++)
                if (IsOn(i) && Playable(i)) pool.Add(i);
            if (pool.Count == 0) return -1;
            if (Shuffle)
            {
                if (pool.Count > 1) pool.Remove(Current);
                return pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            foreach (int i in pool)
                if (i > Current) return i;
            return pool[0];
        }

        void EnsureOneOn()
        {
            if (TrackCount > 0 && OnCount == 0) _off.Remove(_soundtrack.Tracks[0].id);
        }

        void SavePrefs()
        {
            PlayerPrefs.SetString(OffKey, string.Join("|", _off));
            PlayerPrefs.SetInt(ShuffleKey, Shuffle ? 1 : 0);
        }

        bool Valid(int i) => _soundtrack && i >= 0 && i < _soundtrack.Tracks.Count && _soundtrack.Tracks[i] != null;
    }
}

using System;
using System.Globalization;
using CosmicShore.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Audio Studio - music only (Docs/MENU_REDESIGN_B.md §56; the owner: "No mixer just the music"). Now playing
    /// with back / pause / skip, shuffle and stop; every track as a row: press it to play it now, Y (or its tick box) to
    /// put it in or out of the rotation. Drives <see cref="SoundtrackPlayer"/> (AudioSystem.Soundtrack).
    /// While the soundtrack has no per-track events (the FMOD music event plays its tracks at random), rows cannot be
    /// played or named as playing; the notice says so, and back / skip / pause / stop still work.
    /// </summary>
    public class AudioStudioPanel : MonoBehaviour
    {
        [Serializable]
        public struct TrackRow
        {
            public CanvasGroup group;
            [Tooltip("The row: pressing it plays the track now.")]
            public KitTile tile;
            public TMP_Text titleText;
            [Tooltip("The tick box (mouse / touch); Y on the selected row does the same.")]
            public Button tickButton;
            public GameObject tickMark;
            public GameObject playingMark;
        }

        [Header("Now playing")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text artistText;
        [SerializeField] RectTransform progressFill;
        [SerializeField] GameObject progressTrack;
        [SerializeField] Button backButton;
        [SerializeField] Button pauseButton;
        [SerializeField] TMP_Text pauseLabel;
        [SerializeField] Button skipButton;
        [SerializeField] Button shuffleButton;
        [SerializeField] TMP_Text shuffleLabel;
        [SerializeField] Button stopButton;
        [SerializeField] TMP_Text stopLabel;

        [Header("Tracks")]
        [SerializeField] TrackRow[] rows = new TrackRow[9];
        [SerializeField] TMP_Text countText;
        [Tooltip("Shown while tracks cannot be picked yet.")]
        [SerializeField] GameObject randomNotice;
        [SerializeField] CanvasGroup visibleGroup;

        [Header("Words (empty shows nothing)")]
        [SerializeField] string countTemplate = "{on} of {total} in rotation";
        [SerializeField] string pauseWord = "Pause";
        [SerializeField] string resumeWord = "Play";
        [SerializeField] string shuffleOnWord = "Shuffle: on";
        [SerializeField] string shuffleOffWord = "Shuffle: off";
        [SerializeField] string stopWord = "Stop music";
        [SerializeField] string startWord = "Start music";
        [Tooltip("The title while the playing track is not known (the random music event).")]
        [SerializeField] string unknownTrackTitle = "Cosmic Shore soundtrack";
        [SerializeField] string stoppedTitle = "Music stopped";

        SoundtrackPlayer _player;

        SoundtrackPlayer Music => AudioSystem.Instance ? AudioSystem.Instance.Soundtrack : null;

        void Awake()
        {
            if (backButton) backButton.onClick.AddListener(() => Music?.Previous());
            if (pauseButton) pauseButton.onClick.AddListener(() => Music?.TogglePause());
            if (skipButton) skipButton.onClick.AddListener(() => Music?.Next());
            if (shuffleButton) shuffleButton.onClick.AddListener(() => { var p = Music; p?.SetShuffle(!p.Shuffle); });
            if (stopButton) stopButton.onClick.AddListener(() => { var p = Music; if (p == null) return; if (p.Stopped) p.Resume(); else p.Stop(); });
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i].tile) rows[i].tile.onClick.AddListener(() => Music?.PlayTrack(index));
                if (rows[i].tickButton) rows[i].tickButton.onClick.AddListener(() => ToggleTick(index));
            }
        }

        void OnEnable() => Bind();

        void OnDisable()
        {
            if (_player != null) _player.Changed -= Draw;
            _player = null;
        }

        void Bind()
        {
            var p = Music;
            if (p == _player) return;
            if (_player != null) _player.Changed -= Draw;
            _player = p;
            if (_player != null) _player.Changed += Draw;
            Draw();
        }

        void Update()
        {
            if (visibleGroup && visibleGroup.alpha <= 0f) return;
            if (_player == null) { Bind(); if (_player == null) return; }

            float progress = _player.Progress;
            if (progressTrack) progressTrack.SetActive(progress >= 0f);
            if (progressFill && progress >= 0f) progressFill.anchorMax = new Vector2(progress, progressFill.anchorMax.y);

            // Y on the selected row ticks / unticks it.
            var pad = Gamepad.current;
            var keys = Keyboard.current;
            bool y = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (keys != null && keys.tKey.wasPressedThisFrame);
            if (!y) return;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].tile && rows[i].tile.gameObject == selected) { ToggleTick(i); break; }
        }

        void ToggleTick(int i)
        {
            var p = Music;
            if (p != null) p.SetOn(i, !p.IsOn(i));
        }

        void Draw()
        {
            var p = _player;
            if (p == null) return;
            bool known = p.Current >= 0;
            Set(titleText, p.Stopped ? stoppedTitle : known ? p.Title(p.Current) : unknownTrackTitle);
            Set(artistText, known ? p.Artist(p.Current) : p.ArtistLine);
            Set(pauseLabel, p.Paused || p.Stopped ? resumeWord : pauseWord);
            Set(shuffleLabel, p.Shuffle ? shuffleOnWord : shuffleOffWord);
            Set(stopLabel, p.Stopped ? startWord : stopWord);
            if (randomNotice) randomNotice.SetActive(!p.CanPickTracks);
            Set(countText, TemplateText.Fill(countTemplate, "on", p.OnCount.ToString(CultureInfo.InvariantCulture),
                "total", p.TrackCount.ToString(CultureInfo.InvariantCulture)));

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                bool used = i < p.TrackCount;
                KitVisibility.SetShown(r.group, used);
                if (!used) continue;
                bool on = p.IsOn(i);
                Set(r.titleText, p.Title(i));
                if (r.tickMark) r.tickMark.SetActive(on);
                if (r.playingMark) r.playingMark.SetActive(i == p.Current && !p.Stopped);
                if (r.tile)
                {
                    r.tile.IsSelected = i == p.Current && !p.Stopped;
                    r.tile.IsLocked = !p.Playable(i);
                }
            }
        }

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s ?? string.Empty;
        }
    }
}

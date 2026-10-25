using CosmicShore.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The now-playing card, bottom right of Menu_Main (Docs/MENU_REDESIGN_B.md §56, the R10NowPlaying board, like EA
    /// SPORTS FC's). It shows only on the screens listed in <see cref="restingOn"/> (Home since Rev 13, and only while no window is
    /// open over it: the Profile is the pilot record now, whose Audio Studio carries the same controls) and hides while the pilot record is open (its Audio Studio carries the same controls). With
    /// <see cref="toastOffScreens"/> on, it would also slide in elsewhere for <see cref="toastSeconds"/> when a track
    /// starts; it ships off. Hovered, it shows Back / Pause / Skip. Hidden while the music is off in Settings or stopped.
    /// </summary>
    public class NowPlayingCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text artistText;
        [SerializeField] RectTransform progressFill;
        [SerializeField] GameObject progressTrack;
        [Tooltip("Back / Pause / Skip, shown while hovered.")]
        [SerializeField] CanvasGroup controls;
        [SerializeField] Button backButton;
        [SerializeField] Button pauseButton;
        [SerializeField] TMP_Text pauseLabel;
        [SerializeField] Button skipButton;

        [Header("Behaviour")]
        [Tooltip("Menu screens the card shows on (Home since Rev 13; the Profile carries the Audio Studio itself).")]
        [SerializeField] ScreenSwitcher.MenuScreens[] restingOn = { ScreenSwitcher.MenuScreens.HOME };
        [Tooltip("Off: the card shows only on the resting screens. On: elsewhere it also slides in for a few seconds when a track starts.")]
        [SerializeField] bool toastOffScreens;
        [SerializeField, Min(0.5f)] float toastSeconds = 4f;
        [Tooltip("How far right the card slides to hide (canvas units).")]
        [SerializeField] float slideDistance = 520f;
        [SerializeField, Min(0.05f)] float slideSeconds = 0.35f;

        [Header("Words (empty shows nothing)")]
        [SerializeField] string pauseWord = "Pause";
        [SerializeField] string resumeWord = "Play";
        [Tooltip("The title while the playing track is not known (the random music event).")]
        [SerializeField] string unknownTrackTitle = "Cosmic Shore soundtrack";

        ScreenSwitcher _switcher;
        ProfileRecordWindow _record;
        bool _recordSearched;
        SoundtrackPlayer _player;
        Vector2 _home;
        bool _shown;
        bool _hovered;
        float _toastUntil;
        int _lastTrack = int.MinValue;
        Tween _slide;

        SoundtrackPlayer Music => AudioSystem.Instance ? AudioSystem.Instance.Soundtrack : null;

        void Awake()
        {
            if (panel) _home = panel.anchoredPosition;
            if (backButton) backButton.onClick.AddListener(() => Music?.Previous());
            if (pauseButton) pauseButton.onClick.AddListener(() => Music?.TogglePause());
            if (skipButton) skipButton.onClick.AddListener(() => Music?.Next());
            SetControls(false);
            Place(false, true);
        }

        void OnDisable()
        {
            if (_player != null) _player.Changed -= OnChanged;
            _player = null;
            _slide?.Kill();
        }

        void Update()
        {
            if (_player == null)
            {
                _player = Music;
                if (_player == null) return;
                _player.Changed += OnChanged;
                OnChanged();
            }
            if (!_switcher) _switcher = FindAnyObjectByType<ScreenSwitcher>();
            if (!_recordSearched)
            {
                _record = FindAnyObjectByType<ProfileRecordWindow>(FindObjectsInactive.Include);
                _recordSearched = true;
            }

            bool musicOn = AudioSystem.Instance && AudioSystem.Instance.MusicEnabled && !_player.Stopped;
            bool recordOpen = _record && _record.IsOpen;
            bool resting = Resting();
            bool toast = toastOffScreens && Time.unscaledTime < _toastUntil;
            bool want = musicOn && !recordOpen && (resting || toast || (_hovered && _shown));
            if (want != _shown) Place(want, false);

            if (_shown)
            {
                float progress = _player.Progress;
                if (progressTrack) progressTrack.SetActive(progress >= 0f);
                if (progressFill && progress >= 0f) progressFill.anchorMax = new Vector2(progress, progressFill.anchorMax.y);
            }
        }

        void OnChanged()
        {
            var p = _player;
            if (p == null) return;
            bool known = p.NowPlaying >= 0;
            if (titleText) titleText.text = known ? p.Title(p.NowPlaying) : unknownTrackTitle;
            if (artistText) artistText.text = known ? p.Artist(p.NowPlaying) : p.ArtistLine;
            if (pauseLabel) pauseLabel.text = p.Paused ? resumeWord : pauseWord;
            if (p.NowPlaying != _lastTrack)
            {
                // A new track (or the random music restarting) announces itself.
                if (_lastTrack != int.MinValue) _toastUntil = Time.unscaledTime + toastSeconds;
                _lastTrack = p.NowPlaying;
            }
        }

        bool Resting()
        {
            if (!_switcher || restingOn == null) return false;
            // A window over the screen (a hub window, a configure, Settings) hides it: Home stays the active SCREEN under
            // every modal, so without this the card showed over every window (Shombith, 2026-10-25: "Music should not
            // show up everywhere").
            if (_switcher.HasActiveModal) return false;
            foreach (var s in restingOn)
                if (_switcher.ScreenIsActive(s)) return true;
            return false;
        }

        void Place(bool shown, bool instant)
        {
            _shown = shown;
            if (group)
            {
                group.interactable = shown;
                group.blocksRaycasts = shown;
            }
            if (!panel) return;
            _slide?.Kill();
            var target = shown ? _home : _home + new Vector2(slideDistance, 0f);
            if (instant)
            {
                panel.anchoredPosition = target;
                if (group) group.alpha = shown ? 1f : 0f;
                return;
            }
            if (group) group.alpha = 1f;
            _slide = panel.DOAnchorPos(target, slideSeconds).SetEase(shown ? Ease.OutCubic : Ease.InCubic).SetUpdate(true)
                .OnComplete(() => { if (!_shown && group) group.alpha = 0f; });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            SetControls(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            SetControls(false);
        }

        void SetControls(bool on)
        {
            if (!controls) return;
            controls.alpha = on ? 1f : 0f;
            controls.interactable = on;
            controls.blocksRaycasts = on;
        }
    }
}

using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Arkway voyage's one screen-space surface, with two channels — a persistent COUNTDOWN
    /// ("RETURN TO THE ARK — 3", held while the leash is breached) and timed BANNERS, the voyage's
    /// toasts ("STAY WITH THE ARK", "THE ARK HAS FALLEN"; Frogma Rev 113, Compare: a band top
    /// centre, a bold line and an optional line under it, an event, a threat or something good,
    /// one at a time).
    /// Built programmatically like <see cref="EnvironmentLoadVeil"/>'s overlay, because a leash
    /// telegraph has to reach a player who is by definition FAR from every world-space label the
    /// toy owns. It never blocks input and it fades rather than popping — continuity of
    /// existence applies to UI too.
    /// </summary>
    public sealed class ArkwayVoyageHud : MonoBehaviour
    {
        /// <summary>What a banner marks (Frogma Rev 113, Compare): an event (white), a threat (red) or something good
        /// (green). A plain <see cref="ShowBanner(string,float)"/> is an event.</summary>
        public enum Mood
        {
            Event = 0,
            Threat = 1,
            Good = 2,
        }

        const float FadeSeconds = 0.25f;
        /// <summary>A banner with others waiting behind it stays at least this long, then gives way (one at a time).</summary>
        const float QueuedMinSeconds = 1.6f;
        const int MaxQueued = 3;

        // Rev 113's toast colours, traced through the worn colour set (a set worn later repaints them, UIThemeLive).
        static readonly Color EventBand = new(0f, 0.016f, 0.122f, 0.9f);
        static readonly Color ThreatBand = new(0.314f, 0.039f, 0.078f, 0.88f);
        static readonly Color EventInk = new(0.902f, 0.914f, 1f);
        static readonly Color ThreatInk = new(1f, 0.541f, 0.486f);
        static readonly Color GoodInk = new(0.6f, 1f, 0.502f);
        static readonly Color UnderInk = new(0.722f, 0.753f, 0.941f);
        static readonly Color CountdownInk = new(1f, 0.92f, 0.6f);

        // Frogma Rev 116 A (Marquee): in a voyage the toast has ONE spot, just under the marquee the objective box
        // draws top centre, like an arcade cabinet's call-out line. Elsewhere (the Arkway toy) it keeps the top spot.
        const float TopY = 40f, UnderMarqueeY = 238f, CountdownGap = 110f;

        CanvasGroup _bannerGroup, _countdownGroup;
        ArkwayToastBand _band;
        RectTransform _countdownRect;
        TMP_Text _head, _under, _countdown;
        bool _countdownShown;
        float _bannerFrom = -1f, _bannerUntil = -1f;
        readonly Queue<(Mood mood, string line, string under, float seconds)> _queue = new();

        /// <summary>
        /// While this answers true the surface is held: nothing new comes up, a banner already up fades and keeps the
        /// time it had left, and the queue waits. The run holds it while the load veil covers the screen and while a
        /// voyage intro plays, so a toast raised then is read after, not spent unseen behind the veil or the intro.
        /// </summary>
        public System.Func<bool> Hold { get; set; }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000; // above gameplay HUD, below the load veil (30000)
            // No GraphicRaycaster: this surface is a readout and must never eat a tap.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // The banner: a band top centre, a bold line and an optional line under it.
            var band = Child("Banner", transform, new Vector2(0f, -40f), new Vector2(900f, 96f));
            _bannerGroup = band.gameObject.AddComponent<CanvasGroup>();
            _band = band.gameObject.AddComponent<ArkwayToastBand>();
            _band.raycastTarget = false;
            _head = Line(band, "Line", new Vector2(0f, -12f), new Vector2(860f, 44f), 32f, FontStyles.Bold);
            _under = Line(band, "Under", new Vector2(0f, -56f), new Vector2(860f, 30f), 21f, FontStyles.Normal);
            _under.color = ColorTrace.Retrace(ColorTrace.Ui, UnderInk);
            _bannerGroup.alpha = 0f;

            // The countdown: the leash, held while it is breached, below the banner.
            var cd = Child("Countdown", transform, new Vector2(0f, -(TopY + CountdownGap)), new Vector2(900f, 60f));
            _countdownRect = cd;
            _countdownGroup = cd.gameObject.AddComponent<CanvasGroup>();
            _countdown = Line(cd, "Line", Vector2.zero, new Vector2(900f, 60f), 34f, FontStyles.Normal);
            _countdown.color = ColorTrace.Retrace(ColorTrace.Ui, CountdownInk);
            _countdownGroup.alpha = 0f;
        }

        static RectTransform Child(string name, Transform parent, Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            return rect;
        }

        static TMP_Text Line(Transform parent, string name, Vector2 at, Vector2 size, float fontSize, FontStyles style)
        {
            var text = Child(name, parent, at, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        /// <summary>Hold a countdown line on screen until <see cref="HideCountdown"/>.</summary>
        public void ShowCountdown(string line)
        {
            _countdownShown = true;
            _countdown.text = line;
        }

        public void HideCountdown() => _countdownShown = false;

        /// <summary>Show a line for <paramref name="seconds"/>, then fade it away (an event).</summary>
        public void ShowBanner(string line, float seconds) => ShowBanner(line, seconds, Mood.Event, null);

        /// <summary>Show a banner of <paramref name="mood"/>, with <paramref name="under"/> beneath it (may be empty), for
        /// <paramref name="seconds"/>. Banners show one at a time: a new one waits until the one up has been read.</summary>
        public void ShowBanner(string line, float seconds, Mood mood, string under)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (_queue.Count >= MaxQueued) _queue.Dequeue(); // a backlog never outlives the moment it described
            _queue.Enqueue((mood, line, under ?? string.Empty, Mathf.Max(0.5f, seconds)));
        }

        void Update()
        {
            Place();
            float now = Time.unscaledTime;
            float step = Time.unscaledDeltaTime / FadeSeconds;
            if (Hold != null && Hold())
            {
                // The banner up keeps its time: it comes back when the hold lifts, then goes on as it would have.
                if (_bannerUntil >= 0f)
                {
                    _bannerFrom += Time.unscaledDeltaTime;
                    _bannerUntil += Time.unscaledDeltaTime;
                }
                _bannerGroup.alpha = Mathf.MoveTowards(_bannerGroup.alpha, 0f, step);
                _countdownGroup.alpha = Mathf.MoveTowards(_countdownGroup.alpha, 0f, step);
                return;
            }
            bool showing = _bannerUntil >= 0f && now < _bannerUntil;
            // Another banner waits: the one up gives way once it has been read.
            if (showing && _queue.Count > 0 && now - _bannerFrom >= QueuedMinSeconds) showing = false;
            if (!showing) _bannerUntil = -1f;
            if (!showing && _bannerGroup.alpha <= 0f && _queue.Count > 0) Next(now);

            _bannerGroup.alpha = Mathf.MoveTowards(_bannerGroup.alpha, _bannerUntil >= 0f ? 1f : 0f, step);
            _countdownGroup.alpha = Mathf.MoveTowards(_countdownGroup.alpha, _countdownShown ? 1f : 0f, step);
        }

        /// <summary>The toast spot: under the voyage marquee while it is up, the top spot otherwise.</summary>
        void Place()
        {
            float y = CosmicShore.UI.VoyageObjectiveBox.Showing ? UnderMarqueeY : TopY;
            var band = (RectTransform)_band.transform;
            if (!Mathf.Approximately(band.anchoredPosition.y, -y))
            {
                band.anchoredPosition = new Vector2(0f, -y);
                _countdownRect.anchoredPosition = new Vector2(0f, -(y + CountdownGap));
            }
        }

        void Next(float now)
        {
            var (mood, line, under, seconds) = _queue.Dequeue();
            _head.text = line;
            _head.color = ColorTrace.Retrace(ColorTrace.Ui, mood switch
            {
                Mood.Threat => ThreatInk,
                Mood.Good => GoodInk,
                _ => EventInk,
            });
            _band.color = ColorTrace.Retrace(ColorTrace.Ui, mood == Mood.Threat ? ThreatBand : EventBand);
            _band.Accent = _head.color;
            bool hasUnder = !string.IsNullOrEmpty(under);
            _under.text = under;
            _under.gameObject.SetActive(hasUnder);
            var band = (RectTransform)_band.transform;
            band.sizeDelta = new Vector2(band.sizeDelta.x, hasUnder ? 96f : 64f);
            _bannerFrom = now;
            _bannerUntil = now + seconds;
        }
    }

    /// <summary>
    /// The toast band (Frogma Rev 116 A): a slanted band with a thin accent line along its foot in the toast's mood
    /// colour, so an event, a threat and a good turn read apart before the words are read.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArkwayToastBand : MaskableGraphic
    {
        const float Slant = 22f;
        const float AccentHeight = 4f;

        Color _accent = Color.white;

        /// <summary>The colour of the line along the band's foot (already traced through the colour set).</summary>
        public Color Accent
        {
            get => _accent;
            set { _accent = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float s = Mathf.Min(Slant, r.width * 0.25f);
            Quad(vh, r.xMin + s, r.xMax, r.xMin, r.xMax - s, r.yMax, r.yMin + AccentHeight, color);
            float ax = s * AccentHeight / Mathf.Max(1f, r.height);
            var accent = _accent;
            Quad(vh, r.xMin + ax, r.xMax - s + ax, r.xMin, r.xMax - s, r.yMin + AccentHeight, r.yMin, accent);
        }

        static void Quad(VertexHelper vh, float topLeft, float topRight, float footLeft, float footRight, float top,
            float foot, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(footLeft, foot), c, Vector2.zero);
            vh.AddVert(new Vector3(topLeft, top), c, Vector2.zero);
            vh.AddVert(new Vector3(topRight, top), c, Vector2.zero);
            vh.AddVert(new Vector3(footRight, foot), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}

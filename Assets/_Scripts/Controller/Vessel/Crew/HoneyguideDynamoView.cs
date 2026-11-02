using System.Collections.Generic;
using CosmicShore.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Ratel sees, and its thumb (the Honeyguide Lab's <c>drawDynamo</c>, <c>drawReveal</c>, <c>engDown</c> and
    /// <c>pathTinkers</c>). One screen-space canvas built at runtime, as <see cref="ArkwayVoyageHud"/> is: a single
    /// <see cref="HoneyguideBoardGraphic"/> draws the hex board, the turning ports, the prisms, the AP and the battery, the
    /// spin bar and the three pattern cards; TextMeshPro labels carry the config's words.
    ///
    /// <para><b>Which is big.</b> Playing the Ratel: the board fills <c>engineerRect</c> and takes the pointer (drag a prism, tap
    /// a port, tap a lit card; tap the AP row to change the free prism's colour; right-click a prism, or on a phone hold it,
    /// to delete it; the Fly button left of the board swaps back to flying). Flying: the board is the small read-only
    /// view in <c>pilotRect</c>; a double tap on it swaps seats, and a chip above it shows what RT and LT do now. On the
    /// lowest preset there is no small board, and the Ratel's board sits on a plain backdrop with the world not drawn
    /// (<see cref="CrewSingleView"/>).</para>
    ///
    /// <para><b>Portrait on a phone</b> (Garrett 2026-10-22: "assume the player is holding the phone in portrait mode"). The
    /// screen stays locked to landscape (<see cref="DeviceOrientationHandler"/>), so the big board is drawn a quarter turn
    /// round, in a frame as wide as the screen is tall, and reads upright with the phone held portrait: turning the phone is
    /// part of taking the Ratel's seat. Its text, cards and Fly button are sized in millimetres for a thumb
    /// (<c>portraitTextMillimetres</c>, <c>portraitCardMillimetres</c>). The small view while flying stays landscape.</para>
    /// </summary>
    sealed class HoneyguideDynamoView
    {
        static readonly Color Panel = new(0.043f, 0.051f, 0.071f, 0.88f);
        static readonly Color Edge = new(0.165f, 0.2f, 0.25f, 1f);
        static readonly Color Cell = new(0.086f, 0.106f, 0.137f, 1f);
        static readonly Color CellEdge = new(0.149f, 0.188f, 0.235f, 1f);
        static readonly Color WildCell = new(0.169f, 0.153f, 0.208f, 1f);
        static readonly Color ApFull = new(1f, 0.698f, 0.122f, 1f);
        static readonly Color ApBonus = new(1f, 0.886f, 0.541f, 1f);
        static readonly Color ApEmpty = new(0.227f, 0.2f, 0.149f, 1f);
        static readonly Color BarTrack = new(0.11f, 0.137f, 0.176f, 1f);
        static readonly Color BarRun = new(0.435f, 0.659f, 0.784f, 1f);
        static readonly Color BarStill = new(0.29f, 0.333f, 0.4f, 1f);
        static readonly Color Card = new(0.075f, 0.094f, 0.125f, 1f);
        static readonly Color UsePip = new(0.227f, 0.259f, 0.314f, 1f);
        static readonly Color Ability = new(0.624f, 0.902f, 1f, 1f);
        static readonly Color Muted = new(0.541f, 0.588f, 0.651f, 1f);
        static readonly Color Cream = new(1f, 0.953f, 0.812f, 1f);
        static readonly Color Label = new(0.851f, 0.788f, 0.639f, 1f);
        static readonly Color Backdrop = new(0.043f, 0.051f, 0.071f, 1f);   // the board's panel, opaque
        static readonly Color HiveBar = new(1f, 0.655f, 0f, 1f);             // hive mode's time left
        static readonly Color StripTrack = new(0.165f, 0.2f, 0.25f, 1f);

        readonly HoneyguideCrew _crew;
        readonly GameObject _root;
        readonly RectTransform _content;   // the board's frame: the screen, or the screen turned a quarter for portrait
        readonly HoneyguideBoardGraphic _graphic;
        readonly TMP_Text _seat, _abilityName, _abilityText, _free, _revealTitle, _revealText, _detail, _chip, _fly;
        readonly TMP_Text[] _slotNames = new TMP_Text[3], _slotTaps = new TMP_Text[3];
        // round 7: the abilities offered to pick from, as cards over the board (laid out with the labels, drawn by the mesh,
        // tapped by Down)
        const int MaxOffers = 4;
        readonly TMP_Text[] _offerNames = new TMP_Text[MaxOffers], _offerTexts = new TMP_Text[MaxOffers];
        readonly Rect[] _offerRects = new Rect[MaxOffers];
        int _offerCount;
        // hive mode (Garrett 2026-10-31): the strip over the board, the Leave button, the thumb's chain
        readonly TMP_Text _strip, _leave;
        Rect _stripRect, _leaveRect;
        string _stripTextFor;
        float _stripTextFs, _stripTextW;
        bool _hiveDrag;
        int _hiveLast = -1;

        // this frame's layout, in canvas pixels (y up)
        Rect _rect, _apRect, _chipRect, _flyRect, _lineRect;
        readonly Rect[] _slots = new Rect[3];
        float _size, _cx, _cy;
        bool _small;
        // the view frame (y up, canvas pixels): its size, which way it is turned, the unit every length is in and the text size
        float _viewW, _viewH, _ui = 1f, _text = 15f, _cardHead;
        int _turn;   // 0 landscape; +1 a quarter counter-clockwise (the screen's LandscapeLeft); -1 clockwise (LandscapeRight)

        // the thumb
        int _dragFrom = -1;
        Vector2 _dragAt;
        Vector2 _pointerAt = new(-1f, -1f);
        int _detailSlot = -1;   // the card whose details are open (a right-click), or -1
        float _lastSmallTap = -99f;
        bool _smallDoubleTap;
        bool _flyTapped;        // the Fly button beside the big board (the phone's way back to flying)
        float _pressAt = -1f;   // touch: when the finger went down on a prism, for the long-press delete
        int _pressCell = -1;
        bool _longFired;
        // the pilot's camera asleep under the Ratel's board on the lowest preset (CrewSingleView)
        readonly PilotCameraPark _park = new();

        internal HoneyguideDynamoView(HoneyguideCrew crew)
        {
            _crew = crew;
            _root = new GameObject("HoneyguideDynamoView", typeof(RectTransform));
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19000;   // above the gameplay HUD, below the Arkway banner (20000) and the load veil (30000)
            // No GraphicRaycaster: the board reads the pointer itself, so it never eats a tap meant for the HUD.

            var cGo = new GameObject("Frame", typeof(RectTransform));
            cGo.transform.SetParent(_root.transform, false);
            _content = (RectTransform)cGo.transform;
            _content.anchorMin = _content.anchorMax = Vector2.zero;
            _content.pivot = Vector2.zero;

            var gGo = new GameObject("Board", typeof(RectTransform));
            gGo.transform.SetParent(_content, false);
            var gRt = (RectTransform)gGo.transform;
            Stretch(gRt);
            // The mesh is built in canvas pixels from the bottom-left corner (the layout's frame, and the labels'), so the
            // graphic's own origin must be that corner: at the default centre pivot the whole board drew half a screen up
            // and right - the small view half off the top, the Ratel's big board entirely off screen (Garrett, 2026-10-21).
            gRt.pivot = Vector2.zero;
            _graphic = gGo.AddComponent<HoneyguideBoardGraphic>();
            _graphic.raycastTarget = false;
            _graphic.View = this;

            _seat = MakeText("Seat", Label, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _abilityName = MakeText("AbilityName", Ability, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _abilityText = MakeText("AbilityText", Muted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _free = MakeText("Free", Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            _revealTitle = MakeText("RevealTitle", Ability, FontStyles.Bold, TextAlignmentOptions.Center);
            _revealText = MakeText("RevealText", Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            _detail = MakeText("CardDetail", Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            _chip = MakeText("TriggerChip", Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            _fly = MakeText("FlyButton", Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            _strip = MakeText("Strip", Muted, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _leave = MakeText("LeaveButton", Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            for (int k = 0; k < 3; k++)
            {
                _slotNames[k] = MakeText("Slot" + k, new Color(0.91f, 0.93f, 0.95f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
                _slotTaps[k] = MakeText("Tap" + k, Color.white, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            }
            for (int k = 0; k < MaxOffers; k++)
            {
                _offerNames[k] = MakeText("OfferName" + k, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                _offerTexts[k] = MakeText("OfferText" + k, Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            }
        }

        internal void Dispose()
        {
            _park.Release();
            if (_root) Object.Destroy(_root);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        TMP_Text MakeText(string name, Color colour, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset) t.font = TMP_Settings.defaultFontAsset;
            t.color = colour;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        static void Place(TMP_Text t, Rect r, float size, string text)
        {
            bool on = !string.IsNullOrEmpty(text) && r.width > 2f && r.height > 2f;
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (!on) return;
            var rt = t.rectTransform;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = r.size;
            t.fontSize = size;
            if (t.text != text) t.text = text;
        }

        // ------------------------------------------------------------------ layout (the lab's dynLayout, y up)

        internal Rect BoardRect => _rect;
        internal bool Small => _small;
        internal float HexSize => _size;
        internal Vector2 Centre => new(_cx, _cy);
        internal Rect SlotRect(int k) => _slots[k];
        internal Rect ApRect => _apRect;
        internal Rect ChipRect => _chipRect;
        internal Rect FlyRect => _flyRect;
        internal int DragFrom => _dragFrom;
        internal Vector2 DragAt => _dragAt;
        internal HoneyguideCrew Crew => _crew;

        void Layout()
        {
            _small = !_crew.HumanEngineers;
            var cfg = _crew.Config;
            float w = Screen.width, h = Screen.height;
            float mm = Screen.dpi > 0f ? Screen.dpi / 25.4f : 6f;
            _turn = !_small && Portrait(cfg) ? (Screen.orientation == ScreenOrientation.LandscapeRight ? -1 : 1) : 0;
            float pad, top, slotsH, freeH;
            if (_turn != 0)
            {
                _viewW = h;
                _viewH = w;
                if (Screen.dpi <= 0f) mm = _viewW / 68f;   // no dpi reported: a phone held portrait is about 68 mm wide
                var safe = ToView(Screen.safeArea);
                var f = cfg.PortraitRect;
                _rect = new Rect(safe.x + f.x * safe.width, safe.y + f.y * safe.height, f.width * safe.width, f.height * safe.height);
                _text = cfg.PortraitTextMillimetres * mm;
                _ui = _text / 15f;   // the landscape board's text is 15 units: every length below keeps its proportion to the text
                pad = 6f * _ui;
                top = 28f * _ui + 4.2f * _text;   // the seat row, then the ability's name and two lines of its words
                slotsH = Mathf.Min(cfg.PortraitCardMillimetres * mm, _rect.height * 0.25f);
                freeH = 2.4f * 0.85f * _text;   // the line above the cards, two lines of it
                _cardHead = 2.1f * 0.8f * _text + 4f * _ui;   // two lines of the card's name
            }
            else
            {
                _viewW = w;
                _viewH = h;
                var f = _small ? cfg.PilotRect : cfg.EngineerRect;
                _rect = new Rect(f.x * w, f.y * h, f.width * w, f.height * h);
                _ui = Mathf.Max(0.6f, h / 900f);
                _text = Mathf.Clamp(_rect.width / 30f, 10f * _ui, 15f * _ui);
                pad = 6f * _ui;
                top = (_small ? 30f : 46f) * _ui;
                slotsH = _small ? Mathf.Max(34f * _ui, _rect.height * 0.2f) : Mathf.Max(64f * _ui, Mathf.Min(110f * _ui, _rect.height * 0.17f));
                freeH = _small ? 0f : 2.3f * 0.8f * _text;   // the line above the cards, two lines of it
                _cardHead = _small ? 2f : 18f * _ui;
            }
            PlaceFrame();
            float ui = _ui;
            // the strip (Garrett 2026-10-31: "a symbolic view ... in a thin strip"): just above the board, on the big board only
            float stripH = _small ? 0f : 1.5f * _text;
            top += stripH;
            _stripRect = _small ? default : new Rect(_rect.x + pad, _rect.yMax - top, _rect.width - 2f * pad, stripH);
            float bw = _rect.width - 2f * pad, bh = _rect.height - top - slotsH - freeH - 2f * pad;
            _size = Mathf.Max(6f, Mathf.Min(bw / (7f * Mathf.Sqrt(3f)), bh / 11f));
            _cx = _rect.center.x;
            _cy = _rect.yMax - top - pad - bh / 2f;
            float gap = 6f * ui, sw = (_rect.width - 2f * pad - gap * 2f) / 3f;
            for (int k = 0; k < 3; k++) _slots[k] = new Rect(_rect.x + pad + k * (sw + gap), _rect.y + pad / 2f, sw, slotsH);
            // the band between the board and the cards: what the last pattern made does, or the free prism's line
            _lineRect = new Rect(_rect.x + 8f * ui, _slots[0].yMax + pad * 0.25f, _rect.width - 16f * ui, freeH);
            // hive mode's Leave button: the band's right end
            float lw = Mathf.Max(5f * _text, 44f * ui);
            _leaveRect = _small || !_crew.InHive ? default
                : new Rect(_lineRect.xMax - lw, _lineRect.y, lw, Mathf.Max(_lineRect.height, 1.8f * _text));
            _apRect = new Rect(_rect.x, _rect.yMax - 28f * ui, Mathf.Max(80f * ui, _rect.width * 0.45f), 28f * ui);
            _chipRect = _small ? new Rect(_rect.x, _rect.yMax + 6f * ui, _rect.width, 52f * ui) : default;
            // The Fly button: a thumb-sized square just left of the big board, at its top (no scaler on this canvas: pixels).
            // Portrait: just above the board, at its left (the board is as wide as the phone).
            float bs = Mathf.Max(_turn != 0 ? 9f * mm : 44f * ui, cfg.FlyButtonMillimetres * mm);
            _flyRect = _small ? default
                : _turn != 0 ? new Rect(_rect.x, Mathf.Min(_rect.yMax + 8f * ui, _viewH - bs), bs, bs)
                : new Rect(Mathf.Max(4f * ui, _rect.x - bs - 8f * ui), _rect.yMax - bs, bs, bs);
        }

        static bool Portrait(HoneyguideConfigSO cfg) => cfg.BoardOrientation switch
        {
            HoneyguideBoardOrientation.Portrait => true,
            HoneyguideBoardOrientation.Landscape => false,
            _ => KitInputDevice.IsTouchPrimary,
        };

        // The frame's transform: a quarter turn counter-clockwise maps the view's (x, y) to the screen's (W - y, x), so the
        // view's top is the screen's left edge - the phone's top when a LandscapeLeft screen is held portrait.
        void PlaceFrame()
        {
            _content.sizeDelta = new Vector2(_viewW, _viewH);
            _content.anchoredPosition = _turn > 0 ? new Vector2(Screen.width, 0f) : _turn < 0 ? new Vector2(0f, Screen.height) : Vector2.zero;
            _content.localRotation = Quaternion.Euler(0f, 0f, 90f * _turn);
        }

        /// <summary>A screen point (pixels, y up) in the view frame.</summary>
        internal Vector2 ToView(Vector2 p) =>
            _turn > 0 ? new Vector2(p.y, Screen.width - p.x) : _turn < 0 ? new Vector2(Screen.height - p.y, p.x) : p;

        Rect ToView(Rect r)
        {
            Vector2 a = ToView(r.min), b = ToView(r.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        internal bool Turned => _turn != 0;

        internal Vector2 CellPx(int i) =>
            new(_cx + _size * Mathf.Sqrt(3f) * (DynamoCore.Q[i] + DynamoCore.R[i] / 2f), _cy - _size * 1.5f * DynamoCore.R[i]);

        int PxCell(Vector2 p)
        {
            float dx = p.x - _cx, dy = _cy - p.y;
            float fq = (dx * Mathf.Sqrt(3f) / 3f - dy / 3f) / _size, fr = dy * 2f / 3f / _size, fs = -fq - fr;
            int q = Mathf.RoundToInt(fq), r = Mathf.RoundToInt(fr), s = Mathf.RoundToInt(fs);
            float dq = Mathf.Abs(q - fq), dr = Mathf.Abs(r - fr), ds = Mathf.Abs(s - fs);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            return DynamoCore.Index(q, r);
        }

        // ------------------------------------------------------------------ the frame

        internal void Frame(float dt)
        {
            var core = _crew.Core;
            if (core == null) return;
            Layout();
            // One view (the lowest preset, CrewSingleView): no small board while the human flies, and while the human plays
            // the Ratel the world behind the board is not drawn - the board on a plain backdrop is all the screen shows.
            bool single = CrewSingleView.On;
            if (single && !_small) _park.Park(CameraManager.ViewCamera, Backdrop);
            else _park.Release();
            _seatButtonOnly = single && _small;
            if (_seatButtonOnly) { FrameSeatButton(); return; }
            var cfg = _crew.Config;
            float ui = _ui, fs = _text;
            bool turned = _turn != 0;

            Place(_seat, new Rect(_rect.x + 10f * ui, _rect.yMax - 28f * ui, 120f * ui, 26f * ui), fs, _small ? "" : cfg.RatelLabel);
            var ab = cfg.AbilityText(core.Ability);
            bool hive = _crew.InHive;
            string abName = hive ? cfg.HiveTitle : ab == null ? "" : ab.name, abText = hive || ab == null ? "" : ab.text;
            if (turned)   // portrait: the ability's name on its own line under the seat row, its words wrapped below it
            {
                float nameY = _rect.yMax - 28f * ui - 1.4f * fs, textH = 2.5f * 0.9f * fs;
                Place(_abilityName, new Rect(_rect.x + 10f * ui, nameY, _rect.width - 20f * ui, 1.4f * fs), fs, abName);
                Place(_abilityText, new Rect(_rect.x + 10f * ui, nameY - textH, _rect.width - 20f * ui, textH), fs * 0.9f, abText);
            }
            else
            {
                float abY = _rect.yMax - 46f * ui;
                Place(_abilityName, new Rect(_rect.x + 10f * ui, abY, _rect.width * (hive ? 0.9f : 0.3f), 18f * ui), fs * 0.9f, _small ? "" : abName);
                Place(_abilityText, new Rect(_rect.x + 12f * ui + _rect.width * 0.3f, abY, _rect.width * 0.7f - 24f * ui, 18f * ui), fs * 0.8f,
                      _small ? "" : abText);
            }

            // Above the cards, always (Garrett 2026-10-23): what the last pattern made does, until the next one; the free
            // prism's line instead while one rides the cursor. Lime when it is about the Dynamo itself; it flashes white as
            // the pattern is made.
            bool freeLine = core.FreePlace > 0 && core.RevealState == null && !hive;
            string madeLine = _small ? ""
                : hive ? (_crew.Hive.Rewards.Count > 0 ? TemplateText.Fill(cfg.HiveEarnedTemplate, "colours", _crew.ColourList(_crew.Hive.Rewards)) : "")
                : freeLine ? _mFree.Fill(cfg.FreePrismTemplate, "count", IntText(core.FreePlace), "colour", cfg.ColourName(core.Free[0]))
                : _crew.MadeLine;
            var lineColour = freeLine || _crew.MadeLineDynamo ? cfg.DynamoLineColour : Cream;
            float lineFlash = cfg.MadeJuiceSeconds > 0f ? 1f - Mathf.Clamp01((Time.time - _crew.LastMade.At) / cfg.MadeJuiceSeconds) : 0f;
            _free.color = Color.Lerp(lineColour, Color.white, lineFlash * lineFlash);
            var lineRect = _leaveRect.width > 0f ? new Rect(_lineRect.x, _lineRect.y, _lineRect.width - _leaveRect.width - 6f * ui, _lineRect.height) : _lineRect;
            Place(_free, lineRect, fs * (turned ? 0.85f : 0.8f), madeLine);
            Place(_leave, _leaveRect, fs, _leaveRect.width > 0f ? cfg.HiveLeaveLabel : "");
            string stripLine = _small ? ""
                : _crew.StripDistance >= 0f ? _mStrip.Fill(cfg.StripOmniTemplate, "distance", IntText(Mathf.RoundToInt(_crew.StripDistance)))
                : cfg.StripNoTargetLine;
            Place(_strip, new Rect(_stripRect.x, _stripRect.y, _stripRect.width, _stripRect.height), fs * 0.8f, stripLine);
            // the track ends short of the distance text; measured only when the line or its size changes
            if (!ReferenceEquals(stripLine, _stripTextFor) || !Mathf.Approximately(fs, _stripTextFs))
            {
                _stripTextFor = stripLine;
                _stripTextFs = fs;
                _stripTextW = string.IsNullOrEmpty(stripLine) ? 0f : _strip.GetPreferredValues(stripLine).x;
            }

            var built = core.Built(_crew.Settings);
            for (int k = 0; k < 3; k++)
            {
                int pid = k < core.Slots.Length ? core.Slots[k] : -1;
                var r = _slots[k];
                string name = !_small && pid >= 0 && pid < cfg.Patterns.Count ? cfg.Patterns[pid].name : "";
                bool lit = built.Length > k && built[k] != null && core.RevealState == null && !hive;
                if (turned)   // portrait: the card's name on up to two lines, sized to the board's text
                {
                    float cs = 0.8f * fs;
                    Place(_slotNames[k], new Rect(r.x + 5f * ui, r.yMax - _cardHead, r.width - 10f * ui, _cardHead - 2f * ui), cs, name);
                    Place(_slotTaps[k], new Rect(r.x + 5f * ui, r.y + 2f * ui, r.width * 0.6f, 1.3f * cs), cs, lit ? cfg.TapToMake : "");
                    continue;
                }
                Place(_slotNames[k], new Rect(r.x + 5f * ui, r.yMax - 18f * ui, r.width - 10f * ui, 16f * ui), Mathf.Clamp(r.width / 10f, 9f * ui, 12f * ui), name);
                Place(_slotTaps[k], new Rect(r.x + 5f * ui, r.y + 2f * ui, r.width * 0.5f, 16f * ui), Mathf.Clamp(r.width / 10f, 9f * ui, 12f * ui),
                      lit && !_small ? cfg.TapToMake : "");
            }

            var rv = core.RevealState;
            var offers = rv != null && rv.Stage == 1 ? rv.Offers : null;
            string title = "", body = "";
            if (offers != null) title = cfg.PickAbilityTitle;
            else if (rv != null && rv.Stage == 1 && ab != null) { title = _mReveal.Fill(cfg.RevealAbilityTemplate, "name", ab.name); body = _small ? "" : ab.text; }
            else if (rv != null && rv.Stage == 2) title = cfg.RevealPortsLine;
            var titleRect = new Rect(_rect.x + 10f * ui, _cy + _size * 0.6f, _rect.width - 20f * ui, 30f * ui);
            LayOffers(cfg, offers, ui, fs, ref titleRect);
            Place(_revealTitle, titleRect, (_small ? 0.75f : 1.25f) * fs, title);
            Place(_revealText, new Rect(_rect.x + 18f * ui, _cy - _size * 2.2f, _rect.width - 36f * ui, _size * 2.6f), fs, body);

            // a right-clicked card's details, over the board
            if (_small || rv != null || _detailSlot >= core.Slots.Length || (_detailSlot >= 0 && core.Slots[_detailSlot] < 0)) _detailSlot = -1;
            string detail = "";
            if (_detailSlot >= 0 && core.Slots[_detailSlot] < cfg.Patterns.Count)
            {
                var pat = cfg.Patterns[core.Slots[_detailSlot]];
                detail = _mDetail.Fill(cfg.CardDetailTemplate, "name", pat.name, "text", pat.effectText,
                                         "uses", IntText(core.Uses[_detailSlot]), "max", IntText(cfg.UsesPerPattern));
            }
            Place(_detail, DetailRect(ui), fs * 1.15f, detail);

            // the triggers, beside the small board while the human flies: RT and LT, each its base power or its current banked upgrade and its uses
            string chip = "";
            ChipUpgraded = false;
            if (_small)
            {
                for (int k = 0; k < 2; k++)
                {
                    var b = _crew.Current(k);
                    ChipUpgraded |= b != null;
                    int uses = _crew.BankedUses(k);
                    string power = b == null ? cfg.BasePowerName(k)
                        : _mPower[k].Fill(cfg.UpgradePowerTemplate, "name", b.Name, "ticks", _mTicks[k].Ticks(cfg.TickGlyph, uses),
                                            "uses", IntText(uses));
                    string line = _mChip[k].Fill(cfg.TriggerChipTemplate, "trigger", cfg.TriggerName(k), "power", power);
                    if (line.Length == 0) continue;
                    chip = chip.Length > 0 ? _mChipJoin.Join(chip, line) : line;
                }
            }
            Place(_chip, _chipRect, fs, chip);
            Place(_fly, _flyRect, fs * 1.1f, _small ? "" : cfg.FlyButtonLabel);
            ChipShown = chip.Length > 0;

            if (_small) ReadSmallViewTaps();
            _graphic.SetVerticesDirty();
        }

        // Perf (Switchback pass 2026-11-02): Frame runs every frame and rebuilt every board string with TemplateText.Fill
        // (params array + one Replace per key) and int.ToString(). Each call site now keeps its last inputs and result,
        // so the strings are only rebuilt when what they say changes. Output text is identical.
        sealed class FillMemo
        {
            string _t, _v1, _v2, _v3, _v4, _r;
            internal string Fill(string t, string k1, string v1, string k2 = null, string v2 = null,
                                 string k3 = null, string v3 = null, string k4 = null, string v4 = null)
            {
                if (_r != null && ReferenceEquals(t, _t) && v1 == _v1 && v2 == _v2 && v3 == _v3 && v4 == _v4) return _r;
                _t = t; _v1 = v1; _v2 = v2; _v3 = v3; _v4 = v4;
                return _r = k4 != null ? TemplateText.Fill(t, k1, v1, k2, v2, k3, v3, k4, v4)
                          : k3 != null ? TemplateText.Fill(t, k1, v1, k2, v2, k3, v3)
                          : k2 != null ? TemplateText.Fill(t, k1, v1, k2, v2)
                          : TemplateText.Fill(t, k1, v1);
            }
            internal string Join(string a, string b)
            {
                if (_r != null && a == _v1 && b == _v2) return _r;
                _v1 = a; _v2 = b;
                return _r = a + "\n" + b;
            }
            int _n = -1;
            internal string Ticks(string glyph, int n)
            {
                if (_r != null && n == _n && ReferenceEquals(glyph, _t)) return _r;
                _t = glyph; _n = n;
                return _r = HoneyguideDynamoView.Ticks(glyph, n);
            }
        }
        readonly FillMemo _mFree = new(), _mStrip = new(), _mReveal = new(), _mDetail = new(), _mChipJoin = new();
        readonly FillMemo[] _mPower = { new(), new() }, _mChip = { new(), new() }, _mTicks = { new(), new() };
        static readonly string[] s_ints = new string[1000];
        static string IntText(int n) => n >= 0 && n < s_ints.Length ? s_ints[n] ??= n.ToString() : n.ToString();

        // One glyph a banked use (the lab's ticks on the trigger's card).
        static string Ticks(string glyph, int n)
        {
            if (string.IsNullOrEmpty(glyph) || n <= 0) return "";
            var sb = new System.Text.StringBuilder(glyph.Length * n);
            for (int i = 0; i < n; i++) sb.Append(glyph);
            return sb.ToString();
        }

        internal bool ChipShown { get; private set; }

        // One view, flying (CrewSingleView): no small board, only a thumb-sized Ratel button at the top of the board's
        // place, the phone's way to the Ratel (the Fly button's twin; a phone has no D-pad and no arrow keys). At the TOP,
        // above where the left thumb flies, and only a SHORT tap counts, so a flying thumb that slides over it is no swap.
        bool _seatButtonOnly;
        float _seatPressAt = -99f;
        const float SeatTapMaxSeconds = 0.3f;

        void FrameSeatButton()
        {
            var cfg = _crew.Config;
            float ui = Mathf.Max(0.6f, Screen.height / 900f), fs = Mathf.Clamp(_rect.width / 30f, 10f * ui, 15f * ui);
            float mm = Screen.dpi > 0f ? Screen.dpi / 25.4f : 6f, bs = Mathf.Max(44f * ui, cfg.FlyButtonMillimetres * mm);
            _flyRect = new Rect(_rect.x, _rect.yMax - bs, Mathf.Max(bs, Mathf.Min(_rect.width, bs * 1.6f)), bs);
            Place(_seat, default, fs, ""); Place(_abilityName, default, fs, ""); Place(_abilityText, default, fs, "");
            Place(_free, default, fs, ""); Place(_revealTitle, default, fs, ""); Place(_revealText, default, fs, "");
            Place(_detail, default, fs, ""); Place(_chip, default, fs, ""); Place(_strip, default, fs, ""); Place(_leave, default, fs, "");
            for (int k = 0; k < 3; k++) { Place(_slotNames[k], default, fs, ""); Place(_slotTaps[k], default, fs, ""); }
            for (int k = 0; k < MaxOffers; k++) { Place(_offerNames[k], default, fs, ""); Place(_offerTexts[k], default, fs, ""); }
            _offerCount = 0;
            Place(_fly, _flyRect, fs * 1.1f, cfg.RatelLabel);
            ChipShown = ChipUpgraded = false;
            _detailSlot = -1;
            if (TryPointer(out var at, out bool pressed, out bool released, out _))
            {
                if (pressed) _seatPressAt = _flyRect.Contains(at) ? Time.unscaledTime : -99f;
                if (released && _flyRect.Contains(at) && Time.unscaledTime - _seatPressAt <= SeatTapMaxSeconds) _flyTapped = true;
                if (released) _seatPressAt = -99f;
            }
            _graphic.SetVerticesDirty();
        }

        /// <summary>True while an upgrade holds either trigger (the chip lights up).</summary>
        internal bool ChipUpgraded { get; private set; }

        Rect DetailRect(float ui) => new(_rect.x + _rect.width * 0.1f, _cy - _size * 3.5f, _rect.width * 0.8f, _size * 7f);

        // Round 7: the offered abilities as a column of cards between the cards' row and the spin bar, centred there, a heading
        // on top (the lab's drawReveal). The small board shows the names only.
        void LayOffers(HoneyguideConfigSO cfg, DynamoAbility[] offers, float ui, float fs, ref Rect titleRect)
        {
            _offerCount = offers == null ? 0 : Mathf.Min(offers.Length, MaxOffers);
            float top = _rect.yMax - (_small ? 3f : 5f) * ui, bottom = _slots[0].yMax, head = (_small ? 1.2f : 2f) * fs, gap = (_small ? 3f : 8f) * ui;
            float w = Mathf.Min(_rect.width - 20f * ui, 30f * fs), x = _rect.center.x - w / 2f;
            float ch = _offerCount > 0 ? Mathf.Min((_small ? 1.6f : 5.5f) * fs, (top - bottom - head - gap * (_offerCount + 1)) / _offerCount) : 0f;
            // centred in the band between the cards and the spin bar (Garrett 2026-11-02: "those abilities should be more
            // vertically centered"), the pick's time line under the last card included
            float block = head + gap + _offerCount * ch + Mathf.Max(0, _offerCount - 1) * gap + 6f * ui;
            top -= Mathf.Max(0f, (top - bottom - block) / 2f);
            if (_offerCount > 0) titleRect = new Rect(x, top - head, w, head);
            for (int k = 0; k < MaxOffers; k++)
            {
                if (k >= _offerCount) { _offerRects[k] = default; Place(_offerNames[k], default, fs, ""); Place(_offerTexts[k], default, fs, ""); continue; }
                var r = _offerRects[k] = new Rect(x, top - head - gap - (k + 1) * ch - k * gap, w, ch);
                var text = cfg.AbilityText(offers[k]);
                float nameH = _small ? ch : 1.5f * fs;
                Place(_offerNames[k], new Rect(r.x + 6f * ui, r.yMax - nameH, r.width - 12f * ui, nameH), (_small ? 0.8f : 1.1f) * fs, text == null ? "" : text.name);
                Place(_offerTexts[k], new Rect(r.x + 8f * ui, r.y + 4f * ui, r.width - 16f * ui, Mathf.Max(0f, r.height - nameH - 6f * ui)), 0.9f * fs,
                      _small || text == null ? "" : text.text);
            }
        }

        // ------------------------------------------------------------------ the pointer

        static bool TryPointer(out Vector2 at, out bool pressed, out bool released, out bool held) =>
            TryPointer(out at, out pressed, out released, out held, out _);

        static bool TryPointer(out Vector2 at, out bool pressed, out bool released, out bool held, out bool touch)
        {
            at = default;
            pressed = released = held = touch = false;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var t = ts.primaryTouch;
                if (t.press.isPressed || t.press.wasReleasedThisFrame)
                {
                    at = t.position.ReadValue();
                    pressed = t.press.wasPressedThisFrame;
                    released = t.press.wasReleasedThisFrame;
                    held = t.press.isPressed;
                    touch = true;
                    return true;
                }
            }
            var m = Mouse.current;
            if (m == null) return false;
            at = m.position.ReadValue();
            pressed = m.leftButton.wasPressedThisFrame;
            released = m.leftButton.wasReleasedThisFrame;
            held = m.leftButton.isPressed;
            return true;
        }

        // Flying: a double tap on the small board swaps seats.
        void ReadSmallViewTaps()
        {
            if (!TryPointer(out var at, out bool pressed, out _, out _) || !pressed) return;
            if (!_rect.Contains(ToView(at))) return;
            if (Time.unscaledTime - _lastSmallTap < 0.35f) { _smallDoubleTap = true; _lastSmallTap = -99f; }
            else _lastSmallTap = Time.unscaledTime;
        }

        /// <summary>True once after a seat swap was asked for on the board: a double tap on the small view while flying, or the
        /// Fly button beside the big board while playing the Ratel (a phone has no D-pad and no arrow keys).</summary>
        internal bool SmallViewDoubleTapped()
        {
            bool d = _smallDoubleTap || _flyTapped;
            _smallDoubleTap = _flyTapped = false;
            return d;
        }

        internal void CancelDrag()
        {
            _dragFrom = -1;
            _hiveDrag = false;
            _hiveLast = -1;
        }

        // Playing the Ratel: the lab's engDown / up. Layout is last frame's (the board does not move between frames).
        internal void ReadEngineerInput(DynamoCore core, DynamoSettings s)
        {
            if (_small || !TryPointer(out var screenAt, out bool pressed, out bool released, out bool held, out bool touch)) return;
            var at = ToView(screenAt);   // every rect and cell below is in the view frame (turned a quarter in portrait)
            _pointerAt = at;
            if (_dragFrom >= 0) _dragAt = at;
            var mouse = Mouse.current;
            bool secondary = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if ((pressed || secondary) && _detailSlot >= 0) { _detailSlot = -1; return; }   // any click closes the card details
            if (pressed && _flyRect.Contains(at)) { _flyTapped = true; return; }            // back to flying
            if (_crew.InHive) { HiveThumb(at, pressed, released, held); return; }
            if (secondary && _rect.Contains(at)) { Secondary(core, at); return; }
            if (pressed && _rect.Contains(at))
            {
                Down(core, at);
                _pressCell = touch ? _dragFrom : -1;   // only a finger on a prism can long-press (a mouse has its right button)
                _pressAt = Time.unscaledTime;
                _longFired = false;
            }
            // Touch: holding a prism still is the phone's right-click - it deletes the prism (1 AP) and drops the drag.
            if (held && !_longFired && _pressCell >= 0 && Time.unscaledTime - _pressAt >= _crew.Config.LongPressSeconds)
            {
                _longFired = true;
                if (PxCell(at) == _pressCell) Secondary(core, at);
                else _pressCell = -1;   // the finger has already left the cell: it is a drag
            }
            if (released && _dragFrom >= 0)
            {
                int from = _dragFrom;
                _dragFrom = -1;
                int to = PxCell(at);
                if (to >= 0 && to != from && core.Cells[from] != 0)
                {
                    if (core.Cells[to] == 0) foreach (var inp in PathTinkers(core, from, to)) _crew.EngineerInput(inp);
                    else if (core.Ability == DynamoAbility.Swap) _crew.EngineerInput(DynamoInput.Tinker(from, to));
                }
            }
            if (!held && !released) _dragFrom = -1;
            if (!held) _pressCell = -1;
        }

        // Right-click (Garrett 2026-10-21): a card opens its details; a prism is deleted, for 1 AP. On a phone a long press on a
        // prism comes here too.
        void Secondary(DynamoCore core, Vector2 at)
        {
            _dragFrom = -1;
            for (int k = 0; k < 3; k++) if (_slots[k].Contains(at)) { if (k < core.Slots.Length && core.Slots[k] >= 0) _detailSlot = k; return; }
            int c = PxCell(at);
            if (c >= 0 && core.Cells[c] != 0) _crew.EngineerInput(DynamoInput.Remove(c));
        }

        void Down(DynamoCore core, Vector2 at)
        {
            if (core.RevealState?.Offers != null)   // round 7: tap an offered ability to pick it; nothing else answers meanwhile
            {
                for (int k = 0; k < _offerCount; k++) if (_offerRects[k].Contains(at)) { _crew.EngineerInput(DynamoInput.Pick(k)); return; }
                return;
            }
            for (int k = 0; k < 3; k++) if (_slots[k].Contains(at)) { _crew.EngineerInput(DynamoInput.Activate(k)); return; }
            int c = PxCell(at);
            if (c < 0 || core.RevealState != null) return;
            if (core.Cells[c] != 0) { _dragFrom = c; _dragAt = at; return; }
            if (core.FreePlace > 0) { _crew.EngineerInput(DynamoInput.Refuel(c, core.Free[0])); return; }
            if (core.PortAt(c) != 0) _crew.EngineerInput(DynamoInput.Refuel(c));
        }

        // Hive mode's thumb (the Ratel Raid Lab's): press on a cell to start a chain, drag over touching cells of its colour
        // (back over the last one to drop it), let go to break it; the Leave button ends hive mode.
        void HiveThumb(Vector2 at, bool pressed, bool released, bool held)
        {
            if (pressed && _leaveRect.Contains(at)) { _hiveDrag = false; _crew.HiveInput(3, -1); return; }
            if (pressed && _rect.Contains(at))
            {
                int c = PxCell(at);
                _hiveDrag = c >= 0;
                _hiveLast = c;
                if (c >= 0) _crew.HiveInput(0, c);
                return;
            }
            if (!_hiveDrag) return;
            if (held)
            {
                int c = PxCell(at);
                if (c >= 0 && c != _hiveLast) { _hiveLast = c; _crew.HiveInput(1, c); }
            }
            if (released || !held)
            {
                _hiveDrag = false;
                _hiveLast = -1;
                _crew.HiveInput(2, -1);
            }
        }

        // a drag across several cells: one move per step, along empty cells (Long Reach: one move); capped by the AP held
        static List<DynamoInput> PathTinkers(DynamoCore core, int from, int to)
        {
            var moves = new List<DynamoInput>();
            if (core.Ability == DynamoAbility.LongReach && core.Reach(from)[to]) { moves.Add(DynamoInput.Tinker(from, to)); return moves; }
            var prev = new int[DynamoCore.CellCount];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var q = new List<int> { from };
            prev[from] = -1;
            for (int h = 0; h < q.Count; h++)
                foreach (int v in DynamoCore.Neighbours[q[h]])
                    if (prev[v] == -2 && core.Cells[v] == 0) { prev[v] = q[h]; q.Add(v); }
            if (prev[to] == -2) return moves;
            var path = new List<int>();
            for (int c = to; c != from; c = prev[c]) path.Add(c);
            path.Reverse();
            int at = from, budget = core.Ap + core.BonusAp;
            foreach (int c in path)
            {
                if (moves.Count >= budget) break;
                moves.Add(DynamoInput.Tinker(at, c));
                at = c;
            }
            return moves;
        }

        // ------------------------------------------------------------------ drawing (called by the graphic)

        internal static float Ease(float x) => x <= 0f ? 0f : x >= 1f ? 1f : x * x * (3f - 2f * x);

        internal void Populate(VertexHelper vh)
        {
            var core = _crew.Core;
            if (core == null || _rect.width < 4f) return;
            if (_seatButtonOnly)
            {
                HoneyguideMesh.Quad(vh, _flyRect, Panel);
                HoneyguideMesh.Outline(vh, _flyRect, 2f, Ability);
                return;
            }
            var cfg = _crew.Config;
            var s = _crew.Settings;
            float now = Time.time, ui = _ui;

            HoneyguideMesh.Quad(vh, _rect, Panel);
            HoneyguideMesh.Outline(vh, _rect, 1f, Edge);

            // the time to the next spin: a bar across the top that shrinks from both sides to the centre (no number; Garrett
            // 2026-10-31, the Ratel Raid Lab's bar); it stands still during a reveal. In hive mode it is the hive's time left.
            var rv = core.RevealState;
            var hive = _crew.Hive;
            var hs = _crew.HiveSettings;
            float barH = (_small ? 3f : 5f) * ui;
            float frac = hive != null ? Mathf.Clamp01(1f - (float)(_crew.HiveTime / hs.RaidSeconds)) : Mathf.Clamp01(1f - (float)(core.Clock / s.TickSeconds));
            bool soon = hive == null && rv == null && s.TickSeconds - core.Clock < 1.5;
            HoneyguideMesh.Quad(vh, new Rect(_rect.x, _rect.yMax - barH, _rect.width, barH), BarTrack);
            HoneyguideMesh.Quad(vh, new Rect(_rect.center.x - _rect.width * frac / 2f, _rect.yMax - barH, _rect.width * frac, barH),
                                hive != null ? HiveBar : rv != null ? BarStill : soon ? ApFull : BarRun);
            if (!_small) Strip(vh, cfg);

            // AP pips and the battery
            float pipY = _rect.yMax - 15f * ui, apX = _rect.x + (_small ? 12f : 70f) * ui;
            int bank = s.ApBank + (core.Ability == DynamoAbility.Overclock ? 1 : 0), pips = Mathf.Max(bank, core.Ap) + core.BonusAp;
            for (int k = 0; k < pips; k++)
                HoneyguideMesh.Disc(vh, new Vector2(apX + k * 13f * ui, pipY), 4.5f * ui,
                                    k < core.Ap + core.BonusAp ? (k >= core.Ap ? ApBonus : ApFull) : ApEmpty);
            int cap = core.BatteryCap(s);
            var made = _crew.LastMade;
            float flight = cfg.ChargeFlightSeconds, since = now - made.At;
            bool landing = made.BatterySlot >= 0 && made.BatterySlot < core.Battery.Count && core.Battery[made.BatterySlot] == made.Colour;
            for (int k = 0; k < Mathf.Max(cap, core.Battery.Count); k++)
            {
                var at = BatterySlotPx(k, cap);
                bool mine = landing && k == made.BatterySlot;
                if (k < core.Battery.Count && !(mine && since < flight))
                {
                    // the charge the flying prism became lands with a swell that settles
                    float swell = mine ? 1f + 0.45f * (1f - Ease(Mathf.Clamp01((since - flight) / ChargePop))) : 1f;
                    HoneyguideMesh.Disc(vh, at, 6.5f * ui * swell, cfg.ColourOf(core.Battery[k]));
                }
                else HoneyguideMesh.Ring(vh, at, 5f * ui, 6.5f * ui, 16, 0f, new Color(0.235f, 0.275f, 0.329f, 1f));
            }

            var built = core.Built(s);
            if (hive != null)
            {
                Comb(vh, hive, cfg, now);
                Cards(vh, core, cfg, s, built, now);
                if (!_small && _leaveRect.width > 2f)
                {
                    HoneyguideMesh.Quad(vh, _leaveRect, Panel);
                    HoneyguideMesh.Outline(vh, _leaveRect, 2f, HiveBar);
                }
                FlyButton(vh);
                return;
            }

            // the cells; a matched empty cell (a port standing in for a prism) is outlined white
            var builtCells = new HashSet<int>();
            foreach (var b in built) if (b != null) foreach (int c in b) builtCells.Add(c);
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
            for (int i = 0; i < DynamoCore.CellCount; i++)
            {
                var p = CellPx(i);
                HoneyguideMesh.Hex(vh, p, _size * 0.95f, 0f, i == DynamoCore.Centre && core.Ability == DynamoAbility.WildCore ? WildCell : Cell);
                HoneyguideMesh.HexRing(vh, p, _size * 0.95f, 1f, 0f, builtCells.Contains(i) && core.Cells[i] == 0 ? Color.white : CellEdge);
            }

            // the ports: a ring of their colour, filled faintly, turning together on a spin; fading in after a new pattern
            float spinAge = now - _crew.SpinAt, newAge = now - _crew.PortsAt;
            float spinFrac = cfg.SpinAnim > 0f && spinAge < cfg.SpinAnim && !(newAge < spinAge) ? Ease(1f - spinAge / cfg.SpinAnim) : 0f;
            float rot = Mathf.PI / 3f * spinFrac, fadeIn = Mathf.Clamp01(newAge / 0.8f);
            foreach (var port in core.Ports)
            {
                var p = Rotate(CellPx(port[0]), rot);
                var col = cfg.ColourOf(port[1]);
                float r = _size * 0.95f * (0.6f + 0.4f * fadeIn);
                HoneyguideMesh.Hex(vh, p, r, rot, Alpha(col, (s.PortsCount && core.Cells[port[0]] == 0 ? 0.3f : 0.08f) * fadeIn));
                HoneyguideMesh.HexRing(vh, p, r, Mathf.Max(2f, _size * 0.12f), rot, Alpha(col, 0.95f * fadeIn));
            }
            if (soon && spinFrac <= 0f)
                foreach (var port in core.NextPorts())
                    HoneyguideMesh.Ring(vh, CellPx(port[0]), Mathf.Max(2f, _size * 0.16f) * (0.7f + 0.5f * pulse) - 1f,
                                        Mathf.Max(2f, _size * 0.16f) * (0.7f + 0.5f * pulse) + 1f, 14, 0f, Alpha(cfg.ColourOf(port[1]), 0.8f));

            // the prisms (a made pattern's prisms pulse), the one in the thumb drawn at the thumb
            for (int i = 0; i < DynamoCore.CellCount; i++)
            {
                int c = core.Cells[i];
                if (c == 0 || i == _dragFrom) continue;
                Prism(vh, CellPx(i), _size * 0.62f, cfg.ColourOf(c), builtCells.Contains(i) ? pulse : 0f);
            }
            if (_dragFrom >= 0 && core.Cells[_dragFrom] != 0) Prism(vh, _dragAt, _size * 0.7f, cfg.ColourOf(core.Cells[_dragFrom]), 0f);
            // a free prism rides the cursor in its own domain, so the Ratel sees it is back and what it is (Garrett 2026-10-21)
            else if (!_small && core.FreePlace > 0 && rv == null && _detailSlot < 0 && _rect.Contains(_pointerAt))
            {
                Prism(vh, _pointerAt + new Vector2(_size * 0.55f, -_size * 0.55f), _size * 0.55f, cfg.ColourOf(core.Free[0]), pulse);
                for (int k = 1; k < core.FreePlace; k++)   // the ones after it, small, beside it
                    HoneyguideMesh.Hex(vh, _pointerAt + new Vector2(_size * (0.95f + 0.45f * k), -_size * 0.95f), _size * 0.22f, 0f, cfg.ColourOf(core.Free[k]));
            }

            Cards(vh, core, cfg, s, built, now);
            CrystalGlow(vh, cfg, now);

            // the reveal: the board dims; the labels name the new ability, then the new ports
            if (rv != null) Reveal(vh, rv, s, barH);

            // over the reveal's dimming: a fourth pattern made starts the reveal, and its juice still plays
            MadeJuice(vh, core, cfg, now, cap);

            if (_detailSlot >= 0)
            {
                var d = DetailRect(ui);
                HoneyguideMesh.Quad(vh, new Rect(_rect.x, _slots[0].yMax, _rect.width, _rect.height - _slots[0].height - barH), new Color(0f, 0f, 0f, 0.6f));
                HoneyguideMesh.Quad(vh, d, Card);
                HoneyguideMesh.Outline(vh, d, 2f, cfg.ColourOf(cfg.Patterns[core.Slots[_detailSlot]].family));
            }

            if (ChipShown)
            {
                float lit = ChipUpgraded ? 1f : 0.35f;
                HoneyguideMesh.Quad(vh, _chipRect, new Color(1f, 0.655f, 0f, 0.35f * lit));
                HoneyguideMesh.Outline(vh, _chipRect, 2f, new Color(1f, 0.655f, 0f, lit));
            }
            FlyButton(vh);
        }

        // the Fly button beside the big board
        void FlyButton(VertexHelper vh)
        {
            if (_small || _flyRect.width <= 2f) return;
            HoneyguideMesh.Quad(vh, _flyRect, Panel);
            HoneyguideMesh.Outline(vh, _flyRect, 2f, Ability);
        }

        // the three cards: lit when the board shows the pattern; the shape; one pip per make before it is replaced
        void Cards(VertexHelper vh, DynamoCore core, HoneyguideConfigSO cfg, DynamoSettings s, List<int>[] built, float now)
        {
            float ui = _ui, pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
            bool hive = _crew.InHive;
            for (int k = 0; k < 3 && k < core.Slots.Length; k++)
            {
                int pid = core.Slots[k];
                if (pid < 0 || pid >= cfg.Patterns.Count) continue;
                var pat = cfg.Patterns[pid];
                var r = _slots[k];
                bool lit = built[k] != null && !hive;
                var fam = cfg.ColourOf(pat.family);
                float flash = Mathf.Clamp01(1f - (now - _crew.RetiredAt[k]));
                HoneyguideMesh.Quad(vh, r, lit ? Alpha(fam, 0.22f + 0.2f * pulse) : flash > 0f ? Alpha(Color.white, 0.25f * flash) : Card);
                HoneyguideMesh.Outline(vh, r, lit ? 2f : 1f, lit ? fam : Alpha(fam, 0.45f));
                int uses = s.UsesPerPattern;
                if (uses > 0 && !_small)
                    for (int u = 0; u < uses; u++)
                        HoneyguideMesh.Disc(vh, new Vector2(r.xMax - 7f * ui - (uses - 1 - u) * 8f * ui, r.y + 7f * ui), 2.6f * ui, u < core.Uses[k] ? ApFull : UsePip);
                var cells = HoneyguideConfigSO.ParseShape(pat.shape);
                if (cells.Length == 0) continue;
                float head = _cardHead, sz = Mathf.Min(r.width / 12f, (r.height - head - 6f) / 6.5f);
                float ox = r.center.x, oy = r.y + (r.height - head) / 2f, mq = 0f, mr = 0f;
                foreach (var c in cells) { mq += c[0] + c[1] / 2f; mr += c[1]; }
                mq /= cells.Length; mr /= cells.Length;
                foreach (var c in cells)
                    HoneyguideMesh.Hex(vh, new Vector2(ox + sz * Mathf.Sqrt(3f) * (c[0] + c[1] / 2f - mq), oy - sz * 1.5f * (c[1] - mr)), sz * 0.92f, 0f, cfg.ColourOf(c[2]));
            }
        }

        void Reveal(VertexHelper vh, DynamoCore.Reveal rv, DynamoSettings s, float barH)
        {
            float ui = _ui;
            bool picking = rv.Stage == 1 && rv.Offers != null;
            float total = rv.Stage == 1 ? (float)(picking ? s.PickSeconds : s.RevealAbility) : (float)s.RevealPorts, prog = total > 0f ? Mathf.Clamp01((float)rv.T / total) : 0f;
            if (rv.Stage == 1) HoneyguideMesh.Quad(vh, new Rect(_rect.x, _slots[0].yMax, _rect.width, _rect.height - _slots[0].height - barH), new Color(0f, 0f, 0f, picking ? 0.75f : 0.6f));
            if (picking)   // round 7: the offered abilities' cards, and under them the time left to pick
            {
                for (int k = 0; k < _offerCount; k++)
                {
                    HoneyguideMesh.Quad(vh, _offerRects[k], Card);
                    HoneyguideMesh.Outline(vh, _offerRects[k], 2f * ui, Ability);
                }
                if (_offerCount > 0 && total > 0f)
                {
                    var last = _offerRects[_offerCount - 1];
                    HoneyguideMesh.Quad(vh, new Rect(last.x, last.y - 6f * ui, last.width * prog, 2f * ui), Ability);
                }
            }
            else HoneyguideMesh.Quad(vh, new Rect(_rect.center.x - _rect.width * 0.4f * prog, _cy - _size * 2.6f, _rect.width * 0.8f * prog, 2f * ui), Ability);
        }

        // ------------------------------------------------------------------ hive mode (the Ratel Raid Lab's drawHive)

        // The comb: the other domains' prisms in their colours, a white ring on a shielded one, danger in the danger colour
        // ringed by its domain, our own domain dimmed as the hive's body; the chain in the thumb outlined and joined; a sting
        // flashes the danger cell it touched.
        void Comb(VertexHelper vh, Hive hive, HoneyguideConfigSO cfg, float now)
        {
            int own = _crew.Own;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
            for (int i = 0; i < DynamoCore.CellCount; i++)
            {
                var p = CellPx(i);
                var x = hive.Cells[i];
                HoneyguideMesh.Hex(vh, p, _size * 0.95f, 0f, Cell);
                HoneyguideMesh.HexRing(vh, p, _size * 0.95f, 1f, 0f, CellEdge);
                if (x.Dom == 0) continue;
                var col = cfg.ColourOf(x.Dom);
                if (x.Danger)
                {
                    HoneyguideMesh.Hex(vh, p, _size * 0.8f, 0f, cfg.HiveDangerColour);
                    HoneyguideMesh.HexRing(vh, p, _size * 0.8f, Mathf.Max(2f, _size * 0.14f), 0f, col);
                    continue;
                }
                if (x.Dom == own) HoneyguideMesh.Hex(vh, p, _size * 0.8f, 0f, Alpha(col, 0.35f));
                else Prism(vh, p, _size * 0.62f, col, 0f);
                if (x.Hits >= 2) HoneyguideMesh.HexRing(vh, p, _size * 0.86f, Mathf.Max(2f, _size * 0.12f), 0f, Alpha(Color.white, 0.9f));
            }
            var ch = hive.Chain;
            for (int k = 0; k < ch.Count; k++)
            {
                var p = CellPx(ch[k]);
                HoneyguideMesh.HexRing(vh, p, _size * 0.95f, Mathf.Max(2f, _size * 0.16f), 0f, Alpha(Color.white, 0.7f + 0.3f * pulse));
                if (k > 0) HoneyguideMesh.Line(vh, CellPx(ch[k - 1]), p, Mathf.Max(2f, _size * 0.18f), Color.white);
            }
            float sting = 1f - Mathf.Clamp01((now - _crew.StungAt) / 0.6f);
            if (sting > 0f && _crew.StungCell >= 0)
            {
                HoneyguideMesh.HexRing(vh, CellPx(_crew.StungCell), _size * (0.95f + 0.6f * (1f - sting)), Mathf.Max(2f, _size * 0.2f), 0f, Alpha(cfg.HiveDangerColour, sting));
                HoneyguideMesh.Outline(vh, _rect, 3f * _ui * sting, Alpha(cfg.HiveDangerColour, sting));
            }
            float open = 1f - Mathf.Clamp01((now - _crew.HiveAt) / 0.8f);   // the hive opening: the board's edge flashes amber
            if (open > 0f) HoneyguideMesh.Outline(vh, _rect, 4f * _ui * open, Alpha(HiveBar, open));
        }

        // After hive mode: the planar crystal the rewards landed on lights, the cells they landed in flash, and the cells it
        // goes on to show the colour each wants - so the crystal's pattern teaches itself (Garrett 2026-10-31).
        void CrystalGlow(VertexHelper vh, HoneyguideConfigSO cfg, float now)
        {
            float g = cfg.CrystalGlowSeconds > 0f ? 1f - (now - _crew.GlowAt) / cfg.CrystalGlowSeconds : 0f;
            if (g <= 0f) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 10f);
            foreach (int i in _crew.GlowCells)
                HoneyguideMesh.HexRing(vh, CellPx(i), _size * 0.95f, Mathf.Max(2f, _size * 0.12f), 0f, Alpha(Color.white, g * (0.5f + 0.5f * pulse)));
            foreach (int i in _crew.GlowLanded)
                HoneyguideMesh.Hex(vh, CellPx(i), _size * 0.95f, 0f, Alpha(Color.white, 0.5f * g));
            foreach (var n in _crew.GlowNext)
                HoneyguideMesh.Hex(vh, CellPx(n[0]), _size * 0.5f, 0f, Alpha(cfg.ColourOf(n[1]), 0.35f * g));
        }

        // The strip: the Honeyguide flies to the right along a track centred in the strip (Garrett 2026-11-02: "it should go
        // to the right not the left and be more centered"); the nearest omnicrystal, a diamond, waits at the track's right
        // end, and the Honeyguide's disc stands back from it by its distance (the far end is stripRangeU). In hive mode the
        // hive, a hex, sits at the right end with the hull beside it. Its distance is the text at the right.
        void Strip(VertexHelper vh, HoneyguideConfigSO cfg)
        {
            var r = _stripRect;
            if (r.width < 4f) return;
            float ui = _ui, cy = r.center.y, x0 = r.x + r.width * 0.24f, x1 = Mathf.Min(r.x + r.width * 0.72f, r.xMax - _stripTextW - 14f * ui);
            if (x1 - x0 < 30f * ui) x0 = Mathf.Max(r.x + 8f * ui, x1 - 30f * ui);   // a long line: the track gives way to it
            if (x1 <= x0) return;
            HoneyguideMesh.Quad(vh, new Rect(x0, cy - 1f * ui, x1 - x0, 2f * ui), StripTrack);
            if (_crew.InHive)
            {
                var hull = new Vector2(x1 - 12f * ui, cy);
                HoneyguideMesh.Hex(vh, new Vector2(x1, cy), 6f * ui, 0f, HiveBar);
                HoneyguideMesh.Disc(vh, hull, 5f * ui, Ability);
                HoneyguideMesh.Ring(vh, hull, 7f * ui, 8.5f * ui, 16, 0f, Alpha(Color.white, 0.8f));   // invulnerable
                return;
            }
            float d = _crew.StripDistance;
            if (d < 0f) { HoneyguideMesh.Disc(vh, new Vector2(x0, cy), 5f * ui, Ability); return; }
            float f = Mathf.Clamp01(d / Mathf.Max(1f, cfg.StripRangeU));
            HoneyguideMesh.Poly(vh, new Vector2(x1, cy), 6f * ui, 4, Mathf.PI / 2f, Color.white);
            HoneyguideMesh.Disc(vh, new Vector2(Mathf.Lerp(x1 - 12f * ui, x0, f), cy), 5f * ui, Ability);
        }

        const float ChargePop = 0.35f;   // seconds the landed charge swells and its ring spreads

        Vector2 BatterySlotPx(int k, int cap) =>
            new(_rect.xMax - 12f * _ui - cap * 18f * _ui + k * 18f * _ui + 8f * _ui, _rect.yMax - 15f * _ui);

        // A made pattern (Garrett 2026-10-23: "the ratel needs juice when it completes a pattern. the prism that is taken and
        // placed in the battery should have an animation that morphs one to the other"). Its cells burst one after another
        // in the card's family colour, its card pops, the board's edge flashes, and the prism it took lifts off its cell and
        // flies on an arc to its battery slot, turning from the board's hex prism into the battery's round charge, where it
        // lands with a ring. Drawn on both boards, the big one and the small one while flying.
        void MadeJuice(VertexHelper vh, DynamoCore core, HoneyguideConfigSO cfg, float now, int cap)
        {
            var made = _crew.LastMade;
            float since = now - made.At, juice = cfg.MadeJuiceSeconds, flight = cfg.ChargeFlightSeconds, ui = _ui;
            if (since < 0f || since >= Mathf.Max(juice, flight + ChargePop)) return;
            var fam = made.Pid >= 0 && made.Pid < cfg.Patterns.Count ? cfg.ColourOf(cfg.Patterns[made.Pid].family) : Color.white;

            if (juice > 0f && since < juice)
            {
                float t = since / juice, fade = 1f - t;
                HoneyguideMesh.Outline(vh, _rect, (1f + 3f * fade) * ui, Alpha(fam, fade));

                // the cells, one after another (the taken prism's first), each a white flash and a ring spreading out
                float step = juice * 0.35f / Mathf.Max(1, made.Cells.Count);
                for (int n = 0, order = 0; n < made.Cells.Count; n++)
                {
                    int c = made.Cells[n];
                    float u = Mathf.Clamp01((since - step * (c == made.Cell ? 0 : ++order)) / (juice * 0.65f));
                    if (u <= 0f || u >= 1f) continue;
                    var p = CellPx(c);
                    HoneyguideMesh.Hex(vh, p, _size * 0.95f, 0f, Alpha(Color.white, 0.55f * (1f - u)));
                    HoneyguideMesh.HexRing(vh, p, _size * (0.95f + 0.75f * Ease(u)), Mathf.Max(1.5f, _size * 0.14f * (1f - u)), 0f, Alpha(fam, 1f - u));
                }

                // the card: a white flash, and its outline spreading out from it
                if (made.Slot >= 0 && made.Slot < 3)
                {
                    var r = _slots[made.Slot];
                    float grow = 10f * ui * Ease(t);
                    HoneyguideMesh.Quad(vh, r, Alpha(Color.white, 0.4f * fade * fade));
                    HoneyguideMesh.Outline(vh, new Rect(r.x - grow, r.y - grow, r.width + 2f * grow, r.height + 2f * grow), Mathf.Max(1f, 3f * ui * fade), Alpha(fam, fade));
                }
            }

            if (made.Cell < 0 || made.Cell >= DynamoCore.CellCount) return;
            var col = cfg.ColourOf(made.Colour);
            float rHex = _size * 0.62f, rDisc = 6.5f * ui;
            var from = CellPx(made.Cell);
            bool landed = made.BatterySlot >= 0 && made.BatterySlot < core.Battery.Count && core.Battery[made.BatterySlot] == made.Colour;
            if (flight > 0f && since < flight)
            {
                float f = since / flight;
                if (!landed)   // no slot holds it (an effect already spent it): it rises off its cell and fades
                {
                    var up = from + new Vector2(0f, _size * 1.5f * Ease(f));
                    HoneyguideMesh.HexToDisc(vh, up, rHex, rDisc, Ease(f), 0f, Alpha(col, 1f - f));
                    return;
                }
                var to = BatterySlotPx(made.BatterySlot, cap);
                // a short lift (it swells on its cell), then the flight; a few fading copies trail it
                for (int g = 3; g >= 0; g--)
                {
                    float fg = f - 0.07f * g;
                    if (fg < 0f) continue;
                    float lift = Mathf.Clamp01(fg / 0.2f), e = Ease(Mathf.Clamp01((fg - 0.12f) / 0.88f));
                    var at = FlightPoint(from, to, e);
                    float swell = 1f + 0.25f * Mathf.Sin(Mathf.PI * lift) * (1f - e);
                    var body = g == 0 ? col : Alpha(col, 0.35f / g);
                    HoneyguideMesh.HexToDisc(vh, at, rHex * swell, rDisc, e, Mathf.PI / 3f * e, body);
                    if (g == 0)   // the prism's highlight, gone by the time it is a charge
                        HoneyguideMesh.HexToDisc(vh, at + new Vector2(-rHex * 0.2f, rHex * 0.2f) * (1f - e), rHex * 0.35f, rDisc * 0.3f, e,
                                                 Mathf.PI / 3f * e, new Color(1f, 1f, 1f, 0.35f * (1f - e)));
                }
                return;
            }
            float pop = (since - flight) / ChargePop;
            if (landed && pop >= 0f && pop < 1f)
            {
                var at = BatterySlotPx(made.BatterySlot, cap);
                float rOut = rDisc * (1f + 1.6f * Ease(pop));
                HoneyguideMesh.Ring(vh, at, Mathf.Max(0f, rOut - 2.5f * ui * (1f - pop)), rOut, 20, 0f, Alpha(col, 1f - pop));
            }
        }

        // the charge's path: an arc bowed to the side, so it reads as thrown rather than slid
        Vector2 FlightPoint(Vector2 from, Vector2 to, float e)
        {
            var d = to - from;
            var side = new Vector2(-d.y, d.x) * 0.22f;
            if (side.y < 0f) side = -side;   // bow upward on the screen
            return Vector2.Lerp(from, to, e) + side * Mathf.Sin(Mathf.PI * e);
        }

        Vector2 Rotate(Vector2 p, float a)
        {
            float dx = p.x - _cx, dy = p.y - _cy, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(_cx + dx * c - dy * s, _cy + dx * s + dy * c);
        }

        static Color Alpha(Color c, float a) => new(c.r, c.g, c.b, c.a * a);

        static void Prism(VertexHelper vh, Vector2 at, float r, Color col, float glow)
        {
            if (glow > 0f) HoneyguideMesh.Hex(vh, at, r * (1.25f + 0.15f * glow), 0f, Alpha(col, 0.25f + 0.25f * glow));
            HoneyguideMesh.Hex(vh, at, r, 0f, col);
            HoneyguideMesh.Hex(vh, at + new Vector2(-r * 0.2f, r * 0.2f), r * 0.35f, 0f, new Color(1f, 1f, 1f, 0.35f));
        }
    }

    /// <summary>The Ratel's board, drawn as one mesh by <see cref="HoneyguideDynamoView.Populate"/> (no sprites or fonts).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class HoneyguideBoardGraphic : MaskableGraphic
    {
        internal HoneyguideDynamoView View;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            View?.Populate(vh);
        }
    }

    /// <summary>Flat UI shapes in canvas pixels (the canvas has no scaler, so canvas units are screen pixels).</summary>
    static class HoneyguideMesh
    {
        static UIVertex V(Vector2 p, Color c) => new() { position = p, color = c, uv0 = Vector4.zero };

        internal static void Quad(VertexHelper vh, Rect r, Color c)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            int i = vh.currentVertCount;
            vh.AddVert(V(new Vector2(r.xMin, r.yMin), c));
            vh.AddVert(V(new Vector2(r.xMin, r.yMax), c));
            vh.AddVert(V(new Vector2(r.xMax, r.yMax), c));
            vh.AddVert(V(new Vector2(r.xMax, r.yMin), c));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        internal static void Outline(VertexHelper vh, Rect r, float w, Color c)
        {
            Quad(vh, new Rect(r.xMin, r.yMin, r.width, w), c);
            Quad(vh, new Rect(r.xMin, r.yMax - w, r.width, w), c);
            Quad(vh, new Rect(r.xMin, r.yMin, w, r.height), c);
            Quad(vh, new Rect(r.xMax - w, r.yMin, w, r.height), c);
        }

        // a regular polygon; a pointy-top hexagon is 6 sides at -30 degrees + rot (the lab's hexPath, y up)
        internal static void Poly(VertexHelper vh, Vector2 at, float r, int sides, float rot, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(V(at, c));
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2f * k / sides + rot;
                vh.AddVert(V(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, c));
            }
            for (int k = 0; k < sides; k++) vh.AddTriangle(i, i + 1 + k, i + 1 + (k + 1) % sides);
        }

        internal static void Ring(VertexHelper vh, Vector2 at, float rIn, float rOut, int sides, float rot, Color c)
        {
            int i = vh.currentVertCount;
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2f * k / sides + rot;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(V(at + d * rIn, c));
                vh.AddVert(V(at + d * rOut, c));
            }
            for (int k = 0; k < sides; k++)
            {
                int a0 = i + 2 * k, a1 = i + 2 * ((k + 1) % sides);
                vh.AddTriangle(a0, a0 + 1, a1 + 1);
                vh.AddTriangle(a0, a1 + 1, a1);
            }
        }

        const float HexRot = Mathf.PI / 6f;   // pointy-top: corners at 30 + 60k degrees

        internal static void Hex(VertexHelper vh, Vector2 at, float r, float rot, Color c) => Poly(vh, at, r, 6, HexRot + rot, c);

        internal static void HexRing(VertexHelper vh, Vector2 at, float r, float w, float rot, Color c) =>
            Ring(vh, at, Mathf.Max(0f, r - w), r, 6, HexRot + rot, c);

        internal static void Disc(VertexHelper vh, Vector2 at, float r, Color c) => Poly(vh, at, r, 14, 0f, c);

        internal static void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var n = new Vector2(-d.y, d.x).normalized * (w / 2f);
            int i = vh.currentVertCount;
            vh.AddVert(V(a - n, c));
            vh.AddVert(V(a + n, c));
            vh.AddVert(V(b + n, c));
            vh.AddVert(V(b - n, c));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        // A pointy-top hexagon of radius rHex turning into a disc of radius rDisc as m runs 0 to 1: every rim point's radius
        // blends from the hexagon's edge at that angle to the circle (24 rim points, so the corners are exact at m = 0).
        internal static void HexToDisc(VertexHelper vh, Vector2 at, float rHex, float rDisc, float m, float rot, Color c)
        {
            const int sides = 24;
            int i = vh.currentVertCount;
            vh.AddVert(V(at, c));
            float apothem = rHex * Mathf.Cos(HexRot), sector = Mathf.PI / 3f;
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2f * k / sides;   // from an edge's midpoint (a pointy-top hexagon's are at 0, 60, ... degrees)
                float d = Mathf.Repeat(a + sector / 2f, sector) - sector / 2f;
                float r = Mathf.Lerp(apothem / Mathf.Cos(d), rDisc, m);
                vh.AddVert(V(at + new Vector2(Mathf.Cos(a + rot), Mathf.Sin(a + rot)) * r, c));
            }
            for (int k = 0; k < sides; k++) vh.AddTriangle(i, i + 1 + k, i + 1 + (k + 1) % sides);
        }
    }
}

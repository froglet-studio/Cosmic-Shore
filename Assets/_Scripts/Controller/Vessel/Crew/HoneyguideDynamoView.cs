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
    /// view in <c>pilotRect</c>; a double tap on it swaps seats, and a chip above it shows what RT and LT do now.</para>
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

        readonly HoneyguideCrew _crew;
        readonly GameObject _root;
        readonly HoneyguideBoardGraphic _graphic;
        readonly TMP_Text _seat, _abilityName, _abilityText, _free, _revealTitle, _revealText, _detail, _chip, _fly;
        readonly TMP_Text[] _slotNames = new TMP_Text[3], _slotTaps = new TMP_Text[3];

        // this frame's layout, in canvas pixels (y up)
        Rect _rect, _apRect, _chipRect, _flyRect;
        readonly Rect[] _slots = new Rect[3];
        float _size, _cx, _cy;
        bool _small;

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

        internal HoneyguideDynamoView(HoneyguideCrew crew)
        {
            _crew = crew;
            _root = new GameObject("HoneyguideDynamoView", typeof(RectTransform));
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19000;   // above the gameplay HUD, below the Arkway banner (20000) and the load veil (30000)
            // No GraphicRaycaster: the board reads the pointer itself, so it never eats a tap meant for the HUD.

            var gGo = new GameObject("Board", typeof(RectTransform));
            gGo.transform.SetParent(_root.transform, false);
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
            for (int k = 0; k < 3; k++)
            {
                _slotNames[k] = MakeText("Slot" + k, new Color(0.91f, 0.93f, 0.95f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
                _slotTaps[k] = MakeText("Tap" + k, Color.white, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            }
        }

        internal void Dispose()
        {
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
            go.transform.SetParent(_root.transform, false);
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
            var f = _small ? _crew.Config.PilotRect : _crew.Config.EngineerRect;
            float w = Screen.width, h = Screen.height;
            _rect = new Rect(f.x * w, f.y * h, f.width * w, f.height * h);
            float ui = Mathf.Max(0.6f, h / 900f);
            float pad = 6f * ui, top = (_small ? 30f : 46f) * ui;
            float slotsH = _small ? Mathf.Max(34f * ui, _rect.height * 0.2f) : Mathf.Max(64f * ui, Mathf.Min(110f * ui, _rect.height * 0.17f));
            float bw = _rect.width - 2f * pad, bh = _rect.height - top - slotsH - 2f * pad;
            _size = Mathf.Max(6f, Mathf.Min(bw / (7f * Mathf.Sqrt(3f)), bh / 11f));
            _cx = _rect.center.x;
            _cy = _rect.yMax - top - pad - bh / 2f;
            float gap = 6f * ui, sw = (_rect.width - 2f * pad - gap * 2f) / 3f;
            for (int k = 0; k < 3; k++) _slots[k] = new Rect(_rect.x + pad + k * (sw + gap), _rect.y + pad / 2f, sw, slotsH);
            _apRect = new Rect(_rect.x, _rect.yMax - 28f * ui, Mathf.Max(80f * ui, _rect.width * 0.45f), 28f * ui);
            _chipRect = _small ? new Rect(_rect.x, _rect.yMax + 6f * ui, _rect.width, 52f * ui) : default;
            // The Fly button: a thumb-sized square just left of the big board, at its top (no scaler on this canvas: pixels).
            float mm = Screen.dpi > 0f ? Screen.dpi / 25.4f : 6f, bs = Mathf.Max(44f * ui, _crew.Config.FlyButtonMillimetres * mm);
            _flyRect = _small ? default : new Rect(Mathf.Max(4f * ui, _rect.x - bs - 8f * ui), _rect.yMax - bs, bs, bs);
        }

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
            var cfg = _crew.Config;
            float ui = Mathf.Max(0.6f, Screen.height / 900f), fs = Mathf.Clamp(_rect.width / 30f, 10f * ui, 15f * ui);

            Place(_seat, new Rect(_rect.x + 10f * ui, _rect.yMax - 28f * ui, 120f * ui, 26f * ui), fs, _small ? "" : cfg.RatelLabel);
            var ab = cfg.AbilityText(core.Ability);
            float abY = _rect.yMax - 46f * ui;
            Place(_abilityName, new Rect(_rect.x + 10f * ui, abY, _rect.width * 0.3f, 18f * ui), fs * 0.9f, _small || ab == null ? "" : ab.name);
            Place(_abilityText, new Rect(_rect.x + 12f * ui + _rect.width * 0.3f, abY, _rect.width * 0.7f - 24f * ui, 18f * ui), fs * 0.8f,
                  _small || ab == null ? "" : ab.text);

            string free = !_small && core.FreePlace > 0 && core.RevealState == null
                ? TemplateText.Fill(cfg.FreePrismTemplate, "count", core.FreePlace.ToString(), "colour", cfg.ColourName(core.Free[0])) : "";
            Place(_free, new Rect(_rect.x + 8f * ui, _cy - _size * 5.6f, _rect.width - 16f * ui, 20f * ui), fs * 0.85f, free);

            var built = core.Built(_crew.Settings);
            for (int k = 0; k < 3; k++)
            {
                int pid = k < core.Slots.Length ? core.Slots[k] : -1;
                var r = _slots[k];
                string name = !_small && pid >= 0 && pid < cfg.Patterns.Count ? cfg.Patterns[pid].name : "";
                Place(_slotNames[k], new Rect(r.x + 5f * ui, r.yMax - 18f * ui, r.width - 10f * ui, 16f * ui), Mathf.Clamp(r.width / 10f, 9f * ui, 12f * ui), name);
                bool lit = built.Length > k && built[k] != null && core.RevealState == null;
                Place(_slotTaps[k], new Rect(r.x + 5f * ui, r.y + 2f * ui, r.width * 0.5f, 16f * ui), Mathf.Clamp(r.width / 10f, 9f * ui, 12f * ui),
                      lit && !_small ? cfg.TapToMake : "");
            }

            var rv = core.RevealState;
            string title = "", body = "";
            if (rv != null && rv.Stage == 1 && ab != null) { title = TemplateText.Fill(cfg.RevealAbilityTemplate, "name", ab.name); body = _small ? "" : ab.text; }
            else if (rv != null && rv.Stage == 2) title = cfg.RevealPortsLine;
            Place(_revealTitle, new Rect(_rect.x + 10f * ui, _cy + _size * 0.6f, _rect.width - 20f * ui, 30f * ui), (_small ? 0.75f : 1.25f) * fs, title);
            Place(_revealText, new Rect(_rect.x + 18f * ui, _cy - _size * 2.2f, _rect.width - 36f * ui, _size * 2.6f), fs, body);

            // a right-clicked card's details, over the board
            if (_small || rv != null || _detailSlot >= core.Slots.Length || (_detailSlot >= 0 && core.Slots[_detailSlot] < 0)) _detailSlot = -1;
            string detail = "";
            if (_detailSlot >= 0 && core.Slots[_detailSlot] < cfg.Patterns.Count)
            {
                var pat = cfg.Patterns[core.Slots[_detailSlot]];
                detail = TemplateText.Fill(cfg.CardDetailTemplate, "name", pat.name, "text", pat.effectText,
                                           "uses", core.Uses[_detailSlot].ToString(), "max", cfg.UsesPerPattern.ToString());
            }
            Place(_detail, DetailRect(ui), fs * 1.15f, detail);

            // the triggers, beside the small board while the human flies: RT and LT, each its base power or the upgrade on it
            string chip = "";
            ChipUpgraded = false;
            if (_small)
            {
                for (int k = 0; k < 2; k++)
                {
                    var u = _crew.Upgrades[k];
                    ChipUpgraded |= u != null;
                    string power = u == null ? cfg.BasePowerName(k)
                        : TemplateText.Fill(cfg.UpgradePowerTemplate, "name", u.Name, "level", u.Level > 1 ? HoneyguideCrew.Roman(u.Level) : "",
                                            "seconds", Mathf.CeilToInt(Mathf.Max(0f, u.Left)).ToString());
                    string line = TemplateText.Fill(cfg.TriggerChipTemplate, "trigger", cfg.TriggerName(k), "power", power);
                    if (line.Length == 0) continue;
                    chip = chip.Length > 0 ? chip + "\n" + line : line;
                }
            }
            Place(_chip, _chipRect, fs, chip);
            Place(_fly, _flyRect, fs * 1.1f, _small ? "" : cfg.FlyButtonLabel);
            ChipShown = chip.Length > 0;

            if (_small) ReadSmallViewTaps();
            _graphic.SetVerticesDirty();
        }

        internal bool ChipShown { get; private set; }

        /// <summary>True while an upgrade holds either trigger (the chip lights up).</summary>
        internal bool ChipUpgraded { get; private set; }

        Rect DetailRect(float ui) => new(_rect.x + _rect.width * 0.1f, _cy - _size * 3.5f, _rect.width * 0.8f, _size * 7f);

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
            if (!_rect.Contains(at)) return;
            if (Time.unscaledTime - _lastSmallTap < 0.35f) { _smallDoubleTap = true; _lastSmallTap = -99f; }
            else _lastSmallTap = Time.unscaledTime;
        }

        /// <summary>True once after a seat swap was asked for on the board: a double tap on the small view while flying, or the
        /// Fly button beside the big board while playing the Ratel (a phone has no D-pad and no 1 / 2 keys).</summary>
        internal bool SmallViewDoubleTapped()
        {
            bool d = _smallDoubleTap || _flyTapped;
            _smallDoubleTap = _flyTapped = false;
            return d;
        }

        internal void CancelDrag() => _dragFrom = -1;

        // Playing the Ratel: the lab's engDown / up. Layout is last frame's (the board does not move between frames).
        internal void ReadEngineerInput(DynamoCore core, DynamoSettings s)
        {
            if (_small || !TryPointer(out var at, out bool pressed, out bool released, out bool held, out bool touch)) return;
            _pointerAt = at;
            if (_dragFrom >= 0) _dragAt = at;
            var mouse = Mouse.current;
            bool secondary = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if ((pressed || secondary) && _detailSlot >= 0) { _detailSlot = -1; return; }   // any click closes the card details
            if (pressed && _flyRect.Contains(at)) { _flyTapped = true; return; }            // back to flying
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
            for (int k = 0; k < 3; k++) if (_slots[k].Contains(at)) { _crew.EngineerInput(DynamoInput.Activate(k)); return; }
            int c = PxCell(at);
            if (c < 0 || core.RevealState != null) return;
            if (core.Cells[c] != 0) { _dragFrom = c; _dragAt = at; return; }
            if (core.FreePlace > 0) { _crew.EngineerInput(DynamoInput.Refuel(c, core.Free[0])); return; }
            if (core.PortAt(c) != 0) _crew.EngineerInput(DynamoInput.Refuel(c));
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
            var cfg = _crew.Config;
            var s = _crew.Settings;
            float now = Time.time, ui = Mathf.Max(0.6f, Screen.height / 900f);

            HoneyguideMesh.Quad(vh, _rect, Panel);
            HoneyguideMesh.Outline(vh, _rect, 1f, Edge);

            // the time to the next spin: a bar across the top that shrinks (no number); it stands still during a reveal
            var rv = core.RevealState;
            float barH = (_small ? 3f : 5f) * ui, frac = Mathf.Clamp01(1f - (float)(core.Clock / s.TickSeconds));
            bool soon = rv == null && s.TickSeconds - core.Clock < 1.5;
            HoneyguideMesh.Quad(vh, new Rect(_rect.x, _rect.yMax - barH, _rect.width, barH), BarTrack);
            HoneyguideMesh.Quad(vh, new Rect(_rect.x, _rect.yMax - barH, _rect.width * frac, barH), rv != null ? BarStill : soon ? ApFull : BarRun);

            // AP pips and the battery
            float pipY = _rect.yMax - 15f * ui, apX = _rect.x + (_small ? 12f : 70f) * ui;
            int bank = s.ApBank + (core.Ability == DynamoAbility.Overclock ? 1 : 0), pips = Mathf.Max(bank, core.Ap) + core.BonusAp;
            for (int k = 0; k < pips; k++)
                HoneyguideMesh.Disc(vh, new Vector2(apX + k * 13f * ui, pipY), 4.5f * ui,
                                    k < core.Ap + core.BonusAp ? (k >= core.Ap ? ApBonus : ApFull) : ApEmpty);
            int cap = core.BatteryCap(s);
            float bx = _rect.xMax - 12f * ui - cap * 18f * ui;
            for (int k = 0; k < Mathf.Max(cap, core.Battery.Count); k++)
            {
                var at = new Vector2(bx + k * 18f * ui + 8f * ui, pipY);
                if (k < core.Battery.Count) HoneyguideMesh.Disc(vh, at, 6.5f * ui, cfg.ColourOf(core.Battery[k]));
                else HoneyguideMesh.Ring(vh, at, 5f * ui, 6.5f * ui, 16, 0f, new Color(0.235f, 0.275f, 0.329f, 1f));
            }

            // the cells; a matched empty cell (a port standing in for a prism) is outlined white
            var built = core.Built(s);
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

            // the three cards: lit when the board shows the pattern; the shape; one pip per make before it is replaced
            for (int k = 0; k < 3 && k < core.Slots.Length; k++)
            {
                int pid = core.Slots[k];
                if (pid < 0 || pid >= cfg.Patterns.Count) continue;
                var pat = cfg.Patterns[pid];
                var r = _slots[k];
                bool lit = built[k] != null;
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
                float head = _small ? 2f : 18f * ui, sz = Mathf.Min(r.width / 12f, (r.height - head - 6f) / 6.5f);
                float ox = r.center.x, oy = r.y + (r.height - head) / 2f, mq = 0f, mr = 0f;
                foreach (var c in cells) { mq += c[0] + c[1] / 2f; mr += c[1]; }
                mq /= cells.Length; mr /= cells.Length;
                foreach (var c in cells)
                    HoneyguideMesh.Hex(vh, new Vector2(ox + sz * Mathf.Sqrt(3f) * (c[0] + c[1] / 2f - mq), oy - sz * 1.5f * (c[1] - mr)), sz * 0.92f, 0f, cfg.ColourOf(c[2]));
            }

            // the reveal: the board dims; the labels name the new ability, then the new ports
            if (rv != null)
            {
                float total = rv.Stage == 1 ? (float)s.RevealAbility : (float)s.RevealPorts, prog = total > 0f ? Mathf.Clamp01((float)rv.T / total) : 0f;
                if (rv.Stage == 1) HoneyguideMesh.Quad(vh, new Rect(_rect.x, _slots[0].yMax, _rect.width, _rect.height - _slots[0].height - barH), new Color(0f, 0f, 0f, 0.6f));
                HoneyguideMesh.Quad(vh, new Rect(_rect.center.x - _rect.width * 0.4f * prog, _cy - _size * 2.6f, _rect.width * 0.8f * prog, 2f * ui), Ability);
            }

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

            // the Fly button beside the big board
            if (!_small && _flyRect.width > 2f)
            {
                HoneyguideMesh.Quad(vh, _flyRect, Panel);
                HoneyguideMesh.Outline(vh, _flyRect, 2f, Ability);
            }
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
    }
}

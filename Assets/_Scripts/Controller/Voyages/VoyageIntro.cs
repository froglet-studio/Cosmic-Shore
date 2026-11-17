using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The voyage's opening, played (<see cref="VoyageIntroSO"/>; Frogma Rev 114, Intro A "Departure", picked by Shombith
    /// 2026-11-16): letterbox bars come in, the camera finds the Ark wide and then pushes along its hull (a small caption),
    /// drops in on the pilot's own vessel with its name card, then each wingmate with theirs, then shows the title, the
    /// goal and the route drawn in as a line of stops, and finally swings in behind the pilot as GO comes up, the bars
    /// slide away and the stick comes back. The first time a pilot sees an intro it plays through; after that holding B
    /// (Escape on a keyboard, a held touch on a phone) skips straight to the hand-over.
    ///
    /// <para>It owns only the camera and the words. Whoever starts it keeps the pilot's vessel safe meanwhile (the run
    /// flies it on its escort AI) and takes the stick back in <c>onDone</c>, which runs exactly once however the intro
    /// ends: finished, skipped, stopped, or its scene torn down.</para>
    ///
    /// <para>The camera is the shared manual replay rig (<see cref="CameraManager.BeginManualReplayCamera"/>, as the
    /// Astro League goal replay and the spectator dolly use it), so the speed tunnel and the occlusion corridor are held
    /// while it flies and lifted by <see cref="CameraManager.RestoreGameplayCamera"/>.</para>
    /// </summary>
    public sealed class VoyageIntro : MonoBehaviour
    {
        const int SortingOrder = 25000;      // above the voyage HUD's banner (20000), below the load veil (30000)
        const float FrameDamping = 3f;       // how quickly a shot's frame follows a turning subject
        const float MinSubjectSize = 3f;
        const float MaxSubjectSize = 120f;
        const float FallbackHullSize = 10f;
        const string SeenKeyPrefix = "voyage.intro.seen.";

        // Rev 114's card and route geometry, on the 1920 x 1080 frame.
        const float CardWidth = 300f, CardHeight = 64f, CardGap = 16f, CardFace = 48f;
        const float RouteWidth = 260f, RouteDot = 12f;

        /// <summary>Raised true as an intro starts and false as it ends (the voyage scene hides its flight HUD).</summary>
        public static event System.Action<bool> Playing;

        /// <summary>True while an intro is on screen.</summary>
        public static bool IsPlaying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Playing = null;
            IsPlaying = false;
        }

        /// <summary>One vessel of the squad, as the intro shows it.</summary>
        public struct Member
        {
            public Transform Hull;
            public VesselClassType Vessel;
            public int Seat;
            public bool IsYou;
            /// <summary>The seat's loadout (its pilot and element), for the name card.</summary>
            public SquadSlot Slot;
            /// <summary>A party friend flying this seat: their player name (empty for an AI or the player).</summary>
            public string Friend;
        }

        /// <summary>Who and what the intro shows, and the voyage-wide tokens its words use.</summary>
        public sealed class Cast
        {
            public Ark Ark;
            /// <summary>The Ark's size: half its hull's length.</summary>
            public float ArkSize = 55f;
            /// <summary>The squad in seat order; the player's own seat is shown first (its own shot), then the wingmates.</summary>
            public readonly List<Member> Squad = new();
            /// <summary>Token pairs every line can use ("voyage", "ARK ESCORT", "legs", "5", ...).</summary>
            public readonly List<string> Tokens = new();
            /// <summary>Called once, as the reel's launch shot starts (<see cref="VoyageIntroSO.Shot.Launch"/>): the squad
            /// leaves the hangar (<see cref="ArkSortie.Launch"/>).</summary>
            public System.Action Launch;
        }

        VoyageIntroSO _script;
        Cast _cast;
        System.Action _onDone;
        CancellationTokenSource _cts;
        bool _done, _launched;

        Transform _rig;
        VoyageKit _kit;
        VoyageLines _lines;
        PilotArtifactCatalogSO _catalog;
        CanvasGroup _bars, _caption, _titleGroup, _cards, _go, _hint;
        RectTransform _topBar, _bottomBar, _cardRow, _route;
        TMP_Text _captionText, _title, _line, _goText, _hintText;
        RectTransform _skipFill;
        readonly List<Image> _routeDots = new();
        float _skipHeld;
        bool _skipAllowed, _skipArmed, _skipped;
        float _startedAt;
        Color _ink, _paper;

        /// <summary>
        /// Play <paramref name="script"/> over <paramref name="cast"/>. Returns null (and calls <paramref name="onDone"/>
        /// at once) when there is nothing to play or no camera to play it on.
        /// </summary>
        public static VoyageIntro Play(VoyageIntroSO script, Cast cast, Transform parent, System.Action onDone)
        {
            if (!script || script.Shots.Count == 0 || cast == null || !cast.Ark || !CameraManager.Instance)
            {
                if (script && cast?.Ark && !CameraManager.Instance)
                    CSDebug.LogWarning("[VoyageIntro] No camera manager - the voyage opens without its intro.");
                onDone?.Invoke();
                return null;
            }
            var go = new GameObject("VoyageIntro");
            go.transform.SetParent(parent, false);
            var intro = go.AddComponent<VoyageIntro>();
            intro._script = script;
            intro._cast = cast;
            intro._onDone = onDone;
            intro._cts = new CancellationTokenSource();
            intro.RunAsync(intro._cts.Token).Forget();
            return intro;
        }

        /// <summary>End the intro now, camera and stick handed back at once (the voyage ended, or was left).</summary>
        public void Stop()
        {
            if (_done) return;
            _cts?.Cancel();
            Finish();
        }

        /// <summary>Whether a pilot who has seen the intro <paramref name="seenBefore"/> may skip it: always, unless the
        /// intro plays through the first time.</summary>
        public static bool MaySkip(bool skippableAfterFirstRun, bool seenBefore) => !skippableAfterFirstRun || seenBefore;

        static string SeenKey(VoyageIntroSO script) => SeenKeyPrefix + (script ? script.name : string.Empty);

        static bool Seen(VoyageIntroSO script)
        {
            try { return PlayerPrefs.GetInt(SeenKey(script), 0) == 1; }
            catch (System.Exception) { return false; }
        }

        static void MarkSeen(VoyageIntroSO script)
        {
            try
            {
                PlayerPrefs.SetInt(SeenKey(script), 1);
                PlayerPrefs.Save();
            }
            catch (System.Exception) { /* the next intro plays through again: harmless */ }
        }

        // ── The reel ─────────────────────────────────────────────────────────

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                var cam = CameraManager.Instance;
                var close = cam.GetCloseCamera();
                var start = close ? new Pose(close.position, close.rotation) : default;
                _rig = cam.BeginManualReplayCamera();
                if (!_rig)
                {
                    CSDebug.LogWarning("[VoyageIntro] The camera manager has no replay camera - the voyage opens without its intro.");
                    Finish();
                    return;
                }
                if (close) _rig.SetPositionAndRotation(start.position, start.rotation);

                _skipAllowed = MaySkip(_script.SkippableAfterFirstRun, Seen(_script));
                AddObjectiveTokens();
                BuildOverlay();
                _startedAt = Time.unscaledTime;
                IsPlaying = true;
                Playing?.Invoke(true);
                VoyageToasts.PlayUi(_script.OpeningSound);
                FadeAsync(_bars, 1f, _script.FadeSeconds, ct).Forget();

                foreach (var shot in _script.Shots)
                {
                    if (shot == null) continue;
                    if (!await PlaySubjectAsync(shot, ct)) break;
                    if (_skipped) break;
                }

                await HandOverAsync(ct);
                MarkSeen(_script);
            }
            catch (System.OperationCanceledException) { /* stopped, or the scene went */ }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[VoyageIntro] The intro threw: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                Finish();
            }
        }

        /// <summary>The objective's own words for the Course shot ({objective}), and the Ark's hull ({hull}).</summary>
        void AddObjectiveTokens()
        {
            var content = VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _catalog = PilotArtifactCatalogSO.Load();
            _cast.Tokens.Add("hull");
            _cast.Tokens.Add(_cast.Ark ? Mathf.RoundToInt(_cast.Ark.HealthFraction * 100f).ToString() : "100");
            var source = VoyageObjective.Source;
            if (source != null && source.TryGetObjective(out var o))
            {
                _cast.Tokens.Add("objective");
                _cast.Tokens.Add(VoyageObjectiveText.GoalLine(_lines, o));
                _routeCells = o.Cells;
                _routeCell = o.Cell;
            }
        }

        int _routeCells, _routeCell;

        /// <summary>One shot, or one per vessel for a Squad or Wingmates shot. False when the intro was skipped.</summary>
        async UniTask<bool> PlaySubjectAsync(VoyageIntroSO.Shot shot, CancellationToken ct)
        {
            switch (shot.Subject)
            {
                case VoyageIntroSO.Subject.Squad:
                    foreach (var m in SquadInShowOrder())
                        if (!await PlayShotAsync(shot, m, ct)) return false;
                    return true;
                case VoyageIntroSO.Subject.You:
                    foreach (var m in _cast.Squad)
                        if (m.IsYou && m.Hull) return await PlayShotAsync(shot, m, ct);
                    return true;
                case VoyageIntroSO.Subject.Wingmates:
                    foreach (var m in _cast.Squad)
                        if (!m.IsYou && m.Hull && !await PlayShotAsync(shot, m, ct)) return false;
                    return true;
                default:
                    return await PlayShotAsync(shot, null, ct);
            }
        }

        IEnumerable<Member> SquadInShowOrder()
        {
            Member? you = null;
            foreach (var m in _cast.Squad)
            {
                if (m.IsYou) { you = m; continue; }
                if (m.Hull) yield return m;
            }
            if (you is { } y && y.Hull) yield return y;
        }

        /// <summary>One shot. False when the intro was skipped (the reel goes straight to the hand-over).</summary>
        async UniTask<bool> PlayShotAsync(VoyageIntroSO.Shot shot, Member? member, CancellationToken ct)
        {
            Transform subject = member is { } m ? m.Hull : _cast.Ark ? _cast.Ark.transform : null;
            if (!subject) return !_skipped;
            float size = member != null ? HullSize(subject) : Mathf.Max(MinSubjectSize, _cast.ArkSize);

            if (shot.Launch && !_launched)
            {
                _launched = true;
                try { _cast.Launch?.Invoke(); }
                catch (System.Exception e) { CSDebug.LogError($"[VoyageIntro] The launch threw: {e.Message}\n{e.StackTrace}"); }
            }
            VoyageToasts.PlayUi(shot.Sound);
            var words = ShowWords(shot, member);
            var from = new Pose(_rig.position, _rig.rotation);
            var frame = subject.rotation;
            float seconds = Mathf.Max(0.2f, shot.Seconds);
            float cut = shot.HardCut ? 0f : Mathf.Min(_script.CutSeconds, seconds * 0.5f);
            float fade = Mathf.Min(_script.FadeSeconds, seconds * 0.3f);
            bool course = shot.Subject == VoyageIntroSO.Subject.Course;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                ct.ThrowIfCancellationRequested();
                if (!subject) break;
                if (ReadSkip()) return false;

                float k = 1f - Mathf.Exp(-FrameDamping * Time.unscaledDeltaTime);
                frame = Quaternion.Slerp(frame, subject.rotation, k);
                var target = VoyageIntroFraming.Evaluate(shot, subject.position, frame, size, elapsed / seconds);
                float w = cut <= 0.05f ? 1f : Mathf.SmoothStep(0f, 1f, elapsed / cut);
                PoseRig(VoyageIntroFraming.Blend(from, target, w));

                // A caption or a title comes and goes with its shot; a name card stays until the course is shown.
                if (words) words.alpha = words == _cards
                    ? Mathf.Max(words.alpha, Mathf.Clamp01(elapsed / fade))
                    : Mathf.Min(Mathf.Clamp01(elapsed / fade), Mathf.Clamp01((seconds - elapsed) / fade));
                if (course) DrawRoute(elapsed / (seconds * 0.7f));

                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
                elapsed += Time.unscaledDeltaTime;
            }
            if (words && words != _cards) words.alpha = 0f;
            return !_skipped;
        }

        /// <summary>The last move: GO comes up, the bars slide away and the camera swings from wherever the reel is into
        /// the chase pose behind the pilot's own vessel; then the gameplay camera is restored onto it.</summary>
        async UniTask HandOverAsync(CancellationToken ct)
        {
            if (_caption) _caption.alpha = 0f;
            if (_titleGroup) _titleGroup.alpha = 0f;
            if (_cards) _cards.alpha = 0f;
            if (_hint) _hint.alpha = 0f;
            Transform you = null;
            foreach (var m in _cast.Squad)
                if (m.IsYou && m.Hull) { you = m.Hull; break; }
            float seconds = _script.HandOverSeconds;
            SlideBarsAsync(seconds, ct).Forget();
            if (_go && !string.IsNullOrEmpty(_goText.text))
            {
                VoyageToasts.PlayUi(_script.GoSound);
                GoAsync(seconds, ct).Forget();
            }
            if (!you) return;

            float size = HullSize(you);
            var from = new Pose(_rig.position, _rig.rotation);
            // Swing into the pilot's own follow camera (the vessel's normal camera, as in every other mode), so the cut
            // back to it lands on the same pose; the script's chase numbers are the fallback when there is none to read.
            var close = CameraManager.Instance ? CameraManager.Instance.GetCloseCamera() : null;
            CustomCameraController follow = null;
            if (close) close.TryGetComponent(out follow);
            if (follow && (!follow.FollowTarget || follow.FollowTarget.root != you.root)) follow = null;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                ct.ThrowIfCancellationRequested();
                if (!you) return;
                Pose chase;
                if (follow && follow.FollowTarget)
                {
                    follow.SnapToTarget();
                    chase = new Pose(follow.transform.position, follow.transform.rotation);
                }
                else chase = VoyageIntroFraming.Chase(you.position, you.rotation, size, _script.ChaseDistance, _script.ChaseHeight);
                PoseRig(VoyageIntroFraming.Blend(from, chase, Mathf.SmoothStep(0f, 1f, elapsed / seconds)));
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
                elapsed += Time.unscaledDeltaTime;
            }
        }

        void PoseRig(Pose p)
        {
            if (_rig) _rig.SetPositionAndRotation(p.position, p.rotation);
        }

        /// <summary>Exactly once: the camera goes back to the pilot, the overlay goes, and the caller takes the stick.</summary>
        void Finish()
        {
            if (_done) return;
            _done = true;
            if (_rig && CameraManager.Instance) CameraManager.Instance.RestoreGameplayCamera();
            _rig = null;
            if (IsPlaying)
            {
                IsPlaying = false;
                Playing?.Invoke(false);
            }
            var done = _onDone;
            _onDone = null;
            try { done?.Invoke(); }
            catch (System.Exception e) { CSDebug.LogError($"[VoyageIntro] Handing the stick back threw: {e.Message}\n{e.StackTrace}"); }
            if (this) Destroy(gameObject);
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            Finish();
            _cts?.Dispose();
            _cts = null;
        }

        static float HullSize(Transform hull)
        {
            var renderers = hull.GetComponentsInChildren<Renderer>();
            bool any = false;
            var bounds = new Bounds(hull.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer or TrailRenderer or LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any ? Mathf.Clamp(bounds.extents.magnitude, MinSubjectSize, MaxSubjectSize) : FallbackHullSize;
        }

        // ── Words ────────────────────────────────────────────────────────────

        /// <summary>Set up the shot's words; returns the group that fades with it (null: none).</summary>
        CanvasGroup ShowWords(VoyageIntroSO.Shot shot, Member? member)
        {
            var pairs = new List<string>(_cast.Tokens);
            if (member is { } m)
            {
                pairs.Add("vessel"); pairs.Add(VesselName(m.Vessel));
                pairs.Add("seat"); pairs.Add(SeatName(m.Seat));
                pairs.Add("who"); pairs.Add(WhoName(m));
            }
            var tokens = pairs.ToArray();
            string title = TemplateText.Fill(shot.Title, tokens);
            string line = TemplateText.Fill(shot.Line, tokens);
            switch (shot.Subject)
            {
                case VoyageIntroSO.Subject.Ark:
                    string caption = string.IsNullOrEmpty(line) ? title : string.IsNullOrEmpty(title) ? line : title + "\n" + line;
                    VoyageKit.Set(_captionText, caption);
                    _caption.alpha = 0f;
                    return string.IsNullOrEmpty(caption) ? null : _caption;
                case VoyageIntroSO.Subject.Course:
                    if (_cards) _cards.alpha = 0f;
                    VoyageKit.Set(_title, title);
                    VoyageKit.Set(_line, line);
                    _titleGroup.alpha = 0f;
                    BuildRoute();
                    return _titleGroup;
                default:
                    if (member is { } who) AddCard(who, title, tokens);
                    return _cards;
            }
        }

        string VesselName(VesselClassType type)
        {
            var list = _script.Vessels;
            return list && list.TryGetVesselByClass(type, out var v, reportMissing: false) && v ? v.CardName : string.Empty;
        }

        SO_Vessel VesselOf(VesselClassType type)
        {
            var list = _script.Vessels;
            return list && list.TryGetVesselByClass(type, out var v, reportMissing: false) ? v : null;
        }

        string SeatName(int seat) => seat switch
        {
            0 => _lines.CardSeatLeader,
            1 => _lines.CardSeatRogueOne,
            _ => _lines.CardSeatRogueTwo,
        };

        string WhoName(Member m) => m.IsYou ? _lines.CardSeatYou : !string.IsNullOrEmpty(m.Friend) ? m.Friend : _lines.CardSeatAi;

        // ── Name cards ───────────────────────────────────────────────────────

        /// <summary>A vessel's name card (Rev 114, Departure shots 3 and 4): the pilot's face, the vessel, "pilot · element"
        /// and the element's flower, one more card in the row each time.</summary>
        void AddCard(Member m, string title, string[] tokens)
        {
            var vessel = VesselOf(m.Vessel);
            var pilot = _catalog ? _catalog.ResolvePilot(m.Slot, vessel ? vessel.Keywords : null) : null;
            var element = m.Slot.Element;
            var pairs = new List<string>(tokens)
            {
                "pilot", VoyageSeats.PilotName(m.Slot, pilot, m.IsYou),
                "element", VoyageHudColors.ElementName(_lines, element),
            };
            string line = TemplateText.Fill(_script.CardLine, pairs.ToArray());

            var card = VoyageKit.Node("Card", _cardRow);
            var le = VoyageKit.Le(card);
            le.minWidth = le.preferredWidth = CardWidth;
            le.minHeight = le.preferredHeight = CardHeight;
            VoyageKit.Block(card, VoyageKit.Fade(_kit.Popup, 0.84f));
            var edge = VoyageKit.Node("Edge", card);
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = new Vector2(0f, 1f);
            edge.pivot = new Vector2(0f, 0.5f);
            edge.sizeDelta = new Vector2(2f, 0f);
            VoyageKit.Block(edge, _kit.Light);

            var faceBox = VoyageKit.Node("Face", card);
            VoyageKit.At(faceBox, 12f, (CardHeight - CardFace) * 0.5f, CardFace, CardFace);
            VoyageKit.Block(faceBox, VoyageKit.Fade(_kit.Outline, 0.45f));
            var face = VoyageKit.CoverBox(faceBox);
            VoyageKit.ShowCover(face, pilot ? pilot.Portrait : null, new Vector2(CardFace, CardFace), VoyageKit.FaceFocus);

            var name = _kit.Text(card, 18, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(name.rectTransform, 72f, 10f, CardWidth - 120f, 24f);
            VoyageKit.Set(name, title);
            var sub = _kit.Text(card, 14, VoyageHudColors.Second, TextAlignmentOptions.Left);
            VoyageKit.At(sub.rectTransform, 72f, 34f, CardWidth - 120f, 20f);
            VoyageKit.Set(sub, line);

            var flower = VoyageKit.Flower(card, element, _kit.Light);
            VoyageKit.At(flower, CardWidth - 40f, (CardHeight - 24f) * 0.5f, 24f, 24f);
        }

        // ── The route ────────────────────────────────────────────────────────

        /// <summary>The course's stops as a line of dots under the title, the last (the destination) in the call colour.</summary>
        void BuildRoute()
        {
            _routeDots.Clear();
            for (int i = _route.childCount - 1; i >= 0; i--) Destroy(_route.GetChild(i).gameObject);
            int n = _routeCells;
            if (n <= 0) return;
            var line = VoyageKit.Node("Line", _route);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.offsetMin = new Vector2(RouteDot * 0.5f, -1f);
            line.offsetMax = new Vector2(-RouteDot * 0.5f, 1f);
            VoyageKit.Block(line, VoyageKit.Fade(_kit.Outline, 0.8f));
            for (int i = 0; i < n; i++)
            {
                float x = n == 1 ? 0.5f : (float)i / (n - 1);
                var dot = VoyageKit.Node("Stop" + i, _route);
                dot.anchorMin = dot.anchorMax = new Vector2(x, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(RouteDot, RouteDot);
                bool last = i == n - 1, here = i < Mathf.Max(1, _routeCell);
                var img = VoyageKit.Block(dot, last ? _kit.CtaFill : here ? _kit.Light : _kit.Sub);
                img.enabled = false;
                _routeDots.Add(img);
            }
        }

        /// <summary>Draw the route in, stop by stop, as <paramref name="t"/> runs 0-1.</summary>
        void DrawRoute(float t)
        {
            int shown = Mathf.CeilToInt(Mathf.Clamp01(t) * _routeDots.Count);
            for (int i = 0; i < _routeDots.Count; i++)
                if (_routeDots[i]) _routeDots[i].enabled = i < shown;
        }

        // ── Skip ─────────────────────────────────────────────────────────────

        /// <summary>True once B has been held for the authored time, when this pilot may skip. A press already down when
        /// the intro began (the pilot was flying a moment ago) never counts: the skip arms only after the grace and a
        /// moment with nothing held.</summary>
        bool ReadSkip()
        {
            if (_skipped) return true;
            if (!_skipAllowed) return false;
            bool held = SkipHeld();
            if (!_skipArmed)
            {
                if (!held && Time.unscaledTime - _startedAt >= _script.SkipGraceSeconds) _skipArmed = true;
                return false;
            }
            _skipHeld = held ? _skipHeld + Time.unscaledDeltaTime : 0f;
            float need = _script.HoldToSkipSeconds;
            if (_skipFill) _skipFill.localScale = new Vector3(Mathf.Clamp01(_skipHeld / need), 1f, 1f);
            if (_skipHeld < need) return false;
            _skipped = true;
            return true;
        }

        /// <summary>B on a pad, Escape or B on a keyboard, the right mouse button, a held touch.</summary>
        static bool SkipHeld()
        {
            var k = Keyboard.current;
            if (k != null && (k.escapeKey.isPressed || k.bKey.isPressed)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var g = Gamepad.current;
            return g != null && g.buttonEast.isPressed;
        }

        // ── The overlay ──────────────────────────────────────────────────────

        void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the intro is watched, and a tap on it is read as a skip, never as a click.

            _kit = new VoyageKit(VoyageBoardStyleSO.Load(), _lines, null);
            _ink = ColorTrace.Retrace(ColorTrace.Ui, new Color(0.02f, 0.03f, 0.05f, 1f));
            _paper = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 0.96f, 0.86f, 1f));
            float share = _script.LetterboxShare;

            _bars = Group("Bars", Vector2.zero, Vector2.one);
            _topBar = Bar(_bars.transform, new Vector2(0f, 1f - share), Vector2.one, _ink);
            _bottomBar = Bar(_bars.transform, Vector2.zero, new Vector2(1f, share), _ink);
            _bars.alpha = 0f;

            // The Ark's caption: small, lower left above the bar.
            _caption = Group("Caption", new Vector2(0.03f, share + 0.03f), new Vector2(0.6f, share + 0.12f));
            _captionText = _kit.Label(_caption.transform, 22, _kit.Light);
            Stretch(_captionText.rectTransform);
            _captionText.alignment = TextAlignmentOptions.BottomLeft;
            _captionText.textWrappingMode = TextWrappingModes.Normal;
            _caption.alpha = 0f;

            // The name cards: a row lower left above the bar, one card added per vessel.
            _cards = Group("Cards", new Vector2(0.03f, share + 0.03f), new Vector2(0.97f, share + 0.03f));
            var cardsRt = (RectTransform)_cards.transform;
            cardsRt.pivot = new Vector2(0f, 0f);
            cardsRt.sizeDelta = new Vector2(0f, CardHeight);
            _cardRow = cardsRt;
            var row = _cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = CardGap;
            row.childAlignment = TextAnchor.LowerLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            _cards.alpha = 0f;

            // The title, the goal and the route, centred over the sea.
            _titleGroup = Group("Title", new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.62f));
            _title = Text(_titleGroup.transform, "Title", new Vector2(0f, 0.55f), Vector2.one, 64f, _paper, FontStyles.Bold,
                TextAlignmentOptions.Bottom);
            _title.characterSpacing = 18f;
            _line = Text(_titleGroup.transform, "Line", new Vector2(0f, 0.32f), new Vector2(1f, 0.52f), 24f,
                VoyageHudColors.Second, FontStyles.Normal, TextAlignmentOptions.Top);
            _route = VoyageKit.Node("Route", _titleGroup.transform);
            _route.anchorMin = _route.anchorMax = new Vector2(0.5f, 0.12f);
            _route.pivot = new Vector2(0.5f, 0.5f);
            _route.sizeDelta = new Vector2(RouteWidth, RouteDot);
            _titleGroup.alpha = 0f;

            // GO, over the hand-over.
            _go = Group("Go", new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.6f));
            _goText = Text(_go.transform, "Go", Vector2.zero, Vector2.one, 96f, _paper, FontStyles.Bold,
                TextAlignmentOptions.Center);
            _goText.characterSpacing = 30f;
            _goText.text = _script.GoWord ?? string.Empty;
            _go.alpha = 0f;

            if (_skipAllowed && !string.IsNullOrEmpty(_script.SkipHint))
            {
                _hint = Group("Skip", new Vector2(0.7f, share * 0.25f), new Vector2(0.96f, share * 0.75f));
                _hintText = _kit.Label(_hint.transform, 14, _kit.Sub, TextAlignmentOptions.Right);
                var hrt = _hintText.rectTransform;
                hrt.anchorMin = new Vector2(0f, 0.3f);
                hrt.anchorMax = Vector2.one;
                hrt.offsetMin = hrt.offsetMax = Vector2.zero;
                _hintText.text = _script.SkipHint;
                var fill = new GameObject("Held", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(_hint.transform, false);
                _skipFill = (RectTransform)fill.transform;
                _skipFill.anchorMin = new Vector2(0.55f, 0f);
                _skipFill.anchorMax = new Vector2(1f, 0.12f);
                _skipFill.offsetMin = _skipFill.offsetMax = Vector2.zero;
                _skipFill.pivot = new Vector2(0f, 0.5f);
                _skipFill.localScale = new Vector3(0f, 1f, 1f);
                var img = fill.GetComponent<Image>();
                img.color = _kit.Light;
                img.raycastTarget = false;
                _hint.alpha = 1f;
            }
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        CanvasGroup Group(string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.GetComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            return g;
        }

        static RectTransform Bar(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max, float size, Color color,
            FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset) t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        static async UniTaskVoid FadeAsync(CanvasGroup group, float to, float seconds, CancellationToken ct)
        {
            float from = group ? group.alpha : to, t = 0f;
            while (t < seconds && group)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                t += Time.unscaledDeltaTime;
            }
            if (group) group.alpha = to;
        }

        /// <summary>The bars slide off the top and bottom of the screen over the hand-over.</summary>
        async UniTaskVoid SlideBarsAsync(float seconds, CancellationToken ct)
        {
            float t = 0f, share = _script.LetterboxShare;
            while (t < seconds && _topBar && _bottomBar)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds) * share;
                _topBar.anchorMin = new Vector2(0f, 1f - share + k);
                _topBar.anchorMax = new Vector2(1f, 1f + k);
                _bottomBar.anchorMin = new Vector2(0f, -k);
                _bottomBar.anchorMax = new Vector2(1f, share - k);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                t += Time.unscaledDeltaTime;
            }
            if (_bars) _bars.alpha = 0f;
        }

        /// <summary>GO pops in, holds, and fades as the stick comes back.</summary>
        async UniTaskVoid GoAsync(float seconds, CancellationToken ct)
        {
            float t = 0f;
            var rt = (RectTransform)_go.transform;
            while (t < seconds && _go)
            {
                float k = t / seconds;
                _go.alpha = k < 0.15f ? k / 0.15f : k > 0.7f ? Mathf.Clamp01((1f - k) / 0.3f) : 1f;
                rt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.2f)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                t += Time.unscaledDeltaTime;
            }
            if (_go) _go.alpha = 0f;
        }
    }

    /// <summary>The intro's camera maths, pure so the tests can hold it: where a shot puts the camera around its subject
    /// at a moment, the chase pose the hand-over lands in, and the blend between two poses.</summary>
    public static class VoyageIntroFraming
    {
        /// <summary>The camera's pose for <paramref name="shot"/> at <paramref name="t"/> (0-1, eased) around a subject at
        /// <paramref name="center"/> facing <paramref name="frame"/>, <paramref name="size"/> world units big.</summary>
        public static Pose Evaluate(VoyageIntroSO.Shot shot, Vector3 center, Quaternion frame, float size, float t)
        {
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            float az = Mathf.Lerp(shot.FromAzimuth, shot.ToAzimuth, s);
            float dist = Mathf.Lerp(shot.FromDistance, shot.ToDistance, s) * size;
            var aim = center + frame * Vector3.forward * (shot.LookAhead * size);
            return Orbit(center, frame, az, shot.Elevation, dist, aim);
        }

        /// <summary>A camera <paramref name="distance"/> from <paramref name="center"/> at <paramref name="azimuth"/>
        /// degrees around its frame (0 ahead, 90 starboard, 180 astern) and <paramref name="elevation"/> degrees above
        /// its plane, looking at <paramref name="aim"/> with the frame's up.</summary>
        public static Pose Orbit(Vector3 center, Quaternion frame, float azimuth, float elevation, float distance, Vector3 aim)
        {
            var dir = frame * (Quaternion.Euler(-elevation, azimuth, 0f) * Vector3.forward);
            var pos = center + dir * distance;
            var look = aim - pos;
            var rot = look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, frame * Vector3.up) : frame;
            return new Pose(pos, rot);
        }

        /// <summary>Behind and above the pilot's vessel, looking along its heading: where the chase camera sits.</summary>
        public static Pose Chase(Vector3 position, Quaternion rotation, float size, float back, float up)
        {
            var pos = position - rotation * Vector3.forward * (back * size) + rotation * Vector3.up * (up * size);
            var aim = position + rotation * Vector3.forward * (back * size);
            return new Pose(pos, Quaternion.LookRotation(aim - pos, rotation * Vector3.up));
        }

        public static Pose Blend(Pose a, Pose b, float w)
        {
            w = Mathf.Clamp01(w);
            return new Pose(Vector3.Lerp(a.position, b.position, w), Quaternion.Slerp(a.rotation, b.rotation, w));
        }
    }
}

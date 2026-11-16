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
    /// The voyage's opening, played (<see cref="VoyageIntroSO"/>): letterbox bars come in, the camera visits the Ark, each
    /// vessel of the squad and then looks down the course while the authored words say what the voyage is, and the last
    /// shot blends into the pilot's own chase camera as the stick comes back. Holding any button skips straight to that
    /// hand-over.
    ///
    /// <para>It owns only the camera and the words. Whoever starts it keeps the pilot's vessel safe meanwhile (the escort
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
        }

        /// <summary>Who and what the intro shows, and the voyage-wide tokens its words use.</summary>
        public sealed class Cast
        {
            public Ark Ark;
            /// <summary>The Ark's size: half its hull's length.</summary>
            public float ArkSize = 55f;
            /// <summary>The squad in seat order; the player's own seat is shown last whatever its number.</summary>
            public readonly List<Member> Squad = new();
            /// <summary>Token pairs every line can use ("voyage", "ARK ESCORT", "legs", "5", ...).</summary>
            public readonly List<string> Tokens = new();
        }

        VoyageIntroSO _script;
        Cast _cast;
        System.Action _onDone;
        CancellationTokenSource _cts;
        bool _done;

        Transform _rig;
        CanvasGroup _bars, _words, _hint;
        TMP_Text _title, _line, _hintText;
        RectTransform _skipFill;
        float _skipHeld;
        bool _skipArmed, _skipped;
        float _startedAt;

        /// <summary>
        /// Play <paramref name="script"/> over <paramref name="cast"/>. Returns null (and calls <paramref name="onDone"/>
        /// at once) when there is nothing to play or no camera to play it on.
        /// </summary>
        public static VoyageIntro Play(VoyageIntroSO script, Cast cast, Transform parent, System.Action onDone)
        {
            if (!script || script.Shots.Count == 0 || cast == null || !cast.Ark || !CameraManager.Instance)
            {
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

                BuildOverlay();
                _startedAt = Time.unscaledTime;
                IsPlaying = true;
                Playing?.Invoke(true);
                FadeBarsAsync(1f, ct).Forget();

                foreach (var shot in _script.Shots)
                {
                    if (shot == null) continue;
                    if (shot.Subject == VoyageIntroSO.Subject.Squad)
                    {
                        foreach (var m in SquadInShowOrder())
                            if (!await PlayShotAsync(shot, m, ct)) break;
                    }
                    else if (!await PlayShotAsync(shot, null, ct)) break;
                    if (_skipped) break;
                }

                await HandOverAsync(ct);
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

            ShowWords(shot, member);
            var from = new Pose(_rig.position, _rig.rotation);
            var frame = subject.rotation;
            float seconds = Mathf.Max(0.5f, shot.Seconds);
            float cut = Mathf.Min(_script.CutSeconds, seconds * 0.5f);
            float fade = Mathf.Min(_script.FadeSeconds, seconds * 0.3f);
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

                _words.alpha = Mathf.Min(Mathf.Clamp01(elapsed / fade), Mathf.Clamp01((seconds - elapsed) / fade));

                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
                elapsed += Time.unscaledDeltaTime;
            }
            _words.alpha = 0f;
            return !_skipped;
        }

        /// <summary>The last move: from wherever the reel is into the chase pose behind the pilot's own vessel, then the
        /// gameplay camera is restored onto it.</summary>
        async UniTask HandOverAsync(CancellationToken ct)
        {
            _words.alpha = 0f;
            if (_hint) _hint.alpha = 0f;
            Transform you = null;
            foreach (var m in _cast.Squad)
                if (m.IsYou && m.Hull) { you = m.Hull; break; }
            FadeBarsAsync(0f, ct).Forget();
            if (!you) return;

            float size = HullSize(you);
            var from = new Pose(_rig.position, _rig.rotation);
            float seconds = _script.HandOverSeconds;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                ct.ThrowIfCancellationRequested();
                if (!you) return;
                var chase = VoyageIntroFraming.Chase(you.position, you.rotation, size, _script.ChaseDistance, _script.ChaseHeight);
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

        void ShowWords(VoyageIntroSO.Shot shot, Member? member)
        {
            var pairs = new List<string>(_cast.Tokens);
            if (member is { } m)
            {
                pairs.Add("vessel"); pairs.Add(VesselName(m.Vessel));
                pairs.Add("seat"); pairs.Add(SeatName(m.Seat));
                pairs.Add("who"); pairs.Add(WhoName(m));
            }
            var tokens = pairs.ToArray();
            _title.text = TemplateText.Fill(shot.Title, tokens);
            _line.text = TemplateText.Fill(shot.Line, tokens);
            _words.alpha = 0f;
        }

        string VesselName(VesselClassType type)
        {
            var list = _script.Vessels;
            return list && list.TryGetVesselByClass(type, out var v, reportMissing: false) && v ? v.CardName : string.Empty;
        }

        static string SeatName(int seat)
        {
            var lines = VoyageContentSO.Load()?.Lines;
            if (lines == null) return string.Empty;
            return seat switch
            {
                0 => lines.CardSeatLeader,
                1 => lines.CardSeatRogueOne,
                _ => lines.CardSeatRogueTwo,
            };
        }

        static string WhoName(Member m)
        {
            var lines = VoyageContentSO.Load()?.Lines;
            if (lines == null) return string.Empty;
            return m.IsYou ? lines.CardSeatYou : lines.CardSeatAi;
        }

        // ── Skip ─────────────────────────────────────────────────────────────

        /// <summary>True once a button has been held for the authored time. A press already down when the intro began
        /// (the pilot was flying a moment ago) never counts: the skip arms only after the grace and a moment with
        /// nothing held.</summary>
        bool ReadSkip()
        {
            if (_skipped) return true;
            bool held = AnyButtonHeld();
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

        static bool AnyButtonHeld()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var g = Gamepad.current;
            return g != null && (g.buttonSouth.isPressed || g.buttonEast.isPressed || g.buttonWest.isPressed ||
                                 g.buttonNorth.isPressed || g.startButton.isPressed || g.selectButton.isPressed ||
                                 g.leftShoulder.isPressed || g.rightShoulder.isPressed);
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

            var ink = ColorTrace.Retrace(ColorTrace.Ui, new Color(0.02f, 0.03f, 0.05f, 1f));
            var paper = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 0.96f, 0.86f, 1f));
            var accent = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 0.92f, 0.6f, 1f));
            float share = _script.LetterboxShare;

            _bars = Group("Bars", Vector2.zero, Vector2.one);
            Bar(_bars.transform, new Vector2(0f, 1f - share), Vector2.one, ink);
            Bar(_bars.transform, Vector2.zero, new Vector2(1f, share), ink);
            _bars.alpha = 0f;

            _words = Group("Words", new Vector2(0.06f, share + 0.03f), new Vector2(0.7f, share + 0.3f));
            _title = Text(_words.transform, "Title", new Vector2(0f, 0.45f), Vector2.one, 60f, paper, FontStyles.Bold,
                TextAlignmentOptions.BottomLeft);
            _line = Text(_words.transform, "Line", Vector2.zero, new Vector2(1f, 0.42f), 28f, accent, FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            _words.alpha = 0f;

            if (!string.IsNullOrEmpty(_script.SkipHint))
            {
                _hint = Group("Skip", new Vector2(0.7f, share * 0.25f), new Vector2(0.96f, share * 0.75f));
                _hintText = Text(_hint.transform, "Hint", new Vector2(0f, 0.3f), Vector2.one, 22f, paper, FontStyles.Normal,
                    TextAlignmentOptions.Right);
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
                img.color = accent;
                img.raycastTarget = false;
                _hint.alpha = 0.75f;
            }
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

        static void Bar(Transform parent, Vector2 min, Vector2 max, Color color)
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

        async UniTaskVoid FadeBarsAsync(float to, CancellationToken ct)
        {
            float from = _bars ? _bars.alpha : to;
            float seconds = _script.FadeSeconds, t = 0f;
            while (t < seconds && _bars)
            {
                _bars.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                t += Time.unscaledDeltaTime;
            }
            if (_bars) _bars.alpha = to;
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

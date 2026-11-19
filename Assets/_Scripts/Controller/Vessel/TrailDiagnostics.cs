using UnityEngine;
using CosmicShore.Utility.PerformanceBenchmark;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The "Trail" section of the diagnostics HUD (F7): the input and trail state of the vessel the local player is
    /// flying, live, so a clip shows which number moved when a trail misbehaves (Caleb, 2026-11-19: the Dolphin's
    /// trail packing at cruise). Rows: input device, triggers and sticks; drift and the drift-trail loop; speed; the
    /// lay (wavelength, spacing, scalers, gates); the gap between the last two prisms on each rail and how far
    /// behind the hull each new prism went down; and the nearest of the vessel's own recent prisms to the hull,
    /// ahead of it or behind.
    ///
    /// <para>Editor and development builds only (pattern 1 of <c>Docs/CONDITIONAL_COMPILATION.md</c>: the type exists
    /// everywhere, its body does not). It spawns itself after the first scene loads, draws nothing of its own (the
    /// HUD's one-surface rule) and costs nothing while the HUD is hidden beyond a few reads a frame. The HUD console
    /// command <c>trail off</c> / <c>trail on</c> removes or restores the section.</para>
    /// </summary>
    public sealed class TrailDiagnostics : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const string Section = "Trail";
        const float VesselRescanSeconds = 1f;
        const float WindowSeconds = 2f;
        const int RecentPrismsChecked = 40;

        static TrailDiagnostics s_instance;

        bool _enabled = true;
        float _rescanAt;
        VesselStatus _vessel;
        VesselPrismController _prisms;
        DriftTrailActionExecutor _driftTrail;

        // Per rail: the last prism laid, for the gap to the next one.
        Vector3 _lastA, _lastB;
        bool _hasA, _hasB;
        float _gapA, _gapB;
        // Over the last WindowSeconds: the smallest gap on either rail and the smallest distance behind the hull a new
        // prism was laid (negative = ahead of the hull).
        float _minGap = float.PositiveInfinity, _minBehind = float.PositiveInfinity;
        float _windowStart;
        float _shownMinGap = float.NaN, _shownMinBehind = float.NaN;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (s_instance) return;
            var go = new GameObject("[TrailDiagnostics]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<TrailDiagnostics>();
        }

        void OnEnable() => DiagnosticsHUD.RegisterCommand("trail", OnCommand);

        void OnDestroy()
        {
            Unbind();
            DiagnosticsHUD.UnregisterCommand("trail");
            DiagnosticsHUD.ClearStats(Section);
            if (s_instance == this) s_instance = null;
        }

        string OnCommand(string[] args)
        {
            _enabled = args.Length == 0 ? !_enabled : args[0] != "off";
            if (!_enabled) DiagnosticsHUD.ClearStats(Section);
            return _enabled ? "trail diagnostics on" : "trail diagnostics off";
        }

        void Update()
        {
            if (!_enabled) return;

            if (Time.unscaledTime >= _rescanAt || !_vessel)
            {
                _rescanAt = Time.unscaledTime + VesselRescanSeconds;
                Rebind(FindLocalVessel());
            }

            if (!_vessel || _vessel.Player == null)
            {
                DiagnosticsHUD.SetStat(Section, "vessel", "none (no local pilot)");
                return;
            }

            if (Time.unscaledTime - _windowStart >= WindowSeconds)
            {
                _shownMinGap = _minGap;
                _shownMinBehind = _minBehind;
                _minGap = _minBehind = float.PositiveInfinity;
                _windowStart = Time.unscaledTime;
            }

            IVesselStatus v = _vessel;
            var input = v.InputStatus;
            Transform hull = _vessel.transform;

            DiagnosticsHUD.SetStat(Section, "vessel", $"{v.VesselType}{(v.AutoPilotEnabled ? " (autopilot)" : "")}");
            DiagnosticsHUD.SetStat(Section, "input", input == null ? "-" :
                $"{input.ActiveInputDevice}  LT {input.LeftTriggerAnalog:0.00}  RT {input.RightTriggerAnalog:0.00}  " +
                $"sum {input.XSum:+0.00;-0.00} {input.YSum:+0.00;-0.00}  diff {input.XDiff:+0.00;-0.00} {input.YDiff:+0.00;-0.00}");

            float noseOnCourse = Vector3.Dot(hull.forward, v.Course);
            DiagnosticsHUD.SetStat(Section, "drift",
                $"{(v.IsDrifting ? "DRIFTING" : "no")}  drift trail {(_driftTrail ? (_driftTrail.IsRunning ? "RUNNING" : "idle") : "-")}" +
                $"  nose.course {noseOnCourse:0.00}");
            DiagnosticsHUD.SetStat(Section, "speed", $"{v.Speed:0} u/s");

            if (!_prisms)
            {
                DiagnosticsHUD.SetStat(Section, "lay", "no prism controller");
                return;
            }

            DiagnosticsHUD.SetStat(Section, "lay",
                $"wl {_prisms.Wavelength:0.0}  spacing {_prisms.LaySpacing:0.0}  gap {_prisms.Gap:0.0}  " +
                $"scale x{_prisms.XScaler:0.00} y{_prisms.YScaler:0.00} z{_prisms.ZScaler:0.00}" +
                $"{(_prisms.PenUp ? "  PEN UP" : "")}{(_prisms.TierHeld ? "  TIER HOLD" : "")}" +
                $"  drift along course {(_prisms.DriftLaysAlongCourse ? "ON" : "OFF")}");
            DiagnosticsHUD.SetStat(Section, "last gap",
                $"rail A {Show(_gapA, _hasA)}  rail B {Show(_gapB, _hasB)}  min/2s {Show(_shownMinGap, true)}");
            DiagnosticsHUD.SetStat(Section, "laid behind hull", $"min/2s {Show(_shownMinBehind, true)} (negative = ahead)");
            DiagnosticsHUD.SetStat(Section, "nearest own prism", NearestOwnPrism(hull));
        }

        static string Show(float value, bool has) =>
            !has || float.IsNaN(value) || float.IsInfinity(value) ? "-" : $"{value:0.0}";

        string NearestOwnPrism(Transform hull)
        {
            float best = float.PositiveInfinity;
            bool ahead = false;
            Vector3 boxSize = Vector3.zero;
            Scan(_prisms.Trail, hull, ref best, ref ahead, ref boxSize);
            Scan(_prisms.SecondaryTrail, hull, ref best, ref ahead, ref boxSize);
            if (float.IsPositiveInfinity(best)) return "-";
            return $"{best:0.0} u {(ahead ? "AHEAD" : "behind")}  (box {boxSize.x:0.0} x {boxSize.y:0.0} x {boxSize.z:0.0})";
        }

        // The distance from the hull's centre to the surface of each of the rail's newest prisms (0 = inside it).
        static void Scan(Trail trail, Transform hull, ref float best, ref bool ahead, ref Vector3 boxSize)
        {
            var list = trail?.TrailList;
            if (list == null) return;
            for (int i = list.Count - 1, n = 0; i >= 0 && n < RecentPrismsChecked; i--, n++)
            {
                var prism = list[i];
                if (!prism) continue;
                var t = prism.transform;
                Vector3 size = prism.TargetScale;
                if (size.x <= 0f) size = t.localScale;
                Vector3 local = Quaternion.Inverse(t.rotation) * (hull.position - t.position);
                Vector3 half = size * 0.5f;
                Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -half.x, half.x),
                                              Mathf.Clamp(local.y, -half.y, half.y),
                                              Mathf.Clamp(local.z, -half.z, half.z));
                Vector3 closest = t.position + t.rotation * clamped;
                float d = Vector3.Distance(hull.position, closest);
                if (d >= best) continue;
                best = d;
                ahead = Vector3.Dot(closest - hull.position, hull.forward) > 0.01f;
                boxSize = size;
            }
        }

        static VesselStatus FindLocalVessel()
        {
            foreach (var status in FindObjectsByType<VesselStatus>(FindObjectsSortMode.None))
            {
                if (!status || status.Player == null) continue;
                IVesselStatus iv = status;
                if (iv.IsLocalUser && !iv.AutoPilotEnabled) return status;
            }
            return null;
        }

        void Rebind(VesselStatus vessel)
        {
            if (vessel == _vessel) return;
            Unbind();
            _vessel = vessel;
            if (!vessel) return;
            IVesselStatus iv = vessel;
            _prisms = iv.VesselPrismController;
            _driftTrail = vessel.GetComponentInChildren<DriftTrailActionExecutor>(true);
            if (_prisms) _prisms.OnBlockSpawned += OnPrismLaid;
            _hasA = _hasB = false;
        }

        void Unbind()
        {
            if (_prisms) _prisms.OnBlockSpawned -= OnPrismLaid;
            _vessel = null;
            _prisms = null;
            _driftTrail = null;
        }

        void OnPrismLaid(Prism prism)
        {
            if (!prism || !_prisms || !_vessel) return;
            Vector3 p = prism.transform.position;

            var a = _prisms.Trail?.TrailList;
            bool onA = a != null && a.Count > 0 && a[a.Count - 1] == prism;
            if (onA)
            {
                if (_hasA) Record(ref _gapA, Vector3.Distance(p, _lastA));
                _lastA = p;
                _hasA = true;
            }
            else
            {
                if (_hasB) Record(ref _gapB, Vector3.Distance(p, _lastB));
                _lastB = p;
                _hasB = true;
            }

            IVesselStatus v = _vessel;
            Transform hull = _vessel.transform;
            float behind = Vector3.Dot(hull.position - p, v.Course);
            if (behind < _minBehind) _minBehind = behind;
        }

        void Record(ref float gap, float value)
        {
            gap = value;
            if (value < _minGap) _minGap = value;
        }
#endif
    }
}

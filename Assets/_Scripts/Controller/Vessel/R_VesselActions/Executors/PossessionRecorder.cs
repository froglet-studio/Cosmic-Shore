#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Records how a PERSON flies a creature they possess with the Jewel Wasp, so the fauna can learn from it (Garrett,
    /// 2026-11-14: "this vessel will give us play time on fauna which will give us data that can be used for more
    /// sophisticated fauna behavior ... similar to what we do already with vessels"). The fauna twin of
    /// <see cref="HumanFlightRecorder"/>, and as automatic: no button. A ride starts when the sting lands and ends when the
    /// wasp leaves, the host dies or another holder takes it, or Play stops; a ride shorter than
    /// <see cref="MinRideSeconds"/> is not kept.
    ///
    /// <para>Ten times a second: the host's position, heading and up, its speed and its top speed, the rider's four stick
    /// axes and throttle, the held triggers and the burst, the nearest rival vessel (position, distance) and the nearest
    /// rival prisms' count within the host's mouth reach (what a hunting creature would see). Every press is an event:
    /// a mouthful (with the prisms eaten), an attack, a hit landed, a burst. Once per ride: the species, its diet, its
    /// body radius and mouth reach, the mode, the cell, the four element levels the rider sat at (normalized, 0..1), and how it ended.</para>
    ///
    /// <para>One <c>possession_&lt;Mode&gt;_&lt;Species&gt;_&lt;time&gt;.json</c> per ride (schema <c>cosmic-shore.possession/1</c>)
    /// beside the flights, in <c>&lt;Diagnostics folder&gt;/Flights/</c>. After Play the editor sends it to the person's
    /// <c>bugledger/&lt;user&gt;</c> branch with the flights (<c>BugHuntReports</c> kind <c>flights</c>; its name is not
    /// <c>flight_*</c>, so the vessel learners never read it as a vessel's flight). Compiles in editor and development
    /// builds only, like the flight recorder; only the send is editor-only.</para>
    /// </summary>
    public sealed class PossessionRecorder
    {
        public const string Schema = "cosmic-shore.possession/1";
        const float SampleHz = 10f;
        const float MinRideSeconds = 3f;
        const string Columns = "t,x,y,z,fx,fy,fz,ux,uy,uz,speed,topSpeed,xSum,ySum,xDiff,yDiff,throttle,rt,lt,burst," +
                               "rivalX,rivalY,rivalZ,rivalDist,prismsInReach";

        readonly StringBuilder _samples = new(16 * 1024);
        readonly StringBuilder _events = new(1024);
        readonly List<Prism> _scratch = new(64);
        Fauna _host;
        string _species, _diet, _mode, _cell, _pilot, _domain;
        float _start, _nextSample, _bodyRadius, _mouthReach;
        float _charge, _mass, _space, _time;
        int _count, _bites, _attacks, _hits, _bursts;
        Vector3 _lastPos;
        float _distance;

        /// <summary>True while a ride is being recorded.</summary>
        public bool Recording => _host;

        public void Begin(Fauna host, IVesselStatus rider)
        {
            if (_host) End("re-stung");
            if (!host || rider == null) return;
            _host = host;
            _samples.Clear();
            _events.Clear();
            _count = _bites = _attacks = _hits = _bursts = 0;
            _distance = 0f;
            _start = Time.time;
            _nextSample = 0f;
            _species = host.name.Replace("(Clone)", "").Trim();
            _diet = host.Diet.ToString();
            _bodyRadius = host.PossessionBodyRadius;
            _mouthReach = host.PossessionMouthReach;
            var cell = Cell.FindNearestActiveCell(host.PossessionNestPoint);
            _cell = cell ? cell.name : "";
            _mode = ModeName();
            _pilot = rider.PlayerName;
            _domain = rider.Domain.ToString();
            var rs = rider.ResourceSystem;
            _charge = Level(rs, Element.Charge);
            _mass = Level(rs, Element.Mass);
            _space = Level(rs, Element.Space);
            _time = Level(rs, Element.Time);
            _lastPos = host.PossessionNestPoint;
        }

        /// <summary>One frame of the ride (the executor calls it every frame it steers the host; it samples at 10 Hz).</summary>
        public void Tick(IVesselStatus rider, Quaternion frame, float throttle, bool rt, bool lt, bool burst,
                         IVesselStatus rival)
        {
            var host = _host;
            if (!host) return;
            float now = Time.time;
            Vector3 at = host.PossessionNestPoint;
            _distance += (at - _lastPos).magnitude;
            _lastPos = at;
            if (now < _nextSample) return;
            _nextSample = now + 1f / SampleHz;
            var input = rider?.InputStatus;
            Vector3 f = frame * Vector3.forward, u = frame * Vector3.up;
            Vector3 rv = rival?.Transform ? rival.Transform.position : Vector3.zero;
            float rd = rival?.Transform ? (rv - at).magnitude : -1f;
            int prisms = PrismsInReach(at, rider != null ? rider.Domain : default);
            if (_count > 0) _samples.Append(',');
            _samples.Append('[');
            Num(now - _start); Vec(at); Vec(f); Vec(u);
            Num(host.CurrentSpeed); Num(host.PossessionTopSpeed);
            Num(input?.XSum ?? 0f); Num(input?.YSum ?? 0f); Num(input?.XDiff ?? 0f); Num(input?.YDiff ?? 0f);
            Num(throttle); Num(rt ? 1 : 0); Num(lt ? 1 : 0); Num(burst ? 1 : 0);
            Vec(rv); Num(rd); Num(prisms, last: true);
            _samples.Append(']');
            _count++;
        }

        public void Suction(int bites) { _bites += bites; Event("suction", bites); }
        public void Attack() { _attacks++; Event("attack", 0); }
        public void Hit() { _hits++; Event("hit", 0); }
        public void Burst() { _bursts++; Event("burst", 0); }

        /// <summary>The ride is over: write it (when it lasted long enough) and stop.</summary>
        public void End(string why)
        {
            var host = _host;
            _host = null;
            float seconds = Time.time - _start;
            if (_count == 0 || seconds < MinRideSeconds) return;
            try
            {
                var sb = new StringBuilder(_samples.Length + _events.Length + 1024);
                sb.Append("{\"schema\":\"").Append(Schema).Append('"');
                Field(sb, "species", _species); Field(sb, "diet", _diet); Field(sb, "mode", _mode); Field(sb, "cell", _cell);
                Field(sb, "pilot", _pilot); Field(sb, "domain", _domain); Field(sb, "ended", why);
                Field(sb, "build", Application.version);
                Field(sb, "date", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                sb.Append(",\"seconds\":").Append(F(seconds)).Append(",\"distance\":").Append(F(_distance));
                sb.Append(",\"bodyRadius\":").Append(F(_bodyRadius)).Append(",\"mouthReach\":").Append(F(_mouthReach));
                sb.Append(",\"elements\":{\"charge\":").Append(F(_charge)).Append(",\"mass\":").Append(F(_mass))
                  .Append(",\"space\":").Append(F(_space)).Append(",\"time\":").Append(F(_time)).Append('}');
                sb.Append(",\"totals\":{\"bites\":").Append(_bites).Append(",\"attacks\":").Append(_attacks)
                  .Append(",\"hits\":").Append(_hits).Append(",\"bursts\":").Append(_bursts).Append('}');
                sb.Append(",\"hz\":").Append(F(SampleHz));
                sb.Append(",\"columns\":[\"").Append(Columns.Replace(",", "\",\"")).Append("\"]");
                sb.Append(",\"samples\":[").Append(_samples).Append(']');
                sb.Append(",\"events\":[").Append(_events).Append("]}");
                string folder = HumanFlightRecorder.Folder;
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"possession_{Safe(_mode)}_{Safe(_species)}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, sb.ToString());
                CSDebug.LogVerbose(CSLogChannel.Ecology, $"[Possession] recorded {_species} for {seconds:0}s ({why}): {file}");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[Possession] the ride on {(host ? host.name : _species)} could not be stored: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ helpers

        int PrismsInReach(Vector3 at, Domains rider)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return -1;
            int found = index.QuerySphere(at, _mouthReach, _scratch);
            int n = 0;
            for (int i = 0; i < found && i < _scratch.Count; i++)
            {
                var p = _scratch[i];
                if (p && !p.destroyed && p.Domain != rider) n++;
            }
            _scratch.Clear();
            return n;
        }

        void Event(string kind, int value)
        {
            if (!_host) return;
            if (_events.Length > 0) _events.Append(',');
            _events.Append("{\"t\":").Append(F(Time.time - _start)).Append(",\"kind\":\"").Append(kind)
                   .Append("\",\"value\":").Append(value).Append('}');
        }

        static float Level(ResourceSystem rs, Element element)
        {
            if (!rs) return 0f;
            try { return rs.GetNormalizedLevel(element); }
            catch { return 0f; }
        }

        static string ModeName()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.IsValid() ? scene.name : "Unknown";
        }

        void Num(float v, bool last = false)
        {
            _samples.Append(F(v));
            if (!last) _samples.Append(',');
        }

        void Vec(Vector3 v) { Num(v.x); Num(v.y); Num(v.z); }

        static void Field(StringBuilder sb, string key, string value) =>
            sb.Append(",\"").Append(key).Append("\":\"").Append(Esc(value)).Append('"');

        static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unknown";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString();
        }

        static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string F(float v) => float.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "0";
    }
}
#endif

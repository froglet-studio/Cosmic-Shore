using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Javelin</b> (Docs/Pilots/ARTIFACTS.md §3, offence): the loaded payload is THROWN, not placed. It flies down the
    /// carrier's heading cracking every rival super-shield along its line, and bursts where it stops: at the end of its
    /// range, or once it has cracked as many super-shields as its Charge allows.
    ///
    /// <para>Its four tracks (the values are on the artifact asset, the twists here):</para>
    /// <list type="bullet">
    /// <item>Mass, the line's width. Twist: the final burst reaches twice as far.</item>
    /// <item>Space, its range. Twist: it pierces, never stopping for what it cracks.</item>
    /// <item>Time, how long the burst lingers, cracking again what is rebuilt. Twist: the whole line lingers.</item>
    /// <item>Charge, how many super-shields it cracks before it stops. Twist: it cracks ordinary shields too.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(fileName = "JavelinTransform", menuName = "ScriptableObjects/Pilots/Payload Transforms/Javelin")]
    public class JavelinTransformSO : PayloadTransformSO
    {
        [Tooltip("World units per second the thrown payload flies.")]
        [Min(1f)]
        [SerializeField] float speed = 260f;

        [Tooltip("Seconds between the lingering burst's re-cracks.")]
        [Min(0.1f)]
        [SerializeField] float lingerPulseSeconds = 0.5f;

        [Tooltip("What the final burst does to every prism it reaches (the plain crack, usually).")]
        [SerializeField] PayloadPrismEffectSO[] burstEffects = System.Array.Empty<PayloadPrismEffectSO>();

        public float Speed => speed;
        public float LingerPulseSeconds => lingerPulseSeconds;
        public IReadOnlyList<PayloadPrismEffectSO> BurstEffects => burstEffects ?? System.Array.Empty<PayloadPrismEffectSO>();

        public override bool Release(in PayloadLaunch launch)
        {
            if (!launch.Root || launch.Parcel == null || launch.Parcel.Count == 0) return false;
            var flight = launch.Root.gameObject.AddComponent<JavelinFlight>();
            flight.Launch(this, launch);
            return true;
        }

        /// <summary>The flight's numbers at <paramref name="levels"/>, pure (tests read it).</summary>
        public JavelinShot Shot(ArtifactSO artifact, in ArtifactLevels levels) => new(
            width: Mathf.Max(0.5f, artifact.Value(Element.Mass, levels)),
            range: Mathf.Max(1f, artifact.Value(Element.Space, levels)),
            linger: Mathf.Max(0f, artifact.Value(Element.Time, levels)),
            cracks: Mathf.Max(1, Mathf.RoundToInt(artifact.Value(Element.Charge, levels))),
            wideBurst: ArtifactSO.Twisted(Element.Mass, levels),
            pierces: ArtifactSO.Twisted(Element.Space, levels),
            lineLingers: ArtifactSO.Twisted(Element.Time, levels),
            alsoShielded: ArtifactSO.Twisted(Element.Charge, levels));
    }

    /// <summary>A Javelin's numbers for one throw.</summary>
    public readonly struct JavelinShot
    {
        public readonly float Width, Range, Linger;
        public readonly int Cracks;
        public readonly bool WideBurst, Pierces, LineLingers, AlsoShielded;

        public JavelinShot(float width, float range, float linger, int cracks, bool wideBurst, bool pierces,
            bool lineLingers, bool alsoShielded)
        {
            Width = width; Range = range; Linger = linger; Cracks = cracks;
            WideBurst = wideBurst; Pierces = pierces; LineLingers = lineLingers; AlsoShielded = alsoShielded;
        }

        /// <summary>True when the throw stops after <paramref name="cracked"/> cracks.</summary>
        public bool StopsAfter(int cracked) => !Pierces && cracked >= Cracks;
    }

    /// <summary>
    /// One thrown Javelin, on the parcel's root: flies, cracks along its line through the spatial index (one sphere
    /// query a frame), bursts where it stops, lingers, then spends the parcel and removes itself with its root.
    /// </summary>
    public sealed class JavelinFlight : MonoBehaviour
    {
        static readonly List<Prism> s_scratch = new(128);

        JavelinTransformSO _so;
        JavelinShot _shot;
        PayloadRelease _release;
        readonly List<Prism> _parcel = new();
        readonly List<Vector3> _path = new();
        Vector3 _dir;
        float _flown;
        int _cracked;
        bool _stopped;
        float _lingerUntil;
        float _nextPulse;

        internal void Launch(JavelinTransformSO so, in PayloadLaunch launch)
        {
            _so = so;
            _shot = so.Shot(launch.Artifact, launch.Levels);
            _release = launch.Release;
            _parcel.AddRange(launch.Parcel);
            _dir = launch.Release.Forward.sqrMagnitude > 0.001f ? launch.Release.Forward.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_dir);
            _path.Add(transform.position);
        }

        void Update()
        {
            if (!_so) { Destroy(gameObject); return; }
            if (!_stopped) Fly(Time.deltaTime);
            else Linger();
        }

        void Fly(float dt)
        {
            float step = Mathf.Min(_so.Speed * dt, _shot.Range - _flown);
            transform.position += _dir * step;
            _flown += step;
            for (int i = 0; i < _parcel.Count; i++)
                if (_parcel[i] && !_parcel[i].destroyed) _parcel[i].NotifyPositionChanged();
            if ((transform.position - _path[_path.Count - 1]).sqrMagnitude >= _shot.Width * _shot.Width)
                _path.Add(transform.position);

            _cracked += CrackAround(transform.position, _shot.Width);
            if (_flown >= _shot.Range - 0.01f || _shot.StopsAfter(_cracked)) Stop();
        }

        void Stop()
        {
            _stopped = true;
            var cfg = ArkHold.Config;
            float radius = cfg.BurstRadius * (_shot.WideBurst ? 2f : 1f);
            var burst = new PayloadRelease(transform.position, _dir, radius, _release.CarrierName, _release.CarrierDomain);
            PayloadBurst.Detonate(burst, _so.BurstEffects);
            PayloadBurst.SpendParcel(_parcel, burst, transform.parent);
            _release = burst;
            _lingerUntil = Time.time + _shot.Linger;
            _nextPulse = Time.time + _so.LingerPulseSeconds;
            if (_shot.Linger <= 0f) Destroy(gameObject);
        }

        void Linger()
        {
            if (Time.time >= _lingerUntil) { Destroy(gameObject); return; }
            if (Time.time < _nextPulse) return;
            _nextPulse = Time.time + _so.LingerPulseSeconds;
            PayloadBurst.Detonate(_release, _so.BurstEffects);
            if (_shot.LineLingers)
                for (int i = 0; i < _path.Count; i++) CrackAround(_path[i], _shot.Width);
        }

        // Crack rival shields within width of a point on the line. Returns how many super-shields came down.
        int CrackAround(Vector3 at, float width)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return 0;
            index.QuerySphere(at, width, s_scratch);
            int cracked = 0;
            for (int i = 0; i < s_scratch.Count; i++)
            {
                var prism = s_scratch[i];
                if (!prism || prism.destroyed || prism.IsCarried) continue;
                var outcome = SuperShieldCrack.TryCrack(prism, _release.CarrierName, _release.CarrierDomain,
                    _shot.AlsoShielded, leaveRegularShield: true);
                if (outcome == SuperShieldCrack.Outcome.CrackedSuperShield) cracked++;
            }
            s_scratch.Clear();
            return cracked;
        }

        void OnDestroy()
        {
            // Torn down mid-flight (scene unload): the parcel must not die with this root.
            if (_parcel.Count > 0) PayloadBurst.SpendParcel(_parcel, _release, transform.parent);
        }
    }
}

using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Hydra</b> (Docs/Pilots/ARTIFACTS.md §3, offence): on release the payload splits into heads that fan out across
    /// the carrier's heading, each carrying its share of the parcel, and each bursts and cracks where it lands. Where a
    /// Javelin breaks one wall deep, a Hydra opens several at once.
    ///
    /// <para>Its four tracks (the values are on the artifact asset, the twists here):</para>
    /// <list type="bullet">
    /// <item>Mass, each head's burst radius. Twist: each head cracks along its path as it flies.</item>
    /// <item>Space, how far the heads fly. Twist: each head turns toward the nearest rival super-shield ahead of it.</item>
    /// <item>Time, how long each burst lingers, cracking again what is rebuilt. Twist: the lingering bursts grow to
    /// twice their width.</item>
    /// <item>Charge, how many heads. Twist: each head splits again where it lands, into two more bursts beside it.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(fileName = "HydraTransform", menuName = "ScriptableObjects/Pilots/Payload Transforms/Hydra")]
    public class HydraTransformSO : PayloadTransformSO
    {
        [Tooltip("World units per second each head flies.")]
        [Min(1f)]
        [SerializeField] float speed = 200f;

        [Tooltip("Degrees between the outermost heads: the width of the fan across the carrier's heading.")]
        [Range(0f, 180f)]
        [SerializeField] float fanDegrees = 50f;

        [Tooltip("How far off its fan line a seeking head (the Space twist) may turn, in degrees.")]
        [Range(0f, 90f)]
        [SerializeField] float seekDegrees = 30f;

        [Tooltip("Seconds between a lingering burst's re-cracks.")]
        [Min(0.1f)]
        [SerializeField] float lingerPulseSeconds = 0.5f;

        public float Speed => speed;
        public float FanDegrees => fanDegrees;
        public float SeekDegrees => seekDegrees;
        public float LingerPulseSeconds => lingerPulseSeconds;

        public override bool Release(in PayloadLaunch launch)
        {
            if (!launch.Root || launch.Parcel == null || launch.Parcel.Count == 0) return false;
            var flight = launch.Root.gameObject.AddComponent<HydraFlight>();
            flight.Launch(this, launch);
            return true;
        }

        /// <summary>The split's numbers at <paramref name="levels"/>, pure (tests read it).</summary>
        public HydraShot Shot(ArtifactSO artifact, in ArtifactLevels levels) => new(
            radius: Mathf.Max(1f, artifact.Value(Element.Mass, levels)),
            range: Mathf.Max(1f, artifact.Value(Element.Space, levels)),
            linger: Mathf.Max(0f, artifact.Value(Element.Time, levels)),
            heads: Mathf.Max(1, Mathf.RoundToInt(artifact.Value(Element.Charge, levels))),
            cracksInFlight: ArtifactSO.Twisted(Element.Mass, levels),
            seeks: ArtifactSO.Twisted(Element.Space, levels),
            lingerGrows: ArtifactSO.Twisted(Element.Time, levels),
            splitsAgain: ArtifactSO.Twisted(Element.Charge, levels));

        /// <summary>Head <paramref name="i"/> of <paramref name="heads"/>'s heading: spread evenly across the fan, in the
        /// plane of <paramref name="forward"/> and <paramref name="up"/>'s right. Pure (tests read it).</summary>
        public static Vector3 HeadDirection(Vector3 forward, Vector3 up, int i, int heads, float fanDegrees)
        {
            if (heads <= 1) return forward.normalized;
            float t = i / (float)(heads - 1) - 0.5f;
            var axis = Vector3.Cross(forward, Vector3.Cross(up, forward)).normalized;
            if (axis.sqrMagnitude < 0.5f) axis = Vector3.up;
            return Quaternion.AngleAxis(t * fanDegrees, axis) * forward.normalized;
        }
    }

    /// <summary>A Hydra's numbers for one release.</summary>
    public readonly struct HydraShot
    {
        public readonly float Radius, Range, Linger;
        public readonly int Heads;
        public readonly bool CracksInFlight, Seeks, LingerGrows, SplitsAgain;

        public HydraShot(float radius, float range, float linger, int heads, bool cracksInFlight, bool seeks,
            bool lingerGrows, bool splitsAgain)
        {
            Radius = radius; Range = range; Linger = linger; Heads = heads;
            CracksInFlight = cracksInFlight; Seeks = seeks; LingerGrows = lingerGrows; SplitsAgain = splitsAgain;
        }

        /// <summary>A lingering burst's radius <paramref name="t"/> (0 to 1) of the way through its linger.</summary>
        public float LingerRadius(float t) => LingerGrows ? Radius * (1f + Mathf.Clamp01(t)) : Radius;
    }

    /// <summary>
    /// One released Hydra, on the parcel's root: the parcel is dealt out to the heads (each a child of the root that
    /// carries its prisms), the heads fly and burst on their own, and the root removes itself once every head is done.
    /// </summary>
    public sealed class HydraFlight : MonoBehaviour
    {
        sealed class Head
        {
            public Transform Body;
            public readonly List<Prism> Parcel = new();
            public Vector3 Dir;
            public float Range, Flown, LandedAt;
            public bool Landed, Done;
            public float NextPulse;
        }

        HydraTransformSO _so;
        HydraShot _shot;
        PayloadRelease _release;
        readonly List<Head> _heads = new();

        internal void Launch(HydraTransformSO so, in PayloadLaunch launch)
        {
            _so = so;
            _shot = so.Shot(launch.Artifact, launch.Levels);
            _release = launch.Release;
            var forward = launch.Release.Forward.sqrMagnitude > 0.001f ? launch.Release.Forward.normalized : transform.forward;
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            int heads = Mathf.Min(_shot.Heads, launch.Parcel.Count);
            for (int i = 0; i < heads; i++)
            {
                var head = new Head { Dir = HydraTransformSO.HeadDirection(forward, up, i, heads, so.FanDegrees), Range = _shot.Range };
                if (_shot.Seeks)
                {
                    var target = PayloadCrackArea.NearestAhead(transform.position, head.Dir, _shot.Range, so.SeekDegrees,
                        launch.Release.CarrierDomain);
                    if (target)
                    {
                        var to = target.transform.position - transform.position;
                        head.Dir = to.normalized;
                        head.Range = Mathf.Max(1f, to.magnitude);
                    }
                }
                head.Body = new GameObject("HydraHead").transform;
                head.Body.SetParent(transform, false);
                head.Body.rotation = Quaternion.LookRotation(head.Dir);
                _heads.Add(head);
            }
            // Deal the parcel round the heads, so each carries its share of the mass.
            for (int p = 0; p < launch.Parcel.Count; p++)
            {
                var prism = launch.Parcel[p];
                if (!prism) continue;
                var head = _heads[p % _heads.Count];
                prism.transform.SetParent(head.Body, true);
                head.Parcel.Add(prism);
            }
        }

        void Update()
        {
            if (!_so) { Destroy(gameObject); return; }
            bool allDone = true;
            float dt = Time.deltaTime;
            for (int i = 0; i < _heads.Count; i++)
            {
                var head = _heads[i];
                if (head.Done) continue;
                allDone = false;
                if (!head.Landed) Fly(head, dt);
                else Linger(head);
            }
            if (allDone) Destroy(gameObject);
        }

        void Fly(Head head, float dt)
        {
            float step = Mathf.Min(_so.Speed * dt, head.Range - head.Flown);
            head.Body.position += head.Dir * step;
            head.Flown += step;
            for (int p = 0; p < head.Parcel.Count; p++)
                if (head.Parcel[p] && !head.Parcel[p].destroyed) head.Parcel[p].NotifyPositionChanged();
            if (_shot.CracksInFlight)
                PayloadCrackArea.Crack(head.Body.position, _shot.Radius * 0.5f, _release.CarrierName, _release.CarrierDomain);
            if (head.Flown >= head.Range - 0.01f) Land(head);
        }

        void Land(Head head)
        {
            head.Landed = true;
            var at = head.Body.position;
            Burst(at, _shot.Radius);
            if (_shot.SplitsAgain)
            {
                var side = Vector3.Cross(head.Dir, Vector3.up);
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(head.Dir, Vector3.forward);
                side = side.normalized * _shot.Radius;
                Burst(at + side, _shot.Radius * 0.6f);
                Burst(at - side, _shot.Radius * 0.6f);
            }
            var burst = new PayloadRelease(at, head.Dir, _shot.Radius, _release.CarrierName, _release.CarrierDomain);
            PayloadBurst.SpendParcel(head.Parcel, burst, transform.parent);
            head.LandedAt = Time.time;
            head.NextPulse = Time.time + _so.LingerPulseSeconds;
            if (_shot.Linger <= 0f) head.Done = true;
        }

        void Linger(Head head)
        {
            float age = Time.time - head.LandedAt;
            if (age >= _shot.Linger) { head.Done = true; return; }
            if (Time.time < head.NextPulse) return;
            head.NextPulse = Time.time + _so.LingerPulseSeconds;
            Burst(head.Body.position, _shot.LingerRadius(age / _shot.Linger));
        }

        void Burst(Vector3 at, float radius) =>
            PayloadCrackArea.Crack(at, radius, _release.CarrierName, _release.CarrierDomain);

        void OnDestroy()
        {
            // Torn down mid-flight (scene unload): no head's parcel may die with this root.
            for (int i = 0; i < _heads.Count; i++)
                if (_heads[i].Parcel.Count > 0) PayloadBurst.SpendParcel(_heads[i].Parcel, _release, transform.parent);
        }
    }
}

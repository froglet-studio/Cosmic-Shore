using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Aftershock</b> (Docs/Pilots/ARTIFACTS.md §3, offence): the payload lands down the carrier's heading and cracks,
    /// then cracks AGAIN in the same place, and again, catching whatever the rival rebuilt between. The parcel is spent
    /// at the first shock (mass is conserved); every later shock is a crack and builds nothing.
    ///
    /// <para>Its four tracks (the values are on the artifact asset, the twists here):</para>
    /// <list type="bullet">
    /// <item>Mass, the shocks' radius. Twist: the last shock reaches twice as far.</item>
    /// <item>Space, how far down your heading it lands. Twist: every shock after the first rings four smaller shocks
    /// around it.</item>
    /// <item>Time, the seconds between shocks. Twist: one more shock follows.</item>
    /// <item>Charge, how many shocks. Twist: the later shocks shatter, leaving no shield behind.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(fileName = "AftershockTransform", menuName = "ScriptableObjects/Pilots/Payload Transforms/Aftershock")]
    public class AftershockTransformSO : PayloadTransformSO
    {
        [Tooltip("The ring's shocks (the Space twist) as a share of the main shock's radius.")]
        [Range(0.1f, 1f)]
        [SerializeField] float ringShare = 0.5f;

        public float RingShare => ringShare;

        public override bool Release(in PayloadLaunch launch)
        {
            if (!launch.Root || launch.Parcel == null || launch.Parcel.Count == 0) return false;
            var shot = Shot(launch.Artifact, launch.Levels);
            var forward = launch.Release.Forward.sqrMagnitude > 0.001f ? launch.Release.Forward.normalized : launch.Root.forward;
            var at = launch.Release.Center + forward * shot.Throw;
            var first = new PayloadRelease(at, forward, shot.RadiusOf(0), launch.Release.CarrierName,
                launch.Release.CarrierDomain);
            PayloadCrackArea.Crack(at, shot.RadiusOf(0), first.CarrierName, first.CarrierDomain);
            launch.Root.position = at;
            PayloadBurst.SpendParcel(launch.Parcel, first, launch.Root.parent);
            if (shot.Shocks <= 1) { Destroy(launch.Root.gameObject); return true; }

            var quake = launch.Root.gameObject.AddComponent<AftershockQuake>();
            quake.Begin(this, shot, first);
            return true;
        }

        /// <summary>The shocks' numbers at <paramref name="levels"/>, pure (tests read it).</summary>
        public AftershockShot Shot(ArtifactSO artifact, in ArtifactLevels levels) => new(
            radius: Mathf.Max(1f, artifact.Value(Element.Mass, levels)),
            throwDistance: Mathf.Max(0f, artifact.Value(Element.Space, levels)),
            interval: Mathf.Max(0.25f, artifact.Value(Element.Time, levels)),
            shocks: Mathf.Max(1, Mathf.RoundToInt(artifact.Value(Element.Charge, levels)))
                    + (ArtifactSO.Twisted(Element.Time, levels) ? 1 : 0),
            lastReachesFar: ArtifactSO.Twisted(Element.Mass, levels),
            rings: ArtifactSO.Twisted(Element.Space, levels),
            shatters: ArtifactSO.Twisted(Element.Charge, levels));
    }

    /// <summary>An Aftershock's numbers for one release.</summary>
    public readonly struct AftershockShot
    {
        public readonly float Radius, Throw, Interval;
        public readonly int Shocks;
        public readonly bool LastReachesFar, Rings, Shatters;

        public AftershockShot(float radius, float throwDistance, float interval, int shocks, bool lastReachesFar,
            bool rings, bool shatters)
        {
            Radius = radius; Throw = throwDistance; Interval = interval; Shocks = shocks;
            LastReachesFar = lastReachesFar; Rings = rings; Shatters = shatters;
        }

        /// <summary>Shock <paramref name="i"/>'s radius (0 is the first).</summary>
        public float RadiusOf(int i) => LastReachesFar && i == Shocks - 1 && Shocks > 1 ? Radius * 2f : Radius;
    }

    /// <summary>The later shocks of one Aftershock, on its spent root, in the same place; removes itself when done.</summary>
    public sealed class AftershockQuake : MonoBehaviour
    {
        static readonly Vector3[] s_ring = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };

        AftershockTransformSO _so;
        AftershockShot _shot;
        PayloadRelease _at;
        int _next = 1;
        float _nextAt;

        internal void Begin(AftershockTransformSO so, in AftershockShot shot, in PayloadRelease at)
        {
            _so = so;
            _shot = shot;
            _at = at;
            _nextAt = Time.time + shot.Interval;
        }

        void Update()
        {
            if (!_so) { Destroy(gameObject); return; }
            if (Time.time < _nextAt) return;
            float radius = _shot.RadiusOf(_next);
            bool leave = !_shot.Shatters;
            PayloadCrackArea.Crack(_at.Center, radius, _at.CarrierName, _at.CarrierDomain, leave);
            if (_shot.Rings)
            {
                var basis = Quaternion.LookRotation(_at.Forward.sqrMagnitude > 0.001f ? _at.Forward : Vector3.forward);
                for (int i = 0; i < s_ring.Length; i++)
                    PayloadCrackArea.Crack(_at.Center + basis * s_ring[i] * radius, radius * _so.RingShare,
                        _at.CarrierName, _at.CarrierDomain, leave);
            }
            _next++;
            _nextAt = Time.time + _shot.Interval;
            if (_next >= _shot.Shocks) Destroy(gameObject);
        }
    }
}

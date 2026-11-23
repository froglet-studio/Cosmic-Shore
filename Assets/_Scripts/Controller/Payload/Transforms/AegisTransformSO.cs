using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Aegis</b> (Docs/Pilots/ARTIFACTS.md §3, defence): released at your own Ark, the loaded payload super-shields
    /// the Ark's hull for a while, protecting everything aboard. Released anywhere else it is refused and stays on
    /// your hull, unless its Space twist lets it reach home from anywhere.
    ///
    /// <para>Its four tracks (the values are on the artifact asset, the twists here):</para>
    /// <list type="bullet">
    /// <item>Mass, the share of the hull it covers, nearest the release first. Twist: the whole hull.</item>
    /// <item>Space, how far from the Ark it may be released. Twist: from anywhere.</item>
    /// <item>Time, how long the ward stands. Twist: it leaves an ordinary shield behind when it ends.</item>
    /// <item>Charge, how many times a cracked plate raises its super-shield again. Twist: it re-raises at once.</item>
    /// </list>
    ///
    /// <para>The parcel is still spent: every payload thins the hull it came from, Aegis included.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AegisTransform", menuName = "ScriptableObjects/Pilots/Payload Transforms/Aegis")]
    public class AegisTransformSO : PayloadTransformSO
    {
        [Tooltip("Seconds a cracked plate waits before its super-shield is raised again (Charge's re-raises).")]
        [Min(0f)]
        [SerializeField] float reraiseDelay = 3f;

        [Tooltip("Banner when an Aegis is released too far from the squad's Ark. Authored; empty shows nothing.")]
        [SerializeField] string notHomeLine = "";

        public float ReraiseDelay => reraiseDelay;
        public override string RefusedLine => notHomeLine;

        static readonly List<Prism> s_hull = new(256);

        public override bool Release(in PayloadLaunch launch)
        {
            var home = launch.Home;
            if (!home || !home.Ark || home.Ark.IsHullLost) return false;
            var ward = Ward(launch.Artifact, launch.Levels);
            if (!ward.Reaches((launch.Release.Center - home.Ark.Position).magnitude)) return false;

            home.Ark.GetLiveHullPrisms(launch.Release.Center, s_hull);
            int count = ward.Covers(s_hull.Count);
            var aegis = home.Ark.gameObject.AddComponent<AegisWard>();
            aegis.Raise(this, ward, s_hull, count);
            s_hull.Clear();

            PayloadBurst.SpendParcel(launch.Parcel, launch.Release, launch.Root ? launch.Root.parent : null);
            return true;
        }

        /// <summary>The ward's numbers at <paramref name="levels"/>, pure (tests read it).</summary>
        public AegisShot Ward(ArtifactSO artifact, in ArtifactLevels levels) => new(
            share: Mathf.Clamp01(artifact.Value(Element.Mass, levels)),
            reach: Mathf.Max(0f, artifact.Value(Element.Space, levels)),
            seconds: Mathf.Max(0.1f, artifact.Value(Element.Time, levels)),
            reraises: Mathf.Max(0, Mathf.RoundToInt(artifact.Value(Element.Charge, levels))),
            wholeHull: ArtifactSO.Twisted(Element.Mass, levels),
            fromAnywhere: ArtifactSO.Twisted(Element.Space, levels),
            leavesShield: ArtifactSO.Twisted(Element.Time, levels),
            reraisesAtOnce: ArtifactSO.Twisted(Element.Charge, levels));
    }

    /// <summary>An Aegis's numbers for one release.</summary>
    public readonly struct AegisShot
    {
        public readonly float Share, Reach, Seconds;
        public readonly int Reraises;
        public readonly bool WholeHull, FromAnywhere, LeavesShield, ReraisesAtOnce;

        public AegisShot(float share, float reach, float seconds, int reraises, bool wholeHull, bool fromAnywhere,
            bool leavesShield, bool reraisesAtOnce)
        {
            Share = share; Reach = reach; Seconds = seconds; Reraises = reraises;
            WholeHull = wholeHull; FromAnywhere = fromAnywhere; LeavesShield = leavesShield; ReraisesAtOnce = reraisesAtOnce;
        }

        /// <summary>True when a release <paramref name="distance"/> from the Ark's centre reaches it.</summary>
        public bool Reaches(float distance) => FromAnywhere || distance <= Reach;

        /// <summary>How many of <paramref name="hull"/> live plates the ward covers.</summary>
        public int Covers(int hull) => WholeHull ? hull : Mathf.Clamp(Mathf.CeilToInt(hull * Share - 1e-4f), 0, hull);
    }

    /// <summary>One standing Aegis on an Ark: raises super-shields on the plates it covers, re-raises cracked ones as
    /// its Charge allows, and lowers only the shields it raised when it ends.</summary>
    public sealed class AegisWard : MonoBehaviour
    {
        const float TickSeconds = 0.25f;

        AegisTransformSO _so;
        AegisShot _shot;
        readonly List<Prism> _plates = new();
        readonly List<int> _reraisesLeft = new();
        readonly List<float> _reraiseAt = new();
        float _endsAt;
        float _nextTick;

        public int Plates => _plates.Count;

        internal void Raise(AegisTransformSO so, in AegisShot shot, List<Prism> hull, int count)
        {
            _so = so;
            _shot = shot;
            _endsAt = Time.time + shot.Seconds;
            for (int i = 0; i < count && i < hull.Count; i++)
            {
                var prism = hull[i];
                // A plate already super-shielded is not ours to lower later.
                if (!prism || prism.prismProperties is { IsSuperShielded: true }) continue;
                prism.ActivateSuperShield();
                _plates.Add(prism);
                _reraisesLeft.Add(shot.Reraises);
                _reraiseAt.Add(-1f);
            }
        }

        void Update()
        {
            if (Time.time >= _endsAt) { Lower(); Destroy(this); return; }
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickSeconds;
            float delay = _shot.ReraisesAtOnce || !_so ? 0f : _so.ReraiseDelay;
            for (int i = 0; i < _plates.Count; i++)
            {
                var prism = _plates[i];
                if (!prism || prism.destroyed || prism.prismProperties is { IsSuperShielded: true } || _reraisesLeft[i] <= 0)
                    continue;
                if (_reraiseAt[i] < 0f) { _reraiseAt[i] = Time.time + delay; continue; }
                if (Time.time < _reraiseAt[i]) continue;
                prism.ActivateSuperShield();
                _reraisesLeft[i]--;
                _reraiseAt[i] = -1f;
            }
        }

        void Lower()
        {
            for (int i = 0; i < _plates.Count; i++)
            {
                var prism = _plates[i];
                if (!prism || prism.destroyed || prism.prismProperties is not { IsSuperShielded: true }) continue;
                prism.DeactivateShields();
                if (_shot.LeavesShield) prism.ActivateShield();
            }
            _plates.Clear();
        }
    }
}

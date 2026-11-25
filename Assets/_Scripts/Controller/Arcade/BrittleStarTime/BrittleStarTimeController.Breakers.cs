using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's breakers in the world (BRITTLESTARTIME.md section 0b): at I4, a disc of plain slabs across the
    /// way into every ring and along the return straight (<see cref="BrittleStarTimeBreakers"/>), laid on every peer from
    /// the broadcast course in the let-go posts' prism. A hull that rams one is slowed by its own kit; its guns and cords
    /// clear a hole. A disc that was broken stands back up once no hull is near it (Headlong's hedge rule), so every lap
    /// meets it whole. Nothing here reads or writes a vessel.
    /// </summary>
    public partial class BrittleStarTimeController
    {
        const string BreakerOwnerId = "BrittleStarTimeBreaker";

        [Header("Breakers (I4)")]
        [Tooltip("The domain colour the breakers are laid in, apart from the let-go posts (Blue) and the ruins " +
                 "(Yash, 2026-11-24: the Brittle Star breaks through obstacles to pass the rings).")]
        [SerializeField] Domains breakerDomain = Domains.Ruby;

        [Tooltip("Seconds after a breaker was last broken before it stands back up, once no hull is near it. Shorter " +
                 "than a lap, so every lap meets it whole.")]
        [SerializeField, Min(0.5f)] float breakerRestandSeconds = 2.5f;

        [Tooltip("Slabs a breaker puts back each frame while it stands up.")]
        [SerializeField, Min(1)] int breakerRestandPerFrame = 40;

        [Tooltip("How far (u) beyond a breaker's radius a hull keeps it from standing back up. Headlong's hedges use " +
                 "150; the coil's rings stand ~170 u apart, so at 150 some hull is nearly always near and the discs " +
                 "stood at ~70% (measured 2026-11-24).")]
        [SerializeField, Min(0f)] float breakerHullReach = 60f;

        sealed class StandingBreaker
        {
            public HeadlongHedges.Hedge Hedge;
            public readonly List<int> Slabs = new();   // indices into _breakerSlabs
            public readonly Trail Trail = new();        // its own, so a broken slab leaves it before the pool re-issues it
            public float DisturbedAt = -1f;
            public int Cursor = -1;                    // >= 0 while standing back up
        }

        readonly List<HeadlongHedges.Hedge> _breakerHedges = new();
        readonly List<HeadlongHedges.Part> _breakerSlabs = new();
        readonly List<int> _breakerOwner = new();
        readonly List<Prism> _breakerPrisms = new();
        readonly List<StandingBreaker> _breakers = new();
        Transform _breakerRoot;
        float _nextBreakerCheck;

        /// <summary>The slabs the ruins keep off (laid first).</summary>
        IReadOnlyList<HeadlongHedges.Part> BreakerSlabs => _breakerSlabs;

        void LayBreakers()
        {
            ClearBreakers();
            if (!postPrefab || _course.Count == 0 || !BrittleStarTimeBreakers.On(Intensity)) return;
            float turn = BrittleStarTimeCourse.CornerRadiusAt(TopSpeed);
            BrittleStarTimeBreakers.Place(_course, Intensity, turn, _breakerHedges);
            BrittleStarTimeBreakers.Build(_course, _breakerHedges, BrittleStarTimeLine.Marks(_course, TopSpeed),
                                          _breakerSlabs, _breakerOwner);
            if (_breakerSlabs.Count == 0) return;
            _breakerRoot = new GameObject($"{ModeName}Breakers").transform;
            for (int h = 0; h < _breakerHedges.Count; h++) _breakers.Add(new StandingBreaker { Hedge = _breakerHedges[h] });
            for (int i = 0; i < _breakerSlabs.Count; i++)
            {
                _breakers[_breakerOwner[i]].Slabs.Add(i);
                _breakerPrisms.Add(LayBreakerSlab(i, forReveal: true));
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[{ModeName}] intensity {Intensity}: {_breakers.Count} breakers, {_breakerSlabs.Count} slabs.");
        }

        Prism LayBreakerSlab(int i, bool forReveal)
        {
            var part = _breakerSlabs[i];
            var prism = PrismTrailBuilder.LayOne(postPrefab,
                new PrismLay(new SpawnPoint(part.Position, part.Rotation, part.Scale), breakerDomain, part.Kind),
                _breakerRoot, _breakers[_breakerOwner[i]].Trail, BreakerOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        static bool HoldsBreaker(Prism prism) => prism && !prism.destroyed && prism.ownerID == BreakerOwnerId;

        bool BreakerDisplaced(int i) =>
            !HoldsBreaker(_breakerPrisms[i]) ||
            (_breakerPrisms[i].transform.position - _breakerSlabs[i].Position).sqrMagnitude > 1f;

        /// <summary>Every frame: a broken breaker, left alone long enough, stands back up a few slabs at a time.</summary>
        void TickBreakers()
        {
            if (_breakers.Count == 0) return;
            TickBreakerRestand();
            float now = Time.time;
            if (now < _nextBreakerCheck) return;
            _nextBreakerCheck = now + 0.5f;
            for (int b = 0; b < _breakers.Count; b++)
            {
                var standing = _breakers[b];
                if (standing.Cursor >= 0) continue;
                bool disturbed = false;
                for (int k = 0; k < standing.Slabs.Count && !disturbed; k++) disturbed = BreakerDisplaced(standing.Slabs[k]);
                if (!disturbed) { standing.DisturbedAt = -1f; continue; }
                if (HullNear(standing.Hedge.Centre, standing.Hedge.Radius + breakerHullReach)) { standing.DisturbedAt = now; continue; }
                if (standing.DisturbedAt < 0f) standing.DisturbedAt = now;
                if (now - standing.DisturbedAt >= breakerRestandSeconds) standing.Cursor = 0;
            }
        }

        void TickBreakerRestand()
        {
            for (int b = 0; b < _breakers.Count; b++)
            {
                var standing = _breakers[b];
                if (standing.Cursor < 0) continue;
                int budget = breakerRestandPerFrame;
                while (budget > 0 && standing.Cursor < standing.Slabs.Count)
                {
                    int i = standing.Slabs[standing.Cursor++];
                    if (!BreakerDisplaced(i)) continue;
                    var prism = _breakerPrisms[i];
                    if (HoldsBreaker(prism))
                    {
                        prism.transform.SetPositionAndRotation(_breakerSlabs[i].Position, _breakerSlabs[i].Rotation);
                        prism.NotifyPositionChanged();
                    }
                    else
                    {
                        ReleaseBroken(prism, BreakerOwnerId, standing.Trail);
                        _breakerPrisms[i] = LayBreakerSlab(i, forReveal: false);
                    }
                    budget--;
                }
                if (standing.Cursor >= standing.Slabs.Count)
                {
                    standing.Cursor = -1;
                    standing.DisturbedAt = -1f;
                }
            }
        }

        /// <summary>
        /// A broken prism of ours goes back to the environment pool before its place is laid again. Without this every
        /// break left its prism hidden under the root for the rest of the race (4,075 of them in one measured I4 race,
        /// 2026-11-24), and the pool kept cloning new ones. It leaves <paramref name="trail"/> first: the pool hands the
        /// same prism straight back to the next lay, which would add it to the trail a second time. A prism the pool
        /// re-issued to someone else is not ours to touch.
        /// </summary>
        static void ReleaseBroken(Prism prism, string ownerId, Trail trail)
        {
            if (!prism || !prism.destroyed || prism.ownerID != ownerId) return;
            if (trail != null)
            {
                s_releaseScratch.Clear();
                s_releaseScratch.Add(prism);
                trail.RemoveMembers(s_releaseScratch);
            }
            if (!EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
        }

        static readonly HashSet<Prism> s_releaseScratch = new();

        bool HullNear(Vector3 centre, float reach)
        {
            var players = gameData ? gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - centre).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        void ClearBreakers()
        {
            for (int i = 0; i < _breakerPrisms.Count; i++)
            {
                var prism = _breakerPrisms[i];
                if (prism && prism.ownerID == BreakerOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _breakerPrisms.Clear();
            _breakerSlabs.Clear();
            _breakerOwner.Clear();
            _breakerHedges.Clear();
            _breakers.Clear();
            if (_breakerRoot) Destroy(_breakerRoot.gameObject);
            _breakerRoot = null;
        }
    }
}

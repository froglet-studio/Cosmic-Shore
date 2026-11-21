using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Skim Race / Joust trail cap on a device tier that asks for one (MobileLow): every vessel
    /// in the match keeps its share of a race-wide budget of trail prisms
    /// (<see cref="PlatformProfileSO.RaceTrailBudget"/>), and past it the OLDEST prism withers away
    /// and returns to its pool. Garrett's Android strip ran exactly this cap and these numbers;
    /// without it a 12-seat race lays trail without bound on a phone that cannot draw it.
    ///
    /// <b>AUTHORIZED EXCEPTION — this is the second place trail mass is recycled.</b> Passive removal
    /// of trail mass is forbidden platform-wide (Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md ▸ <i>Don't
    /// cheat emergence</i>; the reverted <c>maxTrailBlocks</c> ring buffer is the named
    /// counter-example). The project owner granted this carve-out on 2026-10-05 for phones that
    /// cannot carry an uncapped race. It is recorded in <c>Docs/ECOSYSTEM.md</c> §0 beside the
    /// Wanderway tether it is modelled on, and it is fenced:
    ///   • it runs only where a <see cref="PlatformProfileSO"/> sets a race budget (MobileLow) —
    ///     Desktop and MobileHigh carry no budget, so Windows and iOS never add this component;
    ///   • its only callers are <see cref="SkimRaceController"/> and <see cref="JoustController"/>,
    ///     through <see cref="Attach"/>, and it dies with the race scene;
    ///   • <see cref="VesselPrismController"/> grows no cap field: the race reaches in from outside.
    /// Do not attach it from anywhere else, and do not generalise it into a trail length limit.
    ///
    /// Continuity of existence is NOT waived: a retiring prism withers on the GPU clock (one
    /// grow-clock re-stamp toward <see cref="RetiredScale"/>) and goes back to the pool only once it
    /// has shrunk away — the tether's recipe. The strip consumed it instead (an implosion per prism,
    /// a destroyed-but-live object left behind, and a shielded prism only lost its shield); the
    /// wither frees the object and removes shielded and unshielded prisms alike.
    ///
    /// Trail prisms are local to each peer (nothing in <see cref="VesselPrismController"/> is
    /// networked), so the cap is per device: in a match between a MobileLow phone and a PC, the
    /// phone sees every ribbon end two laps back while the PC still draws the whole trail.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceTrailCap : MonoBehaviour
    {
        const float TickSeconds = 0.2f;

        /// <summary>Bound on retirements per vessel per tick: a burst (a lag spike, a boost) drains
        /// over a few ticks rather than stalling one.</summary>
        const int MaxRetiresPerVesselPerTick = 64;

        /// <summary>Scale a retiring prism withers to before it returns to the pool (the tether's).</summary>
        static readonly Vector3 RetiredScale = new(0.02f, 0.02f, 0.02f);

        /// <summary>Seconds a retiring prism withers for before it is handed back to the pool.</summary>
        const float WitherSeconds = 0.8f;

        static readonly ProfilerMarker s_holdMarker = new("RaceTrailCap.Hold");

        GameDataSO _gameData;
        PlatformProfileSO.RaceTrailBudget _budget;
        float _nextTickAt;
        // The ribbon each prism was detached from rides along: a prism its pool took back early and
        // handed to another trail must not be pool-returned out from under that trail.
        readonly List<(Prism prism, float dueAt, Trail from)> _withering = new();
        readonly HashSet<VesselPrismController> _stallReported = new();

        /// <summary>
        /// Cap the race's trails to <paramref name="budget"/>, or do nothing when the tier sets no
        /// budget. Idempotent: a second call on the same race updates the budget.
        /// </summary>
        public static void Attach(MiniGameControllerBase race, GameDataSO gameData,
                                  PlatformProfileSO.RaceTrailBudget budget)
        {
            if (!race || !gameData || !budget.Active) return;

            if (!race.TryGetComponent(out RaceTrailCap cap))
                cap = race.gameObject.AddComponent<RaceTrailCap>();
            cap._gameData = gameData;
            cap._budget = budget;

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[RaceTrailCap] {race.GetType().Name}: {budget.PerVessel(SeatCount(gameData, 0))} prisms per vessel.");
        }

        void Update()
        {
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            RetireDueWithering();

            var players = _gameData ? _gameData.Players : null;
            if (players == null) return;

            int vessels = 0;
            foreach (var player in players)
                if (ControllerOf(player)) vessels++;

            // The seat count, not the vessels alive this tick: a respawn gap must not hand the
            // survivors a larger share for two seconds and then claw it back.
            int cap = _budget.PerVessel(SeatCount(_gameData, vessels));

            foreach (var player in players)
            {
                var controller = ControllerOf(player);
                if (controller) Hold(controller, cap);
            }
        }

        static int SeatCount(GameDataSO gameData, int liveVessels)
        {
            int selected = gameData.SelectedPlayerCount ? gameData.SelectedPlayerCount.Value : 0;
            return Math.Max(1, Math.Max(selected, liveVessels));
        }

        /// <summary>The player's trail controller, or null. Each hop is checked for a destroyed
        /// object: the accessors below a player resolve lazily (GetComponent / GetOrAdd), which a
        /// vessel torn down mid-race would throw on.</summary>
        static VesselPrismController ControllerOf(IPlayer player)
        {
            if (!Alive(player)) return null;
            var vessel = player.Vessel;
            if (!Alive(vessel)) return null;
            var status = vessel.VesselStatus;
            return Alive(status) ? status.VesselPrismController : null;
        }

        static bool Alive(object o) => o is UnityEngine.Object unityObject ? (bool)unityObject : o != null;

        /// <summary>
        /// Hold the vessel's trail at <paramref name="cap"/> prisms across BOTH ribbons (the strip's
        /// cap counted every prism the vessel laid, and a double-trail vessel lays every other prism
        /// in <see cref="VesselPrismController.SecondaryTrail"/>). The ribbons alternate, so taking
        /// from the longer until they are level, then evenly, retires the vessel's oldest prisms to
        /// within one. Each ribbon is cut in ONE <see cref="Trail.RemoveOldest(int)"/> - the per-prism
        /// form re-indexes the whole ribbon every call.
        /// </summary>
        void Hold(VesselPrismController controller, int cap)
        {
            using var _ = s_holdMarker.Auto();

            var primary = controller.Trail;
            var secondary = controller.SecondaryTrail;
            int a = primary?.TrailList.Count ?? 0;
            int b = secondary?.TrailList.Count ?? 0;
            int excess = Math.Min(a + b - cap, MaxRetiresPerVesselPerTick);
            if (excess <= 0) return;

            int fromA, fromB;
            if (a - b >= excess) { fromA = excess; fromB = 0; }
            else if (b - a >= excess) { fromA = 0; fromB = excess; }
            else
            {
                int rest = excess - Math.Abs(a - b);
                fromA = Math.Max(0, a - b) + (rest + 1) / 2;
                fromB = excess - fromA;
            }

            int doneA = RetireOldest(primary, fromA);
            int doneB = RetireOldest(secondary, fromB);
            // A ribbon held up by an unpooled prism hands its share to the other one.
            if (doneA < fromA) doneB += RetireOldest(secondary, fromA - doneA);
            if (doneB < fromB) doneA += RetireOldest(primary, fromB - doneB);

            if (doneA + doneB < excess && _stallReported.Add(controller))
                CSDebug.LogWarning($"[RaceTrailCap] {controller.name}: the oldest trail prism is not " +
                                   "pooled, so this vessel's trail cannot be held at its cap.");
        }

        /// <summary>
        /// Start up to <paramref name="count"/> of the ribbon's oldest prisms withering and detach
        /// them in one cut. Stops early at a prism that is not ours to recycle (unpooled), so the
        /// ribbon stays contiguous and is retried next tick. Returns how many slots were removed.
        /// </summary>
        int RetireOldest(Trail trail, int count)
        {
            if (trail == null || count <= 0) return 0;

            var list = trail.TrailList;
            int take = 0;
            for (; take < count && take < list.Count; take++)
            {
                var prism = list[take];

                // Already gone - eaten, exploded, or taken back by its pool - so the slot is dropped
                // and nothing withers. A consumed prism stays ACTIVE with destroyed = true, so that
                // test is separate; withering or pool-returning it would fight whoever took it
                // (Ark.RetireOldestWake and ArkwayRun make the same calls).
                if (!prism || prism.destroyed || !prism.gameObject.activeInHierarchy || prism.Trail != trail)
                    continue;

                // An unpooled prism has nowhere to go: shrinking it would leave an invisible collider.
                if (prism.OnReturnToPool == null) break;

                prism.TargetScale = RetiredScale;
                _withering.Add((prism, Time.time + WitherSeconds, trail));
            }

            if (take > 0) trail.RemoveOldest(take);
            return take;
        }

        /// <summary>A withered prism goes back to its pool only if it is still the one we detached:
        /// not eaten while it withered, and not already reissued to another trail.</summary>
        static bool StillOurs(Prism prism, Trail from) =>
            prism && !prism.destroyed && prism.Trail == from;

        void RetireDueWithering()
        {
            for (int i = _withering.Count - 1; i >= 0; i--)
            {
                var (prism, dueAt, from) = _withering[i];
                if (prism && Time.time < dueAt) continue;
                _withering.RemoveAt(i);
                if (StillOurs(prism, from)) prism.ReturnToPool();
            }
        }

        void OnDestroy()
        {
            // Race teardown: hand everything still mid-wither straight back (as the tether does).
            foreach (var (prism, _, from) in _withering)
                if (StillOurs(prism, from)) prism.ReturnToPool();
            _withering.Clear();
        }
    }
}

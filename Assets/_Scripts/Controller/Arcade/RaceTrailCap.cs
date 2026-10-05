using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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

        GameDataSO _gameData;
        PlatformProfileSO.RaceTrailBudget _budget;
        float _nextTickAt;
        readonly List<(Prism prism, float dueAt)> _withering = new();

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
        /// in <see cref="VesselPrismController.SecondaryTrail"/>). The ribbons alternate, so the
        /// longer one's oldest prism is the vessel's oldest to within one.
        /// </summary>
        void Hold(VesselPrismController controller, int cap)
        {
            var primary = controller.Trail;
            var secondary = controller.SecondaryTrail;

            for (int guard = MaxRetiresPerVesselPerTick; guard > 0; guard--)
            {
                int a = primary?.TrailList.Count ?? 0;
                int b = secondary?.TrailList.Count ?? 0;
                if (a + b <= cap) return;

                if (!RetireOldest(a >= b ? primary : secondary)) return;
            }
        }

        /// <summary>Detach the ribbon's oldest prism and start it withering. False when the oldest
        /// is not ours to recycle, so the ribbon stays contiguous and is retried next tick.</summary>
        bool RetireOldest(Trail trail)
        {
            var oldest = trail.TrailList[0];

            // Already gone (eaten, exploded) - just drop the slot. A consumed prism stays ACTIVE
            // with destroyed = true, so the test needs both; withering or pool-returning it would
            // fight whoever took it (Ark.RetireOldestWake makes the same call).
            if (!oldest || oldest.destroyed) { trail.RemoveOldest(); return true; }

            // An unpooled prism has nowhere to go: shrinking it would leave an invisible collider.
            if (oldest.OnReturnToPool == null) return false;

            trail.RemoveOldest();
            oldest.TargetScale = RetiredScale;
            _withering.Add((oldest, Time.time + WitherSeconds));
            return true;
        }

        void RetireDueWithering()
        {
            for (int i = _withering.Count - 1; i >= 0; i--)
            {
                var (prism, dueAt) = _withering[i];
                if (prism && Time.time < dueAt) continue;
                _withering.RemoveAt(i);
                if (prism) prism.ReturnToPool();
            }
        }

        void OnDestroy()
        {
            // Race teardown: hand everything still mid-wither straight back (as the tether does).
            for (int i = 0; i < _withering.Count; i++)
                if (_withering[i].prism) _withering[i].prism.ReturnToPool();
            _withering.Clear();
        }
    }
}

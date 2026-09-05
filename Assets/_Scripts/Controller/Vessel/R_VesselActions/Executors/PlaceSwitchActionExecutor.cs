using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Executes <see cref="PlaceSwitchActionSO"/>: gates on switch charges, spends one, and
    /// hands off to a <see cref="ScarabSwitch"/>, which owns the ring visual, the pass-through
    /// detection and the scarab-wing dais it pays out. Per-vessel state lives here (the SO is
    /// shared and stateless). Rides the normal R_VesselActionHandler ServerRpc→ClientRpc
    /// re-execution, so every peer builds the same switch from its replicated transform.
    ///
    /// <para><b>It also RECHARGES the meter (2026-09-05).</b> Nothing else does: the Scarab
    /// prefab authors "Switch Charges" with <c>resourceGainRate 0</c>, and the one refill wired
    /// in the game (<c>ScarabSwitchChargeByCrystalEffect</c>) sits on the four ELEMENTAL crystal
    /// branches, which a Scarab in either of its own arenas essentially never reaches — the
    /// arenas stock OMNI crystals and the skimmer turns those into balls before the hull can
    /// collect them. So the ability was one-shot per life. The recharge is a smooth per-frame
    /// trickle rather than the ResourceSystem's own 1 Hz <c>resourceGainRate</c> coroutine,
    /// which keeps the cadence authored beside the cost on the action SO and lets the count
    /// arrive on the frame it is earned.</para>
    ///
    /// <para><b>Why the recharge runs on EVERY peer, and why the charge gate stays per-peer.</b>
    /// A placement arrives at every machine through the action handler's ClientRpc, so
    /// <see cref="PlaceSwitch"/> — gate, spend and build — runs everywhere, and the peers only
    /// agree about whether a switch exists if their meters agree. A wall-clock recharge is
    /// symmetric by construction (every peer accumulates the same elapsed seconds) and the spend
    /// is symmetric because every peer performs it, so adding it makes the meters agree MORE than
    /// they did. The one remaining asymmetry is pre-existing and is not this class's to fix: an
    /// elemental crystal's effects resolve on the server and are replayed only onto the vessel's
    /// OWNER, so in a match with three or more machines a third peer can miss a crystal grant and
    /// briefly refuse a switch the placer built. Moving the gate ahead of the RPC (a
    /// can-this-action-run veto on <c>ShipActionSO</c>, consulted in
    /// <c>R_VesselActionHandler.OnButtonPressed</c>) is the real fix and is recorded as a
    /// follow-up in SCARAB.md §5.2; gating only the owner here would be worse, because the owner
    /// sends the RPC before it executes and so its refusal cannot recall the switch other peers
    /// have already built.</para>
    /// </summary>
    public class PlaceSwitchActionExecutor : ShipActionExecutorBase
    {
        [Tooltip("The standard pooled prism spawn channel " +
                 "(Assets/_SO_Assets/Event Channels/Prisms/EventOnSpawnPrismAndReturn.asset).")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnEvent;

        // The switch's RING is drawn in this domain's live prism material - the same asset the
        // dais prisms it pays out are laid in - so the two cannot drift. Injected rather than
        // serialized because the vessel is DI-injected on spawn
        // (ServerPlayerVesselInitializer.SpawnVesselForPlayer -> GameObjectInjector.InjectRecursive),
        // which is the same door ScarabCavitationBlast on this hull already comes through.
        // Null-safe by design: ToyFactory falls back to a minted prism material.
        [Inject] GameDataSO _gameData;

        // The float-ulp epsilon from SCARAB.md §3.3: a full 1.0 meter minus two exact 1/3
        // spends lands one ulp BELOW 1/3f, so the gate must sit a hair under the cost.
        const float CostEpsilon = 0.001f;

        IVesselStatus _status;

        // Resolved lazily so the recharge runs from the vessel's first frame rather than from its
        // first placement. R_VesselActionHandler.Initialize runs the executors BEFORE it fills
        // its binding maps, so this gives up only on SUCCESS - latching on the first attempt
        // would pin it null for the life of the vessel (the standing executor->SO trap).
        PlaceSwitchActionSO _so;
        static readonly List<ShipActionSO> s_boundScratch = new();

        // This pilot's unspent switches, oldest first. A threaded switch destroys itself, so
        // entries go null on their own and are pruned rather than unregistered.
        readonly List<ScarabSwitch> _live = new();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
        }

        // ONE symmetric pair, and deliberately NOT gated on the pilot: the refund is a resource
        // bookkeeping event that must happen wherever the spend happened, which is every peer.
        void OnEnable() => ScarabSwitch.OnThreaded += HandleSwitchThreaded;
        void OnDisable() => ScarabSwitch.OnThreaded -= HandleSwitchThreaded;

        /// <summary>
        /// A ball threaded a switch. If it was one of OURS, pay the placer back
        /// (<see cref="PlaceSwitchActionSO.ChargeRefundOnThread"/>) — SCARAB.md §5's second job of
        /// a switch, which until now existed only in the design record.
        ///
        /// Identity comes from the LEDGER, never from a name comparison: this executor holds the
        /// switches this vessel placed, so "mine" is a reference test that cannot be confused by
        /// two pilots sharing a display name, and it costs nothing.
        /// </summary>
        void HandleSwitchThreaded(ScarabSwitch sw, AstroLeagueBall ball)
        {
            if (sw == null) return;

            int index = _live.IndexOf(sw);
            if (index < 0) return;      // somebody else's switch
            _live.RemoveAt(index);      // it is spending itself; do not wait for the prune

            var so = ResolveSo();
            if (!so || so.ChargeRefundOnThread <= 0f) return;

            var resources = _status?.ResourceSystem;
            if (!resources) return;

            float cost = so.ComputeCost(resources);
            if (cost <= 0f) return;

            resources.ChangeResourceAmount(so.ResourceIndex, cost * so.ChargeRefundOnThread);
            CSDebug.LogVerbose(CSLogChannel.ScarabSwitch,
                $"[PlaceSwitch] A switch was threaded — refunding " +
                $"{so.ChargeRefundOnThread:F2} charge(s) to {_status?.PlayerName}.");
        }

        /// <summary>
        /// Earn charges back. One <see cref="PlaceSwitchActionSO.RechargeSecondsPerCharge"/> per
        /// charge, so a full bank costs that times the charge count. Skipped entirely when the
        /// meter is already full, so a Scarab that is not spending switches raises no events.
        /// </summary>
        void Update()
        {
            if (_status == null) return;

            var so = ResolveSo();
            if (!so || so.RechargeSecondsPerCharge <= 0f) return;

            var resources = _status.ResourceSystem;
            if (!resources) return;
            if (so.ResourceIndex < 0 || so.ResourceIndex >= resources.Resources.Count) return;

            var meter = resources.Resources[so.ResourceIndex];
            if (meter == null || meter.MaxAmount <= 0f) return;
            if (meter.CurrentAmount >= meter.MaxAmount) return;

            float cost = so.ComputeCost(resources);
            if (cost <= 0f) return;

            resources.ChangeResourceAmount(so.ResourceIndex,
                                          cost * Time.deltaTime / so.RechargeSecondsPerCharge);
        }

        public void PlaceSwitch(PlaceSwitchActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            _status = status;
            _so = so;

            var resources = status.ResourceSystem;
            if (!resources) return;

            if (!prismSpawnEvent)
            {
                CSDebug.LogError("[PlaceSwitch] Prism spawn event channel is not assigned on the executor.");
                return;
            }

            float cost = so.ComputeCost(resources);
            if (cost <= 0f) return;
            var meter = resources.Resources[so.ResourceIndex];
            if (meter.CurrentAmount < cost - CostEpsilon)
            {
                // Refusal: no charge, nothing spawns. (HUD pips already show the count;
                // a refusal SFX is a follow-up alongside the Scarab HUD pass.)
                CSDebug.LogVerbose(CSLogChannel.ScarabSwitch, "[PlaceSwitch] Refused — no switch charge banked.");
                return;
            }

            var ship = status.ShipTransform ? status.ShipTransform : transform;
            Vector3 course = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : ship.forward;

            float distance = so.placementDistance.EvaluateLive(status); // SPACE, live at use time
            float radius = so.RingRadius * so.switchScale.EvaluateLive(status); // MASS, live
            Vector3 center = ship.position + course * distance;

            // The switch owns its own ring visual and pass-through detection.
            var go = new GameObject($"ScarabSwitch::{status.PlayerName}");
            var sw = go.AddComponent<ScarabSwitch>();
            sw.Build(prismSpawnEvent, status, center, course, radius, so.GrowthRate,
                     so.Dais, so.DaisPrismsPerFrame,
                     _gameData ? _gameData.ThemeManagerData : null);

            RegisterAndEnforceCeiling(sw, so);

            resources.ChangeResourceAmount(so.ResourceIndex, -cost);
            CSDebug.LogVerbose(CSLogChannel.ScarabSwitch,
                $"[PlaceSwitch] Switch ring r={radius:F0} placed {distance:F0}u ahead " +
                $"({_live.Count}/{so.MaxLiveSwitches} standing, recharge {so.RechargeSecondsPerCharge:F0}s/charge).");
        }

        /// <summary>
        /// Track the new switch and, if this pilot is now over the ceiling, retire their oldest
        /// standing ring. That removal is caused by THIS placement — a player putting one switch
        /// too many into the world — never by a clock, which is the same argument the ball's cell
        /// overload makes. A retired ring shrinks away and pays no dais: nothing threaded it.
        /// </summary>
        void RegisterAndEnforceCeiling(ScarabSwitch placed, PlaceSwitchActionSO so)
        {
            // Threaded switches destroy themselves, so prune before counting.
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i]) _live.RemoveAt(i);

            _live.Add(placed);

            int ceiling = so.MaxLiveSwitches;
            while (_live.Count > ceiling)
            {
                var oldest = _live[0];
                _live.RemoveAt(0);
                if (!oldest) continue;
                CSDebug.LogVerbose(CSLogChannel.ScarabSwitch,
                    $"[PlaceSwitch] Ceiling {ceiling} reached — retiring the oldest standing switch.");
                oldest.Retire(so.RetireSeconds);
            }
        }

        /// <summary>
        /// Find this vessel's Place Switch action through the handler's binding maps. The switch
        /// IS bound to an input (unlike a passive ability), so the sweep can resolve it — but only
        /// once the maps are populated, which is after the executors are initialized.
        /// </summary>
        PlaceSwitchActionSO ResolveSo()
        {
            if (_so) return _so;

            var handler = _status?.ActionHandler;
            if (!handler) return null;

            s_boundScratch.Clear();
            foreach (InputEvents inputEvent in System.Enum.GetValues(typeof(InputEvents)))
                handler.CollectBoundActions(inputEvent, s_boundScratch);

            for (int i = 0; i < s_boundScratch.Count; i++)
            {
                if (s_boundScratch[i] is not PlaceSwitchActionSO place) continue;
                _so = place;
                break;
            }

            s_boundScratch.Clear();
            return _so;
        }
    }
}

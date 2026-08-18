using System;
using System.Collections.Generic;
using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin's crystal seeding — a <b>PASSIVE</b> ability with no input of its own. A cooldown
    /// runs continuously; each time it completes the Dolphin seeds a crystal somewhere in the
    /// containing cell's CYTOPLASM (the shell between nucleus and membrane) and the cooldown
    /// restarts immediately.
    ///
    /// This is the Dolphin's own ammunition supply: the crystal it seeds is the crystal it later
    /// flies into to release Echo Obliteration, so the seeding rate IS the blast's tempo.
    ///
    /// Element → parameter: <b>MASS</b> owns this ability. Its multiplier divides the recharge, and
    /// its level-5 upgrade changes WHAT is planted — see <see cref="CurrentCrystalPrefab"/>. Below
    /// the upgrade the seed is an ordinary OMNI crystal standing in open space that any pilot can
    /// take; at Mass 5 it becomes a TEAM crystal only this domain can collect. The HUD reads
    /// <see cref="CooldownRemaining01"/> for the recharge fill and edge-detects
    /// <see cref="SeedCount"/> for the planted beat.
    ///
    /// <para><b>Locally simulated only.</b> <c>TeamCrystal.prefab</c> carries no NetworkObject, so a
    /// seeded crystal has always been a local instantiate — the previous hold-to-plant version ran
    /// on the owner's machine behind the action handler's <c>IsSpawned &amp;&amp; IsOwner</c> gate and
    /// produced an owner-only crystal too. The clock therefore runs for the LOCAL PILOT's Dolphin
    /// and no other, which preserves exactly that scope; letting every peer run it would have each
    /// peer roll its own placement and desync the field outright. Networked seeding is a follow-up
    /// and wants crystal network sync first (see DOLPHIN_CRYSTAL_SEEDING.md ▸ Follow-ups).</para>
    /// </summary>
    public sealed class DeployTeamCrystalActionExecutor : ShipActionExecutorBase
    {
        [Header("Setup")]
        [Tooltip("The crystal planted by each seeding BELOW the Mass level-5 upgrade - an ordinary " +
                 "OMNI crystal (Crystal.prefab) that any pilot who reaches it can collect.")]
        [SerializeField] private Crystal crystalPrefab;

        [Tooltip("The crystal planted once MASS level 5 is active - a TEAM crystal " +
                 "(TeamCrystal.prefab) only this pilot's own domain can collect. Leave empty to " +
                 "keep seeding omni crystals at every level.")]
        [SerializeField] private Crystal upgradedCrystalPrefab;

        [Tooltip("Tuning for the seeding. Wired directly because the ability is PASSIVE - it is " +
                 "bound to no input event, so the action handler's binding maps can never resolve " +
                 "it. Leave empty only if this vessel should not seed.")]
        [SerializeField] private DeployTeamCrystalActionSO config;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        IVesselStatus _status;

        // Live crystals this Dolphin has seeded. Compacted lazily; entries go null when a crystal
        // is collected or destroyed, which is exactly how the cap frees up.
        readonly List<Crystal> _live = new();

        float _nextSeedTime;
        float _activeCooldown;
        bool _clockStarted;

        /// <summary>Monotonic count of seedings performed, for HUD edge detection.</summary>
        public int SeedCount { get; private set; }

        // Kept as a fallback resolution path only - see ResolveSo.
        DeployTeamCrystalActionSO _so;
        static readonly List<ShipActionSO> s_boundScratch = new();

        // No null guard on the SOAP channel: a missing reference must fail loud.
        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;

            // Fresh vessel, fresh clock. The SO may differ after a class swap, so re-resolve it.
            _so = null;
            _clockStarted = false;
            _activeCooldown = 0f;
            SeedCount = 0;
            _live.Clear();
        }

        // ---------------- HUD surface ----------------

        /// <summary>
        /// True once the seed is TEAM-locked — the Mass level-5 upgrade. HUD-readable, so the slot
        /// can say which kind of crystal the next cycle will leave behind.
        /// </summary>
        public bool SeedsTeamCrystal => IsMassUpgraded && upgradedCrystalPrefab;

        /// <summary>
        /// What the next cycle plants: the team crystal once Mass 5 is active, the omni crystal
        /// otherwise. Resolved per seeding rather than latched at init, so losing the upgrade to a
        /// Mass debuff immediately puts the pilot back to planting crystals anyone can take.
        /// </summary>
        Crystal CurrentCrystalPrefab
            => IsMassUpgraded && upgradedCrystalPrefab ? upgradedCrystalPrefab : crystalPrefab;

        /// <summary>
        /// Recharge remaining as a 0-1 fraction: 1 the instant a cycle fires, 0 when the next
        /// seeding is due. Reads 0 while the clock is paused at the live-crystal cap.
        /// </summary>
        public float CooldownRemaining01
        {
            get
            {
                if (_activeCooldown <= 0f) return 0f;
                return Mathf.Clamp01((_nextSeedTime - Time.time) / _activeCooldown);
            }
        }

        /// <summary>Live crystals this Dolphin currently has planted.</summary>
        public int LiveSeededCount
        {
            get { CompactLive(); return _live.Count; }
        }

        // ---------------- The clock ----------------

        void Update()
        {
            var so = ResolveSo();
            if (!so || !crystalPrefab) return;

            // Local pilot only - see the class doc. Player can be null for a frame during a swap.
            var player = _status?.Player;
            if (player == null || !player.IsLocalPilot) return;

            CompactLive();

            // At the cap the clock PAUSES rather than culling anything. Not creating mass is
            // allowed; aging it out is not.
            if (so.MaxLiveSeeded > 0 && _live.Count >= so.MaxLiveSeeded)
            {
                _activeCooldown = 0f;
                _clockStarted = false;
                return;
            }

            if (!_clockStarted)
            {
                StartClock(so);
                return;
            }

            if (Time.time < _nextSeedTime) return;

            // One crystal per cycle at every level. The Mass upgrade changes WHAT is planted, never
            // how many - the yield is the recharge's job and the recharge alone.
            SeedOne(so);

            SeedCount++;
            StartClock(so);
        }

        void StartClock(DeployTeamCrystalActionSO so)
        {
            _activeCooldown = CurrentCooldown();
            _nextSeedTime = Time.time + _activeCooldown;
            _clockStarted = true;
        }

        // ---------------- Seeding ----------------

        void SeedOne(DeployTeamCrystalActionSO so)
        {
            Vector3 point = ResolveSeedPoint(so);

            // Resolved per seeding, not per cycle: the Mass upgrade is what decides whether this
            // crystal is team-locked, and it can flip between one seeding and the next.
            var prefab = CurrentCrystalPrefab;
            if (!prefab) return;

            var crystal = Instantiate(prefab, point, UnityEngine.Random.rotation);

            // Domain is stamped BEFORE activation so the crystal settles straight into the right
            // material (Crystal.ResolveActivationMaterial) instead of lerping through the wrong look
            // on its way there. It is also the COLLECTION gate itself
            // (Crystal.CanBeCollected: Blue is free-for-all), so the two halves of the Mass upgrade
            // - the impactor the prefab carries and the domain stamped here - always agree:
            //
            //   below Mass 5 -> Domains.Blue, the lime free-for-all CTA. Anyone can take it, and it
            //                   LOOKS like anyone can take it (Docs/PALETTE.md 2.2: crystal colour
            //                   signals who may collect).
            //   at Mass 5    -> this pilot's domain, in that domain's crystal colours.
            crystal.ownDomain = SeedsTeamCrystal ? _status.Domain : Domains.Blue;
            crystal.ActivateCrystal();

            _live.Add(crystal);
        }

        /// <summary>
        /// A random point in the containing cell's CYTOPLASM — the shell between the nucleus
        /// surface and the membrane.
        ///
        /// The radius is drawn <b>volume-uniformly</b> across the band (cube-root of a uniform
        /// draw between the cubed radii), not uniformly in radius. A shell's available space grows
        /// as r², so a uniform-in-radius draw crowds seedings against the nucleus and leaves the
        /// outer cytoplasm — most of the actual volume — nearly empty. Same rule the flora planting
        /// band follows (CLAUDE.md ▸ Rampage §27.2).
        ///
        /// The inner edge is clamped outside the nucleus regardless of what the band fractions ask
        /// for: nucleus mass is the cell's territorial claim and a fauna sanctuary, so ability
        /// crystals must not seed into it.
        /// </summary>
        Vector3 ResolveSeedPoint(DeployTeamCrystalActionSO so)
        {
            Vector3 origin = _status.Vessel != null && _status.Vessel.Transform
                ? _status.Vessel.Transform.position
                : transform.position;

            var cell = Cell.FindCellContaining(origin) ?? Cell.FindNearestActiveCell(origin);

            // No cell to measure against (freestyle transit, tool scenes): seed in a ball around
            // the vessel so the ability still does something rather than silently stopping.
            if (cell == null)
                return origin + UnityEngine.Random.insideUnitSphere * so.CellessSeedRadius;

            Vector3 centre = cell.transform.position;
            float nucleus = Mathf.Max(0f, cell.ExpectedNucleusWorldRadius);
            float membrane = cell.MembraneRadius;

            // A cell with no membrane measurement gives us no band to draw from.
            if (membrane <= nucleus)
                return origin + UnityEngine.Random.insideUnitSphere * so.CellessSeedRadius;

            float span = membrane - nucleus;
            float inner = nucleus + span * so.BandInnerFraction;
            float outer = nucleus + span * so.BandOuterFraction;

            // Never inside the nucleus, whatever the fractions say.
            inner = Mathf.Max(inner, nucleus);
            if (outer <= inner) outer = Mathf.Min(membrane, inner + Mathf.Max(1f, span * 0.05f));

            float u = UnityEngine.Random.value;
            float i3 = inner * inner * inner;
            float o3 = outer * outer * outer;
            float radius = Mathf.Pow(Mathf.Lerp(i3, o3, u), 1f / 3f);

            return centre + UnityEngine.Random.onUnitSphere * radius;
        }

        /// <summary>Drops collected/destroyed crystals so the cap counts only what is really there.</summary>
        void CompactLive()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i]) _live.RemoveAt(i);
        }

        /// <summary>
        /// End of turn: stop the clock. The crystals themselves are left to the scene teardown that
        /// owns them — this executor never destroys planted mass.
        ///
        /// The live roster is deliberately NOT cleared. It is cap accounting, not turn state, and
        /// the crystals it counts are still standing: a mode that runs several turns without a
        /// scene reload would otherwise hand the next turn a fresh budget on top of last turn's
        /// crystals, and the cap would ratchet open a turn at a time. <see cref="CompactLive"/>
        /// already drops entries as the crystals are collected, which is the only correct way for
        /// the budget to free up.
        /// </summary>
        void OnTurnEndOfMiniGame()
        {
            _clockStarted = false;
            _activeCooldown = 0f;
        }

        // ---------------- Elemental ----------------

        bool IsMassUpgraded
        {
            get
            {
                var handler = _status?.ElementalAbilityHandler;
                return handler && handler.IsUpgradeActive(Element.Mass);
            }
        }

        /// <summary>
        /// Seconds between seedings right now. Element → parameter (Mass → how soon the next
        /// crystal arrives): anchored at exactly 1x at the resting level, so the authored cooldown
        /// is what a fresh pilot feels, and floored by MinCooldown so it never becomes free.
        ///
        /// Scaled from the SO's OWN authored multiplier rather than the map's generic one, so the
        /// recharge is driven by exactly one number and a future consumer of the map's generic Mass
        /// multiplier cannot silently start moving it too.
        /// </summary>
        float CurrentCooldown()
        {
            var so = ResolveSo();
            if (!so) return 0f;

            float mult = ElementalScaling.Multiplier(_status, Element.Mass,
                so.CooldownMultiplierAtFullMass, so.MinCooldownMultiplier);
            return Mathf.Max(so.MinCooldown, so.Cooldown * mult);
        }

        /// <summary>
        /// The serialized <see cref="config"/> is the real source now: a PASSIVE ability is bound to
        /// no input event, so <c>CollectBoundActions</c> — which walks the input→action maps — can
        /// never find it. The binding sweep is kept only as a fallback for a vessel that still lists
        /// the action against an input.
        ///
        /// It gives up only on SUCCESS, never on the first attempt. R_VesselActionHandler.Initialize
        /// calls InitializeAll on the executors BEFORE it populates the binding maps, so an early
        /// query lands in a window where the maps are still empty — latching there would pin _so
        /// null for the life of the vessel.
        /// </summary>
        DeployTeamCrystalActionSO ResolveSo()
        {
            if (config) return config;
            if (_so) return _so;

            var handler = _status?.ActionHandler;
            if (!handler) return null;

            s_boundScratch.Clear();
            foreach (InputEvents inputEvent in Enum.GetValues(typeof(InputEvents)))
                handler.CollectBoundActions(inputEvent, s_boundScratch);

            foreach (var action in s_boundScratch)
            {
                if (action is not DeployTeamCrystalActionSO deploy) continue;
                _so = deploy;
                break;
            }

            s_boundScratch.Clear();
            return _so;
        }
    }
}

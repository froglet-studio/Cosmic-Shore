using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using Obvious.Soap;
using Reflex.Attributes;
using Reflex.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Termite queen's deck: the pheromone clock, which card of each slot is face up, and
    /// the six cards themselves. Design record: <c>R_VesselActions/TERMITE.md</c> §5.
    ///
    /// <para><b>One decision, taken once, before the press leaves the owner.</b> Every press is
    /// replayed on every peer, so a card that refused itself inside <c>StartAction</c> would be
    /// played on the machines whose pheromone happened to be a frame ahead and refused on the
    /// rest. This executor registers <see cref="R_VesselActionHandler.OwnerPressGate"/> instead:
    /// on the machine that ORIGINATES a press it checks the pheromone (and the live caps), and
    /// either refuses — the press is never sent — or admits it and RESERVES the cost there and
    /// then, so two presses inside one round trip cannot both be admitted against pheromone that
    /// pays for one. When the play comes back through <see cref="Play"/> the reservation is
    /// consumed; a replica that received a play it never reserved simply pays for it (clamped at
    /// zero). Either way every machine plays the same cards in the same order, so every machine
    /// flips the same slots.</para>
    ///
    /// <list type="table">
    /// <item><term>CHARGE</term><description><b>Autothysis</b> — every soldier drone near the queen
    /// ruptures in a blast of her domain (with none, she bursts herself, smaller). ↔ <b>Team
    /// Crystal</b> — plants a crystal only her domain may collect.</description></item>
    /// <item><term>MASS</term><description><b>Queen Drones</b> — soldiers that escort her and
    /// destroy opposing mass. ↔ <b>Mound Drones</b> — workers that graze opposing mass and carry
    /// it home to build her nearest mound.</description></item>
    /// <item><term>SPACE</term><description><b>Teleport</b> — she jumps to the point she was sent
    /// to (snapping onto her own mound if one stands there). ↔ <b>New Mound</b> — founds a mound
    /// behind her.</description></item>
    /// <item><term>TIME</term><description>The pheromone clock that pays for all of it
    /// (<see cref="TermitePheromoneActionSO"/>).</description></item>
    /// </list>
    ///
    /// <para><b>What runs where.</b> Drones, mounds and blasts run on every peer (the press is
    /// replayed everywhere; the fauna's accepted divergence applies to what each machine's drones
    /// choose to eat). The two effects that cannot be simulated twice run on the OWNING machine
    /// only: Teleport (a pose, which <c>SetPose</c> replicates from there) and Team Crystal
    /// (<c>TeamCrystal.prefab</c> carries no NetworkObject — the Dolphin's crystal seeding
    /// precedent, and the same scope).</para>
    /// </summary>
    public sealed class TermiteDeckExecutor : ShipActionExecutorBase
    {
        [Header("Deck")]
        [Tooltip("The Time ability: the pheromone tank and its refill. Wired directly — a passive " +
                 "ability is bound to no input, so the binding sweep can never find it.")]
        [SerializeField] TermitePheromoneActionSO pheromoneConfig;

        [Header("Scene Refs")]
        [Tooltip("Pooled-prism spawn channel (EventOnSpawnPrismAndReturn), the same asset the trail " +
                 "uses. Mounds lay through BoostRingBuilder: full-size colliders from frame 0.")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;

        [Tooltip("TeamCrystal.prefab — a crystal only the stamped domain may collect.")]
        [SerializeField] Crystal teamCrystalPrefab;

        [Tooltip("The blast each rupturing drone makes (a spherical AOEExplosion carrying the " +
                 "Termite's autothysis impactor container).")]
        [SerializeField] AOEExplosion[] autothysisPrefabs;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        [Header("Drones")]
        [SerializeField] TermiteDroneTuning droneTuning = new();
        [Tooltip("Most drones one queen may have alive at once. A drone card is refused at the cap " +
                 "rather than recycling an older drone (nothing is removed on a count).")]
        [SerializeField, Min(1)] int maxLiveDrones = 36;
        [Tooltip("Radius around the queen (or a mound) new drones are released in.")]
        [SerializeField, Min(0f)] float releaseRadius = 9f;

        [Header("Autothysis")]
        [Tooltip("Soldier drones within this distance of the queen rupture when the card is played.")]
        [SerializeField, Min(1f)] float autothysisReach = 260f;
        [Tooltip("Most blasts one play detonates — each is a full AOE explosion.")]
        [SerializeField, Min(1)] int maxBlastsPerPlay = 12;
        [Tooltip("With no soldier near her, the queen ruptures herself: this fraction of one " +
                 "drone's blast, centred on her.")]
        [SerializeField, Range(0.1f, 2f)] float queenBurstFraction = 0.7f;

        [Header("Team Crystal")]
        [Tooltip("Most team crystals one queen may have planted at once (the card is refused at the " +
                 "cap; a crystal is only ever removed by being collected).")]
        [SerializeField, Min(1)] int maxLiveCrystals = 3;

        [Header("Mounds")]
        [SerializeField, Min(1)] int maxMounds = 3;
        [SerializeField, Min(2f)] float moundBaseRadius = 14f;
        [SerializeField, Min(4f)] float moundHeight = 34f;
        [Tooltip("World units between neighbouring mound prisms — ring populations are derived from it.")]
        [SerializeField, Min(0.5f)] float moundSpacing = 3.4f;
        [Tooltip("World scale of one mound prism; +z runs up the spire.")]
        [SerializeField] Vector3 moundPrismScale = new(2.4f, 2.4f, 3.4f);
        [Tooltip("How far BEHIND the queen a new mound is founded, so it never forms inside her.")]
        [SerializeField, Min(0f)] float moundFoundDistance = 28f;

        [Header("Teleport")]
        [Tooltip("A commanded point within this distance of one of her mounds snaps onto the mound.")]
        [SerializeField, Min(0f)] float moundSnapRadius = 120f;

        [Header("AI")]
        [Tooltip("Seconds between an AI queen's card plays (a random value in the range).")]
        [SerializeField] Vector2 aiPlayInterval = new(5f, 9f);

        [Header("Audio")]
        [Tooltip("One field per card. Leave empty for silence — never point one at a borrowed event.")]
        [SerializeField] EventReference autothysisEvent;
        [SerializeField] EventReference teamCrystalEvent;
        [SerializeField] EventReference queenDronesEvent;
        [SerializeField] EventReference moundDronesEvent;
        [SerializeField] EventReference teleportEvent;
        [SerializeField] EventReference newMoundEvent;
        [Tooltip("Played on the owner when a card is refused (not enough pheromone, a cap reached).")]
        [SerializeField] EventReference deniedEvent;

        [Inject] Container _container;

        IVesselStatus _status;
        R_VesselActionHandler _handler;
        Func<InputEvents, bool> _gate;
        TermiteCommander _commander;
        TermiteCommandTransformer _commandTransformer;
        TermiteAnimation _animation;

        float _pheromone;
        readonly Dictionary<Element, bool> _faceB = new();
        readonly Dictionary<TermiteCardActionSO, int> _reserved = new();

        readonly List<TermiteDrone> _drones = new();
        readonly List<TermiteMound> _mounds = new();
        readonly List<Crystal> _crystals = new();
        readonly Dictionary<Prism, TermiteDrone> _claims = new();

        readonly Dictionary<Element, TermiteCardActionSO> _slots = new();
        readonly Dictionary<Element, InputEvents> _slotInputs = new();
        bool _slotsResolved;
        float _nextAiPlay;

        static InputEvents[] s_inputs;
        static readonly Element[] s_slotElements = { Element.Charge, Element.Mass, Element.Space };

        // ---------------- HUD surface ----------------

        public float Pheromone => _pheromone;
        public float MaxPheromone => pheromoneConfig ? pheromoneConfig.MaxPheromone : 10f;
        public int LiveDrones { get { Compact(); return _drones.Count; } }
        public int MaxLiveDrones => maxLiveDrones;
        public int LiveMounds { get { Compact(); return _mounds.Count; } }
        public int MaxMounds => maxMounds;

        /// <summary>Monotonic: advances on every card played on this machine (HUD edge detection).</summary>
        public int PlayCount { get; private set; }
        public TermiteCard LastPlayed { get; private set; }

        /// <summary>Monotonic: advances on every refused press on this machine.</summary>
        public int DenyCount { get; private set; }
        public TermiteCard LastDenied { get; private set; }

        /// <summary>The card slot bound to <paramref name="element"/>'s ability, if any.</summary>
        public bool TryGetSlot(Element element, out TermiteCardActionSO slot)
        {
            ResolveSlots();
            return _slots.TryGetValue(element, out slot) && slot;
        }

        /// <summary>The card face up in <paramref name="element"/>'s slot — the one a press plays.</summary>
        public bool TryGetFaceUp(Element element, out TermiteCardSpec card)
        {
            card = default;
            if (!TryGetSlot(element, out var slot)) return false;
            card = slot.Face(FaceB(element));
            return true;
        }

        /// <summary>The card that comes up after the face-up one is played.</summary>
        public bool TryGetNext(Element element, out TermiteCardSpec card)
        {
            card = default;
            if (!TryGetSlot(element, out var slot)) return false;
            card = slot.Face(!FaceB(element));
            return true;
        }

        /// <summary>Could the face-up card of this slot be played right now?</summary>
        public bool CanPlayFaceUp(Element element)
            => TryGetSlot(element, out var slot) && Admissible(slot.Face(FaceB(element)), slot, out _);

        // ---------------- lifecycle ----------------

        void OnEnable() => OnMiniGameTurnEnd.OnRaised += OnTurnEnd;

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            UnregisterGate();
        }

        void OnDestroy()
        {
            UnregisterGate();
            RetireAllDrones();
        }

        void OnTurnEnd()
        {
            RetireAllDrones();
            for (int i = 0; i < _mounds.Count; i++)
                if (_mounds[i]) _mounds[i].ReturnToPool();
            _mounds.Clear();
            _claims.Clear();
        }

        /// <summary>
        /// Re-runs on a LIVE component (a vessel swap, a pilot swap), so the deck is dealt fresh:
        /// full start pheromone, every slot back to its A card, no reservations. Drones retire —
        /// they were the previous pilot's hands. Mounds STAY: they are conserved mass a pilot
        /// actively placed, and nothing removes mass because the pilot changed.
        /// </summary>
        public override void Initialize(IVesselStatus shipStatus)
        {
            UnregisterGate();
            _status = shipStatus;
            _pheromone = pheromoneConfig ? pheromoneConfig.StartPheromone : 5f;
            _faceB.Clear();
            _reserved.Clear();
            _slots.Clear();
            _slotInputs.Clear();
            _slotsResolved = false;
            RetireAllDrones();

            _queenRoot = shipStatus is Component c ? c.transform : transform.root;
            var root = _queenRoot;
            _commander = root.GetComponentInChildren<TermiteCommander>(true);
            _commandTransformer = root.GetComponentInChildren<TermiteCommandTransformer>(true);
            _animation = root.GetComponentInChildren<TermiteAnimation>(true);

            _handler = shipStatus?.ActionHandler;
            if (_handler)
            {
                _gate = Gate;
                _handler.OwnerPressGate = _gate;
            }
            _nextAiPlay = Time.time + UnityEngine.Random.Range(aiPlayInterval.x, aiPlayInterval.y);
        }

        void UnregisterGate()
        {
            if (_handler && _gate != null && _handler.OwnerPressGate == _gate)
                _handler.OwnerPressGate = null;
            _gate = null;
        }

        void Update()
        {
            if (_status == null || !pheromoneConfig) return;
            float max = pheromoneConfig.MaxPheromone;
            if (_pheromone < max)
                _pheromone = Mathf.Min(max, _pheromone + pheromoneConfig.RegenPerSecond(_status) * Time.deltaTime);

            TickAi();
        }

        // ---------------- the gate (owner side) ----------------

        bool Gate(InputEvents ie)
        {
            if (!_handler || !_handler.TryGetActionOn<TermiteCardActionSO>(ie, out var slot) || !slot)
                return true;   // not a card: nothing to judge

            // The card this press would play: the face up now, turned over once for every play
            // already admitted on this slot that has not come back yet.
            _reserved.TryGetValue(slot, out int pending);
            bool faceB = FaceB(slot.SlotElement) ^ (pending % 2 == 1);
            var card = slot.Face(faceB);

            if (!Admissible(card, slot, out _))
            {
                Deny(card);
                return false;
            }

            _pheromone -= card.Cost;
            _reserved[slot] = pending + 1;
            return true;
        }

        /// <summary>Everything a play must satisfy, judged on the owner. <paramref name="reason"/>
        /// names the failure for the HUD and the log.</summary>
        bool Admissible(TermiteCardSpec card, TermiteCardActionSO slot, out string reason)
        {
            if (_pheromone + 1e-4f < card.Cost) { reason = "pheromone"; return false; }
            switch (card.Card)
            {
                case TermiteCard.QueenDrones:
                    if (LiveDrones >= maxLiveDrones) { reason = "drone cap"; return false; }
                    break;
                case TermiteCard.MoundDrones:
                    if (LiveDrones >= maxLiveDrones) { reason = "drone cap"; return false; }
                    if (LiveMounds == 0) { reason = "no mound"; return false; }
                    break;
                case TermiteCard.NewMound:
                    if (LiveMounds >= maxMounds) { reason = "mound cap"; return false; }
                    if (!prismSpawnChannel) { reason = "unwired"; return false; }
                    break;
                case TermiteCard.TeamCrystal:
                    CompactCrystals();
                    if (_crystals.Count >= maxLiveCrystals) { reason = "crystal cap"; return false; }
                    if (!teamCrystalPrefab) { reason = "unwired"; return false; }
                    break;
                case TermiteCard.Autothysis:
                    if (autothysisPrefabs == null || autothysisPrefabs.Length == 0) { reason = "unwired"; return false; }
                    break;
            }
            reason = null;
            return true;
        }

        void Deny(TermiteCardSpec card)
        {
            DenyCount++;
            LastDenied = card.Card;
            if (_status != null && _status.IsInitializedAsAI) return;   // an AI is not told off
            PlaySfx(deniedEvent);
        }

        // ---------------- the play (every peer) ----------------

        /// <summary>
        /// A card slot was pressed and admitted. Runs on EVERY peer, in press order: pays (or
        /// consumes the owner's reservation), plays the face-up card, turns it over.
        /// </summary>
        public void Play(TermiteCardActionSO slot, IVesselStatus status)
        {
            if (!slot || status == null) return;
            if (_status == null) _status = status;

            var element = slot.SlotElement;
            var card = slot.Face(FaceB(element));

            if (_reserved.TryGetValue(slot, out int pending) && pending > 0)
                _reserved[slot] = pending - 1;                  // already paid at the gate
            else
                _pheromone = Mathf.Max(0f, _pheromone - card.Cost);

            _faceB[element] = !FaceB(element);

            float power = slot.Power(status);
            switch (card.Card)
            {
                case TermiteCard.Autothysis:  PlayAutothysis(card, power, status); break;
                case TermiteCard.TeamCrystal: PlayTeamCrystal(card, power, status); break;
                case TermiteCard.QueenDrones: ReleaseDrones(card, power, status, TermiteDrone.Role.Soldier); break;
                case TermiteCard.MoundDrones: ReleaseDrones(card, power, status, TermiteDrone.Role.Worker); break;
                case TermiteCard.Teleport:    PlayTeleport(card, power, status); break;
                case TermiteCard.NewMound:    PlayNewMound(card, power, status); break;
            }

            PlayCount++;
            LastPlayed = card.Card;
            if (_animation) _animation.PlayCardPulse();
        }

        bool FaceB(Element element) => _faceB.TryGetValue(element, out bool b) && b;

        /// <summary>The queen's root transform, cached at Initialize. Deliberately NOT read through
        /// <c>IVesselStatus.ShipTransform</c>, which dereferences <c>Vessel</c> unguarded and
        /// throws in the window a swap leaves it unset.</summary>
        Transform Queen => _queenRoot ? _queenRoot : transform;
        Transform _queenRoot;

        /// <summary>True on the machine that simulates this queen (the owner; or the only machine,
        /// off-network). Owner-only effects gate on it.</summary>
        bool IsSimulatingOwner(IVesselStatus status)
        {
            var player = status?.Player;
            return player != null && !player.IsNetworkClient;
        }

        // ---------------- CHARGE ----------------

        void PlayAutothysis(TermiteCardSpec card, float power, IVesselStatus status)
        {
            float diameter = Mathf.Max(1f, card.Amount * power);
            Vector3 queen = Queen.position;
            float reach2 = autothysisReach * autothysisReach;
            int blasts = 0;

            Compact();
            for (int i = 0; i < _drones.Count && blasts < maxBlastsPerPlay; i++)
            {
                var drone = _drones[i];
                if (!drone || drone.IsRetiring || drone.DroneRole != TermiteDrone.Role.Soldier) continue;
                if ((drone.transform.position - queen).sqrMagnitude > reach2) continue;
                Detonate(drone.transform.position, diameter, status);
                drone.Retire();
                blasts++;
            }

            // Nobody to spend: the queen ruptures herself, smaller — the card never does nothing.
            if (blasts == 0) Detonate(queen, diameter * queenBurstFraction, status);

            PlaySfx(autothysisEvent, queen);
        }

        void Detonate(Vector3 at, float diameter, IVesselStatus status)
        {
            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = status.Domain,
                Vessel = status.Vessel,
                AnnonymousExplosion = false,
                OverrideMaterial = status.AOEExplosionMaterial,
                MaxScale = diameter,
                SpawnPosition = at,
                SpawnRotation = Quaternion.identity,
            };
            ExplosionHelper.CreateExplosion(autothysisPrefabs, init, _container);
        }

        void PlayTeamCrystal(TermiteCardSpec card, float power, IVesselStatus status)
        {
            var queen = Queen;
            Vector3 at = queen.position + queen.forward * Mathf.Max(0f, card.Amount * power);
            PlaySfx(teamCrystalEvent, at);
            if (!IsSimulatingOwner(status) || !teamCrystalPrefab) return;

            var crystal = Instantiate(teamCrystalPrefab, at, UnityEngine.Random.rotation);
            // Stamped BEFORE activation: the domain is both the collection gate and what the
            // crystal's material is resolved from, so it never shows the wrong collectability.
            crystal.ownDomain = status.Domain;
            crystal.ActivateCrystal();
            _crystals.Add(crystal);
        }

        // ---------------- MASS ----------------

        void ReleaseDrones(TermiteCardSpec card, float power, IVesselStatus status, TermiteDrone.Role role)
        {
            Compact();
            int count = Mathf.Max(1, Mathf.RoundToInt(card.Amount * power));
            count = Mathf.Min(count, maxLiveDrones - _drones.Count);
            if (count <= 0) return;

            TermiteMound mound = role == TermiteDrone.Role.Worker ? NearestMound(Queen.position) : null;
            if (role == TermiteDrone.Role.Worker && !mound) role = TermiteDrone.Role.Soldier;   // a replica without the mound

            var queen = Queen;
            Vector3 origin = mound ? mound.Entrance : queen.position;
            var mesh = TermiteDrone.SharedMesh();
            var material = status.ShipMaterial;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(role == TermiteDrone.Role.Soldier ? "TermiteSoldier" : "TermiteWorker");
                go.transform.position = origin + UnityEngine.Random.insideUnitSphere * releaseRadius;
                go.transform.rotation = queen.rotation;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                if (material) renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var drone = go.AddComponent<TermiteDrone>();
                drone.Initialize(this, droneTuning, role, status.Domain, status.PlayerName, queen, mound,
                                 phase: i * 0.7f + UnityEngine.Random.value * 0.35f);
                _drones.Add(drone);
            }

            PlaySfx(role == TermiteDrone.Role.Soldier ? queenDronesEvent : moundDronesEvent, origin);
        }

        // ---------------- SPACE ----------------

        void PlayTeleport(TermiteCardSpec card, float power, IVesselStatus status)
        {
            var queen = Queen;
            Vector3 from = queen.position;
            PlaySfx(teleportEvent, from);
            if (!IsSimulatingOwner(status) || status.Vessel == null) return;

            float reach = Mathf.Max(1f, card.Amount * power);
            Vector3 target;
            var commanded = _commander ? _commander.LastCommandPoint : null;
            if (commanded.HasValue)
            {
                target = commanded.Value;
                var mound = NearestMound(target);
                if (mound && (mound.Entrance - target).sqrMagnitude <= moundSnapRadius * moundSnapRadius)
                    target = mound.Entrance + mound.transform.up * 12f;
            }
            else target = from + queen.forward * reach;

            // A node of her choosing, within her reach.
            Vector3 jump = target - from;
            if (jump.sqrMagnitude > reach * reach) target = from + jump.normalized * reach;

            status.Vessel.SetPose(new Pose(target, queen.rotation));
            if (_commandTransformer) _commandTransformer.ClearCommandTarget();
        }

        void PlayNewMound(TermiteCardSpec card, float power, IVesselStatus status)
        {
            Compact();
            if (_mounds.Count >= maxMounds || !prismSpawnChannel) return;   // a replica ahead of the owner

            var queen = Queen;
            Vector3 at = queen.position - queen.forward * moundFoundDistance;
            var go = new GameObject("TermiteMound");
            // The spire rises along the queen's up, facing the way she was going.
            go.transform.SetPositionAndRotation(at, queen.rotation);
            var mound = go.AddComponent<TermiteMound>();
            int seed = Mathf.Max(1, Mathf.RoundToInt(card.Amount * power));
            mound.Found(this, prismSpawnChannel, status.Domain, status.PlayerName,
                        moundBaseRadius, moundHeight, moundSpacing, moundPrismScale, seed);
            _mounds.Add(mound);

            PlaySfx(newMoundEvent, at);
        }

        TermiteMound NearestMound(Vector3 near)
        {
            TermiteMound best = null;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < _mounds.Count; i++)
            {
                var m = _mounds[i];
                if (!m || m.IsFallen) continue;
                float d = (m.transform.position - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }

        // ---------------- AI ----------------

        /// <summary>
        /// An AI queen plays a card every few seconds, through the SAME replicated press a human's
        /// input takes — and therefore through the same gate — so an AI can never play a card the
        /// deck would refuse, and its plays are seen on every peer. An AI runs server-only, so a
        /// local StartAction would lay mass on one machine and show it to nobody.
        /// </summary>
        void TickAi()
        {
            if (!_handler || _status == null || !_status.IsInitializedAsAI) return;
            if (!IsSimulatingOwner(_status) || Time.time < _nextAiPlay) return;
            _nextAiPlay = Time.time + UnityEngine.Random.Range(aiPlayInterval.x, Mathf.Max(aiPlayInterval.x, aiPlayInterval.y));

            ResolveSlots();
            int start = UnityEngine.Random.Range(0, s_slotElements.Length);
            for (int k = 0; k < s_slotElements.Length; k++)
            {
                var element = s_slotElements[(start + k) % s_slotElements.Length];
                if (!_slots.TryGetValue(element, out var slot) || !slot) continue;
                if (!_slotInputs.TryGetValue(element, out var input)) continue;
                if (!Admissible(slot.Face(FaceB(element)), slot, out _)) continue;
                _handler.PerformShipControllerActionsReplicated(input);
                return;
            }
        }

        void ResolveSlots()
        {
            if (_slotsResolved || !_handler) return;
            _slotsResolved = true;
            s_inputs ??= (InputEvents[])Enum.GetValues(typeof(InputEvents));
            foreach (var ie in s_inputs)
            {
                if (!_handler.TryGetActionOn<TermiteCardActionSO>(ie, out var slot) || !slot) continue;
                if (_slots.ContainsKey(slot.SlotElement)) continue;
                _slots[slot.SlotElement] = slot;
                _slotInputs[slot.SlotElement] = ie;
            }
        }

        // ---------------- drone bookkeeping ----------------

        internal void Claim(Prism prism, TermiteDrone drone) { if (prism) _claims[prism] = drone; }

        internal void Release(Prism prism, TermiteDrone drone)
        {
            if (prism && _claims.TryGetValue(prism, out var owner) && owner == drone) _claims.Remove(prism);
        }

        internal bool IsClaimedByAnother(Prism prism, TermiteDrone drone)
            => _claims.TryGetValue(prism, out var owner) && owner && owner != drone && !owner.IsRetiring;

        internal void NotifyDroneGone(TermiteDrone drone)
        {
            _drones.Remove(drone);
            if (_claims.Count > 0)
            {
                s_releaseScratch.Clear();
                foreach (var kv in _claims) if (kv.Value == drone || !kv.Key) s_releaseScratch.Add(kv.Key);
                foreach (var p in s_releaseScratch) _claims.Remove(p);
            }
        }
        static readonly List<Prism> s_releaseScratch = new();

        internal void NotifyMoundFallen(TermiteMound mound)
        {
            _mounds.Remove(mound);
            // Its workers move to the next mound, or come home to the queen as escorts.
            for (int i = 0; i < _drones.Count; i++)
            {
                var d = _drones[i];
                if (d && d.Mound == mound) d.Rehome(NearestMound(d.transform.position));
            }
        }

        void RetireAllDrones()
        {
            for (int i = 0; i < _drones.Count; i++)
                if (_drones[i]) _drones[i].Retire();
            _claims.Clear();
        }

        void Compact()
        {
            for (int i = _drones.Count - 1; i >= 0; i--)
                if (!_drones[i] || _drones[i].IsRetiring) _drones.RemoveAt(i);
            for (int i = _mounds.Count - 1; i >= 0; i--)
                if (!_mounds[i] || _mounds[i].IsFallen) _mounds.RemoveAt(i);
        }

        void CompactCrystals()
        {
            for (int i = _crystals.Count - 1; i >= 0; i--)
                if (!_crystals[i]) _crystals.RemoveAt(i);
        }

        void PlaySfx(EventReference e) => PlaySfx(e, Queen.position);

        void PlaySfx(EventReference e, Vector3 at)
        {
            if (e.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(e, at);
        }
    }
}

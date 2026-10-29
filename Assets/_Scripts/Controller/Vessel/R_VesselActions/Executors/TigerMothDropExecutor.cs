using System.Collections.Generic;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs the Tiger Moth's Drop (<see cref="TigerMothDropActionSO"/>, <c>R_VesselActions/TIGER_MOTH.md</c>):
    /// press, and the hull shuts its wings and falls along its own down without turning, passing through everything,
    /// while the chase camera swings up over it; let go, and it pitches 90 degrees nose-down into the line it fell on
    /// and flies on. The recharge it then pays is set by how far it fell, and a fall that pays the full recharge
    /// leaves a wormhole pair between where it started and where it landed.
    ///
    /// <para><b>What runs where</b> (the Fold's split, BUTTERFLY_FOLD.md §7). Every peer runs the press and the
    /// release (the handler replays them), so every peer shuts the wings, stops the hull's own flight, makes it
    /// incorporeal and cuts its ribbons. Only the OWNER moves it: the fall and the landing turn are written on the
    /// owner and reach the others through the vessel's owner-authoritative transform. The wormhole is laid by every
    /// peer from poses every peer agrees on: the replicated pose at the press, and the replicated pose at the moment
    /// the replicated hull finishes its landing turn. The camera is the local pilot's alone.</para>
    ///
    /// <para><b>Incorporeal</b> (Garrett, 2026-10-29: "it should be incorporeal and clip through everything during that
    /// time"): every collider on the hull is switched off for the fall and the landing (prisms, crystals, rival hulls,
    /// its own dust and crystal catcher), and <see cref="VesselIncorporeal"/> tells the wormhole mouths, which test by
    /// position, to let it pass too.</para>
    ///
    /// <para><b>No trail</b>: the wake's pen is up and every ribbon on the hull (its tail and jets) is cut at the press
    /// and restarted at the landing, so nothing streaks the line it fell along.</para>
    /// </summary>
    public sealed class TigerMothDropExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Wired directly so a missing wire is visible in the inspector rather than a silent fallback.")]
        [SerializeField] TigerMothDropActionSO config;

        [Header("Audio")]
        [Tooltip("FMOD event when the drop starts (the wings shut and the hull falls). Every peer, at the hull. " +
                 "Leave empty for silence - never point it at a borrowed event.")]
        [SerializeField] FMODUnity.EventReference dropEngageEvent;

        [Tooltip("FMOD event when the hull has landed and flies on along the drop line. Every peer, at the hull. " +
                 "Leave empty for silence.")]
        [SerializeField] FMODUnity.EventReference dropLandEvent;

        [Tooltip("FMOD event when a full drop's wormhole opens. Once, at the landing mouth. Leave empty for silence.")]
        [SerializeField] FMODUnity.EventReference gatesOpenEvent;

        [Tooltip("FMOD event when a pilot flies into one of this Tiger Moth's mouths and out of the other. Played on the " +
                 "pilot's own machine, at the exit. Leave empty for silence.")]
        [SerializeField] FMODUnity.EventReference gateThreadEvent;

        // The theme the mouths' rims are painted from AND the roster they carry (injected on every spawn path,
        // GameObjectInjector.InjectRecursive), as the Fold's was.
        [Inject] GameDataSO _gameData;

        enum Phase { Idle, Dropping, Landing }

        IVesselStatus _status;
        TigerMothDropActionSO _activeSo;
        ButterflyAnimation _animation;
        ButterflyDustField _dustField;

        Phase _phase;
        bool _held;
        float _phaseSeconds;
        float _cameraSeconds;
        float _dropDistance;
        float _releaseSpeed;
        float _cruiseSpeed;
        Quaternion _hullAtPress = Quaternion.identity;
        Vector3 _pressPosition;
        float _cooldownUntil = float.NegativeInfinity;
        float _cooldownTotal;

        bool _stopped;      // did WE take the stance? only then may we give it back
        bool _turnHeld;     // did WE zero the transformer's turn rate?
        bool _penned;       // did WE pen the wake?
        bool _incorporeal;  // did WE switch the colliders off?
        readonly List<Collider> _disabledColliders = new();
        readonly List<TrailRenderer> _cutRibbons = new();
        readonly List<Collider> _colliderScratch = new();
        readonly List<TrailRenderer> _ribbonScratch = new();

        // The wormhole: the pair standing now, and a peer's wait for the replicated landing.
        WormholeMouth _gateA, _gateB;
        bool _awaitingGates;
        float _gateDeadline;
        Quaternion _landedFrame;
        bool _warnedNoGameData;

        /// <summary>True from the press until the landing has finished, on every peer.</summary>
        public bool IsDropping => _phase != Phase.Idle;

        public bool IsReady => Time.time >= _cooldownUntil;

        /// <summary>Recharge for the HUD's Time card: 1 = just spent, 0 = ready. Relative to the recharge THIS drop
        /// paid, so a hop's short wait sweeps the whole card as a full drop's does.</summary>
        public float CooldownRemaining01
        {
            get
            {
                if (_cooldownTotal <= 0f) return 0f;
                float remaining = _cooldownUntil - Time.time;
                return remaining <= 0f ? 0f : Mathf.Clamp01(remaining / _cooldownTotal);
            }
        }

        TigerMothDropActionSO Config => _activeSo ? _activeSo : config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            var root = shipStatus?.Vessel != null ? shipStatus.Vessel.Transform : null;
            _animation = root ? root.GetComponentInChildren<ButterflyAnimation>(true) : null;
            _dustField = root ? root.GetComponentInChildren<ButterflyDustField>(true) : null;

            // A re-init hands the hull to a different pilot (a vessel swap, an ownership swap): whatever the last
            // pilot was in the middle of is not this one's - and neither is their wormhole.
            End(landed: false);
            _awaitingGates = false;
            RetireGates();
            _cooldownUntil = float.NegativeInfinity;
            _cooldownTotal = 0f;
        }

        void OnDisable() => End(landed: false);

        void OnDestroy()
        {
            // The camera outlives this vessel. Keyed, so a late teardown cannot cancel another hull's frame.
            if (_status != null) VesselPlacementView.ClearFrame(_status.Transform);
            // The pair belongs to the Tiger Moth that laid it; nothing is left to replace it once that hull is gone.
            _awaitingGates = false;
            RetireGates();
        }

        public void Engage(TigerMothDropActionSO so, IVesselStatus status)
        {
            if (!so) return;
            if (_status == null) _status = status;
            if (_status == null || _phase != Phase.Idle || !IsReady) return;   // refused: the card's veil shows the wait

            _activeSo = so;
            _phase = Phase.Dropping;
            _held = true;
            _phaseSeconds = 0f;
            _cameraSeconds = 0f;
            _dropDistance = 0f;
            _hullAtPress = _status.Transform.rotation;
            _pressPosition = _status.Transform.position;
            _cruiseSpeed = _status.Speed;

            SetStopped(true);
            HoldTurn(true);
            SetPen(true);
            SetIncorporeal(true);
            CutRibbons();
            if (_animation) _animation.SetFolded(true);
            PlayCue(dropEngageEvent, _pressPosition);
            PushCamera();
        }

        public void Release(TigerMothDropActionSO so, IVesselStatus status) => _held = false;

        void Update()
        {
            if (_awaitingGates) TickGatePlacement();
            if (_phase == Phase.Idle || _status == null) return;
            var so = Config;
            if (!so) { End(landed: false); return; }

            float dt = Time.deltaTime;
            _phaseSeconds += dt;
            _cameraSeconds += dt;
            Transform root = _status.Transform;
            Vector3 down = TigerMothDropMath.DropDirection(_hullAtPress);

            if (_phase == Phase.Dropping)
            {
                // No longest drop: it falls for as long as the trigger is held. Every peer keeps the distance (it
                // prices the recharge); only the owner moves the hull by it.
                float speed = TigerMothDropMath.DropSpeed(_phaseSeconds, so.DropSpeed, so.DropEaseSeconds);
                float step = speed * dt;
                _dropDistance += step;
                _releaseSpeed = speed;
                if (_status.IsNetworkOwner) root.position += down * step;

                if (!_held)
                {
                    _phase = Phase.Landing;
                    _phaseSeconds = 0f;
                    _cooldownTotal = so.ResolveCooldown(_status, _dropDistance);
                    _cooldownUntil = Time.time + _cooldownTotal;
                }
            }
            else
            {
                var (turn01, speed) = TigerMothDropMath.Landing(_phaseSeconds, so.LandSeconds, _releaseSpeed, _cruiseSpeed);
                if (_status.IsNetworkOwner)
                {
                    root.position += down * (speed * dt);
                    _status.VesselTransformer?.SetRotationImmediate(TigerMothDropMath.Pitched(_hullAtPress, turn01));
                }
                if (turn01 >= 1f)
                {
                    PlayCue(dropLandEvent, root.position);
                    End(landed: true);
                    ArmGates(so);
                    return;
                }
            }

            PushCamera();
        }

        void PushCamera()
        {
            var so = Config;
            if (_status == null || !so) return;
            VesselPlacementView.Frame(_status.Transform, TigerMothDropMath.DropFrame(_hullAtPress),
                TigerMothDropMath.CameraWeight(_cameraSeconds, so.CameraEnterSeconds));
        }

        /// <summary>Hand everything back. Idempotent and unconditional where it can be: a vessel despawned or
        /// re-initialised mid-drop must not keep its stance, its frozen turn, its pen, its ghostliness or the player's
        /// camera. A drop cut short (<paramref name="landed"/> false) leaves the hull where it is, still upright.</summary>
        void End(bool landed)
        {
            _phase = Phase.Idle;
            _held = false;
            SetStopped(false);
            HoldTurn(false);
            SetPen(false);
            SetIncorporeal(false);
            RestartRibbons();
            if (_animation) _animation.SetFolded(false);
            // On a landing the hull now IS the camera's frame, so releasing the frame moves nothing.
            if (_status != null) VesselPlacementView.ClearFrame(_status.Transform);
        }

        // ---- the wormhole ----------------------------------------------------------------------------------------

        /// <summary>Wait for the landing pose this peer can trust. The owner has it now. A peer's transform still trails
        /// the owner's, so it waits until its own copy of the hull has finished the landing turn: that pose is the
        /// owner's landing, whatever the latency.</summary>
        void ArmGates(TigerMothDropActionSO so)
        {
            if (_status == null) return;
            _landedFrame = TigerMothDropMath.DropFrame(_hullAtPress);
            _gateDeadline = Time.time + so.GateSettleSeconds;
            _awaitingGates = true;
            TickGatePlacement();
        }

        void TickGatePlacement()
        {
            var so = Config;
            if (!so || _status == null) { _awaitingGates = false; return; }

            bool settled = _status.IsNetworkOwner || Quaternion.Angle(_status.Transform.rotation, _landedFrame) < 0.5f;
            if (!settled)
            {
                if (Time.time >= _gateDeadline) _awaitingGates = false;   // the pose never arrived: lay nothing
                return;
            }
            _awaitingGates = false;

            // Only a drop that paid the FULL recharge leaves a pair (Garrett, 2026-10-29), measured on poses every peer
            // agrees on, so every peer makes the same call. A shorter drop leaves the standing pair as it was.
            Vector3 landedAt = _status.Transform.position;
            if (!TigerMothDropMath.PaysFullCooldown(Vector3.Distance(_pressPosition, landedAt), so.ResolveFullDistance(_status)))
                return;
            PlaceGates(so, _pressPosition, landedAt);
        }

        void PlaceGates(TigerMothDropActionSO so, Vector3 from, Vector3 to)
        {
            if (_gameData == null)
            {
                if (!_warnedNoGameData)
                {
                    _warnedNoGameData = true;
                    CSDebug.LogWarning("[TigerMothDrop] No GameDataSO injected on this vessel - the drop's wormhole " +
                                       "cannot be placed. A runtime-created vessel must go through " +
                                       "GameObjectInjector.InjectRecursive.");
                }
                return;
            }

            // Retired BEFORE the new pair is built, so a pilot never sees four mouths. Only a new full drop, or the hull
            // being destroyed, closes a pair: there is no lifespan.
            RetireGates();
            _gateA = BuildGate(so, $"DropWormhole::{_status.PlayerName}::A", from);
            _gateB = BuildGate(so, $"DropWormhole::{_status.PlayerName}::B", to);
            WormholeMouth.Pair(_gateA, _gateB);
            PlayCue(gatesOpenEvent, to);
            CSDebug.LogVerbose(CSLogChannel.ButterflyFold,
                $"[TigerMothDrop] {_status.PlayerName} ({_status.Domain}) opened a pair {Vector3.Distance(from, to):F0}u apart.");
        }

        WormholeMouth BuildGate(TigerMothDropActionSO so, string name, Vector3 centre)
        {
            var go = new GameObject(name);
            go.transform.position = centre;
            var mouth = go.AddComponent<WormholeMouth>();
            var domain = _status.Domain;
            mouth.Build(new WormholeMouth.Settings
            {
                SurfaceMaterial = so.WormholeMaterial,
                BloomSeconds = so.GateBloomSeconds,
                ExactRange = so.PortalWindowRange,
                ExactFadeBand = so.PortalWindowFadeBand,
                ExactRenderScale = so.PortalWindowRenderScale,
                PanoramaFaceSize = so.PanoramaFaceSize,
                TransitEvent = gateThreadEvent,
                // The rim wears the placer's domain: the colour says who rides it free.
                RimTint = ToyFactory.DomainAccentColor(_gameData.ThemeManagerData, domain),
                DomainTolled = true,
                TollPetalsPerElement = so.RivalTollPetalsPerElement,
                TollShedSpeed = so.RivalTollShedSpeed,
                TollHitClass = so.TollHitClass,
                TollHitLanded = so.OnTollHitLanded,
                Domain = domain,
                Owner = _status,
                Theme = _gameData.ThemeManagerData,
            }, _gameData.Players, so.GateRadius);
            return mouth;
        }

        void RetireGates()
        {
            float seconds = Config ? Config.GateBloomSeconds : 0.45f;
            if (_gateA) _gateA.Retire(seconds);
            if (_gateB) _gateB.Retire(seconds);
            _gateA = null;
            _gateB = null;
        }

        // ---- holding the hull ------------------------------------------------------------------------------------

        void SetStopped(bool stopped)
        {
            if (stopped == _stopped) return;
            _stopped = stopped;
            // The owner writes the replicated stance; every other peer adopts it.
            if (_status?.Vessel is VesselController controller && _status.IsNetworkOwner)
                controller.SetTranslationRestricted(stopped);
        }

        void HoldTurn(bool hold)
        {
            if (hold == _turnHeld) return;
            _turnHeld = hold;
            var transformer = _status?.VesselTransformer;
            if (transformer) transformer.ExternalTurnRateMultiplier = hold ? 0f : 1f;
        }

        void SetPen(bool penned)
        {
            if (penned == _penned) return;
            _penned = penned;
            _status?.VesselPrismController?.SetSpawnerPaused(penned);
        }

        /// <summary>Every enabled collider on the hull off, and only those back on: a collider something else had
        /// switched off stays off. The dust capsule then re-reads its MODE, in case it changed during the drop.</summary>
        void SetIncorporeal(bool incorporeal)
        {
            if (incorporeal == _incorporeal) return;
            _incorporeal = incorporeal;
            var root = _status?.Vessel != null ? _status.Vessel.Transform : null;
            if (root) VesselIncorporeal.Set(root, incorporeal);

            if (incorporeal)
            {
                _disabledColliders.Clear();
                if (!root) return;
                _colliderScratch.Clear();
                root.GetComponentsInChildren(false, _colliderScratch);
                foreach (var c in _colliderScratch)
                {
                    if (!c || !c.enabled) continue;
                    c.enabled = false;
                    _disabledColliders.Add(c);
                }
            }
            else
            {
                foreach (var c in _disabledColliders)
                    if (c) c.enabled = true;
                _disabledColliders.Clear();
                if (_dustField) _dustField.ReassertColliders();
            }
        }

        /// <summary>Cut every ribbon on the hull where the drop starts (the laid part drains away as a ghost, the way a
        /// teleport's does) and stop it laying, so the fall leaves no line.</summary>
        void CutRibbons()
        {
            _cutRibbons.Clear();
            var root = _status?.Vessel != null ? _status.Vessel.Transform : null;
            if (!root) return;
            _ribbonScratch.Clear();
            root.GetComponentsInChildren(false, _ribbonScratch);
            foreach (var trail in _ribbonScratch)
            {
                if (!trail || !trail.enabled || !trail.emitting) continue;
                if (trail.positionCount > 0) TeleportRibbonGhost.Spawn(trail, _pressPosition, _cruiseSpeed);
                trail.Clear();
                trail.emitting = false;
                _cutRibbons.Add(trail);
            }
        }

        /// <summary>Start every ribbon this drop cut again, from where the hull is now.</summary>
        void RestartRibbons()
        {
            foreach (var trail in _cutRibbons)
            {
                if (!trail) continue;
                trail.Clear();
                trail.emitting = true;
            }
            _cutRibbons.Clear();
        }

        static void PlayCue(FMODUnity.EventReference reference, Vector3 position)
        {
            if (reference.IsNull) return;
            var audio = CosmicShore.Core.AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, position);
        }
    }
}

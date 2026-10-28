using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs the Tiger Moth's Drop (<see cref="TigerMothDropActionSO"/>, <c>R_VesselActions/TIGER_MOTH.md</c>):
    /// press, and the hull shuts its wings and falls along its own down without turning while the chase camera
    /// swings up over it; let go, and it pitches 90 degrees nose-down into the line it fell on and flies on.
    ///
    /// <para><b>What runs where</b> (the Fold's split, BUTTERFLY_FOLD.md §7). Every peer runs the press and the
    /// release (the handler replays them), so every peer shuts the wings and stops the hull's own flight. Only the
    /// OWNER moves it: the fall and the landing turn are written on the owner and reach the others through the
    /// vessel's owner-authoritative transform, as every flight does. The camera is the local pilot's alone
    /// (<see cref="VesselPlacementView"/> ignores any other vessel), so an AI or a remote copy moves no camera.</para>
    ///
    /// <para><b>The hull's own flight is held for the whole ability.</b> The translation stance
    /// (<c>IsTranslationRestricted</c>, replicated) stops the cruise and, with the prefab's
    /// <c>restrictedTurnMultiplier = 0</c>, pitch and yaw; <see cref="VesselTransformer.ExternalTurnRateMultiplier"/>
    /// at 0 stops the roll the stance leaves live. So the only thing moving the hull is this executor, which is
    /// what "the vessel never rotates" needs. The stance does not write <c>VesselStatus.Speed</c>, so the cruise
    /// read at the press is still the vessel's own after the landing.</para>
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

        enum Phase { Idle, Dropping, Landing }

        IVesselStatus _status;
        TigerMothDropActionSO _activeSo;
        ButterflyAnimation _animation;

        Phase _phase;
        bool _held;
        float _phaseSeconds;
        float _cameraSeconds;
        float _dropDistance;
        float _dropRange;
        float _releaseSpeed;
        float _cruiseSpeed;
        Quaternion _hullAtPress = Quaternion.identity;
        float _cooldownUntil = float.NegativeInfinity;

        bool _stopped;      // did WE take the stance? only then may we give it back
        bool _turnHeld;     // did WE zero the transformer's turn rate?
        bool _penned;       // did WE pen the wake?

        /// <summary>True from the press until the landing has finished, on every peer.</summary>
        public bool IsDropping => _phase != Phase.Idle;

        public bool IsReady => Time.time >= _cooldownUntil;

        /// <summary>Recharge for the HUD's Time card: 1 = just spent, 0 = ready.</summary>
        public float CooldownRemaining01
        {
            get
            {
                var so = Config;
                if (!so) return 0f;
                float total = so.ResolveCooldown(_status);
                if (total <= 0f) return 0f;
                float remaining = _cooldownUntil - Time.time;
                return remaining <= 0f ? 0f : Mathf.Clamp01(remaining / total);
            }
        }

        TigerMothDropActionSO Config => _activeSo ? _activeSo : config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _animation = shipStatus?.Vessel != null
                ? shipStatus.Vessel.Transform.GetComponentInChildren<ButterflyAnimation>(true)
                : null;

            // A re-init hands the hull to a different pilot (a vessel swap, an ownership swap): whatever the last
            // pilot was in the middle of is not this one's.
            End(landed: false);
            _cooldownUntil = float.NegativeInfinity;
        }

        void OnDisable() => End(landed: false);

        void OnDestroy()
        {
            // The camera outlives this vessel. Keyed, so a late teardown cannot cancel another hull's frame.
            if (_status != null) VesselPlacementView.ClearFrame(_status.Transform);
        }

        public void Engage(TigerMothDropActionSO so, IVesselStatus status)
        {
            if (!so) return;
            if (_status == null) _status = status;
            if (_status == null || _phase != Phase.Idle || !IsReady) return;   // a refused press shows on the card's veil

            _activeSo = so;
            _phase = Phase.Dropping;
            _held = true;
            _phaseSeconds = 0f;
            _cameraSeconds = 0f;
            _dropDistance = 0f;
            _dropRange = so.ResolveRange(_status);
            _hullAtPress = _status.Transform.rotation;
            _cruiseSpeed = _status.Speed;

            SetStopped(true);
            HoldTurn(true);
            if (!so.LayWakeWhileDropping) SetPen(true);
            if (_animation) _animation.SetFolded(true);
            PlayCue(dropEngageEvent, _status.Transform.position);
            PushCamera();
        }

        public void Release(TigerMothDropActionSO so, IVesselStatus status) => _held = false;

        void Update()
        {
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
                // Every peer keeps the distance (so every peer agrees when the range lands the hull); only the
                // owner moves the hull by it.
                float speed = TigerMothDropMath.DropSpeed(_phaseSeconds, so.DropSpeed, so.DropEaseSeconds);
                float step = Mathf.Min(speed * dt, Mathf.Max(0f, _dropRange - _dropDistance));
                _dropDistance += step;
                _releaseSpeed = speed;
                if (_status.IsNetworkOwner) root.position += down * step;

                if (!_held || _dropDistance >= _dropRange - 1e-3f)
                {
                    _phase = Phase.Landing;
                    _phaseSeconds = 0f;
                    _cooldownUntil = Time.time + so.ResolveCooldown(_status);
                    // The wake comes back at the landing, not at the end of it: the hull is flying the new line
                    // from here, so the trail should start where the turn starts.
                    SetPen(false);
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
        /// re-initialised mid-drop must not keep its stance, its frozen turn, its pen or the player's camera.
        /// A drop cut short (<paramref name="landed"/> false) leaves the hull where it is, still upright: only the
        /// owner's landing turns it, and a cut drop never reaches one.</summary>
        void End(bool landed)
        {
            _phase = Phase.Idle;
            _held = false;
            SetStopped(false);
            HoldTurn(false);
            SetPen(false);
            if (_animation) _animation.SetFolded(false);
            // On a landing the hull now IS the camera's frame, so releasing the frame moves nothing.
            if (_status != null) VesselPlacementView.ClearFrame(_status.Transform);
        }

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

        static void PlayCue(FMODUnity.EventReference reference, Vector3 position)
        {
            if (reference.IsNull) return;
            var audio = CosmicShore.Core.AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, position);
        }
    }
}

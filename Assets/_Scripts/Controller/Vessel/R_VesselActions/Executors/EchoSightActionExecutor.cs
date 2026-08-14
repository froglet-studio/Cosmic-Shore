using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin's <b>Echo Sight</b>. Hold the trigger and the view eases into a zoomed
    /// first-person shot down the blast axis while every prism inside the next crystal blast's
    /// destruction volume lights up; release and it eases back.
    ///
    /// The Dolphin banks blast energy by skimming and spends it all in one shot on a crystal, and
    /// that energy IS the cone's gape (DOLPHIN_ENERGY_ECONOMY.md §1). Before this the pilot could
    /// only read the gape as an ANGLE — off the hull's jaws or the HUD's jaw icon — and had to
    /// guess what that angle actually covered out in the world. The sight answers the question
    /// directly: it draws the volume onto the mass standing in it.
    ///
    /// <para><b>Three view surfaces, three owners.</b> Camera POSE is this executor's (it writes
    /// the follow offset). FOV is <b>not</b>: it is pushed through
    /// <see cref="VesselSpeedTunnel.SetHomeFovOverride"/> so the speed tunnel remains the single
    /// FOV writer — an ability that writes <c>Camera.fieldOfView</c> itself gets silently
    /// overwritten while the tunnel is engaged, and gets its zoom baked in permanently as the
    /// "home" the tunnel restores to. The prism HIGHLIGHT is
    /// <see cref="PrismDestructionSight"/>'s, published as global uniforms with zero per-prism
    /// work.</para>
    ///
    /// <para><b>Local pilot only</b>, and it never touches gameplay: no speed change, no input
    /// mute, nothing replicated. A remote peer sees a Dolphin flying normally, which is correct —
    /// the sight is a thing the pilot looks through, not a thing the vessel does.</para>
    /// </summary>
    public sealed class EchoSightActionExecutor : ShipActionExecutorBase
    {
        [Header("Setup")]
        [Tooltip("The crystal-impact blast this sight previews. Wired directly so the sight reads " +
                 "the SAME authored scales and Space reach the detonation uses - a preview with " +
                 "its own copy of those numbers would drift the first time one was retuned.")]
        [SerializeField] private VesselExplosionByCrystalEffectSO blastEffect;

        IVesselStatus _status;
        EchoSightActionSO _so;

        bool _engaged;
        float _blend;              // 0 = flying normally, 1 = fully sighted
        bool _cameraCaptured;
        Vector3 _neutralFollowOffset;
        CustomCameraController _camera;

        // The FOV the zoom measures DOWN from, captured at engage. It must NOT be re-read live:
        // once the override is in force the tunnel is writing the camera from that override, so
        // reading the camera back would feed the zoom into its own input and run away.
        float _capturedHomeFov;

        public override void Initialize(IVesselStatus shipStatus)
        {
            // A vessel swap re-runs Initialize on a live component, so drop any sight still in
            // force before adopting the new pilot - otherwise the camera and the shader globals
            // stay pushed for a vessel that is no longer flying.
            HardReset();
            _status = shipStatus;
        }

        void OnDisable() => HardReset();

        /// <summary>True while the pilot is holding the sight.</summary>
        public bool IsEngaged => _engaged;

        /// <summary>How far into the sighted view the camera currently is, 0-1. HUD-readable.</summary>
        public float Blend01 => _blend;

        // ---------------- Action ----------------

        public void Engage(EchoSightActionSO so, IVesselStatus status)
        {
            if (so) _so = so;
            if (status != null) _status = status;
            if (!IsLocalPilot) return;
            _engaged = true;
        }

        public void Release(EchoSightActionSO so, IVesselStatus status)
        {
            if (so) _so = so;
            _engaged = false;
        }

        bool IsLocalPilot
        {
            get
            {
                var player = _status?.Player;
                return player != null && player.IsLocalPilot && !_status.IsInitializedAsAI;
            }
        }

        // ---------------- Drive ----------------

        void Update()
        {
            var so = _so;
            if (!so) return;

            // Ease both ways. Nothing snaps into or out of the sight - continuity of existence
            // covers a view transition as much as it covers mass.
            float rate = Time.deltaTime / Mathf.Max(0.01f, so.TransitionSeconds);
            float target = _engaged && IsLocalPilot ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, rate);

            if (_blend <= 0f)
            {
                if (_cameraCaptured) ReleaseView();
                return;
            }

            DriveView(so);
            PublishHighlight(so);
        }

        void DriveView(EchoSightActionSO so)
        {
            if (!_cameraCaptured) CaptureView();
            if (!_camera) return;

            // Smoothstep so the push-in accelerates out of rest and settles rather than tracking
            // the linear blend, which reads mechanical on a camera move.
            float t = _blend * _blend * (3f - 2f * _blend);

            _camera.SetFollowOffset(Vector3.Lerp(_neutralFollowOffset, so.SightFollowOffset, t));

            // FOV goes through the tunnel, never onto the camera. See the class doc.
            if (_capturedHomeFov > 0f)
                VesselSpeedTunnel.SetHomeFovOverride(
                    Mathf.Lerp(_capturedHomeFov, so.SightFieldOfView, t), this);
        }

        void CaptureView()
        {
            _camera = CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;

            if (_camera)
            {
                _neutralFollowOffset = _camera.GetFollowOffset();
                _capturedHomeFov = _camera.Camera != null ? _camera.Camera.fieldOfView : 0f;
            }

            _cameraCaptured = true;
        }

        void ReleaseView()
        {
            if (_camera) _camera.SetFollowOffset(_neutralFollowOffset);

            // Identity-keyed, so a late release from a swapped-out vessel cannot cancel a newer
            // sight's override.
            VesselSpeedTunnel.ClearHomeFovOverride(this);
            PrismDestructionSight.Clear();

            _camera = null;
            _cameraCaptured = false;
            _capturedHomeFov = 0f;
        }

        void PublishHighlight(EchoSightActionSO so)
        {
            if (!blastEffect || _status == null)
            {
                PrismDestructionSight.Clear();
                return;
            }

            if (!blastEffect.TryResolveBlastVolume(_status, out var volume))
            {
                PrismDestructionSight.Clear();
                return;
            }

            PrismDestructionSight.Publish(volume, _blend * so.HighlightStrength);
        }

        void HardReset()
        {
            _engaged = false;
            _blend = 0f;
            if (_cameraCaptured) ReleaseView();
            else
            {
                VesselSpeedTunnel.ClearHomeFovOverride(this);
                PrismDestructionSight.Clear();
            }
        }
    }
}

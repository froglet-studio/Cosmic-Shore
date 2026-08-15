using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin's <b>Echo Sight</b>. Hold the trigger and every prism standing inside the next
    /// crystal blast's destruction volume lights up; release and the highlight fades away.
    ///
    /// The Dolphin banks blast energy by skimming and spends it all in one shot on a crystal, and
    /// that energy IS the cone's gape (DOLPHIN_ENERGY_ECONOMY.md §1). Before this the pilot could
    /// only read the gape as an ANGLE — off the hull's jaws or the HUD's jaw icon — and had to
    /// guess what that angle actually covered out in the world. The sight answers the question
    /// directly: it draws the volume onto the mass standing in it.
    ///
    /// <para><b>It touches nothing but photons.</b> No camera write of any kind — no pose, no field
    /// of view — no speed change, no input mute, nothing replicated. The whole ability is
    /// <see cref="PrismDestructionSight"/>'s global uniforms, published while the trigger is held.
    /// That is what keeps it clear of the speed tunnel (<c>Docs/SPEED_TUNNEL.md</c>), which owns the
    /// gameplay camera's FOV fleet-wide and admits exactly one hold.</para>
    ///
    /// <para><b>Local pilot only.</b> A remote peer sees a Dolphin flying normally, which is
    /// correct — the sight is a thing the pilot looks through, not a thing the vessel does.</para>
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
        float _blend;   // 0 = no highlight, 1 = fully lit

        public override void Initialize(IVesselStatus shipStatus)
        {
            // A vessel swap re-runs Initialize on a live component, so drop any sight still in
            // force before adopting the new pilot - otherwise the globals stay published for a
            // vessel that is no longer flying.
            HardReset();
            _status = shipStatus;
        }

        void OnDisable() => HardReset();

        /// <summary>True while the pilot is holding the sight.</summary>
        public bool IsEngaged => _engaged;

        /// <summary>How far the highlight has faded in, 0-1. HUD-readable.</summary>
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

            // Ease both ways. Nothing snaps into or out of the sight.
            float rate = Time.deltaTime / Mathf.Max(0.01f, so.TransitionSeconds);
            float target = _engaged && IsLocalPilot ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, rate);

            if (_blend <= 0f)
            {
                PrismDestructionSight.Clear();
                return;
            }

            if (!blastEffect || _status == null ||
                !blastEffect.TryResolveBlastVolume(_status, out var volume))
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
            PrismDestructionSight.Clear();
        }
    }
}

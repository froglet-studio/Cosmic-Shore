using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
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
    ///
    /// <para><b>Charge owns this ability</b> (2026-08-17). Charge's multiplier sets the blast's
    /// capsule THICKNESS — 0.75x at rest to 1.5x at level 10, authored on
    /// <see cref="VesselExplosionByCrystalEffectSO"/> — so the shape the sight draws is the shape
    /// Charge has been fattening, and its level-5 upgrade extends the sight from mass to PILOTS:
    /// every vessel inside the same volume brightens in its own domain colour
    /// (<see cref="EchoSightVesselHighlighter"/>). One trigger, one volume, two things standing in
    /// it.</para>
    /// </summary>
    public sealed class EchoSightActionExecutor : ShipActionExecutorBase
    {
        [Header("Setup")]
        [Tooltip("The crystal-impact blast this sight previews. Wired directly so the sight reads " +
                 "the SAME authored scales and Space reach the detonation uses - a preview with " +
                 "its own copy of those numbers would drift the first time one was retuned.")]
        [SerializeField] private VesselExplosionByCrystalEffectSO blastEffect;

        [Header("Charge level-5 — pilot highlight")]
        [Tooltip("Seconds a VESSEL takes to bloom into / fade out of the highlight as the cone " +
                 "sweeps over it. Nothing pops.")]
        [SerializeField, Min(0.01f)] private float vesselHighlightFadeSeconds = 0.18f;
        [Tooltip("Brightness gain applied to a highlighted vessel's own colours at full highlight. " +
                 "It multiplies whatever each material already rests at, so an engine that rests " +
                 "bright brightens further rather than being flattened.")]
        [SerializeField, Min(1f)] private float vesselHighlightGain = 4f;
        [Tooltip("How far the marked hull is driven to its SATURATED domain colour (0 = brightness " +
                 "only). Brightness alone is not enough: the sight lights up the surrounding prisms " +
                 "at the same time, so only HUE separates a marked ship from the lit mass around it.")]
        [SerializeField, Range(0f, 1f)] private float vesselHighlightSaturation = 0.85f;
        [Tooltip("Halo radius as a multiple of the target's own hull radius. The ring lands ON the " +
                 "silhouette, so this also sets how far outside the hull the glow reaches.")]
        [SerializeField, Min(1.05f)] private float vesselHaloScale = 2.4f;
        [Tooltip("Peak additive strength of the halo. This is the ONLY part of the highlight that " +
                 "reads when the target is behind mass (it draws with ZTest Always), so it is what " +
                 "makes the ability work in a dense arena.")]
        [SerializeField, Min(0f)] private float vesselHaloIntensity = 1.4f;
        [Tooltip("Turn the halo off to leave the hull tint as the only mark. Off means a pilot " +
                 "standing behind prisms cannot be seen at all - only do this to isolate a problem.")]
        [SerializeField] private bool vesselHaloEnabled = true;

        // The player roster - the one live list of who is flying. Injected rather than searched:
        // vessels DO get GameObjectInjector.InjectRecursive at spawn, so this is populated by the
        // time any pilot can hold a trigger.
        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        EchoSightActionSO _so;

        EchoSightVesselHighlighter _vesselHighlighter;

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
                DriveVesselHighlight(default, 0f);
                return;
            }

            if (!blastEffect || _status == null ||
                !blastEffect.TryResolveBlastVolume(_status, out var volume))
            {
                PrismDestructionSight.Clear();
                DriveVesselHighlight(default, 0f);
                return;
            }

            float strength = _blend * so.HighlightStrength;
            PrismDestructionSight.Publish(volume, strength);
            DriveVesselHighlight(volume, strength);
        }

        /// <summary>
        /// The Charge level-5 half: pilots standing in the same volume light up.
        ///
        /// Below the upgrade the highlighter is fed a zero strength rather than skipped, so anything
        /// still lit from a moment ago fades out properly when the upgrade is lost mid-flight — a
        /// re-lock must not strand a vessel at full brightness. Gated on
        /// <c>IsUpgradeActive</c> and not a raw level read: this is a thing other players SEE on
        /// their own hull, so every machine has to agree on it.
        /// </summary>
        void DriveVesselHighlight(in BlastVolume volume, float strength01)
        {
            bool upgraded = strength01 > 0f
                            && _status?.ElementalAbilityHandler != null
                            && _status.ElementalAbilityHandler.IsUpgradeActive(Element.Charge);

            if (!upgraded && _vesselHighlighter == null) return;

            _vesselHighlighter ??= new EchoSightVesselHighlighter(
                vesselHighlightFadeSeconds, vesselHighlightGain, vesselHighlightSaturation,
                vesselHaloScale, vesselHaloIntensity, vesselHaloEnabled);

            _vesselHighlighter.Tick(
                upgraded ? _gameData?.Players : null,
                _status?.Vessel,
                volume,
                upgraded ? strength01 : 0f,
                Time.deltaTime,
                ResolveDomainSignalColor);
        }

        /// <summary>
        /// A vessel's own domain colour at full strength. Read live off the shared
        /// <c>ColorSet</c> — the path every other domain-tinted surface reads — so two rivals in one
        /// cone are tellable apart and the freestyle domain-changer toy re-colours a mark mid-flight.
        /// </summary>
        Color ResolveDomainSignalColor(IVessel vessel)
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            var domain = vessel?.VesselStatus != null ? vessel.VesselStatus.Domain : Domains.Blue;
            return colorSet != null ? colorSet.GetDomainSignalColor(domain) : Color.white;
        }

        void HardReset()
        {
            _engaged = false;
            _blend = 0f;
            PrismDestructionSight.Clear();

            // Restores every borrowed brightness immediately. A faded-but-unrestored highlight would
            // leave a rival vessel permanently over-bright with nothing left running to fix it.
            _vesselHighlighter?.ClearAll();
        }
    }
}

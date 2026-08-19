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
    /// of view — no speed change, no input mute, and nothing it does can destroy, move or protect a
    /// single prism. The whole ability is <see cref="PrismDestructionSight"/>'s global uniforms,
    /// published while the trigger is held. That is what keeps it clear of the speed tunnel
    /// (<c>Docs/SPEED_TUNNEL.md</c>), which owns the gameplay camera's FOV fleet-wide and admits
    /// exactly one hold. It does now put three floats on the wire (below), but they describe the
    /// overlay and are read by nothing else — no outcome anywhere depends on them.</para>
    ///
    /// <para><b>Everyone sees it, but not the same way</b> (2026-08-19). The sight used to be
    /// strictly local, on the reasoning that it is a thing the pilot looks through rather than a
    /// thing the vessel does. It is now visible to every player, because in the two Dolphin-only
    /// modes what a rival is about to remove is the single most useful thing on the field — and it
    /// costs the mode nothing to say, since the Dolphin already telegraphs its aim with its jaws.
    /// The two cases are deliberately different looks:
    /// <list type="bullet">
    /// <item><b>Yours</b> goes to <see cref="PrismDestructionSight.PublishLocal"/> and is UNCHANGED
    /// — same pale cool cast, same gain, and (verified bit-for-bit by
    /// <c>Tools/Shaders/verify_prism_sight_composition.py</c>) the same value out of the shader,
    /// including when four rivals are aiming at the same prism. Your cone wins outright on every
    /// prism it covers: an instrument that changes colour because someone else swept past is an
    /// instrument you cannot read.</item>
    /// <item><b>Theirs</b> goes to <see cref="PrismDestructionSight.PublishPeer"/> tinted with that
    /// pilot's DOMAIN colour, so a lit patch of mass says whose blast is coming for it.</item>
    /// </list>
    /// The trigger itself needed no new networking: <see cref="R_VesselActionHandler"/> already
    /// round-trips every press and release through the server, so this executor's
    /// <see cref="Engage"/> and <see cref="Release"/> were already being called on every peer's
    /// replica and only the <c>IsLocalPilot</c> guard was throwing the result away. What DID need
    /// replicating is the cone's SIZE — see <see cref="R_VesselActionHandler.NetEchoSightShape"/>
    /// for the two independent reasons a remote replica cannot derive it.</para>
    ///
    /// <para><b>The Charge-5 pilot highlight stays local.</b> Marking the ships a blast would catch
    /// is a targeting aid for the pilot aiming it, and showing every pilot's marks on every machine
    /// would stack ZTest-Always halos on the same hull from three directions at once. Prisms are
    /// shared because mass is the shared object; a mark on a person is not.</para>
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
        [Tooltip("Halo radius as a multiple of the target's own hull radius, used while the target is " +
                 "close enough that this is the larger of the two sizes. The ring lands ON the " +
                 "silhouette here, so this also sets how far outside the hull the glow reaches.")]
        [SerializeField, Min(1.05f)] private float vesselHaloScale = 2.4f;
        [Tooltip("FLOOR on the halo's on-screen size, as a fraction of half the screen height. This " +
                 "is what stops the halo shrinking with distance: past the depth where the hull-sized " +
                 "disc would fall below it, the halo holds a constant angular size and a rival across " +
                 "the arena stays exactly as findable as one alongside you. Raise it past every " +
                 "practical hull size to make the halo the same size at ALL distances.")]
        [SerializeField, Range(0f, 0.5f)] private float vesselHaloMinScreenRadius = 0.055f;
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

        // This vessel's slot in the peer bank. The instance id rather than the player id so a
        // vessel swap - which builds a whole new executor - can never inherit the old ship's slot.
        int _peerSlotId;

        // Which channels this executor currently occupies. Tracked rather than re-derived from
        // IsLocalPilot at teardown time, because the whole hazard is the case where that answer has
        // CHANGED since the publish: a live vessel handed to another player (the Cellular Duel
        // ownership swap) must retire the channel it was using, not the one it would use now.
        // _peerPublished also keeps a local pilot's teardown from clearing a peer slot it never took.
        bool _localPublished;
        bool _peerPublished;

        // Last shape written to the network, so the owner dirties the NetworkVariable when the
        // cone meaningfully changes rather than on every frame the energy meter creeps.
        Vector3 _lastPublishedShape = Vector3.zero;

        /// <summary>
        /// Fractional change in any of the three shape scalars below which the owner does not
        /// re-publish. At the shipped cone sizes this is well under a pixel of drawn difference at
        /// any range, and it turns a continuously-creeping meter into a few ticks per second
        /// instead of one per frame.
        /// </summary>
        const float ShapeRepublishEpsilon = 0.005f;

        public override void Initialize(IVesselStatus shipStatus)
        {
            // A vessel swap re-runs Initialize on a live component, so drop any sight still in
            // force before adopting the new pilot - otherwise the globals stay published for a
            // vessel that is no longer flying.
            HardReset();
            _status = shipStatus;
            _peerSlotId = GetInstanceID();
        }

        void OnDisable() => HardReset();

        /// <summary>True while the pilot is holding the sight.</summary>
        public bool IsEngaged => _engaged;

        /// <summary>How far the highlight has faded in, 0-1. HUD-readable.</summary>
        public float Blend01 => _blend;

        // ---------------- Action ----------------

        /// <summary>
        /// Called on EVERY machine, not just the holder's: <see cref="R_VesselActionHandler"/>
        /// replicates the press through the server before performing it, so this runs on the
        /// owner, the host and every observer. Which channel the resulting highlight goes to is
        /// decided in <see cref="Update"/> by <see cref="IsLocalPilot"/> — deciding it here would
        /// mean a vessel that changed hands mid-hold (the Cellular Duel ownership swap) kept
        /// publishing to the wrong one.
        /// </summary>
        public void Engage(EchoSightActionSO so, IVesselStatus status)
        {
            if (so) _so = so;
            if (status != null) _status = status;
            _engaged = true;
        }

        /// <summary>Also called on every machine — see <see cref="Engage"/>.</summary>
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

            bool local = IsLocalPilot;

            // Ease both ways, on every machine. Nothing snaps into or out of the sight - a rival's
            // mark blooms and fades exactly like your own, because the press and the release both
            // arrive here through the same replicated input channel.
            float rate = Time.deltaTime / Mathf.Max(0.01f, so.TransitionSeconds);
            float target = _engaged ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, rate);

            if (_blend <= 0f)
            {
                StopPublishing();
                return;
            }

            if (!blastEffect || _status == null ||
                !blastEffect.TryResolveBlastVolume(_status, out var volume))
            {
                StopPublishing();
                return;
            }

            float strength = _blend * so.HighlightStrength;

            if (local)
            {
                // Unchanged from before peers existed, deliberately: this is the pilot's own
                // instrument and it must read identically in every match.
                PublishShapeToPeers(volume);
                PrismDestructionSight.PublishLocal(volume, strength);
                _localPublished = true;
                DriveVesselHighlight(volume, strength);
                return;
            }

            // Somebody else is aiming. Their cone's SIZE has to come off the wire - see
            // R_VesselActionHandler.NetEchoSightShape for why this machine cannot work it out.
            // Its apex and axes are re-derived locally every frame from the replica's own
            // transform, so the mark turns with their ship at full frame rate.
            if (!TryApplyReplicatedShape(ref volume) || !TryResolvePeerTint(out var tint))
            {
                StopPublishing();
                return;
            }

            PrismDestructionSight.PublishPeer(_peerSlotId, volume, strength, tint);
            _peerPublished = true;

            // Fed zero rather than skipped so a vessel that changed hands mid-hold - and so stopped
            // being the local pilot's - fades out anything it had marked instead of stranding it.
            DriveVesselHighlight(default, 0f);
        }

        /// <summary>
        /// Retire whatever this executor was showing, on whichever channel it was showing it.
        /// Driven off what was actually PUBLISHED rather than off what this vessel is now, because
        /// ownership can change under a live vessel (the Cellular Duel swap) and the stale channel
        /// is precisely the one nothing else would ever clear.
        ///
        /// The wire is zeroed too — an owner that stops aiming without publishing
        /// <see cref="Vector3.zero"/> leaves every peer drawing the last size it sent, which no
        /// amount of local cleanup on their machines can fix.
        /// </summary>
        void StopPublishing()
        {
            if (_localPublished)
            {
                PrismDestructionSight.ClearLocal();
                _localPublished = false;
            }

            if (_peerPublished)
            {
                PrismDestructionSight.ClearPeer(_peerSlotId);
                _peerPublished = false;
            }

            // Unconditional: it no-ops on any machine that does not own this vessel, so it is
            // correct here without asking again whether this executor is the local pilot's.
            PublishShapeToPeers(default);

            DriveVesselHighlight(default, 0f);
        }

        /// <summary>
        /// The owner publishes the three scalars a peer cannot derive:
        /// <c>(Height, TanCorePerUnit, TanGapePerUnit)</c>. An invalid volume publishes
        /// <see cref="Vector3.zero"/>, which is the "not aiming" sentinel every reader tests.
        ///
        /// Written only when the shape has meaningfully moved, so holding the trigger while the
        /// energy meter creeps costs a few ticks per second rather than one per frame — and a
        /// vessel that never carries this ability never dirties the variable at all.
        /// </summary>
        void PublishShapeToPeers(in BlastVolume volume)
        {
            var handler = _status?.ActionHandler;
            if (!handler || !handler.IsSpawned || !handler.IsOwner) return;

            Vector3 shape = volume.IsValid && volume.Height > 0f
                ? new Vector3(volume.Height, volume.TanCorePerUnit, volume.TanGapePerUnit)
                : Vector3.zero;

            if (!ShapeChanged(_lastPublishedShape, shape)) return;

            _lastPublishedShape = shape;
            handler.NetEchoSightShape.Value = shape;
        }

        /// <summary>
        /// Relative comparison rather than absolute: the three scalars live on wildly different
        /// scales (a height in the thousands of units next to two tangents under 1), so one
        /// absolute epsilon would either spam the network for the tangents or freeze the height.
        /// Going to or from exactly zero always counts — that transition is the sentinel.
        /// </summary>
        static bool ShapeChanged(Vector3 previous, Vector3 next)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = previous[i], b = next[i];
                if ((a == 0f) != (b == 0f)) return true;
                if (Mathf.Abs(a - b) > ShapeRepublishEpsilon * Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Stamp the owner's replicated cone size onto the locally-derived volume. Returns false
        /// while no shape has arrived — the owner is not aiming, or this machine has not received
        /// their first tick yet — and a peer sight that has nothing authoritative to draw draws
        /// NOTHING. Guessing from the replica's own stale elemental levels is what would make the
        /// overlay lie, and a targeting aid that lies is worse than none.
        /// </summary>
        bool TryApplyReplicatedShape(ref BlastVolume volume)
        {
            var handler = _status?.ActionHandler;
            if (!handler || !handler.IsSpawned) return false;

            Vector3 shape = handler.NetEchoSightShape.Value;
            if (shape.x <= 0f) return false;

            volume.Height = shape.x;
            volume.TanCorePerUnit = shape.y;
            volume.TanGapePerUnit = shape.z;
            return true;
        }

        /// <summary>
        /// The Charge level-5 half: pilots standing in the same volume light up.
        ///
        /// LOCAL PILOT ONLY, unlike the prism half — see the class summary. It is fed a zero
        /// strength on every other path rather than skipped, so nothing is ever stranded lit.
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
                vesselHaloScale, vesselHaloIntensity, vesselHaloMinScreenRadius, vesselHaloEnabled);

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
            var status = vessel?.VesselStatus;

            // Player, not just VesselStatus: IVesselStatus.Domain logs an error and falls back to
            // Jade when the pair is not linked yet, and a replica can be alive for a frame or two
            // before ClientPlayerVesselInitializer resolves it. Blue is the platform's "no team"
            // sentinel and GetDomainSignalColor answers white for it, which is a correct-looking
            // neutral mark rather than a wrong team's colour.
            var domain = status?.Player != null ? status.Domain : Domains.Blue;
            return colorSet != null ? colorSet.GetDomainSignalColor(domain) : Color.white;
        }

        /// <summary>
        /// The colour a PEER's mark is drawn in, or false if this machine cannot yet say whose mark
        /// it is.
        ///
        /// A peer mark with no resolved owner is not drawn at all. Falling back to
        /// <see cref="Domains.Blue"/> — the platform's "no team" sentinel — would be wrong twice
        /// over here: Blue is an authored colour rather than a neutral, and after the shader's
        /// desaturation it lands close enough to the local sight's own pale cool cast that a rival's
        /// cone could be mistaken for your own. The window is a frame or two on a freshly spawned
        /// replica, before <c>ClientPlayerVesselInitializer</c> links the pair, and showing nothing
        /// across it is strictly better than showing something that means the wrong thing.
        /// </summary>
        bool TryResolvePeerTint(out Color tint)
        {
            tint = Color.white;
            var status = _status;
            if (status?.Player == null) return false;

            tint = ResolveDomainSignalColor(status.Vessel);
            return true;
        }

        void HardReset()
        {
            _engaged = false;
            _blend = 0f;

            // Both channels, and the wire. A vessel torn down mid-hold must not leave its cone
            // burned into anyone's arena. ClearLocal is called unconditionally here rather than
            // through the _localPublished flag: this also runs at the TOP of Initialize, where a
            // sight left over from the previous occupant of this component is exactly what needs
            // dropping and no flag on this instance can know about it.
            PrismDestructionSight.ClearLocal();
            _localPublished = false;
            if (_peerPublished)
            {
                PrismDestructionSight.ClearPeer(_peerSlotId);
                _peerPublished = false;
            }
            PublishShapeToPeers(default);
            _lastPublishedShape = Vector3.zero;

            // Restores every borrowed brightness immediately. A faded-but-unrestored highlight would
            // leave a rival vessel permanently over-bright with nothing left running to fix it.
            _vesselHighlighter?.ClearAll();
        }
    }
}

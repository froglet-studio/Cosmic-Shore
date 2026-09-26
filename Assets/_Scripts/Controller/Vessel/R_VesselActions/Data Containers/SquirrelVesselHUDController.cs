using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    public sealed class SquirrelVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SquirrelVesselHUDView view;

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;
        [SerializeField] private ScriptableEventString joustCollisionEvent;
        [SerializeField] private ScriptableEventVesselImpactor squirrelCrystalExplosionEvent;
        // The three drift channels (isDrifting / isDoubleDrifting / driftEnded) were removed with
        // the drift icon juice in the 2026-09 element re-cut: the drift is core flight with no
        // element, and its readout was sitting on the card that now carries the Boost Ring. The
        // channels themselves are untouched and still raised by the drift executor.

        [Header("Shared Config")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier;
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;

        [Inject] private GameDataSO gameData;

        private IVesselStatus _vesselStatus;
        private Domains _lastSourceDomain = Domains.Blue;

        // The domain this HUD is currently PAINTED for. default(Domains) is 0, which is not a
        // member of the enum, so the first poll always repaints - there is no value a real domain
        // could hold that would be mistaken for "already painted".
        private Domains _paintedDomain = default;

        // Polled each frame to drive the tube cooldown icon in the freed HUD slot.
        private SquirrelTubeActionExecutor _tubeExecutor;

        // NOTE: this controller used to look up the Sparrow's OverheatingActionExecutor to drive
        // SquirrelVesselHUDView's heat gauge/throb. That component only ever existed on
        // Sparrow.prefab, so the lookup returned null on every Squirrel and the gauge never moved.
        // The view's SetOverheatHeat / JuiceOverheat* are now gone too (2026-09) - an undriven
        // gauge sitting on an ability card is a lie, not a spare part, and the card it sat on is
        // the CHARGE card, which now carries the crystal joust.

        // Single source of truth - the same ColorSet the vessels and prisms use (R5).
        private Color ResolveDomainColor(Domains domain) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.white;

        // The DANGER tier at signal strength, for the Boost Ring icon - a Boost Ring is made of
        // danger prisms. Domain-independent, and read from the same ColorSet as everything else
        // rather than authored on the prefab, so a palette swap moves it. Alpha 0 when the palette
        // authors no danger colour, which the view reads as "keep what you have".
        private Color ResolveDangerColor() =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDangerSignalColor()
                : new Color(0f, 0f, 0f, 0f);

        // The SHIELDED tier's base face at signal strength, for the omni crystal card. Flying a
        // Squirrel through an omni crystal lays a ring of SHIELDED prisms in the pilot's own
        // domain (AOEShieldedRingSpawner), so the icon is that ring's cross-section and this is
        // the colour those prisms will actually be. Domain-KEYED, unlike the danger colour, for
        // the same reason: the ring wears the pilot's colour and the danger rim wears nobody's.
        //
        // Domains.Blue is REFUSED rather than looked up, and that is the whole of why this card
        // shipped the wrong colour. Blue is the platform's "no team / not yet picked" sentinel AND
        // a real row in the palette, whose ShieldedOutsideBlockColor is authored (0, 0, 0.549) -
        // at signal strength a pure hue-240 blue, MORE saturated and with LESS green than Jade's
        // (0.179, 0.489, 1.000). So a domain that had not resolved yet did not render as a failure,
        // it rendered as a plausible team colour, which is the one outcome a palette read must not
        // have (Docs/PALETTE.md §2.8). Alpha 0 = "keep your white", which reads as untinted (§2.4).
        private Color ResolveShieldedColor(Domains domain) =>
            domain != Domains.Blue && gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetShieldedSignalColor(domain)
                : new Color(0f, 0f, 0f, 0f);

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as SquirrelVesselHUDView;

            if (!view) return;

            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
            {
                view.Hide();
                return;
            }

            view.Initialize();
            view.SetDangerTint(ResolveDangerColor());
            RepaintForDomain(vesselStatus.Domain);
            Subscribe();
            PaintFromStatusFallback();

            // The tube executor lives on a child of the vessel; poll it in Update for the
            // cooldown icon. Local user only (this whole branch is gated on IsLocalUser).
            _tubeExecutor = vesselStatus.Vessel?.Transform
                ? vesselStatus.Vessel.Transform.GetComponentInChildren<SquirrelTubeActionExecutor>(true)
                : null;
        }

        private void Update()
        {
            if (!view) return;
            if (_tubeExecutor != null)
                // Fill grows 0 -> 1 as the ability recharges (ready = full + bright).
                view.SetTubeCooldownReady(1f - _tubeExecutor.CooldownRemaining01);

            PushStealReadout();
            PushDomainPalette();
        }

        /// <summary>
        /// Repaints everything on this HUD that wears the pilot's colour: the omni crystal card's
        /// shielded-ring icon, the boost fill and the steal count.
        /// </summary>
        private void RepaintForDomain(Domains domain)
        {
            _paintedDomain = domain;
            view.SetOmniAbilityTint(ResolveShieldedColor(domain));
            view.SetPlayerDomainColor(ResolveDomainColor(domain));
        }

        /// <summary>
        /// Follows the pilot's LIVE domain rather than the one they had when this HUD was built.
        ///
        /// <para>A domain is not decided by the time a vessel spawns: <c>Player.NetDomain</c> is
        /// server-write and initialises to Jade, the owner's own pick arrives later through
        /// <c>RequestSetDomain_ServerRpc</c>, and the match's active set can move a human again at
        /// spawn (<c>NormalizeUnassignedHumans</c>). CLAUDE.md states the rule outright - <i>do not
        /// snapshot domain at component-creation time</i> - and this controller was snapshotting it
        /// twice, so the omni card and the steal count both held whatever was true one frame after
        /// <c>Initialize</c>.</para>
        ///
        /// <para>Polled off the live <c>Player.Domain</c> mirror rather than subscribed to
        /// <c>NetDomain.OnValueChanged</c> for two reasons: this controller already runs an Update
        /// for the tube cooldown and the steal readout, so the poll is free; and a subscription has
        /// to be torn down against a <c>Player</c> reference that a vessel swap replaces underneath
        /// it, which is the asymmetric-binding failure the vessel contract has paid for three times.
        /// The read is gated on <c>Player</c> being present because
        /// <c>IVesselStatus.Domain</c> logs an ERROR when it is not - a per-frame poll through that
        /// getter would turn one missing reference into console spam.</para>
        /// </summary>
        private void PushDomainPalette()
        {
            if (_vesselStatus?.Player == null) return;

            Domains live = _vesselStatus.Domain;
            if (live != _paintedDomain) RepaintForDomain(live);
        }

        /// <summary>
        /// The SPACE card: how far the steal reaches right now, and how much has been taken.
        ///
        /// <para>Both are POLLED rather than pushed, and for different reasons. The reach is a
        /// continuous function of an element level that nothing raises an event for - it moves with
        /// every crystal, every temporary effect and every comeback buff - and the count lives on a
        /// server-write <c>NetworkVariable</c> (<c>RoundStats.n_PrismStolen</c>), so the owner of a
        /// steal learns about its own steal by reading it back. A per-frame read of two fields is
        /// the cheap half of this controller's Update; the view repaints only on a change.</para>
        /// </summary>
        private void PushStealReadout()
        {
            var skimmer = _vesselStatus?.NearFieldSkimmer;
            if (skimmer) view.SetStealReach01(skimmer.ElementalScale01);

            if (gameData != null && _vesselStatus != null &&
                gameData.TryGetRoundStats(_vesselStatus.PlayerName, out var stats) && stats != null)
                view.SetStealCount(stats.PrismStolen);
        }

        private void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser)
                return;

            if (boostChanged != null)
                boostChanged.OnRaised += HandleBoostChanged;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised += HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised += HandleSquirrelCrystalExplosion;
        }

        private void OnDisable()
        {
            if (boostChanged != null)
                boostChanged.OnRaised -= HandleBoostChanged;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised -= HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised -= HandleSquirrelCrystalExplosion;
        }

        private void HandleBoostChanged(BoostChangedPayload payload)
        {
            if (!view) return;

            // Multiplayer: boostChanged is a shared global SOAP channel raised by EVERY
            // vessel (notably the remote owner's per-frame DecayBoost). Ignore raises that
            // didn't originate from our own vessel, else a remote vessel pins this HUD and
            // the local owner's energy bar goes unresponsive.
            if (payload.VesselStatus != null && payload.VesselStatus != _vesselStatus) return;

            float baseMult = boostBaseMultiplier ? boostBaseMultiplier.Value : 1f;
            float maxMult = payload.MaxMultiplier;
            if (maxMult <= 0f)
                maxMult = boostMaxMultiplier ? boostMaxMultiplier.Value : baseMult;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult = Mathf.Max(baseMult, maxMult);

            float mult = Mathf.Max(0f, payload.BoostMultiplier);

            float boost01 = Mathf.InverseLerp(baseMult, maxMult, mult);
            bool isBoosted = mult > baseMult + 0.0001f;
            bool isFull = mult >= maxMult - 0.0001f;

            // Persist source domain across decay frames so the stolen color holds
            Domains effectiveDomain = payload.SourceDomain;
            if (effectiveDomain != Domains.Blue)
            {
                _lastSourceDomain = effectiveDomain;
            }
            else if (isBoosted)
            {
                effectiveDomain = _lastSourceDomain;
            }
            else
            {
                _lastSourceDomain = Domains.Blue;
            }

            bool hasSourceDomain = effectiveDomain != Domains.Blue;
            Color sourceColor = hasSourceDomain ? ResolveDomainColor(effectiveDomain) : Color.white;

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull,
                sourceColor, hasSourceDomain);
        }

        private void HandleJoustCollision(string playerName)
        {
            if (!view) return;
            // Shared global event - only react to our own vessel's joust collisions.
            if (playerName != _vesselStatus.PlayerName) return;

            // Joust and crystal share ONE impact icon; flash it with the joust colour.
            view.JuiceJoustImpact();
        }

        private void PaintFromStatusFallback()
        {
            if (!view || _vesselStatus == null) return;

            float baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult = boostMaxMultiplier != null ? boostMaxMultiplier.Value : 5f;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult = Mathf.Max(baseMult, maxMult);

            float mult = Mathf.Max(0f, _vesselStatus.BoostMultiplier);

            float boost01 = Mathf.InverseLerp(baseMult, maxMult, mult);
            bool isBoosted = mult > baseMult + 0.0001f;
            bool isFull = mult >= maxMult - 0.0001f;

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull,
                Color.white, false);
        }

        private void HandleSquirrelCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view || vesselImpactor.Vessel.VesselStatus.PlayerName != _vesselStatus.PlayerName)
                return;

            view.FlashCrystalSurge();

            // Joust and crystal share ONE impact icon; flash it with the crystal colour.
            view.JuiceCrystalImpact();
        }
    }
}

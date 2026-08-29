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
        [SerializeField] private ScriptableEventNoParam isDrifting;
        [SerializeField] private ScriptableEventNoParam isDoubleDrifting;
        [SerializeField] private ScriptableEventNoParam driftEnded;

        [Header("Shared Config")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier;
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;

        [Inject] private GameDataSO gameData;

        private IVesselStatus _vesselStatus;
        private Domains _lastSourceDomain = Domains.Blue;

        // Polled each frame to drive the tube cooldown icon in the freed HUD slot.
        private SquirrelTubeActionExecutor _tubeExecutor;

        // NOTE: this controller used to look up the Sparrow's OverheatingActionExecutor to drive
        // SquirrelVesselHUDView's heat gauge/throb. That component only ever existed on
        // Sparrow.prefab, so the lookup returned null on every Squirrel and the gauge never moved.
        // It was removed with the Sparrow's overheat mechanic; the view's SetOverheatHeat /
        // JuiceOverheat* remain, currently undriven, for a future Squirrel meter.

        // Single source of truth - the same ColorSet the vessels and prisms use (R5).
        private Color ResolveDomainColor(Domains domain) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.white;

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

            Color playerColor = ResolveDomainColor(vesselStatus.Domain);

            view.Initialize();
            view.SetPlayerDomainColor(playerColor);
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
        }

        private void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser)
                return;

            if (boostChanged != null)
                boostChanged.OnRaised += HandleBoostChanged;
            if (isDrifting != null)
                isDrifting.OnRaised += UpdateDrift;
            if (isDoubleDrifting != null)
                isDoubleDrifting.OnRaised += UpdateDoubleDrift;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised += HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised += HandleSquirrelCrystalExplosion;
            if (driftEnded != null)
                driftEnded.OnRaised += OnDriftEnded;
        }

        private void OnDisable()
        {
            if (boostChanged != null)
                boostChanged.OnRaised -= HandleBoostChanged;
            if (isDrifting != null)
                isDrifting.OnRaised -= UpdateDrift;
            if (isDoubleDrifting != null)
                isDoubleDrifting.OnRaised -= UpdateDoubleDrift;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised -= HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised -= HandleSquirrelCrystalExplosion;
            if (driftEnded != null)
                driftEnded.OnRaised -= OnDriftEnded;
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

        private void UpdateDrift()
        {
            if (!view) return;
            view.JuiceDriftStart(IsDriftingLeft(), isDoubleDrift: false);
        }

        private void UpdateDoubleDrift()
        {
            if (!view || _vesselStatus == null) return;
            view.JuiceDriftStart(IsDriftingLeft(), isDoubleDrift: true);
        }

        private void OnDriftEnded()
        {
            if (!view) return;
            view.JuiceDriftEnd();
        }

        // Drift IS nose-vs-course divergence, so the drift side falls out of the same signed
        // angle the silhouette rotation uses: nose left of the course = drifting left.
        private bool IsDriftingLeft()
        {
            var ship = _vesselStatus?.ShipTransform;
            if (!ship) return false;

            var fwd2 = Vector3.ProjectOnPlane(ship.forward, Vector3.up);
            var course2 = Vector3.ProjectOnPlane(_vesselStatus.Course, Vector3.up);
            if (fwd2.sqrMagnitude < 1e-6f || course2.sqrMagnitude < 1e-6f) return false;

            return Vector3.SignedAngle(course2, fwd2, Vector3.up) < 0f;
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

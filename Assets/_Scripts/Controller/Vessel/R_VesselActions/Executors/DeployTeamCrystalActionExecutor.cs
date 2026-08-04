using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using DG.Tweening;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin's crystal seeding. Hold the trigger and a preview crystal blooms out in front of
    /// the nose; release and it is planted.
    ///
    /// What gets planted is a TEAM crystal — only the pilot's own domain can collect it, exactly
    /// like the crystals Skim Race lays along its track. That gate is structural rather than
    /// conventional: TeamCrystal.prefab drops the base <see cref="OmniCrystalImpactor"/> in favour
    /// of a <see cref="TeamCrystalImpactor"/>, whose <c>IsDomainMatching</c> rejects every vessel
    /// outside the crystal's domain in the impact chain itself. The PREVIEW says so too — it wears
    /// the domain's crystal colours from the moment it appears, because crystal colour is already
    /// the game's language for "who may collect this" (see <see cref="Crystal.ApplyColorSetTint"/>).
    ///
    /// Element → parameter: CHARGE owns this ability. Its multiplier divides the recharge, and its
    /// level-5 upgrade lets the Dolphin carry a second crystal so two can be planted back to back.
    /// The HUD reads <see cref="ChargesAvailable"/> / <see cref="MaxCharges"/> for the slot pips and
    /// <see cref="CooldownRemaining01"/> for the fill.
    /// </summary>
    public sealed class DeployTeamCrystalActionExecutor : ShipActionExecutorBase
    {
        static readonly int Opacity = Shader.PropertyToID("_opacity");

        [Header("Scene Refs")]
        [SerializeField] private Crystal crystalPrefab;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        Crystal _ghostCrystal;
        Vector3 _ghostRestScale = Vector3.one;
        Tween _ghostTween;
        MaterialPropertyBlock _fadeBlock;

        IVesselStatus _status;

        // The SO carries the tuning, but it only reaches us through Begin/Commit. The HUD polls
        // from frame zero, so resolve it lazily off the action handler the first time anything asks.
        DeployTeamCrystalActionSO _so;
        static readonly List<ShipActionSO> s_boundScratch = new();

        // Charge stack. _charges is what the pilot is holding right now; the recharge clock refills
        // it one slot at a time toward MaxCharges. -1 means "not seeded yet" (starts full).
        int _charges = -1;
        float _rechargeEndTime;
        float _activeCooldown;

        // No null guard on the SOAP channel: a missing reference must fail loud, not silently
        // strand the preview past the end of a turn.
        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            End();
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;

            // Fresh vessel, fresh stack. The SO may differ after a class swap, so re-resolve it.
            _charges = -1;
            _activeCooldown = 0f;
            _so = null;
        }

        // ---------------- HUD surface ----------------

        /// <summary>Crystals the pilot is carrying right now.</summary>
        public int ChargesAvailable
        {
            get { Pump(); return Mathf.Max(0, _charges); }
        }

        /// <summary>
        /// How many crystals can be carried at once: one normally, <see cref="DeployTeamCrystalActionSO.UpgradedCharges"/>
        /// once Charge's level-5 upgrade is active.
        /// </summary>
        public int MaxCharges
        {
            get
            {
                var so = ResolveSo();
                if (!so || !IsChargeUpgraded) return 1;
                return Mathf.Max(1, so.UpgradedCharges);
            }
        }

        /// <summary>
        /// Recharge remaining as a 0-1 fraction: 1 the instant a crystal is planted, 0 when the next
        /// slot is ready. Reads 0 whenever the stack is full.
        /// </summary>
        public float CooldownRemaining01
        {
            get
            {
                Pump();
                if (_activeCooldown <= 0f) return 0f;
                return Mathf.Clamp01((_rechargeEndTime - Time.time) / _activeCooldown);
            }
        }

        /// <summary>True when at least one crystal is available to plant.</summary>
        public bool IsReady => ChargesAvailable > 0;

        // ---------------- Action ----------------

        public void Begin(DeployTeamCrystalActionSO so, IVesselStatus status)
        {
            if (so) _so = so;
            if (!so || _ghostCrystal || !crystalPrefab || status?.Vessel?.Transform == null) return;

            // Out of crystals: no preview at all, so the cooldown is legible from the cockpit and
            // not just from the HUD.
            if (ChargesAvailable <= 0) return;

            _ghostCrystal = Instantiate(crystalPrefab, status.Vessel.Transform);
            _ghostCrystal.transform.localPosition = new Vector3(0f, 0f, so.ForwardOffset);
            _ghostCrystal.transform.localRotation = Quaternion.identity;

            PrepareGhost(_ghostCrystal, so, status.Domain);
            BloomGhost(so);
        }

        public void Commit(DeployTeamCrystalActionSO so, IVesselStatus status)
        {
            if (so) _so = so;
            if (!_ghostCrystal) return;

            var crystal = _ghostCrystal;
            _ghostCrystal = null;

            // The bloom may still be running, and Crystal.Start derives crystalValue from world
            // scale — so settle the transform before the crystal is allowed to wake up.
            _ghostTween?.Kill();
            _ghostTween = null;
            crystal.transform.localScale = _ghostRestScale;

            crystal.transform.SetParent(null, true);

            ActivateCrystal(crystal, status);
            SpendCharge();
        }

        void End()
        {
            if (!_ghostCrystal) return;

            var crystal = _ghostCrystal;
            _ghostCrystal = null;

            _ghostTween?.Kill();
            _ghostTween = null;

            float duration = _so ? _so.PreviewWitherDuration : 0f;

            // On teardown there are no frames left to animate in — the object is going away with
            // the scene either way.
            if (duration <= 0f || !isActiveAndEnabled)
            {
                Destroy(crystal.gameObject);
                return;
            }

            // Continuity of existence: an abandoned preview withers away, it does not blink out.
            crystal.transform.DOScale(Vector3.zero, duration)
                .SetEase(Ease.InBack)
                .SetLink(crystal.gameObject)
                .OnComplete(() => { if (crystal) Destroy(crystal.gameObject); });
        }

        void OnTurnEndOfMiniGame() => End();

        // ---------------- Ghost ----------------

        void PrepareGhost(Crystal cr, DeployTeamCrystalActionSO so, Domains domain)
        {
            cr.enabled = false;
            foreach (var col in cr.GetComponentsInChildren<Collider>(true)) col.enabled = false;

            // The preview wears the pilot's DOMAIN colours rather than the neutral pickup lime,
            // because what is being previewed is a crystal only this domain will be able to
            // collect — and colour is how every other crystal in the game says that.
            cr.ApplyDomainPreview(domain);

            // Half-lit so it still reads as "not planted yet". Driven through a
            // MaterialPropertyBlock over the shared material — renderer.material would clone (and
            // leak) one material per preview and break batching. GetPropertyBlock preserves the
            // domain tint applied just above.
            _fadeBlock ??= new MaterialPropertyBlock();
            foreach (var fade in cr.GetComponentsInChildren<FadeIn>(true))
            {
                fade.enabled = false;
                if (!fade.TryGetComponent<Renderer>(out var r)) continue;
                r.GetPropertyBlock(_fadeBlock);
                _fadeBlock.SetFloat(Opacity, so.FadeValue);
                r.SetPropertyBlock(_fadeBlock);
            }
        }

        void BloomGhost(DeployTeamCrystalActionSO so)
        {
            var t = _ghostCrystal.transform;
            _ghostRestScale = t.localScale;

            // Continuity of existence: nothing pops into being — not even a preview.
            _ghostTween?.Kill();
            t.localScale = Vector3.zero;
            _ghostTween = t.DOScale(_ghostRestScale, so.PreviewBloomDuration)
                .SetEase(Ease.OutBack)
                .SetLink(_ghostCrystal.gameObject);
        }

        void ActivateCrystal(Crystal cr, IVesselStatus status)
        {
            // Domain is stamped BEFORE activation so the crystal settles straight into its team
            // material (Crystal.ResolveActivationMaterial) instead of lerping through the neutral
            // free-for-all look on its way there.
            cr.ownDomain = status.Domain;
            foreach (var col in cr.GetComponentsInChildren<Collider>(true)) col.enabled = true;
            foreach (var fade in cr.GetComponentsInChildren<FadeIn>(true)) fade.enabled = true;
            cr.enabled = true;
            cr.ActivateCrystal();
        }

        // ---------------- Charges ----------------

        bool IsChargeUpgraded
        {
            get
            {
                var handler = _status?.ElementalAbilityHandler;
                return handler && handler.IsUpgradeActive(Element.Charge);
            }
        }

        /// <summary>
        /// Seconds to recharge one slot right now. Element → parameter (Charge → how soon the next
        /// crystal is ready): anchored at exactly 1x at the resting level, so the authored cooldown
        /// is what a fresh pilot feels, and floored by MinCooldown so it never becomes free.
        ///
        /// Scaled from the SO's OWN authored multiplier rather than the map's generic one, because
        /// ChargeBoostActionExecutor already consumes the generic Charge multiplier for the boost
        /// peak — reading it here too would drive two unrelated parameters off one number.
        /// </summary>
        float CurrentCooldown()
        {
            var so = ResolveSo();
            if (!so) return 0f;

            float mult = ElementalScaling.Multiplier(_status, Element.Charge,
                so.CooldownMultiplierAtFullCharge, so.MinCooldownMultiplier);
            return Mathf.Max(so.MinCooldown, so.Cooldown * mult);
        }

        /// <summary>
        /// Advances the recharge clock lazily — called from every accessor and from the action, so
        /// the executor costs nothing per frame on the vessels nobody is looking at (every AI one).
        /// </summary>
        void Pump()
        {
            int max = MaxCharges;

            if (_charges < 0) // first look this vessel: the stack starts full
            {
                _charges = max;
                _activeCooldown = 0f;
                return;
            }

            if (_charges > max) _charges = max; // the level-5 upgrade lapsed
            if (_charges >= max)
            {
                _activeCooldown = 0f;
                return;
            }

            float cooldown = _activeCooldown > 0f ? _activeCooldown : CurrentCooldown();
            if (cooldown <= 0f) // unconfigured cooldown: treat the ability as always ready
            {
                _charges = max;
                _activeCooldown = 0f;
                return;
            }

            // A slot opened with no clock running (the upgrade just landed) — start one.
            if (_activeCooldown <= 0f)
            {
                _activeCooldown = cooldown;
                _rechargeEndTime = Time.time + cooldown;
                return;
            }

            while (_charges < max && Time.time >= _rechargeEndTime)
            {
                _charges++;
                if (_charges < max) _rechargeEndTime += cooldown;
            }

            if (_charges >= max) _activeCooldown = 0f;
        }

        void SpendCharge()
        {
            Pump();
            _charges = Mathf.Max(0, _charges - 1);

            if (_charges >= MaxCharges) return;
            if (_activeCooldown > 0f) return; // a clock is already ticking toward the next slot

            _activeCooldown = CurrentCooldown();
            _rechargeEndTime = Time.time + _activeCooldown;
        }

        /// <summary>
        /// Finds this vessel's crystal action among its own bindings. The action handler builds its
        /// maps AFTER initialising executors, so this cannot happen in Initialize — it runs on the
        /// first access that beats the first deploy (i.e. the HUD's).
        ///
        /// It gives up only on SUCCESS, never on the first attempt. R_VesselActionHandler.Initialize
        /// calls InitializeAll on the executors BEFORE it populates the binding maps, so an early
        /// query lands in a window where the maps are still empty — latching there would pin _so
        /// null for the life of the vessel and leave the HUD reporting "always ready".
        /// </summary>
        DeployTeamCrystalActionSO ResolveSo()
        {
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

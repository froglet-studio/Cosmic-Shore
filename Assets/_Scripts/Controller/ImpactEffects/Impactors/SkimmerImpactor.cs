using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    public class SkimmerImpactor : ImpactorBase
    {
        [FormerlySerializedAs("shipSkimmerEffectsSO")]
        [Header("Effect lists")]
        // [SerializeField] VesselSkimmerEffectsSO[] vesselSkimmerEffectsSO;
        // [SerializeField] SkimmerPrismEffectSO[] skimmerPrismEffectsSO;
        // [SerializeField] SkimmerCrystalEffectSO[] skimmerCrystalEffectsSO;
        [SerializeField]
        private SkimmerImpactorDataContainerSO skimmerImpactorDataContainer;

        /// <summary>
        /// Effect container for this skimmer - exposed so the vessel-level confirmed-joust
        /// dispatch (VesselImpactor.ExecuteJoustImpact) can locate the joust effect config
        /// wired to this vessel without duplicating the reference.
        /// </summary>
        public SkimmerImpactorDataContainerSO EffectContainer => skimmerImpactorDataContainer;

        //[Header("Block-Stay effects (tick while skimming)")] [SerializeField]
        //SkimmerPrismEffectSO[] skimmerPrismStayEffectsSO; // TODO -> Add to the container

        [Header("Refs")] [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;
        public override Domains OwnDomain => Skimmer.Domain;
        // Null-safe: the shell-contact tier polls this every frame for registered
        // probes (not just inside trigger callbacks), so an unwired skimmer ref
        // must read as uninitialized rather than throw.
        protected override bool isInitialized => skimmer != null && skimmer.IsInitialized;

        // runtime state (moved from Skimmer)
        readonly Dictionary<string, float> _skimStartTimes = new();

        // Prisms currently inside this skimmer's BOX trigger (shell-owned contacts are
        // tracked by PrismShellContactManager instead). Maintained on enter/exit so the
        // Rhino energy sword can, on its energize rising edge, re-run its prism effects
        // against a prism that was already overlapping the blade before it ignited (no
        // fresh OnTriggerEnter fires for it). Generic + cheap; only the Rhino path calls
        // ReapplyPrismEffectsToOverlapping.
        readonly HashSet<PrismImpactor> _overlappingPrisms = new();
        readonly List<PrismImpactor> _reapplyBuffer = new();
        //private int ActivelySkimmingBlockCount;
        //[HideInInspector]
        public float CombinedWeight; // exposed for effects that need it

        // ------------------------------------------------------------------
        // Trigger callbacks moved here

        //float scale;
        public float SqrSweetSpot;
        //float sigma;

        // Cached trigger sphere so the skim feel can scale by how close a prism passed to the
        // skimmer centre without a per-impact GetComponent on a dense-trail hot path.
        SphereCollider _sphereCollider;
        bool _sphereLookedUp;

        /// <summary>
        /// World-space radius of the skimmer's trigger sphere. Tracks the runtime SPACE-reach
        /// resize via lossyScale; falls back to half the scale if no sphere collider is present.
        /// </summary>
        public float SphereWorldRadius
        {
            get
            {
                if (!_sphereLookedUp)
                {
                    _sphereLookedUp = true;
                    TryGetComponent(out _sphereCollider);
                }
                float lossy = transform.lossyScale.x;
                return _sphereCollider != null ? _sphereCollider.radius * lossy : lossy * 0.5f;
            }
        }

        //float minMaturePrismSqrDistance;
        //Prism minMaturePrism;
        //PrismImpactor minPrismImpactor;

        //private void Start()
        //{

        //    scale = skimmer.transform.localScale.x;
        //    SqrSweetSpot = scale * scale / 16f;
        //    sigma = SqrSweetSpot / 2.355f;
        //}

        // Shell-tier probes: this skimmer's own colliders (the sphere; on the Rhino
        // also the sword capsule) measured against shielded prisms' analytic shells
        // by PrismShellContactManager. The component set is cached once — world
        // poses are re-read every frame, so runtime scale drivers need no events.
        Collider[] _probeColliders;

        void OnEnable()
        {
            _probeColliders ??= GetComponents<Collider>();
            PrismShellContactManager.RegisterProbeOwner(this, _probeColliders);
        }

        void OnDisable()
        {
            PrismShellContactManager.UnregisterProbeOwner(this);
            // Disabled colliders fire no OnTriggerExit, so the overlap set would go stale.
            _overlappingPrisms.Clear();
        }

        /// <summary>
        /// Re-runs this skimmer's prism effects against every prism currently inside its
        /// box trigger. The Rhino energy sword calls this on its ENERGIZE rising edge: a
        /// prism resting against the blade when it ignites sees no fresh OnTriggerEnter,
        /// so the standing overlap is re-dispatched through the same effect chain (its
        /// shell-tier counterpart is PrismShellContactManager.RedispatchPairsForOwner).
        /// Shell-owned prisms are skipped here for exact parity with AcceptImpactee's
        /// suppression — the shell tier owns those pairs.
        /// </summary>
        public void ReapplyPrismEffectsToOverlapping()
        {
            if (!isInitialized || _overlappingPrisms.Count == 0) return;
            var esp = skimmerImpactorDataContainer.SkimmerPrismEffects;
            if (!DoesEffectExist(esp)) return;

            _reapplyBuffer.Clear();
            _reapplyBuffer.AddRange(_overlappingPrisms);
            for (int i = 0; i < _reapplyBuffer.Count; i++)
            {
                var prismImpactor = _reapplyBuffer[i];
                if (!prismImpactor || prismImpactor.Prism == null || prismImpactor.Prism.destroyed)
                {
                    _overlappingPrisms.Remove(prismImpactor);
                    continue;
                }
                if (PrismShellContactManager.ShellOwnsContact(prismImpactor.Prism))
                    continue;
                for (int e = 0; e < esp.Length; e++)
                {
                    if (IsEffectSlotEmpty(esp[e], skimmerImpactorDataContainer,
                            nameof(SkimmerImpactorDataContainerSO.SkimmerPrismEffects), e))
                        continue;
                    esp[e].Execute(this, prismImpactor);
                }
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (!isInitialized)
                return;
            
            if (skimmer.AllowVaccumCrystal && other.TryGetComponent<Crystal>(out var crystal)
                && !crystal.IsEmbedded) // a living lifeform's heart is never vacuumed out of its body
            {
                // NEW -> Vaccum logic transferred from skimmer to crystal, to reduce crystal dependency
                crystal.Vacuum(transform.position, skimmer.VaccumAmount);
                // skimmer.TryVacuumCrystal(crystal);
                // no return; a Crystal may also have a TrailBlock? (unlikely, safe to continue)
            }

            //// TrailBlock: compute combined weight & run stay effects
            //if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            //var prism = prismImpactor.Prism;
            //if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;

            //// ensure we started skimming
            //StartSkimIfNeeded(prism.ownerID);

            //// choose “mature & nearest” block per your old logic
            //// if (Time.time - prism.prismProperties.TimeCreated <= 4f) return;
            // if ((Time.time - prism.prismProperties.TimeCreated) < 0.25f) return;
            
            //float sqrDistance = (skimmer.transform.position - other.transform.position).sqrMagnitude;
            
            //minMaturePrismSqrDistance = Mathf.Min(minMaturePrismSqrDistance, sqrDistance);
            


            //if (sqrDistance != minMaturePrismSqrDistance) return;

            //minMaturePrism = prism;
            //minPrismImpactor = prismImpactor;
        }

        //private void FixedUpdate()
        //{
        //    if (minMaturePrism)
        //    {
        //        float distanceWeight = Skimmer.ComputeGaussian(minMaturePrismSqrDistance, SqrSweetSpot, sigma);
        //        float directionWeight = Vector3.Dot(skimmer.VesselStatus.Transform.forward, minMaturePrism.transform.forward);

        //        ExecuteBlockStayEffects(distanceWeight * Mathf.Abs(directionWeight), minPrismImpactor);
        //    }
        //    minMaturePrism = null;
        //    minPrismImpactor = null;
        //    minMaturePrismSqrDistance = Mathf.Infinity;
        //}

        void OnTriggerExit(Collider other)
        {
            if (!isInitialized)
                return;

            if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            var prism = prismImpactor.Prism;

            // The box-overlap set tracks BOX residency only, so a box exit always drops
            // the entry — even when the shell tier owns the prism's contact semantics
            // (e.g. a prism shielded mid-overlap), or the record leaks forever.
            _overlappingPrisms.Remove(prismImpactor);

            // Symmetric with the enter-side suppression: while the shell tier owns
            // this prism's contact, exiting the (smaller) box must not tear down
            // the skim bookkeeping the shell contact added - the shell tier's own
            // exit (NotifyShellContactExit) handles it.
            if (PrismShellContactManager.ShellOwnsContact(prism))
                return;
            if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;

            if (!_skimStartTimes.Remove(prism.ownerID)) return;

            //ActivelySkimmingBlockCount = Mathf.Max(0, ActivelySkimmingBlockCount - 1);

            // if (ActivelySkimmingBlockCount < 1)
            //     ExecuteBlockStayEffects(0f, prismImpactor); // stop effects when no longer skimming anything
        }

        // ------------------------------------------------------------------

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (!isInitialized)
                return;
            
            switch (impactee)
            {
                case VesselImpactor shipImpactor:
                    // A skimmer never impacts its own vessel. The Rhino's sword capsule
                    // permanently overlaps its own hull, so without this guard the full
                    // victim-effect chain ran against the pilot themselves (muting their
                    // own RightStickAction and spamming impact SFX).
                    if (ReferenceEquals(shipImpactor.Vessel?.VesselStatus, skimmer.VesselStatus)) return;
                    var evs = skimmerImpactorDataContainer.VesselSkimmerEffects;
                    if (!DoesEffectExist(evs)) return;
                    for (int i = 0; i < evs.Length; i++)
                    {
                        if (IsEffectSlotEmpty(evs[i], skimmerImpactorDataContainer,
                                nameof(SkimmerImpactorDataContainerSO.VesselSkimmerEffects), i))
                            continue;
                        evs[i].Execute(shipImpactor, this);
                    }

                    skimmer.ExecuteImpactOnShip(shipImpactor.Vessel); // secondary call
                    break;

                case PrismImpactor prismImpactor:
                    var prism = prismImpactor.Prism;
                    // A pilot does not skim the ribbon still coming out of their own ship.
                    // OWNER-scoped and time-boxed, never domain-scoped: a teammate's trail and
                    // this pilot's own older trail both skim normally, so a pursuing Squirrel
                    // still farms someone else's fresh ribbon all the way into joust range.
                    // Ahead of the shell guard so a shielded self-prism is suppressed on both
                    // dispatch tiers. See SelfTrailContactConfigSO.
                    if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, skimmer.VesselStatus))
                        return;
                    // While a prism's engaged shell owns contact, the shell tier
                    // (PrismShellContactManager) dispatches this pair at the visible
                    // shell surface — the box trigger must not also dispatch it at
                    // bare-prism reach, or every shielded hit would double-fire.
                    if (!IsShellDispatch && PrismShellContactManager.ShellOwnsContact(prism))
                        return;
                    // Track genuine BOX overlaps only (a shell dispatch is not box
                    // residency; its lifecycle belongs to the shell tier's pair map).
                    if (!IsShellDispatch)
                        _overlappingPrisms.Add(prismImpactor);
                    var esp = skimmerImpactorDataContainer.SkimmerPrismEffects;
                    skimmer.ExecuteImpactOnPrism(prism); // secondary call (booster viz, etc.)
                    if (!DoesEffectExist(esp)) return;

                    for (int i = 0; i < esp.Length; i++)
                    {
                        if (IsEffectSlotEmpty(esp[i], skimmerImpactorDataContainer,
                                nameof(SkimmerImpactorDataContainerSO.SkimmerPrismEffects), i))
                            continue;
                        esp[i].Execute(this, prismImpactor);
                    }


                    if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain)
                        return;
                    StartSkimIfNeeded(prism.ownerID);

                    break;

                case ElementalCrystalImpactor elementalCrystalImpactor:
                    // Mirror the crystal side's collectability guards (ElementalCrystalImpactor.
                    // AcceptImpactee): a living lifeform's embedded heart enters the trigger but is
                    // never skim-collectable — without this gate the skimmer's crystal effects
                    // (e.g. the Rhino sword's crystal burst) would fire on it, repeatedly, since
                    // the heart's collider never gets disabled by a collection.
                    var crystal = elementalCrystalImpactor.Crystal;
                    if (crystal == null || crystal.IsEmbedded || crystal.IsExploding) return;

                    var esc = skimmerImpactorDataContainer.SkimmerCrystalEffects;
                    if (!DoesEffectExist(esc)) return;
                    for (int i = 0; i < esc.Length; i++)
                    {
                        if (IsEffectSlotEmpty(esc[i], skimmerImpactorDataContainer,
                                nameof(SkimmerImpactorDataContainerSO.SkimmerCrystalEffects), i))
                            continue;
                        esc[i].Execute(this, elementalCrystalImpactor);
                    }

                    break;
            }
        }

        /// <summary>
        /// Shell-contact exit — mirrors the prism branch of OnTriggerExit (the skim
        /// bookkeeping removal) for contacts that lived on the analytic shell tier
        /// instead of the box trigger.
        /// </summary>
        internal override void NotifyShellContactExit(PrismImpactor prismImpactor)
        {
            if (!isInitialized)
                return;
            var prism = prismImpactor != null ? prismImpactor.Prism : null;
            if (prism == null)
                return;
            if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain)
                return;
            _skimStartTimes.Remove(prism.ownerID);
        }

        // ------------------------------------------------------------------
        // Internals

        //void ExecuteBlockStayEffects(float combinedWeight, PrismImpactor prismImpactor)
        //{
        //    CombinedWeight = combinedWeight;

        //    if (skimmerPrismStayEffectsSO == null || skimmerPrismStayEffectsSO.Length == 0)
        //        return;

        //    // Run as self-effects. Effects can cast `impactor` to SkimmerImpactor and read `CombinedWeight`.
        //    foreach (var t in skimmerPrismStayEffectsSO)
        //        t?.Execute(this, prismImpactor);
        //}

        void StartSkimIfNeeded(string ownerId)
        {
            if (_skimStartTimes.ContainsKey(ownerId)) return;
            _skimStartTimes.Add(ownerId, Time.time);
            //ActivelySkimmingBlockCount++;
        }
    }
}
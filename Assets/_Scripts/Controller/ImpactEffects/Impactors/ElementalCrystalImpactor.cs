using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        [Header("Collection Effects")]
        [Tooltip("Effects granted to the skimming vessel when this crystal is collected " +
                 "(e.g. element level powerup scaled by crystal size).")]
        [SerializeField] SkimmerCrystalEffectSO[] elementalCrystalShipEffects;

        [Header("Capture")]
        [Tooltip("Optional per-crystal override of the capture feel. Leave empty - the project's " +
                 "single Resources/CrystalCaptureConfig is the source of truth, and per-prefab " +
                 "timing is exactly how the old capture drifted to 1s on some lifeforms and 3s " +
                 "on others.")]
        [SerializeField] CrystalCaptureConfigSO captureConfigOverride;

        bool isImpacting;
        private bool _hasBeenCollected;
        public static event Action<string> OnCrystalCollected;

        /// <summary>
        /// True once a skimmer has collected this crystal (it is now flying to the vessel and will
        /// self-destroy). Runtime owners - e.g. the conveyor toy's microscenes - read this so they
        /// stop managing a departing crystal as a live resident.
        /// </summary>
        public bool HasBeenCollected => _hasBeenCollected;

        /// <summary>
        /// Wires collection effects on a runtime-added impactor (lifeform prefabs author these as
        /// inspector overrides; runtime spawns - e.g. the conveyor toy's pickups - cannot).
        /// </summary>
        internal void SetCollectionEffects(SkimmerCrystalEffectSO[] effects) => elementalCrystalShipEffects = effects;

        /// <summary>
        /// The authored collection effects, readable so runtime crystal replacement (e.g.
        /// LifeFormCrystal's element-contract provisioning) can carry them onto the new crystal.
        /// </summary>
        internal SkimmerCrystalEffectSO[] CollectionEffects => elementalCrystalShipEffects;

        CrystalCaptureConfigSO CaptureConfig =>
            captureConfigOverride ? captureConfigOverride : CrystalCaptureConfigSO.Load();

        void OnEnable()
        {
            _hasBeenCollected = false;
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            // A living lifeform's embedded heart is joustable (vessel-side chain) but never
            // skim-collectable - it only becomes a pickup once death drops it (ActivateCrystal).
            if (Crystal && Crystal.IsEmbedded) return;
            if (impactee is not SkimmerImpactor skimmerImpactor) return;

            CollectBy(skimmerImpactor);
        }

        /// <summary>
        /// Awards this crystal to <paramref name="skimmerImpactor"/>'s vessel WITHOUT a skim
        /// contact - the auto-collect entry point, and the same chain a skim runs (collection
        /// effects, capture flourish into the vessel, spend).
        ///
        /// Its one caller is the Squirrel's Crystal Joust: the jouster reached into a lifeform
        /// and took its heart, so the crystal the kill just freed leaves with that pilot rather
        /// than sitting in the water for whoever passes next. The starvation death is the
        /// deliberate contrast - there nobody took it, so it drops as an ordinary pickup any
        /// vessel can collect (Docs/ECOSYSTEM.md §26).
        ///
        /// Idempotent; returns false when the crystal is already collected, mid-impact, or
        /// exploding.
        /// </summary>
        public bool CollectBy(SkimmerImpactor skimmerImpactor)
        {
            if (!skimmerImpactor) return false;
            if (_hasBeenCollected || isImpacting) return false;
            if (Crystal == null || Crystal.IsExploding) return false;

            isImpacting = true;
            _hasBeenCollected = true;

            var col = Crystal.GetComponent<Collider>();
            if (col) col.enabled = false;

            WaitForImpact().Forget();

            if (DoesEffectExist(elementalCrystalShipEffects))
            {
                foreach (var effect in elementalCrystalShipEffects)
                    effect.Execute(skimmerImpactor, this);
            }

            // Scoring is credited at CONTACT, not at the end of the flourish: the capture is a
            // visual, and a mode's objective must never wait on one.
            var vesselStatus = skimmerImpactor.Skimmer ? skimmerImpactor.Skimmer.VesselStatus : null;
            if (vesselStatus != null) OnCrystalCollected?.Invoke(vesselStatus.PlayerName);

            RunCapture(vesselStatus).Forget();
            return true;
        }

        /// <summary>
        /// The capture flourish: snatch → suction → absorb, then the element's spent-crystal husk
        /// sprays into the vessel's wake and the crystal is spent. Feel lives entirely in
        /// <see cref="CrystalCaptureConfigSO"/>; this method owns only the geometry.
        /// </summary>
        async UniTaskVoid RunCapture(IVesselStatus vesselStatus)
        {
            var crystal = Crystal;
            if (crystal == null) return;

            var cfg = CaptureConfig;
            var vesselTransform = vesselStatus?.VesselTransformer ? vesselStatus.VesselTransformer.transform : null;

            // The idle tumble is ours for the duration - two writers on one rotation would fight.
            var spinners = crystal.GetComponentsInChildren<CosmicShore.UI.JustRotate>(true);
            foreach (var spinner in spinners) spinner.canRotate = false;

            // And the tint block is ours: a collectability cross-fade still in flight (a jousted
            // heart is freed and auto-collected in the same beat, so it always is) would fight the
            // flare frame by frame. This freezes it at the displayed colour and flares from there.
            crystal.BeginCaptureVisual();

            // The Space crystal's blendshape pulse rides ALONG the flight now instead of being
            // awaited after it (that tail was 0.6s of a full-size crystal parked on the hull).
            PlayCollectAnimators(crystal);

            var crystalTransform = crystal.transform;
            Vector3 baseScale = crystalTransform.localScale;
            Quaternion baseRotation = crystalTransform.rotation;
            float radius = Mathf.Max(0.01f, crystalTransform.lossyScale.x);
            Vector3 spinAxis = UnityEngine.Random.onUnitSphere;

            Vector3 start = crystalTransform.position;
            Vector3 target = vesselTransform ? vesselTransform.position : start;
            Vector3 toVessel = target - start;

            // Recoil straight back from the vessel; a crystal captured exactly on the hull has no
            // direction to recoil along, so pick one.
            Vector3 away = toVessel.sqrMagnitude > 1e-4f ? -toVessel.normalized : UnityEngine.Random.onUnitSphere;
            Vector3 anchor = start + away * (cfg.SnatchRecoilRadii * radius);

            // A stable lateral axis for the arc, so the swing does not wobble frame to frame.
            Vector3 arcAxis = Vector3.Cross(toVessel.sqrMagnitude > 1e-4f ? toVessel : away,
                                            UnityEngine.Random.onUnitSphere);
            arcAxis = arcAxis.sqrMagnitude > 1e-4f ? arcAxis.normalized : Vector3.up;

            bool burstFired = false;
            float elapsed = 0f;
            float total = cfg.TotalDuration;

            while (elapsed < total)
            {
                if (crystal == null) return;

                var phase = cfg.ResolvePhase(elapsed, out float u);
                if (phase == CrystalCaptureConfigSO.Phase.Done) break;

                // Track the vessel LIVE - it is moving fast, and the whole reason the old capture
                // read as a drag is that it lerped toward a stale point over three seconds.
                if (vesselTransform) target = vesselTransform.position;

                Vector3 position;
                switch (phase)
                {
                    case CrystalCaptureConfigSO.Phase.Snatch:
                        position = Vector3.LerpUnclamped(start, anchor, CrystalCaptureConfigSO.SnatchProgress01(u));
                        break;

                    case CrystalCaptureConfigSO.Phase.Suction:
                    {
                        float flight = cfg.FlightProgress01(u);
                        position = Vector3.LerpUnclamped(anchor, target, flight)
                                 + arcAxis * (CrystalCaptureConfigSO.ArcOffset01(u) * cfg.SuctionArcRadii * radius);
                        break;
                    }

                    default: // Absorb - ride the hull.
                        if (!burstFired)
                        {
                            burstFired = true;
                            FireHuskBurst(crystal, cfg, vesselStatus, baseScale);
                        }
                        position = target;
                        break;
                }

                crystalTransform.SetPositionAndRotation(
                    position,
                    baseRotation * Quaternion.AngleAxis(cfg.SpinDegrees(phase, u), spinAxis));
                crystalTransform.localScale = baseScale * cfg.ScaleMultiplier(phase, u);

                crystal.ApplyCaptureVisual(cfg.FlareMultiplier(phase, u),
                                           CrystalCaptureConfigSO.Opacity(phase, u));

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (crystal == null) return;

            // A zero-length absorb still owes the payoff.
            if (!burstFired) FireHuskBurst(crystal, cfg, vesselStatus, baseScale);

            crystal.DestroyCrystal();
        }

        /// <summary>
        /// Sprays this element's spent-crystal husk into the collecting vessel's wake - the same
        /// burst (and the same CrystalCollect SFX) an omni crystal pickup plays. Before this, an
        /// elemental crystal simply stopped existing at the end of its flight.
        ///
        /// The husk is sized off the crystal's PRE-capture scale, not its live one: by the time
        /// this fires the flourish has already shrunk the crystal into the hull, and the payoff
        /// should read as the crystal that was picked up.
        /// </summary>
        static void FireHuskBurst(Crystal crystal, CrystalCaptureConfigSO cfg, IVesselStatus vesselStatus,
                                  Vector3 preCaptureLocalScale)
        {
            if (!cfg.SpawnHuskBurst || crystal == null || crystal.IsExploding) return;

            var parent = crystal.transform.parent;
            var huskScale = parent ? Vector3.Scale(parent.lossyScale, preCaptureLocalScale) : preCaptureLocalScale;

            crystal.Explode(new Crystal.ExplodeParams
            {
                Course = vesselStatus?.Course ?? crystal.transform.forward,
                Speed = vesselStatus?.Speed ?? 0f,
                PlayerName = vesselStatus != null ? vesselStatus.PlayerName : string.Empty,
            }, huskScale);
        }

        static void PlayCollectAnimators(Crystal crystal)
        {
            var crystalModels = crystal.CrystalModels;
            if (crystalModels == null) return;

            foreach (var crystalModelData in crystalModels)
            {
                // Unity's implicit bool, not ?. - a destroyed animator is FAKE-null, which the
                // null-conditional happily calls through.
                if (crystalModelData?.spaceCrystalAnimator)
                    crystalModelData.spaceCrystalAnimator.PlayCollect();
            }
        }

        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            isImpacting = false;
        }
    }
}

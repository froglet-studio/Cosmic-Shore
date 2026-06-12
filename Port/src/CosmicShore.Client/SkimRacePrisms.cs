using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Client
{
    /// <summary>
    /// The race's prism factory — the client-layer stand-in for the unported
    /// PrismFactory/pool (Assets/_Scripts/Controller/Prisms/PrismFactory.cs). It answers
    /// the REAL <see cref="VesselPrismController"/> spawn channel
    /// (<see cref="PrismEventChannelWithReturnSO"/>) by assembling the genuine prism
    /// GameObject family — MeshRenderer + BoxCollider + MaterialPropertyAnimator +
    /// PrismScaleAnimator + PrismTeamManager + PrismStateManager + Prism + the contact rig
    /// (PrismImpactor + ImpactCollider) — exactly the PrismTestRig/V15 layout, so every
    /// trail block in the race is a real <see cref="Prism"/> flowing through the real
    /// trigger pipeline.
    ///
    /// Prisms are conserved mass: the factory never decays, caps, or culls them. The only
    /// sink is <see cref="DespawnAll"/> — the race-restart active force.
    /// </summary>
    public sealed class SkimRacePrismFactory
    {
        /// <summary>Wire this into VesselPrismController._onPrismSpawnedEventChannel.</summary>
        public PrismEventChannelWithReturnSO SpawnChannel { get; }

        readonly ThemeManagerDataContainerSO _theme;
        readonly ScriptableEventPrismStats _onCreated;
        readonly ScriptableEventPrismStats _onDestroyed;
        readonly ScriptableEventPrismStats _onRestored;
        readonly ScriptableEventPrismStats _onStolen;
        readonly ScriptableEventPrismStats _onVolumeModified;
        readonly PrismEventChannelWithReturnSO _onBlockImpacted;
        readonly List<GameObject> _live = new();
        int _spawnCount;

        public int TotalSpawned => _spawnCount;
        public int LiveCount => _live.Count;

        public SkimRacePrismFactory(ThemeManagerDataContainerSO theme)
        {
            _theme = theme;
            SpawnChannel = ScriptableObject.CreateInstance<PrismEventChannelWithReturnSO>();
            _onCreated = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
            _onDestroyed = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
            _onRestored = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
            _onStolen = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
            _onVolumeModified = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
            _onBlockImpacted = ScriptableObject.CreateInstance<PrismEventChannelWithReturnSO>();
            SpawnChannel.OnEventReturn += HandleSpawn;
        }

        PrismReturnEventData HandleSpawn(PrismEventData data)
        {
            var go = new GameObject($"prism-{_spawnCount++}");
            go.SetActive(false); // configure-before-activation (PrismTestRig pattern)
            go.transform.position = data.SpawnPosition;
            go.transform.rotation = data.Rotation;

            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();

            var materialAnimator = go.AddComponent<MaterialPropertyAnimator>();
            SkimRaceFactory.SetPrivateField(materialAnimator, "_themeManagerData", _theme);

            var scaleAnimator = go.AddComponent<PrismScaleAnimator>();
            SkimRaceFactory.SetPrivateField(scaleAnimator, "onPrismVolumeModified", _onVolumeModified);

            var teamManager = go.AddComponent<PrismTeamManager>();
            SkimRaceFactory.SetPrivateField(teamManager, "_themeManagerData", _theme);
            SkimRaceFactory.SetPrivateField(teamManager, "onPrismStolen", _onStolen);

            var stateManager = go.AddComponent<PrismStateManager>();
            SkimRaceFactory.SetPrivateField(stateManager, "_themeManagerData", _theme);

            var prism = go.AddComponent<Prism>();
            prism.prismProperties = new PrismProperties();
            SkimRaceFactory.SetPrivateField(prism, "_onTrailBlockCreatedEventChannel", _onCreated);
            SkimRaceFactory.SetPrivateField(prism, "_onTrailBlockDestroyedEventChannel", _onDestroyed);
            SkimRaceFactory.SetPrivateField(prism, "_onTrailBlockRestoredEventChannel", _onRestored);
            SkimRaceFactory.SetPrivateField(prism, "OnBlockImpactedEventChannel", _onBlockImpacted);

            var prismImpactor = go.AddComponent<PrismImpactor>(); // Awake binds .Prism
            var impactCollider = go.AddComponent<ImpactCollider>();
            SkimRaceFactory.SetPrivateField(impactCollider, "impactorObject", prismImpactor);

            go.AddComponent<PrismGrowthDriver>();

            go.SetActive(true); // Awake chain: Prism caches managers, seeds prismProperties
            _live.Add(go);
            return new PrismReturnEventData { SpawnedObject = go };
        }

        /// <summary>
        /// The race-restart active force — the ONLY mass sink. Destroys newest-first so the
        /// engine's sorted behaviour registry removes from its tail (cheap teardown of a
        /// large prismscape). Trail lists are cleared separately by the real
        /// VesselStatus.ResetForPlay → VesselPrismController.ClearTrails path.
        /// </summary>
        public void DespawnAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i])
                    Engine.Object.Destroy(_live[i]);
            _live.Clear();
        }
    }

    /// <summary>
    /// Client-layer scale animation driver — per-prism stand-in for the unported
    /// PrismScaleManager (Assets/_Scripts/Controller/Managers/PrismScaleManager.cs, Jobs +
    /// Burst). Replicates the manager's exact growth math per frame: while scaling,
    /// lerpSpeed = clamp(GrowthRate·dt, 0.05, 0.1) toward PrismScaleAnimator.TargetScale;
    /// within the 0.01 sqr completion threshold it snaps, clears IsScaling, and calls
    /// ExecuteOnScaleComplete() (volume bookkeeping + largest/smallest checks) — the same
    /// completion contract the manager drives in the original.
    /// </summary>
    public sealed class PrismGrowthDriver : MonoBehaviour
    {
        const float CompletionThresholdSqr = 0.01f; // PrismScaleManager.COMPLETION_THRESHOLD_SQR

        PrismScaleAnimator _animator;

        void Awake() => _animator = GetComponent<PrismScaleAnimator>();

        void Update()
        {
            if (_animator == null || !_animator.enabled || !_animator.IsScaling) return;

            var current = transform.localScale;
            var target = _animator.TargetScale;
            if ((target - current).sqrMagnitude > CompletionThresholdSqr)
            {
                float lerpSpeed = Mathf.Clamp(_animator.GrowthRate * Time.deltaTime, 0.05f, 0.1f);
                transform.localScale = Vector3.Lerp(current, target, lerpSpeed);
            }
            else
            {
                transform.localScale = target;
                _animator.IsScaling = false;
                _animator.ExecuteOnScaleComplete();
            }
        }
    }

    /// <summary>
    /// Live skim-contact state for HUD glow / audio shimmer, fed exclusively by the
    /// engine trigger pass: rides the skimmer GameObject (beside the trigger
    /// SphereCollider + SkimmerImpactor) and counts overlapping prism colliders via the
    /// same OnTriggerEnter/OnTriggerExit dispatch the impactor receives. No distance
    /// checks — this IS the real contact state.
    /// </summary>
    public sealed class SkimContactTracker : MonoBehaviour
    {
        // Reference comparer: the engine's Object.Equals treats any two destroyed objects
        // as equal ("fake null"), which would let exit-after-destroy remove the wrong entry.
        readonly HashSet<Collider> _overlapping = new(ReferenceEqualityComparer.Instance);

        public int ActivePrismContacts => _overlapping.Count;
        public bool IsSkimming => _overlapping.Count > 0;

        /// <summary>0..1 contact strength: one prism in reach = 0.5, two or more = full.</summary>
        public float Strength => Mathf.Clamp01(_overlapping.Count / 2f);

        void OnTriggerEnter(Collider other)
        {
            if (other && other.TryGetComponent(out PrismImpactor _))
                _overlapping.Add(other);
        }

        void OnTriggerExit(Collider other)
        {
            if (other is not null)
                _overlapping.Remove(other); // fires on separation AND destroy/disable
        }

        /// <summary>Race restart wipes the prismscape — drop stale contacts immediately.</summary>
        public void ResetContacts() => _overlapping.Clear();
    }

    /// <summary>
    /// The SkimRace trail-skim energy rule, authored as a concrete
    /// <see cref="SkimmerPrismEffectSO"/> — the same extension point the original game's
    /// per-vessel skimmer-prism effect assets use (e.g.
    /// SkimmerChangeResourceByPrismEffectSO). Runs INSIDE the real dispatch chain:
    /// TriggerPass → ImpactorBase.OnTriggerEnter → SkimmerImpactor.AcceptImpactee →
    /// SkimmerPrismEffects. Each prism the skimmer sweeps charges the energy bar; drifting
    /// along a trail charges faster, and Charge levels raise the rate further. Own fresh
    /// trail can't self-charge because the real VesselPrismController keeps a new block's
    /// collider disabled until the spawning vessel's skimmer has cleared it
    /// (waitTillOutsideSkimmer) — lapping back onto your own ribbon is what re-arms it.
    /// </summary>
    public sealed class SkimRaceTrailSkimEnergyEffectSO : SkimmerPrismEffectSO
    {
        [Header("SkimRace energy rule")]
        [Tooltip("Energy added per prism the skimmer sweeps (before bonuses).")]
        // Rung-4 balance pass: 0.045 pegged the bar at 1.00 for any trail-rider
        // (~10 enters/s ≈ 0.45/s, matching the old boost drain exactly). At 0.025 a
        // clean ribbon ride charges ~0.25/s against the 0.55/s boost drain — only
        // drift-skimming (×2 → ~0.5/s) nearly sustains a boost, so the bar breathes.
        public float GainPerPrism = 0.025f;
        [Tooltip("Extra multiplier at full analog drift (1 = up to 2x while drifting).")]
        public float DriftBonus = 1f;
        [Tooltip("Charge-element bonus per level.")]
        public float ChargePerLevel = 0.06f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            var resources = status.ResourceSystem;
            var input = status.InputStatus;
            float drift = input == null ? 0f
                : Mathf.Clamp01(input.LeftTriggerAnalog + input.RightTriggerAnalog);
            resources.ChangeResourceAmount(0,
                GainPerPrism
                * (1f + DriftBonus * drift)
                * (1f + ChargePerLevel * resources.GetLevel(Element.Charge)));
        }
    }
}

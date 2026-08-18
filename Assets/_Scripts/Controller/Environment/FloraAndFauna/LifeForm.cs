using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Abstract base for all lifeforms with health/spindle infrastructure (primarily Flora).
    /// Delegates health block tracking to <see cref="HealthBlockTracker"/> and
    /// spindle tracking to <see cref="SpindleTracker"/> for SRP compliance.
    /// </summary>
    public abstract class LifeForm : MonoBehaviour, ILifeFormEntity
    {
        [Inject] AudioSystem audioSystem;
        [Header("Data References")]
        [Inject] protected GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Health & Visuals")]
        [FormerlySerializedAs("healthBlock")]
        [SerializeField] protected HealthPrism healthPrism;
        [SerializeField] protected Spindle spindle;

        [Header("Lifecycle")]
        [SerializeField] int healthBlocksForMaturity = 1;
        [SerializeField] int minHealthBlocks = 0;
        [SerializeField] float shieldPeriod = 0;
        [SerializeField] private bool autoInitialize = true;

        [Tooltip("Seconds between spindle rings on a JOUSTED death, where the structure " +
                 "unravels from the heart outward instead of detonating (Docs/ECOSYSTEM.md §26). " +
                 "The fauna counterpart is LightFaunaDataSO.witherRingInterval. 0 collapses the " +
                 "whole body in a single frame, which reads as a pop - keep it above zero.")]
        [SerializeField, Min(0.01f)] float witherRingInterval = 0.25f;

        [Header("Team")]
        [FormerlySerializedAs("Team")]
        public Domains domain;

        [Header("Events")]
        [SerializeField] ScriptableEventInt onLifeFormCreated;
        [SerializeField] ScriptableEventInt onLifeFormDestroyed;

        // --- Public contract (ILifeFormEntity) ---
        public Domains Domain => domain;

        /// <summary>Elemental contract: the element this lifeform carries (its crystal's element).</summary>
        public Element Element => crystal ? crystal.crystalProperties.Element : Element.None;

        /// <summary>
        /// Elemental contract: this lifeform's level (1..5). <b>Level is EARNED, and never
        /// ROLLED</b> (Docs/ECOSYSTEM.md §33): a plant earns a level by reproducing, a creature
        /// by feeding a significant amount, and an ordinary spawn starts at 1. A MODE may still
        /// author a higher hatch level deliberately (Wildlife Liberation's per-cage tiers) —
        /// what is gone is the dice.
        /// </summary>
        public int Level { get; protected set; } = 1;

        /// <summary>
        /// Elemental contract: seeds the spawn level. Sizes the heart from the shared level
        /// curve (<see cref="LifeFormCrystal"/> — one size per level for every species and
        /// element); Flora also scales its leaf prisms. Call BEFORE Initialize - it spawns AT
        /// size, nothing pops mid-life.
        ///
        /// <para>The ordinary spawn path passes level 1; anything above it is authored
        /// deliberately — Wildlife Liberation's cage tiers, and the Lifeform Matrix bench,
        /// which spawns a chosen level so a tuner can see the whole band.</para>
        /// </summary>
        public virtual void ApplyLevel(int level, float bodyScalePerLevel)
        {
            Level = Mathf.Clamp(level, 1, Fauna.MaxLifeformLevel);
            _bodyScalePerLevel = Mathf.Max(1f, bodyScalePerLevel);
            // Resolve early: the spawner calls this BEFORE Initialize, so the field it assigns
            // is not populated yet unless ApplyElement provisioned one.
            if (!crystal) crystal = GetComponentInChildren<Crystal>(true);
            LifeFormCrystal.ApplyLevelSize(crystal, Level);
        }

        // Per-level body factor remembered from ApplyLevel so in-world LevelUp uses the same
        // curve. The crystal has no per-species factor - its size is the shared level curve.
        float _bodyScalePerLevel = 1.15f;

        /// <summary>Per-level body scale factor (config-seeded); Flora reads it in LevelUp.</summary>
        protected float BodyScalePerLevel => _bodyScalePerLevel;

        /// <summary>Rooted flora never travel - which makes them trivially joustable.</summary>
        public float CurrentSpeed => 0f;

        /// <summary>
        /// A faster vessel jousted this lifeform's heart: dies through the normal death path
        /// (wither via spindles, crystal drop - mass conserved, continuity honored). Idempotent.
        ///
        /// The style is stamped BEFORE the death runs because the death READS it: a joust does
        /// not detonate the plant. The jouster took the heart, so the structure comes apart
        /// FROM THE HEART OUTWARD and its prisms stay standing as a skeleton
        /// (Docs/ECOSYSTEM.md §26).
        /// </summary>
        public bool Jousted(string killerName)
        {
            if (dying || isCleaningUp) return false;
            dying = true;
            deathStyle = LifeformDeathStyle.Jousted;
            Die(killerName);
            return true;
        }

        /// <summary>
        /// In-world level-up. For flora the earning event is <b>reproduction</b>
        /// (<see cref="Flora"/>); the Squirrel Space-5 'Shepherd' joust on an ally also grants
        /// one. The heart grows a step along the shared level curve; Flora also grows future
        /// leaves. Capped at level 5.
        /// </summary>
        public virtual bool LevelUp()
        {
            if (Level >= Fauna.MaxLifeformLevel) return false;
            Level++;
            // Grows, never pops (continuity of existence). The target is the LOCAL scale that
            // lands the heart on this level's shared world size.
            if (crystal)
                crystal.GrowCrystal(1f, LifeFormCrystal.LocalScaleForLevel(crystal, Level));
            return true;
        }

        /// <summary>
        /// Elemental contract: provisions this lifeform's crystal to EXACTLY the given element
        /// (element as data - one base prefab, per-element variants from config). Call BEFORE
        /// Initialize so the crystal lookup there finds the provisioned one. None is a no-op.
        /// </summary>
        public void ApplyElement(Element element)
        {
            if (element == Element.None) return;
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, element);
        }

        /// <summary>
        /// Applies the config's per-variant expression - the deltas that used to force a flora
        /// prefab variant per element (see FloraVariantTuning). Base handles what every
        /// lifeform has (shield cadence); Flora / AssembledFlora layer leaf shape, growth
        /// tempo, planting radius, and prism budget on top. Call BEFORE Initialize - leafSize
        /// is consumed there.
        /// </summary>
        public virtual void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            if (tuning == null) return;
            if (tuning.ShieldPeriod >= 0f) shieldPeriod = tuning.ShieldPeriod;
            if (tuning.WitherRingInterval > 0f) witherRingInterval = tuning.WitherRingInterval;
        }

        public static event Action<string, int> OnLifeFormDeath;

        // --- Composition: extracted trackers (SRP) ---
        protected HealthBlockTracker healthTracker;
        protected SpindleTracker spindleTracker;

        // --- Internal state ---
        protected Crystal crystal;
        protected Cell cell;

        // How this lifeform is coming apart - written by the force that killed it (today only
        // Jousted, stamped in Jousted()). Everything else keeps the ordinary destruction.
        LifeformDeathStyle deathStyle = LifeformDeathStyle.Withered;

        bool dying;
        bool isCleaningUp;
        bool initialized;

        // --- Lifecycle: Enable / Disable ---

        protected virtual void OnEnable()
        {
            if (gameData != null)
                gameData.OnShowGameEndScreen.OnRaised += HandleTurnEnded;
        }

        protected virtual void OnDisable()
        {
            if (gameData != null)
                gameData.OnShowGameEndScreen.OnRaised -= HandleTurnEnded;
        }

        // --- Lifecycle: Start / Initialize ---

        protected virtual void Start()
        {
            if (!autoInitialize || initialized) return;
            if (!cell)
                cell = cellData.Cell;
            Initialize(cell);
        }

        public virtual void Initialize(Cell cell)
        {
            if (initialized) return;
            initialized = true;

            this.cell = cell;
            // Pass cell to the tracker so Add/Remove/CleanupDeadRefs forward to
            // Cell.AddBlock/RemoveBlock and feed Cell.LiveBlockCount.
            healthTracker = new HealthBlockTracker(healthBlocksForMaturity, minHealthBlocks, cell);
            spindleTracker = new SpindleTracker();

            crystal = GetComponentInChildren<Crystal>();
            // The crystal is this lifeform's HEART while it lives: joustable by a faster vessel
            // (destroys opposing-domain lifeforms; Space-5 levels up allies) but never
            // skim-collectable until death drops it. Cleared by ActivateCrystal in Die.
            if (crystal) crystal.SetEmbeddedIn(this);

            BindEmbeddedParts();

            // The ELEMENT gets the last word on the cadence - after the prefab, after the
            // config's variant block, and after the cell's overrides, because it is the only
            // point where every one of them has already been applied AND the crystal that
            // carries the element has been resolved (two lines up). See
            // Flora.ResolveShieldPeriod: Charge armours its mass, and a config must not be
            // able to author that away by leaving the field at 0.
            shieldPeriod = ResolveShieldPeriod(shieldPeriod);

            if (shieldPeriod > 0)
                StartCoroutine(ShieldRegenCoroutine());

            if (cell != null)
                onLifeFormCreated?.Raise(cell.ID);
        }

        void BindEmbeddedParts()
        {
            foreach (var sp in GetComponentsInChildren<Spindle>(true))
            {
                if (!sp) continue;
                AddSpindle(sp);
            }

            foreach (var hp in GetComponentsInChildren<HealthPrism>(true))
            {
                if (!hp) continue;
                hp.LifeForm = this;
                hp.ChangeTeam(domain);
                hp.Initialize("FaunaPrefab");
                AddHealthBlock(hp);
            }
        }

        // --- Health Block Management (delegates to HealthBlockTracker) ---

        public virtual void AddHealthBlock(HealthPrism healthPrism)
        {
            if (!healthPrism) return;
            healthTracker.Add(healthPrism, this, domain);
        }

        public virtual void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            if (!healthPrism) return;
            healthTracker.Remove(healthPrism, killerName);
            spindleTracker.CleanupDeadRefs();
            CheckIfDead(killerName);
        }

        // --- Spindle Management (delegates to SpindleTracker) ---

        public void AddSpindle(Spindle spindle)
        {
            spindleTracker.Add(spindle, this);
        }

        public Spindle AddSpindle()
        {
            Spindle newSpindle = spindleTracker.Instantiate(spindle, transform);
            spindleTracker.Add(newSpindle, this);
            return newSpindle;
        }

        public virtual void RemoveSpindle(Spindle spindle)
        {
            spindleTracker.Remove(spindle);
            CheckIfDead();
        }

        // --- Death / Lifecycle ---

        public void CheckIfDead(string killerName = "")
        {
            if (dying) return;

            healthTracker.CleanupDeadRefs();
            spindleTracker.CleanupDeadRefs();

            if (healthTracker.IsLethal())
            {
                dying = true;
                Die(killerName);
                return;
            }

            if (spindleTracker.IsEmpty())
            {
                dying = true;
                Die();
            }
        }

        protected virtual void Die(string killerName = "")
        {
            if (isCleaningUp) return;

            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.CreatureDeath, transform.position);

            if (crystal && crystal.gameObject.activeInHierarchy && !isCleaningUp)
                crystal.ActivateCrystal();

            int cellId = cell ? cell.ID : -1;

            if (!string.IsNullOrEmpty(killerName))
                OnLifeFormDeath?.Invoke(killerName, cellId);

            if (cell)
                cell.UnregisterSpawnedObject(gameObject);

            // Before the destruction step, so the ordered wither it may start survives.
            StopAllCoroutines();

            if (deathStyle == LifeformDeathStyle.Jousted)
                WitherToSkeleton();
            else
                DestroyStructure();

            if (gameObject.activeInHierarchy)
                StartCoroutine(DieCoroutine(cellId));
            else if (!isCleaningUp)
                Destroy(gameObject);
        }

        /// <summary>The ordinary flora death: the remaining structure blows apart where it stood.</summary>
        void DestroyStructure()
        {
            healthTracker.DamageAll(Domains.Blue);
            spindleTracker.ForceWitherAll(gameObject);
        }

        /// <summary>
        /// The JOUSTED death (Docs/ECOSYSTEM.md §26): a vessel took this lifeform's heart, so
        /// the plant does not detonate. Its prisms are left standing as a skeleton - the frame
        /// of the thing that grew here, now ordinary cell mass the food web can graze - while
        /// the soft tissue withers spindle by spindle FROM THE HEART OUTWARD, unravelling
        /// around the hole the joust left. Exactly the mirror of the outside-in starvation
        /// wither a creature does (see <see cref="LightFauna"/>).
        ///
        /// <see cref="DieCoroutine"/> is what waits for it: the husk is destroyed only once
        /// every spindle has finished evaporating, so this needs no completion callback.
        /// </summary>
        void WitherToSkeleton()
        {
            // Where the wither starts. Read now — the heart was freed a few lines above and is
            // already on its way to whoever took it.
            Vector3 heart = crystal ? crystal.transform.position : transform.position;

            // Isolate first: ForceWither recurses into child spindles and destroying a spindle
            // destroys its children, either of which would collapse the plant in one step.
            var spindles = GetComponentsInChildren<Spindle>(true).Where(s => s).ToList();
            foreach (var sp in spindles)
                sp.IsolateForOrderedWither(transform);

            // The frame stays. Must precede the wither: a health prism is parented to a
            // spindle, so evaporating spindles first would destroy the mass this conserves.
            Transform skeletonParent = cell ? cell.transform : null;
            foreach (var hp in GetComponentsInChildren<HealthPrism>(true))
                if (hp && !hp.destroyed) hp.LeaveAsSkeleton(skeletonParent);

            spindles.Sort((a, b) =>
                (a.transform.position - heart).sqrMagnitude.CompareTo(
                (b.transform.position - heart).sqrMagnitude));

            // Can't animate while inactive (scene teardown) - the skeleton above already
            // conserved the mass, so collapse what's left in one step rather than throwing.
            if (!gameObject.activeInHierarchy)
            {
                foreach (var sp in spindles)
                    if (sp) sp.ForceWither();
                return;
            }

            StartCoroutine(WitherOutwardCoroutine(spindles));
        }

        IEnumerator WitherOutwardCoroutine(List<Spindle> orderedFromHeart)
        {
            for (int i = 0; i < orderedFromHeart.Count; i++)
            {
                if (orderedFromHeart[i]) orderedFromHeart[i].ForceWither();
                yield return new WaitForSeconds(witherRingInterval);
            }
        }

        IEnumerator DieCoroutine(int cellId)
        {
            while (true)
            {
                spindleTracker.CleanupDeadRefs();
                if (spindleTracker.IsEmpty()) break;
                yield return null;
            }

            if (!isCleaningUp)
            {
                onLifeFormDestroyed?.Raise(cellId);
                Destroy(gameObject);
            }
        }

        // --- Team Assignment ---

        public GameObject GetGameObject() => gameObject;

        public void SetTeam(Domains domain)
        {
            this.domain = domain;
            healthTracker?.SetTeam(domain);

            var allHealthPrisms = GetComponentsInChildren<HealthPrism>(true);
            foreach (var hp in allHealthPrisms)
                if (hp) hp.ChangeTeam(domain);
        }

        // --- Shield Regeneration ---

        /// <summary>
        /// The last word on this lifeform's shield cadence, asked once at
        /// <see cref="Initialize"/> with whatever the prefab + config + cell overrides
        /// resolved to. The base keeps it exactly as authored; a subclass overrides only to
        /// express an ELEMENTAL law that must hold on every spawn path
        /// (<c>Flora.ResolveShieldPeriod</c> - Charge armours its leaves).
        ///
        /// <para>It is a hook rather than a line in <see cref="ApplyVariantTuning"/> because
        /// the element is not known there: the crystal is resolved in Initialize, and the
        /// variant block itself arrives BEFORE it and would be overwritten by the cell's
        /// overrides afterwards.</para>
        /// </summary>
        protected virtual float ResolveShieldPeriod(float authored) => authored;

        IEnumerator ShieldRegenCoroutine()
        {
            while (shieldPeriod > 0)
            {
                var blocks = healthTracker.All.ToList();
                if (blocks.Count > 0)
                {
                    foreach (var block in blocks)
                    {
                        if (block) block.ActivateShield();
                        yield return new WaitForSeconds(shieldPeriod);
                    }
                }
                else
                {
                    yield return new WaitForSeconds(shieldPeriod);
                }
            }
        }

        // --- Turn End Cleanup ---

        protected virtual void HandleTurnEnded()
        {
            isCleaningUp = true;
            StopAllCoroutines();
            // Drain still-living blocks to the cell counter - turn-end Destroys the
            // gameObject without running Die()/RemoveHealthBlock, so without this
            // Cell.LiveBlockCount drifts upward across rounds.
            if (cell != null && healthTracker != null)
                foreach (var hp in healthTracker.All)
                    if (hp) cell.RemoveBlock(hp);
            if (gameObject) Destroy(gameObject);
        }
    }
}

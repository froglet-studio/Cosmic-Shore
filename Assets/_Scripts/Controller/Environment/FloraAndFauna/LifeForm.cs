using System;
using System.Collections;
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

        /// <summary>Elemental contract: this lifeform's level (1..5), seeded from config at spawn.</summary>
        public int Level { get; protected set; } = 1;

        /// <summary>
        /// Elemental contract: seeds the spawn level. Base scales the crystal (level 5 always
        /// carries, and drops, the largest crystal); Flora also scales its leaf prisms. Call
        /// BEFORE Initialize - it spawns AT size, nothing pops mid-life.
        /// </summary>
        public virtual void ApplyLevel(int level, float bodyScalePerLevel, float crystalScalePerLevel)
        {
            Level = Mathf.Clamp(level, 1, 5);
            _bodyScalePerLevel = Mathf.Max(1f, bodyScalePerLevel);
            _crystalScalePerLevel = Mathf.Max(1f, crystalScalePerLevel);
            if (crystal && Level > 1)
                crystal.transform.localScale *= Mathf.Pow(_crystalScalePerLevel, Level - 1);
        }

        // Per-level factors remembered from ApplyLevel so in-world LevelUp uses the same curve.
        float _bodyScalePerLevel = 1.15f;
        float _crystalScalePerLevel = 1.2f;

        /// <summary>Per-level scale factors (config-seeded); Flora reads them in LevelUp.</summary>
        protected float BodyScalePerLevel => _bodyScalePerLevel;
        protected float CrystalScalePerLevel => _crystalScalePerLevel;

        /// <summary>Rooted flora never travel - which makes them trivially joustable.</summary>
        public float CurrentSpeed => 0f;

        /// <summary>
        /// A faster vessel jousted this lifeform's heart: dies through the normal death path
        /// (wither via spindles, crystal drop - mass conserved, continuity honored). Idempotent.
        /// </summary>
        public bool Jousted(string killerName)
        {
            if (dying || isCleaningUp) return false;
            dying = true;
            Die(killerName);
            return true;
        }

        /// <summary>
        /// In-world level-up (the Squirrel Space-5 'Shepherd' joust on an ally). The crystal
        /// grows a step; Flora also grows future leaves. Capped at level 5.
        /// </summary>
        public virtual bool LevelUp()
        {
            if (Level >= 5) return false;
            Level++;
            if (crystal)
                crystal.GrowCrystal(1f, crystal.transform.localScale.x * _crystalScalePerLevel);
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
        }

        public static event Action<string, int> OnLifeFormDeath;

        // --- Composition: extracted trackers (SRP) ---
        protected HealthBlockTracker healthTracker;
        protected SpindleTracker spindleTracker;

        // --- Internal state ---
        protected Crystal crystal;
        protected Cell cell;
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

            healthTracker.DamageAll(Domains.Blue);
            spindleTracker.ForceWitherAll(gameObject);

            if (cell)
                cell.UnregisterSpawnedObject(gameObject);

            StopAllCoroutines();

            if (gameObject.activeInHierarchy)
                StartCoroutine(DieCoroutine(cellId));
            else if (!isCleaningUp)
                Destroy(gameObject);
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

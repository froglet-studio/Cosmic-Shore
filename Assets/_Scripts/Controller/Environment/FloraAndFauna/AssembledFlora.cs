
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    public class GrowthInfo
    {
        public bool CanGrow;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsDangerous;
        public int Depth;
    }

    /// <summary>
    /// A flora that uses an <c cref="Assembler">Assembler</c> to define its growth pattern
    /// </summary>
    public class AssembledFlora : Flora
    {
        struct Branch
        {
            public GameObject gameObject;
            public int depth;
            public Assembler assembler;

            public Branch(HealthPrism healthPrism)
            {
                gameObject = healthPrism.gameObject;
                depth = 0;
                assembler = healthPrism.GetComponent<Assembler>();
            }
        }
        
        /// <summary>
        /// The max recursion depth of the assembler
        /// </summary>
        [SerializeField] int depth = 50;
        [Tooltip("Maximum LIVE prisms this flora can hold. Consumption frees budget — a grazed " +
                 "flora regrows toward this cap instead of staying a permanent un-growing fragment.")]
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] int maxDepth = 30;
        [SerializeField] int itemsPerGrow = 5;
        [SerializeField] int randomItems = 2;
        [SerializeField] float crystalGrowth = 1.01f;
        [Tooltip("How many surviving prisms to re-sprout growth branches from when every active " +
                 "branch has been consumed or exhausted (a mature gyroid has none). This is what " +
                 "lets a grazed flora 'reawaken' instead of sitting as a dead fragment.")]
        [SerializeField] int reseedBranchCount = 3;

        List<Branch> activeBranches = new List<Branch>();
        static readonly List<Branch> s_newBranches = new List<Branch>(16);
        static readonly List<int> s_removeIndices = new List<int>(16);

        Assembler assembler;

        public static class AssemblerFactory
        {
            public static Assembler ProgramAssembler(GameObject gameObject, GrowthInfo growthInfo)
            {
                if (growthInfo is GyroidGrowthInfo gyroidInfo)
                {
                    if (!gameObject.TryGetComponent<GyroidAssembler>(out var newAssembler))
                        newAssembler = gameObject.AddComponent<GyroidAssembler>();
                    newAssembler.BlockType = gyroidInfo.BlockType;
                    newAssembler.Depth = growthInfo.Depth;
                    return newAssembler;
                }
                else if (growthInfo is SchwarzPGrowthInfo schwarzPGrowthInfo)
                {
                    // Pooled HealthPrisms don't carry assembler components — add on demand
                    if (!gameObject.TryGetComponent<SchwarzPAssembler>(out var newAssembler))
                        newAssembler = gameObject.AddComponent<SchwarzPAssembler>();
                    newAssembler.Program(schwarzPGrowthInfo);
                    return newAssembler;
                }
                // Add other assembler types here as needed
                else
                {
                    if (!gameObject.TryGetComponent<Assembler>(out var newAssembler))
                    {
                        // Fallback: add WallAssembler as default concrete type
                        newAssembler = gameObject.AddComponent<WallAssembler>();
                    }
                    newAssembler.Depth = growthInfo.Depth;
                    return newAssembler;
                }
            }
        }

        public override void Grow()
        {
            // Live-prism budget: the flora can hold at most maxTotalSpawnedObjects LIVE
            // prisms. Consumption frees budget, so a grazed flora regrows. (Was: a
            // lifetime spawn counter that never decremented — a fully-grown flora could
            // never grow again even after fauna ate most of it, which is exactly the
            // "ungrowing gyroid fragments" failure observed in-game.)
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: flora grow at a steady rate until the cell crosses into Frenzy,
            // then freeze, resuming automatically when an active force (fauna grazing /
            // vessel abilities) brings the count back below the Frenzy exit threshold.
            // Cell.FloraGrowingEnabled is the single source of truth — no early growth cap
            // (that staggered self-limit was a cheat; the food web is the only down-force).
            if (cell && !cell.FloraGrowingEnabled) return;

            // Reawakening: a flora whose active branches were all consumed or exhausted
            // (a fully-grown gyroid has none) re-sprouts from surviving prisms instead
            // of staying a permanent un-growing fragment. Growth continues next cycle
            // from the re-seeded branches. Guarded on having surviving prisms so a
            // not-yet-seeded flora doesn't spuriously seed here before Plant()/Initialize
            // finish their own setup.
            if (activeBranches.Count == 0)
            {
                if (healthTracker != null && healthTracker.Count > 0)
                    ReseedBranches();
                return;
            }

            s_newBranches.Clear();
            s_removeIndices.Clear();

            float itemsSpawned = 0;
            int skippedItems = 0;
            for (int i = 0; i < activeBranches.Count && itemsSpawned < itemsPerGrow; i++)
            {
                Branch branch = activeBranches[i];

                if (!branch.assembler || branch.depth >= maxDepth)
                {
                    continue;
                }

                // Randomly skip grow sites BEFORE GetGrowthInfo: a successful
                // GetGrowthInfo claims the site in the PrismSpatialIndex, and a
                // skipped-after-claim site would hold its reservation until TTL,
                // blocking siblings from growing there in the meantime.
                if (skippedItems < randomItems && Random.value < 0.5f)
                {
                    skippedItems++;
                    continue;
                }

                var growthInfo = branch.assembler.GetGrowthInfo();
                if (!growthInfo.CanGrow)
                {
                    s_removeIndices.Add(i);
                    continue;
                }

                HealthPrism newHealthPrism = GetHealthPrism(growthInfo.Position, growthInfo.Rotation);
                if (!newHealthPrism) continue;
                AddHealthBlock(newHealthPrism);
                Branch newBranch = new Branch(newHealthPrism);

                var newAssembler = AssemblerFactory.ProgramAssembler(newHealthPrism.gameObject, growthInfo);
                if (newAssembler == null)
                {
                    CSDebug.LogError("Failed to create assembler");
                    continue;
                }

                Spindle newSpindle;
                if (SpindlePoolManager.Instance)
                {
                    newSpindle = SpindlePoolManager.Instance.Get(
                        newHealthPrism.transform.position,
                        newHealthPrism.transform.rotation,
                        branch.gameObject.transform);
                }
                else
                {
                    newSpindle = Instantiate(spindle, branch.gameObject.transform);
                    newSpindle.transform.position = newHealthPrism.transform.position;
                    newSpindle.transform.rotation = newHealthPrism.transform.rotation;
                }
                newSpindle.LifeForm = this;

                newHealthPrism.transform.SetParent(newSpindle.transform, false);
                newHealthPrism.transform.localPosition = Vector3.zero;
                newHealthPrism.transform.localRotation = Quaternion.identity;
                if (growthInfo.IsDangerous) newHealthPrism.MakeDangerous();
                newHealthPrism.Initialize();

                newBranch.gameObject = newSpindle.gameObject;
                newBranch.assembler = newAssembler;
                newBranch.depth = branch.depth + 1;

                s_newBranches.Add(newBranch);
                itemsSpawned++;

                if (branch.depth >= maxDepth - 1 || branch.assembler.IsFullyBonded())
                {
                    s_removeIndices.Add(i);
                }
            }

            // Remove in reverse order to preserve indices
            for (int i = s_removeIndices.Count - 1; i >= 0; i--)
                activeBranches.RemoveAt(s_removeIndices[i]);

            activeBranches.AddRange(s_newBranches);
            s_newBranches.Clear();
            s_removeIndices.Clear();
            GrowCrystal();
        }

        /// <summary>
        /// Re-sprout growth branches from surviving prisms — each surviving prism still
        /// carries its Assembler, so wrapping it in a Branch puts it back in the grow
        /// rotation. Survivors are sampled RANDOMLY (not first-N) so repeated reseeds
        /// don't keep picking the same fully-bonded prisms; ones next to consumed gaps
        /// have room to grow and heal the wound. Branches that turn out to be fully
        /// bonded are culled by the normal Grow() loop. Falls back to a fresh root
        /// assembler when nothing usable survives.
        /// </summary>
        void ReseedBranches()
        {
            var survivors = new List<HealthPrism>();
            foreach (var hp in healthTracker.All)
                if (hp) survivors.Add(hp);

            if (survivors.Count == 0)
            {
                CreateNewAssembler();
                return;
            }

            // Partial Fisher-Yates: try a bounded number of random survivors, keep the
            // ones that still carry an Assembler. Bounded so the cold reseed path never
            // does an unbounded GetComponent sweep over a 1000-prism flora.
            int target = Mathf.Max(1, reseedBranchCount);
            int attempts = Mathf.Min(survivors.Count, target * 3);
            int seeded = 0;
            for (int i = 0; i < attempts && seeded < target; i++)
            {
                int pick = Random.Range(i, survivors.Count);
                (survivors[i], survivors[pick]) = (survivors[pick], survivors[i]);
                if (!survivors[i].TryGetComponent<Assembler>(out _)) continue;
                activeBranches.Add(new Branch(survivors[i]));
                seeded++;
            }

            if (seeded == 0)
                CreateNewAssembler();
        }

        void GrowCrystal()
        {
            if (crystal)
            {
                crystal.GrowCrystal(1, crystal.transform.localScale.x + crystalGrowth);
            }
        }

        public override void RemoveSpindle(Spindle spindle)
        {
            base.RemoveSpindle(spindle);
            for (int i = activeBranches.Count - 1; i >= 0; i--)
            {
                if (activeBranches[i].gameObject == spindle.gameObject)
                {
                    activeBranches.RemoveAt(i);
                    break;
                }
            }
        }

        public override void Plant()
        {
            assembler = CreateNewAssembler();
            if (!assembler) return;
            // Disperse across the cell (fraction of membrane radius — see Flora base)
            // instead of the old hard-coded 200m huddle around the crystal. Dispersed,
            // domain-coherent flora clusters are what give fauna schools of different
            // domains genuinely different anti-domain density targets.
            float radius = ResolvePlantRadius(legacyRadius: 200f);
            transform.position = cellData.CrystalTransform.position + radius * Random.onUnitSphere;
        }

        /// <summary>
        /// Pooled HealthPrisms don't carry Assembler components — the original prefab did.
        /// Copy the Assembler type from the healthPrism prefab template onto the pooled instance.
        /// </summary>
        void EnsureAssemblerComponent(GameObject go)
        {
            if (go.GetComponent<Assembler>()) return;

            // Use the healthPrism prefab (still on LifeForm) as the template for which Assembler type to add
            if (healthPrism && healthPrism.TryGetComponent<GyroidAssembler>(out _))
                go.AddComponent<GyroidAssembler>();
            else if (healthPrism && healthPrism.TryGetComponent<SchwarzPAssembler>(out _))
                go.AddComponent<SchwarzPAssembler>();
            else
                go.AddComponent<WallAssembler>(); // default concrete type
        }

        public Assembler CreateNewAssembler()
        {
            var newSpindle = AddSpindle();

            HealthPrism newHealthPrism = GetHealthPrism(transform.position, transform.rotation);
            if (!newHealthPrism) return null;
            EnsureAssemblerComponent(newHealthPrism.gameObject);
            AddHealthBlock(newHealthPrism);
            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.LifeForm = this;
            newHealthPrism.Initialize();

            Assembler newAssembler = newHealthPrism.GetComponent<Assembler>();
            if (!newAssembler)
            {
                CSDebug.LogError($"[AssembledFlora] Failed to add Assembler to pooled HealthPrism. " +
                    $"Check that the healthPrism prefab on '{name}' has an Assembler component.", this);
                return null;
            }
            newAssembler.Prism = newHealthPrism;
            newAssembler.Spindle = newSpindle;
            newAssembler.Depth = depth;

            Branch newBranch = new Branch(newHealthPrism);
            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = 0;

            activeBranches.Add(newBranch);

            return newAssembler;
        }
    }
}

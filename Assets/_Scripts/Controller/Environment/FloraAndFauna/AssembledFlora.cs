
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

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        Assembler assembler;

        public static class AssemblerFactory
        {
            public static Assembler ProgramAssembler(GameObject gameObject, GrowthInfo growthInfo)
            {
                if (growthInfo is GyroidGrowthInfo)
                {
                    var newAssembler = gameObject.GetComponent<GyroidAssembler>();
                    // Copy properties from growthInfo.assembler to newAssembler
                    newAssembler.BlockType = ((GyroidGrowthInfo)growthInfo).BlockType;
                    newAssembler.Depth = growthInfo.Depth;
                    // Copy other properties as needed
                    return newAssembler;
                }
                // Add other assembler types here as needed
                else
                {
                    var newAssembler = gameObject.GetComponent<Assembler>();
                    // Copy properties from growthInfo.assembler to newAssembler
                    newAssembler.Depth = growthInfo.Depth;
                    // Copy other properties as needed
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

            List<Branch> newBranches = new List<Branch>();
            List<Branch> branchesToRemove = new List<Branch>();

            float itemsSpawned = 0;
            int skippedItems = 0;
            for (int i = 0; i < activeBranches.Count && itemsSpawned < itemsPerGrow; i++)
            {
                Branch branch = activeBranches.ElementAt(i);

                if (!branch.assembler || branch.depth >= maxDepth)
                {
                    continue;
                }

                var growthInfo = branch.assembler.GetGrowthInfo();
                if (!growthInfo.CanGrow)
                {
                    branchesToRemove.Add(branch);
                    continue;
                }

                // Randomly skip viable grow sites
                if (skippedItems < randomItems && Random.value < 0.5f)
                {
                    skippedItems++;
                    continue;
                }

                HealthPrism newHealthPrism = Instantiate(healthPrism, growthInfo.Position, growthInfo.Rotation);
                AddHealthBlock(newHealthPrism);
                Branch newBranch = new Branch(newHealthPrism);

                var newAssembler = AssemblerFactory.ProgramAssembler(newHealthPrism.gameObject, growthInfo);
                if (newAssembler == null)
                {
                    CSDebug.LogError("Failed to create assembler");
                    continue;
                }

                Spindle newSpindle = Instantiate(spindle, branch.gameObject.transform);
                newSpindle.LifeForm = this;
                newSpindle.transform.position = newHealthPrism.transform.position;
                newSpindle.transform.rotation = newHealthPrism.transform.rotation;

                newHealthPrism.transform.SetParent(newSpindle.transform, false);
                newHealthPrism.transform.localPosition = Vector3.zero;
                newHealthPrism.transform.localRotation = Quaternion.identity;
                if (growthInfo.IsDangerous) newHealthPrism.MakeDangerous();
                newHealthPrism.Initialize();
                
                newBranch.gameObject = newSpindle.gameObject;
                newBranch.assembler = newAssembler;
                newBranch.depth = branch.depth + 1;

                newBranches.Add(newBranch);
                itemsSpawned++;

                if (branch.depth >= maxDepth - 1 || branch.assembler.IsFullyBonded())
                {
                    branchesToRemove.Add(branch);
                }
            }

            foreach (Branch branch in branchesToRemove)
            {
                activeBranches.Remove(branch);
            }

            activeBranches.UnionWith(newBranches);
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
            Branch result = activeBranches.FirstOrDefault(item => item.gameObject == spindle.gameObject);
            activeBranches.Remove(result);
        }

        public override void Plant()
        {
            assembler = CreateNewAssembler();
            // Disperse across the cell (fraction of membrane radius — see Flora base)
            // instead of the old hard-coded 200m huddle around the crystal. Dispersed,
            // domain-coherent flora clusters are what give fauna schools of different
            // domains genuinely different anti-domain density targets.
            float radius = ResolvePlantRadius(legacyRadius: 200f);
            transform.position = cellData.CrystalTransform.position + radius * Random.onUnitSphere;
        }

        public Assembler CreateNewAssembler()
        {
            CSDebug.Log("New Assembler");
            var newSpindle = AddSpindle();

            HealthPrism newHealthPrism = Instantiate(healthPrism, transform.position, transform.rotation);
            AddHealthBlock(newHealthPrism);
            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.LifeForm = this;
            newHealthPrism.Initialize();

            Assembler newAssembler = newHealthPrism.GetComponent<Assembler>();
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

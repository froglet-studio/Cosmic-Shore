
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] int maxDepth = 30;
        [SerializeField] int itemsPerGrow = 5;
        [SerializeField] int randomItems = 2;
        [SerializeField] float crystalGrowth = 1.01f;

        [Header("Octagon Flora Seeding")]
        [Tooltip("Self-reference prefab for spawning new gyroid flora per octagon. " +
                 "When set, danger prisms (GEs/DE/EG/EsD) spawn their own flora.")]
        [SerializeField] AssembledFlora gyroidFloraPrefab;

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        int spawnedItemCount = 0;
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
            if (spawnedItemCount >= maxTotalSpawnedObjects) return;

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

                // Check if this is a danger prism that should seed its own octagon flora
                if (growthInfo.IsDangerous && growthInfo is GyroidGrowthInfo gyroidInfo
                    && GyroidAssembler.IsOctagonDangerType(gyroidInfo.BlockType)
                    && gyroidFloraPrefab != null)
                {
                    SpawnOctagonDangerPrism(growthInfo, gyroidInfo, branch);
                    itemsSpawned++;
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
            transform.position = cellData.CrystalTransform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius 
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

        /// <summary>
        /// Spawns an octagon danger prism. Instead of adding it to this flora,
        /// checks for an existing flora near the octagon center. If found, the
        /// prism joins that flora. If not, creates a new gyroid flora with a
        /// crystal at the octagon center and adds this prism as its first health block.
        /// </summary>
        void SpawnOctagonDangerPrism(GrowthInfo growthInfo, GyroidGrowthInfo gyroidInfo, Branch sourceBranch)
        {
            // Instantiate the health prism at the growth position
            HealthPrism newHealthPrism = Instantiate(healthPrism, growthInfo.Position, growthInfo.Rotation);
            newHealthPrism.MakeDangerous();

            // Program the assembler with the correct block type
            var newAssembler = AssemblerFactory.ProgramAssembler(newHealthPrism.gameObject, growthInfo);
            if (newAssembler == null)
            {
                CSDebug.LogError("Failed to create assembler for octagon danger prism");
                Destroy(newHealthPrism.gameObject);
                return;
            }

            var gyroidAssembler = newAssembler as GyroidAssembler;

            // Calculate the octagon center from the prism's bond site geometry
            Vector3 octagonCenter = growthInfo.Position;
            if (gyroidAssembler != null)
            {
                // Temporarily position at growth location so we can compute bond sites
                gyroidAssembler.BlockType = gyroidInfo.BlockType;
                // The octagon center will be computed after the assembler's transform is set
            }

            // Search for an existing flora near the octagon center
            var existingFlora = GyroidAssembler.FindNearbyAssembledFlora(
                growthInfo.Position,
                gyroidAssembler != null ? gyroidAssembler.OctagonCrystalDetectionRadius : 15f);

            if (existingFlora != null)
            {
                // Join the existing flora
                AdoptPrismIntoFlora(existingFlora, newHealthPrism, newAssembler, growthInfo, sourceBranch);
            }
            else
            {
                // Create a new gyroid flora at the octagon center
                var newFlora = CreateOctagonFlora(growthInfo.Position, growthInfo.Rotation);
                if (newFlora != null)
                {
                    AdoptPrismIntoFlora(newFlora, newHealthPrism, newAssembler, growthInfo, sourceBranch);
                }
                else
                {
                    // Fallback: add to this flora if prefab instantiation fails
                    CSDebug.LogWarning("Failed to create octagon flora, falling back to parent flora");
                    AdoptPrismIntoFlora(this, newHealthPrism, newAssembler, growthInfo, sourceBranch);
                }
            }

            spawnedItemCount++;
        }

        /// <summary>
        /// Adopts a newly created health prism into a target flora, creating
        /// the spindle hierarchy and initializing the prism.
        /// </summary>
        void AdoptPrismIntoFlora(AssembledFlora targetFlora, HealthPrism newHealthPrism,
            Assembler newAssembler, GrowthInfo growthInfo, Branch sourceBranch)
        {
            Spindle newSpindle = Instantiate(spindle, targetFlora.transform);
            newSpindle.LifeForm = targetFlora;
            newSpindle.transform.position = newHealthPrism.transform.position;
            newSpindle.transform.rotation = newHealthPrism.transform.rotation;
            targetFlora.AddSpindle(newSpindle);

            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.transform.localPosition = Vector3.zero;
            newHealthPrism.transform.localRotation = Quaternion.identity;
            newHealthPrism.LifeForm = targetFlora;
            targetFlora.AddHealthBlock(newHealthPrism);
            newHealthPrism.Initialize();

            // Add as an active branch in the target flora so it continues growing
            Branch newBranch = new Branch(newHealthPrism);
            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = sourceBranch.depth + 1;
            targetFlora.AddActiveBranch(newBranch);
        }

        /// <summary>
        /// Creates a new AssembledFlora for an octagon, with the crystal at local (0,0,0).
        /// The flora is positioned at the octagon center.
        /// </summary>
        AssembledFlora CreateOctagonFlora(Vector3 octagonCenter, Quaternion rotation)
        {
            if (gyroidFloraPrefab == null) return null;

            var newFlora = Instantiate(gyroidFloraPrefab, octagonCenter, rotation);
            newFlora.domain = domain;

            // Initialize with the same cell as this flora
            if (cell != null)
                newFlora.Initialize(cell);

            return newFlora;
        }

        /// <summary>
        /// Adds a branch to the active branch set. Used by octagon flora adoption
        /// to register danger prisms as growth points in the target flora.
        /// </summary>
        void AddActiveBranch(Branch branch)
        {
            activeBranches.Add(branch);
        }
    }
}

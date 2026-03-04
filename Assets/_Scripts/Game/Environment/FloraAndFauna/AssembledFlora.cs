
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Game;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore
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

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        int spawnedItemCount = 0;
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
            if (!assembler) return;
            transform.position = cellData.CrystalTransform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius
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
            else if (healthPrism && healthPrism.TryGetComponent<WallAssembler>(out _))
                go.AddComponent<WallAssembler>();
            else
                go.AddComponent<WallAssembler>(); // default concrete type
        }

        public Assembler CreateNewAssembler()
        {
            CSDebug.Log("New Assembler");
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

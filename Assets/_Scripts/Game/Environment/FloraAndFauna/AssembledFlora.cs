
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore
{
    public class GrowthInfo
    {
        public bool CanGrow;
        public Vector3 Position;
        public Quaternion Rotation;
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

            public Branch(HealthBlock healthBlock)
            {
                gameObject = healthBlock.gameObject;
                depth = 0;
                assembler = healthBlock.GetComponent<Assembler>();
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
        [SerializeField] float crystalGrowthMultiplier = 1.01f;

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

                HealthBlock newHealthBlock = Instantiate(healthBlock, growthInfo.Position, growthInfo.Rotation);
                AddHealthBlock(newHealthBlock);
                Branch newBranch = new Branch(newHealthBlock);

                var newAssembler = AssemblerFactory.ProgramAssembler(newHealthBlock.gameObject, growthInfo);
                if (newAssembler == null)
                {
                    Debug.LogError("Failed to create assembler");
                    continue;
                }

                Spindle newSpindle = Instantiate(spindle, branch.gameObject.transform);
                newSpindle.LifeForm = this;
                newSpindle.transform.position = newHealthBlock.transform.position;
                newSpindle.transform.rotation = newHealthBlock.transform.rotation;

                newHealthBlock.transform.SetParent(newSpindle.transform, false);
                newHealthBlock.transform.localPosition = Vector3.zero;
                newHealthBlock.transform.localRotation = Quaternion.identity;

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

        // the following function is called by the grow function to grow the crystal
        void GrowCrystal()
        {
            if (crystal)
            {
                crystal.GrowCrystal(1, crystal.transform.localScale.x * crystalGrowthMultiplier);
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
            transform.position = node.GetCrystal().transform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius 
        }

        public Assembler CreateNewAssembler()
        {
            Debug.Log("New Assembler");
            var newSpindle = AddSpindle();

            HealthBlock newHealthBlock = Instantiate(healthBlock, transform.position, transform.rotation);
            AddHealthBlock(newHealthBlock);
            newHealthBlock.transform.SetParent(newSpindle.transform, false);
            newHealthBlock.LifeForm = this;

            Assembler newAssembler = newHealthBlock.GetComponent<Assembler>();
            newAssembler.TrailBlock = newHealthBlock;
            newAssembler.Spindle = newSpindle;
            newAssembler.Depth = depth;

            Branch newBranch = new Branch(newHealthBlock);
            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = 0;

            activeBranches.Add(newBranch);

            return newAssembler;
        }
    }
}

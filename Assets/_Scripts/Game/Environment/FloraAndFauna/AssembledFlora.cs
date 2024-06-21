
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// A flora that uses an <c cref="Assembler">Assembler</c> to define its growth pattern
    /// </summary>
    public class AssembledFlora : Flora
    {
        /// <summary>
        /// The max recursion depth of the assembler
        /// </summary>
        [SerializeField] int depth = 50;
        /// <summary>
        /// does this flora either feed on other blocks or grow on its own?
        /// </summary>
        [SerializeField] bool feeds = false;
        /// <summary>
        /// the assembler that is actually used to grow this flora
        /// </summary>
        Assembler assembler;

        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] int maxDepth = 30;
        HashSet<Branch> activeBranches = new HashSet<Branch>();

        [SerializeField] float itemsPerGrow = 5;

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

        private int spawnedItemCount = 0;

        protected override void Start()
        {
            base.Start();
            //activeBranches.Add(new Branch { gameObject = gameObject, depth = 0 }); // add trunk
            //SeedBranches(); // add more truncks
        }

        void SeedBranches()
        {
                Branch branch = new Branch();
                branch.gameObject = Instantiate(spindle, transform.position, transform.rotation).gameObject;
                branch.gameObject.transform.parent = transform;
                AddSpindle(branch.gameObject.GetComponent<Spindle>());
                branch.depth = 0;
                activeBranches.Add(branch);
                
            
        }

        public static class AssemblerFactory
        {
            public static Assembler CreateAssembler(GameObject gameObject, GrowthInfo growthInfo)
            {
                if (growthInfo.assembler is GyroidAssembler gyroidAssembler)
                {
                    var newAssembler = gameObject.AddComponent<GyroidAssembler>();
                    // Copy properties from growthInfo.assembler to newAssembler
                    newAssembler.BlockType = gyroidAssembler.BlockType;
                    newAssembler.Depth = gyroidAssembler.Depth;
                    // Copy other properties as needed
                    return newAssembler;
                }
                // Add other assembler types here as needed
                else
                {
                    Debug.LogError("Unknown assembler type");
                    return null;
                }
            }
        }

        public override void Grow()
        {
            if (spawnedItemCount >= maxTotalSpawnedObjects) return;

            List<Branch> newBranches = new List<Branch>();
            List<Branch> branchesToRemove = new List<Branch>();

            float itemsSpawned = 0;
            foreach (Branch branch in activeBranches)
            {
                if (branch.depth < maxDepth && itemsSpawned < itemsPerGrow)
                {
                    GrowthInfo growthInfo = branch.assembler.ProgramBlock();
                    if (!growthInfo.canGrow)
                    {
                        Debug.Log("Assembler cannot grow");
                        branchesToRemove.Add(branch);
                        continue;
                    }

                    HealthBlock newHealthBlock = Instantiate(healthBlock, growthInfo.position, growthInfo.rotation);
                    newHealthBlock.LifeForm = this;
                    Branch newBranch = new Branch(newHealthBlock);

                    // Use the factory to create the correct assembler type
                    var newAssembler = AssemblerFactory.CreateAssembler(newHealthBlock.gameObject, growthInfo);
                    if (newAssembler == null)
                    {
                        Debug.LogError("Failed to create assembler");
                        continue;
                    }


                    Spindle newSpindle = Instantiate(spindle, branch.gameObject.transform);
                    newSpindle.LifeForm = this;

                    // Position the spindle relative to the new health block/assembler transform
                    newSpindle.transform.position = newHealthBlock.transform.position;
                    newSpindle.transform.rotation = newHealthBlock.transform.rotation;

                    // Parent the health block to the spindle
                    newHealthBlock.transform.SetParent(newSpindle.transform, false);
                    newHealthBlock.transform.localPosition = Vector3.zero;
                    newHealthBlock.transform.localRotation = Quaternion.identity;

                    // Set the properties of the new branch
                    newBranch.gameObject = newSpindle.gameObject;
                    newBranch.assembler = newAssembler;
                    newBranch.depth = branch.depth + 1;

                    // Add the new branch to the list of new branches
                    newBranches.Add(newBranch);
                    itemsSpawned++;

                    if (branch.depth >= maxDepth || branch.assembler.FullyBonded)
                    {
                        // Remove the branch from the active branches
                        branchesToRemove.Add(branch);
                    }
                }
            }

            // Remove the branches that have grown from the active branches list
            foreach (Branch branch in branchesToRemove)
            {
                activeBranches.Remove(branch);
            }

            // Add the new branches to the active branches list
            activeBranches.UnionWith(newBranches);
        }

        public override void Plant()
        {
            transform.position = node.GetCrystal().transform.position + (100 * Random.onUnitSphere);
            assembler = CreateNewAssembler();

            //if (feeds)
            //{
            //    assembler.StartBonding();
            //}
        }

        public Assembler CreateNewAssembler()
        {
            Spindle newSpindle = Instantiate(spindle, transform.position, transform.rotation, transform);
            AddSpindle(newSpindle);
            newSpindle.LifeForm = this; 

            HealthBlock newHealthBlock = Instantiate(healthBlock, transform.position, transform.rotation);
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

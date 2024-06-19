
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

        struct Branch
        {
            public GameObject gameObject;
            public int depth;
            public Assembler assembler;
        }


        private int spawnedItemCount = 0;

        public override void Grow()
        {
            if (spawnedItemCount >= maxTotalSpawnedObjects) return;

            List<Branch> newBranches = new List<Branch>();
            List<Branch> branchesToRemove = new List<Branch>();

            foreach (Branch branch in activeBranches)
            {
                if (branch.depth < maxDepth)
                {
                    Branch newBranch = new Branch();

                    HealthBlock newHealthBlock = Instantiate(healthBlock, branch.gameObject.transform.position, branch.gameObject.transform.rotation);
                    newHealthBlock.LifeForm = this;

                    newHealthBlock = (HealthBlock)branch.assembler.ProgramBlock(newHealthBlock);
                    if (newHealthBlock == null)
                    {
                        Debug.Log("Assembler returned null health block");
                        branchesToRemove.Add(branch);
                        Destroy(newHealthBlock.gameObject);
                        continue;
                    }
                    var newAssembler = newHealthBlock.GetComponent<Assembler>();

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
            assembler = CreateNewAssembler();

            if (feeds)
            {
                assembler.StartBonding();
            }
        }

        public Assembler CreateNewAssembler()
        {
            Spindle newSpindle = Instantiate(spindle, transform.position, transform.rotation, transform);
            newSpindle.LifeForm = this; 

            HealthBlock newHealthBlock = Instantiate(healthBlock, transform.position + Vector3.left*5, transform.rotation);
            newHealthBlock.transform.SetParent(newSpindle.transform, false);
            newHealthBlock.LifeForm = this;

            Assembler newAssembler = newHealthBlock.GetComponent<Assembler>();
            newAssembler.TrailBlock = newHealthBlock;
            newAssembler.Spindle = newSpindle;
            newAssembler.Depth = depth;

            activeBranches.Add(new Branch { gameObject = newSpindle.gameObject, depth = 0, assembler = newAssembler });

            return newAssembler;
        }
    }
}

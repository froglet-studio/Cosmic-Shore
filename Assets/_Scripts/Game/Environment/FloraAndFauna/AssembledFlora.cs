
using System.Collections.Generic;
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

        List<Branch> activeBranches = new List<Branch>();
        static readonly List<Branch> s_newBranches = new List<Branch>(16);
        static readonly List<int> s_removeIndices = new List<int>(16);

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

                var growthInfo = branch.assembler.GetGrowthInfo();
                if (!growthInfo.CanGrow)
                {
                    s_removeIndices.Add(i);
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

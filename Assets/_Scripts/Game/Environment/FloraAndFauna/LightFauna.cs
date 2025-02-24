using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;

namespace CosmicShore
{
    public class LightFauna : Fauna
    {
        [Header("Behavior Options")]
        [SerializeField] private List<FaunaBehaviorOption> behaviorOptions;
        [SerializeField] private LightFaunaBoidBehavior defaultBoidBehavior;

        [HideInInspector] public float Phase;

        // Expose protected healthBlock from LifeForm
        public HealthBlock HealthBlock => healthBlock;

        protected override void Start()
        {
            base.Start();

            // Initialize health block
            if (healthBlock != null)
            {
                healthBlock.Team = Team;
            }

            // Add spindle if needed
            if (spindle != null)
            {
                AddSpindle(spindle);
            }

            // Ensure we have a default boid behavior
            if (defaultBoidBehavior == null)
            {
                defaultBoidBehavior = GetComponent<LightFaunaBoidBehavior>();
                if (defaultBoidBehavior == null)
                {
                    defaultBoidBehavior = gameObject.AddComponent<LightFaunaBoidBehavior>();
                }
            }

            // Add default behavior if not in options
            if (behaviorOptions == null)
            {
                behaviorOptions = new List<FaunaBehaviorOption>();
            }
            if (!behaviorOptions.Any(opt => opt.behavior == defaultBoidBehavior))
            {
                behaviorOptions.Add(new FaunaBehaviorOption 
                { 
                    behavior = defaultBoidBehavior,
                    weight = 1.0f
                });
            }

            StartCoroutine(BehaviorSelectionLoop());
        }

        private IEnumerator BehaviorSelectionLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(3f);

                var validOptions = behaviorOptions
                    .Where(opt => opt.behavior != null && opt.behavior.CanPerform(this) && opt.weight > 0)
                    .ToList();

                if (validOptions.Count == 0)
                    continue;

                float totalWeight = validOptions.Sum(opt => opt.weight);
                float pick = Random.Range(0, totalWeight);
                FaunaBehavior chosenBehavior = null;

                foreach (var opt in validOptions)
                {
                    if (pick < opt.weight)
                    {
                        chosenBehavior = opt.behavior;
                        break;
                    }
                    pick -= opt.weight;
                }

                if (chosenBehavior != null)
                {
                    yield return StartCoroutine(chosenBehavior.Perform(this));
                    chosenBehavior.OnBehaviorEnd(this);
                }
            }
        }

        protected override void Spawn()
        {
            // Optional: Implement spawn logic if needed
        }
    }
}

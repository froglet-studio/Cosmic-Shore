using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Core;
using System.Collections;
using CosmicShore.Utility.Singleton;

namespace CosmicShore.Game.Projectiles
{
    public class TrailBlockBufferManager : SingletonPersistent<TrailBlockBufferManager>
    {
        [System.Serializable]
        private class BufferSettings
        {
            public int initializationBufferSizePerTeam = 20;
            public int bufferSizePerTeam = 100;
            public float baseInstantiateRate = 5f;
            public float maxInstantiateRate = 20f;
        }

        [SerializeField] private BufferSettings settings;
        [SerializeField] private TrailBlock trailBlockPrefab;

        protected bool Initialized = false;

        private Dictionary<Teams, Queue<TrailBlock>> teamBuffers = new Dictionary<Teams, Queue<TrailBlock>>();
        private Dictionary<Teams, float> instantiateTimers = new Dictionary<Teams, float>();

        public override void Awake()
        {
            base.Awake();
            
            if (!Instance.Initialized)
            {
                Instance.Initialized = true;
                StartCoroutine(WaitForThemeManagerInitialization());
            }
        }

        private IEnumerator WaitForThemeManagerInitialization()
        {
            yield return new WaitUntil(() => ThemeManager.Instance != null);
            
            // Initialize with a small buffer
            // Then hydrate to full size
            InitializeTeamBuffers();
            StartCoroutine(BufferMaintenanceRoutine());
        }

        private void InitializeTeamBuffers()
        {
            // Initialize buffers for each team
            foreach (Teams team in System.Enum.GetValues(typeof(Teams)))
            {
                if (team != Teams.Unassigned && team != Teams.None)
                {
                    // Don't recreate the buffers across scene loads if they already exist
                    if (!teamBuffers.ContainsKey(team))
                    {
                        teamBuffers[team] = new Queue<TrailBlock>();
                        instantiateTimers[team] = 0f;

                        // Pre-instantiate initial blocks
                        for (int i = 0; i < settings.initializationBufferSizePerTeam; i++)
                        {
                            var block = CreateBlockForTeam(team);
                            block.gameObject.SetActive(false);
                            teamBuffers[team].Enqueue(block);
                        }
                    }
                }
            }
        }

        private TrailBlock CreateBlockForTeam(Teams team)
        {
            var block = Instantiate(trailBlockPrefab);
            block.transform.parent = transform;
            block.ChangeTeam(team);
            var renderer = block.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = ThemeManager.Instance.GetTeamBlockMaterial(team);
            }
            return block;
        }

        private IEnumerator BufferMaintenanceRoutine()
        {
            while (true)
            {
                foreach (var team in teamBuffers.Keys)
                {
                    var buffer = teamBuffers[team];
                    if (buffer.Count < settings.bufferSizePerTeam)
                    {
                        float bufferFullness = (float)buffer.Count / settings.bufferSizePerTeam;
                        float currentInstantiateRate = Mathf.Lerp(settings.maxInstantiateRate, settings.baseInstantiateRate, bufferFullness);
                        float instantiateInterval = 1f / currentInstantiateRate;

                        instantiateTimers[team] += Time.deltaTime;

                        while (instantiateTimers[team] >= instantiateInterval && buffer.Count < settings.bufferSizePerTeam)
                        {
                            var block = CreateBlockForTeam(team);
                            block.gameObject.SetActive(false);
                            buffer.Enqueue(block);
                            instantiateTimers[team] -= instantiateInterval;
                        }
                    }
                }

                yield return null;
            }
        }

        public TrailBlock GetBlock(Teams team)
        {
            if (!teamBuffers.ContainsKey(team))
            {
                Debug.LogError($"No buffer exists for team {team}");
                return CreateBlockForTeam(team);
            }

            var buffer = teamBuffers[team];
            if (buffer.Count > 0)
            {
                var block = buffer.Dequeue();
                block.gameObject.SetActive(true);
                return block;
            }
            
            Debug.LogWarning($"Buffer depleted for team {team}! Falling back to direct instantiation.");
            return CreateBlockForTeam(team);
        }

        public bool HasAvailableBlocks(Teams team, int count)
        {
            return teamBuffers.ContainsKey(team) && teamBuffers[team].Count >= count;
        }

        protected void OnDestroy()
        {
            if (teamBuffers != null)
            {
                foreach (var buffer in teamBuffers.Values)
                {
                    while (buffer.Count > 0)
                    {
                        var block = buffer.Dequeue();
                        if (block != null)
                        {
                            Destroy(block.gameObject);
                        }
                    }
                }
            }
        }
    }
}

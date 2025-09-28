using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Core;
using System.Collections;
using CosmicShore.Utilities;
using UnityEngine.Rendering;
using UnityEngine.Serialization;


namespace CosmicShore.Game.Projectiles
{
    public class TrailBlockBufferManager : Singleton<TrailBlockBufferManager>
    {
        [System.Serializable]
        private class BufferSettings
        {
            public int bufferSizePerTeam = 100;
            public float baseInstantiateRate = 5f;
            public float maxInstantiateRate = 20f;
        }

        [SerializeField] private BufferSettings settings;
        [FormerlySerializedAs("trailBlockPrefab")] [SerializeField] private Prism prismPrefab;

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        private Dictionary<Domains, Queue<Prism>> teamBuffers = new Dictionary<Domains, Queue<Prism>>();
        private Dictionary<Domains, float> instantiateTimers = new Dictionary<Domains, float>();

        private void Start()
        {
            InitializeTeamBuffers();
            StartCoroutine(BufferMaintenanceRoutine());
        }

        private void InitializeTeamBuffers()
        {
            // Initialize buffers for each team
            foreach (Domains team in System.Enum.GetValues(typeof(Domains)))
            {
                if (team != Domains.Unassigned && team != Domains.None)
                {
                    teamBuffers[team] = new Queue<Prism>();
                    instantiateTimers[team] = 0f;
                    
                    // Pre-instantiate initial blocks
                    for (int i = 0; i < settings.bufferSizePerTeam; i++)
                    {
                        var block = CreateBlockForTeam(team);
                        block.gameObject.SetActive(false);
                        teamBuffers[team].Enqueue(block);
                    }
                }
            }
        }

        private Prism CreateBlockForTeam(Domains domain)
        {
            var block = Instantiate(prismPrefab);
            block.transform.parent = transform;

            block.ChangeTeam(domain);
            var renderer = block.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = _themeManagerData.GetTeamBlockMaterial(domain);
            }
            block.Initialize();
            return block;
        }

        private IEnumerator BufferMaintenanceRoutine()
        {
            while (true)
            {
                foreach (var team in teamBuffers.Keys)
                {
                    var buffer = teamBuffers[team];
                    float bufferFullness = (float)buffer.Count / settings.bufferSizePerTeam;
                    float currentInstantiateRate = Mathf.Lerp(settings.maxInstantiateRate, settings.baseInstantiateRate, bufferFullness);
                    
                    if (buffer.Count < settings.bufferSizePerTeam)
                    {
                        instantiateTimers[team] += Time.deltaTime;
                        float instantiateInterval = 1f / currentInstantiateRate;

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

        public Prism GetBlock(Domains domain)
        {
            if (!teamBuffers.ContainsKey(domain))
            {
                Debug.LogError($"No buffer exists for team {domain}");
                return CreateBlockForTeam(domain);
            }

            var buffer = teamBuffers[domain];
            if (buffer.Count > 0)
            {
                var block = buffer.Dequeue();
                block.gameObject.SetActive(true);
                return block;
            }
            
            Debug.LogWarning($"Buffer depleted for team {domain}! Falling back to direct instantiation.");
            return CreateBlockForTeam(domain);
        }

        public bool HasAvailableBlocks(Domains domain, int count)
        {
            return teamBuffers.ContainsKey(domain) && teamBuffers[domain].Count >= count;
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

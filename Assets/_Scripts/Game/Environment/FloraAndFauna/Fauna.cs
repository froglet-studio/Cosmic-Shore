using System.Collections;
using CosmicShore.Game;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore
{
    public abstract class Fauna : MonoBehaviour, ITeamAssignable
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;

        [SerializeField] protected CellRuntimeDataSO cellData;
        [FormerlySerializedAs("Team")] public Domains domain;
        [SerializeField] float goalUpdateInterval = 5f;

        [Header("Rabid State")]
        [Tooltip("Own-domain prism count above which this fauna goes rabid and consumes same-domain mass.")]
        [SerializeField] int rabidPrismThreshold = 50;

        public Vector3 Goal;
        public bool IsRabid { get; private set; }

        protected Cell cell => cellData.Cell;

        protected virtual void Start()
        {
            if (domain == Domains.Unassigned)
                Debug.LogWarning($"{name}: Population domain is Unassigned. Assign it before spawning FaunaPrefab, or set it on the prefab.");

            StartCoroutine(UpdateGoal());
        }

        public abstract void Initialize(Cell cell);
        
        protected abstract void Spawn();

        protected abstract void Die(string killername = "");

        void CalculateTeamWeights()
        {
            Vector4 teamVolumes = gameData.GetTeamVolumes(); // StatsManager.Instance.GetTeamVolumes();
            float totalVolume = gameData.GetTotalVolume();

            //Weights = new List<float>
            //{
            //totalVolume / (teamVolumes.x + 1), // +1 to avoid division by zero
            //totalVolume / (teamVolumes.y + 1),
            //totalVolume / (teamVolumes.z + 1),
            //totalVolume / (teamVolumes.w + 1)
            //};
        }

        IEnumerator UpdateGoal()
        {
            while (true)
            {
                yield return new WaitForSeconds(goalUpdateInterval);

                if (!cell) continue;

                IsRabid = cell.GetPrismCount(domain) > rabidPrismThreshold;

                // Rabid fauna seek dense clusters of their own domain's mass; otherwise hunt hostile prisms.
                Goal = IsRabid ? cell.GetSelfDomainTarget(domain) : cell.GetExplosionTarget(domain);
            }
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void SetTeam(Domains domain)
        {
            this.domain = domain;
        }
    }
}
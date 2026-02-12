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
        public Vector3 Goal;
        //public List<float> Weights;
        protected Cell cell;
        
        protected virtual void Start()
        {
            cell = CellControlManager.Instance.GetNearestCell(transform.position);

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
                Vector3 highDensityPosition = cell.GetExplosionTarget(domain);
                Goal = highDensityPosition;
                //CalculateTeamWeights();
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
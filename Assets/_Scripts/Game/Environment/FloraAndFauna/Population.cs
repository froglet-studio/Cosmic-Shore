using CosmicShore.Core;
using System.Collections;
using CosmicShore.Game;
using CosmicShore.SOAP;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore
{
    public abstract class Population : MonoBehaviour, ITeamAssignable
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;
        
        [FormerlySerializedAs("Team")] public Domains domain;
        [SerializeField] float goalUpdateInterval = 5f;
        public Vector3 Goal;
        //public List<float> Weights;
        protected Cell cell;


        protected virtual void Start()
        {
            cell = CellControlManager.Instance.GetNearestCell(transform.position);
            StartCoroutine(UpdateGoal());
        }

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
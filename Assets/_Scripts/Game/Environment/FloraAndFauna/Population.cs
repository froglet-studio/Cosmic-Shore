using CosmicShore.Core;
using System.Collections;
using CosmicShore.Game;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore
{
    public abstract class Population : MonoBehaviour, ITeamAssignable
    {
        [SerializeField]
        MiniGameDataSO miniGameData;
        
        public Teams Team;
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
            Vector4 teamVolumes = miniGameData.GetTeamVolumes(); // StatsManager.Instance.GetTeamVolumes();
            float totalVolume = miniGameData.GetTotalVolume();

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
                Vector3 highDensityPosition = cell.GetExplosionTarget(Team);
                Goal = highDensityPosition;
                //CalculateTeamWeights();
            }
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void SetTeam(Teams team)
        {
            Team = team;
        }
    }
}
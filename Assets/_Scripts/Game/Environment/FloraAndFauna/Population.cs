using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public abstract class Population : MonoBehaviour
    {
        public Teams Team;
        [SerializeField] float goalUpdateInterval = 5f;
        public Vector3 Goal;
        //public List<float> Weights;
        protected Node node;


        protected virtual void Start()
        {
            node = NodeControlManager.Instance.GetNearestNode(transform.position);
            StartCoroutine(UpdateGoal());
        }

        void CalculateTeamWeights()
        {
            Vector4 teamVolumes = StatsManager.Instance.GetTeamVolumes();
            float totalVolume = teamVolumes.x + teamVolumes.y + teamVolumes.z + teamVolumes.w;

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
                //Vector3 highDensityPosition = node.GetExplosionTarget(Team);
                //Goal = highDensityPosition;
                //CalculateTeamWeights();
            }
        }

    }
}

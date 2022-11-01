using UnityEngine;

/// <summary>
/// Chase, Flee (Random Direction for Time), Patrol, None(aka Stay Put)
/// </summary>
namespace StarWriter.Core.NPC.Control
{
    public class AIControllerShip : MonoBehaviour
    {
        [SerializeField]
        private float speed = 10f;
        [SerializeField]
        private float distance;
        [SerializeField]
        private float maxChaseDistance = 20f;
        [SerializeField]
        private float minChaseDistance = 8f;

        [SerializeField]
        private GameObject target; // ref to actual player ship model TODO set ref to ship model holder and search for transform with controller attached

        void Start()
        {
            target = GameObject.FindGameObjectWithTag("Player_Ship");  // Ship Model must be tagged
        }

        void Update()
        {
            if (PauseSystem.Paused)
                return;

            distance = Vector3.Distance(target.transform.position, transform.position);

            if (distance >= maxChaseDistance)
                Chase();
            if (distance <= minChaseDistance)
                Flee();
        }

        public void Chase()
        {
            //Debug.Log("Chasing");
            transform.LookAt(target.transform);
            transform.position += speed * Vector3.forward * Time.deltaTime;
        }

        public void Flee()
        {
            //Debug.Log("Fleeing");
            Vector3 fleeDirection = transform.position - target.transform.position;
            transform.LookAt(fleeDirection);    
            transform.position += speed * Vector3.forward * Time.deltaTime;
        }
    }
}
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
        private float maxChaseDistance = 20f;
        [SerializeField]
        private float minChaseDistance = 8f;


        [SerializeField]
        private Transform target; // ref to player

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            float distance = Vector3.Distance(target.position, transform.position);

            if (distance >= maxChaseDistance)
            {
                Chase();
            }
            if (distance <= minChaseDistance)
            {
                Flee();
            }
        }

        public void Chase()
        {
            Debug.Log("Chasing");
            transform.LookAt(target);
            transform.position += speed * Vector3.forward * Time.deltaTime;
        }

        public void Flee()
        {
            Debug.Log("Fleeing");
            transform.LookAt(target);
            transform.position += speed * -Vector3.forward * Time.deltaTime;
        }
    }
}



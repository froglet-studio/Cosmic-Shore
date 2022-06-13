using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.Tutorial
{
    public class TutorialJailBlockWall : MonoBehaviour
    {
        [SerializeField]
        TutorialManager tutorialManager;

        [SerializeField]
        private GameObject player;

        public Vector3 spawnPointOffset;

        private int tutorialStageIndex = 0;

        private float distance = 10f;

        private string stageName;

        List<Collision> collisions;

        private void OnEnable()
        {
            TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
        }

        private void OnDisable()
        {
            TutorialManager.Instance.OnTutorialIndexChange -= OnIndexChange;
        }

        private void OnIndexChange(int idx)
        {
            tutorialStageIndex = idx;
        }

        // Start is called before the first frame update
        void Start()
        {
            //tutorialManager = TutorialManager.Instance;
            MoveJailBlockWall();
            tutorialStageIndex = tutorialManager.Index;
            collisions = new List<Collision>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            collisions.Add(collision);

        }

        private void Update()
        {
            if (collisions.Count > 0)
            {
                Collide(collisions[0].collider);
                collisions.Clear();
            }

            if (!tutorialManager.tutorialStages[tutorialStageIndex].HasAnotherAttempt)
            {
                if (distance >= (Vector3.Distance(player.transform.position, transform.position)))
                {
                    //if(tutorialManager.tutorialStages[tutorialStageIndex].)
                    MoveJailBlockWall();
                }
            }
        }
        /// <summary>
        /// Collision occurs only by passing thru the bars opening
        /// </summary>
        /// <param name="other"></param>
        void Collide(Collider other)
        {
            stageName = tutorialManager.tutorialStages[tutorialStageIndex].StageName;
            tutorialManager.TutorialTests[stageName] = true;

            gameObject.SetActive(false);

        }
        void MoveJailBlockWall()
        {

            transform.position = player.transform.position +
                                 player.transform.right * spawnPointOffset.x +
                                 player.transform.up * spawnPointOffset.y +
                                 player.transform.forward * spawnPointOffset.z;
        }
    }
}
                


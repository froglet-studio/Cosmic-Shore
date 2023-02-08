using UnityEngine;

namespace StarWriter.Core.AI
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;
        [SerializeField] Player player;

        [SerializeField] float gunnerSpeed = 5;
        [SerializeField] float rotationSpeed = 40;
        [SerializeField] int padding = 3;

        public Teams Team;
        public Ship Ship;
        TrailSpawner trailSpawner;
        
        int nextBlockIndex = 1;
        int previousBlockIndex;
        float lerpAmount;
        bool moveForward = true;
        
        private void Start()
        {
            trailSpawner = player.Ship.TrailSpawner;
            Team = player.Team;
            Ship = player.Ship;
            gun.Team = Team;
            gun.Ship = Ship;
        }

        void Update()
        {
            // Give the ships a small head start so some blocks exist
            if (trailSpawner.trailList.Count < padding + 1)
                return;

            if (trailSpawner.trailList[(int)nextBlockIndex].destroyed) 
                lerpAmount += gunnerSpeed / 4f * Time.deltaTime;
            else 
                lerpAmount += gunnerSpeed * Time.deltaTime;

            // reached end of segment, move to next block
            if (lerpAmount > 1)
            {
                previousBlockIndex = moveForward ? nextBlockIndex++ : nextBlockIndex--;
                lerpAmount -= 1f;
            }

            // reached end of trail, reverse direction
            if (nextBlockIndex > trailSpawner.trailList.Count - padding)
                moveForward = false;

            // reached beginning of trail, reverse direction
            if (nextBlockIndex < padding)
                moveForward = true;

            transform.position = Vector3.Lerp(trailSpawner.trailList[previousBlockIndex].transform.position,
                                              trailSpawner.trailList[nextBlockIndex].transform.position,
                                              lerpAmount);

            transform.rotation = Quaternion.Lerp(trailSpawner.trailList[previousBlockIndex].transform.rotation,
                                                 trailSpawner.trailList[nextBlockIndex].transform.rotation,
                                                 lerpAmount);
            transform.Rotate(90, 0, 0);

            if (trailSpawner.trailList[(int)previousBlockIndex].destroyed)
                trailSpawner.trailList[(int)previousBlockIndex].Restore();

            gunMount.transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            gun.FireGun(player.transform, gunnerSpeed*transform.forward);
        }
    }
}
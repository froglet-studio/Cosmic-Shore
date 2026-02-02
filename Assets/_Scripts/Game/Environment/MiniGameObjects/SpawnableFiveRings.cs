using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;

public class SpawnableFiveRings : SpawnableAbstractBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
    [SerializeField] int blocksPerRing = 12;
    [SerializeField] float ringRadius = 10f;
    [SerializeField] Vector3 scale = new Vector3(4, 4, 9);
    static int ObjectsSpawned = 0;

    public override GameObject Spawn()
    {
        //ringRadius = 
        GameObject container = new GameObject();
        container.name = "FiveRings" + ObjectsSpawned++;

        // The shared point will be at the transform position
        Vector3 sharedPoint = transform.position;

        // Define central axis for five-fold symmetry
        Vector3 centralAxis = transform.forward;

        // Angle for five-fold symmetry
        float angleStep = 360f / 5; // 72 degrees

        // Dihedral angle for the planes
        //float dihedralAngle = 36f;

        // Create the five rings
        for (int ringIndex = 0; ringIndex < 5; ringIndex++)
        {
            // Calculate the rotation around the central axis
            float rotationAngle = ringIndex * angleStep;

            // Create a base vector in XZ plane
            Vector3 baseVector = Mathf.Cos(Mathf.Deg2Rad * rotationAngle)*transform.right + Mathf.Sin(Mathf.Deg2Rad * rotationAngle) * transform.up;

            // Create rotation axis perpendicular to baseVector and centralAxis
            Vector3 planeNormal = Vector3.Cross(baseVector, centralAxis).normalized;

            // Calculate the normal vector for the ring's plane by tilting from vertical
            //Vector3 planeNormal = Quaternion.AngleAxis(dihedralAngle, rotationAxis) * centralAxis;

            // Calculate the center of the ring
            // Ring center is exactly 1 radius away from shared point
            Vector3 ringCenter = sharedPoint + ringRadius * baseVector;

            // Create the ring
            CreateRing(container, ringIndex, sharedPoint, ringCenter, planeNormal);
        }

        return container;
    }

    private void CreateRing(GameObject container, int ringIndex, Vector3 sharedPoint,
                           Vector3 ringCenter, Vector3 planeNormal)
    {
        Trail trail = new Trail(true); // Creating a closed loop trail
        trails.Add(trail);

        // Vector from ring center to shared point (this is in the ring's plane)
        Vector3 toSharedPoint = (sharedPoint - ringCenter).normalized;

        // Vector perpendicular to both the normal and the toSharedPoint vector
        // This gives us the second basis vector in the ring's plane
        Vector3 perpVector = Vector3.Cross(planeNormal, toSharedPoint).normalized;

        // Create blocks around the ring
        for (int block = 0; block < blocksPerRing; block++)
        {
            
            // Calculate angle for this block - start at 0 so first point is at shared point
            float angle = (float)block / blocksPerRing * Mathf.PI / 2;

            if (Mathf.Cos(angle) > .8) continue;

            // Calculate position on the ring using parametric equation of a circle
            Vector3 position = ringCenter +
                            ringRadius * Mathf.Cos(angle) * toSharedPoint +
                            ringRadius * Mathf.Sin(angle) * perpVector;

            // For the look direction, use the next point
            int nextBlock = (block - 1) % blocksPerRing;
            float nextAngle = (float)nextBlock / blocksPerRing * Mathf.PI * 2;

            Vector3 nextPosition = ringCenter +
                                  ringRadius * Mathf.Cos(nextAngle) * toSharedPoint +
                                  ringRadius * Mathf.Sin(nextAngle) * perpVector;

            // Create the block
            CreateBlock(position, nextPosition,
                        container.name + "::RING" + ringIndex + "::BLOCK::" + block,
                        trail, scale, prism, container);
        }
    }

    public override GameObject Spawn(int intensityLevel)
    {
        // Modify properties based on intensity level
        ringRadius = 150 + intensityLevel * 5;
        blocksPerRing = 25 + intensityLevel * 7;
        return Spawn();
    }
}
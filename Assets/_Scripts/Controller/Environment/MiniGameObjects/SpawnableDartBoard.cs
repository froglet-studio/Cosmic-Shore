using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    public class SpawnableDartBoard : SpawnableBase
    {
        [FormerlySerializedAs("greenTrailBlock")] [SerializeField] Prism greenPrism;
        [FormerlySerializedAs("redTrailBlock")] [SerializeField] Prism redPrism;
        [SerializeField] int blockCount = 6;
        [SerializeField] int ringCount = 30;
        [SerializeField] float ringThickness = 5f;
        [SerializeField] float gap = 6;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new SpawnTrailData[ringCount];

            for (int ring = 1; ring <= ringCount; ring++)
            {
                int blocksInRing = blockCount * ring;
                var points = new SpawnPoint[blocksInRing];

                for (int block = 0; block < blocksInRing; block++)
                {
                    float phase = 0;
                    float tilt = 0;
                    float sweep = 0;

                    var position = ring * ringThickness * Mathf.Cos(((block + phase) / (blockCount * ring)) * 2 * Mathf.PI) * Vector3.right +
                                   ring * ringThickness * Mathf.Sin(((block + phase) / (blockCount * ring)) * 2 * Mathf.PI) * Vector3.up +
                                   sweep * ringThickness * Vector3.forward;

                    // Look direction: in old code, SafeLookRotation.TryGet(lookPosition - transform.position, transform.forward, ...)
                    // lookPosition = position + tilt * ringThickness * transform.forward
                    // lookPosition - transform.position = (position - transform.position) + tilt * ringThickness * forward
                    // In local space (origin at 0,0,0): position + tilt * ringThickness * forward
                    // With tilt = 0: just position
                    var lookDirection = position + tilt * ringThickness * Vector3.forward;
                    var rotation = SpawnPoint.LookRotation(lookDirection, Vector3.forward);

                    var blockScale = new Vector3(
                        ((Mathf.PI / 3f) * ringThickness) - (gap / (2 * ring)),  // blockwidth
                        (ringCount - ring) * ringThickness / 3f,                  // dartboard thickness
                        ringThickness - (gap / 5f));                              // annulus thickness

                    points[block] = new SpawnPoint(position, rotation, blockScale);
                }

                trailDataList[ring - 1] = new SpawnTrailData(points, false, domain);
            }

            return trailDataList;
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            for (int ring = 1; ring <= ringCount; ring++)
            {
                var td = trailData[ring - 1];
                var trail = new Trail();
                trails.Add(trail);

                int blocksInRing = blockCount * ring;
                for (int block = 0; block < blocksInRing; block++)
                {
                    Prism prism;
                    Domains blockDomain;
                    if ((block / ring + ring / 3) % 2 == 0)
                    {
                        prism = greenPrism;
                        blockDomain = Domains.Jade;
                    }
                    else
                    {
                        prism = redPrism;
                        blockDomain = Domains.Ruby;
                    }

                    var point = td.Points[block];
                    var blockObj = Instantiate(prism, container.transform);
                    blockObj.ChangeTeam(blockDomain);
                    blockObj.ownerID = $"{container.name}::{ring}::{block}";
                    blockObj.transform.localPosition = point.Position;
                    blockObj.transform.localRotation = point.Rotation;
                    blockObj.TargetScale = point.Scale;
                    blockObj.Trail = trail;
                    blockObj.Initialize();
                    trail.Add(blockObj);
                }
            }
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed, blockCount, ringCount, ringThickness, gap);
        }
    }
}

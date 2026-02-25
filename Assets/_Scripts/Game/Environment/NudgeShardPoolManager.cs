using CosmicShore.Utility.PoolsAndBuffers;
using UnityEngine;
using CosmicShore.Game.Environment.Cytoplasm;
namespace CosmicShore.Game.Environment
{
    public class NudgeShardPoolManager : GenericPoolManager<NudgeShard>
    {
        public override NudgeShard Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true) => Get_(position, rotation, parent);
        public override void Release(NudgeShard instance) =>  Release_(instance);
    }
}

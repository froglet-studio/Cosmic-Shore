using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ZeroWarpData", menuName = "TailGlider/Warp/ZeroWarp", order = 10)]
[System.Serializable] public class ZeroWarp : WarpFieldSO
{

    public ZeroWarp()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 700;
        fieldMax = .7f;
    }

    override public Vector3 HybridVector(Transform node)
    {
        return Vector3.zero;
    }
}

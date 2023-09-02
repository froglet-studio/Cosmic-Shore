using UnityEngine;

[CreateAssetMenu(fileName = "TardisWarpData", menuName = "CosmicShore/Warp/TardisWarp", order = 30)]
[System.Serializable] public class TardisWarp : WarpFieldSO
{
    [SerializeField] float minRadius = 10;

    public TardisWarp()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 200;
        fieldMax = 500f;
    }

    override public Vector3 HybridVector(Transform node)
    {
        return new Vector3(sigmoid(node.position.x),
                           sigmoid(node.position.y),
                           sigmoid(node.position.z));
    }

    float sigmoid(float position)
    {
        if (position > 0) return 3 * Mathf.Atan(position - minRadius) / fieldMax;
        else return 3 * Mathf.Atan(position + minRadius) / fieldMax;
    }
}
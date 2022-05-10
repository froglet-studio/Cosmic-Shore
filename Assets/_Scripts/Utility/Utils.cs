using UnityEngine;

public class Utils : MonoBehaviour
{
    public static Vector3 ScreenToWorld2D(Camera camera, Vector3 position)
    {
        position.z = camera.nearClipPlane;
        Debug.Log("Returned Position " + position);
        return camera.ScreenToWorldPoint(position);
    }

    public static Vector3 ScreenToWorld3D(Camera camera, Vector3 position)
    {
        //position.z = camera.nearClipPlane;
        // camera.ScreenToWorldPoint(position);

        //TODO with camera.ScreenPointToRay
        return Vector3.zero;
    }

    public static bool Flip(bool value)
    {
        return !value;
    }
}

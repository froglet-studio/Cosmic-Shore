using UnityEngine;

[System.Serializable]
public class ShipData : MonoBehaviour
{
    public float inputSpeed = 1;
    public float speedMultiplier = 1;
    public float speed;

    public bool boost;
    public Vector3 velocityDirection;
    public Quaternion blockRotation;

    

    private void Update()
    {
        if (speedMultiplier < 0) speedMultiplier = 0;
        speed = inputSpeed * speedMultiplier;
    }
}